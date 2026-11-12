/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Reflection;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Application;

public class AssemblyVersionTests
{
    [Test]
    public void ConsoleApp_InformationalVersion_IsNonEmptyAndSemVerShaped()
    {
        var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.That(version.Split('+')[0], Does.Match(@"^\d+\.\d+\.\d+"));
    }

    [Test]
    public void ConsoleApp_AndLibrary_AreStampedWithTheSameVersion()
    {
        static string Of(Assembly a) => a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

        Assert.That(Of(typeof(Program).Assembly), Is.EqualTo(Of(typeof(CognitiveCodeAnalysis.CognitiveAnalysis.CognitiveMetrics).Assembly)));
    }
}
