# Continuous integration

This page shows how to run the analysis in a pipeline.
The report formats, their paths and their severities are described in [CI reports](CiReports.md).

The tool exits with 0 after a successful run, however many methods exceed `scoreThreshold`.
Findings reach you as annotations, code scanning alerts or merge request widgets, not as a failed job.
A run fails (non-zero exit code) for an error, such as an unknown report type, an unreadable baseline file or no `.cs` file found.

Run the tool from the root of the repository, so that the paths in the reports are relative to the checkout.

## Install the tool in a job

Download the release archive for the platform of the runner and put the executable on the `PATH`.
Pin the version, so a new release cannot change your results.

```bash
CCA_VERSION=1.1.0
curl -sSL "https://github.com/floriankraemer/csharp-cognitive-code-analysis/releases/download/${CCA_VERSION}/CognitiveCodeAnalysis-${CCA_VERSION}-linux-x64.tar.gz" \
  | tar -xz -C /usr/local/bin ./CognitiveCodeAnalysis
CognitiveCodeAnalysis --version
```

The executable is self-contained, so the job needs no .NET SDK.
The release assets are listed in the [README](../README.md#release-binaries).

## GitHub Actions

### Annotations and code scanning

This workflow writes the `GithubActions` report, which becomes annotations on the pull request, and uploads the `Sarif` report to code scanning.

```yaml
name: Cognitive complexity

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read
  security-events: write # upload SARIF

env:
  CCA_VERSION: 1.1.0

jobs:
  analyse:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5

      - name: Install CognitiveCodeAnalysis
        run: |
          curl -sSL "https://github.com/floriankraemer/csharp-cognitive-code-analysis/releases/download/${CCA_VERSION}/CognitiveCodeAnalysis-${CCA_VERSION}-linux-x64.tar.gz" \
            | sudo tar -xz -C /usr/local/bin ./CognitiveCodeAnalysis

      - name: Analyse
        run: |
          CognitiveCodeAnalysis src --cache -r GithubActions -o cognitive.txt
          CognitiveCodeAnalysis src --cache -r Sarif -o cognitive.sarif

      - name: Annotate
        run: cat cognitive.txt

      - name: Upload SARIF
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: cognitive.sarif
```

The runner reads workflow commands from the standard output of a step, so the `GithubActions` file is written in one step and printed with `cat` in the next.
`--cache` lets the second run reuse the metrics of the first, see [Caching](#caching).
Code scanning needs a repository or plan that supports it, and `security-events: write` is only needed for the upload.
Pull requests from forks get a read-only token, so the upload fails there; run it on `push` only if you accept forks.

### A report as an artifact

```yaml
      - name: HTML report
        run: CognitiveCodeAnalysis src -r Html -o cognitive.html

      - uses: actions/upload-artifact@v4
        with:
          name: cognitive-report
          path: cognitive.html
```

## GitLab CI

The `GitlabCodeQuality` report is shown in the merge request widget.
The test job installs the tool from the release archive in the same way.

```yaml
cognitive-complexity:
  stage: test
  variables:
    CCA_VERSION: "1.1.0"
  script:
    - curl -sSL "https://github.com/floriankraemer/csharp-cognitive-code-analysis/releases/download/${CCA_VERSION}/CognitiveCodeAnalysis-${CCA_VERSION}-linux-x64.tar.gz" | tar -xz -C /usr/local/bin ./CognitiveCodeAnalysis
    - CognitiveCodeAnalysis src -r GitlabCodeQuality -o gl-code-quality.json
  artifacts:
    reports:
      codequality: gl-code-quality.json
```

The widget compares the issues of the merge request with the ones of the target branch by their fingerprint.
The fingerprint hashes the relative path, so run the job on the default branch as well, otherwise every issue looks new.

## Baselines in a pipeline

A [baseline](Baseline.md) turns the report into a comparison with an agreed state, so a pull request shows whether it made methods worse.

1. Generate the baseline on your machine, from the root of the repository: `CognitiveCodeAnalysis src -g`.
2. Commit `.cca/baseline/baseline-<timestamp>.json`.
   Its paths are relative, so it works on every checkout.
3. In the pipeline, run the analysis as usual.
   Without `-b`, the newest file in `.cca/baseline` is used and the run prints `Auto-detected latest baseline file: <name>`.
   All reports then carry the deltas.
4. Pass a file explicitly with `-b path/to/baseline.json` when the pipeline should not depend on which file is the newest.
5. After a deliberate improvement or a configuration change, generate a new baseline and commit it.
   When the metric settings differ from the ones stored in the baseline, the run warns that the comparison may not be accurate.

Keep `.cca/baseline` under version control and put `.cca/cache` into `.gitignore`.
The run must not use `-g` in the pipeline unless the new file is committed or stored, because nothing else reads it.

## Caching

The analysis cache (`--cache`, or `cache.enabled` in the configuration) stores the metrics of each file below `.cca/cache`.
It is off by default, see [Analysis cache](Configuration.md#8-analysis-cache).

In a pipeline it pays off when one job runs the analysis several times on the same sources, for example once for each report type, as in the workflow above.
The first run fills the cache, and the others skip the analysis.

Saving the cache directory between pipeline runs helps only when the sources did not change, such as when a job is re-run, because a change to any file invalidates every entry.
Key the saved directory on the sources and the configuration:

```yaml
      - uses: actions/cache@v4
        with:
          path: .cca/cache
          key: cca-${{ runner.os }}-${{ hashFiles('**/*.cs', 'cognitive-metrics-settings.json') }}
```

A cache that is missing, stale or corrupt is not an error: the tool analyses the files again.
