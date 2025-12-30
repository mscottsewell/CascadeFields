using System;
using System.Collections.Specialized;
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
                settingsRepository);

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
            };

            // Button event handlers
            btnAddChildRelationship.Click += (s, e) =>
            {
                _viewModel.AddRelationshipCommand.Execute(null);
            };

            btnRetrieveConfigured.Click += async (s, e) =>
            {
                await RetrieveConfiguredEntityAsync();
            };

            btnRemoveRelationship.Click += (s, e) =>
            {
                if (_viewModel.SelectedTab != null)
                    _viewModel.RemoveRelationshipCommand.Execute(_viewModel.SelectedTab);
            };

            btnPublish.Click += (s, e) =>
            {
                _viewModel.PublishCommand.Execute(null);
            };

            // Checkbox handlers
            chkEnableTracing.CheckedChanged += (s, e) =>
            {
                _viewModel.EnableTracing = chkEnableTracing.Checked;
            };

            chkIsActive.CheckedChanged += (s, e) =>
            {
                _viewModel.IsActive = chkIsActive.Checked;
            };

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
        /// Called when connection is established
        /// </summary>
        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object? parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);
            if (newService != null)
            {
                EnsureInitialized();
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
