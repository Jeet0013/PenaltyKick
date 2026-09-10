using CyberGoal.Core.Rules;
using NUnit.Framework;

namespace CyberGoal.Core.Tests
{
    [TestFixture]
    public class ShootoutRulesTests
    {
        private static MatchState Play(params KickResult[] results)
        {
            MatchState state = ShootoutRules.CreateMatch(Side.Home);
            foreach (KickResult r in results)
            {
                state = ShootoutRules.RecordKick(state, r);
            }
            return state;
        }

        [Test]
        public void AlternatesStrictlyFromTheCoinToss()
        {
            MatchState state = ShootoutRules.CreateMatch(Side.Away);
            Assert.That(ShootoutRules.SideToKick(state), Is.EqualTo(Side.Away));

            state = ShootoutRules.RecordKick(state, KickResult.Goal);
            Assert.That(ShootoutRules.SideToKick(state), Is.EqualTo(Side.Home));

            state = ShootoutRules.RecordKick(state, KickResult.Goal);
            Assert.That(ShootoutRules.SideToKick(state), Is.EqualTo(Side.Away));
        }

        [Test]
        public void OnlyGoalsScore()
        {
            // §6: a save, a miss, a post and an expired clock are all worth zero.
            MatchState state = Play(
                KickResult.Goal, KickResult.Saved,
                KickResult.Woodwork, KickResult.OffTarget,
                KickResult.Expired, KickResult.Goal);

            Assert.That(ShootoutRules.ScoreOf(state, Side.Home), Is.EqualTo(1));
            Assert.That(ShootoutRules.ScoreOf(state, Side.Away), Is.EqualTo(1));
        }

        [Test]
        public void ClinchesEarlyWhenTheOpponentCannotCatchUp()
        {
            // §7's worked example, exactly: Home 4, Away 1, Away has two left, so
            // Away's ceiling is 3 and the match is decided on the seventh kick.
            MatchState state = Play(
                KickResult.Goal, KickResult.Goal,    // 1-1
                KickResult.Goal, KickResult.Saved,   // 2-1
                KickResult.Goal, KickResult.Saved);  // 3-1

            Assert.That(state.Phase, Is.Not.EqualTo(Phase.Complete),
                "Away can still reach 3 and Home has 3 — a tie is still possible");

            state = ShootoutRules.RecordKick(state, KickResult.Goal); // 4-1
            Assert.That(state.Phase, Is.EqualTo(Phase.Complete));
            Assert.That(state.Winner, Is.EqualTo(Side.Home));
            Assert.That(ShootoutRules.KicksRemaining(state, Side.Away), Is.EqualTo(0),
                "the match is over, so nothing remains to be taken");
        }

        [Test]
        public void ClinchesTheInstantTheCeilingDrops()
        {
            // A shutout decides sooner: 3-0 with Away down to two kicks means an
            // Away ceiling of 2, and 3 > 2 ends it on the sixth kick, not the
            // seventh. Checked because it is the case a naive "wait for five each"
            // implementation gets wrong without ever failing loudly.
            MatchState state = Play(
                KickResult.Goal, KickResult.Saved,
                KickResult.Goal, KickResult.Saved,
                KickResult.Goal, KickResult.Saved);

            Assert.That(state.Phase, Is.EqualTo(Phase.Complete));
            Assert.That(state.Winner, Is.EqualTo(Side.Home));
            Assert.That(state.Kicks.Count, Is.EqualTo(6));
        }

        [Test]
        public void DoesNotClinchWhenTheOpponentCanStillEqual()
        {
            // The off-by-one that matters. Home 3, Away 1 with 2 remaining: Away's
            // best is 3, which *equals* Home. Equal means sudden death, not a win,
            // so the match must continue.
            MatchState state = Play(
                KickResult.Goal, KickResult.Goal,   // 1-1
                KickResult.Goal, KickResult.Saved,  // 2-1
                KickResult.Goal, KickResult.Saved); // 3-1

            Assert.That(state.Phase, Is.EqualTo(Phase.Standard));
            Assert.That(state.Winner, Is.Null);
        }

        [Test]
        public void GoesToSuddenDeathWhenLevelAfterFiveEach()
        {
            MatchState state = Play(
                KickResult.Goal, KickResult.Goal,
                KickResult.Goal, KickResult.Goal,
                KickResult.Saved, KickResult.Saved,
                KickResult.Goal, KickResult.Goal,
                KickResult.Saved, KickResult.Saved);

            Assert.That(state.Phase, Is.EqualTo(Phase.SuddenDeath));
            Assert.That(state.Winner, Is.Null);
        }

        [Test]
        public void SuddenDeathNeedsACompletedRound()
        {
            MatchState state = Play(
                KickResult.Goal, KickResult.Goal,
                KickResult.Goal, KickResult.Goal,
                KickResult.Goal, KickResult.Goal,
                KickResult.Goal, KickResult.Goal,
                KickResult.Goal, KickResult.Goal);
            Assert.That(state.Phase, Is.EqualTo(Phase.SuddenDeath));

            // Home scores. The match must NOT end here — Away has not replied, and
            // ending now would be a coin toss rather than sudden death.
            state = ShootoutRules.RecordKick(state, KickResult.Goal);
            Assert.That(state.Phase, Is.EqualTo(Phase.SuddenDeath));
            Assert.That(state.Winner, Is.Null);

            // Away misses the reply. Now it is over.
            state = ShootoutRules.RecordKick(state, KickResult.Saved);
            Assert.That(state.Phase, Is.EqualTo(Phase.Complete));
            Assert.That(state.Winner, Is.EqualTo(Side.Home));
        }

        [Test]
        public void SuddenDeathContinuesWhenBothScoreOrBothMiss()
        {
            MatchState state = Play(
                KickResult.Saved, KickResult.Saved,
                KickResult.Saved, KickResult.Saved,
                KickResult.Saved, KickResult.Saved,
                KickResult.Saved, KickResult.Saved,
                KickResult.Saved, KickResult.Saved);
            Assert.That(state.Phase, Is.EqualTo(Phase.SuddenDeath));

            state = ShootoutRules.RecordKick(state, KickResult.Goal);
            state = ShootoutRules.RecordKick(state, KickResult.Goal);
            Assert.That(state.Phase, Is.EqualTo(Phase.SuddenDeath), "both scored — continue");

            state = ShootoutRules.RecordKick(state, KickResult.Saved);
            state = ShootoutRules.RecordKick(state, KickResult.Saved);
            Assert.That(state.Phase, Is.EqualTo(Phase.SuddenDeath), "both missed — continue");
        }

        [Test]
        public void RefusesToRecordAfterTheMatchIsOver()
        {
            MatchState state = Play(
                KickResult.Goal, KickResult.Saved,
                KickResult.Goal, KickResult.Saved,
                KickResult.Goal, KickResult.Saved);

            Assert.That(state.Phase, Is.EqualTo(Phase.Complete));
            Assert.Throws<System.InvalidOperationException>(
                () => ShootoutRules.RecordKick(state, KickResult.Goal));
        }

        [Test]
        public void PipsMatchTheScore()
        {
            MatchState state = Play(KickResult.Goal, KickResult.Saved, KickResult.Woodwork);

            var home = ShootoutRules.PipsFor(state, Side.Home);
            Assert.That(home[0], Is.EqualTo(Pip.Goal));
            Assert.That(home[1], Is.EqualTo(Pip.Miss), "a post is not a goal");
            Assert.That(home[2], Is.EqualTo(Pip.Pending));
            Assert.That(home.Count, Is.EqualTo(Shootout.KicksPerSide));
        }
    }
}
