# Custom reporters

A custom reporter is a class in your own assembly that turns the analysis result into any text format you need.
The tool loads it from the configuration and runs it when you pass its key to `-r`.
There are two kinds: one for `analyse` and one for `churn`.

## The plugin interface

The interfaces and the records they receive live in the namespace `CognitiveCodeAnalysis.Reporting.Spi` of `CognitiveCodeAnalysis.dll`.
They are the only part of the library that plugins should use.

```csharp
public interface ICustomCognitiveReporter
{
    string Render(CognitiveReportInput input);
}

public interface ICustomChurnReporter
{
    string Render(ChurnReportInput input);
}
```

Your class needs a public constructor without parameters.
The tool creates one instance per run, calls `Render` once and writes the returned text to the file given with `-o`.
The file is written in one step, so a reader never sees a half-written report.
If `Render` throws or returns `null`, nothing is written and the run fails with a message.

### What `Render` receives

`CognitiveReportInput` for `analyse`:

| Member | Content |
| --- | --- |
| `Methods` | Every analysed method. Ordered by `--sort-by`, otherwise by total score, highest first. Not filtered by the score threshold. |
| `Settings` | `ScoreThreshold`, `ShowOnlyMethodsExceedingThreshold`, `GroupByClass`, `ShowHalsteadComplexity`, `ShowCyclomaticComplexity`, `ShowCouplingMetrics`, `ShowDetailedCognitiveMetrics`. |
| `HasBaseline` | `true` when `--baseline` was given. |

Each `CognitiveMethodRow` has:

- `FilePath`: relative to the working directory with `/` separators, or absolute when the file lies outside it.
- `ClassName`, `MethodName`, `MethodSignature`, `Line`.
- `TotalScore`.
- `LinesOfCode`, `Arguments`, `Returns`, `LocalVariables`, `PropertyAccesses`, `FieldAccesses`, `Ifs`, `Elses`, `Nesting`, `Loops`, `Switches`, `TryCatches`: each a `CountedMetric` with `Count` and `Score`.
- `CyclomaticComplexity`, `HalsteadVolume`, `HalsteadDifficulty`, `HalsteadEffort` (the Halstead values are `null` when not measured).
- `LineCoverage` and `BranchCoverage` in percent, `null` without coverage data.
- `Deltas`: `null` without a baseline or when the method is not in it, otherwise `TotalScore`, `CyclomaticComplexity`, `LineCoverage` and `BranchCoverage` as current minus baseline (each `null` when one side has no value).

`ChurnReportInput` for `churn`:

| Member | Content |
| --- | --- |
| `Classes` | One `ChurnRow` per class, highest churn first. |
| `CoverageProvided` | `true` when `--coverage-cobertura` was given. |
| `Since` | The day the changes are counted from. |

Each `ChurnRow` has `ClassName`, `FilePath` (as above), `Score`, `TimesChanged`, `Churn`, and, with coverage, `Coverage` (0.0 to 1.0), `RiskChurn` and `RiskLevel` (`CRITICAL`, `HIGH`, `MEDIUM` or `LOW`).
The records are immutable.
New members may be added in later releases, existing ones are not renamed or removed.

## Example

The library is not published as a NuGet package.
Reference the `CognitiveCodeAnalysis.dll` that sits next to the tool.
Set `Private` to `false`, so the build does not copy it: at run time the plugin uses the copy the tool already loaded.

`HotspotReporter.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="CognitiveCodeAnalysis">
      <HintPath>C:\tools\cognitive\CognitiveCodeAnalysis.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

</Project>
```

`HotspotReporter.cs`:

```csharp
using System.Globalization;
using System.Linq;
using System.Text;
using CognitiveCodeAnalysis.Reporting.Spi;

namespace Acme;

public sealed class HotspotReporter : ICustomCognitiveReporter
{
    public string Render(CognitiveReportInput input)
    {
        var text = new StringBuilder();
        foreach (CognitiveMethodRow method in input.Methods
            .Where(m => m.TotalScore > input.Settings.ScoreThreshold)
            .Take(10))
        {
            text.Append(CultureInfo.InvariantCulture,
                $"{method.TotalScore:0.###}  {method.ClassName}.{method.MethodName}  {method.FilePath}:{method.Line}\n");
        }

        return text.ToString();
    }
}
```

Build it with `dotnet build -c Release`.
Register it in the configuration file, under `cognitive`:

```json
{
  "cognitive": {
    "scoreThreshold": 0.5,
    "customReporters": {
      "cognitive": {
        "hotspots": {
          "type": "Acme.HotspotReporter",
          "assembly": "plugins/HotspotReporter.dll"
        }
      },
      "churn": {}
    }
  }
}
```

Copy `HotspotReporter.dll` to `plugins/` next to the configuration file and run:

```powershell
CognitiveCodeAnalysis .\src -c .\cognitive-metrics-settings.json -r hotspots -o .\hotspots.txt
```

A churn reporter works the same way with `ICustomChurnReporter`, listed under `customReporters.churn`, and is used with `CognitiveCodeAnalysis churn -r <key>`.

## Configuration

Each key has a `type` and an `assembly`.

- `type` is the full name of the class, including the namespace.
- A relative `assembly` path is resolved against the directory of the configuration file, not against the working directory.
- Keys are case-sensitive.
- Without a configuration file nothing is registered.

## How `-r` picks a report

1. The built-in report types come first, in any letter case.
2. Otherwise the key is looked up in `customReporters`, spelled exactly.
3. Otherwise the run fails with an error that lists the built-in types and the custom keys.

A custom key that has the name of a built-in type (in any letter case, for `churn` also the aliases `svg` and `svg-treemap`) can never be used.
The tool reports that only when `-r` names such a type; other runs are not affected.

## Dependencies and isolation

The plugin runs in its own `AssemblyLoadContext`.
Its own dependencies are loaded from the folder of the plugin dll, using its `.deps.json` when there is one, otherwise the dlls next to it.
`CognitiveCodeAnalysis.dll` always comes from the tool, even if the plugin folder holds another copy, so the interfaces match.
Other assemblies the tool uses, such as Roslyn, are not shared: a plugin that needs one ships its own copy.

## Errors

Each failure stops the run with a non-zero exit code and a message that names the key.
The causes are: the assembly does not exist or cannot be loaded, the type is not in it, it does not implement the interface, it has no public parameterless constructor (or is abstract), the constructor or a static initializer throws, `Render` throws, or `Render` returns `null`.
For exceptions the message shows the exception that was really thrown, not the reflection wrapper around it.
A plugin is trusted code that runs with the permissions of the tool.
Only register assemblies you trust.
