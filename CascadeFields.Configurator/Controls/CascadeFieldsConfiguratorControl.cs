using System;
using System.Collections.Specialized;
using System.Linq;
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
        }

        /// <summary>
        /// Initializes the control with services and ViewModel
        /// </summary>
        public void Initialize()
        {
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

            // Initialize ViewModel with connection
            var connectionId = ConnectionDetail?.ConnectionId.ToString() ?? "default";
            _ = _viewModel.InitializeAsync(connectionId);

            // Wire up UI events
            WireUpBindings();
        }

        /// <summary>
        /// Wires up UI events and handlers
        /// </summary>
        private void WireUpBindings()
        {
            if (_viewModel == null)
                return;

            // Solutions combo box binding
            cmbSolution.DataSource = _viewModel.Solutions;
            cmbSolution.DisplayMember = "FriendlyName";
            cmbSolution.SelectedIndexChanged += (s, e) =>
            {
                _viewModel.SelectedSolution = cmbSolution.SelectedItem as SolutionItem;
            };

            // Parent entity combo box binding
            cmbParentEntity.DataSource = _viewModel.ParentEntities;
            cmbParentEntity.DisplayMember = "DisplayName";
            cmbParentEntity.SelectedIndexChanged += (s, e) =>
            {
                _viewModel.SelectedParentEntity = cmbParentEntity.SelectedItem as EntityItem;
            };

            // Relationship tabs binding
            _viewModel.RelationshipTabs.CollectionChanged += (s, e) =>
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
            };

            // Status message binding
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ConfigurationViewModel.StatusMessage))
                    lblStatus.Text = _viewModel.StatusMessage;
                if (e.PropertyName == nameof(ConfigurationViewModel.ConfigurationJson))
                    txtJsonPreview.Text = _viewModel.ConfigurationJson;
                if (e.PropertyName == nameof(ConfigurationViewModel.EnableTracing))
                    chkEnableTracing.Checked = _viewModel.EnableTracing;
                if (e.PropertyName == nameof(ConfigurationViewModel.IsActive))
                    chkIsActive.Checked = _viewModel.IsActive;
            };

            // Button event handlers
            btnLoadMetadata.Click += (s, e) =>
            {
                _viewModel.LoadSolutionsCommand.Execute(null);
            };

            btnAddChildRelationship.Click += (s, e) =>
            {
                _viewModel.AddRelationshipCommand.Execute(null);
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

            btnClearSession.Click += (s, e) =>
            {
                _viewModel.ClearSessionCommand.Execute(null);
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

        /// <summary>
        /// Adds a tab page for a relationship
        /// </summary>
        private void AddTabPage(RelationshipTabViewModel tabVm)
        {
            var tabPage = new TabPage(tabVm.TabName ?? "Relationship");
            tabPage.Tag = tabVm;

            var fieldMappingControl = new FieldMappingGridControl
            {
                Dock = DockStyle.Fill,
                DataSource = tabVm.FieldMappings
            };

            tabPage.Controls.Add(fieldMappingControl);
            tabControlRightUpper.TabPages.Add(tabPage);
            tabControlRightUpper.SelectedTab = tabPage;
        }

        /// <summary>
        /// Called when connection is established
        /// </summary>
        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object? parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);
            if (newService != null)
            {
                Initialize();
            }
        }
    }
}
