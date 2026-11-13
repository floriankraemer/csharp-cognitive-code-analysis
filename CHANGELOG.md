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

- **`--sort-by <field>` and `--sort-order asc|desc`** order the rows of the console, CSV, HTML, Markdown and JSON reports by total score, class, method, file, line, Halstead volume, cyclomatic complexity or the count or score of any metric.
  Without `--sort-by` every report keeps its order.
- **`--debug`** prints an execution summary to stderr after the run: total time, time per phase and the counts of files and methods.
- **`--version`** prints the version of the tool.
- The `init` command creates a `cognitive-metrics-settings.json` with the built-in defaults.
  In an interactive console it asks for `scoreThreshold` and for the `show*` settings and `groupByClass`, and an empty answer keeps the default.
  `--silent`, a console that is not interactive, or redirected standard input writes the defaults without asking.
  `-p|--path` sets the file to create (default `./cognitive-metrics-settings.json`), and missing parent directories are created.
  An existing file is not overwritten unless `-f|--force` is given, and the check happens before any question is asked.
  A file is written completely or not at all, and `--generate-config` is unchanged.
- `init` is now a command name, so `CognitiveCodeAnalysis init` no longer analyses a folder called `init`.
  Use `./init` to analyse such a folder.
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

### Fixed

- `--generate-config gen.json` now writes the file `gen.json` instead of creating a directory with that name.
  A path that ends in `.json` and is not an existing directory is the file to write; any other path is still the directory that receives `cognitive-metrics-settings.json`.
- The console report no longer prints a false `ConsoleText report generated: <path>` line.
- Console output no longer depends on the culture of the machine (decimal separator of the coverage, churn, summary and threshold numbers).
- The report type is matched case-insensitively (`-r html`).
- A Visual Studio coverage range with an unparsable `end_line` is kept as a single line instead of being dropped.
- `TypeRegistrar.RegisterLazy` checks the factory delegate for `null` instead of its name.
- The Cobertura reader no longer prefixes class names with the package name twice when the package is the assembly name, as coverlet writes it.
