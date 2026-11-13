namespace Gen;

public class Worker
{
    public async Task<int> LoadAsync(int a)
    {
        await Task.Delay(1);
        if (a > 0)
        {
            return a;
        }

        return 0;
    }

    public IEnumerable<int> Numbers(int n)
    {
        for (int i = 0; i < n; i++)
        {
            yield return i;
        }
    }

    public int Sum(int[] xs, int min)
    {
        var big = xs.Where(x =>
            x > min);
        int Twice(int v)
        {
            return v * 2 + min;
        }

        return big.Sum() + Twice(0);
    }

    public int Plain(int a)
    {
        return a + 1;
    }
}
