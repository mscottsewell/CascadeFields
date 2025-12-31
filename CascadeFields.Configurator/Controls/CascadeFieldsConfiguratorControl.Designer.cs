using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using XrmToolBox.Extensibility;

namespace CascadeFields.Configurator.Controls
{
    partial class CascadeFieldsConfiguratorControl : PluginControlBase
    {
        private IContainer components = null!;
        private FlowLayoutPanel ribbonPanel;
        private Button btnAddChildRelationship = null!;
        private Button btnRemoveRelationship = null!;
        private Button btnRetrieveConfigured = null!;
        private Button btnExportJson = null!;
        private Button btnImportJson = null!;
        private Button btnPublish = null!;
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
        private CheckBox chkEnableTracing;
        private CheckBox chkIsActive;
        private Label lblStatus;

        private static Image LoadIcon(string fileName)
        {
            try
            {
                var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                var candidates = new[]
                {
                    Path.Combine(baseDir, "CascadeFieldsConfigurator", "Assets", "Icon", fileName),
                    Path.Combine(baseDir, "Assets", "Icon", fileName),
                };

                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        return Image.FromFile(path);
                    }
                }
            }
            catch
            {
                // ignore
            }

            return SystemIcons.Application.ToBitmap();
        }

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
            btnRetrieveConfigured = new Button();
            btnExportJson = new Button();
            btnImportJson = new Button();
            btnAddChildRelationship = new Button();
            btnRemoveRelationship = new Button();
            btnPublish = new Button();
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
            chkEnableTracing = new CheckBox();
            chkIsActive = new CheckBox();
            lblStatus = new Label();
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
                btnRetrieveConfigured,
                btnExportJson,
                btnImportJson,
                btnAddChildRelationship,
                btnRemoveRelationship,
                btnPublish
            });
            // 
            // btnAddChildRelationship
            // 
            btnAddChildRelationship.FlatStyle = FlatStyle.Flat;
            btnAddChildRelationship.Text = "Add Relationship";
            btnAddChildRelationship.AutoSize = true;
            btnAddChildRelationship.Margin = new Padding(4);
            btnAddChildRelationship.FlatAppearance.BorderSize = 0;
            btnAddChildRelationship.UseVisualStyleBackColor = true;
            btnAddChildRelationship.Image = LoadIcon("CascadeFields_AddRelationship_24.png");
            btnAddChildRelationship.TextImageRelation = TextImageRelation.ImageBeforeText;
            // 
            // btnRemoveRelationship
            // 
            btnRemoveRelationship.FlatStyle = FlatStyle.Flat;
            btnRemoveRelationship.Text = "Remove Relationship";
            btnRemoveRelationship.AutoSize = true;
            btnRemoveRelationship.Margin = new Padding(4);
            btnRemoveRelationship.FlatAppearance.BorderSize = 0;
            btnRemoveRelationship.UseVisualStyleBackColor = true;
            btnRemoveRelationship.Image = LoadIcon("CascadeFields_RemoveRelationship_24.png");
            btnRemoveRelationship.TextImageRelation = TextImageRelation.ImageBeforeText;
            // 
            // btnRetrieveConfigured
            // 
            btnRetrieveConfigured.FlatStyle = FlatStyle.Flat;
            btnRetrieveConfigured.Text = "Retrieve Configured Entity";
            btnRetrieveConfigured.AutoSize = true;
            btnRetrieveConfigured.Margin = new Padding(4);
            btnRetrieveConfigured.FlatAppearance.BorderSize = 0;
            btnRetrieveConfigured.UseVisualStyleBackColor = true;
            btnRetrieveConfigured.Image = LoadIcon("CascadeFields_LoadConfig_24.png");
            btnRetrieveConfigured.TextImageRelation = TextImageRelation.ImageBeforeText;

            // btnExportJson
            btnExportJson.FlatStyle = FlatStyle.Flat;
            btnExportJson.Text = "Export JSON";
            btnExportJson.AutoSize = true;
            btnExportJson.Margin = new Padding(4);
            btnExportJson.FlatAppearance.BorderSize = 0;
            btnExportJson.UseVisualStyleBackColor = true;
            btnExportJson.Image = LoadIcon("CascadeFields_SaveJSON_24.png");
            btnExportJson.TextImageRelation = TextImageRelation.ImageBeforeText;

            // btnImportJson
            btnImportJson.FlatStyle = FlatStyle.Flat;
            btnImportJson.Text = "Import JSON";
            btnImportJson.AutoSize = true;
            btnImportJson.Margin = new Padding(4);
            btnImportJson.FlatAppearance.BorderSize = 0;
            btnImportJson.UseVisualStyleBackColor = true;
            btnImportJson.Image = LoadIcon("CascadeFields_LoadJSON_24.png");
            btnImportJson.TextImageRelation = TextImageRelation.ImageBeforeText;
            // 
            // btnPublish
            // 
            btnPublish.FlatStyle = FlatStyle.Flat;
            btnPublish.Text = "Publish Configuration and Plug-in";
            btnPublish.AutoSize = true;
            btnPublish.Margin = new Padding(4);
            btnPublish.FlatAppearance.BorderSize = 0;
            btnPublish.UseVisualStyleBackColor = true;
            btnPublish.Image = LoadIcon("CascadeFields_PublishConfig_24.png");
            btnPublish.TextImageRelation = TextImageRelation.ImageBeforeText;            
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
            
            // Status label at bottom of left panel
            lblStatus.Dock = DockStyle.Bottom;
            lblStatus.Text = "Ready";
            lblStatus.AutoSize = false;
            lblStatus.Height = 25;
            lblStatus.Padding = new Padding(4);
            lblStatus.BorderStyle = BorderStyle.FixedSingle;
            
            leftPanel.Controls.Add(tabControlLeftLower);
            leftPanel.Controls.Add(lblStatus);
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
            splitContainerRight.SplitterWidth = 5;
            splitContainerRight.FixedPanel = FixedPanel.Panel2;
            splitContainerRight.Panel2MinSize = 40;
            splitContainerRight.Panel1.Controls.Add(tabControlRightUpper);
            splitContainerRight.Panel2.Controls.Add(panelRightLower);

            // 
            // tabControlRightUpper
            // 
            tabControlRightUpper.Dock = DockStyle.Fill;
            tabControlRightUpper.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControlRightUpper.SizeMode = TabSizeMode.Fixed;
                tabControlRightUpper.ItemSize = new Size(180, 75);
                tabControlRightUpper.Padding = new System.Drawing.Point(0, 0);
            tabControlRightUpper.Alignment = TabAlignment.Top;
            tabControlRightUpper.DrawItem += TabControlRightUpper_DrawItem;
            // Child entity tabs will be added dynamically

            // 
            // panelRightLower
            // 
            panelRightLower.Dock = DockStyle.Fill;
            panelRightLower.Padding = new Padding(8);
            
            // Create layout for checkboxes: EnableTracing on left, IsActive on right
            var checkboxLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Dock = DockStyle.Bottom,
                AutoSize = true,
                Height = 30
            };
            checkboxLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            checkboxLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            checkboxLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            checkboxLayout.Controls.Add(chkEnableTracing, 0, 0);
            checkboxLayout.Controls.Add(chkIsActive, 1, 0);
            
            panelRightLower.Controls.Add(checkboxLayout);

            // 
            // chkIsActive
            // 
            chkIsActive.Dock = DockStyle.Bottom;
            chkIsActive.Text = "Is Active";
            chkIsActive.Checked = true;
            chkIsActive.Height = 25;
            chkIsActive.Padding = new Padding(4);

            // 
            // chkEnableTracing
            // 
            chkEnableTracing.Dock = DockStyle.Bottom;
            chkEnableTracing.Text = "Enable Detailed Tracing (disable in production for reduced log verbosity)";
            chkEnableTracing.Checked = true;
            chkEnableTracing.Height = 30;
            chkEnableTracing.Padding = new Padding(4);

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
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}