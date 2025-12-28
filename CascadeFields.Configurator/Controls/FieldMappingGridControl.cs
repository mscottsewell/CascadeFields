using System;
using System.Collections.ObjectModel;
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
        private ObservableCollection<FieldMappingViewModel>? _dataSource;

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

            // Source Field column
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "SourceField",
                HeaderText = "Source Field",
                DisplayMember = "DisplayName",
                ValueMember = "LogicalName",
                DataPropertyName = "SourceField",
                Width = 150
            });

            // Target Field column
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "TargetField",
                HeaderText = "Target Field",
                DisplayMember = "DisplayName",
                ValueMember = "LogicalName",
                DataPropertyName = "TargetField",
                Width = 150
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
        /// Handles user deletion of rows
        /// </summary>
        private void Grid_UserDeletedRow(object? sender, DataGridViewRowEventArgs e)
        {
            // Row is already removed from grid, just ensure last row is blank
            if (_dataSource?.Count > 0)
            {
                var lastItem = _dataSource[_dataSource.Count - 1];
                if (lastItem != null && 
                    !string.IsNullOrEmpty(lastItem.SourceField) &&
                    !string.IsNullOrEmpty(lastItem.TargetField))
                {
                    _dataSource.Add(new FieldMappingViewModel());
                }
            }
        }
    }
}
