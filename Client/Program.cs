using Sandbox.Implement.Runner;
using Sandbox.Interface;

namespace Client;

internal static class Program
{
    private static void Main(string[] args)
    {
        Dictionary<int, IRunner> runners = new()
        {
            { 1, new DependencyInjectionRunner() },
            { 2, new DataTypePerformanceRunner() },
            { 3, new LoopPerformanceRunner() },
            { 4, new FailSoftArrayRunner() },
            { 5, new AsyncNumberToStringRunner() }
        };

        foreach ((int option, IRunner runner) in runners)
        {
            Console.WriteLine($"Option {option}: {runner.GetType().Name}");
        }

        Console.Write("Enter option: ");
        string? input = Console.ReadLine();
        if (!int.TryParse(input, out int optionSelected) || !runners.TryGetValue(optionSelected, out IRunner? currentRunner))
        {
            Console.WriteLine("Invalid option.");
            return;
        }

        Console.WriteLine(currentRunner.GetType().Name);
        currentRunner.RunExample();
    }
}
