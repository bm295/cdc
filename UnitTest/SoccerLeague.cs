using SoccerLeague;
using Xunit;

namespace UnitTest
{
    public class SoccerLeague
    {
        [Theory]
        [InlineData(0, 5)]
        public void GoalForEachTeam(int minGoal, int maxGoal)
        {
            var team = new Team();
            var teamGoal = team.Score();
            Assert.IsType<int>(teamGoal);
            Assert.InRange(teamGoal, minGoal, maxGoal);
        }

        [Fact]
        public void TeamInitialized()
        {
            var teams = new TeamBL().GetAllTeams();
            var candidate = new Team() { Id = 1, Name = "Pranaya", Group = "IT" };
            Assert.Contains(candidate, teams);
            Assert.True(candidate.Equals(teams[0]));
            Assert.True(Equals(candidate, teams[0]));
        }
    }
}
