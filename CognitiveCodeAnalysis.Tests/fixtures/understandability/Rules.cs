namespace Fixtures.Understandability;

public class Rules
{
    public Rules(int a, int b) : this(a > 0 ? 1 : 0)
    {
        if (b > 0) { }
    }

    void Empty() { }

    void IfOnly(bool a)
    {
        if (a) { }
    }

    void IfElse(bool a)
    {
        if (a) { } else { }
    }

    void ElseIfChainIsNotNestedDeeper(int a)
    {
        if (a > 0)
        {
            if (a > 1) { }
        }
        else if (a < 0)
        {
            if (a < -1) { }
        }
        else if (a == 0)
        {
            if (a == 0) { }
        }
        else
        {
            if (a == 0) { }
        }
    }

    void ElseBodyNests(bool a, bool b)
    {
        if (a) { } else
        {
            while (b) { }
        }
    }

    int Ternary(bool a) => a ? 1 : 0;

    int NestedTernary(bool a, bool b) => a ? (b ? 1 : 2) : 0;

    int TernaryInConditionIsNotNested(bool a, bool b)
    {
        if (a ? b : !b)
        {
            return 1;
        }

        return 0;
    }

    void SwitchCountsOnceNotPerCase(int a)
    {
        switch (a)
        {
            case 1:
            case 2:
                break;
            case 3:
                break;
            default:
                break;
        }
    }

    int SwitchExpressionNestsItsArms(int a, bool b) => a switch
    {
        1 => b ? 1 : 2,
        _ => 0,
    };

    int WhenGuardCountsOnlyItsSequence(int a, bool b, bool c) => a switch
    {
        > 0 when b && c => 1,
        _ => 0,
    };

    void CaseGuardCountsOnlyItsSequence(object o, bool b)
    {
        switch (o)
        {
            case int x when x > 0 || b:
                break;
            case string:
                break;
        }
    }

    bool PatternCombinatorsAreSequences(int a) => a is > 0 and < 10 or 20;

    bool OtherPatternsDoNotCount(object o) => o is not string { Length: > 0 };

    void Loops(List<string> items, Dictionary<string, int> map, bool b)
    {
        for (int i = 0; i < 1; i++) { }
        foreach (string item in items) { }
        foreach (var (key, value) in map) { }
        while (b) { }
        do { } while (b);
    }

    void NestedLoops(List<string> items, bool b)
    {
        foreach (string item in items)
        {
            while (b)
            {
                do { } while (b);
            }
        }
    }

    void DoWhileBodyNestsButConditionDoesNot(bool a, bool b)
    {
        do
        {
            if (a) { }
        } while (a && b);
    }

    void TryAndFinallyDoNotCount(bool b)
    {
        try
        {
            Run();
        }
        catch (InvalidOperationException e) when (e.Data != null && b)
        {
            if (e != null) { }
        }
        catch (Exception)
        {
        }
        finally
        {
            Run();
        }
    }

    void LockUsingCheckedAndUnsafeDoNotNest(bool a, IDisposable resource)
    {
        lock (this)
        {
            using (resource)
            {
                checked
                {
                    unsafe
                    {
                        if (a) { }
                    }
                }
            }
        }
    }

    void LambdaNests(List<string> items)
    {
        items.ForEach(item =>
        {
            if (item.Length == 0) { }
        });
    }

    void LambdaInLambdaNestsTwice(List<List<string>> items)
    {
        items.ForEach(inner => inner.ForEach((string item) =>
        {
            if (item.Length == 0) { }
        }));
    }

    void LambdaInsideIfNests(bool a, List<string> items)
    {
        if (a)
        {
            items.ForEach(item =>
            {
                if (item.Length == 0) { }
            });
        }
    }

    void AnonymousMethodNests(List<string> items)
    {
        items.ForEach(delegate (string item)
        {
            if (item.Length == 0) { }
        });
    }

    void LocalFunctionsNest(List<string> items)
    {
        void Check(string item)
        {
            if (item.Length == 0) { }
        }

        int Twice(int x) => x > 0 ? x : 0;

        items.ForEach(Check);
    }

    bool LikeOperators(bool a, bool b, bool c) => a && b && c;

    bool LikeOrOperators(bool a, bool b, bool c) => a || b || c;

    bool AndThenOr(bool a, bool b, bool c) => a && b || c;

    bool OrThenAnd(bool a, bool b, bool c) => a || b && c;

    bool MixedSequence(bool a, bool b, bool c, bool d, bool e, bool f) => a && b && c || d || e && f;

    bool ParenthesesDoNotBreakASequence(bool a, bool b, bool c) => a && (b && c);

    bool NegationStartsANewSequence(bool a, bool b, bool c) => a && !(b && c);

    bool BitwiseOperatorsDoNotCount(bool a, bool b) => a & b | a ^ b;

    string NullCoalescingDoesNotCount(string? a, string? b, string? c)
    {
        c ??= b;
        return (a ?? b) ?? c?.Trim() ?? "";
    }

    bool SequenceInACallArgumentIsItsOwn(bool a, bool b, bool c) => a && Check(b && c);

    void GotoCounts(int a)
    {
        switch (a)
        {
            case 1:
                goto case 2;
            case 2:
                goto default;
            default:
                goto End;
        }

    End:;
    }

    void BreakAndContinueDoNotCount(int[] cells)
    {
        foreach (int cell in cells)
        {
            if (cell < 0)
            {
                continue;
            }

            if (cell > 9)
            {
                break;
            }
        }
    }

    int RecursionCountsOnce(int n)
    {
        if (n <= 1)
        {
            return 1;
        }

        return RecursionCountsOnce(n - 1) + this.RecursionCountsOnce(n - 2);
    }

    void CallOnOtherObjectIsNoRecursion(Rules other) => other.CallOnOtherObjectIsNoRecursion(this);

    void RecursionInLambda(List<string> items) => items.ForEach(item => RecursionInLambda([]));

    async Task<int> AwaitDoesNotCountButAwaitForeachIsALoop(Task<int> task, IAsyncEnumerable<int> values)
    {
        await foreach (int value in values) { }
        await Task.Yield();
        return await task;
    }

    IEnumerable<int> QueryClausesDoNotCount(int[] values) =>
        from value in values
        where value > 0 && value < 10
        let twice = value * 2
        orderby twice descending
        select twice;

    void HeadersAreMeasuredOnTheLevelOfTheirStatement(int a, bool b, int[] items, string s)
    {
        int i;
        for (i = b ? 1 : 0; i < (b ? 2 : 3); i += b ? 1 : 2) { }
        for (int j = b ? 1 : 0; j < 1; j++) { }
        foreach (int item in b ? items : []) { }
        switch (b && a > 0)
        {
            default:
                break;
        }

        _ = (b || a > 0) switch { true => 1, _ => 0 };
        _ = b ? 0 : (a > 0 ? 1 : 2);
        _ = s is { Length: 1 or 2 } or null;
    }

    void Run() { }

    bool Check(bool a) => a;
}
