using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CascadeFields.Configurator.Models.UI;

namespace CascadeFields.Configurator.Dialogs
{
    /// <summary>
    /// Modal dialog for selecting a previously configured parent entity to reload its full cascade configuration.
    /// </summary>
    /// <remarks>
    /// <para><strong>Purpose:</strong></para>
    /// <para>
    /// Groups all configured relationships by their parent entity and presents them in a hierarchical
    /// list view. Users select a parent entity row to reload the complete configuration including
    /// all child relationships, field mappings, and filters.
    /// </para>
    ///
    /// <para><strong>Grouping Strategy:</strong></para>
    /// <para>
    /// Configurations are grouped by parent entity logical name. Each parent group shows:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Parent entity row (selectable) - loads all child relationships</description></item>
    /// <item><description>Indented child rows (informational) - show configured relationships</description></item>
    /// </list>
    ///
    /// <para><strong>UI Layout:</strong></para>
    /// <list type="bullet">
    /// <item><description>Four-column list view: Parent Entity, Child, Lookup Field, Relationship Schema</description></item>
    /// <item><description>Parent rows show "(all configured children)" in the child column</description></item>
    /// <item><description>Child rows are indented with "↳" for visual hierarchy</description></item>
    /// <item><description>Double-click or OK button to select</description></item>
    /// </list>
    ///
    /// <para><strong>Return Value:</strong></para>
    /// <para>
    /// SelectedConfiguration contains the parent entity and the complete JSON configuration
    /// for all its child relationships.
    /// </para>
    /// </remarks>
    internal class ConfigurationPickerDialog : Form
    {
        private readonly ListView _listView;
        private readonly Button _okButton;
        private readonly Button _cancelButton;
        private readonly Dictionary<string, ParentEntityGroup> _parentGroups;
        private readonly Dictionary<string, ListViewItem> _parentItems;
        private bool _isAdjustingSelection;

        /// <summary>
        /// Gets the selected configuration, or null if the dialog was canceled.
        /// </summary>
        /// <remarks>
        /// The SelectedConfiguration contains the parent entity name and the full JSON configuration
        /// string representing all configured child relationships.
        /// </remarks>
        public ConfiguredRelationship? SelectedConfiguration { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationPickerDialog"/> class.
        /// </summary>
        /// <param name="configurations">The collection of configured relationships to display.</param>
        public ConfigurationPickerDialog(IEnumerable<ConfiguredRelationship> configurations)
        {
            Text = "Select Configured Entity";
            StartPosition = FormStartPosition.CenterParent;
            Width = 950;
            Height = 420;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _parentItems = new Dictionary<string, ListViewItem>(StringComparer.OrdinalIgnoreCase);

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
            _listView.ItemSelectionChanged += ListView_ItemSelectionChanged;

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

                if (!_parentItems.ContainsKey(group.ParentEntity))
                {
                    _parentItems[group.ParentEntity] = parentItem;
                }

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

        /// <summary>
        /// Forces selection back to the parent row when a child row is clicked, so loading always targets the parent and all children.
        /// </summary>
        private void ListView_ItemSelectionChanged(object? sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (_isAdjustingSelection)
                return;

            if (e.IsSelected && e.Item.Tag is RowTag tag && tag.IsChild)
            {
                if (_parentItems.TryGetValue(tag.Group.ParentEntity, out var parentItem))
                {
                    _isAdjustingSelection = true;
                    try
                    {
                        _listView.SelectedItems.Clear();
                        parentItem.Selected = true;
                        parentItem.Focused = true;
                        parentItem.EnsureVisible();
                    }
                    finally
                    {
                        _isAdjustingSelection = false;
                    }
                }
            }
        }

        /// <summary>
        /// Handles the user's selection when OK is clicked or an item is double-clicked.
        /// </summary>
        /// <remarks>
        /// Creates a ConfiguredRelationship containing the parent entity and the complete JSON
        /// configuration for all child relationships in that parent's group.
        /// </remarks>
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

        /// <summary>
        /// Tag object attached to each list view item to associate it with its parent group.
        /// </summary>
        private class RowTag
        {
            public ParentEntityGroup Group { get; set; } = null!;
            public bool IsChild { get; set; }
        }

        /// <summary>
        /// Formats an entity for display as "DisplayName (LogicalName)" or just the logical name if display name is missing.
        /// </summary>
        /// <param name="logicalName">The entity logical name.</param>
        /// <param name="displayName">The entity display name (optional).</param>
        /// <returns>Formatted entity string.</returns>
        private static string FormatEntity(string logicalName, string? displayName = null)
        {
            var display = string.IsNullOrWhiteSpace(displayName) ? logicalName : displayName;
            return string.IsNullOrWhiteSpace(logicalName)
                ? display ?? string.Empty
                : $"{display} ({logicalName})";
        }

        /// <summary>
        /// Formats a field for display as "DisplayName (LogicalName)" or just the logical name if display name is missing.
        /// </summary>
        /// <param name="logicalName">The field logical name.</param>
        /// <param name="displayName">The field display name (optional).</param>
        /// <returns>Formatted field string.</returns>
        private static string FormatField(string logicalName, string? displayName = null)
        {
            var display = string.IsNullOrWhiteSpace(displayName) ? logicalName : displayName;
            return string.IsNullOrWhiteSpace(logicalName)
                ? display ?? string.Empty
                : $"{display} ({logicalName})";
        }

        /// <summary>
        /// Internal grouping class that aggregates all child relationships under a common parent entity.
        /// </summary>
        private class ParentEntityGroup
        {
            public string ParentEntity { get; set; } = string.Empty;
            public List<ConfiguredRelationship> Children { get; set; } = new();
            public string? RawJson { get; set; }
        }
    }
}
