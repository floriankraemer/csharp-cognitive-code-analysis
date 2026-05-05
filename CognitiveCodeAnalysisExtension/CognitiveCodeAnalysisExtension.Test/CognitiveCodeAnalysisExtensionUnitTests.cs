using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Threading.Tasks;

using VerifyCS = CognitiveCodeAnalysisExtension.Test.CSharpAnalyzerVerifier<
    CognitiveCodeAnalysisExtension.CognitiveCodeAnalysisExtensionAnalyzer>;

namespace CognitiveCodeAnalysisExtension.Test
{
    [TestClass]
    public class CognitiveCodeAnalysisExtensionUnitTest
    {
        //No diagnostics expected for empty code
        [TestMethod]
        public async Task EmptyCode_NoDiagnostics()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // Threshold filtering (default scoreThreshold 0.5): need nesting deeper than analyzer defaults allow for a lone if —
        // mirrors ClassWithMethods Method2 scoring (~0.7 from nestingLevels).
        [TestMethod]
        public async Task SimpleMethod_ReportsCognitiveComplexity()
        {
            var test = @"
using System;

namespace TestNamespace
{
    public class {|#0:TestClass|}
    {
        public void {|#1:SimpleMethod|}()
        {
            for (int i = 0; i < 10; i++)
            {
                if (i > 5)
                {
                    Console.WriteLine(""Hello"");
                }
            }
        }
    }
}";

            var expectedClass = VerifyCS.Diagnostic("CognitiveComplexityClass").WithLocation(0).WithArguments("0,7");
            var expectedMethod = VerifyCS.Diagnostic("CognitiveComplexityMethod").WithLocation(1).WithArguments("0,7");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedClass, expectedMethod);
        }

        //Test class with cognitive complexity diagnostic
        [TestMethod]
        public async Task ClassWithMethods_ReportsClassCognitiveComplexity()
        {
            var test = @"
using System;

namespace TestNamespace
{
    public class {|#0:TestClass|}
    {
        public void {|#1:Method1|}()
        {
            if (true)
            {
                Console.WriteLine(""Hello"");
            }
        }

        public void {|#2:Method2|}()
        {
            for (int i = 0; i < 10; i++)
            {
                if (i > 5)
                {
                    Console.WriteLine(i);
                }
            }
        }
    }
}";

            var expectedClass = VerifyCS.Diagnostic("CognitiveComplexityClass").WithLocation(0).WithArguments("0,7");
            var expectedMethod2 = VerifyCS.Diagnostic("CognitiveComplexityMethod").WithLocation(2).WithArguments("0,7");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedClass, expectedMethod2);
        }

        /// <summary>Default showOnlyMethodsExceedingThreshold: scores at or below threshold do not surface as IDE warnings.</summary>
        [TestMethod]
        public async Task SimpleMethodWithoutComplexity_NoDiagnosticsUnderDefaultThreshold()
        {
            var test = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void SimpleMethod()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
