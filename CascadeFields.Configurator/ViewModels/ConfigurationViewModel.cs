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

        public IConfigurationService ConfigurationService => _configurationService;

        private bool _isConnected;
        private string _connectionId = string.Empty;
        private bool _isLoading;
        private string _statusMessage = "Ready. Connect to Dataverse and click 'Reload Metadata'.";
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
            ISettingsRepository settingsRepository)
        {
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));

            // Initialize commands
            LoadSolutionsCommand = new AsyncRelayCommand(
                LoadSolutionsAsync,
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

            // Subscribe to relationship tabs changes to update JSON
            RelationshipTabs.CollectionChanged += (s, e) =>
            {
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

            // Try to restore session
            var session = await _settingsRepository.LoadSessionAsync(connectionId);
            if (session?.IsValid == true)
            {
                StatusMessage = "Restoring previous session...";
                await RestoreSessionAsync(session);
            }
            else
            {
                StatusMessage = "Ready. Click 'Reload Metadata' to get started.";
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
                // Load solutions first
                StatusMessage = "Loading solutions...";
                await LoadSolutionsAsync(null);

                if (Solutions.Count == 0)
                    return;

                // Select the saved solution
                SelectedSolution = Solutions.FirstOrDefault(s => s.UniqueName == session.SolutionUniqueName);
                if (SelectedSolution == null)
                    return;

                // Load parent entities (triggered by SelectedSolution change)
                await Task.Delay(500); // Wait for entities to load

                // Select parent entity
                SelectedParentEntity = ParentEntities.FirstOrDefault(
                    e => e.LogicalName == session.ParentEntityLogicalName);

                if (SelectedParentEntity == null)
                    return;

                // Restore configuration from JSON
                if (!string.IsNullOrWhiteSpace(session.ConfigurationJson))
                {
                    await ApplyConfigurationAsync(session.ConfigurationJson);
                }

                StatusMessage = "Session restored.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error restoring session: {ex.Message}");
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
        private async Task LoadSolutionsAsync(object? parameter)
        {
            if (IsLoading)
                return;

            IsLoading = true;
            StatusMessage = "Loading solutions...";

            try
            {
                var solutions = await _metadataService.GetUnmanagedSolutionsAsync();
                Solutions.Clear();
                foreach (var solution in solutions)
                {
                    Solutions.Add(solution);
                }

                StatusMessage = $"Loaded {solutions.Count} solutions.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading solutions: {ex.Message}";
                Debug.WriteLine($"Error in LoadSolutionsAsync: {ex}");
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
                return;
            }

            IsLoading = true;
            StatusMessage = "Loading entities...";

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
                ScheduleSave();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading entities: {ex.Message}";
                Debug.WriteLine($"Error in OnSolutionChangedAsync: {ex}");
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

            if (SelectedParentEntity == null)
                return;

            // Try to load existing configuration for this entity
            try
            {
                var existingConfig = await _configurationService
                    .GetConfigurationForParentEntityAsync(SelectedParentEntity.LogicalName);

                if (!string.IsNullOrWhiteSpace(existingConfig))
                {
                    await ApplyConfigurationAsync(existingConfig);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading existing configuration: {ex.Message}");
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
                return;
            }

            try
            {
                var config = CascadeConfigurationModel.FromJson(json!);

                // If parent entity not selected, try to select it from config
                if (SelectedParentEntity == null || SelectedParentEntity.LogicalName != config.ParentEntity)
                {
                    var parentEntity = ParentEntities.FirstOrDefault(e => e.LogicalName == config.ParentEntity);
                    if (parentEntity != null)
                    {
                        SelectedParentEntity = parentEntity;
                        // Wait for async entity change to complete
                        await Task.Delay(500);
                    }
                    else
                    {
                        StatusMessage = $"Parent entity '{config.ParentEntity}' not found in current solution.";
                        return;
                    }
                }

                // Clear existing tabs
                RelationshipTabs.Clear();

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
                    if (!string.IsNullOrWhiteSpace(relatedEntity.RelationshipName))
                    {
                        var relationships = await _metadataService.GetChildRelationshipsAsync(config.ParentEntity);
                        var matchingRelationship = relationships
                            .FirstOrDefault(r => r.SchemaName == relatedEntity.RelationshipName);
                        if (matchingRelationship != null)
                        {
                            tabVm.SelectedRelationship = matchingRelationship;
                            Debug.WriteLine($"Set relationship {matchingRelationship.SchemaName} for {relatedEntity.EntityName}");
                        }
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
                    }
                    else
                    {
                        Debug.WriteLine($"Tab for {relatedEntity.EntityName} already exists, skipping duplicate.");
                    }
                }

                // Apply global settings
                EnableTracing = config.EnableTracing;
                IsActive = config.IsActive;

                UpdateJsonPreview();
                StatusMessage = $"Loaded configuration with {config.RelatedEntities.Count} relationship(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error applying configuration: {ex.Message}";
                Debug.WriteLine($"Error in ApplyConfigurationAsync: {ex}");
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
                        $"The CascadeFields plugin is not registered in this environment.\n\nFile Version: {status.fileVersion ?? "Unknown"}\n\nWould you like to register it now?",
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
                        $"The registered plugin version differs from the file version:\n\nRegistered: {status.registeredVersion ?? "Unknown"}\nFile: {status.fileVersion ?? "Unknown"}\n\nWould you like to update the plugin now?",
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
