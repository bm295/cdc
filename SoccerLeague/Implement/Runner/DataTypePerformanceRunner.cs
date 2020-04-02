using SoccerLeague.Interface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SoccerLeague.Implement.Runner
{
    public class DataTypePerformanceRunner : IRunner
    {
        public void RunExample()
        {
            var listA = new List<int>();
            var stopWatch = new Stopwatch();
            
            stopWatch.Start();
            for (var i = 0; i < 10000; i++)
            {
                listA.Add(i);
            }
            stopWatch.Stop();
            Console.WriteLine($"Using List<int> costs {stopWatch.ElapsedTicks}");

            stopWatch.Reset();
            var listB = new int[10000];

            stopWatch.Start();
            for (var i = 0; i < 10000; i++)
            {
                listB[i] = i;
            }
            stopWatch.Stop();
            Console.WriteLine($"Using int[] costs {stopWatch.ElapsedTicks}");
        }
    }
}
