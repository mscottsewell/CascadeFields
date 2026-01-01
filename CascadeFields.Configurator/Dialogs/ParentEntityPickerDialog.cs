using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CascadeFields.Configurator.Dialogs
{
    /// <summary>
    /// Represents a configured parent entity and its associated child relationships for display in the picker grid.
    /// </summary>
    /// <remarks>
    /// Used as a data transfer object to group a parent entity with metadata about its
    /// configured child relationships. Supports data binding in the ParentEntityPickerDialog.
    /// </remarks>
    public class ParentEntityItem
    {
        /// <summary>
        /// Gets or sets the parent entity logical name.
        /// </summary>
        public string ParentEntity { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the parent entity display name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of configured child relationships for this parent.
        /// </summary>
        public int ChildCount { get; set; }

        /// <summary>
        /// Gets or sets the list of child entity descriptors showing configured relationships.
        /// </summary>
        public List<ChildEntityInfo> ChildEntities { get; set; } = new List<ChildEntityInfo>();

        /// <summary>
        /// Gets or sets the last modification timestamp for this parent entity's configuration.
        /// </summary>
        public DateTime LastModified { get; set; }
    }

    /// <summary>
    /// Lightweight descriptor for a child entity relationship displayed in the picker grid.
    /// </summary>
    /// <remarks>
    /// Provides formatted display information for a single child relationship including
    /// entity name, lookup field, and relationship schema name.
    /// </remarks>
    public class ChildEntityInfo
    {
        /// <summary>
        /// Gets or sets the child entity display name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the relationship schema name.
        /// </summary>
        public string RelationshipName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the lookup field display name.
        /// </summary>
        public string LookupFieldDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Returns a formatted string representation showing display name, lookup field, and relationship.
        /// </summary>
        /// <returns>String in the format "DisplayName (LookupFieldDisplayName) (RelationshipName)".</returns>
        public override string ToString() => $"{DisplayName} ({LookupFieldDisplayName}) ({RelationshipName})";
    }

    /// <summary>
    /// Modal dialog for selecting a previously configured parent entity to reload its configuration.
    /// </summary>
    /// <remarks>
    /// <para><strong>Purpose:</strong></para>
    /// <para>
    /// Displays a grid of parent entities that have existing cascade configurations, showing
    /// the number of child relationships and a multi-line list of destination entities.
    /// </para>
    ///
    /// <para><strong>UI Features:</strong></para>
    /// <list type="bullet">
    /// <item><description>Three-column grid: Source Entity, Destination Count, Destination(s)</description></item>
    /// <item><description>Multi-line Destination(s) column showing all child entities with wrap mode</description></item>
    /// <item><description>Full-row selection for easy parent choice</description></item>
    /// <item><description>Double-click or Open button to select</description></item>
    /// <item><description>Auto-sized rows to accommodate multiple destination lines</description></item>
    /// </list>
    ///
    /// <para><strong>Return Value:</strong></para>
    /// <para>
    /// SelectedParentEntity contains the parent entity logical name if the user selected and confirmed.
    /// </para>
    /// </remarks>
    public partial class ParentEntityPickerDialog : Form
    {
        private readonly List<ParentEntityItem> _parents;

        /// <summary>
        /// Gets the logical name of the selected parent entity, or null if canceled.
        /// </summary>
        public string? SelectedParentEntity { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParentEntityPickerDialog"/> class.
        /// </summary>
        /// <param name="parents">The list of parent entities with their configured child relationships.</param>
        public ParentEntityPickerDialog(List<ParentEntityItem> parents)
        {
            _parents = parents ?? new List<ParentEntityItem>();
            InitializeComponent();
            InitializeGrid();
        }

        private void InitializeComponent()
        {
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Select Configured Entity";
            this.ShowIcon = false;

            var gridParents = new DataGridView
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

            // Create columns
            var colEntity = new DataGridViewTextBoxColumn
            {
                HeaderText = "Source Entity",
                DataPropertyName = "DisplayName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };

            var colChildCount = new DataGridViewTextBoxColumn
            {
                HeaderText = "Destination Count",
                DataPropertyName = "ChildCount",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };

            var colDestinations = new DataGridViewTextBoxColumn
            {
                HeaderText = "Destination(s)",
                DataPropertyName = "ChildEntities",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            gridParents.Columns.AddRange(new DataGridViewColumn[] { colEntity, colChildCount, colDestinations });
            gridParents.DataSource = _parents;

            // Format the Destination(s) column to show a line-separated list
            gridParents.CellFormatting += (s, e) =>
            {
                if (e.ColumnIndex == 2 && e.Value is List<ChildEntityInfo> children && children.Count > 0)
                {
                    e.Value = string.Join(Environment.NewLine, children.Select(c => c.ToString()));
                    e.FormattingApplied = true;
                }
            };

            // Enable word wrap for multi-line destination display
            gridParents.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            gridParents.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

            gridParents.DoubleClick += (s, e) =>
            {
                if (gridParents.CurrentRow?.DataBoundItem is ParentEntityItem item)
                {
                    SelectedParentEntity = item.ParentEntity;
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
                Text = "Open",
                DialogResult = DialogResult.OK,
                Width = 100,
                Height = 30,
                Left = this.ClientSize.Width - 220,
                Top = 10
            };

            btnOK.Click += (s, e) =>
            {
                if (gridParents.CurrentRow?.DataBoundItem is ParentEntityItem item)
                {
                    SelectedParentEntity = item.ParentEntity;
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
            Controls.Add(gridParents);
        }

        /// <summary>
        /// Initializes the parent entity grid with data and formatting.
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
