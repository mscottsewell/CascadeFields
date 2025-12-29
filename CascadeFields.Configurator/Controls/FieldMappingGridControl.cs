using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Forms;
using CascadeFields.Configurator.Models.UI;
using CascadeFields.Configurator.ViewModels;

namespace CascadeFields.Configurator.Controls
{
    /// <summary>
    /// Encapsulated DataGridView control for field mapping rows
    /// Binds to ObservableCollection<FieldMappingViewModel>
    /// </summary>
    public class FieldMappingGridControl : UserControl
    {
        private DataGridView? _grid;
        private BindingSource? _bindingSource;
        private ObservableCollection<FieldMappingViewModel>? _dataSource;
        private ObservableCollection<AttributeItem>? _parentAttributes;
        private ObservableCollection<AttributeItem>? _childAttributes;

        public FieldMappingGridControl()
        {
            CreateGrid();
        }

        /// <summary>
        /// Gets or sets the data source collection
        /// </summary>
        public ObservableCollection<FieldMappingViewModel>? DataSource
        {
            get => _dataSource;
            set
            {
                _dataSource = value;
                if (_grid != null && value != null)
                {
                    BindData();
                }
            }
        }

        /// <summary>
        /// Available parent attributes for source field dropdown
        /// </summary>
        public ObservableCollection<AttributeItem>? ParentAttributes
        {
            get => _parentAttributes;
            set
            {
                if (_parentAttributes != null)
                {
                    _parentAttributes.CollectionChanged -= Attributes_CollectionChanged;
                }

                _parentAttributes = value;

                if (_parentAttributes != null)
                {
                    _parentAttributes.CollectionChanged += Attributes_CollectionChanged;
                }

                UpdateComboSources();
            }
        }

        /// <summary>
        /// Available child attributes for target field dropdown
        /// </summary>
        public ObservableCollection<AttributeItem>? ChildAttributes
        {
            get => _childAttributes;
            set
            {
                if (_childAttributes != null)
                {
                    _childAttributes.CollectionChanged -= Attributes_CollectionChanged;
                }

                _childAttributes = value;

                if (_childAttributes != null)
                {
                    _childAttributes.CollectionChanged += Attributes_CollectionChanged;
                }

                UpdateComboSources();
            }
        }

        /// <summary>
        /// Creates and configures the grid
        /// </summary>
        private void CreateGrid()
        {
            // Container for toolbar + grid
            var container = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };

            // Toolbar
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 28,
                AutoSize = false,
                Padding = new Padding(2),
                Margin = new Padding(0)
            };

            var btnAdd = new Button
            {
                Text = "+ Add Row",
                AutoSize = true,
                Margin = new Padding(2)
            };
            btnAdd.Click += (s, e) =>
            {
                if (_bindingSource != null)
                {
                    _bindingSource.Add(new FieldMappingViewModel());
                }
                else if (_dataSource != null)
                {
                    _dataSource.Add(new FieldMappingViewModel());
                }
            };

            var btnDelete = new Button
            {
                Text = "- Delete Row",
                AutoSize = true,
                Margin = new Padding(2)
            };
            btnDelete.Click += (s, e) =>
            {
                if (_grid?.SelectedRows.Count > 0)
                {
                    var rowsToDelete = new System.Collections.Generic.List<int>();
                    foreach (DataGridViewRow row in _grid.SelectedRows)
                    {
                        if (row.IsNewRow)
                            continue;
                        rowsToDelete.Add(row.Index);
                    }

                    rowsToDelete.Sort((a, b) => b.CompareTo(a));
                    foreach (var rowIndex in rowsToDelete)
                    {
                        if (_bindingSource != null && rowIndex >= 0 && rowIndex < _bindingSource.Count)
                        {
                            _bindingSource.RemoveAt(rowIndex);
                        }
                        else if (_dataSource != null && rowIndex >= 0 && rowIndex < _dataSource.Count)
                        {
                            _dataSource.RemoveAt(rowIndex);
                        }
                    }
                }
            };

            toolbar.Controls.Add(btnAdd);
            toolbar.Controls.Add(btnDelete);

            // Grid
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                AutoGenerateColumns = false,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AllowUserToOrderColumns = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                EditMode = DataGridViewEditMode.EditOnEnter,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders,
                Margin = new Padding(0)
            };

            // Source Field column
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "SourceField",
                HeaderText = "Source Field",
                DisplayMember = "FilterDisplayName",
                ValueMember = "LogicalName",
                DataPropertyName = "SourceField",
                Width = 180
            });

            // Target Field column
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "TargetField",
                HeaderText = "Target Field",
                DisplayMember = "FilterDisplayName",
                ValueMember = "LogicalName",
                DataPropertyName = "TargetField",
                Width = 180
            });

            // Trigger Field checkbox column
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "IsTriggerField",
                HeaderText = "Trigger Field",
                DataPropertyName = "IsTriggerField",
                Width = 80
            });

            _grid.CellValueChanged += Grid_CellValueChanged;
            _grid.CurrentCellDirtyStateChanged += Grid_CurrentCellDirtyStateChanged;
            _grid.EditingControlShowing += Grid_EditingControlShowing;
            _grid.UserDeletedRow += Grid_UserDeletedRow;
            _grid.KeyDown += Grid_KeyDown;
            _grid.DataError += (s, e) => { e.ThrowException = false; };

            // Add controls so toolbar is processed first for docking
            container.Controls.Add(_grid);
            container.Controls.Add(toolbar);

            Controls.Add(container);
        }

        /// <summary>
        /// Binds the collection to the grid
        /// </summary>
        private void BindData()
        {
            if (_grid == null || _dataSource == null)
                return;

            UpdateComboSources();
            _bindingSource = new BindingSource { DataSource = _dataSource };
            _grid.DataSource = _bindingSource;

            // Subscribe to collection changes for external updates
            _dataSource.CollectionChanged += (s, e) => { _grid.Refresh(); };
        }

        /// <summary>
        /// Updates combo box column sources when attributes change
        /// </summary>
        private void UpdateComboSources()
        {
            if (_grid == null)
                return;

            if (_grid.Columns["SourceField"] is DataGridViewComboBoxColumn sourceCol)
            {
                sourceCol.DataSource = _parentAttributes != null
                    ? new System.Collections.Generic.List<AttributeItem>(_parentAttributes)
                    : null;
            }

            if (_grid.Columns["TargetField"] is DataGridViewComboBoxColumn targetCol)
            {
                targetCol.DataSource = _childAttributes != null
                    ? new System.Collections.Generic.List<AttributeItem>(_childAttributes)
                    : null;
            }
        }

        private void Attributes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateComboSources();
        }

        /// <summary>
        /// Handles EditingControlShowing to filter target field based on source field
        /// </summary>
        private void Grid_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_grid == null || _grid.CurrentCell == null)
                return;

            // Only handle Target Field column
            if (_grid.CurrentCell.ColumnIndex != _grid.Columns["TargetField"]?.Index)
                return;

            if (e.Control is ComboBox comboBox)
            {
                var rowIndex = _grid.CurrentCell.RowIndex;
                if (rowIndex < 0 || rowIndex >= (_dataSource?.Count ?? 0))
                    return;

                var item = _dataSource?[rowIndex];
                if (item == null)
                    return;

                // Get the selected source field
                var sourceFieldName = item.SourceField;
                if (string.IsNullOrEmpty(sourceFieldName))
                {
                    // No source field selected, show all child attributes
                    comboBox.DataSource = _childAttributes != null
                        ? new System.Collections.Generic.List<AttributeItem>(_childAttributes)
                        : null;
                    return;
                }

                // Find the source attribute
                var sourceAttr = _parentAttributes?.FirstOrDefault(a => a.LogicalName == sourceFieldName);
                if (sourceAttr == null)
                {
                    // Source attribute not found, show all
                    comboBox.DataSource = _childAttributes != null
                        ? new System.Collections.Generic.List<AttributeItem>(_childAttributes)
                        : null;
                    return;
                }

                // Filter compatible target attributes
                var compatibleTargets = _childAttributes?
                    .Where(target => AreFieldsCompatible(sourceAttr, target))
                    .ToList();

                comboBox.DataSource = compatibleTargets ?? new System.Collections.Generic.List<AttributeItem>();
            }
        }

        /// <summary>
        /// Forces combo to commit changes immediately so filtering works on first selection
        /// </summary>
        private void Grid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (_grid == null || _grid.CurrentCell == null)
                return;

            // Commit changes immediately when a source field is selected
            if (_grid.CurrentCell.ColumnIndex == _grid.Columns["SourceField"]?.Index && _grid.IsCurrentCellDirty)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        /// <summary>
        /// Checks if two fields are compatible for field mapping
        /// </summary>
        private bool AreFieldsCompatible(AttributeItem source, AttributeItem target)
        {
            if (source?.Metadata == null || target?.Metadata == null)
                return false;

            var sourceType = source.Metadata.AttributeType;
            var targetType = target.Metadata.AttributeType;

            // Same type is always compatible
            if (sourceType == targetType)
                return true;

            // String types compatibility
            if (IsStringType(sourceType) && IsStringType(targetType))
                return true;

            // Numeric types compatibility
            if (IsNumericType(sourceType) && IsNumericType(targetType))
                return true;

            // DateTime types compatibility
            if (IsDateTimeType(sourceType) && IsDateTimeType(targetType))
                return true;

            // Lookup/reference types
            if (IsLookupType(sourceType) && IsLookupType(targetType))
            {
                // For lookups, check if they reference compatible entities
                if (source.Metadata is Microsoft.Xrm.Sdk.Metadata.LookupAttributeMetadata sourceLookup &&
                    target.Metadata is Microsoft.Xrm.Sdk.Metadata.LookupAttributeMetadata targetLookup)
                {
                    // Allow if they target the same entity types
                    var sourceTargets = sourceLookup.Targets ?? new string[0];
                    var targetTargets = targetLookup.Targets ?? new string[0];
                    
                    return sourceTargets.Any(s => targetTargets.Contains(s));
                }
            }

            // Boolean compatibility
            if (sourceType == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.Boolean &&
                targetType == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.Boolean)
                return true;

            // Picklist/OptionSet compatibility
            if (IsOptionSetType(sourceType) && IsOptionSetType(targetType))
                return true;

            return false;
        }

        private bool IsStringType(Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode? type)
        {
            return type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.String ||
                   type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.Memo;
        }

        private bool IsNumericType(Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode? type)
        {
            return type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.Integer ||
                   type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.BigInt ||
                   type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.Decimal ||
                   type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.Double ||
                   type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.Money;
        }

        private bool IsDateTimeType(Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode? type)
        {
            return type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.DateTime;
        }

        private bool IsLookupType(Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode? type)
        {
            return type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.Lookup ||
                   type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.Customer ||
                   type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.Owner;
        }

        private bool IsOptionSetType(Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode? type)
        {
            return type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.Picklist ||
                   type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.State ||
                   type == Microsoft.Xrm.Sdk.Metadata.AttributeTypeCode.Status;
        }

        /// <summary>
        /// Handles cell value changes
        /// </summary>
        private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_dataSource == null || e.RowIndex < 0 || e.RowIndex >= _dataSource.Count)
                return;

            var item = _dataSource[e.RowIndex];
            if (item == null)
                return;

            // Update ViewModel properties based on changed cell
            if (_grid?.Columns[e.ColumnIndex].Name == "SourceField")
            {
                var value = _grid[e.ColumnIndex, e.RowIndex].Value;
                item.SourceField = (value as string)!;
            }
            else if (_grid?.Columns[e.ColumnIndex].Name == "TargetField")
            {
                var value = _grid[e.ColumnIndex, e.RowIndex].Value;
                item.TargetField = (value as string)!;
            }
            else if (_grid?.Columns[e.ColumnIndex].Name == "IsTriggerField")
            {
                item.IsTriggerField = (bool)(_grid[e.ColumnIndex, e.RowIndex].Value ?? false);
            }

            // Automatically add new blank row when last row is filled
            if (e.RowIndex == _dataSource.Count - 1 &&
                !string.IsNullOrEmpty(item.SourceField) &&
                !string.IsNullOrEmpty(item.TargetField))
            {
                _dataSource.Add(new FieldMappingViewModel());
            }
        }

        /// <summary>
        /// Handles deletion of rows via Delete key
        /// </summary>
        private void Grid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_grid == null)
                return;

            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;

                // Remove selected rows in reverse order to avoid index shifting
                var rowsToDelete = new System.Collections.Generic.List<int>();
                foreach (DataGridViewRow row in _grid.SelectedRows)
                {
                    // Don't delete the new blank row (last row)
                    if (row.IsNewRow)
                        continue;
                    rowsToDelete.Add(row.Index);
                }

                // Sort in descending order and remove
                rowsToDelete.Sort((a, b) => b.CompareTo(a));
                foreach (var rowIndex in rowsToDelete)
                {
                    if (_dataSource != null && rowIndex >= 0 && rowIndex < _dataSource.Count)
                    {
                        _dataSource.RemoveAt(rowIndex);
                    }
                }
            }
        }

        /// <summary>
        /// Handles user deletion of rows
        /// </summary>
        private void Grid_UserDeletedRow(object? sender, DataGridViewRowEventArgs e)
        {
            // Windows Forms has already removed the row from the grid
            // Just update the data source if needed
        }
    }
}
