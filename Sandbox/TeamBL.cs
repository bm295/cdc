using System.Collections.Generic;
using Sandbox.Implement;
using Sandbox.Interface;

namespace Sandbox
{
    public class TeamBL
    {
        public ITeamDAL teamDAL;
        public TeamBL()
        {
            teamDAL = new TeamDAL();
        }
        public TeamBL(ITeamDAL teamDAL)
        {
            this.teamDAL = teamDAL;
        }
        public List<Team> GetAllTeams()
        {
            return teamDAL.SelectAllTeams();
        }
    }
}
