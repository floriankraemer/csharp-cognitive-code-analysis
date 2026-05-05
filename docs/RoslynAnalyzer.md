# Roslyn analyzer (dotnet / Visual Studio)

The cognitive analyzer runs as a **compilation Roslyn analyzer**. That matches Microsoft’s guidance for analyzers that should participate in **`dotnet build`**, IntelliSense squiggles, and the **Error List**—not only when a VSIX is installed.

The VSIX can still be used to ship editor integration, but **live diagnostics** for your code should come from the analyzer assembly referenced by MSBuild (see links in `analyzer/CognitiveAnalysis.LocalAnalyzers.targets`).

## One-time bootstrap (this repository)

From the `Cognitive Code Analysis` directory:

```powershell
pwsh -File .\make.ps1 bootstrap-local-analyzer
```

This builds `CognitiveCodeAnalysisExtension` in **Release** and copies `*.dll` from `CognitiveCodeAnalysisExtension\CognitiveCodeAnalysisExtension\bin\Release\netstandard2.0\` into **`artifacts\local-analyzer\`**.

Then **rebuild** the solution (or reload the project in Visual Studio) so MSBuild picks up **`CognitiveCodeAnalysisExtension.dll`** from that folder.

The drop is listed under `artifacts/` (typically gitignored). If the folder is missing or stale, run bootstrap again after changing analyzer code.

## How projects load the analyzer

`Directory.Build.targets` imports `analyzer\CognitiveAnalysis.LocalAnalyzers.targets`, which:

- Looks for **`artifacts\local-analyzer\CognitiveCodeAnalysisExtension.dll`** (override path with MSBuild property **`LocalCognitiveAnalyzersRoot`** if needed).
- When found and **`CognitiveAnalysisUseLocalAnalyzers`** is not **`false`**, adds **`Analyzer`** items for every `*.dll` in that directory **except** `Microsoft.CodeAnalysis*.dll` (avoids duplicate Roslyn references).

To **disable** local analyzers for a project or tree (e.g. CI without bootstrap, or the extension projects that build the analyzer itself), set:

```xml
<PropertyGroup>
  <CognitiveAnalysisUseLocalAnalyzers>false</CognitiveAnalysisUseLocalAnalyzers>
</PropertyGroup>
```

The extension solution already sets this under `CognitiveCodeAnalysisExtension\Directory.Build.props`.

## Configuring thresholds and metrics (parity with CLI)

The analyzer merges **embedded CLI defaults** with optional JSON overlays. Use the **same schema** as the command-line tool: a `cognitive` section in **`cognitive-metrics-settings.json`**.

Roslyn passes configuration through **additional files**:

1. Add or reuse **`cognitive-metrics-settings.json`** in your project (see [Configuration.md](./Configuration.md)).
2. Register it so analyzers receive it:

```xml
<ItemGroup>
  <AdditionalFiles Include="cognitive-metrics-settings.json" />
</ItemGroup>
```

The analyzer looks for additional inputs whose **file name** is **`cognitive-metrics-settings.json`** (case-insensitive). Typical options:

| Setting | Role |
|---------|------|
| `scoreThreshold` | Methods/classes below or equal to this **total score** do not produce warnings when filtering is on. |
| `showOnlyMethodsExceedingThreshold` | If `true`, only report methods with **totalScore > scoreThreshold** (and class diagnostics when any method qualifies). |
| `metrics` | Same per-metric `threshold`, `scale`, `enabled` as CLI (`ScoreCalculator`). |

## Diagnostic IDs

| ID | Meaning |
|----|--------|
| `CognitiveComplexityMethod` | Per-method cognitive score (Maintainability warning). |
| `CognitiveComplexityClass` | Sum of method scores for the type (Maintainability warning). |

Severity comes from the analyzer descriptor (currently **Warning**). Tune behavior via **`cognitive-metrics-settings.json`** and rebuild.

## Troubleshooting

- **No diagnostics after clone:** Run **`bootstrap-local-analyzer`** and rebuild.
- **Diagnostics on “zero” complexity:** With **`showOnlyMethodsExceedingThreshold`: true**, small scores should not warn; confirm **`AdditionalFiles`** points at your JSON and that **`scoreThreshold`** matches expectations.
- **Building the analyzer project:** Projects under `CognitiveCodeAnalysisExtension` set **`CognitiveAnalysisUseLocalAnalyzers=false`** so they don’t analyze themselves with a duplicate copy of the same assembly mid-build.
