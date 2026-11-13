# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
The project follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- **Baselines: `--generate-baseline` (`-g`), automatic use of the newest baseline, and a configuration check.**
  `-g` writes the analysis to `.cca/baseline/baseline-<UTC yyyy-MM-dd_HH-mm-ss>[-N].json` and prints `Baseline file generated: <path>`.
  A file is never overwritten; when the name is taken, `-1`, `-2` and so on are appended.
  Without `-b`, `analyse` uses the newest file of `.cca/baseline` and prints `Auto-detected latest baseline file: <name>`.
  Nothing changes for a project that has no `.cca/baseline` directory.
  A generated baseline stores the SHA-256 of the metric settings as `configHash`.
  When it differs from the current settings, `analyse` warns that the comparison may not be accurate; baselines without a hash never warn.
  See [Baseline](docs/Baseline.md).

### Changed

- **Baselines match methods by their path relative to the working directory**, so a baseline also works in another checkout of the project.
  A baseline with absolute paths, such as every one written by `-r Json` so far, still works: when the exact key is not found, the method is matched by class and signature when that is unique among the entries that are left.
  A nested class that gained a containing type through the change below is matched to its old name when exactly one containing type was added and that is unambiguous.
- **A baseline file is validated when it is read.**
  Wrong types, negative numbers and numbers that are not finite are rejected, and all problems are reported together as `Invalid baseline file format in <path>: ...`.
  Only the fields that are read are checked, so unknown fields and older files with missing fields still load.
- **More declarations are analysed: the methods of structs, records and record structs, interface methods with a body, and constructors.**
  Before, only the methods of `class` declarations were analysed.
  This adds rows to every report, the summary counts and baselines; existing rows keep their names, signatures and metrics.
  A constructor is named after its type (`MyApp.Order::Order`), a static constructor too, with the signature `static Order()`.
  Property accessors, operators and finalizers are still not analysed, as in the Java port.
  See [Analysed methods](docs/Configuration.md#7-analysed-methods).
- A class nested in a struct, record or interface is named through that type (`MyApp.Shape.Builder`).
  Before, such a containing type was left out of the name (`MyApp.Builder`), so two nested classes of the same name could collide.
  A baseline written earlier still matches these methods under their old name, as long as that is unambiguous.

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

- **`--coverage-clover <file>` for `analyse` and `churn`.**
  The Clover reader takes the class figures from the statement and conditional metrics and the method figures from the `method` and `stmt` lines, and it understands the `signature` attribute of OpenClover.
  The option cannot be combined with `--coverage-cobertura` (`Only one coverage format can be specified at a time.`).
  `--coverage-cobertura` detects Cobertura and Visual Studio files as before.
- **The HTML and Markdown reports show `Line Coverage` and `Branch Coverage` columns after `Churn` when a coverage file was given.**
- **`analyse` warns when a coverage file matches only some of the analysed classes** (`Coverage report <file> matched M of N analysed classes.`).
- All coverage readers skip a `DOCTYPE` without loading DTDs or external entities and without expanding entities, so a report cannot read local files or run an entity bomb.
  A coverage file that is not well-formed is reported as `Invalid coverage file <file>: <reason>` and the command exits with a non-zero code.
- See [docs/Coverage.md](docs/Coverage.md).
- **`--sort-by <field>` and `--sort-order asc|desc`** order the rows of the console, CSV, HTML, Markdown, JSON, Checkstyle and JUnit reports by total score, class, method, file, line, Halstead volume, cyclomatic complexity or the count or score of any metric.
  Without `--sort-by` every report keeps its order.
- **`--debug`** prints an execution summary to stderr after the run: total time, time per phase and the counts of files and methods.
- **`--version`** prints the version of the tool.
- `@cca-ignore` in the XML doc comment or in any other comment of a type or member leaves it out of the analysis; a type leaves out its nested types too.
- The search path accepts a single `.cs` file and a comma-separated list of files and directories (`src,tools/Build.cs`).
  A path that exists as written stays one path even when it contains a comma, so existing invocations keep their meaning.
  The `churn` command accepts the same forms.
- `excludeFilePatterns` (regular expressions on the absolute path, with `/` separators) and `excludePatterns` (regular expressions on `Namespace.Class::Method`) are applied.
  Before, they were read but had no effect.
  An invalid pattern is ignored with a warning that names it, so an old configuration with such a pattern keeps working.
- A constructor is matched to the `.ctor` (or `.cctor`) entry of a coverage report.
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

- The `Checkstyle` report type (`-r Checkstyle`) writes Checkstyle XML with one `error` per method above `scoreThreshold`.
  The severity is `error` from twice the threshold on and `warning` otherwise.
- The `JUnit` report type (`-r JUnit`) writes JUnit XML with one test case per method.
  A method above `scoreThreshold` is a failed test case.
- `docs/CiReports.md` describes the CI formats, their paths and severities, and how to use them in GitHub Actions and GitLab CI.

### Fixed

- `--generate-config gen.json` now writes the file `gen.json` instead of creating a directory with that name.
  A path that ends in `.json` and is not an existing directory is the file to write; any other path is still the directory that receives `cognitive-metrics-settings.json`.
- Directories named `bin` or `obj` and directories whose name starts with a dot (`.git`, `.vs`) are no longer searched for source files.
  Before, generated files under `obj` (such as `AssemblyInfo.cs` and source generator output) were analysed and reported.
  A path passed on the command line is still searched even when it is such a directory.
- A symbolic link to a parent directory no longer makes the file search loop until the path gets too long, reporting the same files many times.
  Every real directory is now searched once.
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
