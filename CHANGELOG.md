# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
The project follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed

- **Nesting depth now accumulates through loops, `switch`, `try`, `lock`, `using`, `fixed`, `checked` and `unsafe` statements.**
  Before, only nested `if` statements added up.
  A loop, `switch` or `try` raised the maximum by one but passed its old depth on to its children, so a statement nested inside it was measured as if the loop were not there.
  `nestingLevels` and therefore `nestingScore` and `totalScore` can rise for existing code, and methods can newly appear in reports or cross `scoreThreshold`.
  Baselines taken before this change show the difference as a delta on the first comparison.
  Before and after, with the shipped defaults (`nestingLevels`: threshold 1, scale 1.0):

  ```csharp
  public void Process(List<Order> orders)
  {
      foreach (var order in orders)          // level 1
      {
          foreach (var line in order.Lines)  // level 2
          {
              while (line.HasWork())         // level 3
              {
                  line.Step();
              }
          }
      }
  }
  ```

  | | `nestingLevels` | `nestingScore` |
  | --- | ---: | ---: |
  | before | 1 | 0.000 |
  | after | 3 | 1.099 |

  `catch` and `finally` blocks share the level of their `try`, so `try { } catch { }` is still one level.
  `countElseAsNesting` and `countElseIfAsNesting` keep their meaning and now also apply to loops and other statements inside an `else`.
- Deconstructing `foreach` loops (`foreach (var (key, value) in map)`) count as loops and as nesting, and their variables as locals, like other `foreach` loops.
- `foreach` iteration variables and `catch` variables are counted as local variables (`localVariableCount`).
  This only changes scores when `localVariableCount` is enabled.
- The built-in defaults, the file written by `--generate-config` and the shipped `cognitive-metrics-settings.json` are now one and the same.
  `--generate-config` used to write `localVariableCount` and `propertyAccessCount` as enabled and omit several metrics.
  Configuration files you generated earlier keep working unchanged.
- The console report orders methods deterministically.
  The methods of an ungrouped report are listed by descending score.
- The score colors of the console and HTML reports follow `scoreThreshold`: green up to the threshold, yellow up to twice the threshold, red above.
  Before, they used the fixed limits 0.5 and 0.85.
- Method coverage is matched by method name and a first coverage line inside the method's source lines, not by the exact line of the declaration.
- Branch coverage is "no data" (`n/a`) for a method without branches, and the churn score then uses the line coverage.
  Before, such a method counted as 0% branch coverage, which made fully covered code look untested.
- Class coupling for types in the global namespace is keyed by the bare type name (`Alpha`), not `<global namespace>.Alpha`.
  A baseline written earlier that contains coupling for such a type reports it as new on the next comparison.

### Added

- The console report has the columns Else, Loops, Switch and Try/Catch, like the other reports.
- The CSV report has a `LinesOfCodeScore` column (and `LinesOfCodeScoreDelta` with a baseline), appended after all existing columns so that positional readers are not affected.
- Unknown options and surplus arguments on the command line print a warning that lists them.
  They are still ignored and the exit code is unchanged.
- Unknown keys in the `metrics` section of a configuration file print a warning (`Unknown metric 'x' in configuration is ignored. Valid metrics: ...`) and are ignored, so configurations that worked before keep working.
  Before, they were ignored silently, except a key that matched a non-numeric member of the metrics (for example `methodName`), which crashed the scoring with a `FormatException`.
- The Markdown report shows baseline deltas (▲ and ▼) like the HTML report, and its Method column is left-aligned.
- `showDetailedCognitiveMetrics` is bound and honoured: with `false`, the console, HTML and Markdown reports hide the detail columns.
- The Cobertura reader reads reports that carry rates only (`line-rate`, `branch-rate`), which is what coverlet writes, next to reports with counts.
- Coverlet's compiler-generated types are merged into their methods: async state machines (`Outer/<Method>d__N`), iterators, and the holders of lambdas, closures and local functions (`<>c__DisplayClass`).
  A method that has such data never falls back to class-level coverage.
- When the console report is not written to an interactive terminal (a pipe, a file, a CI log), it is rendered 400 columns wide instead of Spectre's 80-column default, so tables stay readable.
  The Method and Score columns never wrap.
- A warning is printed when a coverage file is read but no method matches.
- `variableCount` and `propertyCallCount` are accepted as deprecated aliases of `localVariableCount` and `propertyAccessCount` with a warning.
  The first settings file of this project contained them.
  A configuration that uses them now gets those thresholds applied: they never scored anything before, so scores can change.
  When a deprecated key and its current name are both configured, the current name wins and a warning says so (`Deprecated metric 'variableCount' ignored because 'localVariableCount' is also configured.`).

- The `Checkstyle` report type (`-r Checkstyle`) writes Checkstyle XML with one `error` per method above `scoreThreshold`.
  The severity is `error` from twice the threshold on and `warning` otherwise.
- The `JUnit` report type (`-r JUnit`) writes JUnit XML with one test case per method.
  A method above `scoreThreshold` is a failed test case.
- `docs/CiReports.md` describes the CI formats, their paths and severities, and how to use them in GitHub Actions and GitLab CI.

### Fixed

- The paths of the `Sarif`, `GitlabCodeQuality` and `GithubActions` reports, and of the new `Checkstyle` and `JUnit` reports, are relative to the working directory with `/` separators when the file is below it.
  Before, they were the paths as found, so the same method had a different location on every machine.
  Files outside the working directory keep their absolute path, which SARIF writes as a `file://` URI.
- SARIF relative locations are URI references against `%SRCROOT%`, which `originalUriBaseIds` declares as the working directory, and each path segment is percent-encoded.
- The GitLab Code Quality fingerprint hashes the relative path, so the fingerprints of all issues change once with this release.
  Until the default branch has the new fingerprints, the merge request widget shows each issue as removed and as new.
- The `GithubActions` report escapes `:` and `,` in the file name and `%`, carriage returns and line feeds in the title and the message, as the runner expects.
- Line numbers below 1 are reported as line 1 in every CI format, because a method cannot start before the first line.
- The console report no longer prints a false `ConsoleText report generated: <path>` line.
- Console output no longer depends on the culture of the machine (decimal separator of the coverage, churn, summary and threshold numbers).
- The report type is matched case-insensitively (`-r html`).
- A Visual Studio coverage range with an unparsable `end_line` is kept as a single line instead of being dropped.
- `TypeRegistrar.RegisterLazy` checks the factory delegate for `null` instead of its name.
- The Cobertura reader no longer prefixes class names with the package name twice when the package is the assembly name, as coverlet writes it.
