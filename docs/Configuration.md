# Configuration Guide

This project reads analysis settings from a JSON file with a `cognitive` root section. For a short overview and links to complexity references, see the [README](../README.md).

## 1) Configuration file location

By default, the app loads:

- `cognitive-metrics-settings.json`
- from the application base directory (`AppContext.BaseDirectory`)

If you use the library directly, you can load another file path with `ConfigurationLoader.Load("path-to-file.json")`.

## 2) Configuration file structure

Use this structure:

```json
{
  "cognitive": {
    "excludeFilePatterns": [],
    "excludePatterns": [],
    "scoreThreshold": 0.5,
    "showOnlyMethodsExceedingThreshold": true,
    "showHalsteadComplexity": false,
    "showCyclomaticComplexity": false,
    "showUnderstandability": false,
    "showCouplingMetrics": false,
    "showDetailedCognitiveMetrics": true,
    "groupByClass": true,
    "countElseAsNesting": false,
    "countElseIfAsNesting": false,
    "cache": { "enabled": false, "directory": ".cca/cache" },
    "metrics": {
      "ifCount": { "threshold": 3, "scale": 1.0, "enabled": true },
      "elseCount": { "threshold": 1, "scale": 1.0, "enabled": true },
      "loopCount": { "threshold": 2, "scale": 1.0, "enabled": true },
      "switchCount": { "threshold": 1, "scale": 1.0, "enabled": true },
      "tryCatchCount": { "threshold": 1, "scale": 1.0, "enabled": true },
      "returnCount": { "threshold": 2, "scale": 5.0, "enabled": true },
      "argumentCount": { "threshold": 4, "scale": 1.0, "enabled": true },
      "nestingLevels": { "threshold": 1, "scale": 1.0, "enabled": true }
    }
  }
}
```

## 3) Top-level options

- `excludeFilePatterns` (`string[]`): regular expressions; a source file is left out when one of them matches its absolute path, written with `/` as the separator on every platform.
- `excludePatterns` (`string[]`): regular expressions; a method or constructor is left out when one of them matches its name `Namespace.Outer.Inner::Method`, see [Analysed methods](#7-analysed-methods).
  All overloads of a method share that name.

  Both lists use .NET regular expressions, case-sensitive, and a pattern matches when it is found anywhere in the text; use `^` and `$` to anchor it.
  An invalid regular expression is ignored with a warning that names the pattern and the key (`Invalid regular expression '(' in 'excludePatterns' is ignored: ...`).
  The keys had no effect in earlier versions, so an old configuration with an invalid pattern keeps working.

  ```json
  {
    "cognitive": {
      "excludeFilePatterns": ["/Generated/", "\\.g\\.cs$", "Tests\\.cs$"],
      "excludePatterns": ["::(Get|Set)[A-Z]", "^MyApp\\.Legacy\\."]
    }
  }
  ```
- `scoreThreshold` (`double`): threshold used in report filtering and summary counts.
  It also sets the score colors of the console and HTML reports: green up to the threshold, yellow up to twice the threshold, red above.
- `showOnlyMethodsExceedingThreshold` (`bool`): if `true`, console report only shows methods above `scoreThreshold`.
- `groupByClass` (`bool`): groups output by class in console and HTML reports. HTML output also includes a client-side filter (class name / file path) and default ordering by cognitive score: classes by **maximum** method score in the class (highest first), methods within each class by `totalScore` (highest first).
- `countElseAsNesting` (`bool`): controls whether `else` increases nesting depth.
- `countElseIfAsNesting` (`bool`): controls whether `else if` increases nesting depth.
  The nesting depth of a method (`nestingLevels`) is the deepest chain of nested `if`, `for`, `foreach`, `while`, `do`, `switch`, `try`, `lock`, `using`, `fixed`, `checked` and `unsafe` statements.
  Each of them adds one level for everything inside it.
  The `catch` and `finally` blocks of a `try` share the level of the `try` body.
  `for (...) { for (...) { for (...) { } } }` has a nesting depth of 3.
- `showHalsteadComplexity` (`bool`): when `true`, console and HTML reports include Halstead volume, difficulty, and effort columns (metrics are always computed during analysis).
- `showCyclomaticComplexity` (`bool`): when `true`, console and HTML reports include cyclomatic complexity (computed during analysis).
- `showUnderstandability` (`bool`, default `false`): when `true`, the console, HTML and Markdown reports end with an `Understandability` column (`7 (medium)`) and the CSV report ends with the columns `Understandability` and `UnderstandabilityRiskLevel`.
  Understandability is the SonarSource cognitive complexity; see [Understandability.md](Understandability.md).
  It is always computed and always in the JSON report.
- `showCouplingMetrics` (`bool`): when `true` and `groupByClass` is `true`, console and HTML reports show class coupling (incoming, outgoing, stability) in each class header. Coupling is always computed during analysis.
- `showDetailedCognitiveMetrics` (`bool`, default `true`): shows the detail columns (Lines, Ifs, Else, Loops, Switch, Try/Catch, Arguments, Nesting, Returns, Locals, Fields, Props) in the console, HTML and Markdown reports.
  With `false`, those reports keep only Method, Score and the optional Halstead, Cyclomatic Complexity and Churn columns.
  The CSV, JSON, SARIF and CI reports always carry every metric.
- `cache` (`object`): the analysis cache, off by default; see [Analysis cache](#8-analysis-cache).
  `cache.enabled` (`bool`, default `false`) turns it on and `cache.directory` (`string`, default `.cca/cache`) names the directory.
- `customReporters` (`{ "cognitive": {...}, "churn": {...} }`): reporters from plugin assemblies, each key mapping to `{ "type": "Namespace.Type", "assembly": "path/to/plugin.dll" }`.
  A relative `assembly` is resolved against the directory of this file.
  Select a reporter with `-r <key>`; see [Custom reporters](CustomReporters.md).
- `metrics` (`Dictionary<string, MetricConfiguration>`): scoring rules per metric key.
  Every key must be one of the supported metric keys in section 5.
  An unknown key is ignored with a warning that names the key and lists the valid keys.

## 4) Metric option fields

Each metric entry contains:

- `threshold` (`int`): score starts increasing only above this value.
- `scale` (`double`): controls growth rate of the logarithmic weight.
- `enabled` (`bool`): enables/disables score contribution for that metric key.

## 5) Metric keys

### Supported metric keys

These align with analyzer fields and can contribute to `totalScore` when `enabled` is `true`:

- `linesOfCode` → `linesOfCodeScore`
- `ifCount` → `ifScore`
- `elseCount` → `elseScore`
- `loopCount` → `loopScore`
- `switchCount` → `switchScore`
- `tryCatchCount` → `tryCatchScore`
- `returnCount` → `returnScore`
- `argumentCount` → `argumentScore`
- `nestingLevels` → `nestingScore`
- `localVariableCount` → `localVariableScore`
- `fieldAccessCount` → `fieldAccessScore`
- `propertyAccessCount` → `propertyAccessScore`

### Deprecated metric keys

The first settings file of this project contained `variableCount` and `propertyCallCount`.
They never scored anything.
They are still accepted as aliases of `localVariableCount` and `propertyAccessCount`, with a warning that asks you to rename them.
If you enabled one of them, it now takes effect under its new name.

### Shipped defaults

The shipped `cognitive-metrics-settings.json` is the single source of the built-in defaults.
It is embedded in the library, it is what the tool uses when no config file is found, and `--generate-config` writes it out unchanged.

The shipped `cognitive-metrics-settings.json` enables core control-flow and size metrics (`linesOfCode`, `ifCount`, `elseCount`, `argumentCount`, `returnCount`, `nestingLevels`).

Additional metrics (`loopCount`, `switchCount`, `tryCatchCount`, `localVariableCount`, `propertyAccessCount`, `fieldAccessCount`) are present but **disabled by default** so existing `totalScore` behavior is unchanged until you opt in by setting `enabled` to `true`.

All listed metrics are counted during analysis and appear in console/CSV output regardless of the `enabled` flag; `enabled` only controls whether the metric contributes to `totalScore`.

Local variables include `foreach` iteration variables and `catch` variables, in addition to declared locals.
Deconstruction and pattern variables are not counted.

## 6) How to execute

From repo root, run the console app with:

```powershell
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- [searchPath] [options]
```

- `[searchPath]` optional. A `.cs` file, a directory, or a comma-separated list of both. Defaults to current directory.
  Directories are searched recursively for `*.cs` files.
  The entries of a list are trimmed, made absolute, deduplicated and sorted.
  A path that exists as written stays one path even when it contains a comma.
  A path that does not exist contributes no files; when no file is found at all, the tool prints `No C# files found in <paths>.` and exits with a non-zero status.
  Directories named `bin` or `obj` and directories whose name starts with a dot (`.git`, `.vs`, ...) are not searched, because they hold build output and tool state.
  A path you pass yourself is always searched, even when it is such a directory.
  Symbolic links to directories are followed, but every real directory is searched only once, so a link to a parent directory neither loops nor reports a file twice.
  Use `excludeFilePatterns` to leave out anything else.

### CLI options

- `-c|--config <path>`: load `ConfigurationLoader.Load(path)` from that JSON file (same `cognitive` schema as the default file).
- `-r|--report-type <type>`: `ConsoleText` (default), `Html`, `Markdown`, `Json`, `Csv`, `Sarif`, `GithubActions`, `GitlabCodeQuality`, `Checkstyle`, or `JUnit`.
  Matched case-insensitively.
  See [CiReports.md](CiReports.md) for the CI formats.
- `-o|--output-file <path>`: output path (default: `cognitive-analysis-report`).
- `-b|--baseline <path>`: path to a JSON baseline snapshot (from `--generate-baseline` or a prior `-r Json` run). When set, all reports include deltas versus that baseline. HTML report shows red ▲ / green ▼ suffixes after values.
  Without this option the newest file in `.cca/baseline` is used, if that directory exists.
  See [Baseline.md](Baseline.md).
- `-g|--generate-baseline`: write the analysis as a new baseline to `.cca/baseline`.
- `--coverage-cobertura <path>`: optional coverage file (Cobertura with counts, Cobertura with rates such as coverlet writes, or Visual Studio XML).
  Methods match by name and by a first coverage line inside the method.
  Methods without branches show no branch coverage, and their churn uses the line coverage.
  A coverage file that loads but matches no method produces a warning, and so does one that matches only some of the analysed classes.
- `--sort-by <field>`: sort the rows of the report by a field instead of by total score.
  Fields: `score`, `class`, `method`, `file`, `line`, `halstead` (volume), `cyclomatic`, `understandability`, the count of every metric (`linesOfCode`, `argumentCount`, `returnCount`, `localVariableCount`, `propertyAccessCount`, `fieldAccessCount`, `ifCount`, `elseCount`, `nestingLevels`, `loopCount`, `switchCount`, `tryCatchCount`) and the score of every metric (`<metric>Score`, for example `ifCountScore` or `nestingLevelsScore`).
  Field names are case-insensitive; the score column names of the CSV and JSON reports (`ifScore`, `nestingScore`, ...) are accepted as well.
  Numbers compare numerically, text ignores the letter case, and equal values keep the order of class, method, file and line.
  With `groupByClass` the classes keep their order and the rows inside each class table are sorted.
  An unknown field or order fails with the list of valid values.
- `--sort-order <asc|desc>`: direction of `--sort-by` (default `asc`, case-insensitive).
- `--debug`: after the run, print an execution summary to stderr with the total time, the time of each phase and statistics (files found, analysed and failed, methods analysed).
  Phases that run side by side, such as cognitive and coupling analysis, can overlap, so the phase times need not add up to the total.
- `--version`: print the version without the `+sha` suffix and exit.
- `--coverage-clover <path>`: optional Clover coverage file.
  It cannot be combined with `--coverage-cobertura`.
  See [Coverage.md](Coverage.md) for the formats, the matching and the columns the HTML and Markdown reports gain.
- `--show-halstead`: force `showHalsteadComplexity` on for this run (overrides config).
- `--show-cyclomatic`: force `showCyclomaticComplexity` on for this run (overrides config).
- `--show-coupling`: force `showCouplingMetrics` on for this run (overrides config).
- `--show-understandability`: force `showUnderstandability` on for this run (overrides config).
- `--cache` / `--no-cache`: turn the analysis cache on or off for this run (overrides `cache.enabled`).
  The two options cannot be combined.

An unknown option or a surplus argument is ignored with a warning that lists it; the exit code is not affected.

CLI boolean flags use Spectre’s optional `bool?` binding: omit the option to keep the JSON value; pass `--show-halstead` / `--show-cyclomatic` to enable display for that run. To force `false` from the shell, rely on the config file or omit these flags.

### Creating a config file

`init` writes a config file that contains every setting, with the built-in defaults.

```powershell
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- init
```

In an interactive console, `init` asks for these settings.
Each question has a one-line explanation above it, and an empty answer keeps the default shown in brackets.

- `scoreThreshold` (default `0.5`) must be a number greater than 0.
  An invalid answer is reported, and the question is asked again.
- `showOnlyMethodsExceedingThreshold`, `showHalsteadComplexity`, `showCyclomaticComplexity`, `showCouplingMetrics`, `showDetailedCognitiveMetrics` and `groupByClass` are yes/no questions.

The answers are written into the built-in defaults, so the file keeps the keys and the metric settings of the shipped file.

- `--path`, `-p <file>` sets the file to create.
  The default is `./cognitive-metrics-settings.json`, and missing parent directories are created.
- `--silent` skips the questions and writes the defaults.
  Questions are also skipped when the output is not an interactive console or standard input is redirected.
- `--force`, `-f` overwrites an existing file.
  Without it, `init` refuses to replace the file, reports it, and exits with a non-zero code before asking any question.

The file is created in one step, so a failed write does not leave a partial file behind.
`init` does not change `--generate-config`, which still writes the shipped file unchanged.

### Examples

```powershell
# Analyze current folder with console output
dotnet run --project .\CognitiveCodeAnalysisConsoleApp --

# Analyze a specific folder
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- .\src

# Generate HTML report
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- .\src -r Html -o .\report.html

# Custom config and enable Halstead + cyclomatic columns for this run
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- .\src -c .\my-config.json --show-halstead --show-cyclomatic

# Cobertura coverage (optional)
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- .\src --coverage-cobertura .\coverage.cobertura.xml

# Save a JSON baseline snapshot
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- .\src -r Json -o .\baseline.json

# Compare against baseline (HTML shows colored deltas)
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- .\src -b .\baseline.json -r Html -o .\report.html
```

## 7) Analysed methods

The methods and constructors of classes, structs, records, record structs and interfaces are analysed.
Interface members count only when they have a body, that is default interface methods and static interface methods with a body.
Abstract and `extern` methods of other types count with all metrics at zero except the line and argument counts, as before.

A type is named by its namespace and its containing types, separated by dots, such as `MyApp.Outer.Inner`, whether the containing types are classes, structs, records or interfaces.
A method is named `MyApp.Outer.Inner::Method`.
A constructor is named after its type, `MyApp.Outer::Outer`, and so is a static constructor; its signature (`static Outer()`) tells it apart.
Overloads share the name and are told apart by their signature, as before.
In a coverage report, a constructor is matched to the entry `.ctor` and a static constructor to `.cctor`.

Property, indexer and event accessors, operators, conversion operators, finalizers and primary constructors (`record Point(int X, int Y)`, `class Service(ILogger logger)`) are not analysed.
Local functions, lambdas and anonymous methods are part of the method that contains them, and their statements count for that method.
Enums and delegates have no methods to analyse.

### `@cca-ignore`

To leave out a type or a member, put `@cca-ignore` into a comment that belongs to its declaration:

- its XML doc comment (`/// <remarks>@cca-ignore</remarks>`),
- any comment between the previous declaration and its name, including a comment after the opening `{` of its type and comments between attributes and modifiers,
- or a comment at the end of its last line.

```csharp
// @cca-ignore: generated by the protocol compiler
public partial class Messages { ... }

public class Service
{
    /// <summary>Maps every status code.</summary>
    /// <remarks>@cca-ignore</remarks>
    public string Describe(int status) { ... }

    public Service() { } // @cca-ignore
}
```

Ignoring a type also ignores the types nested in it.
A comment inside a body does not ignore the method.
A comment after the end of a declaration belongs to that declaration, unless another declaration starts after it on the same line: in `void A() { } /* @cca-ignore */ void B() { }` only `B` is ignored.
The tag is case-sensitive.

## 8) Analysis cache

The cache stores the metrics of each file, so a repeated run on an unchanged tree does not analyse the files again.
This helps most in CI, where one job writes several report types from the same sources.

It is off by default.
A run without it reads and writes nothing and creates no directory.
Turn it on with `cache.enabled: true` or `--cache` and off with `--no-cache`; the command line wins over the file.
`cache.directory` is resolved against the working directory.
Add it to `.gitignore`; the directory is safe to delete at any time.

- Each entry is one JSON file at `<directory>/<first two characters of the key>/<key>.json`.
- The key is a SHA-256 hash of the file path, the hash of the content of the file, a digest of the content of every analysed file, the whole configuration (every key except `cache`), the version of the tool and the .NET runtime.
- Coverage, baselines and the report type are applied after the cache, so changing them does not invalidate it.
- Any other setting change, including thresholds and display options, invalidates the entries, so a setting added later can never give a stale result.
- Class coupling is not cached; it is computed across all files on every run.
- A corrupt, unreadable or outdated entry counts as missing and is overwritten.
- Entries are written to a temporary file and moved into place, so a run that is interrupted leaves no partial entry.
- When the cache cannot be written, the run prints one warning and continues without it.

**Limit: any change to any file invalidates every entry.**
The field and property counts of a method come from the semantic model of all files together, so the metrics of a file can change when another file changes, for example when a member turns from a field into a property.
The cache therefore cannot reuse the entries of the untouched files after an edit.
It pays off for repeated runs on the same sources, not for incremental runs while editing.
