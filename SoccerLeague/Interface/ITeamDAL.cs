using System.Collections.Generic;

namespace SoccerLeague.Interface
{
    interface ITeamDAL
    {
        List<Team> SelectAllTeams();
    }
}
