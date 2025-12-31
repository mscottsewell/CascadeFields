using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CascadeFields.Configurator.Models.UI;
using CascadeFields.Configurator.ViewModels;

namespace CascadeFields.Configurator.Controls
{
    /// <summary>
    /// Encapsulated DataGridView for field mappings; binds to ObservableCollection<FieldMappingViewModel>.
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

            // CRITICAL: Update combo sources BEFORE binding to prevent InvalidOperationException
            // when DataGridView tries to create new rows with uninitialized combo boxes
            UpdateComboSources();
            
            // Unbind existing data source to prevent memory leaks and duplicate event handlers
            if (_bindingSource != null)
            {
                _grid.DataSource = null;
                _bindingSource.Dispose();
                _bindingSource = null;
            }
            
            // Suspend layout to prevent intermediate refresh issues
            _grid.SuspendLayout();
            
            try
            {
                _bindingSource = new BindingSource 
                { 
                    DataSource = _dataSource,
                    AllowNew = true
                };
                _grid.DataSource = _bindingSource;

                // NOTE: Do NOT manually handle CollectionChanged events here.
                // BindingSource automatically handles collection changes and syncs with the grid.
                // Manual Refresh() calls during collection changes can cause ArgumentOutOfRangeException
                // due to timing conflicts between the grid's internal state and the collection.
            }
            finally
            {
                _grid.ResumeLayout();
            }
        }

        /// <summary>
        /// Updates combo box column sources when attributes change
        /// </summary>
        private void UpdateComboSources()
        {
            if (_grid == null)
                return;

            // Ensure combo boxes have valid data sources (even if empty) to prevent InvalidOperationException
            if (_grid.Columns["SourceField"] is DataGridViewComboBoxColumn sourceCol)
            {
                // Always provide a valid list, even if empty
                sourceCol.DataSource = _parentAttributes != null && _parentAttributes.Count > 0
                    ? new System.Collections.Generic.List<AttributeItem>(_parentAttributes)
                    : new System.Collections.Generic.List<AttributeItem>();
            }

            if (_grid.Columns["TargetField"] is DataGridViewComboBoxColumn targetCol)
            {
                // Always provide a valid list, even if empty
                targetCol.DataSource = _childAttributes != null && _childAttributes.Count > 0
                    ? new System.Collections.Generic.List<AttributeItem>(_childAttributes)
                    : new System.Collections.Generic.List<AttributeItem>();
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

            if (e.Control is ComboBox anyCombo)
            {
                ApplyComboBoxStyling(anyCombo);
            }

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
                
                // Determine which attributes to show
                System.Collections.Generic.List<AttributeItem> compatibleTargets;
                
                if (string.IsNullOrEmpty(sourceFieldName))
                {
                    // No source field selected, show all child attributes
                    compatibleTargets = _childAttributes != null && _childAttributes.Count > 0
                        ? new System.Collections.Generic.List<AttributeItem>(_childAttributes)
                        : new System.Collections.Generic.List<AttributeItem>();
                }
                else
                {
                    // Find the source attribute
                    var sourceAttr = _parentAttributes?.FirstOrDefault(a => a.LogicalName == sourceFieldName);
                    if (sourceAttr == null)
                    {
                        // Source attribute not found, show all
                        compatibleTargets = _childAttributes != null && _childAttributes.Count > 0
                            ? new System.Collections.Generic.List<AttributeItem>(_childAttributes)
                            : new System.Collections.Generic.List<AttributeItem>();
                    }
                    else
                    {
                        // Filter compatible target attributes
                        compatibleTargets = _childAttributes?
                            .Where(target => AreFieldsCompatible(sourceAttr, target))
                            .ToList() ?? new System.Collections.Generic.List<AttributeItem>();
                    }
                }

                // CRITICAL FIX: Preserve DisplayMember and ValueMember when setting DataSource
                // to prevent rendering issues and ensure values are properly committed
                comboBox.DisplayMember = "FilterDisplayName";
                comboBox.ValueMember = "LogicalName";
                comboBox.DataSource = compatibleTargets;
                
                // Force the combo box to select the current value if it exists
                var currentValue = item.TargetField;
                if (!string.IsNullOrEmpty(currentValue) &&
                    compatibleTargets.Any(a => a.LogicalName == currentValue) &&
                    !string.IsNullOrEmpty(comboBox.ValueMember))
                {
                    comboBox.SelectedValue = currentValue;
                }
            }
        }

        private void ApplyComboBoxStyling(ComboBox comboBox)
        {
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.DrawMode = DrawMode.OwnerDrawFixed;
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.BackColor = SystemColors.Window;
            comboBox.ForeColor = SystemColors.WindowText;

            comboBox.DrawItem -= ComboBox_DrawItem;
            comboBox.DrawItem += ComboBox_DrawItem;
        }

        private void ComboBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || sender is not ComboBox combo)
                return;

            var item = combo.Items[e.Index];
            var displayText = item switch
            {
                AttributeItem attribute => attribute.FilterDisplayName ?? attribute.LogicalName ?? string.Empty,
                _ => item?.ToString() ?? string.Empty
            };

            var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var backColor = isSelected ? SystemColors.Highlight : SystemColors.Window;
            var foreColor = isSelected ? SystemColors.HighlightText : SystemColors.WindowText;

            using (var backBrush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
            }

            var textBounds = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 1, e.Bounds.Width - 4, e.Bounds.Height - 2);

            using (var textBrush = new SolidBrush(foreColor))
            {
                e.Graphics.DrawString(displayText, e.Font, textBrush, textBounds);
            }

            e.DrawFocusRectangle();
        }

        /// <summary>
        /// Forces cell to commit changes immediately when user moves to next cell
        /// This ensures values are saved before grid state changes
        /// </summary>
        private void Grid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (_grid == null || _grid.CurrentCell == null || !_grid.IsCurrentCellDirty)
                return;

            try
            {
                // Commit changes immediately for combo box columns to ensure value is saved
                var columnName = _grid.CurrentCell?.OwningColumn?.Name;
                if (columnName == "SourceField" || columnName == "TargetField" || columnName == "IsTriggerField")
                {
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            }
            catch (Exception)
            {
                // Ignore commit errors - DataError event will handle any issues
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
            try
            {
                if (_dataSource == null || e.RowIndex < 0 || e.RowIndex >= _dataSource.Count)
                    return;

                var item = _dataSource[e.RowIndex];
                if (item == null)
                    return;

                // Get the cell value - this will be the LogicalName from the combo box's ValueMember
                var cellValue = _grid?[e.ColumnIndex, e.RowIndex].Value;

                // Update ViewModel properties based on changed cell
                if (_grid?.Columns[e.ColumnIndex].Name == "SourceField")
                {
                    var newValue = (cellValue as string) ?? string.Empty;
                    if (item.SourceField != newValue)
                    {
                        item.SourceField = newValue;
                        // When source field changes, the target field filtering will update automatically
                        // on next edit, so no need to clear it
                    }
                }
                else if (_grid?.Columns[e.ColumnIndex].Name == "TargetField")
                {
                    var newValue = (cellValue as string) ?? string.Empty;
                    if (item.TargetField != newValue)
                    {
                        item.TargetField = newValue;
                    }
                }
                else if (_grid?.Columns[e.ColumnIndex].Name == "IsTriggerField")
                {
                    item.IsTriggerField = (bool)(cellValue ?? false);
                }

                // Note: Removed automatic row addition to prevent timing conflicts.
                // Users can add rows via "+ Add Row" button or by typing in the last (new) row.
            }
            catch (Exception)
            {
                // Silently catch any exceptions to prevent crashes
                // Grid errors are already handled by DataError event
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
