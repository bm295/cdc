using System.Collections.Generic;

namespace SoccerLeague
{
    class TeamBL
    {
        public TeamDAL teamDAL;
        public List<Team> GetAllTeams()
        {
            teamDAL = new TeamDAL();
            return teamDAL.SelectAllTeams();
        }
    }
}
