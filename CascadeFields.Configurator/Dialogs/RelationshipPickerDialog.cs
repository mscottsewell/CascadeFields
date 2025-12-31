using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CascadeFields.Configurator.Models.UI;

namespace CascadeFields.Configurator.Dialogs
{
    /// <summary>
    /// Dialog that lists available relationships to add for the selected parent entity.
    /// </summary>
    internal class RelationshipPickerDialog : Form
    {
        private readonly DataGridView _grid;
        private readonly Button _okButton;
        private readonly Button _cancelButton;

        public RelationshipItem? SelectedRelationship { get; private set; }

        public RelationshipPickerDialog(IEnumerable<RelationshipItem> availableRelationships)
        {
            Text = "Select Relationship to Add";
            StartPosition = FormStartPosition.CenterParent;
            Width = 700;
            Height = 400;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var label = new Label
            {
                Text = "Select a relationship to add to this parent entity:",
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(8),
                AutoSize = true
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            // Entity Name column
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "EntityName",
                HeaderText = "Entity Name",
                Width = 150,
                ReadOnly = true
            });

            // Lookup Field column
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LookupField",
                HeaderText = "Lookup Field",
                Width = 150,
                ReadOnly = true
            });

            // Schema Name column
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SchemaName",
                HeaderText = "Relationship Schema Name",
                Width = 250,
                ReadOnly = true
            });

            // Populate grid with relationships
            foreach (var rel in availableRelationships.OrderBy(r => r.ReferencingEntity))
            {
                var entityDisplay = string.IsNullOrWhiteSpace(rel.ChildEntityDisplayName)
                    ? rel.ReferencingEntity
                    : $"{rel.ChildEntityDisplayName} ({rel.ReferencingEntity})";

                var lookupDisplay = rel.LookupFieldDisplayName;
                if (string.IsNullOrWhiteSpace(lookupDisplay))
                {
                    lookupDisplay = rel.ReferencingAttribute;
                }
                else
                {
                    lookupDisplay = $"{lookupDisplay} ({rel.ReferencingAttribute})";
                }

                _grid.Rows.Add(
                    entityDisplay,
                    lookupDisplay,
                    rel.SchemaName
                );

                // Store relationship in row tag for retrieval
                _grid.Rows[_grid.Rows.Count - 1].Tag = rel;
            }

            _grid.DoubleClick += (s, e) => HandleSelection();

            _okButton = new Button
            {
                Text = "OK",
                Width = 90,
                Margin = new Padding(6)
            };
            _okButton.Click += (s, e) => HandleSelection();

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Margin = new Padding(6)
            };

            var buttonFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(0),
                WrapContents = false
            };
            buttonFlow.Controls.Add(_cancelButton);
            buttonFlow.Controls.Add(_okButton);

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(8)
            };
            buttonPanel.Controls.Add(buttonFlow);

            Controls.Add(buttonPanel);
            Controls.Add(_grid);
            Controls.Add(label);

            AcceptButton = _okButton;
            CancelButton = _cancelButton;
        }

        private void HandleSelection()
        {
            if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is RelationshipItem rel)
            {
                SelectedRelationship = rel;
                DialogResult = DialogResult.OK;
            }
        }
    }
}
