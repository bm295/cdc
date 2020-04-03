using System.Collections.Generic;

namespace Sandbox.Interface
{
    public interface ITeamDAL
    {
        List<Team> SelectAllTeams();
    }
}
