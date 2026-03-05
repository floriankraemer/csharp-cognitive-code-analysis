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

        //Test method with cognitive complexity diagnostic
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
            if (true)
            {
                Console.WriteLine(""Hello"");
            }
        }
    }
}";

            var expectedClass = VerifyCS.Diagnostic("CognitiveComplexityClass").WithLocation(0).WithArguments("0,0");
            var expectedMethod = VerifyCS.Diagnostic("CognitiveComplexityMethod").WithLocation(1).WithArguments("0,0");
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
            var expectedMethod1 = VerifyCS.Diagnostic("CognitiveComplexityMethod").WithLocation(1).WithArguments("0,0");
            var expectedMethod2 = VerifyCS.Diagnostic("CognitiveComplexityMethod").WithLocation(2).WithArguments("0,7");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedClass, expectedMethod1, expectedMethod2);
        }

        //Test method with no complexity (should still report score)
        [TestMethod]
        public async Task SimpleMethodWithoutComplexity_ReportsZeroScore()
        {
            var test = @"
using System;

namespace TestNamespace
{
    public class {|#0:TestClass|}
    {
        public void {|#1:SimpleMethod|}()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";

            var expectedClass = VerifyCS.Diagnostic("CognitiveComplexityClass").WithLocation(0).WithArguments("0,0");
            var expectedMethod = VerifyCS.Diagnostic("CognitiveComplexityMethod").WithLocation(1).WithArguments("0,0");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedClass, expectedMethod);
        }
    }
}
