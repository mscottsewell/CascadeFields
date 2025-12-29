using System;
using System.IO;
using System.Reflection;

namespace CascadeFields.Configurator.Helpers
{
    internal static class AssemblyResolver
    {
        internal static void Register()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= OnResolve;
            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
        }

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
