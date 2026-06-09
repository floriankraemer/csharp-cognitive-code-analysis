/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Collections.Immutable;
using System.Reflection;

using Microsoft.CodeAnalysis;

namespace CognitiveCodeAnalysis.Common;

internal static class RoslynMetadataReferences
{
    internal static ImmutableArray<MetadataReference> Get()
    {
        var references = new List<MetadataReference>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddAssembly(Assembly assembly)
        {
            if (!string.IsNullOrEmpty(assembly.Location) && seenPaths.Add(assembly.Location))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        AddAssembly(typeof(object).Assembly);
        AddAssembly(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly);
        AddAssembly(typeof(System.Linq.Enumerable).Assembly);

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedAssemblies)
        {
            foreach (string path in trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (seenPaths.Add(path))
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }

        return references.ToImmutableArray();
    }
}
