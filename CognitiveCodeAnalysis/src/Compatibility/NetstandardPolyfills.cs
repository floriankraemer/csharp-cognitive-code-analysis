/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

#if NETSTANDARD2_0
using System;

namespace System.Runtime.CompilerServices;

// C# 9 init-only setters
internal static class IsExternalInit { }

// C# 11 required members
[AttributeUsage(
    AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Field
    | AttributeTargets.Property
    | AttributeTargets.Constructor,
    Inherited = false,
    AllowMultiple = false
)]
internal sealed class RequiredMemberAttribute : Attribute { }

[AttributeUsage(
    AttributeTargets.All,
    Inherited = false,
    AllowMultiple = true
)]
internal sealed class CompilerFeatureRequiredAttribute : Attribute
{
    public CompilerFeatureRequiredAttribute(string featureName)
    {
        FeatureName = featureName;
    }

    public string FeatureName { get; }

    public bool IsOptional { get; set; }
}
#endif

