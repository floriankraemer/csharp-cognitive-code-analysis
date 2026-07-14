/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysisConsoleApp.Infrastructure;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Infrastructure;

public class TimestampedConsoleTracerTests
{
    [Test]
    public void Trace_WritesTimestampedMessageToStandardError()
    {
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            new TimestampedConsoleTracer().Trace("hello");

            string output = writer.ToString();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(output, Does.Contain("hello"));
                Assert.That(output, Does.Match(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}"));
            }
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Test]
    public void TraceStep_WritesStartAndElapsedMilliseconds()
    {
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            new TimestampedConsoleTracer().TraceStep("Compile", () => Thread.Sleep(5));

            string output = writer.ToString();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(output, Does.Contain("Compile..."));
                Assert.That(output, Does.Contain("Compile done"));
                Assert.That(output, Does.Contain("ms"));
            }
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
