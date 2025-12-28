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
        private Button btnAddChildRelationship;
        private Button btnRetrieveConfigured;
        private Button btnUpdatePlugin;
        private Button btnPublish;
        private Button btnClearSession;
        private SplitContainer splitContainerMain;
        private SplitContainer splitContainerLeft;
        private SplitContainer splitContainerRight;
        private TableLayoutPanel leftUpperLayout;
        private TabControl tabControlLeftLower;
        private TabPage tabLog;
        private TabPage tabJson;
        private TabControl tabControlRightUpper;
        private Panel panelRightLower;
        private Label lblSolution;
        private ComboBox cmbSolution;
        private Label lblParentEntity;
        private ComboBox cmbParentEntity;
        private TextBox txtLog;
        private TextBox txtJsonPreview;
        private DataGridView gridMappings;
        private DataGridViewComboBoxColumn colSourceField;
        private DataGridViewComboBoxColumn colTargetField;
        private DataGridViewCheckBoxColumn colTrigger;
        private DataGridViewButtonColumn colDelete;
        private FilterCriteriaControl filterControl;
        private CheckBox chkEnableTracing;

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
            btnAddChildRelationship = new Button();
            btnRetrieveConfigured = new Button();
            btnUpdatePlugin = new Button();
            btnPublish = new Button();
            btnClearSession = new Button();
            splitContainerMain = new SplitContainer();
            splitContainerLeft = new SplitContainer();
            leftUpperLayout = new TableLayoutPanel();
            lblSolution = new Label();
            cmbSolution = new ComboBox();
            lblParentEntity = new Label();
            cmbParentEntity = new ComboBox();
            tabControlLeftLower = new TabControl();
            tabLog = new TabPage();
            txtLog = new TextBox();
            tabJson = new TabPage();
            txtJsonPreview = new TextBox();
            splitContainerRight = new SplitContainer();
            tabControlRightUpper = new TabControl();
            panelRightLower = new Panel();
            filterControl = new FilterCriteriaControl();
            chkEnableTracing = new CheckBox();
            gridMappings = new DataGridView();
            colSourceField = new DataGridViewComboBoxColumn();
            colTargetField = new DataGridViewComboBoxColumn();
            colTrigger = new DataGridViewCheckBoxColumn();
            colDelete = new DataGridViewButtonColumn();
            ribbonPanel.SuspendLayout();
            ((ISupportInitialize)splitContainerMain).BeginInit();
            splitContainerMain.Panel1.SuspendLayout();
            splitContainerMain.Panel2.SuspendLayout();
            splitContainerMain.SuspendLayout();
            ((ISupportInitialize)splitContainerLeft).BeginInit();
            splitContainerLeft.Panel1.SuspendLayout();
            splitContainerLeft.Panel2.SuspendLayout();
            splitContainerLeft.SuspendLayout();
            leftUpperLayout.SuspendLayout();
            tabControlLeftLower.SuspendLayout();
            tabLog.SuspendLayout();
            tabJson.SuspendLayout();
            ((ISupportInitialize)splitContainerRight).BeginInit();
            splitContainerRight.Panel1.SuspendLayout();
            splitContainerRight.Panel2.SuspendLayout();
            splitContainerRight.SuspendLayout();
            tabControlRightUpper.SuspendLayout();
            panelRightLower.SuspendLayout();
            ((ISupportInitialize)gridMappings).BeginInit();
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
                btnAddChildRelationship,
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
            // btnAddChildRelationship
            // 
            btnAddChildRelationship.FlatStyle = FlatStyle.Flat;
            btnAddChildRelationship.Text = "Add Child Relationship";
            btnAddChildRelationship.AutoSize = true;
            btnAddChildRelationship.Margin = new Padding(4);
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
            // splitContainerMain
            // 
            splitContainerMain.Dock = DockStyle.Fill;
            splitContainerMain.SplitterDistance = 420;
            
            // Create a panel for left side with fixed controls at top
            var leftPanel = new Panel { Dock = DockStyle.Fill };
            
            // Add fixed layout for all entity/form selectors at top
            var fixedLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(8)
            };
            fixedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            fixedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            fixedLayout.RowCount = 2;  // Solution + Parent Entity
            fixedLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            fixedLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            fixedLayout.Controls.Add(lblSolution, 0, 0);
            fixedLayout.Controls.Add(cmbSolution, 1, 0);
            fixedLayout.Controls.Add(lblParentEntity, 0, 1);
            fixedLayout.Controls.Add(cmbParentEntity, 1, 1);
            
            // Add tab control directly below the fixed header (no splitter needed for left side)
            tabControlLeftLower.Dock = DockStyle.Fill;
            
            leftPanel.Controls.Add(tabControlLeftLower);
            leftPanel.Controls.Add(fixedLayout);
            
            splitContainerMain.Panel1.Controls.Add(leftPanel);
            splitContainerMain.Panel2.Controls.Add(splitContainerRight);
            
            // Remove old leftUpperLayout references
            // leftUpperLayout is no longer needed
            // 
            // labels and combos
            // 
            lblSolution.Text = "Solution";
            lblSolution.Anchor = AnchorStyles.Left;
            lblSolution.AutoSize = true;
            lblSolution.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            cmbSolution.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbSolution.DropDownStyle = ComboBoxStyle.DropDownList;

            lblParentEntity.Text = "Parent Entity";
            lblParentEntity.Anchor = AnchorStyles.Left;
            lblParentEntity.AutoSize = true;
            lblParentEntity.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            cmbParentEntity.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbParentEntity.DropDownStyle = ComboBoxStyle.DropDownList;

            // 
            // tabControlLeftLower
            // 
            tabControlLeftLower.Dock = DockStyle.Fill;
            tabControlLeftLower.Controls.Add(tabLog);
            tabControlLeftLower.Controls.Add(tabJson);

            // 
            // tabLog
            // 
            tabLog.Text = "Log";
            tabLog.Padding = new Padding(4);
            tabLog.Controls.Add(txtLog);

            txtLog.Dock = DockStyle.Fill;
            txtLog.Multiline = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.ReadOnly = true;

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
            // splitContainerRight (Upper 70% / Lower 30%)
            // 
            splitContainerRight.Dock = DockStyle.Fill;
            splitContainerRight.Orientation = Orientation.Horizontal;
            splitContainerRight.SplitterDistance = 420;
            splitContainerRight.Panel1.Controls.Add(tabControlRightUpper);
            splitContainerRight.Panel2.Controls.Add(panelRightLower);

            // 
            // tabControlRightUpper
            // 
            tabControlRightUpper.Dock = DockStyle.Fill;
            tabControlRightUpper.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControlRightUpper.SizeMode = TabSizeMode.Fixed;
            tabControlRightUpper.ItemSize = new Size(180, 40);
            tabControlRightUpper.DrawItem += tabControlRightUpper_DrawItem;
            // Child entity tabs will be added dynamically

            // 
            // panelRightLower
            // 
            panelRightLower.Dock = DockStyle.Fill;
            panelRightLower.Padding = new Padding(8);
            panelRightLower.Controls.Add(filterControl);
            panelRightLower.Controls.Add(chkEnableTracing);

            // 
            // chkEnableTracing
            // 
            chkEnableTracing.Dock = DockStyle.Bottom;
            chkEnableTracing.Text = "Enable Detailed Tracing (disable in production for reduced log verbosity)";
            chkEnableTracing.Checked = true;
            chkEnableTracing.Height = 30;
            chkEnableTracing.Padding = new Padding(4);

            // 
            // filterControl
            // 
            filterControl.Dock = DockStyle.Fill;

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
            // CascadeFieldsConfiguratorControl
            // 
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(splitContainerMain);
            Controls.Add(ribbonPanel);
            Name = nameof(CascadeFieldsConfiguratorControl);
            Dock = DockStyle.Fill;
            ribbonPanel.ResumeLayout(false);
            ribbonPanel.PerformLayout();
            splitContainerMain.Panel1.ResumeLayout(false);
            splitContainerMain.Panel2.ResumeLayout(false);
            ((ISupportInitialize)splitContainerMain).EndInit();
            splitContainerMain.ResumeLayout(false);
            splitContainerLeft.Panel1.ResumeLayout(false);
            splitContainerLeft.Panel2.ResumeLayout(false);
            ((ISupportInitialize)splitContainerLeft).EndInit();
            splitContainerLeft.ResumeLayout(false);
            leftUpperLayout.ResumeLayout(false);
            leftUpperLayout.PerformLayout();
            tabControlLeftLower.ResumeLayout(false);
            tabLog.ResumeLayout(false);
            tabLog.PerformLayout();
            tabJson.ResumeLayout(false);
            tabJson.PerformLayout();
            splitContainerRight.Panel1.ResumeLayout(false);
            splitContainerRight.Panel2.ResumeLayout(false);
            ((ISupportInitialize)splitContainerRight).EndInit();
            splitContainerRight.ResumeLayout(false);
            tabControlRightUpper.ResumeLayout(false);
            panelRightLower.ResumeLayout(false);
            ((ISupportInitialize)gridMappings).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}