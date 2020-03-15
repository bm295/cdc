using System.Collections.Generic;
using SoccerLeague.Interface;

namespace SoccerLeague
{
    class TeamBL
    {
        public ITeamDAL teamDAL;
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
