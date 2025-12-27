using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using CascadeFields.Configurator.Models;

namespace CascadeFields.Configurator.Controls
{
    public partial class FilterCriteriaControl : UserControl
    {
        private readonly BindingList<FilterRow> _filterRows = new();
        private readonly List<FilterOperator> _operators = FilterOperator.GetAll();
        private List<AttributeItem> _availableFields = new();
        private bool _isUpdating = false;

        public event EventHandler? FilterChanged;

        public FilterCriteriaControl()
        {
            InitializeComponent();
            
            // Disable automatic new row addition - we'll handle it manually
            _filterRows.AllowNew = false;
            
            InitializeBehavior();
        }

        private void InitializeBehavior()
        {
            // Disable automatic ListChanged handling - we'll handle updates manually
            // _filterRows.ListChanged += (s, e) => OnFilterChanged();

            gridFilters.DataSource = _filterRows;
            
            // Initialize operator column properties (but NOT DataSource - we'll populate per-cell)
            colOperator.DisplayMember = nameof(FilterOperator.Display);
            colOperator.ValueMember = nameof(FilterOperator.Code);
            // Note: Don't set DataSource here - we populate individual cells in InitializeOperatorDropdown
            
            gridFilters.CellValueChanged += GridFilters_CellValueChanged;
            gridFilters.CellEndEdit += (s, e) => 
            {
                // Raise FilterChanged only after the user has finished editing the cell
                if (e.RowIndex >= 0)
                {
                    OnFilterChanged();
                    
                    // If user just edited the last row and it's not blank, add a new blank row
                    if (e.RowIndex == _filterRows.Count - 1)
                    {
                        var lastRow = _filterRows[e.RowIndex];
                        if (!string.IsNullOrWhiteSpace(lastRow.Field))
                        {
                            _filterRows.Add(new FilterRow());
                        }
                    }
                }
            };
            gridFilters.EditingControlShowing += GridFilters_EditingControlShowing;
            gridFilters.CellContentClick += GridFilters_CellContentClick;
            gridFilters.CellClick += GridFilters_CellClick;
            gridFilters.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (gridFilters.CurrentCell is DataGridViewComboBoxCell)
                {
                    gridFilters.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            gridFilters.DataError += (s, e) => { e.ThrowException = false; };
            gridFilters.UserDeletedRow += (s, e) => OnFilterChanged();
            gridFilters.RowsAdded += (s, e) =>
            {
                // Initialize dropdowns for new rows
                for (int i = e.RowIndex; i < e.RowIndex + e.RowCount && i < gridFilters.Rows.Count; i++)
                {
                    InitializeFieldDropdown(i);
                    InitializeOperatorDropdown(i);
                }
            };

            btnAddFilter.Click += (s, e) => AddFilter();
            btnClearFilters.Click += (s, e) => ClearFilters();

            // Add initial blank row
            if (_filterRows.Count == 0)
            {
                _filterRows.Add(new FilterRow());
            }
            
            // Initialize operator dropdowns for any existing rows
            for (int i = 0; i < gridFilters.Rows.Count; i++)
            {
                InitializeOperatorDropdown(i);
            }
        }

        private void GridFilters_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is ComboBox combo && gridFilters.CurrentCell != null)
            {
                // Ensure operator column has its items when editing starts
                if (gridFilters.CurrentCell.ColumnIndex == colOperator.Index)
                {
                    // Populate the editing combobox directly with operators
                    combo.DataSource = null;
                    combo.Items.Clear();
                    combo.DisplayMember = nameof(FilterOperator.Display);
                    combo.ValueMember = nameof(FilterOperator.Code);
                    
                    foreach (var op in _operators)
                    {
                        combo.Items.Add(op);
                    }
                }
                
                // Remove previous handlers to prevent duplicates
                combo.SelectedIndexChanged -= ComboBox_SelectedIndexChanged;
                combo.SelectedIndexChanged += ComboBox_SelectedIndexChanged;
            }
        }

        private void ComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (sender is ComboBox)
            {
                gridFilters.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void GridFilters_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            // Don't do anything here - CellValueChanged fires during editing
            // We'll raise FilterChanged only when the edit is committed (user leaves the cell)
        }

        private void GridFilters_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            // Handle delete button click
            if (e.RowIndex >= 0 && e.ColumnIndex == colDelete.Index)
            {
                if (gridFilters.Rows.Count > 1 || _filterRows[e.RowIndex].Field != null)
                {
                    _filterRows.RemoveAt(e.RowIndex);

                    // Ensure at least one blank row exists
                    if (_filterRows.Count == 0)
                    {
                        _filterRows.Add(new FilterRow());
                    }
                }
            }
        }

        private void GridFilters_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            // Auto-open dropdown on first click for ComboBox cells
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var cell = gridFilters.Rows[e.RowIndex].Cells[e.ColumnIndex];
            
            if (cell is DataGridViewComboBoxCell)
            {
                // Begin edit mode if not already in it
                if (!gridFilters.IsCurrentCellInEditMode)
                {
                    gridFilters.BeginEdit(true);
                }

                // Show dropdown if we're editing a ComboBox
                if (gridFilters.EditingControl is ComboBox combo)
                {
                    combo.DroppedDown = true;
                }
            }
        }

        private void InitializeFieldDropdown(int rowIndex)
        {
            if (rowIndex >= 0 && rowIndex < gridFilters.Rows.Count)
            {
                var cell = gridFilters.Rows[rowIndex].Cells[colField.Index] as DataGridViewComboBoxCell;
                if (cell != null)
                {
                    var currentValue = cell.Value as string;
                    cell.Items.Clear();

                    foreach (var attr in _availableFields)
                    {
                        cell.Items.Add(attr);
                    }

                    cell.DisplayMember = nameof(AttributeItem.FilterDisplayName);
                    cell.ValueMember = nameof(AttributeItem.LogicalName);

                    // Restore previous value if still valid
                    if (!string.IsNullOrWhiteSpace(currentValue) &&
                        _availableFields.Any(a => a.LogicalName == currentValue))
                    {
                        cell.Value = currentValue;
                    }
                }
            }
        }

        private void InitializeOperatorDropdown(int rowIndex)
        {
            if (_isUpdating) return;
            
            if (rowIndex >= 0 && rowIndex < gridFilters.Rows.Count)
            {
                var cell = gridFilters.Rows[rowIndex].Cells[colOperator.Index] as DataGridViewComboBoxCell;
                if (cell != null)
                {
                    var currentValue = cell.Value as string;
                    cell.Items.Clear();

                    foreach (var op in _operators)
                    {
                        cell.Items.Add(op);
                    }

                    cell.DisplayMember = nameof(FilterOperator.Display);
                    cell.ValueMember = nameof(FilterOperator.Code);

                    // Restore previous value if still valid
                    if (!string.IsNullOrWhiteSpace(currentValue) &&
                        _operators.Any(o => o.Code == currentValue))
                    {
                        cell.Value = currentValue;
                    }
                }
            }
        }

        public void SetAvailableFields(List<AttributeItem> fields)
        {
            _availableFields = fields ?? new();
            RefreshFieldDropdowns();
        }

        private void RefreshFieldDropdowns()
        {
            for (int i = 0; i < gridFilters.Rows.Count; i++)
            {
                var cell = gridFilters.Rows[i].Cells[colField.Index] as DataGridViewComboBoxCell;
                if (cell != null)
                {
                    var currentValue = cell.Value as string;
                    cell.Items.Clear();

                    foreach (var attr in _availableFields)
                    {
                        cell.Items.Add(attr);
                    }

                    cell.DisplayMember = nameof(AttributeItem.FilterDisplayName);
                    cell.ValueMember = nameof(AttributeItem.LogicalName);

                    // Restore previous value if still valid
                    if (!string.IsNullOrWhiteSpace(currentValue) &&
                        _availableFields.Any(a => a.LogicalName == currentValue))
                    {
                        cell.Value = currentValue;
                    }
                }
            }
        }

        public void AddFilter()
        {
            _filterRows.Add(new FilterRow());
        }

        public void ClearFilters()
        {
            _filterRows.Clear();
            _filterRows.Add(new FilterRow());
            
            // Ensure operator dropdown is initialized for the new blank row
            if (gridFilters.Rows.Count > 0)
            {
                InitializeOperatorDropdown(0);
            }
        }

        public void LoadFilters(List<SavedFilterCriteria>? filters)
        {
            _filterRows.Clear();

            if (filters != null && filters.Count > 0)
            {
                foreach (var filter in filters)
                {
                    _filterRows.Add(new FilterRow
                    {
                        Field = filter.Field,
                        Operator = filter.Operator,
                        Value = filter.Value
                    });
                }
            }

            // Ensure at least one blank row
            if (_filterRows.Count == 0)
            {
                _filterRows.Add(new FilterRow());
            }
        }

        public List<SavedFilterCriteria> GetFilters()
        {
            return _filterRows
                .Where(f => !string.IsNullOrWhiteSpace(f.Field) &&
                           !string.IsNullOrWhiteSpace(f.Operator))
                .Select(f => new SavedFilterCriteria
                {
                    Field = f.Field,
                    Operator = f.Operator,
                    Value = f.Value
                })
                .ToList();
        }

        public string GetFilterString()
        {
            var filters = GetFilters();
            if (filters.Count == 0)
                return string.Empty;

            return string.Join(";", filters.Select(f =>
            {
                var parts = new List<string> { f.Field ?? string.Empty, f.Operator ?? string.Empty };
                
                // null/notnull operators don't need a value
                if (f.Operator != "null" && f.Operator != "notnull")
                {
                    parts.Add(f.Value ?? string.Empty);
                }
                else
                {
                    // Use "null" as placeholder value for null operators
                    parts.Add("null");
                }

                return string.Join("|", parts);
            }));
        }

        public void LoadFromFilterString(string? filterString)
        {
            _filterRows.Clear();

            if (!string.IsNullOrWhiteSpace(filterString))
            {
                var filters = filterString!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var filter in filters)
                {
                    var parts = filter.Split('|');
                    if (parts.Length >= 2)
                    {
                        _filterRows.Add(new FilterRow
                        {
                            Field = parts[0],
                            Operator = parts[1],
                            Value = parts.Length > 2 ? parts[2] : null
                        });
                    }
                }
            }

            // Ensure at least one blank row
            if (_filterRows.Count == 0)
            {
                _filterRows.Add(new FilterRow());
            }
        }

        protected virtual void OnFilterChanged()
        {
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
