using System.Collections.Generic;
using Sandbox.Interface;

namespace Sandbox.Implement
{
    public class TeamDAL : ITeamDAL
    {
        public List<Team> SelectAllTeams()
        {
            var teams = new List<Team>
            {
                new Team() { Id = 1, Name = "Pranaya", Group = "IT" },
                new Team() { Id = 2, Name = "Kumar", Group = "HR" },
                new Team() { Id = 3, Name = "Rout", Group = "Payroll" }
            };
            return teams;
        }
    }
}
