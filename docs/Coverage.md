# Coverage

The `analyse` and `churn` commands can read the coverage of a test run.
The tool reads a report that another tool has written, so it runs no tests itself.

## Supported formats

| Option | Format | Typical producer |
| --- | --- | --- |
| `--coverage-cobertura <file>` | Cobertura XML (with counts or with rates), or Visual Studio coverage XML | coverlet, Cobertura |
| `--coverage-clover <file>` | Clover XML | PHPUnit, OpenClover, Clover |

`--coverage-cobertura` detects the format by the root element of the file, so it reads Cobertura and Visual Studio files.
A Clover file has the same root element as a Cobertura file, so it has to be given with `--coverage-clover`.

Only one of the two options can be given in a run.
Giving both is an error: `Only one coverage format can be specified at a time.`
The command stops with a non-zero exit code.

A missing file is reported as `Coverage file not found: <file>`.
A file that is not well-formed XML, or that is a report of another format, is reported as `Invalid coverage file <file>: <reason>`.

## Clover

* The class figures come from the `<metrics>` of the class: `coveredstatements` of `statements` is the line coverage, and `coveredconditionals` of `conditionals` is the branch coverage.
* A method is a `<line type="method">` entry.
  Its line coverage counts the `stmt` lines up to the next method, and its branch coverage counts the `cond` lines (two branches each).
  A method without statements is as covered as it is called.
* The name of a method is its `name`, or its `signature` without the parameters in OpenClover files.
* The location of a file is its `path`, or its `name` when there is no `path`.
* Classes of the `testproject` of OpenClover are ignored.

## Matching

Coverage is matched to the analysed methods by file, and then by method name and class.
A method matches the method of the report with the same name whose first line lies inside the lines of the analysed method.
A method without a match takes the coverage of its class, and then of its file.

## Where coverage appears

In `analyse`, the console shows a `Line: x% | Branch: y%` column.
The HTML and Markdown reports show the columns `Churn`, `Line Coverage` and `Branch Coverage`, appended after the other columns and only when a coverage file was given.
A method without data shows `n/a`, and a method without branches shows `n/a` for the branch coverage.
The values have one decimal and a percent sign, such as `83.3%`.

In `churn`, the console and the reports show the columns `Risk Churn`, `Coverage` and `Risk Level`, see [Churn](./Churn.md#risk).

## Warnings

When the report knows none or only some of the analysed classes, `analyse` prints a warning before the results:

```text
Loaded 4 coverage entries from <file> but none matched an analysed method. Check that ...
Coverage report <file> matched 2 of 3 analysed classes.
```

A warning of this kind almost always means that the report was produced from other sources or another module.
The analysis still runs, and the methods without a match have no coverage.

## Safety

The XML parser skips a `DOCTYPE`: it loads no DTD and no external entity, and it does not expand entities.
A report cannot make the tool read local files or the network, and an entity bomb is rejected.
Such a file is reported as `Invalid coverage file <file>: <reason>`.
