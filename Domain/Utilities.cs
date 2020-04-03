using System;
using System.Collections.Generic;
using System.Text;

namespace Domain
{
    public static class Utilities
    {
        public static void ShowProfileMessage(string content, long ticks)
        {
            Console.WriteLine($"{content} costs {ticks:N} ticks");
        }
    }
}
