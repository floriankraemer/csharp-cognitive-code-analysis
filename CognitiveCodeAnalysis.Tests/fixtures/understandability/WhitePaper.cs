namespace Fixtures.Understandability;

/// <summary>
/// The examples of the SonarSource white paper "Cognitive Complexity" by G. Ann Campbell, translated to C#,
/// with the increments the paper gives. Unknown types are fine: the analysis does not need them to resolve.
/// </summary>
public class WhitePaper
{
    int SumOfPrimes(int max)
    {
        int total = 0;
        for (int i = 1; i <= max; ++i) // +1
        {
            for (int j = 2; j < i; ++j) // +2 (nesting = 1)
            {
                if (i % j == 0) // +3 (nesting = 2)
                {
                    goto Next; // +1 (C# has no labeled continue)
                }
            }

            total += i;
        Next:;
        }

        return total;
    } // total 7

    string GetWords(int number)
    {
        switch (number) // +1
        {
            case 1:
                return "one";
            case 2:
                return "a couple";
            case 3:
                return "a few";
            default:
                return "lots";
        }
    } // total 1

    private MethodJavaSymbol OverriddenSymbolFrom(ClassJavaType classType)
    {
        if (classType.IsUnknown()) // +1
        {
            return Symbols.UnknownMethodSymbol;
        }

        bool unknownFound = false;
        List<JavaSymbol> symbols = classType.GetSymbol().Members().Lookup(name);
        foreach (JavaSymbol overrideSymbol in symbols) // +1
        {
            if (overrideSymbol.IsKind(JavaSymbol.MTH) // +2 (nesting = 1)
                && !overrideSymbol.IsStatic()) // +1
            {
                MethodJavaSymbol methodJavaSymbol = (MethodJavaSymbol)overrideSymbol;
                if (CanOverride(methodJavaSymbol)) // +3 (nesting = 2)
                {
                    bool? overriding = CheckOverridingParameters(methodJavaSymbol, classType);
                    if (overriding == null) // +4 (nesting = 3)
                    {
                        if (!unknownFound) // +5 (nesting = 4)
                        {
                            unknownFound = true;
                        }
                    }
                    else if (overriding.Value) // +1
                    {
                        return methodJavaSymbol;
                    }
                }
            }
        }

        if (unknownFound) // +1
        {
            return Symbols.UnknownMethodSymbol;
        }

        return null;
    } // total 19

    private void AddVersion(Entry entry, Transaction txn)
    {
        TransactionIndex ti = _persistit.GetTransactionIndex();
        while (true) // +1
        {
            try
            {
                lock (this)
                {
                    if (frst != null) // +2 (nesting = 1)
                    {
                        if (frst.GetVersion() > entry.GetVersion()) // +3 (nesting = 2)
                        {
                            throw new RollbackException();
                        }

                        if (txn.IsActive()) // +3 (nesting = 2)
                        {
                            for (Entry e = frst; e != null; e = e.GetPrevious()) // +4 (nesting = 3)
                            {
                                long version = e.GetVersion();
                                long depends = ti.WwDependency(version, txn.GetTransactionStatus(), 0);
                                if (depends == TIMED_OUT) // +5 (nesting = 4)
                                {
                                    throw new WWRetryException(version);
                                }

                                if (depends != 0 // +5 (nesting = 4)
                                    && depends != ABORTED) // +1
                                {
                                    throw new RollbackException();
                                }
                            }
                        }
                    }

                    entry.SetPrevious(frst);
                    frst = entry;
                    break;
                }
            }
            catch (WWRetryException re) // +2 (nesting = 1)
            {
                try
                {
                    long depends = _persistit.GetTransactionIndex()
                        .WwDependency(re.GetVersion(), txn.GetTransactionStatus(), SharedResource.DEFAULT_MAX_WAIT_TIME);
                    if (depends != 0 // +3 (nesting = 2)
                        && depends != ABORTED) // +1
                    {
                        throw new RollbackException();
                    }
                }
                catch (InterruptedException ie) // +3 (nesting = 2)
                {
                    throw new PersistitInterruptedException(ie);
                }
            }
            catch (InterruptedException ie) // +2 (nesting = 1)
            {
                throw new PersistitInterruptedException(ie);
            }
        }
    } // total 35

    private static string ToRegexp(string antPattern, string directorySeparator)
    {
        string escapedDirectorySeparator = '\\' + directorySeparator;
        StringBuilder sb = new StringBuilder(antPattern.Length);
        sb.Append('^');
        int i = antPattern.StartsWith("/") // +1
            || antPattern.StartsWith("\\") ? 1 : 0; // +1
        while (i < antPattern.Length) // +1
        {
            char ch = antPattern[i];
            if (SPECIAL_CHARS.IndexOf(ch) != -1) // +2 (nesting = 1)
            {
                sb.Append('\\').Append(ch);
            }
            else if (ch == '*') // +1
            {
                if (i + 1 < antPattern.Length // +3 (nesting = 2)
                    && antPattern[i + 1] == '*') // +1
                {
                    if (i + 2 < antPattern.Length // +4 (nesting = 3)
                        && IsSlash(antPattern[i + 2])) // +1
                    {
                        sb.Append("(?:.*").Append(escapedDirectorySeparator).Append("|)");
                        i += 2;
                    }
                    else // +1
                    {
                        sb.Append(".*");
                        i += 1;
                    }
                }
                else // +1
                {
                    sb.Append("[^").Append(escapedDirectorySeparator).Append("]*?");
                }
            }
            else if (ch == '?') // +1
            {
                sb.Append("[^").Append(escapedDirectorySeparator).Append("]");
            }
            else if (IsSlash(ch)) // +1
            {
                sb.Append(escapedDirectorySeparator);
            }
            else // +1
            {
                sb.Append(ch);
            }

            i++;
        }

        sb.Append('$');
        return sb.ToString();
    } // total 20
}
