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
            set => SetProperty(ref _selectedRelationship, value);
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
        }

        /// <summary>
        /// Initializes the tab by loading metadata
        /// </summary>
        public async Task InitializeAsync()
        {
            IsLoading = true;

            try
            {
                // Load parent attributes
                var parentAttrs = await _metadataService.GetAttributesAsync(ParentEntityLogicalName);
                ParentAttributes.Clear();
                foreach (var attr in parentAttrs.OrderBy(a => a.DisplayName))
                {
                    ParentAttributes.Add(attr);
                }

                // Load child attributes
                var childAttrs = await _metadataService.GetAttributesAsync(ChildEntityLogicalName);
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
        /// Converts this ViewModel to a domain model
        /// </summary>
        public RelatedEntityConfigModel ToRelatedEntityConfig()
        {
            return new RelatedEntityConfigModel
            {
                EntityName = ChildEntityLogicalName,
                RelationshipName = SelectedRelationship?.SchemaName,
                UseRelationship = SelectedRelationship is not null,
                LookupFieldName = SelectedRelationship?.ReferencingAttribute,
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
        /// Builds the filter criteria string in pipe-delimited format
        /// </summary>
        private string BuildFilterString()
        {
            var validFilters = FilterCriteria
                .Where(f => f.IsValid)
                .Select(f => $"{f.Field}|{f.Operator}|{f.Value}");

            return string.Join(";", validFilters);
        }

        /// <summary>
        /// Loads configuration from a domain model
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
            if (!string.IsNullOrWhiteSpace(model.FilterCriteria))
            {
                var filters = model.FilterCriteria.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
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
