using System.ComponentModel.Composition;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;
using CascadeFields.Configurator.Controls;

namespace CascadeFields.Configurator
{
    [Export(typeof(IXrmToolBoxPlugin))]
    [ExportMetadata("Name", "CascadeFields Configurator")]
    [ExportMetadata("Description", "Configure and deploy the CascadeFields Dataverse plugin.")]
    [ExportMetadata("SmallImageBase64", null)]
    [ExportMetadata("BigImageBase64", null)]
    [ExportMetadata("BackgroundColor", "#0078D4")]
    [ExportMetadata("PrimaryFontColor", "#FFFFFF")]
    [ExportMetadata("SecondaryFontColor", "#FFFFFF")]
    public class CascadeFieldsConfiguratorPlugin : PluginBase, IHelpPlugin, IAboutPlugin
    {
        public string HelpUrl => "https://github.com/mscottsewell/CascadeFields";

        public override IXrmToolBoxPluginControl GetControl()
        {
            return new CascadeFieldsConfiguratorControl();
        }

        public void ShowAboutDialog()
        {
            var message = "CascadeFields Configurator\nConfigure mappings, publish plugin steps, and update the CascadeFields plugin assembly.";
            MessageBox.Show(message, "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
