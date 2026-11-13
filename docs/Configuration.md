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
    "showCouplingMetrics": false,
    "showDetailedCognitiveMetrics": true,
    "groupByClass": true,
    "countElseAsNesting": false,
    "countElseIfAsNesting": false,
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

- `excludeFilePatterns` (`string[]`): configured but currently not applied in source file discovery.
- `excludePatterns` (`string[]`): configured but currently not applied in source file discovery.
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
- `showCouplingMetrics` (`bool`): when `true` and `groupByClass` is `true`, console and HTML reports show class coupling (incoming, outgoing, stability) in each class header. Coupling is always computed during analysis.
- `showDetailedCognitiveMetrics` (`bool`, default `true`): shows the detail columns (Lines, Ifs, Else, Loops, Switch, Try/Catch, Arguments, Nesting, Returns, Locals, Fields, Props) in the console, HTML and Markdown reports.
  With `false`, those reports keep only Method, Score and the optional Halstead, Cyclomatic Complexity and Churn columns.
  The CSV, JSON, SARIF and CI reports always carry every metric.
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

- `[searchPath]` optional. Defaults to current directory.

### CLI options

- `-c|--config <path>`: load `ConfigurationLoader.Load(path)` from that JSON file (same `cognitive` schema as the default file).
- `-r|--report-type <type>`: `ConsoleText` (default), `Html`, `Markdown`, `Json`, `Csv`, `Sarif`, `GithubActions`, or `GitlabCodeQuality`. Matched case-insensitively.
- `-o|--output-file <path>`: output path (default: `cognitive-analysis-report`).
- `-b|--baseline <path>`: path to a JSON baseline snapshot (from a prior `-r Json` run). When set, all reports include deltas versus that baseline. HTML report shows red ▲ / green ▼ suffixes after values.
- `--coverage-cobertura <path>`: optional coverage file (Cobertura with counts, Cobertura with rates such as coverlet writes, or Visual Studio XML).
  Methods match by name and by a first coverage line inside the method.
  Methods without branches show no branch coverage, and their churn uses the line coverage.
  A coverage file that loads but matches no method produces a warning.
- `--show-halstead`: force `showHalsteadComplexity` on for this run (overrides config).
- `--show-cyclomatic`: force `showCyclomaticComplexity` on for this run (overrides config).
- `--show-coupling`: force `showCouplingMetrics` on for this run (overrides config).

An unknown option or a surplus argument is ignored with a warning that lists it; the exit code is not affected.

CLI boolean flags use Spectre’s optional `bool?` binding: omit the option to keep the JSON value; pass `--show-halstead` / `--show-cyclomatic` to enable display for that run. To force `false` from the shell, rely on the config file or omit these flags.

### Creating a config file

`init` writes a config file that contains every setting, with the built-in defaults.

```powershell
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- init
```

In an interactive console, `init` asks for these settings.
An empty answer keeps the default shown in brackets.

- `scoreThreshold` (default `0.5`) must be a number greater than 0.
  An invalid answer is reported, and the question is asked again.
- `showOnlyMethodsExceedingThreshold`, `showHalsteadComplexity`, `showCyclomaticComplexity`, `showCouplingMetrics`, `showDetailedCognitiveMetrics` and `groupByClass` are yes/no questions.

The answers are written into the built-in defaults, so the file keeps the keys and the metric settings of the shipped file.

- `--path`, `-p <file>` sets the file to create.
  The default is `./cognitive-metrics-settings.json`, and missing parent directories are created.
- `--silent` skips the questions and writes the defaults.
  Questions are also skipped when the console is not interactive, such as when the output is redirected.
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
