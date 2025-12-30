using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Forms;
using CascadeFields.Configurator.Infrastructure.Commands;
using CascadeFields.Configurator.Models.Domain;
using CascadeFields.Configurator.Models.Session;
using CascadeFields.Configurator.Models.UI;
using CascadeFields.Configurator.Services;
using Newtonsoft.Json;
using Microsoft.Xrm.Sdk.Metadata;

namespace CascadeFields.Configurator.ViewModels
{
    /// <summary>
    /// Main ViewModel - Single source of truth for all application state
    /// Owns:
    /// - All collections (solutions, entities, relationship tabs)
    /// - All selected items
    /// - Application state (loading, validation, etc.)
    /// - Session management
    /// - JSON generation
    /// - Commands for all user actions
    /// </summary>
    public class ConfigurationViewModel : ViewModelBase
    {
        private readonly IMetadataService _metadataService;
        private readonly IConfigurationService _configurationService;
        private readonly ISettingsRepository _settingsRepository;
        private readonly Action<string>? _log;

        public IConfigurationService ConfigurationService => _configurationService;

        private bool _isConnected;
        private string _connectionId = string.Empty;
        private bool _isLoading;
        private string _statusMessage = "Ready. Connect to Dataverse and click 'Retrieve Configured Entity'.";
        private SolutionItem? _selectedSolution;
        private EntityItem? _selectedParentEntity;
        private RelationshipTabViewModel? _selectedTab;
        private bool _enableTracing = true;
        private bool _isActive = true;
        private bool _hasChanges;

        #region Collections

        /// <summary>
        /// Available solutions in the environment
        /// </summary>
        public ObservableCollection<SolutionItem> Solutions { get; } = new();

        /// <summary>
        /// Available entities in the selected solution
        /// </summary>
        public ObservableCollection<EntityItem> ParentEntities { get; } = new();

        /// <summary>
        /// Relationship tabs for the selected parent entity
        /// One tab per child relationship
        /// </summary>
        public ObservableCollection<RelationshipTabViewModel> RelationshipTabs { get; } = new();

        #endregion

        #region Selected Items

        /// <summary>
        /// Selected solution
        /// </summary>
        public SolutionItem? SelectedSolution
        {
            get => _selectedSolution;
            set
            {
                if (SetProperty(ref _selectedSolution, value))
                {
                    _ = OnSolutionChangedAsync();
                }
            }
        }

        /// <summary>
        /// Selected parent entity
        /// </summary>
        public EntityItem? SelectedParentEntity
        {
            get => _selectedParentEntity;
            set
            {
                if (SetProperty(ref _selectedParentEntity, value))
                {
                    _ = OnParentEntityChangedAsync();
                }
            }
        }

        /// <summary>
        /// Selected relationship tab
        /// </summary>
        public RelationshipTabViewModel? SelectedTab
        {
            get => _selectedTab;
            set => SetProperty(ref _selectedTab, value);
        }

        #endregion

        #region State Properties

        /// <summary>
        /// Whether connected to Dataverse
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            private set => SetProperty(ref _isConnected, value);
        }

        /// <summary>
        /// Connection identifier
        /// </summary>
        public string ConnectionId
        {
            get => _connectionId;
            private set => SetProperty(ref _connectionId, value);
        }

        /// <summary>
        /// Whether currently loading data
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Current status message for the user
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Whether tracing is enabled in the configuration
        /// </summary>
        public bool EnableTracing
        {
            get => _enableTracing;
            set
            {
                if (SetProperty(ref _enableTracing, value))
                {
                    UpdateJsonPreview();
                    ScheduleSave();
                }
            }
        }

        /// <summary>
        /// Whether the configuration is active
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetProperty(ref _isActive, value))
                {
                    UpdateJsonPreview();
                    ScheduleSave();
                }
            }
        }

        /// <summary>
        /// Whether there are unsaved changes
        /// </summary>
        public bool HasChanges
        {
            get => _hasChanges;
            private set => SetProperty(ref _hasChanges, value);
        }

        /// <summary>
        /// Generated JSON configuration
        /// </summary>
        public string ConfigurationJson { get; private set; } = string.Empty;

        /// <summary>
        /// Whether configuration is valid (can be published)
        /// </summary>
        public bool IsConfigurationValid => 
            SelectedParentEntity != null &&
            RelationshipTabs.Count > 0 &&
            RelationshipTabs.All(t => 
                t.FieldMappings.Any(m => m.IsValid) &&
                t.SelectedRelationship != null);

        /// <summary>
        /// Whether configuration can be published
        /// </summary>
        public bool CanPublish => IsConnected && IsConfigurationValid && !IsLoading;

        #endregion

        #region Commands

        public ICommand LoadSolutionsCommand { get; }
        public ICommand AddRelationshipCommand { get; }
        public ICommand RemoveRelationshipCommand { get; }
        public ICommand PublishCommand { get; }
        public ICommand ClearSessionCommand { get; }

        #endregion

        #region Constructor & Initialization

        public ConfigurationViewModel(
            IMetadataService metadataService,
            IConfigurationService configurationService,
            ISettingsRepository settingsRepository,
            Action<string>? log = null)
        {
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _log = log;

            // Initialize commands
            LoadSolutionsCommand = new AsyncRelayCommand(
                async _ => await LoadSolutionsAsync(true),
                (o) => IsConnected && !IsLoading);

            AddRelationshipCommand = new AsyncRelayCommand(
                AddRelationshipAsync,
                (o) => SelectedParentEntity != null && !IsLoading);

            RemoveRelationshipCommand = new RelayCommand(
                (o) => RemoveRelationship(o),
                (o) => SelectedTab != null);

            PublishCommand = new AsyncRelayCommand(
                (o) => PublishAsync(o),
                (o) => CanPublish);

            ClearSessionCommand = new RelayCommand(
                (o) => ClearSession(),
                (o) => IsConnected);

            // Subscribe to relationship tabs changes to update JSON and hook child collections
            RelationshipTabs.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (RelationshipTabViewModel tab in e.NewItems)
                    {
                        HookTabEvents(tab);
                    }
                }

                if (e.OldItems != null)
                {
                    foreach (RelationshipTabViewModel tab in e.OldItems)
                    {
                        UnhookTabEvents(tab);
                    }
                }

                UpdateJsonPreview();
                OnPropertyChanged(nameof(IsConfigurationValid));
                ScheduleSave();
            };
        }

        /// <summary>
        /// Initializes the ViewModel after connection
        /// </summary>
        public async Task InitializeAsync(string connectionId)
        {
            ConnectionId = connectionId;
            IsConnected = true;

            Log($"InitializeAsync start for connection {connectionId}");

            // Try to restore session
            var session = await _settingsRepository.LoadSessionAsync(connectionId);
            if (session?.IsValid == true)
            {
                StatusMessage = "Restoring previous session...";
                Log($"Restoring session for {connectionId}; last modified {session.LastModified:O}");
                await RestoreSessionAsync(session);
            }
            else
            {
                // No saved session - load solutions and auto-select Default Solution
                StatusMessage = "Loading solutions...";
                await LoadSolutionsAsync(true);
                StatusMessage = "Ready. Click 'Retrieve Configured Entity' to get started.";
                Log("No saved session found; loaded solutions fresh");
            }
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Restores configuration from saved session
        /// </summary>
        private async Task RestoreSessionAsync(SessionState session)
        {
            try
            {
                Log($"Restoring session: solution={session.SolutionUniqueName}, parent={session.ParentEntityLogicalName}, json length={session.ConfigurationJson?.Length ?? 0}");
                // Load solutions first (don't auto-select Default - we'll restore the saved solution)
                StatusMessage = "Loading solutions...";
                await LoadSolutionsAsync(false);

                if (Solutions.Count == 0)
                    return;

                // Select the saved solution
                SelectedSolution = Solutions.FirstOrDefault(s => s.UniqueName == session.SolutionUniqueName);
                if (SelectedSolution == null)
                    return;

                // Load parent entities (triggered by SelectedSolution change)
                await Task.Delay(500); // Wait for entities to load

                // Select parent entity directly (skip property setter to avoid OnParentEntityChangedAsync)
                var parentEntity = ParentEntities.FirstOrDefault(
                    e => e.LogicalName == session.ParentEntityLogicalName);
                if (parentEntity == null)
                    return;

                _selectedParentEntity = parentEntity;
                OnPropertyChanged(nameof(SelectedParentEntity));

                // Restore configuration from JSON
                if (!string.IsNullOrWhiteSpace(session.ConfigurationJson))
                {
                    await ApplyConfigurationAsync(session.ConfigurationJson);
                }

                StatusMessage = "Session restored.";
                Log("Session restore complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error restoring session: {ex.Message}");
                Log($"Session restore error: {ex}");
                StatusMessage = "Failed to restore session. Starting fresh.";
            }
        }

        /// <summary>
        /// Saves current state to session
        /// </summary>
        public async Task SaveSessionAsync()
        {
            if (!IsConnected || SelectedParentEntity == null)
                return;

            var session = new SessionState
            {
                ConnectionId = ConnectionId,
                SolutionUniqueName = SelectedSolution?.UniqueName,
                ParentEntityLogicalName = SelectedParentEntity.LogicalName,
                ConfigurationJson = ConfigurationJson,
                LastModified = DateTime.UtcNow
            };

            await _settingsRepository.SaveSessionAsync(session);
        }

        /// <summary>
        /// Clears the saved session
        /// </summary>
        private void ClearSession()
        {
            RelationshipTabs.Clear();
            SelectedParentEntity = null;
            SelectedSolution = null;
            ConfigurationJson = string.Empty;
            StatusMessage = "Session cleared.";
            _ = _settingsRepository.ClearSessionAsync(ConnectionId);
        }

        #endregion

        #region Data Loading

        /// <summary>
        /// Loads all unmanaged solutions
        /// </summary>
        /// <param name="autoSelectDefault">If true and no solution is selected, auto-selects Default Solution</param>
        private async Task LoadSolutionsAsync(bool autoSelectDefault)
        {
            if (IsLoading)
                return;

            IsLoading = true;
            StatusMessage = "Loading solutions...";
            Log($"LoadSolutionsAsync(autoSelectDefault: {autoSelectDefault})");

            try
            {
                var solutions = await _metadataService.GetUnmanagedSolutionsAsync();
                Solutions.Clear();
                foreach (var solution in solutions)
                {
                    Solutions.Add(solution);
                }

                // Auto-select "Default" solution if requested and no solution is currently selected
                if (autoSelectDefault && SelectedSolution == null && Solutions.Count > 0)
                {
                    var defaultSolution = Solutions.FirstOrDefault(s => s.UniqueName.Equals("Default", StringComparison.OrdinalIgnoreCase));
                    if (defaultSolution != null)
                    {
                        SelectedSolution = defaultSolution;
                        StatusMessage = $"Loaded {solutions.Count} solutions. Selected 'Default Solution'.";
                        Log("Default solution auto-selected");
                    }
                    else
                    {
                        StatusMessage = $"Loaded {solutions.Count} solutions.";
                    }
                }
                else
                {
                    StatusMessage = $"Loaded {solutions.Count} solutions.";
                }
                Log($"Solutions loaded: {solutions.Count}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading solutions: {ex.Message}";
                Debug.WriteLine($"Error in LoadSolutionsAsync: {ex}");
                Log($"Error loading solutions: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Handles solution selection change
        /// </summary>
        private async Task OnSolutionChangedAsync()
        {
            if (SelectedSolution == null)
            {
                ParentEntities.Clear();
                Log("SelectedSolution cleared; ParentEntities cleared");
                return;
            }

            IsLoading = true;
            StatusMessage = "Loading entities...";
            Log($"OnSolutionChangedAsync -> {SelectedSolution.UniqueName}");

            try
            {
                var entities = await _metadataService.GetSolutionEntitiesAsync(SelectedSolution.UniqueName);
                ParentEntities.Clear();
                foreach (var entity in entities.OrderBy(
                             e => e.DisplayName?.UserLocalizedLabel?.Label ?? e.LogicalName,
                             StringComparer.OrdinalIgnoreCase))
                {
                    var item = new EntityItem
                    {
                        LogicalName = entity.LogicalName,
                        DisplayName = entity.DisplayName?.UserLocalizedLabel?.Label ?? entity.LogicalName,
                        Metadata = entity
                    };
                    ParentEntities.Add(item);
                }

                StatusMessage = $"Loaded {entities.Count} entities.";
                Log($"Entities loaded for solution {SelectedSolution.UniqueName}: {entities.Count}");
                ScheduleSave();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading entities: {ex.Message}";
                Debug.WriteLine($"Error in OnSolutionChangedAsync: {ex}");
                Log($"Error loading entities: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Handles parent entity selection change
        /// </summary>
        private async Task OnParentEntityChangedAsync()
        {
            RelationshipTabs.Clear();
            ConfigurationJson = string.Empty;

            var parentEntity = SelectedParentEntity;
            if (parentEntity == null)
                return;

            var parentLogicalName = parentEntity.LogicalName ?? string.Empty;

            Log($"Parent entity changed -> {parentEntity.LogicalName}");

            // Try to load existing configuration for this entity
            try
            {
                var existingConfig = await _configurationService
                    .GetConfigurationForParentEntityAsync(parentLogicalName);

                if (!string.IsNullOrWhiteSpace(existingConfig))
                {
                    var configText = existingConfig!;
                    Log($"Existing configuration found for {parentLogicalName}; length={configText.Length}");
                    await ApplyConfigurationAsync(configText);
                }
                else
                {
                    Log($"No existing configuration for {parentLogicalName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading existing configuration: {ex.Message}");
                Log($"Error loading existing configuration: {ex}");
            }

            ScheduleSave();
        }

        #endregion

        #region Configuration Management

        /// <summary>
        /// Applies a configuration from JSON
        /// </summary>
        public async Task ApplyConfigurationAsync(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                StatusMessage = "No configuration found to apply.";
                Log("ApplyConfigurationAsync called with empty json");
                return;
            }

            try
            {
                Log($"ApplyConfigurationAsync json length={json!.Length}");
                var config = CascadeConfigurationModel.FromJson(json!);

                Log($"Parsed config: parent={config.ParentEntity}, related count={config.RelatedEntities.Count}");

                // Check if parent entity exists in current solution
                var parentEntity = ParentEntities.FirstOrDefault(e => e.LogicalName == config.ParentEntity);
                
                if (parentEntity == null)
                {
                    // Parent entity not in current solution - try switching to Default Solution
                    var defaultSolution = Solutions.FirstOrDefault(s => s.UniqueName.Equals("Default", StringComparison.OrdinalIgnoreCase));
                    if (defaultSolution != null && SelectedSolution?.UniqueName != defaultSolution.UniqueName)
                    {
                        MessageBox.Show(
                            $"Parent entity '{config.ParentEntity}' is not in the selected solution.\n\nSwitching to 'Default Solution' to load this configuration.",
                            "Parent Entity Not Found",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        
                        // Switch solution and wait for entities to load
                        StatusMessage = "Switching to Default Solution...";
                        _selectedSolution = defaultSolution;
                        OnPropertyChanged(nameof(SelectedSolution));
                        Log("Parent not in current solution; switching to Default");
                        await OnSolutionChangedAsync();
                        
                        // Try again to find parent entity
                        parentEntity = ParentEntities.FirstOrDefault(e => e.LogicalName == config.ParentEntity);
                        if (parentEntity == null)
                        {
                            StatusMessage = $"Parent entity '{config.ParentEntity}' not found even in Default Solution.";
                            Log(StatusMessage);
                            return;
                        }
                    }
                    else
                    {
                        StatusMessage = $"Parent entity '{config.ParentEntity}' not found in current solution.";
                        Log(StatusMessage);
                        return;
                    }
                }

                // Set parent entity directly (skip property setter to avoid OnParentEntityChangedAsync 
                // which would auto-load config from database - we handle loading ourselves)
                _selectedParentEntity = parentEntity;
                OnPropertyChanged(nameof(SelectedParentEntity));

                // Clear existing tabs
                RelationshipTabs.Clear();

                // Load relationships once for this parent entity
                var relationships = await _metadataService.GetChildRelationshipsAsync(config.ParentEntity);
                Debug.WriteLine($"Loaded {relationships.Count} relationships for {config.ParentEntity}");
                Log($"Child relationships loaded for {config.ParentEntity}: {relationships.Count}");

                // Create tab for each related entity in config
                foreach (var relatedEntity in config.RelatedEntities)
                {
                    var tabVm = new RelationshipTabViewModel(
                        _metadataService,
                        config.ParentEntity,
                        relatedEntity.EntityName);

                    // Load metadata for this tab
                    await tabVm.InitializeAsync();

                    // Find and set the matching relationship
                    var matchingRelationship = ResolveRelationship(relatedEntity, relationships);
                    if (matchingRelationship != null)
                    {
                        tabVm.SelectedRelationship = matchingRelationship;

                        // Back-fill missing config details so JSON and UI are complete
                        if (string.IsNullOrWhiteSpace(relatedEntity.RelationshipName))
                        {
                            relatedEntity.RelationshipName = matchingRelationship.SchemaName;
                            Log($"Inferred relationship {matchingRelationship.SchemaName} for {relatedEntity.EntityName}");
                        }

                        if (string.IsNullOrWhiteSpace(relatedEntity.LookupFieldName))
                        {
                            relatedEntity.LookupFieldName = matchingRelationship.ReferencingAttribute;
                            Log($"Inferred lookup field {matchingRelationship.ReferencingAttribute} for {relatedEntity.EntityName}");
                        }

                        Debug.WriteLine($"Set relationship {matchingRelationship.SchemaName} for {relatedEntity.EntityName}");
                    }
                    else
                    {
                        Debug.WriteLine($"Warning: No relationship found for {relatedEntity.EntityName}");
                        Log($"No relationship found for {relatedEntity.EntityName}; tab will use logical name only");
                    }

                    // Apply configuration to tab
                    tabVm.LoadFromModel(relatedEntity);

                    // CRITICAL: Check if this relationship tab already exists
                    var existingTab = RelationshipTabs.FirstOrDefault(t => 
                        t.ChildEntityLogicalName == relatedEntity.EntityName &&
                        t.SelectedRelationship?.SchemaName == relatedEntity.RelationshipName);

                    if (existingTab == null)
                    {
                        RelationshipTabs.Add(tabVm);
                        Debug.WriteLine($"Added tab for {relatedEntity.EntityName}");
                        Log($"Tab added for {relatedEntity.EntityName}");
                    }
                    else
                    {
                        Debug.WriteLine($"Tab for {relatedEntity.EntityName} already exists, skipping duplicate.");
                        Log($"Tab for {relatedEntity.EntityName} already exists; skipped duplicate");
                    }
                }

                // Apply global settings
                EnableTracing = config.EnableTracing;
                IsActive = config.IsActive;

                UpdateJsonPreview();
                StatusMessage = $"Loaded configuration with {config.RelatedEntities.Count} relationship(s).";
                Log(StatusMessage);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error applying configuration: {ex.Message}";
                Debug.WriteLine($"Error in ApplyConfigurationAsync: {ex}");
                Log($"Error applying configuration: {ex}");
            }
            
            // Force UI refresh
            OnPropertyChanged(nameof(RelationshipTabs));
        }

        /// <summary>
        /// Adds a new relationship tab
        /// </summary>
        private async Task AddRelationshipAsync(object? parameter)
        {
            if (SelectedParentEntity == null)
                return;

            IsLoading = true;
            StatusMessage = "Loading available relationships...";

            try
            {
                // Load relationships for the parent entity
                var allRelationships = await _metadataService
                    .GetChildRelationshipsAsync(SelectedParentEntity.LogicalName);

                if (allRelationships.Count == 0)
                {
                    StatusMessage = "No child relationships available for this entity.";
                    return;
                }

                // Filter out relationships that are already configured
                var configuredEntityNames = new HashSet<string>(
                    RelationshipTabs.Select(t => t.ChildEntityLogicalName),
                    StringComparer.OrdinalIgnoreCase);

                var availableRelationships = allRelationships
                    .Where(r => !configuredEntityNames.Contains(r.ReferencingEntity))
                    .ToList();

                if (availableRelationships.Count == 0)
                {
                    StatusMessage = "All available relationships are already configured.";
                    return;
                }

                IsLoading = false;

                // Show picker dialog
                var picker = new Dialogs.RelationshipPickerDialog(availableRelationships);
                if (picker.ShowDialog() == DialogResult.OK && picker.SelectedRelationship != null)
                {
                    IsLoading = true;
                    var relationship = picker.SelectedRelationship;

                    var tabVm = new RelationshipTabViewModel(
                        _metadataService,
                        SelectedParentEntity.LogicalName,
                        relationship.ReferencingEntity);

                    await tabVm.InitializeAsync();
                    tabVm.SelectedRelationship = relationship;

                    // CRITICAL: Check if this relationship is already in the tabs collection
                    var existingTab = RelationshipTabs.FirstOrDefault(t => 
                        t.ChildEntityLogicalName == relationship.ReferencingEntity &&
                        t.SelectedRelationship?.SchemaName == relationship.SchemaName);

                    if (existingTab == null)
                    {
                        RelationshipTabs.Add(tabVm);
                        SelectedTab = tabVm;
                        UpdateJsonPreview();
                        StatusMessage = $"Added relationship to {relationship.ReferencingEntity}.";
                    }
                    else
                    {
                        // Tab already exists, select it instead
                        SelectedTab = existingTab;
                        StatusMessage = $"Relationship to {relationship.ReferencingEntity} already exists. Showing existing tab.";
                    }
                }
                else
                {
                    StatusMessage = "Add relationship cancelled.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding relationship: {ex.Message}";
                Debug.WriteLine($"Error in AddRelationshipAsync: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Removes the selected relationship tab
        /// </summary>
        private void RemoveRelationship(object? parameter)
        {
            if (SelectedTab != null)
            {
                RelationshipTabs.Remove(SelectedTab);
                UpdateJsonPreview();
                StatusMessage = "Relationship removed.";
                ScheduleSave();
            }
        }

        /// <summary>
        /// Publishes the configuration to a plugin step
        /// </summary>
        private async Task PublishAsync(object? parameter)
        {
            if (!IsConfigurationValid)
                return;

            IsLoading = true;
            StatusMessage = "Publishing configuration...";

            try
            {
                var progress = new Progress<string>(msg => StatusMessage = msg);
                var config = BuildConfigurationModel();

                // Check plugin status before publishing
                var pluginCheckResult = await CheckAndUpdatePluginIfNeededAsync(progress);
                if (!pluginCheckResult)
                {
                    StatusMessage = "Publish cancelled.";
                    return;
                }

                await _configurationService.PublishConfigurationAsync(
                    config,
                    progress,
                    System.Threading.CancellationToken.None);

                StatusMessage = "Configuration published successfully.";
                HasChanges = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error publishing configuration: {ex.Message}";
                Debug.WriteLine($"Error in PublishAsync: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks plugin status and prompts user to register/update if needed
        /// </summary>
        /// <returns>True if plugin is ready or user confirmed update, false if user cancelled</returns>
        private async Task<bool> CheckAndUpdatePluginIfNeededAsync(IProgress<string> progress)
        {
            try
            {
                // Resolve plugin DLL path - it's in the CascadeFieldsConfigurator subfolder
                var baseDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                    ?? AppDomain.CurrentDomain.BaseDirectory;
                var assemblyPath = System.IO.Path.Combine(baseDir, 
                    "CascadeFieldsConfigurator", "Assets", "DataversePlugin", "CascadeFields.Plugin.dll");

                if (!System.IO.File.Exists(assemblyPath))
                {
                    progress.Report($"Warning: Plugin DLL not found at {assemblyPath}");
                    var result = System.Windows.Forms.MessageBox.Show(
                        $"Plugin DLL not found at:\n{assemblyPath}\n\nDo you want to continue publishing anyway?",
                        "Plugin DLL Not Found",
                        System.Windows.Forms.MessageBoxButtons.YesNo,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                    return result == System.Windows.Forms.DialogResult.Yes;
                }

                // Check plugin status
                var status = _configurationService.CheckPluginStatus(assemblyPath);

                // Plugin not registered
                if (!status.isRegistered)
                {
                    progress.Report("CascadeFields plugin is not registered.");
                    var result = System.Windows.Forms.MessageBox.Show(
                        $"The CascadeFields plugin is not registered in this environment.\n\nAssembly Version: {status.assemblyVersion ?? "Unknown"}\nFile Version: {status.fileVersion ?? "Unknown"}\n\nWould you like to register it now?",
                        "Register Plugin",
                        System.Windows.Forms.MessageBoxButtons.YesNo,
                        System.Windows.Forms.MessageBoxIcon.Question);

                    if (result == System.Windows.Forms.DialogResult.Yes)
                    {
                        await _configurationService.UpdatePluginAssemblyAsync(assemblyPath, progress, System.Threading.CancellationToken.None, SelectedSolution?.Id);
                        progress.Report("Plugin registered successfully.");
                        return true;
                    }
                    return false;
                }

                // Plugin registered but needs update
                if (status.needsUpdate)
                {
                        progress.Report($"Plugin version mismatch detected.");
                    var result = System.Windows.Forms.MessageBox.Show(
                        $"The registered plugin version differs from the assembly version:\n\nRegistered: {status.registeredVersion ?? "Unknown"}\nAssembly: {status.assemblyVersion ?? "Unknown"}\nFile: {status.fileVersion ?? "Unknown"}\n\nWould you like to update the plugin now?",
                        "Update Plugin",
                        System.Windows.Forms.MessageBoxButtons.YesNo,
                        System.Windows.Forms.MessageBoxIcon.Question);

                    if (result == System.Windows.Forms.DialogResult.Yes)
                    {
                        await _configurationService.UpdatePluginAssemblyAsync(assemblyPath, progress, System.Threading.CancellationToken.None, SelectedSolution?.Id);
                        progress.Report("Plugin updated successfully.");
                        return true;
                    }
                    return false;
                }

                // Plugin is current
                progress.Report($"Plugin is current (version {status.registeredVersion}).");
                return true;
            }
            catch (Exception ex)
            {
                progress.Report($"Error checking plugin status: {ex.Message}");
                Debug.WriteLine($"Error in CheckAndUpdatePluginIfNeededAsync: {ex}");
                
                var result = System.Windows.Forms.MessageBox.Show(
                    $"Error checking plugin status:\n{ex.Message}\n\nDo you want to continue publishing anyway?",
                    "Plugin Check Error",
                    System.Windows.Forms.MessageBoxButtons.YesNo,
                    System.Windows.Forms.MessageBoxIcon.Warning);
                return result == System.Windows.Forms.DialogResult.Yes;
            }
        }

        #endregion

        #region JSON Generation

        /// <summary>
        /// Updates the JSON preview from current state
        /// </summary>
        private void UpdateJsonPreview()
        {
            try
            {
                var config = BuildConfigurationModel();
                ConfigurationJson = config.ToJson();
                HasChanges = true;
                OnPropertyChanged(nameof(ConfigurationJson));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating JSON: {ex.Message}");
            }
        }

        /// <summary>
        /// Hooks change events on a relationship tab so JSON stays in sync
        /// </summary>
        private void HookTabEvents(RelationshipTabViewModel tab)
        {
            // Field mappings collection changes
            tab.FieldMappings.CollectionChanged += TabChildCollectionChanged;
            // Filter criteria collection changes
            tab.FilterCriteria.CollectionChanged += TabChildCollectionChanged;

            // Existing item property changes
            foreach (var mapping in tab.FieldMappings)
            {
                mapping.PropertyChanged += TabChildItemPropertyChanged;
            }
            foreach (var filter in tab.FilterCriteria)
            {
                filter.PropertyChanged += TabChildItemPropertyChanged;
            }
        }

        /// <summary>
        /// Unhooks change events from a relationship tab
        /// </summary>
        private void UnhookTabEvents(RelationshipTabViewModel tab)
        {
            tab.FieldMappings.CollectionChanged -= TabChildCollectionChanged;
            tab.FilterCriteria.CollectionChanged -= TabChildCollectionChanged;

            foreach (var mapping in tab.FieldMappings)
            {
                mapping.PropertyChanged -= TabChildItemPropertyChanged;
            }
            foreach (var filter in tab.FilterCriteria)
            {
                filter.PropertyChanged -= TabChildItemPropertyChanged;
            }
        }

        private void TabChildCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Hook property change events for new items
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is ViewModelBase vm)
                    {
                        vm.PropertyChanged += TabChildItemPropertyChanged;
                    }
                }
            }

            // Unhook removed items
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is ViewModelBase vm)
                    {
                        vm.PropertyChanged -= TabChildItemPropertyChanged;
                    }
                }
            }

            UpdateJsonPreview();
            ScheduleSave();
        }

        private void TabChildItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateJsonPreview();
            ScheduleSave();
        }

        /// <summary>
        /// Builds a configuration model from the current ViewModel state
        /// </summary>
        private CascadeConfigurationModel BuildConfigurationModel()
        {
            if (SelectedParentEntity == null)
                throw new InvalidOperationException("Parent entity not selected");

            return new CascadeConfigurationModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{SelectedParentEntity.LogicalName} cascade configuration",
                ParentEntity = SelectedParentEntity.LogicalName,
                IsActive = IsActive,
                EnableTracing = EnableTracing,
                RelatedEntities = RelationshipTabs
                    .Where(t => t.SelectedRelationship != null)
                    .Select(t => t.ToRelatedEntityConfig())
                    .ToList()
            };
        }

        public async Task<(bool isValid, List<string> errors, List<string> warnings, List<(int componentType, Guid componentId, string description)> missingComponents)> ValidateConfigurationJsonAsync(string json, string? currentSolutionUniqueName)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var missingComponents = new List<(int componentType, Guid componentId, string description)>();

            CascadeConfigurationModel? config;
            try
            {
                config = CascadeConfigurationModel.FromJson(json);
            }
            catch (Exception ex)
            {
                errors.Add($"Invalid JSON: {ex.Message}");
                return (false, errors, warnings, missingComponents);
            }

            // Resolve solution membership for warnings
            var solutionEntities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(currentSolutionUniqueName))
            {
                try
                {
                    var entitiesInSolution = await _metadataService.GetSolutionEntitiesAsync(currentSolutionUniqueName!);
                    foreach (var em in entitiesInSolution)
                    {
                        solutionEntities.Add(em.LogicalName);
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"Could not verify solution contents: {ex.Message}");
                }
            }

            // Parent entity metadata
            EntityMetadata? parentMeta = null;
            try
            {
                parentMeta = await _metadataService.GetEntityMetadataAsync(config.ParentEntity);
            }
            catch
            {
                errors.Add($"Parent entity '{config.ParentEntity}' does not exist in this environment.");
            }

            if (parentMeta is not null && solutionEntities.Count > 0 && !solutionEntities.Contains(config.ParentEntity))
            {
                warnings.Add($"Parent entity '{config.ParentEntity}' is not in solution '{currentSolutionUniqueName}'. Add it to keep the solution compatible.");
                if (parentMeta.MetadataId.HasValue)
                    missingComponents.Add((1, parentMeta.MetadataId.Value, $"Entity: {config.ParentEntity}"));
            }

            // Cache parent attributes
            var parentAttributes = parentMeta?.Attributes?.ToDictionary(a => a.LogicalName, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, AttributeMetadata>(StringComparer.OrdinalIgnoreCase);

            foreach (var related in config.RelatedEntities)
            {
                EntityMetadata? childMeta = null;
                try
                {
                    childMeta = await _metadataService.GetEntityMetadataAsync(related.EntityName);
                }
                catch
                {
                    errors.Add($"Child entity '{related.EntityName}' does not exist in this environment.");
                    continue;
                }

                if (childMeta is not null && solutionEntities.Count > 0 && !solutionEntities.Contains(related.EntityName))
                {
                    warnings.Add($"Child entity '{related.EntityName}' is not in solution '{currentSolutionUniqueName}'. Add it to keep the solution compatible.");
                    if (childMeta.MetadataId.HasValue)
                        missingComponents.Add((1, childMeta.MetadataId.Value, $"Entity: {related.EntityName}"));
                }

                var childAttributes = childMeta?.Attributes?.ToDictionary(a => a.LogicalName, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, AttributeMetadata>(StringComparer.OrdinalIgnoreCase);

                // Relationship validation
                if (related.UseRelationship)
                {
                    var match = (parentMeta?.OneToManyRelationships ?? Array.Empty<OneToManyRelationshipMetadata>())
                        .FirstOrDefault(r =>
                            string.Equals(r.SchemaName, related.RelationshipName, StringComparison.OrdinalIgnoreCase) ||
                            (string.Equals(r.ReferencingEntity, related.EntityName, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(r.ReferencingAttribute, related.LookupFieldName, StringComparison.OrdinalIgnoreCase)));

                    if (match == null)
                    {
                        errors.Add($"Relationship not found for child '{related.EntityName}'. Expected schema '{related.RelationshipName ?? "(unspecified)"}' or lookup '{related.LookupFieldName ?? "(unspecified)"}'.");
                    }
                    else if (solutionEntities.Count > 0 && childMeta is not null && !solutionEntities.Contains(related.EntityName))
                    {
                        if (match.MetadataId.HasValue)
                            missingComponents.Add((3, match.MetadataId.Value, $"Relationship: {match.SchemaName}"));
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(related.LookupFieldName))
                    {
                        errors.Add($"Child '{related.EntityName}' requires lookupFieldName when useRelationship is false.");
                    }
                }

                // Lookup field check
                var lookupField = related.LookupFieldName;
                if (!string.IsNullOrWhiteSpace(lookupField) && !childAttributes.ContainsKey(lookupField!))
                {
                    errors.Add($"Lookup field '{lookupField}' not found on child '{related.EntityName}'.");
                }

                // Field mappings
                foreach (var mapping in related.FieldMappings)
                {
                    var sourceField = mapping.SourceField;
                    var targetField = mapping.TargetField;

                    if (string.IsNullOrWhiteSpace(sourceField) || string.IsNullOrWhiteSpace(targetField))
                    {
                        errors.Add($"Mapping is missing source or target field for child '{related.EntityName}'.");
                        continue;
                    }
                    if (!parentAttributes.ContainsKey(sourceField))
                    {
                        errors.Add($"Source field '{sourceField}' not found on parent '{config.ParentEntity}'.");
                    }
                    if (!childAttributes.ContainsKey(targetField))
                    {
                        errors.Add($"Target field '{targetField}' not found on child '{related.EntityName}'.");
                    }
                    else if (childMeta is not null && solutionEntities.Count > 0 && !solutionEntities.Contains(related.EntityName))
                    {
                        var attr = childAttributes[targetField]!;
                        if (attr.MetadataId.HasValue)
                            missingComponents.Add((2, attr.MetadataId.Value, $"Field: {related.EntityName}.{targetField}"));
                    }
                }

                // Filters
                var filterCriteria = related.FilterCriteria ?? string.Empty;
                if (string.IsNullOrWhiteSpace(filterCriteria))
                    continue;

                var filters = filterCriteria.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var filter in filters)
                {
                    var fc = FilterCriterionModel.FromFilterString(filter);
                    if (fc == null)
                    {
                        errors.Add($"Invalid filter format '{filter}' for child '{related.EntityName}'.");
                        continue;
                    }
                    var fcField = fc.Field;
                    if (string.IsNullOrWhiteSpace(fcField))
                    {
                        errors.Add($"Filter field is missing for child '{related.EntityName}'.");
                    }
                    else
                    {
                        var filterField = fcField!;
                        if (!childAttributes.ContainsKey(filterField))
                        {
                            errors.Add($"Filter field '{filterField}' not found on child '{related.EntityName}'.");
                        }
                        else if (childMeta is not null && solutionEntities.Count > 0 && !solutionEntities.Contains(related.EntityName))
                        {
                            var attr = childAttributes[filterField];
                            if (attr.MetadataId.HasValue)
                                missingComponents.Add((2, attr.MetadataId.Value, $"Field: {related.EntityName}.{filterField}"));
                        }
                    }
                }
            }

            return (!errors.Any(), errors, warnings, missingComponents);
        }

        private void Log(string message)
        {
            _log?.Invoke(message);
            Debug.WriteLine(message);
        }

        /// <summary>
        /// Attempts to resolve a relationship when the configuration is missing relationshipName
        /// </summary>
        private static RelationshipItem? ResolveRelationship(RelatedEntityConfigModel relatedEntity, IReadOnlyList<RelationshipItem> relationships)
        {
            if (relatedEntity == null || relationships == null)
                return null;

            // 1) Exact schema name match (if provided)
            if (!string.IsNullOrWhiteSpace(relatedEntity.RelationshipName))
            {
                var matchBySchema = relationships.FirstOrDefault(r =>
                    string.Equals(r.SchemaName, relatedEntity.RelationshipName, StringComparison.OrdinalIgnoreCase));
                if (matchBySchema != null)
                    return matchBySchema;
            }

            // 2) Match by child entity + lookup field
            if (!string.IsNullOrWhiteSpace(relatedEntity.LookupFieldName))
            {
                var matchByLookup = relationships.FirstOrDefault(r =>
                    string.Equals(r.ReferencingEntity, relatedEntity.EntityName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.ReferencingAttribute, relatedEntity.LookupFieldName, StringComparison.OrdinalIgnoreCase));
                if (matchByLookup != null)
                    return matchByLookup;
            }

            // 3) If only one relationship exists for this child, use it
            var candidates = relationships
                .Where(r => string.Equals(r.ReferencingEntity, relatedEntity.EntityName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            return null;
        }

        #endregion

        #region Save Debouncing

        private System.Timers.Timer? _saveTimer;

        /// <summary>
        /// Schedules a save with debouncing (2 seconds)
        /// </summary>
        private void ScheduleSave()
        {
            _saveTimer?.Stop();
            _saveTimer = new System.Timers.Timer(2000) { AutoReset = false };
            _saveTimer.Elapsed += async (s, e) =>
            {
                await SaveSessionAsync();
            };
            _saveTimer.Start();
        }

        #endregion
    }
}
