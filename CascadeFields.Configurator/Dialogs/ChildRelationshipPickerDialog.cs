using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CascadeFields.Configurator.Models.UI;

namespace CascadeFields.Configurator.Dialogs
{
    /// <summary>
    /// Modal dialog for selecting a child relationship to configure for cascade operations.
    /// </summary>
    /// <remarks>
    /// <para><strong>Purpose:</strong></para>
    /// <para>
    /// Displays available child relationships for a parent entity in a selectable grid format,
    /// allowing users to choose which relationship they want to add to their cascade configuration.
    /// </para>
    ///
    /// <para><strong>UI Features:</strong></para>
    /// <list type="bullet">
    /// <item><description>Three-column grid showing entity name, lookup field, and relationship schema</description></item>
    /// <item><description>Full-row selection for easy relationship choice</description></item>
    /// <item><description>Double-click to select and close</description></item>
    /// <item><description>Displays both display names and logical names for clarity</description></item>
    /// </list>
    ///
    /// <para><strong>Usage:</strong></para>
    /// <para>
    /// Instantiate with available relationships, call ShowDialog(), and check SelectedRelationship
    /// if DialogResult is OK.
    /// </para>
    /// </remarks>
    public partial class ChildRelationshipPickerDialog : Form
    {
        private readonly List<RelationshipItem> _availableRelationships;

        /// <summary>
        /// Gets the relationship selected by the user, or null if canceled.
        /// </summary>
        public RelationshipItem? SelectedRelationship { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChildRelationshipPickerDialog"/> class.
        /// </summary>
        /// <param name="relationships">The list of available child relationships to display.</param>
        public ChildRelationshipPickerDialog(List<RelationshipItem> relationships)
        {
            _availableRelationships = relationships ?? new List<RelationshipItem>();
            InitializeComponent();
            InitializeGrid();
        }

        private void InitializeComponent()
        {
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Select Child Relationship";
            this.ShowIcon = false;

            var gridRelationships = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true
            };

            // Create columns with auto-sizing
            var colDestination = new DataGridViewTextBoxColumn
            {
                HeaderText = "Destination Entity",
                DataPropertyName = "ChildEntityDisplayName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };

            var colLookupField = new DataGridViewTextBoxColumn
            {
                HeaderText = "Lookup Field",
                DataPropertyName = "LookupFieldDisplayName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };

            var colSchema = new DataGridViewTextBoxColumn
            {
                HeaderText = "Schema Name",
                DataPropertyName = "SchemaName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            gridRelationships.Columns.AddRange(new DataGridViewColumn[] { colDestination, colLookupField, colSchema });
            gridRelationships.DataSource = _availableRelationships;

            // Format the Destination Entity column to show DisplayName (SchemaName)
            gridRelationships.CellFormatting += (s, e) =>
            {
                if (e.ColumnIndex == 0 && e.RowIndex >= 0)
                {
                    if (gridRelationships.Rows[e.RowIndex].DataBoundItem is RelationshipItem rel)
                    {
                        e.Value = $"{rel.ChildEntityDisplayName} ({rel.ReferencingEntity})";
                        e.FormattingApplied = true;
                    }
                }
            };

            // Format the Lookup Field column to show DisplayName (SchemaName)
            gridRelationships.CellFormatting += (s, e) =>
            {
                if (e.ColumnIndex == 1 && e.RowIndex >= 0)
                {
                    if (gridRelationships.Rows[e.RowIndex].DataBoundItem is RelationshipItem rel)
                    {
                        e.Value = $"{rel.LookupFieldDisplayName} ({rel.ReferencingAttribute})";
                        e.FormattingApplied = true;
                    }
                }
            };

            gridRelationships.DoubleClick += (s, e) =>
            {
                if (gridRelationships.CurrentRow?.DataBoundItem is RelationshipItem rel)
                {
                    SelectedRelationship = rel;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            var panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(8)
            };

            var btnOK = new Button
            {
                Text = "Select",
                DialogResult = DialogResult.OK,
                Width = 100,
                Height = 30,
                Left = this.ClientSize.Width - 220,
                Top = 10
            };

            btnOK.Click += (s, e) =>
            {
                if (gridRelationships.CurrentRow?.DataBoundItem is RelationshipItem rel)
                {
                    SelectedRelationship = rel;
                }
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 100,
                Height = 30,
                Left = this.ClientSize.Width - 110,
                Top = 10
            };

            panelButtons.Controls.Add(btnCancel);
            panelButtons.Controls.Add(btnOK);

            Controls.Add(panelButtons);
            Controls.Add(gridRelationships);
        }

        /// <summary>
        /// Initializes the relationship grid with data and formatting.
        /// </summary>
        /// <remarks>
        /// Grid initialization is handled in InitializeComponent for this dialog.
        /// </remarks>
        private void InitializeGrid()
        {
            // Grid initialization is done in InitializeComponent
        }
    }
}
