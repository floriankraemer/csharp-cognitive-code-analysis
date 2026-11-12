/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Reflection;
using System.Text.Json;

using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class SarifToolVersionTests
{
    [Test]
    public void ToolDriverVersion_IsTheStampedBuildVersionWithoutSourceRevisionMetadata()
    {
        var informational = typeof(SarifReport).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var path = Path.Combine(Path.GetTempPath(), "sarif-version-" + Guid.NewGuid() + ".json");

        try
        {
            new SarifReport().RenderMetrics(path, ReportGoldenFixtures.StandardCollection(), ReportGoldenFixtures.StandardConfig());
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var version = doc.RootElement.GetProperty("runs")[0].GetProperty("tool").GetProperty("driver").GetProperty("version").GetString();

            Assert.That(version, Is.Not.Null.And.Not.Empty);
            Assert.That(version, Is.EqualTo(informational.Split('+')[0]));
            Assert.That(version, Does.Not.Contain("+"));
            Assert.That(version, Is.Not.EqualTo("0.0.0"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Library_InformationalVersion_IsSemVerShaped()
    {
        var library = typeof(SarifReport).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.That(library, Is.Not.Empty);
        Assert.That(library.Split('+')[0], Does.Match(@"^\d+\.\d+\.\d+"));
    }
}
