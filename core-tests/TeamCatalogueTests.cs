using System.Linq;
using System.Reflection;
using CyberGoal.Core.Teams;
using NUnit.Framework;

namespace CyberGoal.Core.Tests
{
    [TestFixture]
    public class TeamCatalogueTests
    {
        [Test]
        public void TeamsCarryNoCompetitiveAttributes()
        {
            // §36 forbids pay-to-win. Asserted structurally rather than by reading
            // the class, so that adding "just a small" strength rating fails here
            // rather than quietly shipping.
            var allowed = new[]
            {
                "Id", "Name", "ShortName", "League", "Primary", "Secondary", "Neon"
            };

            string[] actual = typeof(Team)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToArray();

            Assert.That(actual, Is.EquivalentTo(allowed),
                "a team is a name and colours — anything else is a balance lever");
        }

        [Test]
        public void EveryTeamIsCompleteAndUnique()
        {
            Assert.That(TeamCatalogue.All.Count, Is.GreaterThanOrEqualTo(12));

            Assert.That(TeamCatalogue.All.Select(t => t.Id).Distinct().Count(),
                Is.EqualTo(TeamCatalogue.All.Count), "ids must be unique");
            Assert.That(TeamCatalogue.All.Select(t => t.Name).Distinct().Count(),
                Is.EqualTo(TeamCatalogue.All.Count), "names must be unique");

            foreach (Team team in TeamCatalogue.All)
            {
                Assert.That(team.Id, Is.Not.Null.And.Not.Empty);
                Assert.That(team.Name, Is.Not.Null.And.Not.Empty);
                Assert.That(team.ShortName.Length, Is.InRange(2, 4),
                    $"{team.Name}: a short name has to fit a scoreboard");
            }
        }

        [Test]
        public void EveryLeagueIsPopulated()
        {
            foreach (League league in System.Enum.GetValues(typeof(League)).Cast<League>())
            {
                Assert.That(TeamCatalogue.InLeague(league).Count(), Is.GreaterThanOrEqualTo(4),
                    $"{league} needs enough teams for a bracket");
            }
        }

        [Test]
        public void PrimaryAndNeonAreDistinctEnoughToTellApart()
        {
            // The two colours identify a side on a dark pitch. If they are close,
            // the trim vanishes into the shirt and the team reads as one flat mass.
            foreach (Team team in TeamCatalogue.All)
            {
                int distance =
                    System.Math.Abs(team.Primary.R - team.Neon.R) +
                    System.Math.Abs(team.Primary.G - team.Neon.G) +
                    System.Math.Abs(team.Primary.B - team.Neon.B);

                Assert.That(distance, Is.GreaterThan(120),
                    $"{team.Name}: primary and neon are too close to distinguish");
            }
        }

        [Test]
        public void TheDefaultsAreRealTeamsAndAreNotTheSame()
        {
            Assert.That(TeamCatalogue.DefaultHome, Is.Not.Null);
            Assert.That(TeamCatalogue.DefaultAway, Is.Not.Null);
            Assert.That(TeamCatalogue.DefaultHome.Id, Is.Not.EqualTo(TeamCatalogue.DefaultAway.Id));
        }

        [Test]
        public void HexRoundTrips()
        {
            Assert.That(Rgb.FromHex(0x38D6FF).ToHex(), Is.EqualTo(0x38D6FF));
        }
    }
}
