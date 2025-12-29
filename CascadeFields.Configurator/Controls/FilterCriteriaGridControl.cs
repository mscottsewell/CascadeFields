using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Forms;
using CascadeFields.Configurator.ViewModels;
using CascadeFields.Configurator.Models.UI;

namespace CascadeFields.Configurator.Controls
{
    /// <summary>
    /// Control for managing filter criteria
    /// Binds to ObservableCollection<FilterCriterionViewModel>
    /// </summary>
    public partial class FilterCriteriaGridControl : UserControl
    {
        private DataGridView? _grid;
        private ObservableCollection<FilterCriterionViewModel>? _dataSource;
        private ObservableCollection<Models.UI.AttributeItem>? _availableAttributes;

        public FilterCriteriaGridControl()
        {
            CreateGrid();
        }

        /// <summary>
        /// Gets or sets the data source (filter criteria collection)
        /// </summary>
        public ObservableCollection<FilterCriterionViewModel>? DataSource
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
        /// Gets or sets the available attributes for dropdown
        /// </summary>
        public ObservableCollection<Models.UI.AttributeItem>? AvailableAttributes
        {
            get => _availableAttributes;
            set
            {
                if (_availableAttributes != null)
                {
                    _availableAttributes.CollectionChanged -= Attributes_CollectionChanged;
                }

                _availableAttributes = value;

                if (_availableAttributes != null)
                {
                    _availableAttributes.CollectionChanged += Attributes_CollectionChanged;
                }

                UpdateFieldColumnSource();
            }
        }

        /// <summary>
        /// Creates and configures the grid
        /// </summary>
        private void CreateGrid()
        {
            // Create a container with toolbar and grid
            var container = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };

            // Create toolbar
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
                var bindingSource = _grid?.DataSource as BindingSource;
                if (bindingSource != null)
                {
                    bindingSource.Add(new FilterCriterionViewModel());
                }
                else if (_dataSource != null)
                {
                    _dataSource.Add(new FilterCriterionViewModel());
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
                        var bindingSource = _grid?.DataSource as BindingSource;
                        if (bindingSource != null && rowIndex >= 0 && rowIndex < bindingSource.Count)
                        {
                            bindingSource.RemoveAt(rowIndex);
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

            // Field column (combobox)
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Field",
                HeaderText = "Field",
                DisplayMember = "FilterDisplayName",
                ValueMember = "LogicalName",
                DataPropertyName = "Field",
                Width = 180
            });

            // Operator column (combobox)
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Operator",
                HeaderText = "Operator",
                DataSource = FilterOperator.GetAll(),
                DisplayMember = nameof(FilterOperator.Display),
                ValueMember = nameof(FilterOperator.Code),
                DataPropertyName = "Operator",
                Width = 140
            });

            // Value column (text)
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Value",
                HeaderText = "Value",
                DataPropertyName = "Value",
                Width = 150
            });

            _grid.CellValueChanged += Grid_CellValueChanged;
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

            UpdateFieldColumnSource();
            var bindingSource = new BindingSource { DataSource = _dataSource };
            _grid.DataSource = bindingSource;

            // Subscribe to collection changes
            _dataSource.CollectionChanged += (s, e) => { _grid.Refresh(); };
        }

        private void UpdateFieldColumnSource()
        {
            if (_grid == null || _grid.Columns.Count == 0)
                return;

            var fieldColumn = _grid.Columns["Field"] as DataGridViewComboBoxColumn;
            if (fieldColumn != null)
            {
                fieldColumn.DataSource = _availableAttributes != null
                    ? new System.Collections.Generic.List<Models.UI.AttributeItem>(_availableAttributes)
                    : null;
            }
        }

        private void Attributes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateFieldColumnSource();
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
            var columnName = _grid?.Columns[e.ColumnIndex].Name;
            var cellValue = _grid?[e.ColumnIndex, e.RowIndex].Value;

            if (columnName == "Field")
            {
                item.Field = (cellValue as string)!;
            }
            else if (columnName == "Operator")
            {
                item.Operator = (cellValue as string)!;
            }
            else if (columnName == "Value")
            {
                item.Value = (cellValue as string)!;
            }

            // Automatically add new blank row when last row is filled
            if (e.RowIndex == _dataSource.Count - 1 &&
                !string.IsNullOrEmpty(item.Field))
            {
                _dataSource.Add(new FilterCriterionViewModel());
            }
        }


        /// <summary>
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
