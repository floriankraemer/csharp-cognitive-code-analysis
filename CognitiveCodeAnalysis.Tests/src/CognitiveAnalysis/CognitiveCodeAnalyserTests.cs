/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class CognitiveCodeAnalyserTests
{
    private CognitiveCodeAnalyser _analyser;
    private CognitiveConfiguration _configuration;
    private TempFiles _tempFiles;

    [SetUp]
    public void SetUp()
    {
        _analyser = new CognitiveCodeAnalyser();
        _configuration = new CognitiveConfiguration();
        _tempFiles = new TempFiles();
    }

    [TearDown]
    public void TearDown()
    {
        _tempFiles.CleanUp();
    }

    [Test]
    public void AnalyseFiles_CountsLoopAndSwitchStatements()
    {
        const string content = @"
namespace X {
    public class Y {
        public void Run() {
            for (int i = 0; i < 1; i++) { }
            foreach (var item in new int[0]) { }
            while (false) { }
            do { } while (false);
            switch (1) {
                case 1: break;
            }
        }
    }
}";
        string file = _tempFiles.CreateFileWithContent("Loops.cs", content);

        var metrics = _analyser.AnalyseFiles([file], _configuration);

        Assert.That(metrics.Count, Is.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(metrics.First().loopCount, Is.EqualTo(4));
            Assert.That(metrics.First().switchCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void AnalyseFiles_CountsLocalVariables()
    {
        const string content = @"
namespace X {
    public class Y {
        public void Run(int arg) {
            int a = 1;
            var b = 2;
            string c = ""x"";
        }
    }
}";
        string file = _tempFiles.CreateFileWithContent("Locals.cs", content);

        var metrics = _analyser.AnalyseFiles([file], _configuration);

        Assert.That(metrics.First().localVariableCount, Is.EqualTo(3));
    }

    [Test]
    public void AnalyseFiles_CountsFieldAndPropertyAccesses()
    {
        const string content = @"
namespace X {
    public class Y {
        private int _field;
        public int Prop { get; set; }

        public void Run() {
            _field = 1;
            Prop = 2;
            int local = _field + Prop;
        }
    }
}";
        string file = _tempFiles.CreateFileWithContent("Members.cs", content);

        var metrics = _analyser.AnalyseFiles([file], _configuration);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metrics.First().fieldAccessCount, Is.EqualTo(2));
            Assert.That(metrics.First().propertyAccessCount, Is.EqualTo(2));
        }
    }
}
