using System;

namespace SoccerLeague
{
    public class Team : IEquatable<Team>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }

        public bool Equals(Team other)
        {
            if (other == null)
            {
                return false;
            }

            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            var teamObj = obj as Team;
            if (teamObj == null)
            {
                return false;
            }

            return Equals(teamObj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public int Score()
        {
            var randomGenerator = new Random();
            return randomGenerator.Next(0, 5);
        }
    }
}