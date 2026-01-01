using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CascadeFields.Configurator.Models;
using CascadeFields.Configurator.Models.UI;

namespace CascadeFields.Configurator.Controls
{
    /// <summary>
    /// User control for editing filter criteria in a data grid format.
    /// </summary>
    /// <remarks>
    /// <para><strong>Purpose:</strong></para>
    /// <para>
    /// Provides a grid-based interface for defining filter conditions on child entity records.
    /// Filters are used to determine which child records should be affected by cascade operations.
    /// </para>
    ///
    /// <para><strong>Filter Format:</strong></para>
    /// <para>
    /// Each filter row consists of three components:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Field: The attribute to filter on (dropdown from available child entity attributes)</description></item>
    /// <item><description>Operator: Comparison operator (eq, ne, gt, lt, null, notnull, etc.)</description></item>
    /// <item><description>Value: The value to compare against (disabled for null/notnull operators)</description></item>
    /// </list>
    ///
    /// <para><strong>Features:</strong></para>
    /// <list type="bullet">
    /// <item><description>Automatic row addition when the last row is populated</description></item>
    /// <item><description>Per-row delete buttons for easy removal</description></item>
    /// <item><description>Add and Clear buttons for bulk operations</description></item>
    /// <item><description>Custom dropdown styling with owner-drawn combo boxes</description></item>
    /// <item><description>Automatic value cell disabling for null operators</description></item>
    /// <item><description>FilterChanged event for real-time validation and preview</description></item>
    /// </list>
    ///
    /// <para><strong>String Serialization:</strong></para>
    /// <para>
    /// Filters are serialized to/from a string format: "field1|op1|value1;field2|op2|value2"
    /// This format is stored in the JSON configuration and passed to the plugin.
    /// </para>
    /// </remarks>
    public partial class FilterCriteriaControl : UserControl
    {
        private readonly BindingList<FilterRow> _filterRows = new();
        private readonly List<FilterOperator> _operators = FilterOperator.GetAll();
        private List<AttributeItem> _availableFields = new();
        private bool _isUpdating = false;

        /// <summary>
        /// Occurs when the filter criteria collection changes.
        /// </summary>
        /// <remarks>
        /// Raised after cell edits, row additions, or row deletions to notify parent controls
        /// that the filter configuration has changed and may need validation or JSON regeneration.
        /// </remarks>
        public event EventHandler? FilterChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterCriteriaControl"/> class.
        /// </summary>
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
            
            // Initialize operator column with data source at column level
            colOperator.DataSource = _operators;
            colOperator.DisplayMember = nameof(FilterOperator.Display);
            colOperator.ValueMember = nameof(FilterOperator.Code);
            
            gridFilters.CellFormatting += GridFilters_CellFormatting;
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
                // Initialize dropdowns and value cell states for new rows
                for (int i = e.RowIndex; i < e.RowIndex + e.RowCount && i < gridFilters.Rows.Count; i++)
                {
                    InitializeFieldDropdown(i);
                    InitializeOperatorDropdown(i);
                    InitializeValueCellState(i);
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

        private void GridFilters_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            // CellFormatting is kept for any future custom formatting needs
            // Operator column now uses column-level DataSource, so no special handling needed
        }

        private void GridFilters_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is ComboBox combo && gridFilters.CurrentCell != null)
            {
                ApplyComboBoxStyling(combo);

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
            string displayText;

            if (item is AttributeItem attribute)
            {
                displayText = attribute.FilterDisplayName ?? attribute.LogicalName ?? string.Empty;
            }
            else if (item is FilterOperator op)
            {
                displayText = op.Display ?? op.Code ?? string.Empty;
            }
            else
            {
                displayText = item?.ToString() ?? string.Empty;
            }

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

        private void GridFilters_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            // Handle operator change - clear/disable value for null operators
            if (e.RowIndex >= 0 && e.ColumnIndex == colOperator.Index)
            {
                var row = gridFilters.Rows[e.RowIndex];
                var operatorCell = row.Cells[colOperator.Index];
                var valueCell = row.Cells[colValue.Index];
                
                string? operatorCode = operatorCell.Value as string;
                bool isNullOperator = operatorCode == "null" || operatorCode == "notnull";
                
                // Clear and disable value cell if using null operator
                if (isNullOperator)
                {
                    valueCell.Value = null;
                    valueCell.ReadOnly = true;
                    valueCell.Style.BackColor = System.Drawing.Color.LightGray;
                }
                else
                {
                    valueCell.ReadOnly = false;
                    valueCell.Style.BackColor = System.Drawing.Color.White;
                }
            }
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
            
            if (rowIndex >= 0 && rowIndex < gridFilters.Rows.Count && rowIndex < _filterRows.Count)
            {
                var cell = gridFilters.Rows[rowIndex].Cells[colOperator.Index] as DataGridViewComboBoxCell;
                if (cell != null)
                {
                    // Get the current value from the bound FilterRow data
                    var currentValue = _filterRows[rowIndex].Operator;

                    // Since column DataSource is set, just ensure the value is set correctly
                    if (!string.IsNullOrWhiteSpace(currentValue))
                    {
                        cell.Value = currentValue;
                        // Force the cell to refresh its display
                        gridFilters.InvalidateCell(cell);
                    }
                }
            }
        }

        private void InitializeValueCellState(int rowIndex)
        {
            if (rowIndex >= 0 && rowIndex < gridFilters.Rows.Count)
            {
                var row = gridFilters.Rows[rowIndex];
                var operatorCell = row.Cells[colOperator.Index];
                var valueCell = row.Cells[colValue.Index];
                
                string? operatorCode = operatorCell.Value as string;
                bool isNullOperator = operatorCode == "null" || operatorCode == "notnull";
                
                // Disable and clear value cell if using null operator
                if (isNullOperator)
                {
                    valueCell.Value = null;
                    valueCell.ReadOnly = true;
                    valueCell.Style.BackColor = System.Drawing.Color.LightGray;
                }
                else
                {
                    valueCell.ReadOnly = false;
                    valueCell.Style.BackColor = System.Drawing.Color.White;
                }
            }
        }

        /// <summary>
        /// Sets the list of available attributes for the field dropdown.
        /// </summary>
        /// <param name="fields">The list of child entity attributes to make available for filtering.</param>
        /// <remarks>
        /// This method should be called whenever the child entity changes to populate the
        /// field dropdowns with appropriate attributes. Refreshes all existing field dropdowns
        /// while preserving currently selected values if they remain valid.
        /// </remarks>
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

        /// <summary>
        /// Adds a new blank filter row to the grid.
        /// </summary>
        /// <remarks>
        /// Called when the user clicks the Add Filter button. The new row is added to the end
        /// of the filter list and is ready for immediate editing.
        /// </remarks>
        public void AddFilter()
        {
            _filterRows.Add(new FilterRow());
        }

        /// <summary>
        /// Removes all filter rows and adds a single blank row.
        /// </summary>
        /// <remarks>
        /// Resets the filter grid to its initial state with one empty row.
        /// Initializes operator dropdowns and value cell states for the new row.
        /// </remarks>
        public void ClearFilters()
        {
            _filterRows.Clear();
            _filterRows.Add(new FilterRow());

            // Ensure operator dropdown and value cell state are initialized for the new blank row
            if (gridFilters.Rows.Count > 0)
            {
                InitializeOperatorDropdown(0);
                InitializeValueCellState(0);
            }
        }

        /*
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

            // Defer initialization until after the grid has processed the data binding
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => InitializeLoadedFilters()));
            }
            else
            {
                InitializeLoadedFilters();
            }
        }

        private void InitializeLoadedFilters()
        {
            // Initialize dropdowns and value cell states for all loaded rows
            for (int i = 0; i < gridFilters.Rows.Count; i++)
            {
                InitializeFieldDropdown(i);
                InitializeOperatorDropdown(i);
                InitializeValueCellState(i);
            }
            
            // Force the grid to refresh its display to show the operator values
            gridFilters.Refresh();
        }
        */

        /// <summary>
        /// Gets the list of valid filter criteria from the grid.
        /// </summary>
        /// <returns>
        /// A list of <see cref="SavedFilterCriteria"/> objects representing all rows that have
        /// at least a field and operator specified.
        /// </returns>
        /// <remarks>
        /// Blank rows and rows with missing required fields are excluded from the result.
        /// </remarks>
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

        /// <summary>
        /// Serializes the filter criteria to the pipe-and-semicolon string format.
        /// </summary>
        /// <returns>
        /// A string in the format "field1|op1|value1;field2|op2|value2" or empty string if no valid filters exist.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This format is used in the JSON configuration and passed to the plugin for execution.
        /// </para>
        /// <para>
        /// Special handling for null operators: The value component is set to "null" as a
        /// placeholder since null/notnull operators don't require a comparison value.
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// Loads filter criteria from the pipe-and-semicolon string format.
        /// </summary>
        /// <param name="filterString">
        /// The filter string in the format "field1|op1|value1;field2|op2|value2" or null/empty to clear filters.
        /// </param>
        /// <remarks>
        /// <para>
        /// Clears existing filters and populates the grid with the parsed filter rows.
        /// Ensures at least one blank row exists after loading.
        /// </para>
        /// <para>
        /// Each filter segment is separated by semicolons, and each field/operator/value is
        /// separated by pipes.
        /// </para>
        /// </remarks>
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

            // Initialize value cell states for all loaded rows
            for (int i = 0; i < gridFilters.Rows.Count; i++)
            {
                InitializeValueCellState(i);
            }
        }

        /// <summary>
        /// Raises the <see cref="FilterChanged"/> event.
        /// </summary>
        /// <remarks>
        /// Called after any modification to the filter criteria (cell edits, additions, deletions).
        /// Allows parent controls to respond to filter changes for validation or JSON updates.
        /// </remarks>
        protected virtual void OnFilterChanged()
        {
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
