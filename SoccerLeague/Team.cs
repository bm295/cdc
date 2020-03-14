using System;

namespace SoccerLeague
{
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
        public int Score()
        {
            var randomGenerator = new Random();
            return randomGenerator.Next(0, 5);
        }
    }
}