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
        private bool _isLoading;

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
                    UpdateTabName();
                }
            }
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
        /// Loads parent/child attribute metadata for drop-downs used by mapping and filter grids.
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
        /// Converts this tab into the domain model used by plugin publishing.
        /// </summary>
        public RelatedEntityConfigModel ToRelatedEntityConfig()
        {
            var lookupFieldName = SelectedRelationship?.ReferencingAttribute;
            var useRelationship = SelectedRelationship is not null;

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
                RelationshipName = SelectedRelationship?.SchemaName,
                UseRelationship = useRelationship,
                LookupFieldName = lookupFieldName,
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
        /// Builds the serialized filter criteria string expected by the plugin (field|op|value blocks).
        /// </summary>
        private string BuildFilterString()
        {
            var validFilters = FilterCriteria
                .Where(f => f.IsValid)
                .Select(f => $"{f.Field}|{f.Operator}|{f.Value}");

            return string.Join(";", validFilters);
        }

        /// <summary>
        /// Builds a multi-line tab title with child display name, lookup field, and schema name.
        /// </summary>
        private void UpdateTabName()
        {
            var entityDisplay = SelectedRelationship?.ChildEntityDisplayName ?? ChildEntityLogicalName;

            var lookupDisplay = SelectedRelationship?.LookupFieldDisplayName;
            var schemaDisplay = SelectedRelationship?.SchemaName;

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
        /// Populates mappings/filters from a previously saved domain model configuration.
        /// </summary>
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
        }
    }
}
