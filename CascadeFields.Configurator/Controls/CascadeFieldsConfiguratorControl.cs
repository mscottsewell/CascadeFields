using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CascadeFields.Configurator.Infrastructure.Commands;
using CascadeFields.Configurator.Models.UI;
using CascadeFields.Configurator.Services;
using CascadeFields.Configurator.ViewModels;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using XrmToolBox.Extensibility;

namespace CascadeFields.Configurator.Controls
{
    /// <summary>
    /// Main configuration control - MVVM based
    /// Wires ViewModel to UI without WPF binding (Windows Forms)
    /// </summary>
    public partial class CascadeFieldsConfiguratorControl : PluginControlBase
    {
        private ConfigurationViewModel? _viewModel;
        private string? _currentConnectionId;

        public CascadeFieldsConfiguratorControl()
        {
            InitializeComponent();
            splitContainerMain.SizeChanged += (s, e) => SetMainSplitterDistance();
            splitContainerRight.SizeChanged += (s, e) => SetRightSplitterDistance();
        }

        /// <summary>
        /// Initializes the control with services and ViewModel
        /// </summary>
        public void Initialize()
        {
            if (_viewModel != null)
                return;

            if (Service == null)
                return;

            // Create services
            var metadataService = new MetadataService(Service);
            var configurationService = new ConfigurationService(Service);
            var settingsRepository = new SettingsRepository();

            // Create ViewModel with services
            _viewModel = new ConfigurationViewModel(
                metadataService,
                configurationService,
                settingsRepository,
                AppendLog);

            // Initialize ViewModel with connection and restore session
            var connectionId = ConnectionDetail?.ConnectionId.ToString() ?? "default";
            var initTask = _viewModel.InitializeAsync(connectionId);

            // Wire up UI events
            WireUpBindings();

            // Wait for initialization and session restore to complete, then sync UI
            initTask.ContinueWith(_ =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke((MethodInvoker)(() => SyncUiFromViewModel()));
                }
                else
                {
                    SyncUiFromViewModel();
                }
            });
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            EnsureInitialized();
            SetMainSplitterDistance();
            SetRightSplitterDistance();
        }

        /// <summary>
        /// Syncs combo box selections and tabs to match ViewModel state
        /// </summary>
        private void SyncUiFromViewModel()
        {
            if (_viewModel == null)
                return;

            // Update solution combo if ViewModel has a selected solution
            if (_viewModel.SelectedSolution != null && cmbSolution.Items.Count > 0)
            {
                var solutionIndex = -1;
                for (int i = 0; i < cmbSolution.Items.Count; i++)
                {
                    if (cmbSolution.Items[i] is SolutionItem item && 
                        item.UniqueName == _viewModel.SelectedSolution.UniqueName)
                    {
                        solutionIndex = i;
                        break;
                    }
                }
                if (solutionIndex >= 0)
                {
                    cmbSolution.SelectedIndex = solutionIndex;
                }
            }

            // Update parent entity combo if ViewModel has a selected parent entity
            if (_viewModel.SelectedParentEntity != null && cmbParentEntity.Items.Count > 0)
            {
                var entityIndex = -1;
                for (int i = 0; i < cmbParentEntity.Items.Count; i++)
                {
                    if (cmbParentEntity.Items[i] is EntityItem item &&
                        item.LogicalName == _viewModel.SelectedParentEntity.LogicalName)
                    {
                        entityIndex = i;
                        break;
                    }
                }
                if (entityIndex >= 0)
                {
                    cmbParentEntity.SelectedIndex = entityIndex;
                }
            }
        }

        private void EnsureInitialized()
        {
            if (_viewModel == null)
            {
                Initialize();
            }
        }

        /// <summary>
        /// Handles connection change by clearing state and reinitializing
        /// </summary>
        private async void HandleConnectionChange(string newConnectionId)
        {
            _currentConnectionId = newConnectionId;

            // Clear log
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke((MethodInvoker)(() => txtLog.Clear()));
            }
            else
            {
                txtLog.Clear();
            }

            // Clear tabs immediately
            if (tabControlRightUpper.InvokeRequired)
            {
                tabControlRightUpper.Invoke((MethodInvoker)(() => tabControlRightUpper.TabPages.Clear()));
            }
            else
            {
                tabControlRightUpper.TabPages.Clear();
            }

            // Clear JSON preview
            if (txtJsonPreview.InvokeRequired)
            {
                txtJsonPreview.Invoke((MethodInvoker)(() => txtJsonPreview.Clear()));
            }
            else
            {
                txtJsonPreview.Clear();
            }

            // Get connection name for logging
            var connectionName = ConnectionDetail?.ConnectionName ?? "Unknown";
            AppendLog($"Connection changed to: {connectionName}");
            AppendLog("Loading metadata from new environment...");

            // Dispose old ViewModel and recreate with new services
            _viewModel = null;

            // Create new services with the updated Service property (set by base.UpdateConnection)
            var metadataService = new MetadataService(Service);
            var configurationService = new ConfigurationService(Service);
            var settingsRepository = new SettingsRepository();

            // Create new ViewModel
            _viewModel = new ConfigurationViewModel(
                metadataService,
                configurationService,
                settingsRepository,
                AppendLog);

            // Wire up UI events for new ViewModel
            WireUpBindings();

            // Initialize with new connection
            await _viewModel.InitializeAsync(newConnectionId);

            // Sync UI
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)(() => SyncUiFromViewModel()));
            }
            else
            {
                SyncUiFromViewModel();
            }
        }

        private void SetMainSplitterDistance()
        {
            if (splitContainerMain.Width <= 0)
                return;

            // Maintain roughly 40/60 split left/right
            var target = (int)(splitContainerMain.Width * 0.4);
            splitContainerMain.SplitterDistance = Math.Max(200, target);
        }

        private void SetRightSplitterDistance()
        {
            if (splitContainerRight.Height <= 0)
                return;

            // Keep bottom panel fixed at its minimum size (no extra padding)
            var bottom = splitContainerRight.Panel2MinSize;
            var distance = Math.Max(200, splitContainerRight.Height - bottom);
            splitContainerRight.SplitterDistance = distance;
        }

        /// <summary>
        /// Wires up UI events and handlers
        /// </summary>
        private void WireUpBindings()
        {
            if (_viewModel == null)
                return;

            void RefreshSolutions() => RebindCombo(cmbSolution, _viewModel.Solutions, nameof(SolutionItem.FriendlyName));
            void RefreshParentEntities() => RebindCombo(cmbParentEntity, _viewModel.ParentEntities, nameof(EntityItem.DisplayNameWithSchema));

            // Solutions combo box binding
            RefreshSolutions();
            _viewModel.Solutions.CollectionChanged += (s, e) => RefreshSolutions();
            cmbSolution.SelectedIndexChanged += (s, e) =>
            {
                _viewModel.SelectedSolution = cmbSolution.SelectedItem as SolutionItem;
            };

            // Parent entity combo box binding
            RefreshParentEntities();
            _viewModel.ParentEntities.CollectionChanged += (s, e) => RefreshParentEntities();
            cmbParentEntity.SelectedIndexChanged += (s, e) =>
            {
                _viewModel.SelectedParentEntity = cmbParentEntity.SelectedItem as EntityItem;
            };

            // Relationship tabs binding
            _viewModel.RelationshipTabs.CollectionChanged += (s, e) =>
            {
                void UpdateTabs()
                {
                    if (e.Action == NotifyCollectionChangedAction.Add)
                    {
                        foreach (var item in e.NewItems ?? new System.Collections.ArrayList())
                        {
                            if (item is RelationshipTabViewModel tabVm)
                                AddTabPage(tabVm);
                        }
                    }
                    else if (e.Action == NotifyCollectionChangedAction.Remove)
                    {
                        foreach (var item in e.OldItems ?? new System.Collections.ArrayList())
                        {
                            if (item is RelationshipTabViewModel tabVm)
                            {
                                var page = tabControlRightUpper.TabPages.Cast<TabPage>()
                                    .FirstOrDefault(p => p.Tag == tabVm);
                                if (page != null)
                                    tabControlRightUpper.TabPages.Remove(page);
                            }
                        }
                    }
                    else if (e.Action == NotifyCollectionChangedAction.Reset)
                    {
                        tabControlRightUpper.TabPages.Clear();
                    }
                }

                if (InvokeRequired)
                {
                    BeginInvoke((MethodInvoker)(() => UpdateTabs()));
                }
                else
                {
                    UpdateTabs();
                }
            };

            tabControlRightUpper.SelectedIndexChanged += (s, e) =>
            {
                if (tabControlRightUpper.SelectedTab?.Tag is RelationshipTabViewModel vm)
                {
                    _viewModel.SelectedTab = vm;
                }
            };

            // Status message binding
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ConfigurationViewModel.StatusMessage))
                {
                    lblStatus.Text = _viewModel.StatusMessage;
                    AppendLog(_viewModel.StatusMessage);
                }
                if (e.PropertyName == nameof(ConfigurationViewModel.ConfigurationJson))
                    txtJsonPreview.Text = _viewModel.ConfigurationJson;
                if (e.PropertyName == nameof(ConfigurationViewModel.EnableTracing))
                    chkEnableTracing.Checked = _viewModel.EnableTracing;
                if (e.PropertyName == nameof(ConfigurationViewModel.IsActive))
                    chkIsActive.Checked = _viewModel.IsActive;

                // Keep solution/parent entity combos in sync when selection changes in ViewModel
                if (e.PropertyName == nameof(ConfigurationViewModel.SelectedSolution) ||
                    e.PropertyName == nameof(ConfigurationViewModel.SelectedParentEntity))
                {
                    if (InvokeRequired)
                    {
                        BeginInvoke((MethodInvoker)(() => SyncUiFromViewModel()));
                    }
                    else
                    {
                        SyncUiFromViewModel();
                    }
                }
            };

            // Button event handlers (ensure single subscription)
            btnAddChildRelationship.Click -= BtnAddChildRelationship_Click;
            btnAddChildRelationship.Click += BtnAddChildRelationship_Click;

            btnExportJson.Click -= BtnExportJson_Click;
            btnExportJson.Click += BtnExportJson_Click;

            btnImportJson.Click -= BtnImportJson_Click;
            btnImportJson.Click += BtnImportJson_Click;

            btnRetrieveConfigured.Click -= BtnRetrieveConfigured_Click;
            btnRetrieveConfigured.Click += BtnRetrieveConfigured_Click;

            btnRemoveRelationship.Click -= BtnRemoveRelationship_Click;
            btnRemoveRelationship.Click += BtnRemoveRelationship_Click;

            btnPublish.Click -= BtnPublish_Click;
            btnPublish.Click += BtnPublish_Click;

            // Checkbox handlers (ensure single subscription)
            chkEnableTracing.CheckedChanged -= ChkEnableTracing_CheckedChanged;
            chkEnableTracing.CheckedChanged += ChkEnableTracing_CheckedChanged;

            chkIsActive.CheckedChanged -= ChkIsActive_CheckedChanged;
            chkIsActive.CheckedChanged += ChkIsActive_CheckedChanged;

            // Initialize UI
            lblStatus.Text = _viewModel.StatusMessage;
        }

        private static void RebindCombo(ComboBox combo, object data, string displayMember)
        {
            void Bind()
            {
                var current = combo.SelectedItem;
                combo.DataSource = null;
                combo.DisplayMember = displayMember;
                combo.DataSource = data;
                if (current != null)
                {
                    combo.SelectedItem = current;
                }
            }

            if (combo.IsHandleCreated && combo.InvokeRequired)
            {
                combo.BeginInvoke((MethodInvoker)(() => Bind()));
            }
            else
            {
                Bind();
            }
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            void Write() => txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

            if (txtLog.IsHandleCreated && txtLog.InvokeRequired)
            {
                txtLog.BeginInvoke((MethodInvoker)(() => Write()));
            }
            else
            {
                Write();
            }
        }

        private void ExportJson()
        {
            var vm = _viewModel;
            if (vm == null || string.IsNullOrWhiteSpace(vm.ConfigurationJson))
            {
                MessageBox.Show("No configuration to export.", "Export JSON", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var parentNameForFile = vm.SelectedParentEntity?.LogicalName;
            using var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = string.IsNullOrWhiteSpace(parentNameForFile)
                    ? "cascade-config.json"
                    : $"{parentNameForFile}-cascade-config.json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllText(dialog.FileName, vm.ConfigurationJson);
                AppendLog($"Configuration exported to {dialog.FileName}");
                MessageBox.Show("Configuration JSON exported.", "Export JSON", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async Task ImportJsonAsync()
        {
            var vm = _viewModel;
            if (vm == null)
                return;

            using var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    AppendLog($"Importing configuration from {dialog.FileName}");

                    var (isValid, errors, warnings, missingComponents) = await vm.ValidateConfigurationJsonAsync(json, vm.SelectedSolution?.UniqueName);

                    if (!isValid)
                    {
                        var message = "Import blocked. Please fix the following issues:\n- " + string.Join("\n- ", errors);
                        MessageBox.Show(message, "Import JSON", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        AppendLog(message);
                        return;
                    }

                    if (warnings.Any())
                    {
                        var warnText = "Warnings:\n- " + string.Join("\n- ", warnings);
                        MessageBox.Show(warnText + "\n\nYou can add the missing items to the current solution now.", "Import JSON", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        AppendLog(warnText);
                    }

                    // Offer to add missing components to the current solution
                    if (vm.SelectedSolution == null)
                    {
                        MessageBox.Show("Select a solution before importing.", "Import JSON", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    if (missingComponents.Any())
                    {
                        var detail = string.Join("\n- ", missingComponents.Select(c => c.description));
                        var prompt = $"The following items are not in solution '{vm.SelectedSolution.UniqueName}':\n- {detail}\n\nAdd them now?";
                        var result = MessageBox.Show(prompt, "Add to Solution", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (result == DialogResult.No)
                        {
                            AppendLog("Import cancelled by user (missing items not added).");
                            return;
                        }

                        var progress = new Progress<string>(msg => AppendLog(msg));
                        await vm.ConfigurationService.AddComponentsToSolutionAsync(vm.SelectedSolution!.Id, missingComponents, progress);
                    }

                    await vm.ApplyConfigurationAsync(json);
            txtJsonPreview.Text = vm.ConfigurationJson;
                    MessageBox.Show("Configuration JSON imported.", "Import JSON", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import JSON: {ex.Message}", "Import JSON", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    AppendLog($"Failed to import JSON: {ex}");
                }
            }
        }

        // Named handlers to avoid duplicate subscriptions after reconnects
        private void BtnAddChildRelationship_Click(object? sender, EventArgs e) => _viewModel?.AddRelationshipCommand.Execute(null);
        private void BtnRemoveRelationship_Click(object? sender, EventArgs e)
        {
            if (_viewModel?.SelectedTab != null)
                _viewModel.RemoveRelationshipCommand.Execute(_viewModel.SelectedTab);
        }
        private void BtnPublish_Click(object? sender, EventArgs e) => _viewModel?.PublishCommand.Execute(null);
        private void BtnExportJson_Click(object? sender, EventArgs e) => ExportJson();
        private async void BtnImportJson_Click(object? sender, EventArgs e) => await ImportJsonAsync();
        private async void BtnRetrieveConfigured_Click(object? sender, EventArgs e) => await RetrieveConfiguredEntityAsync();
        private void ChkEnableTracing_CheckedChanged(object? sender, EventArgs e)
        {
            if (_viewModel != null)
                _viewModel.EnableTracing = chkEnableTracing.Checked;
        }
        private void ChkIsActive_CheckedChanged(object? sender, EventArgs e)
        {
            if (_viewModel != null)
                _viewModel.IsActive = chkIsActive.Checked;
        }

        /// <summary>
        /// Adds a tab page for a relationship
        /// </summary>
        private void AddTabPage(RelationshipTabViewModel tabVm)
        {
            try
            {
                AppendLog($"Creating tab for {tabVm.ChildEntityLogicalName}...");
                var tabPage = new TabPage(tabVm.TabName ?? "Relationship");
                tabPage.Tag = tabVm;

                var splitContainer = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Horizontal,
                    SplitterDistance = 250
                };

                var fieldMappingControl = new FieldMappingGridControl
                {
                    Dock = DockStyle.Fill,
                    DataSource = tabVm.FieldMappings,
                    ParentAttributes = tabVm.ParentAttributes,
                    ChildAttributes = tabVm.ChildAttributes
                };

                var filterControl = new FilterCriteriaGridControl
                {
                    Dock = DockStyle.Fill,
                    DataSource = tabVm.FilterCriteria,
                    AvailableAttributes = tabVm.ChildAttributes
                };

                splitContainer.Panel1.Controls.Add(fieldMappingControl);
                splitContainer.Panel2.Controls.Add(filterControl);
                tabPage.Controls.Add(splitContainer);
                
                tabControlRightUpper.TabPages.Add(tabPage);
                tabControlRightUpper.SelectedIndex = tabControlRightUpper.TabPages.Count - 1;
                tabControlRightUpper.Refresh();
                tabControlRightUpper.Invalidate();
                AppendLog($"Tab created for {tabVm.ChildEntityLogicalName}. Total tabs: {tabControlRightUpper.TabPages.Count}");
            }
            catch (Exception ex)
            {
                AppendLog($"Error creating tab: {ex.Message}");
            }
        }

        private async Task RetrieveConfiguredEntityAsync()
        {
            if (_viewModel == null)
                return;

            try
            {
                _viewModel.StatusMessage = "Loading existing configurations...";
                var configurations = await _viewModel.ConfigurationService.GetExistingConfigurationsAsync();
                
                if (configurations.Count == 0)
                {
                    MessageBox.Show("No configured entities found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Show picker dialog
                var picker = new Dialogs.ConfigurationPickerDialog(configurations);
                if (picker.ShowDialog() == DialogResult.OK && picker.SelectedConfiguration != null)
                {
                    var config = picker.SelectedConfiguration;
                    if (!string.IsNullOrWhiteSpace(config.RawJson))
                    {
                        _viewModel.StatusMessage = "Loading configuration...";
                        await _viewModel.ApplyConfigurationAsync(config.RawJson);
                    }
                }
                else
                {
                    _viewModel.StatusMessage = "Configuration loading cancelled.";
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Error retrieving configurations: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Called when connection is established or changed
        /// </summary>
        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object? parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);
            if (newService != null)
            {
                var newConnectionId = detail?.ConnectionId.ToString() ?? "default";
                
                // Detect connection change
                if (_currentConnectionId != null && _currentConnectionId != newConnectionId)
                {
                    // Connection changed - clear state and reinitialize
                    HandleConnectionChange(newConnectionId);
                }
                else
                {
                    // First connection or same connection
                    _currentConnectionId = newConnectionId;
                    EnsureInitialized();
                }
            }
        }

        /// <summary>
        /// Custom drawing for tab headers: first line bold and larger, text vertically centered
        /// </summary>
        private void TabControlRightUpper_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= tabControlRightUpper.TabPages.Count)
                return;

            var tabPage = tabControlRightUpper.TabPages[e.Index];
            var tabText = tabPage.Text;
            var bounds = e.Bounds;

            // Fill background
            using (var brush = new System.Drawing.SolidBrush(e.BackColor))
            {
                e.Graphics.FillRectangle(brush, bounds);
            }

            // Split text into lines
            var lines = tabText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            // Calculate total height of all lines to center vertically
            float totalHeight = 0;
            using (var boldFont = new System.Drawing.Font(e.Font.FontFamily, e.Font.Size + 1, System.Drawing.FontStyle.Bold))
            {
                if (lines.Length > 0)
                {
                    totalHeight += e.Graphics.MeasureString(lines[0], boldFont).Height;
                }
                for (int i = 1; i < lines.Length; i++)
                {
                    totalHeight += e.Graphics.MeasureString(lines[i], e.Font).Height;
                }
            }

                // Center vertically with reduced top margin
                var verticalMargin = (bounds.Height - totalHeight) / 2;
                var yOffset = bounds.Top + Math.Max(2, verticalMargin);

            // Draw first line in bold and larger
            if (lines.Length > 0)
            {
                using (var boldFont = new System.Drawing.Font(e.Font.FontFamily, e.Font.Size + 1, System.Drawing.FontStyle.Bold))
                using (var textBrush = new System.Drawing.SolidBrush(e.ForeColor))
                {
                    e.Graphics.DrawString(lines[0], boldFont, textBrush, bounds.Left + 4, yOffset);
                    yOffset += e.Graphics.MeasureString(lines[0], boldFont).Height;
                }
            }

            // Draw remaining lines in normal font
            using (var textBrush = new System.Drawing.SolidBrush(e.ForeColor))
            {
                for (int i = 1; i < lines.Length; i++)
                {
                    e.Graphics.DrawString(lines[i], e.Font, textBrush, bounds.Left + 4, yOffset);
                    yOffset += e.Graphics.MeasureString(lines[i], e.Font).Height;
                }
            }

            // Draw focus rectangle if needed
            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
            {
                e.DrawFocusRectangle();
            }
        }
    }
}
