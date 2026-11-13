# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
The project follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- **`churn` command: complex code that changes often.**
  It multiplies the cognitive score of each class with the number of git commits that changed its file, and lists the classes by churn.
  `--since <yyyy-MM-dd>` sets the start day, `-r` writes a `Json`, `Csv`, `Html`, `Markdown` or `SvgTreemap` report to `-o`, and `--coverage-cobertura` or `--coverage-clover` add risk churn, coverage and a risk level.
  `CognitiveCodeAnalysis <path>` without a command still analyses.
  The `Churn` column of the analysis reports keeps its name and still means the score weighted with the missing coverage.
  See [Churn](docs/Churn.md).
- **`init` command** creates a `cognitive-metrics-settings.json` with the built-in defaults.
  In an interactive console it asks for `scoreThreshold` and for the `show*` settings and `groupByClass`, and an empty answer keeps the default.
  `--silent`, a console that is not interactive, or redirected standard input writes the defaults without asking.
  `-p|--path` sets the file to create (default `./cognitive-metrics-settings.json`), and missing parent directories are created.
  An existing file is not overwritten unless `-f|--force` is given, and the check happens before any question is asked.
  A file is written completely or not at all, and `--generate-config` is unchanged.
  `init` is now a command name, so `CognitiveCodeAnalysis init` no longer analyses a folder called `init`; use `./init` for such a folder.
- **Baselines: `--generate-baseline` (`-g`), automatic use of the newest baseline, and a configuration check.**
  `-g` writes the analysis to `.cca/baseline/baseline-<UTC yyyy-MM-dd_HH-mm-ss>[-N].json` and prints `Baseline file generated: <path>`.
  A file is never overwritten; when the name is taken, `-1`, `-2` and so on are appended.
  Without `-b`, `analyse` uses the newest file of `.cca/baseline` and prints `Auto-detected latest baseline file: <name>`.
  Nothing changes for a project that has no `.cca/baseline` directory.
  A generated baseline stores the SHA-256 of the metric settings as `configHash`.
  When it differs from the current settings, `analyse` warns that the comparison may not be accurate; baselines without a hash never warn.
  See [Baseline](docs/Baseline.md).
- **Custom reporters.**
  A class in your own assembly can render the `analyse` or `churn` result in any format.
  Register it under `cognitive.customReporters` in the configuration file and select it with `-r <key>`.
  The plugin interface (`ICustomCognitiveReporter`, `ICustomChurnReporter` and their immutable input records) is in the namespace `CognitiveCodeAnalysis.Reporting.Spi`.
  Built-in report types win over custom keys, and a custom key with the name of a built-in type is an error when that type is requested.
  An unknown `-r` value is reported with the same message and in the same order as before; when custom reporters are configured, the message also lists their keys.
  See [Custom reporters](docs/CustomReporters.md).
- **Understandability: the cognitive complexity as specified by SonarSource (G. Ann Campbell's white paper), measured for every method and constructor.**
  It counts structural increments with a nesting penalty (`if`, `?:`, `switch`, loops, `catch`), `else` and `else if`, sequences of `&&` and `||`, `goto` and recursion, with the C# choices of SonarC#.
  It does not enter the score.
  `showUnderstandability` (default `false`) or `--show-understandability` appends an `Understandability` column such as `7 (medium)` to the console, HTML and Markdown reports and the columns `Understandability` and `UnderstandabilityRiskLevel` to the end of the CSV report.
  The JSON report and baseline files always carry the fields `understandability` and `understandabilityRiskLevel`; older baseline files without them still load.
  See [Understandability](docs/Understandability.md).
- **Analysis cache** (off by default).
  `cache.enabled` and `cache.directory` in the configuration file, and `--cache` / `--no-cache`, store the metrics of each file so a repeated run on an unchanged tree skips the analysis.
  Because the field and property counts depend on all files, any change to any file invalidates every entry.
  See [Analysis cache](docs/Configuration.md#8-analysis-cache).
- **Clover coverage: `--coverage-clover <file>` for `analyse` and `churn`.**
  The reader takes the class figures from the statement and conditional metrics and the method figures from the `method` and `stmt` lines, and it understands the `signature` attribute of OpenClover.
  The option cannot be combined with `--coverage-cobertura` (`Only one coverage format can be specified at a time.`).
  `--coverage-cobertura` detects Cobertura and Visual Studio files as before.
  See [Coverage](docs/Coverage.md).
- **Coverage in the reports.**
  The HTML and Markdown reports show `Line Coverage` and `Branch Coverage` columns after `Churn` when a coverage file was given.
  `analyse` warns when a coverage file matches no method (`Loaded N coverage entries from <file> but none matched an analysed method.`) or only some of the analysed classes (`Coverage report <file> matched M of N analysed classes.`).
  The Cobertura reader also reads reports that carry rates only (`line-rate`, `branch-rate`), which is what coverlet writes, and a constructor is matched to the `.ctor` (or `.cctor`) entry of a report.
- **`Checkstyle` and `JUnit` report types.**
  `-r Checkstyle` writes Checkstyle XML with one `error` per method above `scoreThreshold`; the severity is `error` from twice the threshold on and `warning` otherwise.
  `-r JUnit` writes JUnit XML with one test case per method, and a method above `scoreThreshold` is a failed test case.
  See [CI reports](docs/CiReports.md).
- **`--sort-by <field>` and `--sort-order asc|desc`** order the rows of the console, CSV, HTML, Markdown, JSON, Checkstyle and JUnit reports by total score, class, method, file, line, Halstead volume, cyclomatic complexity, understandability or the count or score of any metric.
  Without `--sort-by` every report keeps its order.
- **`--debug`** prints an execution summary to stderr after the run: total time, time per phase and the counts of files and methods.
- **`--version`** prints the version of the tool.
- **`@cca-ignore`** in the XML doc comment or in any other comment of a type or member leaves it out of the analysis; a type leaves out its nested types too.
- **More ways to name the sources.**
  The search path accepts a single `.cs` file and a comma-separated list of files and directories (`src,tools/Build.cs`), and so does `churn`.
  A path that exists as written stays one path even when it contains a comma, so existing invocations keep their meaning.
- **Warnings for configuration mistakes.**
  Unknown options and surplus arguments on the command line print a warning that lists them, and are still ignored.
  Unknown keys in the `metrics` section of a configuration file print a warning (`Unknown metric 'x' in configuration is ignored. Valid metrics: ...`) and are ignored.
  An invalid regular expression in `excludeFilePatterns` or `excludePatterns` is ignored with a warning that names it.
- The console report has the columns Else, Loops, Switch and Try/Catch, like the other reports.
- The CSV report has a `LinesOfCodeScore` column (and `LinesOfCodeScoreDelta` with a baseline), appended after all existing columns so that positional readers are not affected.
- The Markdown report shows baseline deltas (▲ and ▼) like the HTML report, and its Method column is left-aligned.
- `docs/CI.md` describes pipelines for GitHub Actions and GitLab CI, with baselines and caching, and the README documents installation, every command and the differences to the PHP version.

### Changed

- **More declarations are analysed: the methods of structs, records and record structs, interface methods with a body, and constructors.**
  Before, only the methods of `class` declarations were analysed.
  This adds rows to every report, the summary counts and baselines; existing rows keep their names, signatures and metrics.
  A constructor is named after its type (`MyApp.Order::Order`), a static constructor too, with the signature `static Order()`.
  Property accessors, operators and finalizers are still not analysed, as in the Java port.
  A class nested in a struct, record or interface is named through that type (`MyApp.Shape.Builder`); before, such a containing type was left out of the name (`MyApp.Builder`), so two nested classes of the same name could collide.
  See [Analysed methods](docs/Configuration.md#7-analysed-methods).
- **Nesting depth accumulates through loops, `switch`, `try`, `lock`, `using`, `fixed`, `checked` and `unsafe` statements.**
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
- **Baselines match methods by their path relative to the working directory**, so a baseline also works in another checkout of the project.
  A baseline with absolute paths, such as every one written by `-r Json` so far, still works: when the exact key is not found, the method is matched by class and signature when that is unique among the entries that are left.
  A nested class that gained a containing type through the naming change above is matched to its old name when exactly one containing type was added and that is unambiguous.
- **A baseline file is validated when it is read.**
  Wrong types, negative numbers and numbers that are not finite are rejected, and all problems are reported together as `Invalid baseline file format in <path>: ...`.
  Only the fields that are read are checked, so unknown fields and older files with missing fields still load.
- **Coverage is matched more precisely.**
  A method is matched by its name and a first coverage line inside the method's source lines, not by the exact line of the declaration.
  Branch coverage is "no data" (`n/a`) for a method without branches, and the churn score then uses the line coverage; before, such a method counted as 0% branch coverage, which made fully covered code look untested.
  Coverlet's compiler-generated types are merged into their methods: async state machines (`Outer/<Method>d__N`), iterators, and the holders of lambdas, closures and local functions (`<>c__DisplayClass`).
  A method that has such data never falls back to class-level coverage.
- **Coverage files are parsed safely.**
  All readers skip a `DOCTYPE` without loading DTDs or external entities and without expanding entities, so a report cannot read local files or run an entity bomb.
  A coverage file that is not well-formed is reported as `Invalid coverage file <file>: <reason>` and the command exits with a non-zero code.
- **The paths of the CI reports are relative to the working directory.**
  The `Sarif`, `GitlabCodeQuality`, `GithubActions`, `Checkstyle` and `JUnit` reports use paths with `/` separators when the file is below the working directory.
  Before, they were the paths as found, so the same method had a different location on every machine.
  Files outside the working directory keep their absolute path, which SARIF writes as a `file://` URI.
  The GitLab Code Quality fingerprint hashes the relative path, so the fingerprints of all issues change once with this release.
  Until the default branch has the new fingerprints, the merge request widget shows each issue as removed and as new.
- The built-in defaults, the file written by `--generate-config` and the shipped `cognitive-metrics-settings.json` are now one and the same.
  `--generate-config` used to write `localVariableCount` and `propertyAccessCount` as enabled and omit several metrics.
  Configuration files you generated earlier keep working unchanged.
- `variableCount` and `propertyCallCount` are accepted as deprecated aliases of `localVariableCount` and `propertyAccessCount` with a warning.
  The first settings file of this project contained them.
  A configuration that uses them now gets those thresholds applied: they never scored anything before, so scores can change.
  When a deprecated key and its current name are both configured, the current name wins and a warning says so (`Deprecated metric 'variableCount' ignored because 'localVariableCount' is also configured.`).
- `foreach` iteration variables and `catch` variables are counted as local variables (`localVariableCount`), and deconstructing `foreach` loops (`foreach (var (key, value) in map)`) count as loops and as nesting like other `foreach` loops.
  This only changes scores when `localVariableCount` is enabled.
- `showDetailedCognitiveMetrics` is bound and honoured: with `false`, the console, HTML and Markdown reports hide the detail columns.
- The console report orders methods deterministically.
  The methods of an ungrouped report are listed by descending score.
- The score colors of the console and HTML reports follow `scoreThreshold`: green up to the threshold, yellow up to twice the threshold, red above.
  Before, they used the fixed limits 0.5 and 0.85.
- When the console report is not written to an interactive terminal (a pipe, a file, a CI log), it is rendered 400 columns wide instead of Spectre's 80-column default, so tables stay readable.
  The Method and Score columns never wrap.
- Class coupling for types in the global namespace is keyed by the bare type name (`Alpha`), not `<global namespace>.Alpha`.
  A baseline written earlier that contains coupling for such a type reports it as new on the next comparison.
- The version of the tool is taken from the release tag, and the build treats compiler warnings as errors.

### Fixed

- `excludeFilePatterns` (regular expressions on the absolute path, with `/` separators) and `excludePatterns` (regular expressions on `Namespace.Class::Method`) are applied.
  Before, they were read but had no effect.
  An invalid pattern is ignored with a warning that names it, so an old configuration with such a pattern keeps working.
- Unknown keys in the `metrics` section no longer crash the scoring when they match a non-numeric member of the metrics (for example `methodName`); they are ignored with the warning described above.
- `--generate-config gen.json` now writes the file `gen.json` instead of creating a directory with that name.
  A path that ends in `.json` and is not an existing directory is the file to write; any other path is still the directory that receives `cognitive-metrics-settings.json`.
- Directories named `bin` or `obj` and directories whose name starts with a dot (`.git`, `.vs`) are no longer searched for source files.
  Before, generated files under `obj` (such as `AssemblyInfo.cs` and source generator output) were analysed and reported.
  A path passed on the command line is still searched even when it is such a directory.
- A symbolic link to a parent directory no longer makes the file search loop until the path gets too long, reporting the same files many times.
  Every real directory is now searched once.
- SARIF relative locations are URI references against `%SRCROOT%`, which `originalUriBaseIds` declares as the working directory, and each path segment is percent-encoded.
- The `GithubActions` report escapes `:` and `,` in the file name and `%`, carriage returns and line feeds in the title and the message, as the runner expects.
- Line numbers below 1 are reported as line 1 in every CI format, because a method cannot start before the first line.
- The console report no longer prints a false `ConsoleText report generated: <path>` line.
- Console output no longer depends on the culture of the machine (decimal separator of the coverage, churn, summary and threshold numbers).
- The report type is matched case-insensitively (`-r html`).
- A Visual Studio coverage range with an unparsable `end_line` is kept as a single line instead of being dropped.
- The Cobertura reader no longer prefixes class names with the package name twice when the package is the assembly name, as coverlet writes it.
- `TypeRegistrar.RegisterLazy` checks the factory delegate for `null` instead of its name.
