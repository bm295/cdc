using System.Collections.Generic;
using SoccerLeague.Implement;
using SoccerLeague.Interface;

namespace SoccerLeague
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
