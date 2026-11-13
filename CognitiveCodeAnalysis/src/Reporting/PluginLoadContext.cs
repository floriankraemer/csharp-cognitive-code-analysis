/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Reflection;
using System.Runtime.Loader;

using CognitiveCodeAnalysis.Reporting.Spi;

namespace CognitiveCodeAnalysis.Reporting;

/// <summary>
/// Loads a plugin and its dependencies from the folder of the plugin. The SPI assembly always comes from the default
/// context, so the plugin's interface is the interface the tool knows, even when the plugin ships its own copy.
/// </summary>
internal sealed class PluginLoadContext(string pluginPath) : AssemblyLoadContext(Path.GetFileName(pluginPath))
{
    private static readonly string SpiAssemblyName = typeof(ICustomCognitiveReporter).Assembly.GetName().Name!;

    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name == SpiAssemblyName)
        {
            return null;
        }

        // Without a .deps.json the resolver still finds the dlls next to the plugin.
        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
