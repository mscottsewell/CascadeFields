using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CascadeFields.Configurator.Models.UI;

namespace CascadeFields.Configurator.Dialogs
{
    /// <summary>
    /// Dialog that groups configured relationships by parent entity so a user can reload them.
    /// </summary>
    internal class ConfigurationPickerDialog : Form
    {
        private readonly ListView _listView;
        private readonly Button _okButton;
        private readonly Button _cancelButton;
        private readonly Dictionary<string, ParentEntityGroup> _parentGroups;

        public ConfiguredRelationship? SelectedConfiguration { get; private set; }

        public ConfigurationPickerDialog(IEnumerable<ConfiguredRelationship> configurations)
        {
            Text = "Select Configured Entity";
            StartPosition = FormStartPosition.CenterParent;
            Width = 950;
            Height = 420;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Group configurations by parent entity
            _parentGroups = configurations
                .GroupBy(c => c.ParentEntity)
                .ToDictionary(
                    g => g.Key,
                    g => new ParentEntityGroup
                    {
                        ParentEntity = g.Key,
                        Children = g.ToList(),
                        RawJson = g.First().RawJson
                    });

            var label = new Label
            {
                Text = "Select a parent entity to load its configuration (with all child relationships):",
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new System.Windows.Forms.Padding(8)
            };

            _listView = new ListView
            {
                Dock = DockStyle.Top,
                Height = 300,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };

            _listView.Columns.Add("Parent Entity", 260);
            _listView.Columns.Add("Child", 220);
            _listView.Columns.Add("Lookup Field", 220);
            _listView.Columns.Add("Relationship Schema", 200);

            _listView.DoubleClick += (s, e) => { if (_listView.SelectedItems.Count > 0) HandleSelection(); };

            // Populate list with a parent row, followed by its child relationship rows
            foreach (var group in _parentGroups.Values.OrderBy(g => g.ParentEntity))
            {
                // Parent row (selectable)
                var parentItem = new ListViewItem(FormatEntity(group.ParentEntity))
                {
                    Tag = new RowTag { Group = group, IsChild = false }
                };
                parentItem.SubItems.Add("(all configured children)");
                parentItem.SubItems.Add(string.Empty);
                parentItem.SubItems.Add(string.Empty);
                _listView.Items.Add(parentItem);

                // Child rows (informational, selection maps back to parent)
                // Skip children without a relationship name to reduce noise; the parent row already represents them.
                var childRows = group.Children
                    .Where(c => !string.IsNullOrWhiteSpace(c.RelationshipName))
                    .ToList();

                foreach (var child in childRows)
                {
                    var lookup = FormatField(child.LookupFieldName, child.LookupFieldDisplayName);
                    var schema = string.IsNullOrWhiteSpace(child.RelationshipName) ? string.Empty : child.RelationshipName;

                    var childItem = new ListViewItem("    ↳")
                    {
                        Tag = new RowTag { Group = group, IsChild = true }
                    };
                    childItem.SubItems.Add(FormatEntity(child.ChildEntity, child.ChildEntityDisplayName));
                    childItem.SubItems.Add(lookup);
                    childItem.SubItems.Add(schema);
                    _listView.Items.Add(childItem);
                }
            }

            _listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            _listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);

            _okButton = new Button
            {
                Text = "OK",
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Left = 400,
                Top = 340,
                Width = 80
            };
            _okButton.Click += (s, e) => HandleSelection();

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Left = 490,
                Top = 340,
                Width = 80
            };

            Controls.Add(_listView);
            Controls.Add(label);
            Controls.Add(_okButton);
            Controls.Add(_cancelButton);

            AcceptButton = _okButton;
            CancelButton = _cancelButton;
        }

        private void HandleSelection()
        {
            if (_listView.SelectedItems.Count > 0 && _listView.SelectedItems[0].Tag is RowTag tag)
            {
                // Create a representative ConfiguredRelationship with the parent's full JSON
                SelectedConfiguration = new ConfiguredRelationship
                {
                    ParentEntity = tag.Group.ParentEntity,
                    ChildEntity = string.Join(", ", tag.Group.Children.Select(c => c.ChildEntity).Distinct()),
                    RelationshipName = string.Empty,
                    RawJson = tag.Group.RawJson
                };
                DialogResult = DialogResult.OK;
            }
        }

        private class RowTag
        {
            public ParentEntityGroup Group { get; set; } = null!;
            public bool IsChild { get; set; }
        }

        private static string FormatEntity(string logicalName, string? displayName = null)
        {
            var display = string.IsNullOrWhiteSpace(displayName) ? logicalName : displayName;
            return string.IsNullOrWhiteSpace(logicalName)
                ? display ?? string.Empty
                : $"{display} ({logicalName})";
        }

        private static string FormatField(string logicalName, string? displayName = null)
        {
            var display = string.IsNullOrWhiteSpace(displayName) ? logicalName : displayName;
            return string.IsNullOrWhiteSpace(logicalName)
                ? display ?? string.Empty
                : $"{display} ({logicalName})";
        }
        private class ParentEntityGroup
        {
            public string ParentEntity { get; set; } = string.Empty;
            public List<ConfiguredRelationship> Children { get; set; } = new();
            public string? RawJson { get; set; }
        }
    }
}
