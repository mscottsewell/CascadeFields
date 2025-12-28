using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CascadeFields.Configurator.Dialogs;
using CascadeFields.Configurator.Models;
using CascadeFields.Configurator.Services;
using CascadeFields.Plugin.Models;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Messages;
using Newtonsoft.Json;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;
using CascadeConfigurationModel = CascadeFields.Plugin.Models.CascadeConfiguration;

namespace CascadeFields.Configurator.Controls
{
    public partial class CascadeFieldsConfiguratorControl : PluginControlBase
    {
        private readonly BindingList<MappingRow> _mappingRows = new();
        // NEW: Store mapping rows for additional child entity tabs (beyond the first/selected one)
        private readonly Dictionary<string, BindingList<MappingRow>> _additionalChildMappings = new(StringComparer.OrdinalIgnoreCase);
        // NEW: Store relationship info per child entity tab
        private readonly Dictionary<string, RelationshipItem> _childRelationships = new(StringComparer.OrdinalIgnoreCase);
        // NEW: Store filter criteria per child entity tab
        private readonly Dictionary<string, List<SavedFilterCriteria>> _filterCriteriaPerTab = new(StringComparer.OrdinalIgnoreCase);
        
        private readonly Dictionary<string, List<EntityMetadata>> _solutionEntityCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TabPage> _childEntityTabs = new(StringComparer.OrdinalIgnoreCase);

        private TabPage? _previouslySelectedTab;  // Track previous tab for saving filters before switch
        private ConfiguratorSettings _settings = new();
        private SessionSettings? _session;
        private bool _isRestoringSession; // legacy flag, used for guarded save/restore
        private bool _isApplyingSession;  // prevents duplicate restore/logging across event cascades
        private List<SolutionItem> _solutions = new();
        private List<EntityMetadata> _currentEntities = new();
        private List<AttributeItem> _parentAttributes = new();
        private List<AttributeItem> _childAttributes = new();
        private List<RelationshipItem> _availableRelationships = new();
        private RelationshipItem? _selectedRelationship;

        private MetadataService? _metadataService;
        private ConfigurationService? _configurationService;

        // Event handlers stored as fields for proper attach/detach
        private EventHandler? _solutionChangedHandler;
        private EventHandler? _parentEntityChangedHandler;

        // Optional: link to repository, can be surfaced via About dialog
        private string RepositoryName => "mscottsewell/CascadeFields";

        public CascadeFieldsConfiguratorControl()
        {
            InitializeComponent();
            InitializeBehavior();
        }

        #region Initialization

        private void InitializeBehavior()
        {
            _mappingRows.ListChanged += (s, e) => UpdateJsonPreview();

            gridMappings.DataSource = _mappingRows;
            gridMappings.CellValueChanged += GridMappings_CellValueChanged;
            gridMappings.EditingControlShowing += GridMappings_EditingControlShowing;
            gridMappings.CellClick += GridMappings_CellClick;
            gridMappings.CellContentClick += GridMappings_CellContentClick;
            gridMappings.CurrentCellDirtyStateChanged += GridMappings_CurrentCellDirtyStateChanged;
            gridMappings.DataError += (s, e) => { e.ThrowException = false; };
            gridMappings.UserDeletedRow += (s, e) => UpdateJsonPreview();
            gridMappings.RowsAdded += GridMappings_RowsAdded;
            
            // Add initial blank row
            if (_mappingRows.Count == 0)
            {
                _mappingRows.Add(new MappingRow());
            }

            // Wire up filter control events - save current tab's filters when changed
            filterControl.FilterChanged += (s, e) => SaveCurrentTabFilters();
            chkEnableTracing.CheckedChanged += (s, e) => UpdateJsonPreview();

            // Wire up tab control events for per-tab filter management
            tabControlRightUpper.Selected += TabControlRightUpper_Selected;
            tabControlRightUpper.MouseDown += TabControlRightUpper_MouseDown;  // For close button

            // Keep per-tab filters in sync whenever the filter control changes
            filterControl.FilterChanged += (s, e) => SaveCurrentTabFilters();

            btnLoadMetadata.Click += async (s, e) => await LoadSolutionsAsync();
            btnAddChildRelationship.Click += async (s, e) => await OnAddChildRelationshipAsync();
            btnRetrieveConfigured.Click += async (s, e) => await RetrieveConfiguredAsync();
            btnUpdatePlugin.Click += async (s, e) => await UpdatePluginAssemblyAsync();
            btnPublish.Click += async (s, e) => await PublishConfigurationAsync();
            btnClearSession.Click += (s, e) => ClearSession();

            // Store event handlers as fields for proper detach/reattach
            _solutionChangedHandler = async (s, e) => await OnSolutionChangedAsync();
            _parentEntityChangedHandler = async (s, e) => await OnParentEntityChangedAsync();

            cmbSolution.SelectedIndexChanged += _solutionChangedHandler;
            cmbParentEntity.SelectedIndexChanged += _parentEntityChangedHandler;
        }

        private void EnsureServices()
        {
            if (Service != null)
            {
                _metadataService ??= new MetadataService(Service);
                _configurationService ??= new ConfigurationService(Service);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            
            // Set initial split proportions
            if (splitContainerMain.Width > 0)
            {
                splitContainerMain.SplitterDistance = splitContainerMain.Width / 2;
            }
            if (splitContainerLeft.Height > 0)
            {
                splitContainerLeft.SplitterDistance = (int)(splitContainerLeft.Height * 0.3);
            }
            if (splitContainerRight.Height > 0)
            {
                splitContainerRight.SplitterDistance = (int)(splitContainerRight.Height * 0.7);
            }
            
            LoadSettings();

            // If already connected on load, pre-load solutions; otherwise wait for user.
            if (Service != null && ConnectionDetail != null)
            {
                _ = LoadSolutionsAsync();
            }
            else
            {
                // Ensure all controls start disabled when not connected
                cmbSolution.DataSource = null;
                cmbSolution.Enabled = false;
                cmbParentEntity.DataSource = null;
                cmbParentEntity.Enabled = false;
            }

            AppendLog("Ready. Connect to Dataverse and click 'Load Metadata'.");
        }

        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail? detail, string actionName = "", object? parameter = null)
        {
            // Save current session before switching
            if (_session != null)
            {
                SaveSettings();
                AppendLog("Saved current session before connection change.");
            }

            // Clear UI
            ClearUIForConnectionChange();

            // Update connection
            base.UpdateConnection(newService, detail, actionName, parameter);

            // Load settings for new connection
            RefreshSession();
            AppendLog($"Switched to connection: {detail?.Organization ?? "unknown"}");

            // If we have a saved session for this connection, load solutions
            if (_session != null && !string.IsNullOrWhiteSpace(_session.SolutionUniqueName))
            {
                _ = LoadSolutionsAsync();
            }
        }

        private void ClearUIForConnectionChange()
        {
            AppendLog("Clearing UI for connection change...");

            // Clear solutions
            _solutions.Clear();
            cmbSolution.DataSource = null;
            cmbSolution.Enabled = false;

            // Clear entities and cached solution entity data
            _currentEntities.Clear();
            _solutionEntityCache.Clear();
            cmbParentEntity.DataSource = null;
            cmbParentEntity.Enabled = false;

            // Clear relationships
            _selectedRelationship = null;

            // Clear attributes
            _parentAttributes.Clear();
            _childAttributes.Clear();

            // Clear mappings
            _mappingRows.Clear();
            _mappingRows.Add(new MappingRow());

            // Clear JSON preview
            txtJsonPreview.Text = string.Empty;
            
            // Clear filters
            filterControl.ClearFilters();
            chkEnableTracing.Checked = true;
        }


        private void LoadSettings()
        {
            _settings = Services.SettingsStorage.Load() ?? new ConfiguratorSettings();
            AppendLog($"Settings loaded: {_settings.Sessions.Count} session(s) found.");
            RefreshSession();
        }

        private void SaveSettings()
        {
            // Save solution and configuration JSON
            if (_session != null)
            {
                _session.ConfigurationJson = txtJsonPreview.Text;
            }
            Services.SettingsStorage.Save(_settings);
        }

        private void RefreshSession()
        {
            var connectionKey = GetConnectionKey();
            if (string.IsNullOrWhiteSpace(connectionKey) || connectionKey == "default")
            {
                AppendLog("No active connection - session not loaded.");
                return;
            }

            _session = _settings.GetOrCreateSession(connectionKey);
            AppendLog($"Session key: {connectionKey}");
            if (!string.IsNullOrWhiteSpace(_session.SolutionUniqueName))
            {
                AppendLog($"Session found - Solution: {_session.SolutionUniqueName}");
            }
        }

        private string GetConnectionKey()
        {
            if (ConnectionDetail != null)
            {
                // Use organization unique name and URL for stable key across sessions
                var key = $"{ConnectionDetail.Organization}_{ConnectionDetail.OrganizationUrlName}";
                return key.ToLowerInvariant();
            }
            return "default";
        }

        #endregion

        #region Event handlers

        private async System.Threading.Tasks.Task LoadSolutionsAsync()
        {
            if (!EnsureConnected()) return;
            EnsureServices();
            
            if (_metadataService == null)
            {
                AppendLog("Error: Metadata service is not available. Please reconnect to Dataverse.");
                MessageBox.Show("Metadata service is not available. Please reconnect to Dataverse.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Clear cached entity data to force fresh load
            _solutionEntityCache.Clear();
            AppendLog("Cleared cached solution entity metadata.");

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading solutions and metadata...",
                Work = (worker, args) =>
                {
                    var solutions = _metadataService!.GetUnmanagedSolutionsAsync().GetAwaiter().GetResult();
                    args.Result = solutions;
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        AppendLog($"Failed to load solutions: {args.Error.Message}");
                        return;
                    }

                    _solutions = args.Result as List<SolutionItem> ?? new List<SolutionItem>();

                    // Apply session only if we actually have a saved solution
                    _isApplyingSession = !string.IsNullOrWhiteSpace(_session?.SolutionUniqueName);
                    _isRestoringSession = _isApplyingSession;
                    BindSolutions();

                    AppendLog($"Loaded {_solutions.Count} unmanaged solutions.");
                }
            });

            await Task.CompletedTask;
        }

        private void BindSolutions()
        {
            // Temporarily remove handler to prevent events during binding
            if (_solutionChangedHandler != null)
            {
                cmbSolution.SelectedIndexChanged -= _solutionChangedHandler;
            }
            
            cmbSolution.DataSource = null;
            cmbSolution.DataSource = _solutions;
            cmbSolution.DisplayMember = nameof(SolutionItem.FriendlyName);
            cmbSolution.ValueMember = nameof(SolutionItem.Id);
            cmbSolution.Enabled = true;

            if (_session != null && !string.IsNullOrWhiteSpace(_session.SolutionUniqueName))
            {
                var saved = _solutions.FirstOrDefault(s => s.UniqueName.Equals(_session.SolutionUniqueName, StringComparison.OrdinalIgnoreCase));
                if (saved != null)
                {
                    AppendLog($"Restoring saved solution: {saved.FriendlyName}");
                    cmbSolution.SelectedItem = saved;
                    // Re-attach handler and manually trigger the change to load entities
                    if (_solutionChangedHandler != null)
                    {
                        cmbSolution.SelectedIndexChanged += _solutionChangedHandler;
                    }
                    _ = OnSolutionChangedAsync();
                    return;
                }
            }

            // Re-attach handler
            if (_solutionChangedHandler != null)
            {
                cmbSolution.SelectedIndexChanged += _solutionChangedHandler;
            }

            // No default selection when no saved session; wait for user.
            cmbSolution.SelectedIndex = -1;
            cmbParentEntity.DataSource = null;
            cmbParentEntity.Enabled = false;
        }

        private async System.Threading.Tasks.Task OnSolutionChangedAsync()
        {
            var selectedSolution = cmbSolution.SelectedItem as SolutionItem;
            AppendLog($"OnSolutionChangedAsync: _isApplyingSession={_isApplyingSession}, selected={selectedSolution?.UniqueName ?? "(null)"}, saved={_session?.SolutionUniqueName ?? "(null)"}");
            
            // If we're binding and have no saved solution, ignore the initial SelectedIndexChanged fired by data binding
            if (_isApplyingSession && string.IsNullOrWhiteSpace(_session?.SolutionUniqueName))
            {
                return;
            }

            // When applying a saved session, ignore transient selections that don't match the saved solution
            if (_isApplyingSession && _session != null && selectedSolution != null)
            {
                if (!selectedSolution.UniqueName.Equals(_session.SolutionUniqueName, StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog($"Ignoring transient solution selection during restore: {selectedSolution.UniqueName}");
                    return;
                }
            }

            if (selectedSolution == null)
            {
                // Cleared selection; disable and clear dependent controls
                cmbParentEntity.SelectedIndex = -1;
                cmbParentEntity.DataSource = null;
                cmbParentEntity.Enabled = false;
                UpdateEnableStates();
                return;
            }

            // When user manually changes solution (not during restore), clear all dependent selections
            if (_session != null && !_isApplyingSession && !_isRestoringSession)
            {
                AppendLog($"User changed solution to {selectedSolution.UniqueName}. Clearing dependent selections.");
                _session.SolutionUniqueName = selectedSolution.UniqueName;
                _session.SolutionId = selectedSolution.Id;
                _session.ConfigurationJson = null; // Clear saved configuration when user changes solution
                
                // Clear UI controls
                cmbParentEntity.DataSource = null;
                _mappingRows.Clear();
                
                SaveSettings();
            }

            await LoadEntitiesForSolutionAsync(selectedSolution.UniqueName);
            UpdateEnableStates();

            // Don't clear _isApplyingSession here - let LoadEntitiesForSolutionAsync handle it

            await Task.CompletedTask;
        }

        private async System.Threading.Tasks.Task LoadEntitiesForSolutionAsync(string solutionUniqueName)
        {
            if (string.IsNullOrWhiteSpace(solutionUniqueName) || !EnsureConnected())
            {
                return;
            }

            EnsureServices();

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Loading entities for {solutionUniqueName}...",
                Work = (worker, args) =>
                {
                    if (_solutionEntityCache.TryGetValue(solutionUniqueName, out var cached))
                    {
                        args.Result = cached;
                        return;
                    }

                    var entities = _metadataService!.GetSolutionEntitiesAsync(solutionUniqueName).GetAwaiter().GetResult();
                    args.Result = entities;
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        AppendLog($"Failed to load entities: {args.Error.Message}");
                        return;
                    }

                    _currentEntities = (args.Result as List<EntityMetadata>) ?? new List<EntityMetadata>();

                    // Always ensure saved parent/child entities exist in the current set (even when served from cache)
                    EnsureSessionEntitiesPresent();

                    _solutionEntityCache[solutionUniqueName] = _currentEntities;

                    AppendLog($"Loaded {_currentEntities.Count} entities for solution {solutionUniqueName}. Starting BindParentEntities...");
                    
                    // Call async method synchronously since we're already in PostWorkCallBack (UI thread)
                    BindParentEntities().GetAwaiter().GetResult();
                    
                    // Attempt to restore session if we have saved configuration
                    if (!string.IsNullOrWhiteSpace(_session?.ConfigurationJson))
                    {
                        AppendLog("Configuration found in session, will attempt restoration...");
                        RestoreSessionStateIfNeeded();
                    }
                    else
                    {
                        AppendLog("No saved configuration to restore.");
                    }
                }
            });

            await Task.CompletedTask;
        }

        private void EnsureSessionEntitiesPresent()
        {
            // This method is no longer needed since we restore from complete configuration JSON
            // Just log that entities are available
            AppendLog($"Solution entities available: {_currentEntities.Count} entities loaded.");
        }

        private async System.Threading.Tasks.Task BindParentEntities()
        {
            var items = _currentEntities
                .Select(e => new EntityItem
                {
                    LogicalName = e.LogicalName,
                    DisplayName = e.DisplayName?.UserLocalizedLabel?.Label ?? e.LogicalName,
                    Metadata = e
                })
                .OrderBy(e => e.DisplayName)
                .ToList();

            // Temporarily remove handler to prevent events during binding
            if (_parentEntityChangedHandler != null)
            {
                cmbParentEntity.SelectedIndexChanged -= _parentEntityChangedHandler;
            }
            
            cmbParentEntity.DataSource = null;
            cmbParentEntity.DataSource = items;
            cmbParentEntity.DisplayMember = nameof(EntityItem.DisplayName);
            cmbParentEntity.ValueMember = nameof(EntityItem.LogicalName);
            cmbParentEntity.Enabled = items.Any();

            AppendLog($"BindParentEntities: {items.Count} entities available");

            // Re-attach handler
            if (_parentEntityChangedHandler != null)
            {
                cmbParentEntity.SelectedIndexChanged += _parentEntityChangedHandler;
            }

            // No default selection when no saved session; wait for user.
            cmbParentEntity.SelectedIndex = -1;

            UpdateEnableStates();
        }

        private async System.Threading.Tasks.Task OnParentEntityChangedAsync()
        {
            if (cmbParentEntity.SelectedItem is not EntityItem parent)
            {
                UpdateEnableStates();
                return;
            }

            // If we're in restoration mode, skip clearing and let ApplyConfiguration handle it
            if (!(_isRestoringSession || _isApplyingSession))
            {
                // Normal parent change - reset child-specific UI/data
                ClearChildTabsAndData();
                _parentAttributes.Clear();
                _childAttributes.Clear();
                _selectedRelationship = null;
            }
            else
            {
                // During restoration, just return - ApplyConfiguration handles everything
                return;
            }

            await BindChildRelationships(parent);
            RefreshAttributeLists();

            var configurationApplied = await TryLoadExistingParentConfigurationAsync(parent);
            if (!configurationApplied)
            {
                RestoreSessionStateIfNeeded();
            }

            if (_mappingRows.Count == 0)
            {
                _mappingRows.Add(new MappingRow());
            }

            if (_childEntityTabs.Count == 0)
            {
                await OnAddChildRelationshipAsync();
            }

            UpdateEnableStates();

            await Task.CompletedTask;

        }

        private async System.Threading.Tasks.Task OnAddChildRelationshipAsync()
        {
            if (cmbParentEntity.SelectedItem is not EntityItem parent)
            {
                MessageBox.Show("Please select a parent entity first.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!EnsureConnected()) return;
            EnsureServices();

            if (_metadataService == null)
            {
                MessageBox.Show("Metadata service is not available.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Get available relationships for the parent entity
            var availableRelationships = _metadataService.GetChildRelationships(parent.Metadata, _currentEntities).ToList();
            
            if (!availableRelationships.Any())
            {
                MessageBox.Show("No child relationships found for the selected parent entity.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Filter out already-added relationships
            var alreadyAdded = new HashSet<string>(_childEntityTabs.Keys, StringComparer.OrdinalIgnoreCase);
            var availableForAdd = availableRelationships.Where(r => !alreadyAdded.Contains(r.SchemaName)).ToList();

            if (!availableForAdd.Any())
            {
                MessageBox.Show("All available child relationships are already configured.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Show dialog to select a relationship
            using var dialog = new ChildRelationshipPickerDialog(availableForAdd);
            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedRelationship != null)
            {
                await OnChildRelationshipSelectedAsync(dialog.SelectedRelationship);
            }

            await Task.CompletedTask;
        }

        private async System.Threading.Tasks.Task OnChildRelationshipSelectedAsync(RelationshipItem relationship)
        {
            if (relationship == null) return;

            _childAttributes.Clear();
            _selectedRelationship = relationship;

            RefreshAttributeLists();

            // Create or switch to tab for this child entity
            EnsureChildEntityTab(relationship);
            RefreshDestinationFieldDropdownsForAllTabs();

            // Add default statecode = 0 filter for new relationships
            if (!_filterCriteriaPerTab.ContainsKey(relationship.SchemaName))
            {
                AddDefaultStateCodeFilter(relationship.SchemaName);
            }

            // Load filters for the newly selected tab
            LoadCurrentTabFilters();

            UpdateEnableStates();
            AppendLog($"Added child relationship: {relationship.DisplayName}");
        }

        private void AddDefaultStateCodeFilter(string tabKey)
        {
            var defaultFilters = new List<SavedFilterCriteria>
            {
                new SavedFilterCriteria { Field = "statecode", Operator = "eq", Value = "0" }
            };
            _filterCriteriaPerTab[tabKey] = defaultFilters;
            AppendLog($"Added default statecode=0 filter for {tabKey}");
        }

        private async System.Threading.Tasks.Task BindChildRelationships(EntityItem parent)
        {
            if (_metadataService == null)
            {
                return;
            }

            var relationships = _metadataService.GetChildRelationships(parent.Metadata, _currentEntities).ToList();
            _availableRelationships = relationships;
            AppendLog($"BindChildRelationships for {parent.LogicalName}: found {relationships.Count} relationships");
        }

        private void EnsureChildEntityTab(RelationshipItem? relationship)
        {
            if (relationship == null) return;

            var tabKey = relationship.SchemaName;
            // Compact label: show entity name as tab text; schema drawn on second line in owner-draw
            var tabName = relationship.DisplayName;

            // Check if tab already exists
            if (_childEntityTabs.TryGetValue(tabKey, out var existingTab))
            {
                tabControlRightUpper.SelectedTab = existingTab;
                return;
            }

            // Create new tab for this child entity
            var newTab = new TabPage(tabName)
            {
                Padding = new Padding(4)
            };

            // CREATE SEPARATE BINDINGLIST FOR THIS TAB
            // First tab uses _mappingRows for backward compatibility, additional tabs get their own
            BindingList<MappingRow> tabMappings;
            if (_childEntityTabs.Count == 0)
            {
                // First tab - use existing _mappingRows
                tabMappings = _mappingRows;
            }
            else
            {
                // Additional tab - create new mapping list
                tabMappings = new BindingList<MappingRow>();
                tabMappings.Add(new MappingRow()); // Start with one blank row
                tabMappings.ListChanged += (s, e) => UpdateJsonPreview();
                _additionalChildMappings[tabKey] = tabMappings;
            }

            // Store the relationship for this tab
            _childRelationships[tabKey] = relationship;

            // Create a new DataGridView for this tab with its own data source
            var newGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                DataSource = tabMappings  // Each tab gets its own binding list
            };

            // Store reference to grid and key in tab's Tag for later retrieval
            newTab.Tag = new { Key = tabKey, Grid = newGrid, Relationship = relationship, Mappings = tabMappings };

            // Clone the column definitions from the main grid
            var clonedSourceCol = (DataGridViewComboBoxColumn)colSourceField.Clone();
            var clonedTargetCol = (DataGridViewComboBoxColumn)colTargetField.Clone();
            var clonedTriggerCol = (DataGridViewCheckBoxColumn)colTrigger.Clone();
            var clonedDeleteCol = (DataGridViewButtonColumn)colDelete.Clone();
            
            newGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                clonedSourceCol,
                clonedTargetCol,
                clonedTriggerCol,
                clonedDeleteCol
            });

            // Set up the source field dropdown with parent attributes
            clonedSourceCol.DataSource = _parentAttributes;
            clonedSourceCol.DisplayMember = nameof(AttributeItem.DisplayName);
            clonedSourceCol.ValueMember = nameof(AttributeItem.LogicalName);

            newTab.Controls.Add(newGrid);
            
            // Wire up the same event handlers
            newGrid.CellValueChanged += (s, e) => Grid_CellValueChanged(newGrid, e);
            newGrid.EditingControlShowing += GridMappings_EditingControlShowing;
            newGrid.CellClick += GridMappings_CellClick;
            newGrid.CellContentClick += GridMappings_CellContentClick;
            newGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (newGrid.IsCurrentCellDirty && newGrid.CurrentCell is DataGridViewComboBoxCell)
                {
                    newGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            newGrid.DataError += (s, e) => { e.ThrowException = false; };
            newGrid.UserDeletedRow += (s, e) => UpdateJsonPreview();
            newGrid.RowsAdded += (s, e) => GridMappings_RowsAdded_ForGrid(newGrid, e);

            _childEntityTabs[tabKey] = newTab;
            tabControlRightUpper.TabPages.Add(newTab);
            tabControlRightUpper.SelectedTab = newTab;

            AppendLog($"Created tab for child entity: {tabName} (Total tabs: {_childEntityTabs.Count})");
        }

        private void RefreshFilterFieldDropdowns()
        {
            // Get filter attributes - always includes statecode/statuscode
            if (_metadataService == null || _selectedRelationship == null)
            {
                return;
            }

            var childMeta = _currentEntities.FirstOrDefault(e => e.LogicalName.Equals(_selectedRelationship.ReferencingEntity, StringComparison.OrdinalIgnoreCase));
            if (childMeta != null)
            {
                var filterAttributes = _metadataService.GetFilterAttributeItems(childMeta, null).ToList();
                filterControl.SetAvailableFields(filterAttributes);
            }
        }

        private void RefreshDestinationFieldDropdowns()
        {
            // Iterate through all rows and refresh their target field dropdowns
            for (int i = 0; i < gridMappings.Rows.Count; i++)
            {
                var row = gridMappings.Rows[i];
                var sourceLogical = row.Cells[colSourceField.Index].Value as string;
                var targetCell = row.Cells[colTargetField.Index] as DataGridViewComboBoxCell;
                var currentTargetValue = targetCell?.Value as string;
                
                if (string.IsNullOrWhiteSpace(sourceLogical))
                {
                    // No source field selected, skip this row
                    continue;
                }
                
                // Get compatible attributes for this source field
                var compatibleAttributes = GetCompatibleChildAttributes(sourceLogical).ToList();
                
                // Update the target cell's Items collection
                if (targetCell != null)
                {
                    targetCell.DataSource = null;
                    targetCell.DisplayMember = nameof(AttributeItem.DisplayName);
                    targetCell.ValueMember = nameof(AttributeItem.LogicalName);
                    targetCell.Items.Clear();
                    
                    foreach (var attr in compatibleAttributes)
                    {
                        targetCell.Items.Add(attr);
                    }
                    
                    // Check if the currently selected field is still valid
                    if (!string.IsNullOrWhiteSpace(currentTargetValue))
                    {
                        var stillValid = compatibleAttributes.Any(a => a.LogicalName.Equals(currentTargetValue, StringComparison.OrdinalIgnoreCase));
                        
                        if (!stillValid)
                        {
                            // Clear the selection as it's no longer valid
                            targetCell.Value = null;
                        }
                    }
                    
                    gridMappings.InvalidateCell(targetCell);
                }
            }
            
            UpdateJsonPreview();
        }

        private void RefreshDestinationFieldDropdownsForAllTabs()
        {
            // Refresh dropdowns for all dynamically created tab grids
            foreach (var kvp in _childEntityTabs)
            {
                var tab = kvp.Value;
                var grid = tab.Controls.OfType<DataGridView>().FirstOrDefault();
                if (grid != null)
                {
                    RefreshDestinationFieldDropdownsForGrid(grid);
                }
            }
        }

        private void RefreshDestinationFieldDropdownsForGrid(DataGridView grid)
        {
            if (grid.Columns.Count < 2) return;

            var sourceColIndex = 0; // Source field column
            var targetColIndex = 1; // Target field column

            // Iterate through all rows and refresh their target field dropdowns
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                var row = grid.Rows[i];
                var sourceLogical = row.Cells[sourceColIndex].Value as string;
                var targetCell = row.Cells[targetColIndex] as DataGridViewComboBoxCell;
                var currentTargetValue = targetCell?.Value as string;
                
                if (string.IsNullOrWhiteSpace(sourceLogical))
                {
                    // No source field selected, skip this row
                    continue;
                }
                
                // Get compatible attributes for this source field
                var compatibleAttributes = GetCompatibleChildAttributes(sourceLogical).ToList();
                
                // Update the target cell's Items collection
                if (targetCell != null)
                {
                    targetCell.DataSource = null;
                    targetCell.DisplayMember = nameof(AttributeItem.DisplayName);
                    targetCell.ValueMember = nameof(AttributeItem.LogicalName);
                    targetCell.Items.Clear();
                    
                    foreach (var attr in compatibleAttributes)
                    {
                        targetCell.Items.Add(attr);
                    }
                    
                    // Check if the currently selected field is still valid
                    if (!string.IsNullOrWhiteSpace(currentTargetValue))
                    {
                        var stillValid = compatibleAttributes.Any(a => a.LogicalName.Equals(currentTargetValue, StringComparison.OrdinalIgnoreCase));
                        
                        if (!stillValid)
                        {
                            // Clear the selection as it's no longer valid
                            targetCell.Value = null;
                        }
                    }
                    
                    grid.InvalidateCell(targetCell);
                }
            }
        }

        private void RefreshAttributeLists()
        {
            if (_metadataService == null)
            {
                return;
            }

            var parentMeta = (cmbParentEntity.SelectedItem as EntityItem)?.Metadata;
            var childMeta = _selectedRelationship != null
                ? _currentEntities.FirstOrDefault(e => e.LogicalName.Equals(_selectedRelationship.ReferencingEntity, StringComparison.OrdinalIgnoreCase))
                : null;

            _parentAttributes = parentMeta != null
                ? _metadataService.GetAttributeItems(parentMeta, null).ToList()
                : new List<AttributeItem>();

            _childAttributes = childMeta != null
                ? _metadataService.GetAttributeItems(childMeta, null).ToList()
                : new List<AttributeItem>();

            if (_parentAttributes.Any() || _childAttributes.Any())
            {
                AppendLog($"Loaded {_parentAttributes.Count} parent and {_childAttributes.Count} child attributes");
            }

            BindAttributeColumns();
            UpdateJsonPreview();
        }

        private void RestoreSessionStateIfNeeded()
        {
            // Try to restore from configuration JSON if available
            if (_session != null && !string.IsNullOrWhiteSpace(_session.ConfigurationJson))
            {
                try
                {
                    var config = JsonConvert.DeserializeObject<CascadeConfigurationModel>(_session.ConfigurationJson ?? string.Empty);
                    if (config != null)
                    {
                        AppendLog("Restoring configuration from saved state...");
                        
                        // Set restoration flags to prevent OnParentEntityChangedAsync from interfering
                        _isApplyingSession = true;
                        _isRestoringSession = true;
                        
                        ApplyConfiguration(config).GetAwaiter().GetResult();
                        
                        // Clear flags after restoration completes
                        _isApplyingSession = false;
                        _isRestoringSession = false;
                        
                        AppendLog("Configuration restored successfully from saved settings.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"Warning: Failed to restore configuration from JSON: {ex.Message}");
                    _isApplyingSession = false;
                    _isRestoringSession = false;
                }
            }

            // Fallback: no configuration to restore
            AppendLog("No previous configuration to restore.");
        }

        private void BindAttributeColumns()
        {
            colSourceField.DataSource = _parentAttributes;
            colSourceField.DisplayMember = nameof(AttributeItem.DisplayName);
            colSourceField.ValueMember = nameof(AttributeItem.LogicalName);

            // Don't set DataSource for target field - each cell's Items collection is populated dynamically
            // based on the selected source field in CellValueChanged and RowsAdded handlers
            colTargetField.DataSource = null;
            colTargetField.DisplayMember = nameof(AttributeItem.DisplayName);
            colTargetField.ValueMember = nameof(AttributeItem.LogicalName);
        }

        private void Grid_CellValueChanged(DataGridView grid, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var sourceColIndex = 0; // Source field column
            var targetColIndex = 1; // Target field column

            // When source field changes, populate compatible target fields
            if (e.ColumnIndex == sourceColIndex)
            {
                var sourceLogical = grid.Rows[e.RowIndex].Cells[sourceColIndex].Value as string;
                var targetCell = grid.Rows[e.RowIndex].Cells[targetColIndex] as DataGridViewComboBoxCell;

                if (targetCell != null)
                {
                    if (string.IsNullOrWhiteSpace(sourceLogical))
                    {
                        targetCell.Value = null;
                        targetCell.ReadOnly = true;
                        targetCell.Items.Clear();
                    }
                    else
                    {
                        targetCell.ReadOnly = false;
                        var compatibleAttributes = GetCompatibleChildAttributes(sourceLogical).ToList();
                        targetCell.DataSource = null;
                        targetCell.DisplayMember = nameof(AttributeItem.DisplayName);
                        targetCell.ValueMember = nameof(AttributeItem.LogicalName);
                        targetCell.Items.Clear();

                        foreach (var attr in compatibleAttributes)
                        {
                            targetCell.Items.Add(attr);
                        }
                    }

                    // Force the cell and row to refresh
                    grid.InvalidateCell(targetCell);
                    grid.InvalidateRow(e.RowIndex);
                }
            }

            // Check if we should add a new row
            CheckAndAddNewRow(e.RowIndex);

            UpdateJsonPreview();
        }

        private void GridMappings_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            Grid_CellValueChanged(gridMappings, e);
        }
        
        private void GridMappings_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            // Commit the cell value immediately so CellValueChanged fires
            if (gridMappings.IsCurrentCellDirty && gridMappings.CurrentCell is DataGridViewComboBoxCell)
            {
                gridMappings.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }
        
        private void GridMappings_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            // Handle delete button click (column 3 is delete button)
            if (e.RowIndex >= 0 && e.ColumnIndex == 3)
            {
                _mappingRows.RemoveAt(e.RowIndex);
                
                // Ensure at least one blank row exists
                if (_mappingRows.Count == 0)
                {
                    _mappingRows.Add(new MappingRow());
                }
                
                UpdateJsonPreview();
            }
        }
        
        private void CheckAndAddNewRow(int changedRowIndex)
        {
            // Only add new row if the changed row now has both source and target fields
            if (changedRowIndex >= 0 && changedRowIndex < _mappingRows.Count)
            {
                var row = _mappingRows[changedRowIndex];
                if (!string.IsNullOrWhiteSpace(row.SourceField) && !string.IsNullOrWhiteSpace(row.TargetField))
                {
                    // Check if this is the last row
                    if (changedRowIndex == _mappingRows.Count - 1)
                    {
                        // Add a new blank row
                        _mappingRows.Add(new MappingRow());
                    }
                }
            }
        }
        
        private void GridMappings_RowsAdded(object? sender, DataGridViewRowsAddedEventArgs e)
        {
            if (sender is DataGridView grid)
            {
                GridMappings_RowsAdded_ForGrid(grid, e);
            }
        }

        private void GridMappings_RowsAdded_ForGrid(DataGridView grid, DataGridViewRowsAddedEventArgs e)
        {
            var targetColIndex = 1; // Target field column
            
            // Initialize cells for newly added rows
            for (int i = e.RowIndex; i < e.RowIndex + e.RowCount; i++)
            {
                if (i < grid.Rows.Count && i < _mappingRows.Count)
                {
                    var row = _mappingRows[i];
                    var targetCell = grid.Rows[i].Cells[targetColIndex] as DataGridViewComboBoxCell;
                    
                    if (targetCell != null)
                    {
                        // If the row has a source field, enable and populate the target cell
                        if (!string.IsNullOrWhiteSpace(row.SourceField))
                        {
                            targetCell.ReadOnly = false;
                            var compatibleAttributes = GetCompatibleChildAttributes(row.SourceField).ToList();
                            
                            // Use Items collection - set DisplayMember/ValueMember FIRST, then populate
                            targetCell.DataSource = null;
                            targetCell.DisplayMember = nameof(AttributeItem.DisplayName);
                            targetCell.ValueMember = nameof(AttributeItem.LogicalName);
                            targetCell.Items.Clear();
                            
                            foreach (var attr in compatibleAttributes)
                            {
                                targetCell.Items.Add(attr);
                            }
                        }
                        else
                        {
                            // No source field, keep target disabled
                            targetCell.ReadOnly = true;
                            targetCell.DataSource = null;
                            targetCell.Items.Clear();
                            targetCell.Value = null;
                        }
                        
                        grid.InvalidateCell(targetCell);
                    }
                }
            }
        }

        private void GridMappings_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (sender is not DataGridView grid) return;
            
            // Auto-open dropdown on first click for ComboBox cells
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            
            // Don't allow clicking on disabled target field
            if (cell is DataGridViewComboBoxCell comboCell && comboCell.ReadOnly)
            {
                return;
            }
            
            if (cell is DataGridViewComboBoxCell)
            {
                // Begin edit mode if not already in it
                if (!grid.IsCurrentCellInEditMode)
                {
                    grid.BeginEdit(true);
                }

                // Show dropdown if we're editing a ComboBox
                if (grid.EditingControl is ComboBox combo)
                {
                    combo.DroppedDown = true;
                }
            }
        }

        private void GridMappings_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (sender is not DataGridView grid) return;
            if (grid.CurrentCell == null) return;
            
            var sourceColIndex = 0;
            var targetColIndex = 1;
            
            if (grid.CurrentCell.ColumnIndex == sourceColIndex && e.Control is ComboBox sourceCombo)
            {
                sourceCombo.DataSource = _parentAttributes;
                sourceCombo.DisplayMember = nameof(AttributeItem.DisplayName);
                sourceCombo.ValueMember = nameof(AttributeItem.LogicalName);
            }
            else if (grid.CurrentCell.ColumnIndex == targetColIndex && e.Control is ComboBox targetCombo)
            {
                // Don't override with DataSource - the cell's Items collection is already populated
                // in CellValueChanged or RowsAdded event handlers
            }
        }

        private IEnumerable<AttributeItem> GetCompatibleChildAttributes(string? sourceLogical)
        {
            if (string.IsNullOrWhiteSpace(sourceLogical))
            {
                return Enumerable.Empty<AttributeItem>();
            }

            var sourceAttr = _parentAttributes.FirstOrDefault(a => a.LogicalName.Equals(sourceLogical, StringComparison.OrdinalIgnoreCase))?.Metadata;
            if (sourceAttr == null)
            {
                AppendLog($"Warning: Source attribute '{sourceLogical}' not found in parent attributes");
                return Enumerable.Empty<AttributeItem>();
            }

            var compatible = _childAttributes.Where(child => IsCompatible(sourceAttr, child.Metadata)).ToList();
            return compatible;
        }

        private bool IsCompatible(AttributeMetadata source, AttributeMetadata target)
        {
            if (target.IsValidForUpdate != true)
            {
                return false;
            }

            if (source.AttributeType == target.AttributeType)
            {
                return true;
            }

            // Allow mapping any type to string/memo (plugin converts to text when needed)
            if (target.AttributeType == AttributeTypeCode.String || target.AttributeType == AttributeTypeCode.Memo)
            {
                return true;
            }

            // Numeric grouping
            bool IsNumeric(AttributeMetadata attr) => attr.AttributeType == AttributeTypeCode.Integer || attr.AttributeType == AttributeTypeCode.BigInt || attr.AttributeType == AttributeTypeCode.Decimal || attr.AttributeType == AttributeTypeCode.Double || attr.AttributeType == AttributeTypeCode.Money;
            if (IsNumeric(source) && IsNumeric(target))
            {
                return true;
            }

            // Option set grouping
            bool IsOption(AttributeMetadata attr) => attr.AttributeType == AttributeTypeCode.Picklist || attr.AttributeType == AttributeTypeCode.State || attr.AttributeType == AttributeTypeCode.Status;
            if (IsOption(source) && IsOption(target))
            {
                return true;
            }

            // Lookup grouping
            bool IsLookup(AttributeMetadata attr) => attr.AttributeType == AttributeTypeCode.Lookup || attr.AttributeType == AttributeTypeCode.Customer || attr.AttributeType == AttributeTypeCode.Owner;
            if (IsLookup(source) && IsLookup(target))
            {
                return true;
            }

            return false;
        }

        private void UpdateJsonPreview()
        {
            // Always save current tab's filters before generating JSON
            SaveCurrentTabFilters();
            
            if (cmbParentEntity.SelectedItem is not EntityItem parent)
            {
                txtJsonPreview.Text = "Select a parent entity to preview configuration.";
                return;
            }

            // NEW: Collect RelatedEntityConfig from ALL child tabs
            var relatedEntities = new List<RelatedEntityConfig>();

            AppendLog($"UpdateJsonPreview: Processing {tabControlRightUpper.TabPages.Count} child tabs");
            foreach (TabPage tab in tabControlRightUpper.TabPages)
            {
                var tabKey = GetTabKey(tab);
                
                AppendLog($"  Processing tab: {tab.Text} (key: {tabKey})");

                // Get relationship and mappings for this tab
                if (tabKey == null || !_childRelationships.TryGetValue(tabKey, out var relationship))
                {
                    AppendLog($"  Warning: No relationship found for tab {tab.Text}");
                    continue;
                }

                // Get the grid and mapping data from tab Tag
                var tabData = tab.Tag as dynamic;
                if (tabData == null)
                {
                    AppendLog($"  Warning: No data found for tab {tab.Text}");
                    continue;
                }

                BindingList<MappingRow> tabMappings = tabData.Mappings;
                AppendLog($"    Found {tabMappings.Count} total mapping rows");

                // Convert mappings to FieldMapping objects
                var mappings = tabMappings
                    .Where(m => !string.IsNullOrWhiteSpace(m.SourceField) && !string.IsNullOrWhiteSpace(m.TargetField))
                    .Select(m => new FieldMapping
                    {
                        SourceField = m.SourceField!,
                        TargetField = m.TargetField!,
                        IsTriggerField = m.IsTriggerField
                    })
                    .ToList();

                AppendLog($"    Found {mappings.Count} valid mapping rows (non-empty)");

                // Skip tabs with no valid mappings
                if (!mappings.Any())
                {
                    AppendLog($"  Info: Tab {tab.Text} has no valid mappings, skipping");
                    continue;
                }

                // Get per-tab filter criteria
                string? filterString = null;
                if (_filterCriteriaPerTab.TryGetValue(tabKey, out var tabFilters) && tabFilters.Any())
                {
                    AppendLog($"    Found {tabFilters.Count} filter criteria");
                    filterString = string.Join(";", tabFilters.Select(f =>
                    {
                        var parts = new List<string> { f.Field ?? string.Empty, f.Operator ?? string.Empty };
                        if (f.Operator != "null" && f.Operator != "notnull")
                        {
                            parts.Add(f.Value ?? string.Empty);
                        }
                        else
                        {
                            parts.Add("null");
                        }
                        return string.Join("|", parts);
                    }));
                }

                relatedEntities.Add(new RelatedEntityConfig
                {
                    EntityName = relationship.ReferencingEntity,
                    RelationshipName = relationship.SchemaName,
                    LookupFieldName = relationship.ReferencingAttribute,
                    UseRelationship = true,
                    FilterCriteria = string.IsNullOrWhiteSpace(filterString) ? null : filterString,
                    FieldMappings = mappings
                });

                AppendLog($"  Added {relationship.SchemaName} to JSON with {mappings.Count} mappings");
            }

            if (!relatedEntities.Any())
            {
                txtJsonPreview.Text = "Add at least one child relationship with field mappings to preview configuration.";
                return;
            }

            // Generate name based on number of children
            var configName = relatedEntities.Count == 1
                ? $"{parent.DisplayName} to {relatedEntities[0].EntityName}"
                : $"{parent.DisplayName} to {relatedEntities.Count} child entities";

            var config = new CascadeConfigurationModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = configName,
                ParentEntity = parent.LogicalName,
                RelatedEntities = relatedEntities,
                EnableTracing = chkEnableTracing.Checked,
                IsActive = true
            };

            txtJsonPreview.Text = JsonConvert.SerializeObject(config, Formatting.Indented);
        }

        private async System.Threading.Tasks.Task RetrieveConfiguredAsync()
        {
            if (!EnsureConnected()) return;
            EnsureServices();
            
            if (_configurationService == null)
            {
                AppendLog("Error: Configuration service is not available.");
                MessageBox.Show("Configuration service is not available. Please reconnect to Dataverse.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Retrieving existing CascadeFields configurations...",
                Work = (worker, args) =>
                {
                    args.Result = _configurationService!.GetExistingConfigurationsAsync().GetAwaiter().GetResult();
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        AppendLog($"Failed to retrieve configurations: {args.Error.Message}");
                        return;
                    }

                    var configs = args.Result as List<ConfiguredRelationship> ?? new List<ConfiguredRelationship>();
                    if (!configs.Any())
                    {
                        MessageBox.Show("No existing configurations were found.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    // Group configurations by parent entity and build parent items with metadata
                    var parentEntities = configs
                        .GroupBy(c => c.Configuration?.ParentEntity ?? "Unknown")
                        .Select(g =>
                        {
                            var parentLogical = g.Key;
                            var parentDisplay = GetEntityDisplayName(parentLogical);
                            var childEntities = new List<ChildEntityInfo>();

                            // Collect all unique child entities across all configs for this parent
                            foreach (var config in g)
                            {
                                if (config.Configuration?.RelatedEntities != null)
                                {
                                    foreach (var related in config.Configuration.RelatedEntities)
                                    {
                                        var childDisplay = GetEntityDisplayName(related.EntityName);
                                        var lookupFieldDisplay = GetAttributeDisplayName(related.EntityName, related.LookupFieldName);
                                        childEntities.Add(new ChildEntityInfo
                                        {
                                            DisplayName = childDisplay,
                                            RelationshipName = related.RelationshipName,
                                            LookupFieldDisplayName = lookupFieldDisplay
                                        });
                                    }
                                }
                            }

                            // Remove duplicates
                            childEntities = childEntities
                                .GroupBy(x => x.RelationshipName)
                                .Select(g2 => g2.First())
                                .ToList();

                            return new ParentEntityItem
                            {
                                ParentEntity = parentLogical,
                                DisplayName = parentDisplay,
                                ChildCount = childEntities.Count,
                                ChildEntities = childEntities,
                                LastModified = DateTime.Now
                            };
                        })
                        .OrderBy(p => p.DisplayName)
                        .ToList();

                    if (!parentEntities.Any())
                    {
                        MessageBox.Show("No parent entities with configurations were found.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    using var dialog = new ParentEntityPickerDialog(parentEntities);
                    if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dialog.SelectedParentEntity))
                    {
                        // Find the first (or only) configuration for the selected parent
                        var selectedConfig = configs
                            .FirstOrDefault(c => c.Configuration?.ParentEntity == dialog.SelectedParentEntity)
                            ?.Configuration;

                        if (selectedConfig != null)
                        {
                            ApplyConfiguration(selectedConfig).GetAwaiter().GetResult();
                        }
                    }
                }
            });

            await Task.CompletedTask;
        }

        private string GetEntityDisplayName(string logicalName)
        {
            // Try to find the entity in loaded metadata
            if (_currentEntities != null)
            {
                var entity = _currentEntities.FirstOrDefault(e => 
                    e.LogicalName.Equals(logicalName, StringComparison.OrdinalIgnoreCase));
                if (entity != null)
                {
                    return $"{entity.DisplayName.UserLocalizedLabel?.Label ?? entity.LogicalName} ({logicalName})";
                }
            }
            return logicalName;
        }

        private string GetAttributeDisplayName(string entityLogicalName, string attributeLogicalName)
        {
            // Try to find the attribute in loaded metadata
            if (_currentEntities != null && !string.IsNullOrEmpty(attributeLogicalName))
            {
                var entity = _currentEntities.FirstOrDefault(e => 
                    e.LogicalName.Equals(entityLogicalName, StringComparison.OrdinalIgnoreCase));
                if (entity?.Attributes != null)
                {
                    var attribute = entity.Attributes.FirstOrDefault(a => 
                        a.LogicalName.Equals(attributeLogicalName, StringComparison.OrdinalIgnoreCase));
                    if (attribute != null)
                    {
                        return attribute.DisplayName.UserLocalizedLabel?.Label ?? attributeLogicalName;
                    }
                }
            }
            return attributeLogicalName ?? string.Empty;
        }

        private async Task ApplyConfiguration(CascadeConfigurationModel configuration)
        {
            if (configuration == null)
            {
                return;
            }

            AppendLog($"Applying configuration for parent '{configuration.ParentEntity}'.");

            // Find and select parent entity WITHOUT triggering event handler
            EntityItem? parentEntity = null;
            if (cmbParentEntity.DataSource is List<EntityItem> parents)
            {
                parentEntity = parents.FirstOrDefault(p => p.LogicalName.Equals(configuration.ParentEntity, StringComparison.OrdinalIgnoreCase));
                if (parentEntity != null)
                {
                    // Temporarily remove handler to prevent OnParentEntityChangedAsync from firing
                    if (_parentEntityChangedHandler != null)
                    {
                        cmbParentEntity.SelectedIndexChanged -= _parentEntityChangedHandler;
                    }
                    
                    cmbParentEntity.SelectedItem = parentEntity;
                    
                    // Re-attach handler
                    if (_parentEntityChangedHandler != null)
                    {
                        cmbParentEntity.SelectedIndexChanged += _parentEntityChangedHandler;
                    }
                    
                    // Load child relationships for this parent entity so relationship matching works
                    await BindChildRelationships(parentEntity);
                    RefreshAttributeLists();
                }
            }

            if (parentEntity == null)
            {
                AppendLog($"Warning: Parent entity '{configuration.ParentEntity}' not found in current solution");
                return;
            }

            // Clear existing child tabs and data
            _childEntityTabs.Clear();
            _additionalChildMappings.Clear();
            _childRelationships.Clear();
            _filterCriteriaPerTab.Clear();
            tabControlRightUpper.TabPages.Clear();

            var relatedEntities = configuration.RelatedEntities ?? new List<RelatedEntityConfig>();
            if (!relatedEntities.Any())
            {
                AppendLog("No child entities in configuration");
                return;
            }

            // Apply tracing setting
            chkEnableTracing.Checked = configuration.EnableTracing;

            // Load each child relationship as a separate tab
            foreach (var related in relatedEntities)
            {
                // Find matching relationship from available relationships
                // CRITICAL: Match ONLY by SchemaName to handle multiple relationships to the same entity
                RelationshipItem? matchingRelationship = null;
                var availableRelationships = _availableRelationships ?? new List<RelationshipItem>();
                matchingRelationship = availableRelationships.FirstOrDefault(r => 
                    r.SchemaName.Equals(related.RelationshipName, StringComparison.OrdinalIgnoreCase));

                if (matchingRelationship == null)
                {
                    AppendLog($"Warning: Could not find relationship '{related.RelationshipName}' for entity '{related.EntityName}', skipping");
                    continue;
                }

                // Create or find the tab for this child entity - don't call OnChildRelationshipSelectedAsync
                // because it modifies _selectedRelationship and calls LoadCurrentTabFilters which
                // interferes with loading multiple tabs during restoration
                var tabKey = matchingRelationship.SchemaName;
                TabPage? tab = null;
                
                if (!_childEntityTabs.TryGetValue(tabKey, out tab))
                {
                    // Create new tab for this relationship
                    EnsureChildEntityTab(matchingRelationship);
                    if (!_childEntityTabs.TryGetValue(tabKey, out tab))
                    {
                        AppendLog($"Error: Failed to create tab for {related.EntityName}");
                        continue;
                    }
                }
                else
                {
                    // Tab already exists, just update it
                }

                // Get the appropriate BindingList for this tab
                BindingList<MappingRow> targetMappings;
                if (_childEntityTabs.Count == 1 && _childEntityTabs.First().Value == tab)
                {
                    targetMappings = _mappingRows; // First tab uses shared list
                }
                else
                {
                    if (!_additionalChildMappings.TryGetValue(tabKey, out targetMappings!))
                    {
                        targetMappings = new BindingList<MappingRow>();
                        _additionalChildMappings[tabKey] = targetMappings;
                    }
                }

                // Load mappings for this tab
                targetMappings.Clear();
                foreach (var mapping in related.FieldMappings)
                {
                    targetMappings.Add(new MappingRow
                    {
                        SourceField = mapping.SourceField,
                        TargetField = mapping.TargetField,
                        IsTriggerField = mapping.IsTriggerField
                    });
                }

                // Store relationship info
                _childRelationships[tabKey] = matchingRelationship;

                // Load filter criteria for this tab
                if (!string.IsNullOrWhiteSpace(related.FilterCriteria))
                {
                    var filters = new List<SavedFilterCriteria>();
                    var filterParts = related.FilterCriteria.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (var part in filterParts)
                    {
                        var components = part.Split('|');
                        if (components.Length >= 2)
                        {
                            var filter = new SavedFilterCriteria
                            {
                                Field = components[0],
                                Operator = components[1],
                                Value = components.Length > 2 && components[2] != "null" ? components[2] : null
                            };
                            filters.Add(filter);
                        }
                    }

                    _filterCriteriaPerTab[tabKey] = filters;
                    AppendLog($"Loaded {filters.Count} filter(s) for {related.EntityName}: {related.FilterCriteria}");
                }

                AppendLog($"Loaded {targetMappings.Count} field mapping(s) for child entity '{related.EntityName}'");
            }

            // Refresh dropdowns for ALL tabs - each tab needs its own child attributes loaded
            foreach (var kvp in _childEntityTabs)
            {
                var tabKey = kvp.Key;
                var tab = kvp.Value;
                
                // Get the relationship for this tab
                if (_childRelationships.TryGetValue(tabKey, out var relationship))
                {
                    // Load child attributes for this specific relationship
                    var childMeta = _currentEntities.FirstOrDefault(e => 
                        e.LogicalName.Equals(relationship.ReferencingEntity, StringComparison.OrdinalIgnoreCase));
                    
                    if (childMeta != null)
                    {
                        var childAttributes = _metadataService?.GetAttributeItems(childMeta, null).ToList() ?? new List<AttributeItem>();
                        
                        // Get the grid for this tab
                        var grid = tab.Controls.OfType<DataGridView>().FirstOrDefault();
                        if (grid != null)
                        {
                            // Refresh destination field dropdowns for each row based on its source field
                            for (int i = 0; i < grid.Rows.Count; i++)
                            {
                                var row = grid.Rows[i];
                                var sourceLogical = row.Cells[0].Value as string;
                                var targetCell = row.Cells[1] as DataGridViewComboBoxCell;
                                
                                if (!string.IsNullOrWhiteSpace(sourceLogical) && targetCell != null)
                                {
                                    var sourceAttr = _parentAttributes.FirstOrDefault(a => 
                                        a.LogicalName.Equals(sourceLogical, StringComparison.OrdinalIgnoreCase))?.Metadata;
                                    
                                    if (sourceAttr != null)
                                    {
                                        var compatibleAttributes = childAttributes
                                            .Where(child => IsCompatible(sourceAttr, child.Metadata))
                                            .ToList();
                                        
                                        targetCell.DataSource = null;
                                        targetCell.DisplayMember = nameof(AttributeItem.DisplayName);
                                        targetCell.ValueMember = nameof(AttributeItem.LogicalName);
                                        targetCell.Items.Clear();
                                        
                                        foreach (var attr in compatibleAttributes)
                                        {
                                            targetCell.Items.Add(attr);
                                        }
                                        
                                        grid.InvalidateCell(targetCell);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // Refresh filter field dropdowns for each tab
            if (tabControlRightUpper.TabPages.Count > 0)
            {
                // Set the selected relationship for the first tab to enable filter dropdown refresh
                if (_childEntityTabs.Count > 0)
                {
                    var firstTabKey = _childEntityTabs.Keys.First();
                    if (_childRelationships.TryGetValue(firstTabKey, out var firstRelationship))
                    {
                        _selectedRelationship = firstRelationship;
                    }
                }
                
                RefreshFilterFieldDropdowns();
                tabControlRightUpper.SelectedIndex = 0;
                LoadCurrentTabFilters();
            }

            UpdateJsonPreview();
            AppendLog($"Configuration applied: {relatedEntities.Count} child relationship(s) loaded");
        }

        private async System.Threading.Tasks.Task PublishConfigurationAsync()
        {
            if (!EnsureConnected()) return;
            EnsureServices();

            if (cmbParentEntity.SelectedItem is not EntityItem parent)
            {
                MessageBox.Show("Select a parent entity before publishing.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // NEW: Check if we have any child tabs with mappings
            if (_childEntityTabs.Count == 0)
            {
                MessageBox.Show("Add at least one child relationship before publishing.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (_configurationService == null)
            {
                MessageBox.Show("Configuration service is not available. Please reconnect to Dataverse.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var confirm = MessageBox.Show(
                $"This will create or update the CascadeFields step and pre-image for {_childEntityTabs.Count} child relationship(s). Continue?",
                "Confirm Publish", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            // Validate that ALL child tabs have at least one mapping before allowing publish
            var tabsWithoutMappings = new List<string>();

            foreach (var tabKvp in _childEntityTabs)
            {
                var tabKey = tabKvp.Key;
                var tab = tabKvp.Value;

                var tabData = tab.Tag as dynamic;
                if (tabData == null)
                {
                    tabsWithoutMappings.Add(tab.Text);
                    continue;
                }

                BindingList<MappingRow> tabMappings = tabData.Mappings;
                var validMappings = tabMappings
                    .Where(m => !string.IsNullOrWhiteSpace(m.SourceField) && !string.IsNullOrWhiteSpace(m.TargetField))
                    .ToList();

                if (!validMappings.Any())
                {
                    tabsWithoutMappings.Add(tab.Text);
                }
            }

            // Block publish if any child relationships don't have mappings
            if (tabsWithoutMappings.Any())
            {
                var message = $"The following child relationship(s) do not have any field mappings configured:\n\n" +
                              string.Join("\n", tabsWithoutMappings.Select(t => $"  • {t}")) +
                              "\n\nEach child relationship must have at least one field mapping before publishing.";
                MessageBox.Show(message, "Mappings Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Collect RelatedEntityConfig from ALL child tabs (same logic as UpdateJsonPreview)
            var relatedEntities = new List<RelatedEntityConfig>();

            foreach (var tabKvp in _childEntityTabs)
            {
                var tabKey = tabKvp.Key;
                var tab = tabKvp.Value;

                if (!_childRelationships.TryGetValue(tabKey, out var relationship))
                {
                    AppendLog($"Warning: No relationship found for tab {tab.Text}, skipping");
                    continue;
                }

                var tabData = tab.Tag as dynamic;
                if (tabData == null)
                {
                    AppendLog($"Warning: No data found for tab {tab.Text}, skipping");
                    continue;
                }

                BindingList<MappingRow> tabMappings = tabData.Mappings;

                var mappings = tabMappings
                    .Where(m => !string.IsNullOrWhiteSpace(m.SourceField) && !string.IsNullOrWhiteSpace(m.TargetField))
                    .Select(m => new FieldMapping
                    {
                        SourceField = m.SourceField!,
                        TargetField = m.TargetField!,
                        IsTriggerField = m.IsTriggerField
                    })
                    .ToList();

                if (!mappings.Any())
                {
                    AppendLog($"Warning: Tab {tab.Text} has no valid mappings, skipping");
                    continue;
                }

                // Get per-tab filter criteria
                string? filterString = null;
                if (_filterCriteriaPerTab.TryGetValue(tabKey, out var tabFilters) && tabFilters.Any())
                {
                    filterString = string.Join(";", tabFilters.Select(f =>
                    {
                        var parts = new List<string> { f.Field ?? string.Empty, f.Operator ?? string.Empty };
                        if (f.Operator != "null" && f.Operator != "notnull")
                        {
                            parts.Add(f.Value ?? string.Empty);
                        }
                        else
                        {
                            parts.Add("null");
                        }
                        return string.Join("|", parts);
                    }));
                }

                relatedEntities.Add(new RelatedEntityConfig
                {
                    EntityName = relationship.ReferencingEntity,
                    RelationshipName = relationship.SchemaName,
                    LookupFieldName = relationship.ReferencingAttribute,
                    UseRelationship = true,
                    FilterCriteria = string.IsNullOrWhiteSpace(filterString) ? null : filterString,
                    FieldMappings = mappings
                });
            }

            if (!relatedEntities.Any())
            {
                MessageBox.Show("Add at least one field mapping in at least one child relationship before publishing.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var configName = relatedEntities.Count == 1
                ? $"{parent.DisplayName} to {relatedEntities[0].EntityName}"
                : $"{parent.DisplayName} to {relatedEntities.Count} child entities";

            var config = new CascadeConfigurationModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = configName,
                ParentEntity = parent.LogicalName,
                RelatedEntities = relatedEntities,
                EnableTracing = chkEnableTracing.Checked,
                IsActive = true
            };

            var progress = new Progress<string>(msg => AppendLog(msg));

            SetBusy(true);

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Publishing configuration to Dataverse...",
                Work = (worker, args) =>
                {
                    var solutionId = _session?.SolutionId ?? Guid.Empty;
                    _configurationService!.PublishConfigurationAsync(config, progress, CancellationToken.None, solutionId).GetAwaiter().GetResult();
                    args.Result = JsonConvert.SerializeObject(config, Formatting.Indented);
                },
                PostWorkCallBack = args =>
                {
                    SetBusy(false);
                    if (args.Error != null)
                    {
                        AppendLog($"Publish failed: {args.Error.Message}");
                        MessageBox.Show($"Publish failed: {args.Error.Message}", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    AppendLog($"Publish complete: {relatedEntities.Count} child relationship(s) configured.");
                    var solutionMsg = _session?.SolutionId != null && _session.SolutionId != Guid.Empty
                        ? " Components have been added to the selected solution."
                        : string.Empty;
                    MessageBox.Show($"Publish complete: step and pre-image upserted for {relatedEntities.Count} child relationship(s).{solutionMsg}", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    txtJsonPreview.Text = args.Result as string ?? txtJsonPreview.Text;
                }
            });

            await Task.CompletedTask;
        }

        private async System.Threading.Tasks.Task UpdatePluginAssemblyAsync()
        {
            if (!EnsureConnected()) return;
            EnsureServices();
            
            if (_configurationService == null)
            {
                AppendLog("Error: Configuration service is not available.");
                MessageBox.Show("Configuration service is not available. Please reconnect to Dataverse.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var pluginPath = ResolvePluginAssemblyPath();
            await Task.CompletedTask;
            if (string.IsNullOrWhiteSpace(pluginPath) || !File.Exists(pluginPath))
            {
                var expected = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CascadeFields.Plugin.dll");
                MessageBox.Show($"Plugin assembly not found. Expected at '{expected}' or dev build output. Run pack-nuget.ps1 to copy the plugin into XrmToolBox plugins, or build CascadeFields.Plugin.",
                    "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var assemblyPath = pluginPath; // validated path for async calls

            var progress = new Progress<string>(msg => AppendLog(msg));

            SetBusy(true);

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Updating CascadeFields plugin assembly...",
                Work = (worker, args) =>
                {
                    _configurationService!.UpdatePluginAssemblyAsync(assemblyPath!, progress, CancellationToken.None).GetAwaiter().GetResult();
                },
                PostWorkCallBack = args =>
                {
                    SetBusy(false);
                    if (args.Error != null)
                    {
                        AppendLog($"Update plugin failed: {args.Error.Message}");
                        MessageBox.Show($"Update failed: {args.Error.Message}", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    AppendLog("Plugin assembly and type upserted.");
                    MessageBox.Show("Plugin assembly and type upserted.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            });
        }

        #endregion

        #region Helpers

        private bool EnsureConnected()
        {
            if (Service == null)
            {
                MessageBox.Show("Connect to Dataverse before performing this action.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private void AppendLog(string message)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        private void SetBusy(bool isBusy)
        {
            btnLoadMetadata.Enabled = !isBusy;
            btnRetrieveConfigured.Enabled = !isBusy;
            btnUpdatePlugin.Enabled = !isBusy;
            btnPublish.Enabled = !isBusy;
            btnClearSession.Enabled = !isBusy;
        }

        private void ClearChildTabsAndData()
        {
            _childEntityTabs.Clear();
            _additionalChildMappings.Clear();
            _childRelationships.Clear();
            _filterCriteriaPerTab.Clear();
            tabControlRightUpper.TabPages.Clear();

            _mappingRows.Clear();
            _previouslySelectedTab = null;
            filterControl.ClearFilters();
        }

        private string? ResolvePluginAssemblyPath()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var baseDir = string.IsNullOrWhiteSpace(assemblyLocation) ? null : Path.GetDirectoryName(assemblyLocation);
            
            var candidates = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                // Check CascadeFieldsConfigurator/Assets/DataversePlugin subdirectory (primary deployed location)
                candidates.Add(Path.Combine(baseDir, "CascadeFieldsConfigurator", "Assets", "DataversePlugin", "CascadeFields.Plugin.dll"));
                
                // Check Assets/DataversePlugin subdirectory (when running from subfolder)
                candidates.Add(Path.Combine(baseDir, "Assets", "DataversePlugin", "CascadeFields.Plugin.dll"));
                
                // Check parent directory's CascadeFieldsConfigurator/Assets folder
                var parent = Directory.GetParent(baseDir);
                if (parent != null)
                {
                    candidates.Add(Path.Combine(parent.FullName, "CascadeFieldsConfigurator", "Assets", "DataversePlugin", "CascadeFields.Plugin.dll"));
                    candidates.Add(Path.Combine(parent.FullName, "Assets", "DataversePlugin", "CascadeFields.Plugin.dll"));
                }
                
                // Check base directory directly (legacy/fallback)
                candidates.Add(Path.Combine(baseDir, "CascadeFields.Plugin.dll"));
                
                // Check relative dev build paths
                candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "CascadeFields.Plugin", "bin", "Release", "net462", "CascadeFields.Plugin.dll")));
                candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "CascadeFields.Plugin", "bin", "Release", "net462", "CascadeFields.Plugin.dll")));
            }

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    AppendLog($"Found plugin assembly at: {path}");
                    return path;
                }
            }
            
            AppendLog($"Warning: Plugin assembly not found. Checked {candidates.Count} locations.");
            return null;
        }

        private void ClearSession()
        {
            var key = GetConnectionKey();
            if (!string.IsNullOrWhiteSpace(key))
            {
                _settings.Sessions.RemoveAll(s => string.Equals(s.ConnectionKey, key, StringComparison.OrdinalIgnoreCase));
                SaveSettings();
                _session = _settings.GetOrCreateSession(key);
            }

            _solutionEntityCache.Clear();
            _currentEntities.Clear();
            _parentAttributes.Clear();
            _childAttributes.Clear();
            _selectedRelationship = null;
            ClearChildTabsAndData();

            cmbSolution.SelectedIndex = -1;
            cmbParentEntity.DataSource = null;

            UpdateEnableStates();
            UpdateJsonPreview();
            AppendLog("Session cleared. Re-select Solution and entities.");
        }

        private void UpdateEnableStates()
        {
            var hasSolution = cmbSolution.SelectedItem != null;
            cmbParentEntity.Enabled = hasSolution && (cmbParentEntity.DataSource as List<EntityItem>)?.Any() == true;
        }

        private async Task<bool> TryLoadExistingParentConfigurationAsync(EntityItem parent)
        {
            if (!EnsureConnected() || _configurationService == null)
            {
                return false;
            }

            var tcs = new TaskCompletionSource<bool>();

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Checking for existing configuration...",
                Work = (worker, args) =>
                {
                    args.Result = _configurationService!.GetExistingConfigurationsAsync().GetAwaiter().GetResult();
                },
                PostWorkCallBack = args =>
                {
                    var applied = false;

                    if (args.Error != null)
                    {
                        AppendLog($"Failed to check configurations: {args.Error.Message}");
                        tcs.SetResult(false);
                        return;
                    }

                    var configs = args.Result as List<ConfiguredRelationship> ?? new List<ConfiguredRelationship>();
                    var parentConfig = configs
                        .FirstOrDefault(c => string.Equals(c.ParentEntity, parent.LogicalName, StringComparison.OrdinalIgnoreCase))
                        ?.Configuration;

                    if (parentConfig != null)
                    {
                        ApplyConfiguration(parentConfig).GetAwaiter().GetResult();
                        AppendLog($"Existing configuration applied for parent: {parent.DisplayName}");
                        applied = true;
                    }
                    else
                    {
                        AppendLog($"No existing configuration found for parent: {parent.DisplayName}");
                    }

                    tcs.SetResult(applied);
                }
            });

            return await tcs.Task;
        }

        private async Task LoadExistingMappingForSelectionAsync()
        {
            if (!EnsureConnected() || _configurationService == null || _selectedRelationship == null || cmbParentEntity.SelectedItem is not EntityItem parent)
            {
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Checking for existing configuration...",
                Work = (worker, args) =>
                {
                    args.Result = _configurationService!.GetExistingConfigurationsAsync().GetAwaiter().GetResult();
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null) { AppendLog($"Failed to check configurations: {args.Error.Message}"); return; }
                    var configs = args.Result as List<ConfiguredRelationship> ?? new List<ConfiguredRelationship>();
                    var match = configs.FirstOrDefault(c => string.Equals(c.ParentEntity, parent.LogicalName, StringComparison.OrdinalIgnoreCase)
                                                            && string.Equals(c.ChildEntity, _selectedRelationship.ReferencingEntity, StringComparison.OrdinalIgnoreCase)
                                                            && string.Equals(c.RelationshipName, _selectedRelationship.SchemaName, StringComparison.OrdinalIgnoreCase));
                    if (match?.Configuration != null)
                    {
                        ApplyConfiguration(match.Configuration).GetAwaiter().GetResult();
                        AppendLog("Existing mapping loaded for selected relationship.");
                    }
                }
            });

            await Task.CompletedTask;
        }

        #endregion

        #region Child Relationship and Tab Management


        private void TabControlRightUpper_Selected(object? sender, TabControlEventArgs e)
        {
            if (e.TabPage == null) return;

            // Save previous tab's filters before switching
            if (_previouslySelectedTab != null && _previouslySelectedTab != e.TabPage)
            {
                var previousTabKey = GetTabKey(_previouslySelectedTab);
                if (previousTabKey != null)
                {
                    var filters = filterControl.GetFilters();
                    _filterCriteriaPerTab[previousTabKey] = filters;
                    AppendLog($"Saved {filters.Count} filter(s) for previous tab: {previousTabKey}");
                }
            }

            // Update the tracked previous tab
            _previouslySelectedTab = e.TabPage;

            // Load the new tab's filters
            LoadTabFilters(e.TabPage);

            UpdateJsonPreview();
        }

        private void TabControlRightUpper_MouseDown(object? sender, MouseEventArgs e)
        {
            // Handle left-click on X emoji, middle-click, or right-click to close tab
            for (int i = 0; i < tabControlRightUpper.TabCount; i++)
            {
                var tabRect = tabControlRightUpper.GetTabRect(i);
                if (tabRect.Contains(e.Location))
                {
                    var tab = tabControlRightUpper.TabPages[i];
                    
                    // Check if click was on the X emoji (rightmost part of tab)
                    // X emoji is typically about 20-30 pixels wide
                    bool isClickOnX = (e.Location.X - tabRect.X) > (tabRect.Width - 30);
                    
                    if (e.Button == MouseButtons.Left && isClickOnX)
                    {
                        // Left-click on X closes directly with confirmation
                        RemoveChildTab(tab);
                    }
                    else if (e.Button == MouseButtons.Right)
                    {
                        // Show context menu for right-click
                        var contextMenu = new ContextMenuStrip();
                        contextMenu.Items.Add("Close Tab", null, (s, args) => RemoveChildTab(tab));
                        contextMenu.Show(tabControlRightUpper, e.Location);
                    }
                    else if (e.Button == MouseButtons.Middle)
                    {
                        // Middle-click closes directly
                        RemoveChildTab(tab);
                    }
                    break;
                }
            }
        }

        // Custom draw two-line tab labels: Entity name (top) + relationship schema (bottom)
        private void tabControlRightUpper_DrawItem(object? sender, DrawItemEventArgs e)
        {
            var tab = tabControlRightUpper.TabPages[e.Index];
            var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var bounds = e.Bounds;

            // Background
            using (var bg = new System.Drawing.SolidBrush(isSelected ? System.Drawing.SystemColors.ControlLightLight : System.Drawing.SystemColors.Control))
            {
                e.Graphics.FillRectangle(bg, bounds);
            }

            // Prepare text lines
            string line1 = tab.Text;
            string line2 = string.Empty;
            var tabKey = GetTabKey(tab);
            if (tabKey != null && _childRelationships.TryGetValue(tabKey, out var rel))
            {
                line2 = "(" + rel.SchemaName + ")";
            }

            var textColor1 = System.Drawing.SystemColors.ControlText;
            var textColor2 = System.Drawing.Color.DimGray;

            // Text area (leave room for close glyph on right)
            var textRect = new System.Drawing.Rectangle(bounds.X + 6, bounds.Y + 6, bounds.Width - 24, bounds.Height - 12);
            int lineHeight = (int)e.Font.GetHeight(e.Graphics);

            var line1Point = new System.Drawing.Point(textRect.X, textRect.Y);
            var line2Point = new System.Drawing.Point(textRect.X, textRect.Y + lineHeight);

            TextRenderer.DrawText(e.Graphics, line1, e.Font, line1Point, textColor1, System.Drawing.Color.Transparent);
            if (!string.IsNullOrEmpty(line2))
            {
                TextRenderer.DrawText(e.Graphics, line2, e.Font, line2Point, textColor2, System.Drawing.Color.Transparent);
            }

            // Close glyph (×) on the right
            var closePoint = new System.Drawing.Point(bounds.Right - 18, bounds.Y + 10);
            TextRenderer.DrawText(e.Graphics, "×", e.Font, closePoint, System.Drawing.Color.Gray, System.Drawing.Color.Transparent);

            e.DrawFocusRectangle();
        }

        private void RemoveChildTab(TabPage tab)
        {
            if (_childEntityTabs.Count <= 1)
            {
                MessageBox.Show("You must have at least one child relationship configured.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show($"Remove the child relationship '{tab.Text}'?", "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            // Find and remove from all dictionaries
            var tabKey = _childEntityTabs.FirstOrDefault(kvp => kvp.Value == tab).Key;
            if (tabKey != null)
            {
                _childEntityTabs.Remove(tabKey);
                _childRelationships.Remove(tabKey);
                _additionalChildMappings.Remove(tabKey);
                _filterCriteriaPerTab.Remove(tabKey);
            }

            tabControlRightUpper.TabPages.Remove(tab);
            tab.Dispose();

            UpdateJsonPreview();
            AppendLog($"Removed child relationship: {tab.Text}");
        }

        private void SaveCurrentTabFilters()
        {
            if (tabControlRightUpper.SelectedTab == null) return;

            var tabKey = GetTabKey(tabControlRightUpper.SelectedTab);
            if (tabKey != null)
            {
                var filters = filterControl.GetFilters();
                _filterCriteriaPerTab[tabKey] = filters;
            }
        }

        private void LoadCurrentTabFilters()
        {
            if (tabControlRightUpper.SelectedTab != null)
            {
                LoadTabFilters(tabControlRightUpper.SelectedTab);
            }
        }

        private string? GetTabKey(TabPage? tab)
        {
            if (tab?.Tag is null) return null;

            // Prefer the key stored in Tag
            var keyProperty = tab.Tag.GetType().GetProperty("Key");
            if (keyProperty != null)
            {
                return keyProperty.GetValue(tab.Tag) as string;
            }

            // Fallback to dictionary lookup
            return _childEntityTabs.FirstOrDefault(kvp => kvp.Value == tab).Key;
        }

        private void LoadTabFilters(TabPage tab)
        {
            var tabKey = GetTabKey(tab);
            if (tabKey != null && _filterCriteriaPerTab.TryGetValue(tabKey, out var filters))
            {
                AppendLog($"Loading {filters.Count} filter(s) for tab: {tabKey}");
                filterControl.LoadFilters(filters);
            }
            else
            {
                AppendLog($"No saved filters for tab: {tabKey ?? "(unknown)"} - clearing filters");
                filterControl.ClearFilters();
            }
            
            // Force refresh the filter grid to ensure it displays the correct data
            filterControl.Refresh();
        }

        #endregion
    }
}
