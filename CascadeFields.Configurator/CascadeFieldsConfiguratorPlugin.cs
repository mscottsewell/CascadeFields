using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;
using CascadeFields.Configurator.Controls;
using CascadeFields.Configurator.Helpers;

namespace CascadeFields.Configurator
{
    /// <summary>
    /// XrmToolBox plugin entry point for the CascadeFields Configurator.
    /// </summary>
    /// <remarks>
    /// <para><strong>Purpose:</strong></para>
    /// <para>
    /// This plugin allows administrators to configure cascade field operations for Dataverse entities.
    /// When a parent record's fields are updated, the plugin automatically copies those changes to
    /// related child records based on configured mappings and filters.
    /// </para>
    ///
    /// <para><strong>Key Features:</strong></para>
    /// <list type="bullet">
    /// <item><description>Visual configuration of parent-to-child field mappings</description></item>
    /// <item><description>Support for multiple child relationships per parent entity</description></item>
    /// <item><description>Filter criteria to limit which child records are updated</description></item>
    /// <item><description>Trigger field selection to control when cascade occurs</description></item>
    /// <item><description>JSON configuration preview and validation</description></item>
    /// <item><description>One-click deployment to Dataverse as plugin steps</description></item>
    /// <item><description>Automatic plugin assembly registration and updates</description></item>
    /// <item><description>Session persistence for quick configuration restoration</description></item>
    /// </list>
    ///
    /// <para><strong>Assembly Loading:</strong></para>
    /// <para>
    /// The static constructor registers a custom assembly resolver to ensure dependent DLLs
    /// (Newtonsoft.Json, etc.) load correctly within the XrmToolBox plugin isolation environment.
    /// See <see cref="AssemblyResolver"/> for implementation details.
    /// </para>
    ///
    /// <para><strong>XrmToolBox Integration:</strong></para>
    /// <para>
    /// This class uses MEF (Managed Extensibility Framework) attributes to register with XrmToolBox,
    /// providing metadata like name, description, and icons that appear in the plugin browser.
    /// </para>
    /// </remarks>
    [Export(typeof(IXrmToolBoxPlugin))]
    [ExportMetadata("Name", "CascadeFields Configurator")]
    [ExportMetadata("Description", "Configure and deploy the CascadeFields plugin. Copies selected fields from a parent record to their related child records.")]
    [ExportMetadata("SmallImageBase64", "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")]
    [ExportMetadata("BigImageBase64", "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")]
    [ExportMetadata("BackgroundColor", "#FFFFFF")]
    [ExportMetadata("PrimaryFontColor", "#3a0381")]
    [ExportMetadata("SecondaryFontColor", "#000000")]
    public class CascadeFieldsConfiguratorPlugin : PluginBase, IHelpPlugin, IAboutPlugin
    {
        /// <summary>
        /// Static constructor that registers the custom assembly resolver for dependent DLLs.
        /// </summary>
        static CascadeFieldsConfiguratorPlugin()
        {
            AssemblyResolver.Register();
        }

        /// <summary>
        /// Gets the help URL for the CascadeFields Configurator.
        /// </summary>
        public string HelpUrl => "https://github.com/mscottsewell/CascadeFields";

        /// <summary>
        /// Creates and returns the main configurator control instance.
        /// Implements comprehensive error logging on failure to aid in troubleshooting.
        /// </summary>
        /// <returns>The configurator control instance.</returns>
        /// <exception cref="Exception">Rethrows exceptions after logging for XrmToolBox to handle.</exception>
        public override IXrmToolBoxPluginControl GetControl()
        {
            try
            {
                return new CascadeFieldsConfiguratorControl();
            }
            catch (Exception ex)
            {
                var paths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XrmToolBox", "Logs", "CascadeFieldsConfigurator.log"),
                    Path.Combine(Path.GetDirectoryName(typeof(CascadeFieldsConfiguratorPlugin).Assembly.Location) ?? string.Empty, "CascadeFieldsConfigurator.log"),
                    Path.Combine(Path.GetTempPath(), "CascadeFieldsConfigurator.log")
                };

                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:O}] GetControl failure");
                sb.AppendLine(ex.ToString());

                foreach (var path in paths)
                {
                    if (string.IsNullOrWhiteSpace(path))
                        continue;
                    try
                    {
                        var dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrWhiteSpace(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        File.AppendAllText(path, sb.ToString());
                    }
                    catch
                    {
                        // ignore
                    }
                }

                MessageBox.Show(
                    $"CascadeFields Configurator failed to load. Last error: {ex.Message}\n\nSearched log locations:\n- %APPDATA%/XrmToolBox/Logs/CascadeFieldsConfigurator.log\n- Plugin folder next to DLL\n- %TEMP%/CascadeFieldsConfigurator.log",
                    "CascadeFields Configurator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                throw;
            }
        }

        /// <summary>
        /// Displays an About dialog with plugin information.
        /// </summary>
        public void ShowAboutDialog()
        {
            var message = "CascadeFields Configurator\nConfigure mappings, publish plugin steps, and update the CascadeFields plugin assembly.";
            MessageBox.Show(message, "CascadeFields Configurator", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
