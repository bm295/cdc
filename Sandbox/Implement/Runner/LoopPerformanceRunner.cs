using Domain;
using Sandbox.Interface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Sandbox.Implement.Runner
{
    public class LoopPerformanceRunner : IRunner
    {
        public void RunExample()
        {
            List<int> baseList = new List<int>();
            for (int i = 0; i < Constant.LOOP_COUNT; i++)
            {
                baseList.Add(i);
            }

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            List<int> listA = new List<int>();
            for (var i = 0; i < baseList.Count; i++)
            {
                listA.Add(i);
            }
            stopWatch.Stop();
            Utilities.ShowProfileMessage("for loop (not access the element)", stopWatch.ElapsedTicks);

            stopWatch.Reset();
            stopWatch.Start();
            for (var i = 0; i < baseList.Count; i++)
            {
                listA.Add(baseList[i]);
            }
            stopWatch.Stop();
            Utilities.ShowProfileMessage("for loop (with access the element)", stopWatch.ElapsedTicks);

            stopWatch.Reset();
            stopWatch.Start();
            List<int> listB = new List<int>();
            foreach (var i in baseList)
            {
                listB.Add(i);
            }
            stopWatch.Stop();
            Utilities.ShowProfileMessage("foreach loop", stopWatch.ElapsedTicks);
        }
    }
}
