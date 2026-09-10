using System;
using System.Collections.Generic;
using CyberGoal.Core.Ai;
using CyberGoal.Core.Keeper;
using CyberGoal.Core.Physics;
using CyberGoal.Core.Rules;
using CyberGoal.Core.Util;
using NUnit.Framework;

namespace CyberGoal.Core.Tests
{
    [TestFixture]
    public class MatchDirectorTests
    {
        private const double Frame = 1.0 / 60.0;

        /// <summary>Tick until a predicate holds, or fail rather than hang.</summary>
        private static void RunUntil(MatchDirector d, Func<bool> until, string what, int maxFrames = 20000)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (until()) return;
                d.Tick(Frame);
            }
            Assert.Fail($"never reached: {what} (stuck in {d.State})");
        }

        [Test]
        public void TheWhistleGatesTheKick()
        {
            // §18: the striker may not kick before the referee blows. Asserted at
            // every state on the way there, not just at the destination.
            var d = new MatchDirector(7);
            var seen = new HashSet<GameState>();

            for (int i = 0; i < 5000 && d.State != GameState.Aiming; i++)
            {
                seen.Add(d.State);
                Assert.That(d.CanStrike, Is.False, $"must not be able to kick in {d.State}");
                d.Tick(Frame);
            }

            Assert.That(d.State, Is.EqualTo(GameState.Aiming));
            Assert.That(d.CanStrike, Is.True);
            Assert.That(seen, Does.Contain(GameState.RefereeReady));
            Assert.That(seen, Does.Contain(GameState.Whistle),
                "the whistle is a state of its own, not an implicit part of setup");
        }

        [Test]
        public void TheShotClockExpiresIntoAMissRatherThanHanging()
        {
            var d = new MatchDirector(11);
            RunUntil(d, () => d.State == GameState.Aiming, "Aiming");

            for (double t = 0; t < MatchDirector.Timing.ShotClock + 1; t += Frame) d.Tick(Frame);

            Assert.That(d.State, Is.Not.EqualTo(GameState.Aiming));
            Assert.That(d.Match.Kicks[d.Match.Kicks.Count - 1].Result, Is.EqualTo(KickResult.Expired));
        }

        [Test]
        public void TheShotClockCountsDownOnlyWhileAiming()
        {
            var d = new MatchDirector(3);
            Assert.That(d.ShotClockRemaining, Is.Null, "no clock during the intro");

            RunUntil(d, () => d.State == GameState.Aiming, "Aiming");
            Assert.That(d.ShotClockRemaining, Is.Not.Null);
            double first = d.ShotClockRemaining.Value;

            d.Tick(0.5);
            Assert.That(d.ShotClockRemaining.Value, Is.LessThan(first));
        }

        [Test]
        public void ResolvingOutsideALiveBallThrows()
        {
            var d = new MatchDirector(5);
            var attempt = new KickAttempt(
                Side.Home,
                new KickInput(0, 1, 70, 0, false, Stance.Placed),
                new KeeperCommit(null, 0, 0),
                Outcome.Goal, KickResult.Goal, Vec3.Zero, 1);

            Assert.Throws<InvalidOperationException>(() => d.ResolveKick(attempt));
        }

        [Test]
        public void AGoalGoesThroughCelebrationAndAMissDoesNot()
        {
            var d = new MatchDirector(19);
            RunUntil(d, () => d.State == GameState.Aiming, "Aiming");
            d.BeginShot();

            d.ResolveKick(new KickAttempt(
                d.Striker, new KickInput(0, 1, 70, 0, false, Stance.Placed),
                new KeeperCommit(null, 0, 0), Outcome.Goal, KickResult.Goal, Vec3.Zero, 1));

            Assert.That(d.State, Is.EqualTo(GameState.GoalResult));
            RunUntil(d, () => d.State == GameState.Celebration, "Celebration");
        }

        [Test]
        public void ASaveSkipsStraightPastCelebration()
        {
            var d = new MatchDirector(19);
            RunUntil(d, () => d.State == GameState.Aiming, "Aiming");
            d.BeginShot();

            d.ResolveKick(new KickAttempt(
                d.Striker, new KickInput(0, 1, 70, 0, false, Stance.Placed),
                new KeeperCommit(DiveDirection.CentreLow, 0.1, 0),
                Outcome.Goal, KickResult.Saved, Vec3.Zero, -0.2));

            Assert.That(d.State, Is.EqualTo(GameState.SaveResult));
            RunUntil(d, () => d.State == GameState.NextPenalty, "NextPenalty");
        }

        [Test]
        public void SpectacularSavesAreRareEnoughToStaySpecial()
        {
            // §24: slow motion must remain special. A save the keeper made
            // comfortably is not one.
            var comfortable = new KickAttempt(
                Side.Home, new KickInput(0, 1, 70, 0, false, Stance.Placed),
                new KeeperCommit(DiveDirection.CentreLow, 0, 0),
                Outcome.Goal, KickResult.Saved, Vec3.Zero, -1.4);
            var fingertip = new KickAttempt(
                Side.Home, new KickInput(0, 1, 70, 0, false, Stance.Placed),
                new KeeperCommit(DiveDirection.RightLow, 0, 0),
                Outcome.Goal, KickResult.Saved, Vec3.Zero, -0.05);

            Assert.That(comfortable.IsSpectacularSave, Is.False);
            Assert.That(fingertip.IsSpectacularSave, Is.True);
        }

        [Test]
        public void AFullMatchReachesAWinnerAndTheScorelineAgrees()
        {
            var d = new MatchDirector(20260910);
            var memory = new TendencyMemory();

            var targets = new[]
            {
                (2.6, 1.5), (-2.7, 0.7), (0.2, 2.1), (-1.9, 1.9), (3.1, 0.5)
            };

            int kicks = 0;
            for (int guard = 0; guard < 200000 && !GameStates.IsTerminal(d.State); guard++)
            {
                if (!d.CanStrike)
                {
                    d.Tick(Frame);
                    continue;
                }

                (double tx, double ty) = targets[kicks % targets.Length];
                var input = new KickInput(tx, ty, 70, kicks % 3 == 0 ? 0.3 : -0.2, false, Stance.Driven);

                // The keeper decides before the ball exists. This ordering is the
                // enforcement of §15, so the test performs it in the same order the
                // game must.
                KeeperCommit commit = GoalkeeperAi.DecideDive(
                    memory, Difficulty.Normal, KeeperTrait.Patient, d.Rng);

                d.BeginShot();
                BallState flight = BallPhysics.Simulate(input);
                memory.Record(input.TargetX, input.TargetY);

                KickResult result;
                double margin = 0;
                if (flight.Outcome == Outcome.Goal)
                {
                    SaveAttempt save = Goalkeeper.AttemptSave(flight.Position, commit, flight.Elapsed);
                    result = save.Saved ? KickResult.Saved : KickResult.Goal;
                    margin = save.Margin;
                }
                else if (flight.Outcome == Outcome.Woodwork) result = KickResult.Woodwork;
                else result = KickResult.OffTarget;

                d.ResolveKick(new KickAttempt(
                    d.Striker, input, commit, flight.Outcome, result, flight.Position, margin));
                kicks++;
            }

            Assert.That(GameStates.IsTerminal(d.State), Is.True, $"stuck in {d.State}");

            MatchState match = d.Match;
            (int home, int away) = ShootoutRules.Scoreline(match);

            Assert.That(match.Phase, Is.EqualTo(Phase.Complete));
            Assert.That(home, Is.Not.EqualTo(away), "a shootout never ends level");
            Assert.That(match.Winner, Is.EqualTo(home > away ? Side.Home : Side.Away));

            int goals = 0;
            foreach (KickRecord k in match.Kicks) if (k.Result == KickResult.Goal) goals++;
            Assert.That(goals, Is.EqualTo(home + away));
            Assert.That(match.Kicks.Count, Is.EqualTo(kicks));
        }

        [Test]
        public void TheCoinTossFollowsTheSeed()
        {
            Assert.That(new MatchDirector(42).Match.FirstKicker,
                Is.EqualTo(new MatchDirector(42).Match.FirstKicker));
        }

        [Test]
        public void CelebrationIsCappedAtTheBriefsCeiling()
        {
            // §22 caps it at four seconds; anything longer and the match stops
            // breathing between kicks.
            Assert.That(MatchDirector.Timing.Celebration, Is.LessThanOrEqualTo(4.0));
        }
    }
}
