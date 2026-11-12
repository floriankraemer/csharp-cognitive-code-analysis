/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Baseline;

public class BaselineLoaderMessagesTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    [TestCase("")]
    [TestCase("   ")]
    public void Load_BlankPath_Throws(string path)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => BaselineLoader.Load(path));

        Assert.That(ex!.Message, Is.EqualTo("Baseline file path is required."));
    }

    [Test]
    public void Load_MissingFile_ThrowsWithPathInMessageAndFileName()
    {
        var path = Path.Combine(_temp.tmpDirectory, "none.json");

        var ex = Assert.Throws<FileNotFoundException>(() => BaselineLoader.Load(path));

        Assert.That(ex!.Message, Is.EqualTo($"Baseline file not found: {path}"));
        Assert.That(ex.FileName, Is.EqualTo(path));
    }

    [Test]
    public void Load_JsonNull_ThrowsEmptyOrInvalid()
    {
        var path = _temp.CreateFileWithContent("null.json", "null");

        var ex = Assert.Throws<InvalidOperationException>(() => BaselineLoader.Load(path));

        Assert.That(ex!.Message, Is.EqualTo($"Baseline file '{path}' is empty or invalid JSON."));
    }

    [Test]
    public void Load_UnsupportedSchema_NamesBothVersions()
    {
        var path = _temp.CreateFileWithContent("v2.json", """{"schemaVersion": 2, "methods": []}""");

        var ex = Assert.Throws<InvalidOperationException>(() => BaselineLoader.Load(path));

        Assert.That(ex!.Message, Is.EqualTo("Unsupported baseline schema version 2. Expected version 1."));
    }

    [Test]
    public void Load_PropertyNamesAreCaseInsensitive()
    {
        var path = _temp.CreateFileWithContent("ci.json", """{"SCHEMAVERSION": 1, "Methods": [{"FILEPATH": "a.cs", "classname": "C", "methodSignature": "void M()", "totalscore": 2.5}]}""");

        var snapshot = BaselineLoader.Load(path);

        Assert.That(snapshot.Methods.Single().TotalScore, Is.EqualTo(2.5));
        Assert.That(snapshot.Methods.Single().FilePath, Is.EqualTo("a.cs"));
    }

    [Test]
    public void Serialize_ThenLoad_RoundTripsCamelCaseAndOmitsNulls()
    {
        var snapshot = new CognitiveBaselineSnapshot
        {
            Methods = [new CognitiveBaselineMethodSnapshot { FilePath = "a.cs", ClassName = "C", MethodName = "M", MethodSignature = "void M()", TotalScore = 1.5 }],
        };

        var json = BaselineLoader.Serialize(snapshot);
        var loaded = BaselineLoader.Load(_temp.CreateFileWithContent("rt.json", json));

        Assert.That(json, Does.Contain("\"schemaVersion\": 1"));
        Assert.That(json, Does.Contain("\"totalScore\": 1.5"));
        Assert.That(json, Does.Not.Contain("halsteadVolume"));
        Assert.That(loaded.Methods.Single().MethodName, Is.EqualTo("M"));
    }

    [Test]
    public void SnapshotDefaults_AreEmptyStringsAndEmptyLists()
    {
        var m = new CognitiveBaselineMethodSnapshot();
        var c = new CognitiveBaselineClassCouplingSnapshot();

        Assert.That(new[] { m.FilePath, m.ClassName, m.MethodName, m.MethodSignature, c.ClassName }, Is.All.Empty);
        Assert.That(new CognitiveBaselineSnapshot().Methods, Is.Empty);
        Assert.That(new CognitiveBaselineSnapshot().ClassCoupling, Is.Empty);
    }

    [Test]
    public void AutoDetectReader_ErrorMessages()
    {
        var reader = new AutoDetectCoverageReader();
        var missing = Path.Combine(_temp.tmpDirectory, "m.xml");

        var notFound = Assert.Throws<FileNotFoundException>(() => reader.ReadCoverage(missing));
        var unknown = Assert.Throws<InvalidOperationException>(() => reader.ReadCoverage(_temp.CreateFileWithContent("u.xml", "<x/>")));

        Assert.That(notFound!.Message, Is.EqualTo($"Coverage file not found: {missing}"));
        Assert.That(notFound.FileName, Is.EqualTo(missing));
        Assert.That(
            unknown!.Message,
            Is.EqualTo("Unrecognized coverage XML format (root element: 'x'). Expected Cobertura (<coverage>) or Visual Studio coverage (<results>)."));
    }

    [Test]
    public void AutoDetectReader_DtdInInput_IsIgnoredNotProcessed()
    {
        var path = _temp.CreateFileWithContent("dtd.xml", """<!DOCTYPE coverage [<!ENTITY x "y">]><coverage/>""");

        Assert.That(new AutoDetectCoverageReader().ReadCoverage(path), Is.Empty);
    }
}
