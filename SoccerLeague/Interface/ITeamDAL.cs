using System.Collections.Generic;

namespace SoccerLeague.Interface
{
    public interface ITeamDAL
    {
        List<Team> SelectAllTeams();
    }
}
