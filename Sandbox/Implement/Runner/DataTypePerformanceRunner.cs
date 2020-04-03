using Domain;
using Sandbox.Interface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Sandbox.Implement.Runner
{
    public class DataTypePerformanceRunner : IRunner
    {
        public void RunExample()
        {
            var listA = new List<int>();
            var stopWatch = new Stopwatch();
            
            stopWatch.Start();
            for (var i = 0; i < Constant.LOOP_COUNT; i++)
            {
                listA.Add(i);
            }
            stopWatch.Stop();            
            Utilities.ShowProfileMessage("Using List<int>", stopWatch.ElapsedTicks);

            stopWatch.Reset();
            var listB = new int[Constant.LOOP_COUNT];

            stopWatch.Start();
            for (var i = 0; i < Constant.LOOP_COUNT; i++)
            {
                listB[i] = i;
            }
            stopWatch.Stop();
            Utilities.ShowProfileMessage("Using int[]", stopWatch.ElapsedTicks);
        }
    }
}
