using CascadeFields.Configurator.ViewModels;

namespace CascadeFields.Configurator.ViewModels
{
    /// <summary>
    /// ViewModel for a single field mapping row
    /// Used for data binding in the field mapping grid
    /// </summary>
    public class FieldMappingViewModel : ViewModelBase
    {
        private string _sourceField = string.Empty;
        private string _targetField = string.Empty;
        private bool _isTriggerField = true;

        /// <summary>
        /// Source field name on the parent entity
        /// </summary>
        public string SourceField
        {
            get => _sourceField;
            set => SetProperty(ref _sourceField, value);
        }

        /// <summary>
        /// Target field name on the child entity
        /// </summary>
        public string TargetField
        {
            get => _targetField;
            set => SetProperty(ref _targetField, value);
        }

        /// <summary>
        /// Whether changes to this field trigger the cascade operation
        /// </summary>
        public bool IsTriggerField
        {
            get => _isTriggerField;
            set => SetProperty(ref _isTriggerField, value);
        }

        /// <summary>
        /// Whether this mapping is valid (has both source and target)
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(SourceField) &&
            !string.IsNullOrWhiteSpace(TargetField);
    }

    /// <summary>
    /// ViewModel for a single filter criterion row
    /// Used for data binding in the filter criteria grid
    /// </summary>
    public class FilterCriterionViewModel : ViewModelBase
    {
        private string _field = string.Empty;
        private string _operator = "eq";
        private string _value = string.Empty;

        /// <summary>
        /// Field name to filter on
        /// </summary>
        public string Field
        {
            get => _field;
            set => SetProperty(ref _field, value);
        }

        /// <summary>
        /// Filter operator (eq, ne, gt, lt, etc.)
        /// </summary>
        public string Operator
        {
            get => _operator;
            set => SetProperty(ref _operator, value);
        }

        /// <summary>
        /// Filter value
        /// </summary>
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        /// <summary>
        /// Whether this filter is valid (has a field)
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(Field);
    }
}
