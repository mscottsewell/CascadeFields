using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using Newtonsoft.Json;
using CascadeFields.Configurator.Services;
using McTools.Xrm.Connection;
using PluginModels = CascadeFields.Plugin.Models;
using WinFormsLabel = System.Windows.Forms.Label;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using XrmToolBox.Extensibility;

namespace CascadeFields.Configurator
{
    public class CascadeFieldsConfiguratorControl : PluginControlBase
    {
        private readonly ComboBox _solutionCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox _entityCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox _targetFormCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill }; // optional
        private readonly ComboBox _lookupCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox _parentTargetCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox _sourceFormCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill }; // optional
        private readonly TextBox _assemblyPath = new TextBox { Dock = DockStyle.Fill };
        private readonly Button _browseAssemblyButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        private readonly DataGridView _mappingGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false };
        private readonly Button _addRowButton = new Button { Text = "Add Mapping", Dock = DockStyle.Fill };
        private readonly Button _removeRowButton = new Button { Text = "Remove Selected", Dock = DockStyle.Fill };
        private readonly Button _deployButton = new Button { Text = "Save && Deploy", Dock = DockStyle.Fill };
        private readonly TextBox _logBox = new TextBox { Multiline = true, Dock = DockStyle.Fill, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly TextBox _configurationPreview = new TextBox { Multiline = true, Dock = DockStyle.Fill, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly TabControl _parentTabs = new TabControl { Dock = DockStyle.Fill, Height = 34, SizeMode = TabSizeMode.FillToRight, Appearance = TabAppearance.Normal, Multiline = true }; 

        private MetadataService? _metadataService;
        private RegistrationService? _registrationService;
        private ConfiguratorSettings _settings = new ConfiguratorSettings();
        private bool _isRestoringMappings;
        private string? _lastLoadedAttributesKey;
        private bool _suppressParentTabChange;
        private bool _isLoadingAttributes;
        private string? _currentAttributesKey;
        private string? _lastLookupLoadKey;
        private bool _suppressTabSync;
        private bool _isRestoringSettings;
        private bool _isLoading; // Prevent concurrent restore operations
        private bool _eventsHooked; // Track whether event handlers have been attached

        private List<SolutionOption> _solutions = new List<SolutionOption>();
        private List<EntityOption> _entities = new List<EntityOption>();
        private List<FormOption> _targetForms = new List<FormOption>();
        private List<FormOption> _sourceForms = new List<FormOption>();
        private List<LookupFieldOption> _lookups = new List<LookupFieldOption>();
        private List<AttributeOption> _parentAttributes = new List<AttributeOption>();
        private List<AttributeOption> _childAttributes = new List<AttributeOption>();
        private List<AttributeOption> _filteredParentAttributes = new List<AttributeOption>();
        private List<AttributeOption> _filteredChildAttributes = new List<AttributeOption>();

        private readonly Dictionary<string, List<FormOption>> _formCache = new Dictionary<string, List<FormOption>>(StringComparer.OrdinalIgnoreCase);

        private FormOption? _selectedTargetForm;
        private FormOption? _selectedSourceForm;

        private string? _selectedParentEntity;
        private string _settingsKey = "default";

        public CascadeFieldsConfiguratorControl()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            
            if (_isLoading)
            {
                Log("[OnLoad] Already loading, skipping duplicate call");
                return;
            }
            _isLoading = true;
            Log("[OnLoad] Starting load sequence");
            
            _settingsKey = GetSettingsKey();
            Log($"Loading settings with key: {_settingsKey}");
            SettingsManager.Instance.TryLoad(GetType(), out _settings, _settingsKey);
            Log($"[OnLoad] After TryLoad: SolutionId={_settings?.SolutionId}, UniqueName={_settings?.SolutionUniqueName}");
            Log($"[OnLoad]   ChildEntity={_settings?.ChildEntityLogicalName}, Lookup={_settings?.LookupFieldLogicalName}");
            Log($"[OnLoad]   ParentEntity={_settings?.ParentEntityLogicalName}, TargetForm={_settings?.TargetFormId}, SourceForm={_settings?.SourceFormId}");
            EnsureSettings();
            HookEvents();
            RefreshServices();
            
            // Only load solutions if UpdateConnection hasn't already done it
            if (Service != null)
            {
                Log("[OnLoad] Service already available, skipping LoadSolutions (UpdateConnection handled it)");
                _isLoading = false;
            }
            else
            {
                Log("[OnLoad] Service not available, calling LoadSolutions");
                LoadSolutions();
                // _isLoading will be cleared when restore completes in BindSourceForms
            }
            // RestoreSettings will be called after solutions load
            
            Log("[OnLoad] Completed load sequence");
        }

        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);
            
            if (_isLoading)
            {
                Log("[UpdateConnection] Already loading, skipping duplicate call");
                return;
            }
            _isLoading = true;
            Log("[UpdateConnection] Starting load sequence");
            
            _settingsKey = GetSettingsKey(detail);
            Log($"Loading settings for connection with key: {_settingsKey}");
            SettingsManager.Instance.TryLoad(GetType(), out _settings, _settingsKey);
            Log($"[UpdateConnection] After TryLoad: SolutionId={_settings?.SolutionId}, UniqueName={_settings?.SolutionUniqueName}");
            Log($"[UpdateConnection]   ChildEntity={_settings?.ChildEntityLogicalName}, Lookup={_settings?.LookupFieldLogicalName}");
            Log($"[UpdateConnection]   ParentEntity={_settings?.ParentEntityLogicalName}, TargetForm={_settings?.TargetFormId}, SourceForm={_settings?.SourceFormId}");
            EnsureSettings();
            RestoreSettings();
            RefreshServices();
            LoadSolutions();
            // _isLoading will be cleared when restore completes in BindSourceForms
            
            Log("[UpdateConnection] Started load sequence, waiting for async completion");
        }

        public override void ClosingPlugin(PluginCloseInfo info)
        {
            try
            {
                Log($"[ClosingPlugin] INVOKED with key: {_settingsKey}");
                SaveSettings();
                Log("[ClosingPlugin] SaveSettings completed");
            }
            catch (Exception ex)
            {
                Log($"[ClosingPlugin] Exception in SaveSettings: {ex.Message}");
            }
            finally
            {
                Log("[ClosingPlugin] Calling base.ClosingPlugin");
                base.ClosingPlugin(info);
            }
        }

        private void RefreshServices()
        {
            if (Service != null)
            {
                _metadataService = new MetadataService(Service);
                _registrationService = new RegistrationService(Service);
            }
        }

        private void HookEvents()
        {
            if (_eventsHooked)
            {
                Log("[HookEvents] Events already hooked, skipping");
                return;
            }
            
            Log("[HookEvents] Attaching event handlers");
            _eventsHooked = true;
            
            _solutionCombo.SelectedIndexChanged += (s, e) => 
            { 
                var selected = _solutionCombo.SelectedItem as SolutionOption;
                Log($"[Event] Solution selected: {selected?.FriendlyName} ({selected?.Id})");
                LoadEntities();
            };
            _entityCombo.SelectedIndexChanged += (s, e) => { LoadTargetForms(); LoadLookupFields(); };
            _targetFormCombo.SelectedIndexChanged += (s, e) => { OnTargetFormChanged(); };
            _lookupCombo.SelectedIndexChanged += (s, e) => LoadParentTargets();
            _parentTargetCombo.SelectedIndexChanged += (s, e) =>
            {
                SyncTabToParentCombo();

                if (_parentTargetCombo.SelectedItem is EntityOption parentOption)
                {
                    _selectedParentEntity = parentOption.LogicalName;
                    _settings.ParentEntityLogicalName = _selectedParentEntity;
                }

                LoadSourceForms();
                LoadAttributesForMapping();
            };
            _sourceFormCombo.SelectedIndexChanged += (s, e) => { _selectedSourceForm = _sourceFormCombo.SelectedItem as FormOption; RebindAttributesForForms(); };
            _parentTabs.SelectedIndexChanged += ParentTabsOnSelectedIndexChanged;
            _browseAssemblyButton.Click += BrowseAssemblyButtonOnClick;
            _addRowButton.Click += (s, e) => AddMappingRow();
            _removeRowButton.Click += (s, e) => RemoveSelectedRow();
            _deployButton.Click += DeployButtonOnClick;
            _mappingGrid.DataError += MappingGridOnDataError;
            _mappingGrid.EditingControlShowing += MappingGridOnEditingControlShowing;
            _mappingGrid.CellValueChanged += (s, e) => UpdateConfigurationPreview();
            _mappingGrid.RowsAdded += (s, e) => UpdateConfigurationPreview();
            _mappingGrid.RowsRemoved += (s, e) => UpdateConfigurationPreview();
            _mappingGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_mappingGrid.IsCurrentCellDirty)
                {
                    _mappingGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }

                UpdateConfigurationPreview();
            };
        }

        private void InitializeComponent()
        {
            _addRowButton.Margin = new Padding(3);
            _removeRowButton.Margin = new Padding(3);
            _deployButton.Margin = new Padding(3);

            var ribbon = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6)
            };

            ribbon.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ribbon.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ribbon.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ribbon.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            ribbon.Controls.Add(_addRowButton, 0, 0);
            ribbon.Controls.Add(_removeRowButton, 1, 0);
            ribbon.Controls.Add(_deployButton, 2, 0);

            var detailsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 7, AutoSize = true };
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            for (var i = 0; i < 7; i++)
            {
                detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            }

            detailsLayout.Controls.Add(new WinFormsLabel { Text = "Solution", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 0);
            detailsLayout.Controls.Add(_solutionCombo, 1, 0);
            detailsLayout.SetColumnSpan(_solutionCombo, 3);

            detailsLayout.Controls.Add(new WinFormsLabel { Text = "Target Entity", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 1);
            detailsLayout.Controls.Add(_entityCombo, 1, 1);
            detailsLayout.SetColumnSpan(_entityCombo, 3);

            detailsLayout.Controls.Add(new WinFormsLabel { Text = "Target Form (optional)", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 2);
            detailsLayout.Controls.Add(_targetFormCombo, 1, 2);
            detailsLayout.SetColumnSpan(_targetFormCombo, 3);

            detailsLayout.Controls.Add(new WinFormsLabel { Text = "Lookup Field", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 3);
            detailsLayout.Controls.Add(_lookupCombo, 1, 3);
            detailsLayout.SetColumnSpan(_lookupCombo, 3);

            detailsLayout.Controls.Add(new WinFormsLabel { Text = "Source Entity", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 4);
            detailsLayout.Controls.Add(_parentTargetCombo, 1, 4);
            detailsLayout.SetColumnSpan(_parentTargetCombo, 3);

            detailsLayout.Controls.Add(new WinFormsLabel { Text = "Source Form (optional)", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 5);
            detailsLayout.Controls.Add(_sourceFormCombo, 1, 5);
            detailsLayout.SetColumnSpan(_sourceFormCombo, 3);

            detailsLayout.Controls.Add(new WinFormsLabel { Text = "Plugin Assembly", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 6);
            detailsLayout.Controls.Add(_assemblyPath, 1, 6);
            detailsLayout.SetColumnSpan(_assemblyPath, 2);
            detailsLayout.Controls.Add(_browseAssemblyButton, 3, 6);

            ConfigureMappingGrid();

            var mappingLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(0, 6, 0, 0) };
            mappingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mappingLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mappingLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            mappingLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

            mappingLayout.Controls.Add(_parentTabs, 0, 0);
            mappingLayout.Controls.Add(_mappingGrid, 0, 1);

            var configGroup = new GroupBox { Text = "Configuration Preview (JSON)", Dock = DockStyle.Fill };
            configGroup.Controls.Add(_configurationPreview);
            mappingLayout.Controls.Add(configGroup, 0, 2);

            var logGroup = new GroupBox { Text = "Log", Dock = DockStyle.Fill };
            logGroup.Controls.Add(_logBox);

            var leftColumn = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            leftColumn.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftColumn.Controls.Add(detailsLayout, 0, 0);
            leftColumn.Controls.Add(logGroup, 0, 1);

            var bodyLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            bodyLayout.Controls.Add(leftColumn, 0, 0);
            bodyLayout.Controls.Add(mappingLayout, 1, 0);

            var container = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            container.Controls.Add(ribbon, 0, 0);
            container.Controls.Add(bodyLayout, 0, 1);

            Controls.Add(container);
        }

        private void ConfigureMappingGrid()
        {
            var parentColumn = new DataGridViewComboBoxColumn
            {
                HeaderText = "Parent Field",
                Name = "ParentField",
                DisplayMember = "DisplayLabel",
                ValueMember = "LogicalName",
                DataSource = _parentAttributes,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            var childColumn = new DataGridViewComboBoxColumn
            {
                HeaderText = "Child Field",
                Name = "ChildField",
                DisplayMember = "DisplayLabel",
                ValueMember = "LogicalName",
                DataSource = _childAttributes,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            var triggerColumn = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Trigger",
                Name = "IsTrigger",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader
            };

            _mappingGrid.Columns.AddRange(parentColumn, childColumn, triggerColumn);
        }

        private void LoadSolutions()
        {
            if (_metadataService == null)
            {
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading unmanaged solutions...",
                Work = (worker, args) => { args.Result = _metadataService.GetUnmanagedSolutions(); },
                PostWorkCallBack = args =>
                {
                    _solutions = args.Result as List<SolutionOption> ?? new List<SolutionOption>();
                    
                    _isRestoringSettings = true; // Prevent LoadEntities from overwriting settings
                    _solutionCombo.DataSource = _solutions;
                    _solutionCombo.DisplayMember = nameof(SolutionOption.FriendlyName);
                    _solutionCombo.ValueMember = nameof(SolutionOption.Id);
                    Log($"Loaded {_solutions.Count} unmanaged solutions");
                    RestoreSettings();
                    RestoreSolutionSelection();
                    // Don't clear _isRestoringSettings here - it will be cleared when the async restore chain completes
                    
                    UpdateMappingHeaders();
                }
            });
        }

        private void LoadEntities()
        {
            if (_metadataService == null || _solutionCombo.SelectedItem == null)
            {
                return;
            }

            var solution = (SolutionOption)_solutionCombo.SelectedItem;
            
            // Only update settings if we're not restoring (user is actively selecting)
            if (!_isRestoringSettings)
            {
                Log($"[LoadEntities] User selected solution, updating settings: {solution.FriendlyName}");
                _settings.SolutionId = solution.Id;
                _settings.SolutionUniqueName = solution.UniqueName;
            }
            else
            {
                Log($"[LoadEntities] Restoring - NOT overwriting settings. Current: {solution.FriendlyName}");
            }

            ResetDependentSelections();

            Log($"[LoadEntities] Starting WorkAsync to load entities for solution: {solution.Id}");
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading entities from solution...",
                Work = (worker, args) => 
                { 
                    Log($"[LoadEntities.Work] Calling GetEntitiesForSolution");
                    args.Result = _metadataService.GetEntitiesForSolution(solution.Id);
                    Log($"[LoadEntities.Work] GetEntitiesForSolution completed");
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        Log($"[LoadEntities.PostWork] ERROR: {args.Error.Message}");
                        Log($"[LoadEntities.PostWork] Stack: {args.Error.StackTrace}");
                        return;
                    }
                    
                    Log($"[LoadEntities.PostWork] Processing results");
                    _entities = args.Result as List<EntityOption> ?? new List<EntityOption>();
                    _entityCombo.DataSource = _entities;
                    _entityCombo.DisplayMember = nameof(EntityOption.DisplayLabel);
                    _entityCombo.ValueMember = nameof(EntityOption.LogicalName);
                    _entityCombo.Enabled = _entities.Count > 0;
                    Log($"Loaded {_entities.Count} entities from solution {solution.FriendlyName}");
                    RestoreEntitySelection();
                    UpdateMappingHeaders();
                    _lastLoadedAttributesKey = null;
                    _currentAttributesKey = null;
                    _lastLookupLoadKey = null;
                    UpdateConfigurationPreview();
                }
            });
        }

        private void ResetDependentSelections()
        {
            Log($"[ResetDependentSelections] CLEARING all dependent controls - IsRestoring: {_isRestoringSettings}");
            
            try
            {
                Log("[ResetDependentSelections] Clearing form cache");
                _formCache.Clear();
                _lastLookupLoadKey = null;
                _lastLoadedAttributesKey = null;
                _currentAttributesKey = null;

                Log("[ResetDependentSelections] Clearing entities");
                _entities = new List<EntityOption>();
                _entityCombo.DataSource = null;
                _entityCombo.Enabled = false;

                Log("[ResetDependentSelections] Clearing target forms");
                _targetForms = new List<FormOption>();
                _targetFormCombo.DataSource = null;
                _selectedTargetForm = null;
                _targetFormCombo.Enabled = false;

                Log("[ResetDependentSelections] Clearing lookups");
                _lookups = new List<LookupFieldOption>();
                _lookupCombo.DataSource = null;
                _lookupCombo.Enabled = false;

                Log("[ResetDependentSelections] Clearing parent targets");
                _parentTargetCombo.DataSource = null;
                _parentTabs.TabPages.Clear();
                _selectedParentEntity = null;
                _parentTargetCombo.Enabled = false;

                Log("[ResetDependentSelections] Clearing source forms");
                _sourceForms = new List<FormOption>();
                _sourceFormCombo.DataSource = null;
                _selectedSourceForm = null;
                _sourceFormCombo.Enabled = false;

                Log("[ResetDependentSelections] Clearing mappings");
                _mappingGrid.Rows.Clear();
                Log("[ResetDependentSelections] Updating preview");
                UpdateConfigurationPreview();
                Log("[ResetDependentSelections] COMPLETED successfully");
            }
            catch (Exception ex)
            {
                Log($"[ResetDependentSelections] EXCEPTION: {ex.Message}");
                Log($"[ResetDependentSelections] Stack: {ex.StackTrace}");
                throw;
            }
        }

        private void LoadTargetForms()
        {
            if (_metadataService == null || _entityCombo.SelectedItem == null)
            {
                return;
            }

            var entity = (EntityOption)_entityCombo.SelectedItem;
            var key = $"{_settings.SolutionId.GetValueOrDefault(Guid.Empty)}:{entity.LogicalName}";

            if (_formCache.TryGetValue(key, out var cached))
            {
                _targetForms = cached;
                BindTargetForms();
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading target forms...",
                Work = (worker, args) => { args.Result = _metadataService.GetFormsForEntity(_settings.SolutionId.GetValueOrDefault(Guid.Empty), entity.LogicalName ?? string.Empty); },
                PostWorkCallBack = args =>
                {
                    _targetForms = args.Result as List<FormOption> ?? new List<FormOption>();
                    _formCache[key] = _targetForms;
                    BindTargetForms();
                    Log($"Loaded {_targetForms.Count} target forms for {entity.DisplayLabel}");
                }
            });
        }

        private void BindTargetForms()
        {
            _targetFormCombo.DataSource = _targetForms;
            _targetFormCombo.DisplayMember = nameof(FormOption.DisplayLabel);
            _targetFormCombo.ValueMember = nameof(FormOption.Id);
            _targetFormCombo.Enabled = _targetForms.Count > 0;

            if (_settings.TargetFormId.HasValue)
            {
                var existing = _targetForms.FirstOrDefault(f => f.Id == _settings.TargetFormId.Value);
                if (existing != null)
                {
                    _targetFormCombo.SelectedItem = existing;
                }
            }

            _selectedTargetForm = _targetFormCombo.SelectedItem as FormOption;
            OnTargetFormChanged();
        }

        private void OnTargetFormChanged()
        {
            _selectedTargetForm = _targetFormCombo.SelectedItem as FormOption;
            FilterLookupByTargetForm();
            RebindAttributesForForms();
        }

        private void LoadSourceForms()
        {
            Log($"[LoadSourceForms] Entry - ParentEntity: '{_selectedParentEntity}', SolutionId: {_settings.SolutionId}");
            
            if (_metadataService == null || string.IsNullOrWhiteSpace(_selectedParentEntity))
            {
                Log("[LoadSourceForms] Skipping - metadataService or selectedParentEntity is null/empty");
                _sourceFormCombo.DataSource = null;
                _selectedSourceForm = null;
                return;
            }

            var key = $"{_settings.SolutionId.GetValueOrDefault(Guid.Empty)}:{_selectedParentEntity}";
            if (string.IsNullOrWhiteSpace(key))
            {
                _sourceFormCombo.DataSource = null;
                _selectedSourceForm = null;
                return;
            }
            if (_formCache.TryGetValue(key, out var cached))
            {
                _sourceForms = cached;
                BindSourceForms();
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading source forms...",
                Work = (worker, args) => { args.Result = _metadataService.GetFormsForEntity(_settings.SolutionId.GetValueOrDefault(Guid.Empty), _selectedParentEntity!); },
                PostWorkCallBack = args =>
                {
                    _sourceForms = args.Result as List<FormOption> ?? new List<FormOption>();
                    _formCache[key] = _sourceForms;
                    BindSourceForms();
                    Log($"Loaded {_sourceForms.Count} source forms for {_selectedParentEntity}");
                }
            });
        }

        private void BindSourceForms()
        {
            _sourceFormCombo.DataSource = _sourceForms;
            _sourceFormCombo.DisplayMember = nameof(FormOption.DisplayLabel);
            _sourceFormCombo.ValueMember = nameof(FormOption.Id);
            _sourceFormCombo.Enabled = _sourceForms.Count > 0;
            Log($"[BindSourceForms] Forms count: {_sourceForms.Count}, Enabled: {_sourceFormCombo.Enabled}");

            if (_settings.SourceFormId.HasValue)
            {
                var existing = _sourceForms.FirstOrDefault(f => f.Id == _settings.SourceFormId.Value);
                if (existing != null)
                {
                    Log($"[BindSourceForms] Restoring source form: {existing.DisplayLabel}");
                    _sourceFormCombo.SelectedItem = existing;
                    _sourceFormCombo.Invalidate();
                    _sourceFormCombo.Update();
                }
                else
                {
                    Log($"[BindSourceForms] Source form not found: {_settings.SourceFormId}");
                }
            }

            _selectedSourceForm = _sourceFormCombo.SelectedItem as FormOption;
            Log($"[BindSourceForms] SelectedItem: {_selectedSourceForm?.DisplayLabel}");
            RebindAttributesForForms();
            
            // Clear loading flag now that the complete restore chain has finished
            if (_isLoading)
            {
                _isLoading = false;
                _isRestoringSettings = false; // Also clear restoring flag
                Log("[BindSourceForms] Restore chain completed, cleared _isLoading and _isRestoringSettings flags");
                
                // Now that restore is complete, sync the restored values back to settings
                // so they'll be available when SaveSettings is called
                _settings.ParentEntityLogicalName = _selectedParentEntity;
                _settings.SourceFormId = _selectedSourceForm?.Id;
                Log($"[BindSourceForms] Synced restored values: ParentEntity={_selectedParentEntity}, SourceForm={_selectedSourceForm?.Id}");
                
                // Attach event handlers now that restore is complete
                HookEvents();
                Log("[BindSourceForms] Event handlers attached after restore completion");
            }
        }

        private void LoadLookupFields()
        {
            if (_metadataService == null || _entityCombo.SelectedItem == null)
            {
                return;
            }

            var entity = (EntityOption)_entityCombo.SelectedItem;
            var key = entity.LogicalName;

            if (string.Equals(_lastLookupLoadKey, key, StringComparison.OrdinalIgnoreCase) && _lookups.Count > 0)
            {
                FilterLookupByTargetForm();
                UpdateConfigurationPreview();
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading lookup fields...",
                Work = (worker, args) => { args.Result = _metadataService.GetLookupFields(entity.LogicalName); },
                PostWorkCallBack = args =>
                {
                    _lastLookupLoadKey = key;
                    _lookups = args.Result as List<LookupFieldOption> ?? new List<LookupFieldOption>();
                    FilterLookupByTargetForm();
                    Log($"Loaded {_lookups.Count} lookup fields for {entity.DisplayName}");
                    RestoreLookupSelection();
                    UpdateConfigurationPreview();
                }
            });
        }

        private void FilterLookupByTargetForm()
        {
            var selected = _lookupCombo.SelectedItem as LookupFieldOption;
            IEnumerable<LookupFieldOption> source = _lookups;

            if (_selectedTargetForm != null && _selectedTargetForm.Fields.Count > 0)
            {
                source = source.Where(l => _selectedTargetForm.Fields.Contains(l.LogicalName));
            }

            var filtered = source.ToList();
            _lookupCombo.DataSource = filtered;
            _lookupCombo.DisplayMember = nameof(LookupFieldOption.DisplayLabel);
            _lookupCombo.ValueMember = nameof(LookupFieldOption.LogicalName);
            _lookupCombo.Enabled = filtered.Count > 0;

            if (selected != null)
            {
                var match = filtered.FirstOrDefault(l => l.LogicalName.Equals(selected.LogicalName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    _lookupCombo.SelectedItem = match;
                }
            }
        }

        private void LoadParentTargets()
        {
            if (_lookupCombo.SelectedItem is not LookupFieldOption lookup)
            {
                Log("[LoadParentTargets] No lookup selected, clearing parent/source combos");
                _parentTargetCombo.DataSource = null;
                _parentTabs.TabPages.Clear();
                _parentTargetCombo.Enabled = false;
                _sourceFormCombo.DataSource = null;
                _sourceFormCombo.Enabled = false;
                return;
            }

            var targets = lookup.Targets?.ToList() ?? new List<string>();
            if (targets.Count == 0)
            {
                Log("[LoadParentTargets] Lookup has no targets, clearing parent/source combos");
                _parentTargetCombo.DataSource = null;
                _parentTabs.TabPages.Clear();
                _parentTargetCombo.Enabled = false;
                _sourceFormCombo.DataSource = null;
                _sourceFormCombo.Enabled = false;
                UpdateConfigurationPreview();
                return;
            }

            var solutionEntities = new HashSet<string>(_entities.Select(e => e.LogicalName), StringComparer.OrdinalIgnoreCase);

            var targetOptions = targets
                .Select(t => new EntityOption
                {
                    LogicalName = t,
                    DisplayName = _metadataService?.GetEntityDisplayName(t) ?? t
                })
                .Where(t => solutionEntities.Contains(t.LogicalName))
                .OrderBy(t => t.DisplayLabel)
                .ToList();

            if (targetOptions.Count == 0)
            {
                _parentTargetCombo.DataSource = null;
                _parentTabs.TabPages.Clear();
                UpdateConfigurationPreview();
                return;
            }

            _parentTargetCombo.DataSource = targetOptions;
            _parentTargetCombo.DisplayMember = nameof(EntityOption.DisplayLabel);
            _parentTargetCombo.ValueMember = nameof(EntityOption.LogicalName);
            _parentTargetCombo.Enabled = targetOptions.Count > 0;
            Log($"[LoadParentTargets] Target options count: {targetOptions.Count}, Enabled: {_parentTargetCombo.Enabled}");

            var restore = _settings.ParentEntityLogicalName;
            Log($"[LoadParentTargets] Restoring parent entity: {restore}");
            var match = targetOptions.FirstOrDefault(t => t.LogicalName.Equals(restore, StringComparison.OrdinalIgnoreCase)) ?? targetOptions.First();
            var previousParent = _selectedParentEntity;
            Log($"[LoadParentTargets] Match found: {match.LogicalName}, setting SelectedItem");
            _parentTargetCombo.SelectedItem = match;
            _parentTargetCombo.Text = match.DisplayLabel; // Force text display
            _parentTargetCombo.Invalidate(); // Force repaint
            _parentTargetCombo.Update(); // Process paint messages immediately
            _parentTargetCombo.Parent?.Invalidate(); // Force parent repaint
            Log($"[LoadParentTargets] After set, SelectedItem = {(_parentTargetCombo.SelectedItem as EntityOption)?.LogicalName}, Text = {_parentTargetCombo.Text}");
            _selectedParentEntity = match.LogicalName;
            
            // Only update settings if not in restore mode
            if (!_isRestoringSettings)
            {
                _settings.ParentEntityLogicalName = _selectedParentEntity;
            }
            BuildParentTabs(targetOptions);
            SelectParentTab(_selectedParentEntity);
            if (!string.Equals(previousParent, _selectedParentEntity, StringComparison.OrdinalIgnoreCase))
            {
                _lastLoadedAttributesKey = null;
                _currentAttributesKey = null;
            }
            LoadSourceForms();
            LoadAttributesForMapping();
            UpdateMappingHeaders();
            UpdateConfigurationPreview();
        }

        private void BuildParentTabs(List<EntityOption> targets)
        {
            _suppressParentTabChange = true;
            try
            {
                _parentTabs.TabPages.Clear();

                foreach (var target in targets)
                {
                    var tab = new TabPage(target.DisplayLabel) { Tag = target.LogicalName };
                    _parentTabs.TabPages.Add(tab);
                }
            }
            finally
            {
                _suppressParentTabChange = false;
            }
        }

        private void SelectParentTab(string? logicalName)
        {
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                return;
            }

            foreach (TabPage tab in _parentTabs.TabPages)
            {
                if (string.Equals(tab.Tag as string, logicalName, StringComparison.OrdinalIgnoreCase))
                {
                    _parentTabs.SelectedTab = tab;
                    break;
                }
            }
        }

        private void ParentTabsOnSelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressParentTabChange)
            {
                return;
            }

            if (_parentTabs.SelectedTab?.Tag is not string logicalName)
            {
                return;
            }

            if (_parentTargetCombo.DataSource is List<EntityOption> options)
            {
                var match = options.FirstOrDefault(o => o.LogicalName.Equals(logicalName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    _suppressTabSync = true;
                    _parentTargetCombo.SelectedItem = match;
                    _suppressTabSync = false;
                }
            }
        }

        private void SyncTabToParentCombo()
        {
            if (_suppressTabSync)
            {
                return;
            }

            if (_parentTargetCombo.SelectedItem is not EntityOption option)
            {
                return;
            }

            try
            {
                _suppressParentTabChange = true;
                _suppressTabSync = true;
                SelectParentTab(option.LogicalName);
            }
            finally
            {
                _suppressParentTabChange = false;
                _suppressTabSync = false;
            }
        }

        private void LoadAttributesForMapping()
        {
            if (_metadataService == null || _entityCombo.SelectedItem == null)
            {
                return;
            }

            var child = (EntityOption)_entityCombo.SelectedItem;
            _selectedParentEntity = (_parentTargetCombo.SelectedItem as EntityOption)?.LogicalName ?? _selectedParentEntity;
            if (string.IsNullOrWhiteSpace(_selectedParentEntity))
            {
                return;
            }

            var key = $"{_selectedParentEntity}|{child.LogicalName}";
            if (_isLoadingAttributes && string.Equals(_currentAttributesKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(_lastLoadedAttributesKey, key, StringComparison.OrdinalIgnoreCase))
            {
                UpdateConfigurationPreview();
                return;
            }

            _currentAttributesKey = key;
            _isLoadingAttributes = true;

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading fields for mapping...",
                Work = (worker, args) => { args.Result = _metadataService!.GetAttributeOptions(_selectedParentEntity!, child.LogicalName); },
                PostWorkCallBack = args =>
                {
                    _isLoadingAttributes = false;
                    _currentAttributesKey = null;
                    _lastLoadedAttributesKey = key;

                    var tuple = ((List<AttributeOption>, List<AttributeOption>))args.Result;
                    _parentAttributes = tuple.Item1;
                    _childAttributes = tuple.Item2;
                    RebindAttributesForForms();
                    RestoreMappings();
                    UpdateMappingHeaders();
                    UpdateConfigurationPreview();
                    Log($"Loaded {_parentAttributes.Count} parent fields and {_childAttributes.Count} child fields");
                }
            });
        }

        private void BrowseAssemblyButtonOnClick(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*",
                Title = "Select CascadeFields plugin assembly"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _assemblyPath.Text = dialog.FileName;
            }
        }

        private void AddMappingRow()
        {
            _mappingGrid.Rows.Add();
            UpdateConfigurationPreview();
        }

        private void RemoveSelectedRow()
        {
            foreach (DataGridViewRow row in _mappingGrid.SelectedRows)
            {
                _mappingGrid.Rows.Remove(row);
            }

            UpdateConfigurationPreview();
        }

        private void BindMappingColumns()
        {
            if (_mappingGrid.Columns["ParentField"] is DataGridViewComboBoxColumn parentColumn)
            {
                parentColumn.DataSource = _filteredParentAttributes;
                parentColumn.DisplayMember = nameof(AttributeOption.DisplayLabel);
            }

            if (_mappingGrid.Columns["ChildField"] is DataGridViewComboBoxColumn childColumn)
            {
                childColumn.DataSource = _filteredChildAttributes;
                childColumn.DisplayMember = nameof(AttributeOption.DisplayLabel);
            }
        }

        private void RebindAttributesForForms()
        {
            _filteredParentAttributes = ApplySourceFormFilter(_parentAttributes);
            _filteredChildAttributes = ApplyTargetFormFilter(_childAttributes);
            BindMappingColumns();
        }

        private List<AttributeOption> ApplySourceFormFilter(List<AttributeOption> source)
        {
            if (_selectedSourceForm == null || _selectedSourceForm.Fields.Count == 0)
            {
                return source;
            }

            return source.Where(a => _selectedSourceForm.Fields.Contains(a.LogicalName)).ToList();
        }

        private List<AttributeOption> ApplyTargetFormFilter(List<AttributeOption> source)
        {
            if (_selectedTargetForm == null || _selectedTargetForm.Fields.Count == 0)
            {
                return source;
            }

            return source.Where(a => _selectedTargetForm.Fields.Contains(a.LogicalName)).ToList();
        }

        private List<AttributeOption> FilterChildOptionsForParent(AttributeOption? parent)
        {
            var baseList = ApplyTargetFormFilter(_childAttributes);
            if (parent == null)
            {
                return baseList;
            }

            return baseList.Where(child => AreCompatible(parent, child)).ToList();
        }

        private void UpdateConfigurationPreview()
        {
            Log("[UpdateConfigurationPreview] Entry");
            try
            {
                var config = BuildPreviewConfiguration();
                if (config == null)
                {
                    _configurationPreview.Text = "Select a solution, child entity, lookup, and mappings to preview configuration.";
                    Log("[UpdateConfigurationPreview] Config is null, showing default message");
                    return;
                }

                _configurationPreview.Text = JsonConvert.SerializeObject(config, Formatting.Indented);
                Log("[UpdateConfigurationPreview] Config serialized successfully");
            }
            catch (Exception ex)
            {
                _configurationPreview.Text = $"Unable to build configuration preview: {ex.Message}";
                Log($"[UpdateConfigurationPreview] Exception: {ex.Message}");
            }
        }

        private PluginModels.CascadeConfiguration? BuildPreviewConfiguration()
        {
            if (_entityCombo.SelectedItem is not EntityOption childEntity)
            {
                return null;
            }

            if (_lookupCombo.SelectedItem is not LookupFieldOption lookup)
            {
                return null;
            }

            var parentEntity = (_parentTargetCombo.SelectedItem as EntityOption)?.LogicalName ?? _selectedParentEntity;
            if (string.IsNullOrWhiteSpace(parentEntity))
            {
                return null;
            }

            var mappings = ReadMappings();
            return BuildConfiguration(parentEntity!, childEntity.LogicalName, lookup.LogicalName, mappings, string.Empty);
        }

        private void UpdateMappingHeaders()
        {
            var parentLabel = FormatEntityLabel(_selectedParentEntity) ?? "Parent";
            var childLabel = _entityCombo.SelectedItem is EntityOption child ? child.DisplayLabel : "Child";

            if (_mappingGrid.Columns["ParentField"] is DataGridViewColumn parentColumn)
            {
                parentColumn.HeaderText = $"Source: {parentLabel} Field";
            }

            if (_mappingGrid.Columns["ChildField"] is DataGridViewColumn childColumn)
            {
                childColumn.HeaderText = $"Target: {childLabel} Field";
            }
        }

        private string FormatEntityLabel(string? logicalName)
        {
            var safeLogical = logicalName ?? string.Empty;

            if (string.IsNullOrWhiteSpace(safeLogical))
            {
                return "Parent";
            }

            var display = _metadataService?.GetEntityDisplayName(safeLogical) ?? safeLogical;
            if (string.IsNullOrWhiteSpace(display))
            {
                display = safeLogical;
            }

            return $"{display} ({safeLogical})";
        }

        private void DeployButtonOnClick(object sender, EventArgs e)
        {
            if (_registrationService == null)
            {
                MessageBox.Show("Connect to an environment first.");
                return;
            }

            if (_solutionCombo.SelectedItem is not SolutionOption solution)
            {
                MessageBox.Show("Select a solution.");
                return;
            }

            if (_entityCombo.SelectedItem is not EntityOption childEntity)
            {
                MessageBox.Show("Select a child entity.");
                return;
            }

            if (_lookupCombo.SelectedItem is not LookupFieldOption lookup)
            {
                MessageBox.Show("Select a lookup field.");
                return;
            }

            var parentEntity = (_parentTargetCombo.SelectedItem as EntityOption)?.LogicalName;
            if (string.IsNullOrWhiteSpace(parentEntity))
            {
                MessageBox.Show("Select a parent entity target.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_assemblyPath.Text))
            {
                MessageBox.Show("Select the CascadeFields plugin assembly to deploy.");
                return;
            }

            if (_mappingGrid.Rows.Count == 0)
            {
                MessageBox.Show("Add at least one field mapping.");
                return;
            }

            var mappings = ReadMappings();
            if (mappings.Count == 0)
            {
                MessageBox.Show("Mappings are incomplete.");
                return;
            }

            if (!ValidateMappingCompatibility(mappings))
            {
                return;
            }

            var config = BuildConfiguration(parentEntity!, childEntity.LogicalName, lookup.LogicalName, mappings, string.Empty);
            var triggerFields = mappings.Where(m => m.IsTriggerField).Select(m => m.SourceField).Distinct().ToList();

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Deploying plugin registration...",
                Work = (worker, args) =>
                {
                    var assemblyId = _registrationService.EnsurePluginAssembly(_assemblyPath.Text, solution.UniqueName);
                    var pluginTypeId = _registrationService.EnsurePluginType(assemblyId, typeof(CascadeFields.Plugin.CascadeFieldsPlugin).FullName, "CascadeFields Plugin", solution.UniqueName);
                    _registrationService.EnsureStep(pluginTypeId, config, solution.UniqueName, triggerFields);
                    args.Result = config;
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.Message, "Deployment failed");
                        Log(args.Error.ToString());
                        return;
                    }

                    Log("Deployment completed successfully");
                    SaveSettings();
                }
            });
        }

        private List<PluginModels.FieldMapping> ReadMappings()
        {
            var list = new List<PluginModels.FieldMapping>();

            foreach (DataGridViewRow row in _mappingGrid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var parentValue = row.Cells["ParentField"].Value as string;
                var childValue = row.Cells["ChildField"].Value as string;
                var isTrigger = row.Cells["IsTrigger"].Value != null && (bool)row.Cells["IsTrigger"].Value;

                if (string.IsNullOrWhiteSpace(parentValue) || string.IsNullOrWhiteSpace(childValue))
                {
                    continue;
                }

                list.Add(new PluginModels.FieldMapping
                {
                    SourceField = parentValue,
                    TargetField = childValue,
                    IsTriggerField = isTrigger
                });
            }

            return list;
        }

        private PluginModels.CascadeConfiguration BuildConfiguration(string parentEntity, string childEntity, string lookupField, List<PluginModels.FieldMapping> mappings, string filter)
        {
            return new PluginModels.CascadeConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Cascade {parentEntity} -> {childEntity}",
                ParentEntity = parentEntity,
                RelatedEntities = new List<PluginModels.RelatedEntityConfig>
                {
                    new PluginModels.RelatedEntityConfig
                    {
                        EntityName = childEntity,
                        UseRelationship = false,
                        LookupFieldName = lookupField,
                        FilterCriteria = filter,
                        FieldMappings = mappings
                    }
                },
                IsActive = true,
                EnableTracing = true
            };
        }

        private bool ValidateMappingCompatibility(List<PluginModels.FieldMapping> mappings)
        {
            foreach (var mapping in mappings)
            {
                var parent = _parentAttributes.FirstOrDefault(a => a.LogicalName == mapping.SourceField);
                var child = _childAttributes.FirstOrDefault(a => a.LogicalName == mapping.TargetField);

                if (parent == null || child == null)
                {
                    MessageBox.Show($"Could not resolve mapping {mapping.SourceField} -> {mapping.TargetField}");
                    return false;
                }

                if (!AreCompatible(parent, child))
                {
                    MessageBox.Show($"Incompatible field types: {parent.DisplayName} and {child.DisplayName}");
                    return false;
                }
            }

            return true;
        }

        private bool AreCompatible(AttributeOption parent, AttributeOption child)
        {
            if (parent.AttributeType == child.AttributeType)
            {
                return true;
            }

            var parentIsLookup = IsLookup(parent.AttributeType);
            var childIsLookup = IsLookup(child.AttributeType);

            var parentIsText = IsText(parent.AttributeType);
            var childIsText = IsText(child.AttributeType);

            if (parentIsText && childIsText)
            {
                // Allow copying between single- and multi-line text
                return true;
            }

            if (parentIsLookup && childIsLookup)
            {
                var overlap = parent.Targets.Intersect(child.Targets, StringComparer.OrdinalIgnoreCase).Any();
                return overlap;
            }

            if (parentIsLookup && (child.AttributeType == AttributeTypeCode.String || child.AttributeType == AttributeTypeCode.Memo))
            {
                // Copy lookup display text into text fields
                return true;
            }

            return false;
        }

        private bool IsLookup(AttributeTypeCode? type)
        {
            return type == AttributeTypeCode.Lookup || type == AttributeTypeCode.Customer || type == AttributeTypeCode.Owner;
        }

        private bool IsText(AttributeTypeCode? type)
        {
            return type == AttributeTypeCode.String || type == AttributeTypeCode.Memo;
        }

        private string BuildFilterFromView(ViewOption? view)
        {
            if (view == null || string.IsNullOrWhiteSpace(view.FetchXml))
            {
                return string.Empty;
            }

            try
            {
                var doc = XDocument.Parse(view.FetchXml);
                var conditions = doc.Descendants("condition")
                    .Select(c =>
                    {
                        var attribute = c.Attribute("attribute")?.Value;
                        var oper = c.Attribute("operator")?.Value;
                        var value = c.Attribute("value")?.Value ?? "null";
                        if (string.IsNullOrWhiteSpace(attribute) || string.IsNullOrWhiteSpace(oper))
                        {
                            return null;
                        }

                        return $"{attribute}|{oper}|{value}";
                    })
                    .Where(s => !string.IsNullOrWhiteSpace(s));

                return string.Join(";", conditions);
            }
            catch
            {
                return string.Empty;
            }
        }

        private void RestoreSettings()
        {
            EnsureSettings();
            _assemblyPath.Text = _settings.AssemblyPath ?? GuessDefaultAssemblyPath();
        }

        private void ClearRestoreFlags()
        {
            if (_isLoading || _isRestoringSettings)
            {
                _isLoading = false;
                _isRestoringSettings = false;
                Log("[ClearRestoreFlags] Cleared _isLoading and _isRestoringSettings flags (restore chain ended early)");
            }
        }

        private void RestoreSolutionSelection()
        {
            Log($"[RestoreSolutionSelection] Total solutions loaded: {_solutions.Count}");
            foreach (var sol in _solutions)
            {
                Log($"  - {sol.FriendlyName} ({sol.Id})");
            }

            if (_settings.SolutionId.HasValue)
            {
                Log($"[RestoreSolutionSelection] Looking for saved solution: {_settings.SolutionUniqueName} ({_settings.SolutionId})");
                var solution = _solutions.FirstOrDefault(s => s.Id == _settings.SolutionId.Value);
                if (solution != null)
                {
                    Log($"[RestoreSolutionSelection] Found solution in list, selecting: {solution.FriendlyName}");
                    _solutionCombo.SelectedItem = solution;
                    Log($"[RestoreSolutionSelection] SelectedItem set to: {_solutionCombo.SelectedItem}");
                    // Explicitly trigger load in case event didn't fire
                    LoadEntities();
                }
                else
                {
                    Log($"[RestoreSolutionSelection] Solution not found in loaded solutions list");
                }
            }
            else
            {
                Log("[RestoreSolutionSelection] No saved solution to restore");
            }
        }

        private void RestoreEntitySelection()
        {
            Log($"[RestoreEntitySelection] Entry - Looking for entity: {_settings.ChildEntityLogicalName}");
            if (!string.IsNullOrWhiteSpace(_settings.ChildEntityLogicalName))
            {
                Log($"[RestoreEntitySelection] Searching in {_entities.Count} entities");
                var entity = _entities.FirstOrDefault(e => e.LogicalName == _settings.ChildEntityLogicalName);
                if (entity != null)
                {
                    Log($"[RestoreEntitySelection] Found entity, setting SelectedItem: {entity.LogicalName}");
                    _entityCombo.SelectedItem = entity;
                    Log($"[RestoreEntitySelection] After set, SelectedItem = {(_entityCombo.SelectedItem as EntityOption)?.LogicalName}");
                    // Explicitly trigger load in case event didn't fire
                    LoadTargetForms();
                    LoadLookupFields();
                }
                else
                {
                    Log($"[RestoreEntitySelection] Entity not found in list");
                    // Entity not found, so restore chain ends here - clear flags
                    ClearRestoreFlags();
                }
            }
            else
            {
                Log($"[RestoreEntitySelection] No entity to restore (settings value is empty)");
                // Nothing to restore, so restore chain ends here - clear flags
                ClearRestoreFlags();
            }
        }

        private void RestoreLookupSelection()
        {
            Log($"[RestoreLookupSelection] Entry - Looking for lookup: {_settings.LookupFieldLogicalName}");
            if (!string.IsNullOrWhiteSpace(_settings.LookupFieldLogicalName))
            {
                var source = _lookupCombo.DataSource as List<LookupFieldOption> ?? _lookups;
                Log($"[RestoreLookupSelection] Searching in {source.Count} lookups");
                var lookup = source.FirstOrDefault(l => l.LogicalName == _settings.LookupFieldLogicalName);
                if (lookup != null)
                {
                    Log($"[RestoreLookupSelection] Found lookup, setting SelectedItem: {lookup.LogicalName}");
                    _lookupCombo.SelectedItem = lookup;
                    Log($"[RestoreLookupSelection] After set, SelectedItem = {(_lookupCombo.SelectedItem as LookupFieldOption)?.LogicalName}");
                    // Explicitly trigger load in case event didn't fire
                    LoadParentTargets();
                }
                else
                {
                    Log($"[RestoreLookupSelection] Lookup not found in list");
                    // Lookup not found, so restore chain ends here - clear flags
                    ClearRestoreFlags();
                }
            }
            else
            {
                Log($"[RestoreLookupSelection] No lookup to restore (settings value is empty)");
                // Nothing to restore, so restore chain ends here - clear flags
                ClearRestoreFlags();
            }
        }

        private void RestoreMappings()
        {
            if (_isRestoringMappings)
            {
                return;
            }

            _isRestoringMappings = true;
            _mappingGrid.EndEdit();
            _mappingGrid.ClearSelection();
            _mappingGrid.CurrentCell = null;
            _mappingGrid.SuspendLayout();
            _mappingGrid.Enabled = false;
            try
            {
                while (_mappingGrid.Rows.Count > 0)
                {
                    _mappingGrid.Rows.RemoveAt(_mappingGrid.Rows.Count - 1);
                }
            }
            finally
            {
                _mappingGrid.Enabled = true;
                _mappingGrid.ResumeLayout();
                _isRestoringMappings = false;
            }

            if (_settings.LastMappings == null || _settings.LastMappings.Count == 0)
            {
                return;
            }

            foreach (var mapping in _settings.LastMappings)
            {
                if (!_parentAttributes.Any(a => a.LogicalName.Equals(mapping.ParentField, StringComparison.OrdinalIgnoreCase)) ||
                    !_childAttributes.Any(a => a.LogicalName.Equals(mapping.ChildField, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var rowIndex = _mappingGrid.Rows.Add();
                var row = _mappingGrid.Rows[rowIndex];
                row.Cells["ParentField"].Value = mapping.ParentField;
                row.Cells["ChildField"].Value = mapping.ChildField;
                row.Cells["IsTrigger"].Value = mapping.IsTrigger;
            }

            _mappingGrid.ClearSelection();
            _mappingGrid.CurrentCell = null;
            UpdateConfigurationPreview();
        }

        private void MappingGridOnDataError(object? sender, DataGridViewDataErrorEventArgs e)
        {
            // Suppress combo binding errors when restored values are missing from the current datasource
            e.ThrowException = false;
        }

        private void MappingGridOnEditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_mappingGrid.CurrentCell == null)
            {
                return;
            }

            var columnName = _mappingGrid.CurrentCell.OwningColumn?.Name;

            if (columnName == "ChildField" && e.Control is ComboBox childCombo)
            {
                var row = _mappingGrid.CurrentCell.OwningRow;
                var parentValue = row?.Cells["ParentField"].Value as string;
                var parentAttr = _parentAttributes.FirstOrDefault(a => a.LogicalName.Equals(parentValue ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                var options = FilterChildOptionsForParent(parentAttr);
                childCombo.DataSource = options;
                childCombo.DisplayMember = nameof(AttributeOption.DisplayLabel);
                childCombo.ValueMember = nameof(AttributeOption.LogicalName);
            }

            if (columnName == "ParentField" && e.Control is ComboBox parentCombo)
            {
                parentCombo.DataSource = _filteredParentAttributes;
                parentCombo.DisplayMember = nameof(AttributeOption.DisplayLabel);
                parentCombo.ValueMember = nameof(AttributeOption.LogicalName);
            }
        }

        private void SaveSettings()
        {
            try
            {
                Log("[SaveSettings] ENTRY");
                EnsureSettings();
                var selectedSolution = _solutionCombo.SelectedItem as SolutionOption;
                Log($"[SaveSettings] SelectedItem: {selectedSolution?.FriendlyName} ({selectedSolution?.Id})");
                Log($"[SaveSettings] SelectedIndex: {_solutionCombo.SelectedIndex}");
                
                _settings.SolutionId = selectedSolution?.Id;
                _settings.SolutionUniqueName = selectedSolution?.UniqueName;
                _settings.AssemblyPath = _assemblyPath.Text;
                _settings.ChildEntityLogicalName = (_entityCombo.SelectedItem as EntityOption)?.LogicalName;
                _settings.LookupFieldLogicalName = (_lookupCombo.SelectedItem as LookupFieldOption)?.LogicalName;
                _settings.ParentEntityLogicalName = _selectedParentEntity;
                _settings.TargetFormId = (_targetFormCombo.SelectedItem as FormOption)?.Id;
                _settings.SourceFormId = (_sourceFormCombo.SelectedItem as FormOption)?.Id;
                _settings.LastMappings = ReadMappings().Select(m => new FieldMappingSetting
                {
                    ParentField = m.SourceField,
                    ChildField = m.TargetField,
                    IsTrigger = m.IsTriggerField
                }).ToList();

                Log($"[SaveSettings] About to call SettingsManager.Save with key: {_settingsKey}");
                SettingsManager.Instance.Save(GetType(), _settings, _settingsKey);
                Log("[SaveSettings] SUCCESS - Settings saved");
            }
            catch (Exception ex)
            {
                Log($"[SaveSettings] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        private void EnsureSettings()
        {
            _settings ??= new ConfiguratorSettings();
            _settings.LastMappings ??= new List<FieldMappingSetting>();
        }

        private string GuessDefaultAssemblyPath()
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            var candidate = Path.Combine(root, "CascadeFields.Plugin.dll");
            return File.Exists(candidate) ? candidate : string.Empty;
        }

        private void Log(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        private string GetSettingsKey(ConnectionDetail? detail = null)
        {
            detail ??= ConnectionDetail;
            
            // Use ConnectionId as primary key for stable connection-specific settings
            var key = detail?.ConnectionId?.ToString();

            if (string.IsNullOrWhiteSpace(key))
            {
                key = detail?.ConnectionName;
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                string? host = null;
                var url = detail?.OrganizationServiceUrl;
                if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    host = uri.Host;
                }
                key = host;
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                key = "default";
            }

            return key!;
        }
    }
}
