# Churn

Churn shows where complex code changes often.
A class that is both hard to understand and edited all the time is a likely source of bugs.
The `churn` command finds these hotspots.

## Two meanings of "churn"

The word is used for two different numbers in this tool.

| Name | Where | Formula |
| --- | --- | --- |
| **Churn** (git churn) | The `churn` command | cognitive score of a class × number of git changes of its file |
| **Risk churn** | The `Churn` column of the `analyse` reports, and the `Risk Churn` column of the `churn` command | cognitive score × (1 − coverage) |

The `Churn` column of the `analyse` reports keeps its name for backward compatibility.
It does not use the version history at all.
It weights the score of a method with the part of the code that no test covers.
The `churn` command calls this number risk churn, and it uses the plain word churn for the git-based number.

## How it works

The command calculates the cognitive complexity score of every method, as the `analyse` command does.
The score of a class is the sum of the total scores of its methods.
Then it asks git how often the file of the class changed.
The number of changes is counted once per file, however many classes the file declares.
Merge commits are not counted.

```text
churn = times changed × score
```

The classes are sorted by churn, highest first, and by class name when the churn is equal.
If two files declare a class with the same name, they are merged into one entry with the summed score and the higher number of changes.
Score and churn are rounded to three decimals.

## Usage

```bash
CognitiveCodeAnalysis churn ./src --since 2024-01-01
```

`churn` is a command of its own.
`CognitiveCodeAnalysis <path> [options]` without a command still runs `analyse`, exactly as before.
A directory that is literally named `churn` must therefore be passed as `./churn`.

The console shows the classes with a score and a churn above zero.

| Option | Default | Description |
| --- | --- | --- |
| `[path]` | current directory | Directory that is searched recursively for `*.cs` files. |
| `-s`, `--since <date>` | `2000-01-01` | Count the changes since the start of this day, in the format `yyyy-MM-dd`. |
| `--vcs <type>` | `git` | Version control system. Only `git` is supported. |
| `-r`, `--report-type <type>` | console table | `Json`, `Csv`, `Html`, `Markdown` or `SvgTreemap`, in any letter case. `svg` and `svg-treemap` are aliases of `SvgTreemap`. Requires `--output-file`. |
| `-o`, `--output-file <file>` | none | The file the report is written to. Requires `--report-type`. Missing directories are created. |
| `-c`, `--config <file>` | `./cognitive-metrics-settings.json` if it exists | Configuration file, see [Configuration](./Configuration.md). |
| `--coverage-cobertura <file>` | none | Cobertura XML coverage report, see [Risk](#risk). |
| `-h`, `--help` | | Show the help and exit. |

An invalid date, an unknown VCS, an unknown report type or a report type without an output file (or the other way round) prints a message and exits with a non-zero status.

## Git

The command needs a `git` executable on the `PATH`, and the analysed files must be inside a git repository.
Otherwise it prints `git executable not found` or `Not a git repository: <directory>` and exits with a non-zero status.
Any other failure of git is reported with its exit code and message.

Git is started without a shell, once per source file, with the locale `LC_ALL=C` so that its messages are stable:

```bash
git -C <directory of the file> rev-list "--since=<date> 00:00:00" --no-merges --count HEAD -- <file>
```

`--since` is the start of the given day in the time zone of the machine.
A call that takes longer than 60 seconds is stopped and reported.
The history of the current branch is used and it must be complete, so a shallow clone can report fewer changes than the project has.
A file that git does not track has no changes and therefore no churn.

## Reports

| Type | Content |
| --- | --- |
| `Json` | `createdAt` and a `classes` object with `file`, `score`, `timesChanged`, `churn`, `coverage`, `riskChurn` and `riskLevel` per class. |
| `Csv` | The columns `Class`, `File`, `Score`, `Churn` and `Times Changed`, followed by `Risk Churn`, `Coverage` and `Risk Level` when coverage is given. |
| `Html` | A Bootstrap table of all classes. Class names are escaped. |
| `Markdown` | A table of the classes with a score and a churn above zero. |
| `SvgTreemap` | A treemap of 1200 × 800 pixels. |

All numbers are written with the invariant culture, so a report does not depend on the language of the machine.
Files are written atomically: a temporary file next to the target is moved into place, so a reader never sees a half-written report.

The area of a rectangle in the treemap is proportional to the churn of the class.
Its colour goes from green to red with the score, relative to the lowest and highest score in the report.
The label is cut to 40 characters, the last one being `…`, and the tooltip shows the full class name, the churn and the score.
Classes without churn are not drawn.
The layout is the same slice-and-dice layout as in the PHP implementation, and the SVG is byte-identical to its output for the same data.

## Risk

With `--coverage-cobertura`, the command adds the line coverage of each class, the risk churn and a risk level.

```text
risk churn = times changed × score × (1 − coverage)
```

The coverage is the line coverage of the class in the report, between 0.0 and 1.0.
The risk level is the first rule that matches.

| Level | Churn | Coverage |
| --- | --- | --- |
| `CRITICAL` | above 30 | below 0.5 |
| `HIGH` | above 20 | below 0.7 |
| `MEDIUM` | above 10 | below 0.8 |
| `LOW` | anything else | |

### Classes without coverage data

A class that is absent from the coverage report, or present without any lines, has no coverage.
Its coverage, risk churn and risk level are `null` in JSON, empty in CSV and `N/A` in the other outputs.
It does not count as 0% covered.
The command warns how many classes are missing from the report.
