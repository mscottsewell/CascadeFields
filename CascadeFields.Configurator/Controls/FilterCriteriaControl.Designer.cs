using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CascadeFields.Configurator.Controls
{
    partial class FilterCriteriaControl
    {
        private IContainer components = null!;
        private FlowLayoutPanel toolbarPanel;
        private Button btnAddFilter;
        private Button btnClearFilters;
        private Label lblPreview;
        private DataGridView gridFilters;
        private DataGridViewComboBoxColumn colField;
        private DataGridViewComboBoxColumn colOperator;
        private DataGridViewTextBoxColumn colValue;
        private DataGridViewButtonColumn colDelete;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new Container();
            toolbarPanel = new FlowLayoutPanel();
            btnAddFilter = new Button();
            btnClearFilters = new Button();
            lblPreview = new Label();
            gridFilters = new DataGridView();
            colField = new DataGridViewComboBoxColumn();
            colOperator = new DataGridViewComboBoxColumn();
            colValue = new DataGridViewTextBoxColumn();
            colDelete = new DataGridViewButtonColumn();
            toolbarPanel.SuspendLayout();
            ((ISupportInitialize)gridFilters).BeginInit();
            SuspendLayout();

            // 
            // toolbarPanel
            // 
            toolbarPanel.AutoSize = true;
            toolbarPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            toolbarPanel.Dock = DockStyle.Top;
            toolbarPanel.Padding = new Padding(4);
            toolbarPanel.Controls.AddRange(new Control[] { btnAddFilter, btnClearFilters, lblPreview });

            // 
            // btnAddFilter
            // 
            btnAddFilter.FlatStyle = FlatStyle.Flat;
            btnAddFilter.Text = "+ Add Filter";
            btnAddFilter.AutoSize = true;
            btnAddFilter.Margin = new Padding(2);

            // 
            // btnClearFilters
            // 
            btnClearFilters.FlatStyle = FlatStyle.Flat;
            btnClearFilters.Text = "Clear All";
            btnClearFilters.AutoSize = true;
            btnClearFilters.Margin = new Padding(2);

            // 
            // lblPreview
            // 
            lblPreview.Text = "Format: field|operator|value;field2|operator2|value2";
            lblPreview.AutoSize = true;
            lblPreview.Margin = new Padding(8, 6, 2, 2);
            lblPreview.ForeColor = SystemColors.GrayText;

            // 
            // gridFilters
            // 
            gridFilters.Dock = DockStyle.Fill;
            gridFilters.AllowUserToAddRows = false;
            gridFilters.AutoGenerateColumns = false;
            gridFilters.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridFilters.RowHeadersVisible = false;
            gridFilters.Columns.AddRange(new DataGridViewColumn[] { colField, colOperator, colValue, colDelete });

            // 
            // colField
            // 
            colField.HeaderText = "Field";
            colField.FillWeight = 40F;
            colField.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
            colField.DataPropertyName = "Field";

            // 
            // colOperator
            // 
            colOperator.HeaderText = "Operator";
            colOperator.FillWeight = 30F;
            colOperator.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
            colOperator.DataPropertyName = "Operator";
            // Note: Operator items are populated in InitializeBehavior using FilterOperator.GetAll()

            // 
            // colValue
            // 
            colValue.HeaderText = "Value";
            colValue.FillWeight = 22F;
            colValue.DataPropertyName = "Value";

            // 
            // colDelete
            // 
            colDelete.HeaderText = "";
            colDelete.Text = "✖";
            colDelete.UseColumnTextForButtonValue = true;
            colDelete.FillWeight = 8F;

            // 
            // FilterCriteriaControl
            // 
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(gridFilters);
            Controls.Add(toolbarPanel);
            Name = nameof(FilterCriteriaControl);
            Dock = DockStyle.Fill;
            toolbarPanel.ResumeLayout(false);
            toolbarPanel.PerformLayout();
            ((ISupportInitialize)gridFilters).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
