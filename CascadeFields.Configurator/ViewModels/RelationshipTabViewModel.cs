using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CascadeFields.Configurator.Models.Domain;
using CascadeFields.Configurator.Models.UI;
using CascadeFields.Configurator.Services;

namespace CascadeFields.Configurator.ViewModels
{
    /// <summary>
    /// ViewModel for a single child relationship tab
    /// Encapsulates all state for one relationship configuration
    /// </summary>
    public class RelationshipTabViewModel : ViewModelBase
    {
        private readonly IMetadataService _metadataService;
        private string _tabName = string.Empty;
        private RelationshipItem? _selectedRelationship;
        private bool _useRelationship = true;
        private string _lookupFieldName = string.Empty;
        private bool _isLoading;
        private bool _isPublished;

        /// <summary>
        /// Parent entity logical name
        /// </summary>
        public string ParentEntityLogicalName { get; }

        /// <summary>
        /// Child entity logical name
        /// </summary>
        public string ChildEntityLogicalName { get; }

        /// <summary>
        /// Display name for the tab
        /// </summary>
        public string TabName
        {
            get => _tabName;
            set => SetProperty(ref _tabName, value);
        }

        /// <summary>
        /// Selected relationship for this tab
        /// </summary>
        public RelationshipItem? SelectedRelationship
        {
            get => _selectedRelationship;
            set
            {
                if (SetProperty(ref _selectedRelationship, value))
                {
                    if (_useRelationship && string.IsNullOrWhiteSpace(_lookupFieldName))
                    {
                        _lookupFieldName = _selectedRelationship?.ReferencingAttribute ?? string.Empty;
                        OnPropertyChanged(nameof(LookupFieldName));
                    }
                    UpdateTabName();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to use the relationship schema name for configuration.
        /// When true, uses the relationship metadata; when false, uses only the lookup field name.
        /// </summary>
        public bool UseRelationship
        {
            get => _useRelationship;
            set
            {
                if (SetProperty(ref _useRelationship, value))
                {
                    if (!_useRelationship && string.IsNullOrWhiteSpace(_lookupFieldName))
                    {
                        _lookupFieldName = _selectedRelationship?.ReferencingAttribute ?? string.Empty;
                        OnPropertyChanged(nameof(LookupFieldName));
                    }
                    UpdateTabName();
                }
            }
        }

        /// <summary>
        /// Gets or sets the lookup field name on the child entity that references the parent.
        /// Required when UseRelationship is false, optional when true.
        /// </summary>
        public string LookupFieldName
        {
            get => _lookupFieldName;
            set => SetProperty(ref _lookupFieldName, value ?? string.Empty);
        }

        /// <summary>
        /// Whether metadata is currently loading
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Parent entity attributes (for source field dropdown)
        /// </summary>
        public ObservableCollection<AttributeItem> ParentAttributes { get; }

        /// <summary>
        /// Child entity attributes (for target field and filter dropdowns)
        /// </summary>
        public ObservableCollection<AttributeItem> ChildAttributes { get; }

        /// <summary>
        /// Field mappings for this relationship
        /// </summary>
        public ObservableCollection<FieldMappingViewModel> FieldMappings { get; }

        /// <summary>
        /// Filter criteria for this relationship
        /// </summary>
        public ObservableCollection<FilterCriterionViewModel> FilterCriteria { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RelationshipTabViewModel"/> class.
        /// </summary>
        /// <param name="metadataService">Service for retrieving entity and attribute metadata.</param>
        /// <param name="parentEntityLogicalName">Logical name of the parent entity.</param>
        /// <param name="childEntityLogicalName">Logical name of the child entity.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        public RelationshipTabViewModel(
            IMetadataService metadataService,
            string parentEntityLogicalName,
            string childEntityLogicalName)
        {
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
            ParentEntityLogicalName = parentEntityLogicalName ?? throw new ArgumentNullException(nameof(parentEntityLogicalName));
            ChildEntityLogicalName = childEntityLogicalName ?? throw new ArgumentNullException(nameof(childEntityLogicalName));

            ParentAttributes = new ObservableCollection<AttributeItem>();
            ChildAttributes = new ObservableCollection<AttributeItem>();
            FieldMappings = new ObservableCollection<FieldMappingViewModel>();
            FilterCriteria = new ObservableCollection<FilterCriterionViewModel>();

            TabName = childEntityLogicalName;

            // Subscribe to collection changes to raise property changed
            FieldMappings.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FieldMappings));
            FilterCriteria.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FilterCriteria));

            // Add initial empty rows
            FieldMappings.Add(new FieldMappingViewModel());
            FilterCriteria.Add(new FilterCriterionViewModel());

            UpdateTabName();
        }

        /// <summary>
        /// Indicates this tab was loaded from a previously published configuration.
        /// Used to prompt before removal so the publish pipeline can delete it in Dataverse.
        /// </summary>
        public bool IsPublished
        {
            get => _isPublished;
            set => SetProperty(ref _isPublished, value);
        }

        /// <summary>
        /// Loads parent and child entity attribute metadata for use in field mapping and filter drop-downs.
        /// Parent attributes include read-only and logical fields; child attributes exclude read-only.
        /// </summary>
        public async Task InitializeAsync()
        {
            IsLoading = true;

            try
            {
                // Load parent attributes
                // Source field should allow read-only and logical attributes (e.g., address composites) for mapping
                var parentAttrs = await _metadataService.GetAttributesAsync(
                    ParentEntityLogicalName,
                    includeReadOnly: true,
                    includeLogical: true);
                ParentAttributes.Clear();
                foreach (var attr in parentAttrs.OrderBy(a => a.DisplayName))
                {
                    ParentAttributes.Add(attr);
                }

                // Load child attributes
                // Target field: allow logical attributes (e.g., address composites) but keep read-only excluded
                var childAttrs = await _metadataService.GetAttributesAsync(
                    ChildEntityLogicalName,
                    includeReadOnly: false,
                    includeLogical: true);
                ChildAttributes.Clear();
                foreach (var attr in childAttrs.OrderBy(a => a.DisplayName))
                {
                    ChildAttributes.Add(attr);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Converts this ViewModel to a domain model suitable for JSON serialization and plugin publishing.
        /// Includes relationship configuration, field mappings, and filter criteria.
        /// </summary>
        /// <returns>A <see cref="RelatedEntityConfigModel"/> containing all configuration data.</returns>
        public RelatedEntityConfigModel ToRelatedEntityConfig()
        {
            var lookupFieldName = SelectedRelationship?.ReferencingAttribute;
            var useRelationship = UseRelationship && SelectedRelationship is not null;

            // Prefer explicit lookup field over relationship for reliability
            // Child relink step requires lookupFieldName to be set
            if (useRelationship && string.IsNullOrWhiteSpace(lookupFieldName))
            {
                // Log a warning that child relink step won't be published
                System.Diagnostics.Debug.WriteLine(
                    $"Warning: Relationship '{SelectedRelationship?.SchemaName}' for child entity '{ChildEntityLogicalName}' " +
                    $"does not have a lookup field name. Child relink step will not be published. " +
                    $"Set 'useRelationship: false' and provide 'lookupFieldName' for full child support.");
            }

            return new RelatedEntityConfigModel
            {
                EntityName = ChildEntityLogicalName,
                RelationshipName = useRelationship ? SelectedRelationship?.SchemaName : null,
                UseRelationship = useRelationship,
                LookupFieldName = string.IsNullOrWhiteSpace(LookupFieldName) ? lookupFieldName : LookupFieldName,
                FilterCriteria = BuildFilterString(),
                FieldMappings = FieldMappings
                    .Where(m => m.IsValid)
                    .Select(m => new FieldMappingModel
                    {
                        SourceField = m.SourceField,
                        TargetField = m.TargetField,
                        IsTriggerField = m.IsTriggerField
                    })
                    .ToList()
            };
        }

        /// <summary>
        /// Builds a semicolon-delimited filter criteria string for the plugin.
        /// Format: "field|operator|value;field|operator|value"
        /// </summary>
        /// <returns>A serialized filter string, or empty string if no valid filters exist.</returns>
        private string BuildFilterString()
        {
            var validFilters = FilterCriteria
                .Where(f => f.IsValid)
                .Select(f => $"{f.Field}|{f.Operator}|{f.Value}");

            return string.Join(";", validFilters);
        }

        /// <summary>
        /// Updates the tab display name based on the current relationship configuration.
        /// Creates a multi-line title showing entity name, lookup field, and relationship schema.
        /// </summary>
        private void UpdateTabName()
        {
            var entityDisplay = SelectedRelationship?.ChildEntityDisplayName ?? ChildEntityLogicalName;

            var lookupDisplay = UseRelationship
                ? SelectedRelationship?.LookupFieldDisplayName
                : LookupFieldName;
            var schemaDisplay = UseRelationship
                ? SelectedRelationship?.SchemaName
                : (string.IsNullOrWhiteSpace(LookupFieldName) ? null : "(lookup field)");

            // Build three-line title: Entity (bold/larger), Lookup, Schema
            var title = entityDisplay;
            if (!string.IsNullOrWhiteSpace(lookupDisplay))
            {
                title += Environment.NewLine + lookupDisplay;
            }
            if (!string.IsNullOrWhiteSpace(schemaDisplay))
            {
                title += Environment.NewLine + schemaDisplay;
            }

            if (!string.Equals(title, TabName, StringComparison.Ordinal))
            {
                TabName = title;
            }
        }

        /// <summary>
        /// Populates this tab's field mappings and filter criteria from a previously saved configuration.
        /// Clears existing data and reconstructs the ViewModels from the domain model.
        /// </summary>
        /// <param name="model">The related entity configuration to load.</param>
        /// <exception cref="ArgumentNullException">Thrown when model is null.</exception>
        public void LoadFromModel(RelatedEntityConfigModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            // Clear existing mappings and filters
            FieldMappings.Clear();
            FilterCriteria.Clear();

            // Load field mappings
            foreach (var mapping in model.FieldMappings ?? Enumerable.Empty<FieldMappingModel>())
            {
                FieldMappings.Add(new FieldMappingViewModel
                {
                    SourceField = mapping.SourceField,
                    TargetField = mapping.TargetField,
                    IsTriggerField = mapping.IsTriggerField
                });
            }

            // Add empty row if no mappings
            if (FieldMappings.Count == 0)
            {
                FieldMappings.Add(new FieldMappingViewModel());
            }

            // Load filter criteria
            var filterCriteria = model.FilterCriteria;
            if (!string.IsNullOrWhiteSpace(filterCriteria))
            {
                var filters = filterCriteria!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var filter in filters)
                {
                    var criterion = FilterCriterionModel.FromFilterString(filter);
                    if (criterion != null)
                    {
                        FilterCriteria.Add(new FilterCriterionViewModel
                        {
                            Field = criterion.Field,
                            Operator = criterion.Operator,
                            Value = criterion.Value ?? string.Empty
                        });
                    }
                }
            }

            // Add empty row if no filters
            if (FilterCriteria.Count == 0)
            {
                FilterCriteria.Add(new FilterCriterionViewModel());
            }

            UseRelationship = model.UseRelationship;
            LookupFieldName = model.LookupFieldName ?? string.Empty;
            UpdateTabName();
        }
    }
}
