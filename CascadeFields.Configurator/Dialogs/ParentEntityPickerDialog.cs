using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CascadeFields.Configurator.Dialogs
{
    public class ParentEntityItem
    {
        public string ParentEntity { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int ChildCount { get; set; }
        public List<ChildEntityInfo> ChildEntities { get; set; } = new List<ChildEntityInfo>();
        public DateTime LastModified { get; set; }
    }

    public class ChildEntityInfo
    {
        public string DisplayName { get; set; } = string.Empty;
        public string RelationshipName { get; set; } = string.Empty;
        public string LookupFieldDisplayName { get; set; } = string.Empty;

        public override string ToString() => $"{DisplayName} ({LookupFieldDisplayName}) ({RelationshipName})";
    }

    public partial class ParentEntityPickerDialog : Form
    {
        private readonly List<ParentEntityItem> _parents;

        public string? SelectedParentEntity { get; private set; }

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

        private void InitializeGrid()
        {
            // Grid initialization is done in InitializeComponent
        }
    }
}
