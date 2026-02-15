using Sandbox.Interface;

namespace Sandbox.Implement.Runner;

public class AsyncNumberToStringRunner : IRunner
{
    public void RunExample()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    private static async Task RunAsync()
    {
        int[] numbers = [1, 2, 3, 4];

        IEnumerable<Task<string>> tasks = numbers.Select(n => Task.Run(() => n.ToString()));
        string[] results = await Task.WhenAll(tasks);

        foreach (string value in results)
        {
            Console.Write(value + " ");
        }

        Console.WriteLine();
    }
}
