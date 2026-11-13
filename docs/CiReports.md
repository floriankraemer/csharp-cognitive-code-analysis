# CI reports

The analysis writes five report formats for continuous integration systems.
Use them with `-r <type> -o <file>`, and the type name is matched case-insensitively.

| Type | File format | Read by |
| --- | --- | --- |
| `Sarif` | SARIF 2.1.0 JSON | GitHub code scanning, other SARIF viewers |
| `GitlabCodeQuality` | Code Climate JSON | GitLab merge request widget (`artifacts:reports:codequality`) |
| `GithubActions` | Workflow commands, one per line | GitHub Actions annotations |
| `Checkstyle` | Checkstyle XML | Jenkins warnings and Checkstyle tooling |
| `JUnit` | JUnit XML | Jenkins, GitLab (`artifacts:reports:junit`), test reporter actions on GitHub |

The SARIF, GitLab Code Quality and GitHub Actions formats list every method, and the methods at or below `scoreThreshold` get the lower severity.
The Checkstyle format lists only the violations, which are the methods above `scoreThreshold`.
The JUnit format has one test case per method, and a method above the threshold is a failed one.
Setting `showOnlyMethodsExceedingThreshold` to `true` restricts the SARIF, GitLab, GitHub Actions and JUnit formats to the violations as well.

## Paths

The SARIF, GitLab Code Quality, GitHub Actions and Checkstyle formats name each file relative to the working directory, with `/` separators, when the file lies below it.
Run the analysis from the root of the repository so that the paths match the checkout.
A file outside the working directory keeps its absolute path.

In SARIF, a relative location is a URI reference against `%SRCROOT%`, and `originalUriBaseIds` declares the working directory as that base.
Each path segment is percent-encoded, so a file called `My File #1.cs` becomes `My%20File%20%231.cs`.
An absolute path is written as a `file://` URI of its own, such as `file:///opt/src/A.cs`.

## Severities

| Type | Method above `scoreThreshold` | Method at or below `scoreThreshold` |
| --- | --- | --- |
| `Sarif` | `warning` | `note` |
| `GitlabCodeQuality` | `minor` | `info` |
| `GithubActions` | `warning` | `notice` |
| `Checkstyle` | `warning`, or `error` from twice the threshold | not listed |
| `JUnit` | failed test case | passing test case |

The SARIF, GitLab Code Quality and GitHub Actions severities are the ones these formats had before the paths changed, so a method's severity does not depend on how far above the threshold it is.
The method line is at least 1, so a method reported on line 0 by the analysis appears on line 1.

## GitHub Actions

The runner reads workflow commands from the standard output of a step, so the report file has to be printed.
Write the file in one step and `cat` it in the next:

```yaml
- name: Analyse cognitive complexity
  run: CognitiveCodeAnalysis . -r GithubActions -o cognitive.txt

- name: Annotate the pull request
  run: cat cognitive.txt
```

The annotations appear on the lines of the pull request that the file paths point at.
The message, the title and the file name are escaped as the workflow command syntax requires.

## GitLab CI

Write the report and declare it as a Code Quality artifact:

```yaml
cognitive-complexity:
  stage: test
  script:
    - CognitiveCodeAnalysis . -r GitlabCodeQuality -o gl-code-quality.json
  artifacts:
    reports:
      codequality: gl-code-quality.json
```

The fingerprint of each issue hashes the relative path, the line and the method, so it stays the same when the repository is checked out in another directory.

## GitHub code scanning

Upload the SARIF file with the CodeQL action:

```yaml
- name: Analyse cognitive complexity
  run: CognitiveCodeAnalysis . -r Sarif -o cognitive.sarif

- name: Upload SARIF
  uses: github/codeql-action/upload-sarif@v3
  with:
    sarif_file: cognitive.sarif
```

## Checkstyle and JUnit

Both files are UTF-8 without a byte order mark, and both end with a line feed.
Publish the JUnit file with the test report support of your CI system, and pass the Checkstyle file to a tool that reads Checkstyle XML.
