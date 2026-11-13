# Understandability

Understandability is the cognitive complexity as specified by SonarSource in the white paper ["Cognitive Complexity"](https://www.sonarsource.com/docs/CognitiveComplexity.pdf) by G. Ann Campbell.
It is measured for every analysed method and constructor, next to the other metrics, and does not enter the score.
Where the white paper leaves a C# construct open, the tool follows SonarC#, the C# analyzer of SonarSource.

The score of this tool weights the counts of its metrics, while the understandability adds up the increments of the Sonar specification.
Both tell how hard a method is to read, from two different angles.

## Showing it

Set `showUnderstandability` to `true` in the configuration file, or pass `--show-understandability` for one run.
It is off by default.

When it is on, the console, HTML and Markdown reports get an `Understandability` column as their last column, with the value and its risk level, such as `7 (medium)`.
The CSV report gets the columns `Understandability` and `UnderstandabilityRiskLevel` after every other column.
The JSON report, and therefore a baseline file, always has the fields `understandability` and `understandabilityRiskLevel` for each method.
Baseline files written before these fields existed still load.
The understandability has no baseline delta.

## Rules

- **Structural increments** add 1 plus the current nesting level and nest their body: `if`, the conditional expression `?:`, the `switch` statement and the `switch` expression (once, not per case or arm), `for`, `foreach` (also `await foreach` and deconstructing `foreach`), `while`, `do` and `catch`.
- **Hybrid increments** add 1 without a nesting penalty: `else` and `else if`.
  The body of an `else if` is nested on the level of its `if`, not one level deeper.
- **Lambdas, anonymous methods (`delegate { }`) and local functions** raise the nesting level of their body without an increment.
- **Fundamental increments** add 1 each:
  - every run of like operators in a sequence of `&&` and `||`, so `a && b && c` is 1 and `a && b || c` is 2.
    Parentheses do not break a sequence, while a negation `!(...)` or a method argument starts a new one.
    The pattern combinators `and` and `or` form sequences in the same way, so `x is > 0 and < 10 or 20` is 2.
  - every `goto`, including `goto case` and `goto default`.
  - recursion, once per method however often the method calls itself.
- Conditions, `switch` governing expressions, loop headers and `catch` filters (`when`) stay on the level of their statement; only bodies are nested.
- The `when` guard of a `case` label or a `switch` expression arm is no increment of its own; only the `&&` and `||` sequences in it count.
- These do not count: `try`, `finally`, `lock`, `using`, `fixed`, `checked`, `unsafe`, `break`, `continue`, `return`, `throw`, `await`, the bitwise operators `&`, `|` and `^`, the null-coalescing operators `??` and `??=` and the null-conditional operators `?.` and `?[]` (as in SonarC#), the `not` pattern and all other patterns.
- LINQ query syntax does not count: `from`, `where`, `let`, `orderby`, `join` and `select` clauses add nothing, while the `&&` and `||` sequences inside them count as anywhere else.
  The method syntax (`Where(x => ...)`) nests its lambdas instead, so the two styles can differ by the nesting of conditions inside the lambdas.
- A constructor initializer such as `: this(a > 0 ? 1 : 0)` is measured as part of the constructor.

A call is recursion when it has the name of the method and calls it without a receiver (`Find(n)`, also `Find<T>(n)`), on `this` (`this.Find(n)`) or on the simple name of the type that declares the method (`Order.Find(n)`).
Overloads match by name, so `Find(int)` calling `Find(string)` counts.
A call on `base`, through `?.`, on another type or on any other object is not recursion.
Constructors are never recursive.

## Breakdown and risk level

The metrics model (`CognitiveMetrics.Understandability`) keeps the breakdown: the number of structural, hybrid and fundamental increments, the points added for nesting, and `Recursion` as 1 when the method is recursive, which is also one of the fundamental increments.
The total is the sum of structural, hybrid, fundamental and nesting.

The risk level uses the bands of the cyclomatic complexity:

| Total | Risk level |
| --- | --- |
| up to 5 | `low` |
| 6 to 10 | `medium` |
| 11 to 15 | `high` |
| above 15 | `very_high` |

## Example

The `sumOfPrimes` example of the white paper, in C#, has a total of 7:

```csharp
int SumOfPrimes(int max)
{
    int total = 0;
    for (int i = 1; i <= max; ++i)       // +1
    {
        for (int j = 2; j < i; ++j)      // +2 (nesting = 1)
        {
            if (i % j == 0)              // +3 (nesting = 2)
            {
                goto Next;               // +1
            }
        }

        total += i;
    Next:;
    }

    return total;
}
```

C# has no labeled `continue`, so the `goto` takes the place of the `continue OUT` of the Java original and counts the same.
