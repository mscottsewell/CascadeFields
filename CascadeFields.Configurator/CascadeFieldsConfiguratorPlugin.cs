using System;
using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;
using System.ComponentModel.Composition.Primitives;

namespace CascadeFields.Configurator
{
    [Export(typeof(IXrmToolBoxPlugin))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [ExportMetadata("Name", "CascadeFields Configurator")]
    [ExportMetadata("Description", "Configure and deploy the CascadeFields Dataverse plugin.")]
    [ExportMetadata("SmallImageBase64", TransparentPngBase64)]
    [ExportMetadata("BigImageBase64", TransparentPngBase64)]
    [ExportMetadata("BackgroundColor", "#0A2D4A")]
    [ExportMetadata("PrimaryFontColor", "#FFFFFF")]
    [ExportMetadata("SecondaryFontColor", "#D0E6F7")]
    public class CascadeFieldsConfiguratorPlugin : PluginBase, IHelpPlugin, IGitHubPlugin
    {
        private static readonly Guid PluginId = new Guid("f3e7c5e9-2f7a-4cde-9da0-8b8e5e6c33ad");
        private const string TransparentPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/xcAAn8B9UhtqL4AAAAASUVORK5CYII=";

        public override IXrmToolBoxPluginControl GetControl()
        {
            return new CascadeFieldsConfiguratorControl();
        }

        public override Guid GetId()
        {
            return PluginId;
        }

        public string HelpUrl => "https://github.com/mscottsewell/CascadeFields";

        public string RepositoryUrl => "https://github.com/mscottsewell/CascadeFields";

        public string UserName => "mscottsewell";

        public string RepositoryName => "CascadeFields";
    }
}
