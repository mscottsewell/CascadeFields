using System;
using System.IO;
using System.Reflection;

namespace CascadeFields.Configurator.Helpers
{
    /// <summary>
    /// Resolves the CascadeFields.Plugin assembly from the bundled Assets folder so the configurator can deserialize shared models.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// The configurator needs to reference types from CascadeFields.Plugin (such as CascadeConfiguration model)
    /// to deserialize existing configurations. However, the plugin DLL isn't in the standard probing paths.
    /// This resolver tells the runtime where to find the plugin assembly when it's requested.
    ///
    /// <para><b>Architecture:</b></para>
    /// XrmToolBox plugins are loaded into isolated directories. The CascadeFields.Plugin DLL is bundled in
    /// the Assets/DataversePlugin folder within the configurator plugin directory. When the runtime needs
    /// to load the plugin assembly (e.g., for JSON deserialization), this resolver provides the correct path.
    ///
    /// <para><b>Usage:</b></para>
    /// Call <see cref="Register"/> early in the plugin initialization (typically in the constructor or OnLoad)
    /// to ensure assembly resolution works before any deserialization occurs.
    ///
    /// <para><b>Example:</b></para>
    /// <code>
    /// public CascadeFieldsConfiguratorPlugin()
    /// {
    ///     AssemblyResolver.Register();  // Must be called before deserializing plugin models
    ///     InitializeComponent();
    /// }
    /// </code>
    /// </remarks>
    internal static class AssemblyResolver
    {
        /// <summary>
        /// Registers the assembly resolver with the current AppDomain.
        /// </summary>
        /// <remarks>
        /// <para><b>Idempotency:</b></para>
        /// This method can be called multiple times safely. It unregisters any existing handler
        /// before registering a new one to prevent duplicate event subscriptions.
        ///
        /// <para><b>Thread Safety:</b></para>
        /// AppDomain.AssemblyResolve is thread-safe, but callers should ensure this is called
        /// during plugin initialization before concurrent operations begin.
        ///
        /// <para><b>Best Practice:</b></para>
        /// Call this method as early as possible in the plugin lifecycle, ideally in the
        /// constructor or OnLoad method, before any code attempts to deserialize plugin models.
        /// </remarks>
        internal static void Register()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= OnResolve;
            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
        }

        /// <summary>
        /// Attempts to load CascadeFields.Plugin from the local Assets path when the runtime cannot locate it.
        /// </summary>
        /// <param name="sender">The source of the event (typically the AppDomain).</param>
        /// <param name="args">Event arguments containing the name of the assembly being resolved.</param>
        /// <returns>
        /// The loaded <see cref="Assembly"/> if CascadeFields.Plugin was requested and found in the Assets folder;
        /// otherwise, null to let other resolvers or the default probing logic handle the request.
        /// </returns>
        /// <remarks>
        /// <para><b>Resolution Logic:</b></para>
        /// <list type="number">
        ///     <item><description>Check if the requested assembly is "CascadeFields.Plugin" (case-insensitive)</description></item>
        ///     <item><description>If not, return null immediately (don't interfere with other assembly loads)</description></item>
        ///     <item><description>Construct path to bundled plugin DLL in Assets/DataversePlugin folder</description></item>
        ///     <item><description>If file exists, load and return it; otherwise return null</description></item>
        /// </list>
        ///
        /// <para><b>Expected Path:</b></para>
        /// The plugin DLL is expected at:
        /// <c>[PluginDirectory]/CascadeFieldsConfigurator/Assets/DataversePlugin/CascadeFields.Plugin.dll</c>
        ///
        /// <para><b>Error Handling:</b></para>
        /// Returns null if the assembly can't be found or loaded, allowing the runtime to continue
        /// searching or throw a FileNotFoundException if no resolver succeeds.
        /// </remarks>
        private static Assembly? OnResolve(object? sender, ResolveEventArgs args)
        {
            var requested = new AssemblyName(args.Name);
            if (!string.Equals(requested.Name, "CascadeFields.Plugin", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var baseDir = Path.GetDirectoryName(typeof(AssemblyResolver).Assembly.Location);
            if (string.IsNullOrEmpty(baseDir))
            {
                return null;
            }

            // Expect the plugin to be in <plugins>/CascadeFieldsConfigurator/Assets/DataversePlugin/CascadeFields.Plugin.dll
            var target = Path.Combine(baseDir, "CascadeFieldsConfigurator", "Assets", "DataversePlugin", "CascadeFields.Plugin.dll");
            return File.Exists(target) ? Assembly.LoadFrom(target) : null;
        }
    }
}
