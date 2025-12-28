using System;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using CascadeFields.Configurator.ViewModels;

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
                _availableAttributes = value;
                if (_grid != null && _grid.Columns.Count > 0)
                {
                    var fieldColumn = _grid.Columns["Field"] as DataGridViewComboBoxColumn;
                    if (fieldColumn != null && value != null)
                    {
                        fieldColumn.DataSource = new System.Collections.Generic.List<Models.UI.AttributeItem>(value);
                    }
                }
            }
        }

        /// <summary>
        /// Creates and configures the grid
        /// </summary>
        private void CreateGrid()
        {
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                AllowUserToOrderColumns = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                EditMode = DataGridViewEditMode.EditOnEnter,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            // Field column (combobox)
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Field",
                HeaderText = "Field",
                DisplayMember = "DisplayName",
                ValueMember = "LogicalName",
                DataPropertyName = "Field",
                Width = 150
            });

            // Operator column (combobox)
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Operator",
                HeaderText = "Operator",
                DataSource = new[] { "eq", "ne", "gt", "lt", "gte", "lte", "like" },
                DataPropertyName = "Operator",
                Width = 100
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
            _grid.DataError += (s, e) => { e.ThrowException = false; };

            Controls.Add(_grid);
        }

        /// <summary>
        /// Binds the collection to the grid
        /// </summary>
        private void BindData()
        {
            if (_grid == null || _dataSource == null)
                return;

            _grid.DataSource = _dataSource;

            // Subscribe to collection changes
            _dataSource.CollectionChanged += (s, e) =>
            {
                _grid.Refresh();
            };
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
        /// Handles user deletion of rows
        /// </summary>
        private void Grid_UserDeletedRow(object? sender, DataGridViewRowEventArgs e)
        {
            // Row is already removed from grid, just ensure last row is blank
            if (_dataSource?.Count > 0)
            {
                var lastItem = _dataSource[_dataSource.Count - 1];
                if (lastItem != null && !string.IsNullOrEmpty(lastItem.Field))
                {
                    _dataSource.Add(new FilterCriterionViewModel());
                }
            }
        }
    }
}
