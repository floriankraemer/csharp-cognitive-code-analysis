namespace Fixtures.Understandability;

public class Recursion
{
    static int ClassName(int n) => Recursion.ClassName(n - 1);

    int ThisReceiver(int n) => this.ThisReceiver(n - 1);

    int CountsOnce(int n)
    {
        CountsOnce(n);
        this.CountsOnce(n);
        return Recursion.CountsOnce(n);
    }

    int Overload(int n) => Overload("x");

    int Overload(string s) => 0;

    T Generic<T>(T value) => Generic<T>(value);

    public override string ToString() => base.ToString();

    static int OtherClassName(int n) => Other.OtherClassName(n);

    int OtherObject(Recursion other) => other.OtherObject(this);

    int ConditionalAccess(Recursion? other) => other?.ConditionalAccess(null) ?? 0;

    int InsideLocalFunction(int n)
    {
        int Next(int m) => InsideLocalFunction(m - 1);
        return Next(n);
    }

    public Recursion(int n) : this(n, 0)
    {
        Recursion(n);
    }

    class Inner
    {
        int InnerThis(int n) => this.InnerThis(n - 1);

        int InnerClassName(int n) => Inner.InnerClassName(n - 1);

        int OuterClassName(int n) => Recursion.OuterClassName(n - 1);
    }
}
