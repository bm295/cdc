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
    }
}
