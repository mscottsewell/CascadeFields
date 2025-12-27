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
        private readonly Dictionary<string, List<EntityMetadata>> _solutionEntityCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _parentFormFields = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _childFormFields = new(StringComparer.OrdinalIgnoreCase);

        private ConfiguratorSettings _settings = new();
        private SessionSettings? _session;
        private bool _isRestoringSession; // legacy flag, used for guarded save/restore
        private bool _isApplyingSession;  // prevents duplicate restore/logging across event cascades
        private bool _restoredChildRelationship;
        private bool _waitingForParentFormLoad;  // track parent form load during restore
        private bool _waitingForChildFormLoad;   // track child form load during restore
        private List<SolutionItem> _solutions = new();
        private List<EntityMetadata> _currentEntities = new();
        private List<AttributeItem> _parentAttributes = new();
        private List<AttributeItem> _childAttributes = new();
        private RelationshipItem? _selectedRelationship;

        private MetadataService? _metadataService;
        private ConfigurationService? _configurationService;

        // Event handlers stored as fields for proper attach/detach
        private EventHandler? _solutionChangedHandler;
        private EventHandler? _parentEntityChangedHandler;
        private EventHandler? _parentFormChangedHandler;
        private EventHandler? _childEntityChangedHandler;
        private EventHandler? _childFormChangedHandler;

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

            btnLoadMetadata.Click += async (s, e) => await LoadSolutionsAsync();
            btnRetrieveConfigured.Click += async (s, e) => await RetrieveConfiguredAsync();
            btnUpdatePlugin.Click += async (s, e) => await UpdatePluginAssemblyAsync();
            btnPublish.Click += async (s, e) => await PublishConfigurationAsync();
            btnClearSession.Click += (s, e) => ClearSession();

            // Store event handlers as fields for proper detach/reattach
            _solutionChangedHandler = async (s, e) => await OnSolutionChangedAsync();
            _parentEntityChangedHandler = async (s, e) => await OnParentEntityChangedAsync();
            _parentFormChangedHandler = (s, e) => OnParentFormChanged();
            _childEntityChangedHandler = async (s, e) => await OnChildEntityChangedAsync();
            _childFormChangedHandler = (s, e) => OnChildFormChanged();

            cmbSolution.SelectedIndexChanged += _solutionChangedHandler;
            cmbParentEntity.SelectedIndexChanged += _parentEntityChangedHandler;
            cmbParentForm.SelectedIndexChanged += _parentFormChangedHandler;
            cmbChildEntity.SelectedIndexChanged += _childEntityChangedHandler;
            cmbChildForm.SelectedIndexChanged += _childFormChangedHandler;
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
            if (splitContainer.Width > 0)
            {
                splitContainer.SplitterDistance = splitContainer.Width / 2;
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
                cmbParentForm.DataSource = null;
                cmbParentForm.Enabled = false;
                cmbChildEntity.DataSource = null;
                cmbChildEntity.Enabled = false;
                cmbChildForm.DataSource = null;
                cmbChildForm.Enabled = false;
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

            // Clear entities
            _currentEntities.Clear();
            cmbParentEntity.DataSource = null;
            cmbParentEntity.Enabled = false;

            // Clear forms
            cmbParentForm.DataSource = null;
            cmbParentForm.Enabled = false;
            cmbChildForm.DataSource = null;
            cmbChildForm.Enabled = false;

            // Clear relationships
            cmbChildEntity.DataSource = null;
            cmbChildEntity.Enabled = false;
            _selectedRelationship = null;

            // Clear attributes
            _parentAttributes.Clear();
            _childAttributes.Clear();
            _parentFormFields.Clear();
            _childFormFields.Clear();

            // Clear mappings
            _mappingRows.Clear();
            _mappingRows.Add(new MappingRow());

            // Clear JSON preview
            txtJsonPreview.Text = string.Empty;
        }


        private void LoadSettings()
        {
            _settings = Services.SettingsStorage.Load() ?? new ConfiguratorSettings();
            AppendLog($"Settings loaded: {_settings.Sessions.Count} session(s) found.");
            RefreshSession();
        }

        private void SaveSettings()
        {
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
                AppendLog($"Session found - Solution: {_session.SolutionUniqueName}, Parent: {_session.ParentEntity ?? "(none)"}");
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
                _currentEntities = (args.Result as List<EntityMetadata>) ?? new List<EntityMetadata>();

                // Ensure saved parent/child entities are available even if not explicitly in the solution components
                if (_isApplyingSession && _session != null)
                {
                    var needed = new[] { _session.ParentEntity, _session.ChildEntity }
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var logicalName in needed)
                    {
                        var exists = _currentEntities.Any(e => e.LogicalName.Equals(logicalName, StringComparison.OrdinalIgnoreCase));
                        if (!exists)
                        {
                            var metadata = _metadataService!.GetEntityMetadataAsync(logicalName).GetAwaiter().GetResult();
                            if (metadata != null)
                            {
                                _currentEntities.Add(metadata);
                            }
                        }
                    }
                }
                        return;
                    }

                    _solutions = args.Result as List<SolutionItem> ?? new List<SolutionItem>();

                    // Apply session only if we actually have a saved solution
                    _isApplyingSession = !string.IsNullOrWhiteSpace(_session?.SolutionUniqueName);
                    _isRestoringSession = _isApplyingSession;
                    _restoredChildRelationship = false;
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
            cmbParentForm.DataSource = null;
            cmbParentForm.Enabled = false;
            cmbChildEntity.DataSource = null;
            cmbChildEntity.Enabled = false;
            cmbChildForm.DataSource = null;
            cmbChildForm.Enabled = false;
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
                cmbParentForm.SelectedIndex = -1;
                cmbParentForm.DataSource = null;
                cmbParentForm.Enabled = false;
                cmbChildEntity.SelectedIndex = -1;
                cmbChildEntity.DataSource = null;
                cmbChildEntity.Enabled = false;
                cmbChildForm.SelectedIndex = -1;
                cmbChildForm.DataSource = null;
                cmbChildForm.Enabled = false;
                UpdateEnableStates();
                return;
            }

            _restoredChildRelationship = false; // reset per solution change

            // When user manually changes solution (not during restore), clear all dependent selections
            if (_session != null && !_isApplyingSession && !_isRestoringSession)
            {
                AppendLog($"User changed solution to {selectedSolution.UniqueName}. Clearing dependent selections.");
                _session.SolutionUniqueName = selectedSolution.UniqueName;
                _session.SolutionId = selectedSolution.Id;
                _session.ParentEntity = null;
                _session.ParentFormId = null;
                _session.ChildEntity = null;
                _session.ChildFormId = null;
                
                // Clear UI controls
                cmbParentEntity.DataSource = null;
                cmbParentForm.DataSource = null;
                cmbChildEntity.DataSource = null;
                cmbChildForm.DataSource = null;
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

                    _isApplyingSession = true;
                    _isRestoringSession = true;
                    _restoredChildRelationship = false;
                    AppendLog($"Loaded {_currentEntities.Count} entities for solution {solutionUniqueName}. Starting BindParentEntities...");
                    
                    // Call async method synchronously since we're already in PostWorkCallBack (UI thread)
                    BindParentEntities().GetAwaiter().GetResult();
                    
                    // If no saved parent to restore, clear flags now
                    if (string.IsNullOrWhiteSpace(_session?.ParentEntity))
                    {
                        _isApplyingSession = false;
                        _isRestoringSession = false;
                        AppendLog("No saved parent entity to restore - clearing restore flags.");
                    }
                }
            });

            await Task.CompletedTask;
        }

        private void EnsureSessionEntitiesPresent()
        {
            if (_session == null)
            {
                return;
            }

            var needed = new[] { _session.ParentEntity, _session.ChildEntity }
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var logicalName in needed)
            {
                var exists = _currentEntities.Any(e => e.LogicalName.Equals(logicalName, StringComparison.OrdinalIgnoreCase));
                if (exists)
                {
                    AppendLog($"Session entity '{logicalName}' already in entity list.");
                    continue;
                }

                try
                {
                    var metadata = _metadataService?.GetEntityMetadataAsync(logicalName).GetAwaiter().GetResult();
                    if (metadata != null)
                    {
                        _currentEntities.Add(metadata);
                        AppendLog($"Loaded missing session entity '{logicalName}' into entity list.");
                    }
                    else
                    {
                        AppendLog($"Warning: Could not load metadata for saved entity '{logicalName}'.");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"Warning: Failed to load metadata for saved entity '{logicalName}': {ex.Message}");
                }
            }
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

            AppendLog($"BindParentEntities: _isApplyingSession={_isApplyingSession}, _session.ParentEntity={_session?.ParentEntity ?? "(null)"}, items.Count={items.Count}");

            if (_session != null && !string.IsNullOrWhiteSpace(_session.ParentEntity) && _isApplyingSession)
            {
                var match = items.FirstOrDefault(i => i.LogicalName.Equals(_session.ParentEntity, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    AppendLog($"Restoring saved parent entity: {match.DisplayName}");
                    cmbParentEntity.SelectedValue = match.LogicalName;
                    // Re-attach handler
                    if (_parentEntityChangedHandler != null)
                    {
                        cmbParentEntity.SelectedIndexChanged += _parentEntityChangedHandler;
                    }
                    
                    // Directly trigger cascade loads with the matched entity
                    AppendLog($"Triggering child relationships and form loads for parent: {match.LogicalName}");
                    _waitingForParentFormLoad = true;
                    _waitingForChildFormLoad = !string.IsNullOrWhiteSpace(_session?.ChildEntity);
                    _ = BindChildRelationships(match);
                    _ = LoadFormsAsync(match.LogicalName, isParent: true);
                    
                    UpdateEnableStates();
                    return;
                }
                AppendLog($"Warning: Saved parent entity '{_session.ParentEntity}' not found in loaded entities ({items.Count} entities).");
            }

            // Re-attach handler
            if (_parentEntityChangedHandler != null)
            {
                cmbParentEntity.SelectedIndexChanged += _parentEntityChangedHandler;
            }

            // No default selection when no saved session; wait for user.
            cmbParentEntity.SelectedIndex = -1;
            cmbParentForm.DataSource = null;
            cmbParentForm.Enabled = false;
            cmbChildEntity.DataSource = null;
            cmbChildEntity.Enabled = false;
            cmbChildForm.DataSource = null;
            cmbChildForm.Enabled = false;

            UpdateEnableStates();
        }

        private async System.Threading.Tasks.Task OnParentEntityChangedAsync()
        {
            // If we're binding and have no saved parent, ignore the initial SelectedIndexChanged fired by data binding
            if (_isApplyingSession && string.IsNullOrWhiteSpace(_session?.ParentEntity))
            {
                return;
            }

            _parentAttributes.Clear();
            _childAttributes.Clear();
            _parentFormFields.Clear();
            _selectedRelationship = null;
            cmbChildEntity.DataSource = null;
            cmbParentForm.DataSource = null;
            cmbChildForm.DataSource = null;
            _mappingRows.Clear();

            if (cmbParentEntity.SelectedItem is not EntityItem parent)
            {
                cmbParentForm.Enabled = false;
                cmbChildEntity.Enabled = false;
                cmbChildForm.Enabled = false;
                return;
            }

            if (_session != null && !_isRestoringSession)
            {
                _session.ParentEntity = parent.LogicalName;
                _session.ParentFormId = null;
                _session.ChildEntity = null;
                _session.ChildFormId = null;
                SaveSettings();
            }

            _ = BindChildRelationships(parent);
            _ = LoadFormsAsync(parent.LogicalName, isParent: true);
            RefreshAttributeLists();
            UpdateEnableStates();

            // If applying a saved session and there is no saved child, stop applying
            if (_isApplyingSession && string.IsNullOrWhiteSpace(_session?.ChildEntity))
            {
                _isApplyingSession = false;
                _isRestoringSession = false;
            }
            await Task.CompletedTask;
        }

        private async System.Threading.Tasks.Task BindChildRelationships(EntityItem parent)
        {
            if (_metadataService == null)
            {
                return;
            }

            var relationships = _metadataService.GetChildRelationships(parent.Metadata, _currentEntities).ToList();
            AppendLog($"BindChildRelationships for {parent.LogicalName}: found {relationships.Count} relationships, _isApplyingSession={_isApplyingSession}, saved child={_session?.ChildEntity ?? "(null)"}");
            
            // Temporarily remove handler to prevent events during binding
            if (_childEntityChangedHandler != null)
            {
                cmbChildEntity.SelectedIndexChanged -= _childEntityChangedHandler;
            }
            
            cmbChildEntity.DataSource = relationships;
            cmbChildEntity.DisplayMember = nameof(RelationshipItem.DisplayText);
            cmbChildEntity.ValueMember = nameof(RelationshipItem.ReferencingEntity);
            cmbChildEntity.Enabled = relationships.Count > 0;

            if (_session != null && !string.IsNullOrWhiteSpace(_session.ChildEntity) && _isApplyingSession && !_restoredChildRelationship)
            {
                var match = relationships.FirstOrDefault(r => r.ReferencingEntity.Equals(_session.ChildEntity, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    AppendLog($"Restoring saved child relationship: {match.DisplayName}");
                    _restoredChildRelationship = true;
                    cmbChildEntity.SelectedValue = match.ReferencingEntity;
                    
                    // CRITICAL: Set _selectedRelationship since the handler was detached when SelectedValue was set
                    _selectedRelationship = match;
                    AppendLog($"[BindChild] Manually set _selectedRelationship to {match.ReferencingEntity}");
                    
                    // Re-attach handler
                    if (_childEntityChangedHandler != null)
                    {
                        cmbChildEntity.SelectedIndexChanged += _childEntityChangedHandler;
                    }
                    // Manually trigger to load child forms (flags will be cleared when child form load completes)
                    _ = LoadFormsAsync(match.ReferencingEntity, isParent: false);
                    UpdateEnableStates();
                    return;
                }
                AppendLog($"Warning: Saved child entity '{_session.ChildEntity}' not found in relationships ({relationships.Count} relationships).");
                // Clear flags if we couldn't restore child
                if (_isApplyingSession)
                {
                    AppendLog($"Clearing flags due to missing child entity.");
                    _isApplyingSession = false;
                    _isRestoringSession = false;
                }
            }

            // Re-attach handler
            if (_childEntityChangedHandler != null)
            {
                cmbChildEntity.SelectedIndexChanged += _childEntityChangedHandler;
            }

            cmbChildEntity.SelectedIndex = -1;
            cmbChildForm.DataSource = null;
            cmbChildForm.Enabled = false;
        }

        private async System.Threading.Tasks.Task OnChildEntityChangedAsync()
        {
            AppendLog($"OnChildEntityChangedAsync: _isApplyingSession={_isApplyingSession}, _session.ChildEntity={_session?.ChildEntity ?? "(null)"}");
            
            // If we're binding and have no saved child, ignore the initial SelectedIndexChanged fired by data binding
            if (_isApplyingSession && string.IsNullOrWhiteSpace(_session?.ChildEntity))
            {
                return;
            }

            _childAttributes.Clear();
            _childFormFields.Clear();
            cmbChildForm.DataSource = null;
            _selectedRelationship = cmbChildEntity.SelectedItem as RelationshipItem;
            _mappingRows.Clear();

            if (_session != null && !_isApplyingSession && !_isRestoringSession)
            {
                _session.ChildEntity = _selectedRelationship?.ReferencingEntity;
                _session.ChildFormId = null;
                SaveSettings();
            }

            if (_selectedRelationship != null)
            {
                await LoadFormsAsync(_selectedRelationship.ReferencingEntity, isParent: false);
                RefreshAttributeLists();
                await LoadExistingMappingForSelectionAsync();
            }

            UpdateEnableStates();

            // Done applying session once child entity processed
            if (_isApplyingSession)
            {
                AppendLog($"OnChildEntityChangedAsync: Clearing restore flags.");
                _isApplyingSession = false;
                _isRestoringSession = false;
                AppendLog($"Flags cleared. Ready for user interaction.");
            }
            await Task.CompletedTask;
        }

        private void OnParentFormChanged()
        {
            _parentFormFields.Clear();
            if (cmbParentForm.SelectedItem is FormItem form)
            {
                _parentFormFields.UnionWith(_metadataService?.GetFieldsFromFormXml(form.FormXml) ?? Enumerable.Empty<string>());
                if (_session != null && !_isApplyingSession && !_isRestoringSession)
                {
                    _session.ParentFormId = form.Id;
                    SaveSettings();
                }
            }
            RefreshAttributeLists();
            UpdateEnableStates();
        }

        private void OnChildFormChanged()
        {
            _childFormFields.Clear();
            if (cmbChildForm.SelectedItem is FormItem form)
            {
                _childFormFields.UnionWith(_metadataService?.GetFieldsFromFormXml(form.FormXml) ?? Enumerable.Empty<string>());
                AppendLog($"[OnChildForm] Form '{form.Name}' selected, extracted {_childFormFields.Count} fields");
                if (_session != null && !_isApplyingSession && !_isRestoringSession)
                {
                    _session.ChildFormId = form.Id;
                    SaveSettings();
                }
            }
            else
            {
                AppendLog($"[OnChildForm] No form selected");
            }
            RefreshAttributeLists();
            RefreshDestinationFieldDropdowns();
            UpdateEnableStates();
        }

        private void RefreshDestinationFieldDropdowns()
        {
            AppendLog($"[RefreshDestFields] Refreshing destination field dropdowns for {gridMappings.Rows.Count} rows");
            
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
                AppendLog($"[RefreshDestFields] Row {i}: Source='{sourceLogical}', Found {compatibleAttributes.Count} compatible attributes");
                
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
                        
                        if (stillValid)
                        {
                            // Keep the current selection
                            AppendLog($"[RefreshDestFields] Row {i}: Keeping current target '{currentTargetValue}' (still valid)");
                        }
                        else
                        {
                            // Clear the selection as it's no longer valid
                            targetCell.Value = null;
                            AppendLog($"[RefreshDestFields] Row {i}: Cleared target '{currentTargetValue}' (no longer valid)");
                        }
                    }
                    
                    gridMappings.InvalidateCell(targetCell);
                }
            }
            
            UpdateJsonPreview();
        }

        private async System.Threading.Tasks.Task LoadFormsAsync(string logicalName, bool isParent)
        {
            if (_metadataService == null) return;

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Loading forms for {logicalName}...",
                Work = (worker, args) =>
                {
                    AppendLog($"Loading forms for {logicalName}...");
                    args.Result = _metadataService.GetMainFormsAsync(logicalName).GetAwaiter().GetResult();
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        AppendLog($"Failed to load forms for {logicalName}: {args.Error.Message}");
                        return;
                    }

                    var forms = args.Result as List<FormItem> ?? new List<FormItem>();
                    AppendLog($"Loaded {forms.Count} forms for {logicalName}");
                        
                        var combo = isParent ? cmbParentForm : cmbChildForm;
                        combo.DataSource = forms;
                        combo.DisplayMember = nameof(FormItem.Name);
                        combo.ValueMember = nameof(FormItem.Id);
                        combo.Enabled = forms.Count > 0;

                        var targetId = _session != null ? (isParent ? _session.ParentFormId : _session.ChildFormId) : null;
                        if ((_isApplyingSession || _isRestoringSession) && targetId.HasValue)
                        {
                            var match = forms.FirstOrDefault(f => f.Id == targetId.Value);
                            if (match != null)
                            {
                                AppendLog($"Restoring saved {(isParent ? "parent" : "child")} form: {match.Name}");
                                combo.SelectedItem = match;
                            }
                            else
                            {
                                AppendLog($"Warning: Saved {(isParent ? "parent" : "child")} form ID {targetId.Value} not found");
                                combo.SelectedIndex = -1;
                            }
                        }
                        else
                        {
                            combo.SelectedIndex = -1;
                        }

                    UpdateEnableStates();
                    
                    // Mark this form load as complete
                    if (isParent)
                    {
                        _waitingForParentFormLoad = false;
                        AppendLog($"Parent form load complete. Waiting for child: {_waitingForChildFormLoad}");
                    }
                    else
                    {
                        _waitingForChildFormLoad = false;
                        AppendLog($"Child form load complete. Waiting for parent: {_waitingForParentFormLoad}");
                    }
                    
                    // Clear restore flags only after BOTH parent and child forms are loaded
                    if ((_isApplyingSession || _isRestoringSession) && !_waitingForParentFormLoad && !_waitingForChildFormLoad)
                    {
                        AppendLog($"All forms loaded during restore. Clearing flags: _isApplyingSession={_isApplyingSession}, _isRestoringSession={_isRestoringSession}");
                        
                        // Restore field mappings from session
                        if (_session?.FieldMappings != null && _session.FieldMappings.Any())
                        {
                            AppendLog($"Restoring {_session.FieldMappings.Count} field mapping(s) from session");
                            _mappingRows.Clear();
                            
                            foreach (var mapping in _session.FieldMappings)
                            {
                                _mappingRows.Add(new MappingRow
                                {
                                    SourceField = mapping.SourceField,
                                    TargetField = mapping.TargetField,
                                    IsTriggerField = mapping.IsTriggerField
                                });
                            }
                            
                            // Add blank row if needed
                            if (_mappingRows.Count == 0 || 
                                (!string.IsNullOrWhiteSpace(_mappingRows[_mappingRows.Count - 1].SourceField) && 
                                 !string.IsNullOrWhiteSpace(_mappingRows[_mappingRows.Count - 1].TargetField)))
                            {
                                _mappingRows.Add(new MappingRow());
                            }
                        }
                        
                        _isApplyingSession = false;
                        _isRestoringSession = false;
                        AppendLog($"Flags cleared. Ready for user interaction.");
                    }
                }
            });

            await Task.CompletedTask;
        }

        private void RefreshAttributeLists()
        {
            if (_metadataService == null)
            {
                AppendLog("[RefreshAttr] MetadataService is null");
                return;
            }

            var parentMeta = (cmbParentEntity.SelectedItem as EntityItem)?.Metadata;
            var childMeta = _selectedRelationship != null
                ? _currentEntities.FirstOrDefault(e => e.LogicalName.Equals(_selectedRelationship.ReferencingEntity, StringComparison.OrdinalIgnoreCase))
                : null;

            AppendLog($"[RefreshAttr] Parent={parentMeta?.LogicalName ?? "(null)"}, ParentFormFields={_parentFormFields.Count}, Child={childMeta?.LogicalName ?? "(null)"}, ChildFormFields={_childFormFields.Count}, SelectedRel={_selectedRelationship?.ReferencingEntity ?? "(null)"}");

            _parentAttributes = parentMeta != null
                ? _metadataService.GetAttributeItems(parentMeta, _parentFormFields).ToList()
                : new List<AttributeItem>();

            _childAttributes = childMeta != null
                ? _metadataService.GetAttributeItems(childMeta, _childFormFields).ToList()
                : new List<AttributeItem>();

            AppendLog($"[RefreshAttr] Loaded {_parentAttributes.Count} parent attributes, {_childAttributes.Count} child attributes");

            BindAttributeColumns();
            UpdateJsonPreview();
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

        private void GridMappings_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (e.ColumnIndex == colSourceField.Index)
            {
                var sourceLogical = gridMappings.Rows[e.RowIndex].Cells[colSourceField.Index].Value as string;
                var targetCell = gridMappings.Rows[e.RowIndex].Cells[colTargetField.Index] as DataGridViewComboBoxCell;
                
                AppendLog($"[Grid] CellValueChanged: Row={e.RowIndex}, Source={sourceLogical ?? "(null)"}");
                
                // Clear and disable target field if no source selected
                if (string.IsNullOrWhiteSpace(sourceLogical))
                {
                    targetCell!.Value = null;
                    targetCell.ReadOnly = true;
                    targetCell.DataSource = null;
                    AppendLog($"[Grid] Source empty - cleared target cell");
                }
                else
                {
                    targetCell!.ReadOnly = false;
                    
                    // Get compatible attributes and populate the cell
                    var compatibleAttributes = GetCompatibleChildAttributes(sourceLogical).ToList();
                    AppendLog($"[Grid] Found {compatibleAttributes.Count} compatible attributes for source '{sourceLogical}'");
                    
                    // Use Items collection - set DisplayMember/ValueMember FIRST, then populate
                    targetCell.DataSource = null;
                    targetCell.DisplayMember = nameof(AttributeItem.DisplayName);
                    targetCell.ValueMember = nameof(AttributeItem.LogicalName);
                    targetCell.Items.Clear();
                    
                    foreach (var attr in compatibleAttributes)
                    {
                        targetCell.Items.Add(attr);
                    }
                    
                    AppendLog($"[Grid] Populated target cell with {targetCell.Items.Count} items");
                }
                
                // Force the cell and row to refresh
                gridMappings.InvalidateCell(targetCell);
                gridMappings.InvalidateRow(e.RowIndex);
            }
            
            // Check if we should add a new row
            CheckAndAddNewRow(e.RowIndex);

            UpdateJsonPreview();
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
            // Handle delete button click
            if (e.RowIndex >= 0 && e.ColumnIndex == colDelete.Index)
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
            // Initialize cells for newly added rows
            for (int i = e.RowIndex; i < e.RowIndex + e.RowCount; i++)
            {
                if (i < gridMappings.Rows.Count && i < _mappingRows.Count)
                {
                    var row = _mappingRows[i];
                    var targetCell = gridMappings.Rows[i].Cells[colTargetField.Index] as DataGridViewComboBoxCell;
                    
                    if (targetCell != null)
                    {
                        // If the row has a source field, enable and populate the target cell
                        if (!string.IsNullOrWhiteSpace(row.SourceField))
                        {
                            targetCell.ReadOnly = false;
                            var compatibleAttributes = GetCompatibleChildAttributes(row.SourceField).ToList();
                            AppendLog($"[Grid] RowsAdded: Row={i}, Source={row.SourceField}, Found {compatibleAttributes.Count} compatible attributes");
                            
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
                        
                        gridMappings.InvalidateCell(targetCell);
                    }
                }
            }
        }

        private void GridMappings_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            // Auto-open dropdown on first click for ComboBox cells
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var cell = gridMappings.Rows[e.RowIndex].Cells[e.ColumnIndex];
            
            // Don't allow clicking on disabled target field
            if (cell is DataGridViewComboBoxCell comboCell && comboCell.ReadOnly)
            {
                return;
            }
            
            if (cell is DataGridViewComboBoxCell)
            {
                // Begin edit mode if not already in it
                if (!gridMappings.IsCurrentCellInEditMode)
                {
                    gridMappings.BeginEdit(true);
                }

                // Show dropdown if we're editing a ComboBox
                if (gridMappings.EditingControl is ComboBox combo)
                {
                    combo.DroppedDown = true;
                }
            }
        }

        private void GridMappings_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (gridMappings.CurrentCell == null) return;
            if (gridMappings.CurrentCell.ColumnIndex == colSourceField.Index && e.Control is ComboBox sourceCombo)
            {
                sourceCombo.DataSource = _parentAttributes;
                sourceCombo.DisplayMember = nameof(AttributeItem.DisplayName);
                sourceCombo.ValueMember = nameof(AttributeItem.LogicalName);
            }
            else if (gridMappings.CurrentCell.ColumnIndex == colTargetField.Index && e.Control is ComboBox targetCombo)
            {
                // Don't override with DataSource - the cell's Items collection is already populated
                // in CellValueChanged or RowsAdded event handlers
                var sourceLogical = gridMappings.CurrentRow?.Cells[colSourceField.Index].Value as string;
                AppendLog($"[Grid] EditingControlShowing: Target column, Source={sourceLogical ?? "(null)"}");
            }
        }

        private IEnumerable<AttributeItem> GetCompatibleChildAttributes(string? sourceLogical)
        {
            if (string.IsNullOrWhiteSpace(sourceLogical))
            {
                AppendLog($"[GetCompatible] Source is null/empty");
                return Enumerable.Empty<AttributeItem>();
            }

            AppendLog($"[GetCompatible] Looking for source '{sourceLogical}' in {_parentAttributes.Count} parent attributes");
            var sourceAttr = _parentAttributes.FirstOrDefault(a => a.LogicalName.Equals(sourceLogical, StringComparison.OrdinalIgnoreCase))?.Metadata;
            if (sourceAttr == null)
            {
                AppendLog($"[GetCompatible] Source attribute '{sourceLogical}' NOT FOUND in parent attributes!");
                return Enumerable.Empty<AttributeItem>();
            }

            AppendLog($"[GetCompatible] Source found: {sourceAttr.LogicalName} (Type: {sourceAttr.AttributeType}), checking against {_childAttributes.Count} child attributes");
            var compatible = _childAttributes.Where(child => IsCompatible(sourceAttr, child.Metadata)).ToList();
            AppendLog($"[GetCompatible] Compatibility check complete: {compatible.Count} compatible child attributes found");
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
            if (cmbParentEntity.SelectedItem is not EntityItem parent || _selectedRelationship == null)
            {
                txtJsonPreview.Text = "Select a parent and child entity to preview configuration.";
                return;
            }

            var mappings = _mappingRows
                .Where(m => !string.IsNullOrWhiteSpace(m.SourceField) && !string.IsNullOrWhiteSpace(m.TargetField))
                .Select(m => new FieldMapping
                {
                    SourceField = m.SourceField!,
                    TargetField = m.TargetField!,
                    IsTriggerField = m.IsTriggerField
                })
                .ToList();

            // Save mappings to session
            if (_session != null && !_isApplyingSession && !_isRestoringSession)
            {
                _session.FieldMappings = _mappingRows
                    .Where(m => !string.IsNullOrWhiteSpace(m.SourceField) || !string.IsNullOrWhiteSpace(m.TargetField))
                    .Select(m => new SavedFieldMapping
                    {
                        SourceField = m.SourceField,
                        TargetField = m.TargetField,
                        IsTriggerField = m.IsTriggerField
                    })
                    .ToList();
                SaveSettings();
            }

            if (!mappings.Any())
            {
                txtJsonPreview.Text = "Add at least one field mapping to preview configuration.";
                return;
            }

            var config = new CascadeConfigurationModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{parent.DisplayName} to {_selectedRelationship.DisplayName}",
                ParentEntity = parent.LogicalName,
                RelatedEntities = new List<RelatedEntityConfig>
                {
                    new RelatedEntityConfig
                    {
                        EntityName = _selectedRelationship.ReferencingEntity,
                        RelationshipName = _selectedRelationship.SchemaName,
                        LookupFieldName = _selectedRelationship.ReferencingAttribute,
                        UseRelationship = true,
                        FieldMappings = mappings
                    }
                },
                EnableTracing = true,
                IsActive = true
            };

            txtJsonPreview.Text = JsonConvert.SerializeObject(config, Formatting.Indented);
        }

        private async System.Threading.Tasks.Task RetrieveConfiguredAsync()
        {
            if (!EnsureConnected()) return;
            EnsureServices();

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

                    using var dialog = new ConfigurationPickerDialog(configs);
                    if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedConfiguration?.Configuration != null)
                    {
                        ApplyConfiguration(dialog.SelectedConfiguration.Configuration);
                    }
                }
            });

            await Task.CompletedTask;
        }

        private void ApplyConfiguration(CascadeConfigurationModel configuration)
        {
            if (configuration == null)
            {
                return;
            }

            AppendLog($"Applying configuration for parent '{configuration.ParentEntity}'.");

            // Align parent entity selection
            if (cmbParentEntity.DataSource is List<EntityItem> parents)
            {
                var match = parents.FirstOrDefault(p => p.LogicalName.Equals(configuration.ParentEntity, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    cmbParentEntity.SelectedItem = match;
                }
            }

            var related = configuration.RelatedEntities?.FirstOrDefault();
            if (related == null)
            {
                return;
            }

            // Align child entity selection
            if (cmbChildEntity.DataSource is List<RelationshipItem> relationships)
            {
                var match = relationships.FirstOrDefault(r => r.SchemaName.Equals(related.RelationshipName, StringComparison.OrdinalIgnoreCase)
                                                              || r.ReferencingEntity.Equals(related.EntityName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    cmbChildEntity.SelectedItem = match;
                }
            }

            _mappingRows.Clear();
            foreach (var mapping in related.FieldMappings)
            {
                _mappingRows.Add(new MappingRow
                {
                    SourceField = mapping.SourceField,
                    TargetField = mapping.TargetField,
                    IsTriggerField = mapping.IsTriggerField
                });
            }

            UpdateJsonPreview();
        }

        private async System.Threading.Tasks.Task PublishConfigurationAsync()
        {
            if (!EnsureConnected()) return;
            EnsureServices();

            if (_selectedRelationship == null || cmbParentEntity.SelectedItem is not EntityItem parent)
            {
                MessageBox.Show("Select a parent and child entity before publishing.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show("This will create or update the CascadeFields step and pre-image for the selected parent/child relationship. Continue?",
                "Confirm Publish", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            var mappings = _mappingRows.Where(m => !string.IsNullOrWhiteSpace(m.SourceField) && !string.IsNullOrWhiteSpace(m.TargetField)).ToList();
            if (!mappings.Any())
            {
                MessageBox.Show("Add at least one field mapping before publishing.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var config = new CascadeConfigurationModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{parent.DisplayName} to {_selectedRelationship.DisplayName}",
                ParentEntity = parent.LogicalName,
                RelatedEntities = new List<RelatedEntityConfig>
                {
                    new RelatedEntityConfig
                    {
                        EntityName = _selectedRelationship.ReferencingEntity,
                        RelationshipName = _selectedRelationship.SchemaName,
                        LookupFieldName = _selectedRelationship.ReferencingAttribute,
                        UseRelationship = true,
                        FieldMappings = mappings.Select(m => new FieldMapping
                        {
                            SourceField = m.SourceField!,
                            TargetField = m.TargetField!,
                            IsTriggerField = m.IsTriggerField
                        }).ToList()
                    }
                },
                EnableTracing = true,
                IsActive = true
            };

            var progress = new Progress<string>(msg => AppendLog(msg));

            SetBusy(true);

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Publishing configuration to Dataverse...",
                Work = (worker, args) =>
                {
                    _configurationService!.PublishConfigurationAsync(config, progress, CancellationToken.None).GetAwaiter().GetResult();
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
                    AppendLog("Publish complete: step and pre-image upserted.");
                    MessageBox.Show("Publish complete: step and pre-image upserted.", "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    txtJsonPreview.Text = args.Result as string ?? txtJsonPreview.Text;
                }
            });

            await Task.CompletedTask;
        }

        private async System.Threading.Tasks.Task UpdatePluginAssemblyAsync()
        {
            if (!EnsureConnected()) return;
            EnsureServices();

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

        private string? ResolvePluginAssemblyPath()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var baseDir = string.IsNullOrWhiteSpace(assemblyLocation) ? null : Path.GetDirectoryName(assemblyLocation);
            
            AppendLog($"[ResolvePlugin] BaseDirectory: {baseDir ?? "(null)"}");
            
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

            for (int i = 0; i < candidates.Count; i++)
            {
                var path = candidates[i];
                var exists = File.Exists(path);
                AppendLog($"[ResolvePlugin] Candidate {i + 1}: {path} - Exists: {exists}");
                if (exists)
                {
                    AppendLog($"[ResolvePlugin] Found plugin at: {path}");
                    return path;
                }
            }
            
            AppendLog($"[ResolvePlugin] Plugin not found. Checked {candidates.Count} locations.");
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
            _parentFormFields.Clear();
            _childFormFields.Clear();
            _selectedRelationship = null;
            _mappingRows.Clear();

            cmbSolution.SelectedIndex = -1;
            cmbParentEntity.DataSource = null;
            cmbParentForm.DataSource = null;
            cmbChildEntity.DataSource = null;
            cmbChildForm.DataSource = null;

            UpdateEnableStates();
            UpdateJsonPreview();
            AppendLog("Session cleared. Re-select Solution and entities.");
        }

        private void UpdateEnableStates()
        {
            var hasSolution = cmbSolution.SelectedItem != null;
            cmbParentEntity.Enabled = hasSolution && (cmbParentEntity.DataSource as List<EntityItem>)?.Any() == true;
            var hasParent = cmbParentEntity.SelectedItem != null;
            cmbParentForm.Enabled = hasParent && (cmbParentForm.DataSource as List<FormItem>)?.Any() == true;
            cmbChildEntity.Enabled = hasParent && (cmbChildEntity.DataSource as List<RelationshipItem>)?.Any() == true;
            var hasChild = cmbChildEntity.SelectedItem != null;
            cmbChildForm.Enabled = hasChild && (cmbChildForm.DataSource as List<FormItem>)?.Any() == true;
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
                        ApplyConfiguration(match.Configuration);
                        AppendLog("Existing mapping loaded for selected relationship.");
                    }
                }
            });

            await Task.CompletedTask;
        }

        #endregion
    }
}
