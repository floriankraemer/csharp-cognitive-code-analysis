/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace CognitiveCodeAnalysis.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        string artifactsPath = ResolveArtifactsPath();
        Directory.CreateDirectory(artifactsPath);

        IConfig config = ManualConfig.Create(DefaultConfig.Instance)
            .WithArtifactsPath(artifactsPath);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }

    private static string ResolveArtifactsPath()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("CCA_BENCHMARK_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment);
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "benchmark"));
    }
}
