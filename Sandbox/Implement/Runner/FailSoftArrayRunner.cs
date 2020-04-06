using Domain;
using Sandbox.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sandbox.Implement.Runner
{
    public class FailSoftArrayRunner : IRunner
    {
        public void RunExample()
        {
            FailSoftArray failSoftArray = new FailSoftArray(5);
            var x = 0;
            Console.WriteLine("Fail quietly.");
            for (var i = 0; i < failSoftArray.Length * 2; i++)
            {
                failSoftArray[i] = i * 10;
                x = failSoftArray[i];
                Console.WriteLine($"index = {i}, x = {x}");
            }
            Console.WriteLine("Fail with error report.");
            for (var i = 0; i < failSoftArray.Length * 2; i++)
            {
                failSoftArray[i] = i * 10;
                x = failSoftArray[i];
                if (failSoftArray.ErrFlag)
                {
                    Console.WriteLine($"index {i} is out of range");
                }
                else
                {
                    Console.WriteLine($"index = {i}, x = {x}");
                }
            }
        }
    }
}
