# Configuration Guide

This project reads analysis settings from a JSON file with a `cognitive` root section.

## 1) Configuration file location

By default, the app loads:

- `cognitive-metrics-settings.json`
- from the application base directory (`AppContext.BaseDirectory`)

If you use the library directly, you can load another file path with `ConfigurationLoader.Load("path-to-file.json")`.

For **Roslyn analyzers** in Visual Studio / `dotnet build`, the same JSON is supplied via **`AdditionalFiles`**—see [RoslynAnalyzer.md](./RoslynAnalyzer.md).

## 2) Configuration file structure

Use this structure:

```json
{
  "cognitive": {
    "excludeFilePatterns": [],
    "excludePatterns": [],
    "scoreThreshold": 0.5,
    "showOnlyMethodsExceedingThreshold": true,
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
- `showOnlyMethodsExceedingThreshold` (`bool`): if `true`, console report only shows methods above `scoreThreshold`.
- `groupByClass` (`bool`): groups output by class in console and HTML reports.
- `countElseAsNesting` (`bool`): controls whether `else` increases nesting depth.
- `countElseIfAsNesting` (`bool`): controls whether `else if` increases nesting depth.
- `metrics` (`Dictionary<string, MetricConfiguration>`): scoring rules per metric key.

## 4) Metric option fields

Each metric entry contains:

- `threshold` (`int`): score starts increasing only above this value.
- `scale` (`double`): controls growth rate of the logarithmic weight.
- `enabled` (`bool`): enables/disables score contribution for that metric key.

## 5) Metric keys

### Keys that map directly to scoring fields

These align with analyzer metric fields and total score calculation:

- `ifCount`
- `elseCount`
- `loopCount`
- `switchCount`
- `tryCatchCount`
- `returnCount`
- `argumentCount`
- `nestingLevels`

### Notes on current defaults

The shipped `cognitive-metrics-settings.json` contains keys such as `linesOfCode`, `variableCount`, and `propertyCallCount`.
Those keys do not currently map to active score fields in `ScoreCalculator`, so they do not affect `totalScore`.

It also includes `showHalsteadComplexity`, `showCyclomaticComplexity`, and `showDetailedCognitiveMetrics`, which are not part of `CognitiveConfiguration` and are ignored during binding.

## 6) How to execute

From repo root, run the console app with:

```powershell
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- [searchPath] [options]
```

- `[searchPath]` optional. Defaults to current directory.

### CLI options

- `-c|--config <path>`: custom config path (currently defined in CLI options, but not wired into `ConfigurationLoader.Load(...)` in `AnalyseCommand`; default config is still used).
- `-r|--report-type <type>`: report type (`ConsoleText` or `Html`).
- `-o|--output-file <path>`: output path (default: `cognitive-analysis-report`).
- `--coverage-cobertura <path>`: optional Cobertura coverage file.

### Examples

```powershell
# Analyze current folder with console output
dotnet run --project .\CognitiveCodeAnalysisConsoleApp --

# Analyze a specific folder
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- .\src

# Generate HTML report
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- .\src -r Html -o .\report.html

# Pass custom config option (currently ignored by command implementation)
dotnet run --project .\CognitiveCodeAnalysisConsoleApp -- .\src -c .\my-config.json
```
