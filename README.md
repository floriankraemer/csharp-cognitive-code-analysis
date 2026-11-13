# Cognitive Code Analysis for C#

Cognitive Code Analysis is an approach to understanding and improving code by focusing on how human cognition interacts with code.
It emphasizes making code more readable, understandable, and maintainable by considering the cognitive processes of the developers who write and work with the code.

> "Human short-term or working memory was estimated to be limited to 7 ± 2 variables in the 1950s. A more current estimate is 4 ± 1 constructs. Decision quality generally becomes degraded once this limit of four constructs is exceeded."

[Source: Human Cognitive Limitations. Broad, Consistent, Clinical Application of Physiological Principles Will Require Decision Support](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC5822395/)

This tool analyses C# source files and scores every method and constructor by how hard it is to read.
The score weights counts such as `if` statements, loops, nesting depth, arguments and returns with a logarithmic curve, so the first few are cheap and a long tail is expensive.
Next to the score it computes the cyclomatic complexity, the Halstead metrics, the class coupling and the understandability (the cognitive complexity of SonarSource).
Reports exist for people (console, HTML, Markdown, CSV, JSON) and for CI systems (SARIF, GitHub Actions, GitLab Code Quality, Checkstyle, JUnit).
It is the C# sibling of the [PHP implementation](https://github.com/Phauthentic/cognitive-code-analysis) and the Java port.

## Installation

### Release binaries

Every release on the [GitHub Releases page](https://github.com/floriankraemer/csharp-cognitive-code-analysis/releases) has self-contained single-file executables, so no .NET installation is needed.

| Platform | Archive |
| --- | --- |
| Windows x64 | `CognitiveCodeAnalysis-<version>-win-x64.zip` |
| Linux x64 | `CognitiveCodeAnalysis-<version>-linux-x64.tar.gz` |
| macOS x64 (Intel) | `CognitiveCodeAnalysis-<version>-osx-x64.tar.gz` |
| macOS arm64 (Apple silicon) | `CognitiveCodeAnalysis-<version>-osx-arm64.tar.gz` |

Unpack the archive and put the executable `CognitiveCodeAnalysis` (`CognitiveCodeAnalysis.exe` on Windows) on your `PATH`:

```bash
mkdir -p ~/.local/bin
tar -xzf CognitiveCodeAnalysis-<version>-linux-x64.tar.gz -C ~/.local/bin
CognitiveCodeAnalysis --version
```

### .NET tool from a local package

The project is set up as a .NET tool (package `CognitiveCodeAnalysis.Tool`, command `CognitiveCodeAnalysis`), but releases do not publish the package.
Build it yourself:

```bash
make pack-tool
dotnet tool install --global --add-source artifacts/nupkg CognitiveCodeAnalysis.Tool
```

`make pack-tool` runs in the dev container, see [Contributing](#contributing).
Without `make`, run `dotnet pack CognitiveCodeAnalysisConsoleApp -c Release -o artifacts/nupkg` with the .NET 8 SDK.

### From source

```bash
dotnet run --project CognitiveCodeAnalysisConsoleApp -- [searchPath] [options]
```

The rest of this page writes `CognitiveCodeAnalysis` for the executable.

## Quick start

Analyse the current directory and print a table per class:

```bash
CognitiveCodeAnalysis src
```

On a small sample project with two classes and `-c min.json`, a configuration that only sets `"showDetailedCognitiveMetrics": false`, the output is:

```text
Config: /path/to/min.json
Searching for C# files...
Compiling 2 source file(s)...
Analysing 2 file(s)...
Calculating scores for 3 method(s)...
Class: Shop.OrderService
File: /path/to/shop/src/OrderService.cs
┌──────────┬───────┐
│ Method   │ Score │
├──────────┼───────┤
│ L5 Total │ 2.079 │
└──────────┴───────┘


Summary:
Total Classes Processed: 2
Total Methods Processed: 3
Classes with Methods Exceeding Threshold: 1 (50.0%)
Methods Exceeding Threshold: 1 (33.3%)
Threshold: 0.500
```

By default only methods above `scoreThreshold` (0.5) are listed, and `L5` is the line of the method.
The detail columns (Lines, Ifs, Else, Loops, ...) are shown unless `showDetailedCognitiveMetrics` is `false`.
With the same sources a Markdown report (`-r Markdown -o report.md`) starts like this:

```markdown
# Cognitive Code Analysis Report

### Class: Shop.OrderService

**File:** `/path/to/shop/src/OrderService.cs`

| Method | Score |
| :--- | ---: |
| L5 Total | 2.079 |
```

Then save a baseline, so the next run shows what changed:

```bash
CognitiveCodeAnalysis src -g
```

## Commands

`CognitiveCodeAnalysis` has one default command that analyses code, and two named commands:

| Command | Purpose |
| --- | --- |
| `CognitiveCodeAnalysis [searchPath] [options]` | Analyse (the default command, there is no `analyse` keyword). |
| `CognitiveCodeAnalysis churn [path] [options]` | Find complex classes that change often. |
| `CognitiveCodeAnalysis init [options]` | Create a configuration file. |

`churn` and `init` are command names, so a directory with one of these names has to be passed as `./churn` or `./init`.
`CognitiveCodeAnalysis --version` prints the version, and `--help` works on every command.

### Analysis

```bash
CognitiveCodeAnalysis [searchPath] [options]
```

`searchPath` is a `.cs` file, a directory, or a comma-separated list of both, such as `src,tools/Build.cs`.
It defaults to the current directory.
Directories are searched recursively for `*.cs` files.
The list is trimmed and deduplicated, and a path that exists as written stays one path even when it contains a comma.
The directories `bin`, `obj` and those whose name starts with a dot (`.git`, `.vs`) are not searched, use `excludeFilePatterns` to leave out anything else.

| Option | Default | Description |
| --- | --- | --- |
| `-c`, `--config <path>` | `./cognitive-metrics-settings.json` if it exists, else the built-in defaults | Configuration file, see [Configuration](docs/Configuration.md). |
| `--generate-config [path]` | | Write the default `cognitive-metrics-settings.json` and exit. A path that ends in `.json` and is not a directory is the file to write, any other path is the directory that receives the file, and no path means the current directory. See also `init`. |
| `-f`, `-r`, `--report-type`, `--report-format <type>` | `ConsoleText` | `ConsoleText`, `Html`, `Markdown`, `Json`, `Csv`, `Sarif`, `GithubActions`, `GitlabCodeQuality`, `Checkstyle` or `JUnit`, in any letter case, or the key of a [custom reporter](docs/CustomReporters.md). |
| `-o`, `--output-file <path>` | `cognitive-analysis-report` | File for a report that is not printed to the console. The default name has no extension and is the same for every type, so always pass `-o` when you write a file. |
| `-b`, `--baseline <path>` | newest file in `.cca/baseline`, if that directory exists | Baseline to compare with, see [Baseline](docs/Baseline.md). |
| `-g`, `--generate-baseline` | | Write the analysis as a new baseline to `.cca/baseline/baseline-<UTC timestamp>.json`. |
| `--coverage-cobertura <path>` | | Coverage file in Cobertura or Visual Studio format, see [Coverage](docs/Coverage.md). |
| `--coverage-clover <path>` | | Coverage file in Clover format. Cannot be combined with `--coverage-cobertura`. |
| `--show-halstead` | config | Show the Halstead columns for this run. |
| `--show-cyclomatic` | config | Show the cyclomatic complexity for this run. |
| `--show-coupling` | config | Show the class coupling for this run. |
| `--show-understandability` | config | Show the [understandability](docs/Understandability.md) for this run. |
| `--sort-by <field>` | total score | Sort the rows by a field, see [Sorting](#sorting). |
| `--sort-order <asc\|desc>` | `asc` | Direction of `--sort-by`. |
| `--cache` | config | Use the [analysis cache](docs/Configuration.md#8-analysis-cache) for this run. |
| `--no-cache` | config | Do not use the analysis cache for this run. |
| `--debug` | | Print the total time, the time of each phase and statistics to stderr after the run. |
| `-v`, `--verbose` | | Print timestamped diagnostic logs to stderr for each phase. |
| `-h`, `--help` | | Show the help and exit. |

The `--show-*` flags can only turn a column on.
To turn it off, set it to `false` in the configuration file.
Unknown options and surplus arguments are ignored, and the tool prints a warning that lists them.
All output is culture-invariant: numbers always use a dot as the decimal separator.
The exit code is 0 after a successful run, however many methods exceed the threshold.
An error such as an unknown report type, an unreadable baseline or no `.cs` file found gives a non-zero exit code.

What is analysed: the methods and constructors of classes, structs, records, record structs and interfaces (interface members only with a body).
Property accessors, operators, finalizers and primary constructors are not analysed, and local functions and lambdas count for the method that contains them.
Put `@cca-ignore` into a comment of a type or member to leave it out.
See [Analysed methods](docs/Configuration.md#7-analysed-methods).

#### Examples

```bash
# Current directory, console report
CognitiveCodeAnalysis

# One file, or a list of files and folders
CognitiveCodeAnalysis src/Service.cs
CognitiveCodeAnalysis src,tools/Build.cs

# HTML report with a custom configuration
CognitiveCodeAnalysis src -r Html -o report.html -c my-config.json

# Halstead, cyclomatic complexity and understandability for this run
CognitiveCodeAnalysis src --show-halstead --show-cyclomatic --show-understandability

# Worst rows first
CognitiveCodeAnalysis src --sort-by score --sort-order desc

# Coverage from coverlet
CognitiveCodeAnalysis src --coverage-cobertura coverage.cobertura.xml

# Save a baseline and compare with it later
CognitiveCodeAnalysis src -g
CognitiveCodeAnalysis src -r Html -o report.html
```

#### Sorting

`--sort-by` takes `score`, `class`, `method`, `file`, `line`, `halstead` (volume), `cyclomatic`, `understandability`, the count of a metric (`linesOfCode`, `argumentCount`, `returnCount`, `localVariableCount`, `propertyAccessCount`, `fieldAccessCount`, `ifCount`, `elseCount`, `nestingLevels`, `loopCount`, `switchCount`, `tryCatchCount`) or the score of a metric (`<metric>Score`, such as `ifCountScore`).
Field names are case-insensitive, and the score columns of the CSV and JSON reports (`ifScore`, `nestingScore`) are accepted too.
Equal values keep the order of class, method, file and line.
An unknown field fails with the list of valid values.

### Churn

```bash
CognitiveCodeAnalysis churn [path] [options]
```

The `churn` command multiplies the cognitive score of each class with the number of git commits that changed its file.
Classes that are both complex and edited all the time come first.

| Option | Default | Description |
| --- | --- | --- |
| `[path]` | current directory | A `.cs` file, a directory, or a comma-separated list of both. |
| `-s`, `--since <yyyy-MM-dd>` | `2000-01-01` | Count the changes since the start of this day. A date in the future is an error. |
| `--vcs <type>` | `git` | Version control system. Only `git` is supported. |
| `-r`, `--report-type <type>` | console table | `Json`, `Csv`, `Html`, `Markdown` or `SvgTreemap` in any letter case, or the key of a custom reporter. Requires `--output-file`. |
| `-o`, `--output-file <path>` | | File the report is written to. Requires `--report-type`. |
| `-c`, `--config <path>` | `./cognitive-metrics-settings.json` if it exists | Configuration file. |
| `--coverage-cobertura <path>` | | Adds risk churn, coverage and a risk level per class. |
| `--coverage-clover <path>` | | The same with a Clover file. |
| `-h`, `--help` | | Show the help and exit. |

Example, run in a repository with three commits that changed `OrderService.cs`:

```text
╭───────────────────┬───────┬───────┬───────────────╮
│ Class             │ Score │ Churn │ Times Changed │
├───────────────────┼───────┼───────┼───────────────┤
│ Shop.OrderService │ 2.079 │ 6.237 │             3 │
╰───────────────────┴───────┴───────┴───────────────╯
```

Do not confuse this git churn with the `Churn` column of the analysis reports, which is the score weighted with the missing coverage (called risk churn in the `churn` command).
See [Churn](docs/Churn.md) for the git details, the reports and the risk levels.

### Init

```bash
CognitiveCodeAnalysis init [options]
```

`init` writes a `cognitive-metrics-settings.json` with every setting and the built-in defaults.
In an interactive console it asks for the main settings first.

| Option | Default | Description |
| --- | --- | --- |
| `-p`, `--path <file>` | `./cognitive-metrics-settings.json` | Configuration file to create. Missing directories are created. |
| `--silent` | | Skip the questions and write the defaults. Also implied by a console that is not interactive. |
| `-f`, `--force` | | Overwrite an existing file. Without it an existing file is kept and the command fails. |
| `-h`, `--help` | | Show the help and exit. |

See [Creating a config file](docs/Configuration.md#6-creating-a-config-file).

## Configuration

Settings live in a JSON file with a `cognitive` section.
The tool reads, in this order, the file given with `--config`, then `cognitive-metrics-settings.json` in the working directory, then its built-in defaults.
The file sets the score threshold, the metric weights, which columns the reports show, patterns that exclude files and methods, the analysis cache and custom reporters.

```json
{ "cognitive": { "scoreThreshold": 0.5, "groupByClass": true, "metrics": { "ifCount": { "threshold": 3, "scale": 1.0, "enabled": true } } } }
```

All keys are described in [docs/Configuration.md](docs/Configuration.md).

## Report formats

| Type | Content | Documentation |
| --- | --- | --- |
| `ConsoleText` | Tables per class (default) | [Configuration](docs/Configuration.md) |
| `Html` | Page with a class and file filter | [Configuration](docs/Configuration.md) |
| `Markdown` | Tables per class | [Configuration](docs/Configuration.md) |
| `Csv` | One row per method, every metric | [Configuration](docs/Configuration.md) |
| `Json` | Every metric, also usable as a baseline | [Baseline](docs/Baseline.md) |
| `Sarif` | SARIF 2.1.0 for code scanning | [CI reports](docs/CiReports.md) |
| `GithubActions` | Workflow commands for annotations | [CI reports](docs/CiReports.md) |
| `GitlabCodeQuality` | Code Climate JSON for merge requests | [CI reports](docs/CiReports.md) |
| `Checkstyle` | Checkstyle XML, violations only | [CI reports](docs/CiReports.md) |
| `JUnit` | JUnit XML, one test case per method | [CI reports](docs/CiReports.md) |
| custom key | Your own reporter | [Custom reporters](docs/CustomReporters.md) |

The `churn` command writes `Json`, `Csv`, `Html`, `Markdown` and `SvgTreemap`.

## Continuous integration

[docs/CI.md](docs/CI.md) shows how to run the tool in GitHub Actions (annotations and SARIF upload) and GitLab CI, how to use baselines in a pipeline and how to cache the analysis.
The formats are described in [docs/CiReports.md](docs/CiReports.md).

## Differences to the PHP version

The C# tool follows the PHP implementation in spirit, but it keeps its own contract.
These differences are deliberate:

- **Metric names and semantics stay C#'s own.** The keys in `cognitive-metrics-settings.json` (`ifCount`, `nestingLevels`, ...) are unchanged, and the nesting depth counts loops, `switch`, `try`, `lock`, `using` and the like, not only nested `if` statements.
- **JSON configuration, not YAML.** The file is `cognitive-metrics-settings.json`, there is no `phpcca.yaml`.
- **One default command.** The analysis has no `analyse` keyword, and the report file is `-o|--output-file` (the PHP option is `--report-file`).
  `-r|--report-type` is the same.
- **The analysis cache is off by default** and has no `--clear-cache`; delete the cache directory instead.
- **The `Churn` column of the analysis reports** keeps its name and means the score weighted with the missing coverage.
  The git churn of the PHP tool is the separate `churn` command.
- **Coverage** can be Cobertura (counts or rates, as coverlet writes), Visual Studio XML or Clover.
- **Baselines** match methods by their path relative to the working directory, and `-g` writes them to `.cca/baseline`.
- **Understandability** is the SonarSource cognitive complexity and does not enter the score.
- **Plugins** are .NET assemblies that implement `ICustomCognitiveReporter` or `ICustomChurnReporter`.

## Resources

These pages and papers provide more information on cognitive limitations and readability and the impact on the business.

These pages and papers provide more information on cognitive limitations and readability and the impact on the business.

- **Cognitive Complexity**
  - [Cognitive Complexity Wikipedia](https://en.wikipedia.org/wiki/Cognitive_complexity)
  - [Cognitive Complexity and Its Effect on the Code](https://www.baeldung.com/java-cognitive-complexity) by Emanuel Trandafir.
  - [Human Cognitive Limitations. Broad, Consistent, Clinical Application of Physiological Principles Will Require Decision Support](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC5822395/) by Alan H. Morris.
  - [The Magical Number 4 in Short-Term Memory: A Reconsideration of Mental Storage Capacity](https://www.researchgate.net/publication/11830840_The_Magical_Number_4_in_Short-Term_Memory_A_Reconsideration_of_Mental_Storage_Capacity) by Nelson Cowan
  - [Neural substrates of cognitive capacity limitations](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC3131328/) by Timothy J. Buschman,a,1 Markus Siegel,a,b Jefferson E. Roy, and Earl K. Millera.
  - [Code Readability Testing, an Empirical Study](https://www.researchgate.net/publication/299412540_Code_Readability_Testing_an_Empirical_Study) by Todd Sedano.
  - [An Empirical Validation of Cognitive Complexity as a Measure of Source Code Understandability](https://arxiv.org/pdf/2007.12520) by Marvin Muñoz Barón, Marvin Wyrich, and Stefan Wagner.
- **Halstead Complexity**
  - [Halstead Complexity](https://en.wikipedia.org/wiki/Halstead_complexity_measures)
- **Cyclomatic Complexity**
  - [Cyclomatic Complexity](https://en.wikipedia.org/wiki/Cyclomatic_complexity)


## Contributing

All tools run in the dev container, so only a container runtime and `make` are needed (see the header of the `Makefile`).

```bash
make test       # build and run all tests
make coverage   # tests with coverage, HTML report in artifacts/coverage
make mutation   # mutation testing of the library, report in artifacts/mutation
make ci         # what the CI pipeline runs (restore, build, coverage gate, pack)
```

Quality gates enforced in CI:

- Compiler warnings are errors (`TreatWarningsAsErrors` in `Directory.Build.props`).
- Each test project must reach at least **90% line and 90% branch coverage** of the assembly it tests (Coverlet thresholds in the test projects).
- The library must keep a **mutation score of at least 80%** (Stryker.NET, configured in `stryker-config.json`; high 90, low 80, break 80).
  The mutation job runs on pull requests.
- `make mutation` must run with the .NET 8 SDK (the dev container does); Stryker.NET 4.8 fails with the .NET 10 SDK, so the CI job pins SDK 8 for that step only.
- New behavior needs positive and negative tests.

Releases are made by pushing a tag such as `1.1.0`; the release workflow builds the four archives and the version of the tool is taken from the tag.

## License

Copyright Florian Krämer

Licensed under the [MIT license](LICENSE).
