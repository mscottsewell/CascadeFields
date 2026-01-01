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
        private TabControl tabControlLeftLower;
        private TabPage tabConfiguration;
        private TabPage tabLog;
        private TabPage tabJson;
        private TabControl tabControlRightUpper;
        private Label lblSolution;
        private ComboBox cmbSolution;
        private Label lblParentEntity;
        private ComboBox cmbParentEntity;
        private TextBox txtLog;
        private TextBox txtJsonPreview;
        private CheckBox chkEnableTracing;
        private CheckBox chkIsActive;
        private CheckBox chkDeleteAsyncOperationIfSuccessful;
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
            tabControlLeftLower = new TabControl();
            tabConfiguration = new TabPage();
            tabLog = new TabPage();
            txtLog = new TextBox();
            tabJson = new TabPage();
            txtJsonPreview = new TextBox();
            tabControlRightUpper = new TabControl();
            lblSolution = new Label();
            cmbSolution = new ComboBox();
            lblParentEntity = new Label();
            cmbParentEntity = new ComboBox();
            chkIsActive = new CheckBox();
            chkDeleteAsyncOperationIfSuccessful = new CheckBox();
            chkEnableTracing = new CheckBox();
            lblStatus = new Label();
            ribbonPanel.SuspendLayout();
            ((ISupportInitialize)splitContainerMain).BeginInit();
            splitContainerMain.Panel1.SuspendLayout();
            splitContainerMain.Panel2.SuspendLayout();
            splitContainerMain.SuspendLayout();
            tabControlLeftLower.SuspendLayout();
            tabConfiguration.SuspendLayout();
            tabLog.SuspendLayout();
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

            // Left panel - tabs only
            var leftPanel = new Panel { Dock = DockStyle.Fill };

            // Status label at bottom of left panel
            lblStatus.Dock = DockStyle.Bottom;
            lblStatus.Text = "Ready";
            lblStatus.AutoSize = false;
            lblStatus.Height = 25;
            lblStatus.Padding = new Padding(4);
            lblStatus.BorderStyle = BorderStyle.FixedSingle;

            // Tab control fills remaining space
            tabControlLeftLower.Dock = DockStyle.Fill;

            leftPanel.Controls.Add(tabControlLeftLower);
            leftPanel.Controls.Add(lblStatus);

            splitContainerMain.Panel1.Controls.Add(leftPanel);
            splitContainerMain.Panel2.Controls.Add(tabControlRightUpper);

            //
            // tabControlLeftLower
            //
            tabControlLeftLower.Dock = DockStyle.Fill;
            tabControlLeftLower.Controls.Add(tabConfiguration);
            tabControlLeftLower.Controls.Add(tabLog);
            tabControlLeftLower.Controls.Add(tabJson);

            //
            // tabConfiguration
            //
            tabConfiguration.Text = "Configuration";
            tabConfiguration.Padding = new Padding(8);

            // Create layout for Configuration tab content
            var configLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(4)
            };
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            configLayout.RowCount = 5;  // Solution, Parent Entity, Active, Delete Async, Enable Tracing
            configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

            // Add controls to layout
            configLayout.Controls.Add(lblSolution, 0, 0);
            configLayout.Controls.Add(cmbSolution, 1, 0);
            configLayout.Controls.Add(lblParentEntity, 0, 1);
            configLayout.Controls.Add(cmbParentEntity, 1, 1);

            // Add checkboxes spanning both columns
            configLayout.Controls.Add(chkIsActive, 0, 2);
            configLayout.SetColumnSpan(chkIsActive, 2);
            configLayout.Controls.Add(chkDeleteAsyncOperationIfSuccessful, 0, 3);
            configLayout.SetColumnSpan(chkDeleteAsyncOperationIfSuccessful, 2);
            configLayout.Controls.Add(chkEnableTracing, 0, 4);
            configLayout.SetColumnSpan(chkEnableTracing, 2);

            tabConfiguration.Controls.Add(configLayout);

            //
            // lblSolution
            //
            lblSolution.Text = "Solution";
            lblSolution.Anchor = AnchorStyles.Left;
            lblSolution.AutoSize = true;
            lblSolution.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            //
            // cmbSolution
            //
            cmbSolution.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbSolution.DropDownStyle = ComboBoxStyle.DropDownList;

            //
            // lblParentEntity
            //
            lblParentEntity.Text = "Parent Entity";
            lblParentEntity.Anchor = AnchorStyles.Left;
            lblParentEntity.AutoSize = true;
            lblParentEntity.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            //
            // cmbParentEntity
            //
            cmbParentEntity.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbParentEntity.DropDownStyle = ComboBoxStyle.DropDownList;

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
            // chkIsActive
            //
            chkIsActive.Text = "Is Active";
            chkIsActive.Checked = true;
            chkIsActive.AutoSize = true;
            chkIsActive.Padding = new Padding(4);
            chkIsActive.Margin = new Padding(0, 5, 0, 0);

            //
            // chkDeleteAsyncOperationIfSuccessful
            //
            chkDeleteAsyncOperationIfSuccessful.Text = "Auto-delete Successful System Jobs (Parent step only)";
            chkDeleteAsyncOperationIfSuccessful.Checked = true;
            chkDeleteAsyncOperationIfSuccessful.AutoSize = true;
            chkDeleteAsyncOperationIfSuccessful.Padding = new Padding(4);
            chkDeleteAsyncOperationIfSuccessful.Margin = new Padding(0, 5, 0, 0);
            // Create tooltip for the checkbox
            var toolTip = new ToolTip();
            toolTip.SetToolTip(chkDeleteAsyncOperationIfSuccessful,
                "When enabled, successful async operations (parent update step) will be automatically deleted from System Jobs.\n" +
                "This helps prevent clutter in the System Jobs table.\n" +
                "Note: Only applies to the parent update step since child steps run synchronously.\n" +
                "Uncheck to keep successful jobs visible for monitoring.");

            //
            // chkEnableTracing
            //
            chkEnableTracing.Text = "Enable Detailed Tracing (disable in production for reduced log verbosity)";
            chkEnableTracing.Checked = false;
            chkEnableTracing.AutoSize = true;
            chkEnableTracing.Padding = new Padding(4);
            chkEnableTracing.Margin = new Padding(0, 5, 0, 0);

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
            tabControlLeftLower.ResumeLayout(false);
            tabConfiguration.ResumeLayout(false);
            tabLog.ResumeLayout(false);
            tabLog.PerformLayout();
            tabJson.ResumeLayout(false);
            tabJson.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}