using SoccerLeague.Implement.Runner;
using SoccerLeague.Interface;
using System;
using System.Collections.Generic;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            Dictionary<int, IRunner> runners = new Dictionary<int, IRunner>
            {
                { 1, new DependencyInjectionRunner() },
                { 2, new DataTypePerformanceRunner() }
            };
            foreach (var runner in runners)
            {
                Console.WriteLine($"Option {runner.Key}: {runner.Value.GetType().Name}");
            }

            Console.Write("Enter option: ");
            int input = Convert.ToInt32(Console.ReadLine());
            
            var currentRunner = runners[input];
            Console.WriteLine(currentRunner.GetType().Name);
            currentRunner.RunExample();
            Console.ReadKey();
        }
    }
}
