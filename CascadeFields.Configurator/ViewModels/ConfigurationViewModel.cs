using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
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

        private bool _isConnected;
        private string _connectionId = string.Empty;
        private bool _isLoading;
        private string _statusMessage = "Ready. Connect to Dataverse and click 'Load Metadata'.";
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
            private set => SetProperty(ref _statusMessage, value);
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
                StatusMessage = "Ready. Click 'Load Metadata' to get started.";
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
                foreach (var entity in entities.OrderBy(e => e.DisplayName))
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
        public async Task ApplyConfigurationAsync(string json)
        {
            if (SelectedParentEntity == null)
                throw new InvalidOperationException("Parent entity not selected");

            try
            {
                var config = CascadeConfigurationModel.FromJson(json);

                // Clear existing tabs
                RelationshipTabs.Clear();

                // Create tab for each related entity in config
                foreach (var relatedEntity in config.RelatedEntities)
                {
                    var tabVm = new RelationshipTabViewModel(
                        _metadataService,
                        SelectedParentEntity.LogicalName,
                        relatedEntity.EntityName);

                    // Load metadata for this tab
                    await tabVm.InitializeAsync();

                    // Apply configuration to tab
                    tabVm.LoadFromModel(relatedEntity);

                    RelationshipTabs.Add(tabVm);
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
                var relationships = await _metadataService
                    .GetChildRelationshipsAsync(SelectedParentEntity.LogicalName);

                if (relationships.Count == 0)
                {
                    StatusMessage = "No child relationships available for this entity.";
                    return;
                }

                // For now, create a tab for the first relationship
                // In the full implementation, this should open a picker dialog
                var relationship = relationships.First();

                var tabVm = new RelationshipTabViewModel(
                    _metadataService,
                    SelectedParentEntity.LogicalName,
                    relationship.ReferencingEntity);

                await tabVm.InitializeAsync();
                tabVm.SelectedRelationship = relationship;
                tabVm.TabName = relationship.DisplayText;

                RelationshipTabs.Add(tabVm);
                SelectedTab = tabVm;

                UpdateJsonPreview();
                StatusMessage = $"Added relationship to {relationship.ReferencingEntity}.";
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
