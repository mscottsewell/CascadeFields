using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CascadeFields.Configurator.Models;

namespace CascadeFields.Configurator.Dialogs
{
    internal class ConfigurationPickerDialog : Form
    {
        private readonly ListBox _listBox;
        private readonly Button _okButton;
        private readonly Button _cancelButton;

        public ConfiguredRelationship? SelectedConfiguration => _listBox.SelectedItem as ConfiguredRelationship;

        public ConfigurationPickerDialog(IEnumerable<ConfiguredRelationship> configurations)
        {
            Text = "Select Configuration";
            StartPosition = FormStartPosition.CenterParent;
            Width = 480;
            Height = 360;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _listBox = new ListBox
            {
                Dock = DockStyle.Top,
                Height = 260
            };

            _listBox.DoubleClick += (s, e) => { if (SelectedConfiguration != null) DialogResult = DialogResult.OK; };

            foreach (var item in configurations.OrderBy(c => c.DisplayName))
            {
                _listBox.Items.Add(item);
            }

            _okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Left = 280,
                Top = 280,
                Width = 80
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Left = 370,
                Top = 280,
                Width = 80
            };

            Controls.Add(_listBox);
            Controls.Add(_okButton);
            Controls.Add(_cancelButton);

            AcceptButton = _okButton;
            CancelButton = _cancelButton;
        }
    }
}
