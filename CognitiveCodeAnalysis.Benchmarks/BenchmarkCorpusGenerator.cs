/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Benchmarks;

/// <summary>
/// Generates a deterministic synthetic C# corpus for repeatable benchmark runs.
/// Each file contains one class with three methods and one interface for coupling edges.
/// </summary>
internal static class BenchmarkCorpusGenerator
{
    internal static string Generate(int fileCount)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"cca-benchmark-{fileCount}-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(directory);

        for (int index = 1; index <= fileCount; index++)
        {
            string path = Path.Combine(directory, $"Widget{index}.cs");
            File.WriteAllText(path, BuildSourceFile(index));
        }

        return directory;
    }

    internal static List<string> ListSourceFiles(string directory)
    {
        return Directory
            .GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildSourceFile(int index)
    {
        return $$"""
            namespace Bench.N{{index}} {
                public class Widget{{index}} {
                    private int _state;

                    public int Compute(int x, int y) {
                        int result = 0;
                        for (int i = 0; i < x; i++) {
                            if (i % 2 == 0 && y > 0) {
                                result += i;
                            } else {
                                result -= i;
                            }

                            switch (i % 3) {
                                case 0: result++; break;
                                case 1: result--; break;
                                default: break;
                            }
                        }

                        try {
                            result = result / (x - x + 1);
                        } catch (System.Exception) {
                            result = -1;
                        }

                        return result > 0 ? result : _state;
                    }

                    public string Name() => "Widget{{index}}";

                    public void Touch() {
                        _state++;
                    }
                }

                public interface IThing{{index}} {
                    int Compute(int x, int y);
                }
            }
            """;
    }
}
