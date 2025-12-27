using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using XrmToolBox.Extensibility;

namespace CascadeFields.Configurator.Controls
{
    partial class CascadeFieldsConfiguratorControl : PluginControlBase
    {
        private IContainer components = null!;
        private FlowLayoutPanel ribbonPanel;
        private Button btnLoadMetadata;
        private Button btnRetrieveConfigured;
        private Button btnUpdatePlugin;
        private Button btnPublish;
        private Button btnClearSession;
        private SplitContainer splitContainer;
        private TableLayoutPanel leftLayout;
        private Label lblSolution;
        private ComboBox cmbSolution;
        private Label lblParentEntity;
        private ComboBox cmbParentEntity;
        private Label lblParentForm;
        private ComboBox cmbParentForm;
        private Label lblChildEntity;
        private ComboBox cmbChildEntity;
        private Label lblChildForm;
        private ComboBox cmbChildForm;
        private Label lblLog;
        private TextBox txtLog;
        private TabControl tabControl;
        private TabPage tabMappings;
        private TabPage tabJson;
        private DataGridView gridMappings;
        private DataGridViewComboBoxColumn colSourceField;
        private DataGridViewComboBoxColumn colTargetField;
        private DataGridViewCheckBoxColumn colTrigger;
        private DataGridViewButtonColumn colDelete;
        private TextBox txtJsonPreview;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            components = new Container();
            ribbonPanel = new FlowLayoutPanel();
            btnLoadMetadata = new Button();
            btnRetrieveConfigured = new Button();
            btnUpdatePlugin = new Button();
            btnPublish = new Button();
            btnClearSession = new Button();
            splitContainer = new SplitContainer();
            leftLayout = new TableLayoutPanel();
            lblSolution = new Label();
            cmbSolution = new ComboBox();
            lblParentEntity = new Label();
            cmbParentEntity = new ComboBox();
            lblParentForm = new Label();
            cmbParentForm = new ComboBox();
            lblChildEntity = new Label();
            cmbChildEntity = new ComboBox();
            lblChildForm = new Label();
            cmbChildForm = new ComboBox();
            lblLog = new Label();
            txtLog = new TextBox();
            tabControl = new TabControl();
            tabMappings = new TabPage();
            gridMappings = new DataGridView();
            colSourceField = new DataGridViewComboBoxColumn();
            colTargetField = new DataGridViewComboBoxColumn();
            colTrigger = new DataGridViewCheckBoxColumn();
            colDelete = new DataGridViewButtonColumn();
            tabJson = new TabPage();
            txtJsonPreview = new TextBox();
            ribbonPanel.SuspendLayout();
            ((ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            leftLayout.SuspendLayout();
            tabControl.SuspendLayout();
            tabMappings.SuspendLayout();
            ((ISupportInitialize)gridMappings).BeginInit();
            tabJson.SuspendLayout();
            SuspendLayout();
            // 
            // ribbonPanel
            // 
            ribbonPanel.AutoSize = true;
            ribbonPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ribbonPanel.Dock = DockStyle.Top;
            ribbonPanel.Padding = new Padding(6);
            ribbonPanel.WrapContents = false;
            ribbonPanel.Controls.AddRange(new Control[]
            {
                btnLoadMetadata,
                btnRetrieveConfigured,
                btnUpdatePlugin,
                btnPublish,
                btnClearSession
            });
            // 
            // btnLoadMetadata
            // 
            btnLoadMetadata.FlatStyle = FlatStyle.Flat;
            btnLoadMetadata.Text = "Load Metadata";
            btnLoadMetadata.AutoSize = true;
            btnLoadMetadata.Margin = new Padding(4);
            // 
            // btnRetrieveConfigured
            // 
            btnRetrieveConfigured.FlatStyle = FlatStyle.Flat;
            btnRetrieveConfigured.Text = "Retrieve Configured Entity";
            btnRetrieveConfigured.AutoSize = true;
            btnRetrieveConfigured.Margin = new Padding(4);
            // 
            // btnUpdatePlugin
            // 
            btnUpdatePlugin.FlatStyle = FlatStyle.Flat;
            btnUpdatePlugin.Text = "Update Cascade Fields Plug-in";
            btnUpdatePlugin.AutoSize = true;
            btnUpdatePlugin.Margin = new Padding(4);
            // 
            // btnPublish
            // 
            btnPublish.FlatStyle = FlatStyle.Flat;
            btnPublish.Text = "Publish Configuration";
            btnPublish.AutoSize = true;
            btnPublish.Margin = new Padding(4);
            // 
            // btnClearSession
            // 
            btnClearSession.FlatStyle = FlatStyle.Flat;
            btnClearSession.Text = "Clear Session";
            btnClearSession.AutoSize = true;
            btnClearSession.Margin = new Padding(4);
            // 
            // splitContainer
            // 
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.SplitterDistance = 420;
            splitContainer.Panel1.Controls.Add(leftLayout);
            splitContainer.Panel2.Controls.Add(tabControl);
            // 
            // leftLayout
            // 
            leftLayout.ColumnCount = 2;
            leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            leftLayout.Dock = DockStyle.Fill;
            leftLayout.Padding = new Padding(8);
            leftLayout.RowCount = 8;
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 12F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            leftLayout.Controls.Add(lblSolution, 0, 0);
            leftLayout.Controls.Add(cmbSolution, 1, 0);
            leftLayout.Controls.Add(lblParentEntity, 0, 1);
            leftLayout.Controls.Add(cmbParentEntity, 1, 1);
            leftLayout.Controls.Add(lblParentForm, 0, 2);
            leftLayout.Controls.Add(cmbParentForm, 1, 2);
            leftLayout.Controls.Add(lblChildEntity, 0, 3);
            leftLayout.Controls.Add(cmbChildEntity, 1, 3);
            leftLayout.Controls.Add(lblChildForm, 0, 4);
            leftLayout.Controls.Add(cmbChildForm, 1, 4);
            leftLayout.Controls.Add(lblLog, 0, 6);
            leftLayout.SetColumnSpan(lblLog, 2);
            leftLayout.Controls.Add(txtLog, 0, 7);
            leftLayout.SetColumnSpan(txtLog, 2);
            // 
            // labels and combos
            // 
            lblSolution.Text = "Solution";
            lblSolution.Dock = DockStyle.Fill;
            lblSolution.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            cmbSolution.Dock = DockStyle.Fill;
            cmbSolution.DropDownStyle = ComboBoxStyle.DropDownList;

            lblParentEntity.Text = "Parent Entity";
            lblParentEntity.Dock = DockStyle.Fill;
            lblParentEntity.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            cmbParentEntity.Dock = DockStyle.Fill;
            cmbParentEntity.DropDownStyle = ComboBoxStyle.DropDownList;

            lblParentForm.Text = "Parent Form (optional)";
            lblParentForm.Dock = DockStyle.Fill;
            lblParentForm.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            cmbParentForm.Dock = DockStyle.Fill;
            cmbParentForm.DropDownStyle = ComboBoxStyle.DropDownList;

            lblChildEntity.Text = "Child Entity";
            lblChildEntity.Dock = DockStyle.Fill;
            lblChildEntity.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            cmbChildEntity.Dock = DockStyle.Fill;
            cmbChildEntity.DropDownStyle = ComboBoxStyle.DropDownList;

            lblChildForm.Text = "Child Form (optional)";
            lblChildForm.Dock = DockStyle.Fill;
            lblChildForm.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            cmbChildForm.Dock = DockStyle.Fill;
            cmbChildForm.DropDownStyle = ComboBoxStyle.DropDownList;

            lblLog.Text = "Log";
            lblLog.Dock = DockStyle.Fill;
            lblLog.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblLog.Margin = new Padding(0, 4, 0, 0);

            txtLog.Dock = DockStyle.Fill;
            txtLog.Multiline = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.ReadOnly = true;
            txtLog.Margin = new Padding(0, 2, 0, 0);

            // 
            // tabControl
            // 
            tabControl.Dock = DockStyle.Fill;
            tabControl.Controls.Add(tabMappings);
            tabControl.Controls.Add(tabJson);

            // 
            // tabMappings
            // 
            tabMappings.Text = "Field Mappings";
            tabMappings.Padding = new Padding(4);
            tabMappings.Controls.Add(gridMappings);

            // 
            // gridMappings
            // 
            gridMappings.Dock = DockStyle.Fill;
            gridMappings.AllowUserToAddRows = false;
            gridMappings.AutoGenerateColumns = false;
            gridMappings.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridMappings.RowHeadersVisible = false;
            gridMappings.Columns.AddRange(new DataGridViewColumn[] { colSourceField, colTargetField, colTrigger, colDelete });

            colSourceField.HeaderText = "Source: Parent Field";
            colSourceField.FillWeight = 42F;
            colSourceField.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
            colSourceField.DataPropertyName = "SourceField";

            colTargetField.HeaderText = "Destination: Child Field";
            colTargetField.FillWeight = 42F;
            colTargetField.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
            colTargetField.DataPropertyName = "TargetField";

            colTrigger.HeaderText = "Trigger";
            colTrigger.DataPropertyName = "IsTriggerField";
            colTrigger.FillWeight = 8F;
            
            colDelete.HeaderText = "";
            colDelete.Text = "✖";
            colDelete.UseColumnTextForButtonValue = true;
            colDelete.FillWeight = 8F;

            // 
            // tabJson
            // 
            tabJson.Text = "JSON Preview";
            tabJson.Padding = new Padding(4);
            tabJson.Controls.Add(txtJsonPreview);

            txtJsonPreview.Dock = DockStyle.Fill;
            txtJsonPreview.Multiline = true;
            txtJsonPreview.ScrollBars = ScrollBars.Both;
            txtJsonPreview.ReadOnly = true;
            txtJsonPreview.Font = new System.Drawing.Font("Consolas", 9F);

            // 
            // CascadeFieldsConfiguratorControl
            // 
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(splitContainer);
            Controls.Add(ribbonPanel);
            Name = nameof(CascadeFieldsConfiguratorControl);
            Dock = DockStyle.Fill;
            ribbonPanel.ResumeLayout(false);
            ribbonPanel.PerformLayout();
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            ((ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            leftLayout.ResumeLayout(false);
            leftLayout.PerformLayout();
            tabControl.ResumeLayout(false);
            tabMappings.ResumeLayout(false);
            ((ISupportInitialize)gridMappings).EndInit();
            tabJson.ResumeLayout(false);
            tabJson.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
