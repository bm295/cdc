using System;

namespace UnitTest
{
    public class Team
    {
        public int Score()
        {
            var randomGenerator = new Random();
            return randomGenerator.Next(0, 5);
        }
    }
}