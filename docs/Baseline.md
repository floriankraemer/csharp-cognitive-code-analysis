# Baseline

A baseline is a saved result of `analyse`.
Later runs compare their metrics with it, so you can see whether a change made a method more or less complex.
The baseline is a snapshot of the metrics, not a list of accepted violations.

## Generating a baseline

Add `--generate-baseline` (`-g`) to an `analyse` run:

```powershell
CognitiveCodeAnalysis .\src -g
```

The run prints the analysis as usual and then the path of the new file, such as `Baseline file generated: .cca/baseline/baseline-2026-11-12_21-35-02.json`.
The path is relative to the working directory.

The file is written to `.cca/baseline` below the working directory, and its name carries the UTC time of the run.
An existing file is never replaced: when the name is taken, `-1`, `-2` and so on are appended.
The directory is created when it is missing.
The file is written next to its final name and then moved into place, so no reader sees a partial file.

If a baseline is in use, it is applied first, so the report of that run still shows the deltas, and then the new file is written.
The new file holds the metrics of the current run without any deltas.

## Using a baseline

Without an option, `analyse` uses the newest baseline in `.cca/baseline`, and it says so before the results:

```text
Auto-detected latest baseline file: baseline-2026-11-12_21-35-02.json
```

This happens only when the directory `.cca/baseline` exists in the working directory, so a project without it sees no change.
The newest file is the one with the latest timestamp in its name, and the suffix breaks a tie.
Only regular files named `baseline-<timestamp>[-<n>].json` with a valid date are considered.

To compare with another file, pass it with `--baseline` (`-b`):

```powershell
CognitiveCodeAnalysis .\src -b .\.cca\baseline\baseline-2026-11-12_21-35-02.json
```

A file given with `-b` wins over the auto-detected one.
When `-g` is given and the auto-detected baseline cannot be read, `analyse` warns, ignores it and writes the new baseline, so a broken file can be replaced.
A file given with `-b` that cannot be read always fails the run.
If `.cca/baseline` cannot be listed, the run warns and goes on without a baseline.
It must exist, and it must be a valid baseline.
The JSON report (`-r Json`) writes the same format, so a file from an earlier version of this tool works as a baseline as well.

## How methods are matched

A method is identified by its file, its class and its signature.
The file is the path relative to the working directory, so a baseline also works in another checkout of the project.
A baseline with absolute paths, such as every file written by `-r Json`, matches as long as the working directory is below the same absolute path.

Matching has two passes, and a baseline entry is used for at most one method:

1. Exact: the same relative path, class and signature.
2. Fallbacks, only for methods and baseline entries the first pass left over.
   If more than one candidate exists on either side, the method has no match.
   1. The same class and signature in another file, which follows a moved or renamed file or a baseline from another checkout path.
   2. A class whose name gained exactly one containing type, such as `N.Inner` in the baseline and `N.Shape.Inner` now, in the same file.
      Structs, records and interfaces became part of the name of their nested classes, so older baselines need this step.

A method that is new, or whose signature changed, has no delta.
The coupling of a class is matched by its name.
Only a class that exists on one side alone, under a name with exactly one inserted segment and in the same file, is matched across a rename.

## Deltas

Each weight that differs from the baseline gets a delta, and so does the Cognitive Complexity score.
The reports show them as described in [Configuration](Configuration.md).

## File format

A baseline is a JSON object with `schemaVersion` 1, the same object the JSON report writes:

```json
{
  "schemaVersion": 1,
  "generatedAt": "2026-11-12T21:35:02+00:00",
  "configHash": "…",
  "methods": [ { "filePath": "src/Worker.cs", "className": "Demo.Worker", "methodSignature": "int Run(int a, int b)", "totalScore": 1.2 } ],
  "classCoupling": [ ]
}
```

`configHash` is optional.
It is the SHA-256 of the metric settings (`threshold`, `scale` and `enabled` of every metric, sorted by name), the part of the configuration that changes scores.
A file generated with `-g` has relative paths and a `configHash`.

When a file is read, the fields that are used are validated:

- Text fields must be strings, counts must be non-negative integers and all other numbers must be finite and not negative.
- `methods` and `classCoupling` must be arrays of objects.
- Unknown fields are ignored, and missing fields keep their default.

All problems of one file are listed in a single message, such as `Invalid baseline file format in <path>: methods[0].totalScore must be a finite, non-negative number, methods[2] must be an object`, and `analyse` exits with a failure.
A different `schemaVersion` is reported as unsupported.

## Configuration changes

When the metric settings of the current run differ from the ones stored in the baseline, `analyse` prints a warning:

```text
Warning: Baseline config hash (…) does not match current config hash (…). Metrics comparison may not be accurate.
```

The warning does not stop the run.
A change of a threshold, scale or switch changes the score, so the deltas are then not comparable.
Generate a new baseline after a configuration change.
Baselines without a `configHash`, such as those of the JSON report, never trigger the warning.

## Keeping baselines in version control

A baseline is most useful when the whole team compares against the same snapshot.
Commit the file that describes the state you want to be compared with, and keep `.cca/baseline` out of `.gitignore`.
Because the paths are relative, the file works on every checkout.
