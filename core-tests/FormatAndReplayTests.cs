using System.Collections.Generic;
using System.Linq;
using CyberGoal.Core.Ai;
using CyberGoal.Core.Keeper;
using CyberGoal.Core.Physics;
using CyberGoal.Core.Rules;
using CyberGoal.Core.Util;
using NUnit.Framework;

namespace CyberGoal.Core.Tests
{
    [TestFixture]
    public class ShootoutFormatTests
    {
        private static MatchState Play(ShootoutFormat format, params KickResult[] results)
        {
            MatchState state = ShootoutRules.CreateMatch(Side.Home, format);
            foreach (KickResult r in results) state = ShootoutRules.RecordKick(state, r);
            return state;
        }

        [Test]
        public void QuickIsThreeEachAndStandardIsFive()
        {
            Assert.That(Shootout.KicksFor(ShootoutFormat.Quick), Is.EqualTo(3));
            Assert.That(Shootout.KicksFor(ShootoutFormat.Standard), Is.EqualTo(5));
        }

        [Test]
        public void AQuickShootoutEndsAfterThreeEach()
        {
            MatchState state = Play(ShootoutFormat.Quick,
                KickResult.Goal, KickResult.Goal,
                KickResult.Goal, KickResult.Goal,
                KickResult.Goal, KickResult.Saved);

            Assert.That(state.Phase, Is.EqualTo(Phase.Complete));
            Assert.That(state.Winner, Is.EqualTo(Side.Home));
            Assert.That(state.Kicks.Count, Is.EqualTo(6), "three each, not five");
        }

        [Test]
        public void AQuickShootoutClinchesOnItsOwnScale()
        {
            // Home 2, Away 0 with one kick left: Away's ceiling is 1, so it is over
            // after four kicks. The same shape as the standard clinch, and it only
            // works because the rule reads the format instead of a constant.
            MatchState state = Play(ShootoutFormat.Quick,
                KickResult.Goal, KickResult.Saved,
                KickResult.Goal, KickResult.Saved);

            Assert.That(state.Phase, Is.EqualTo(Phase.Complete));
            Assert.That(state.Winner, Is.EqualTo(Side.Home));
        }

        [Test]
        public void AQuickShootoutStillGoesToSuddenDeath()
        {
            MatchState state = Play(ShootoutFormat.Quick,
                KickResult.Goal, KickResult.Goal,
                KickResult.Saved, KickResult.Saved,
                KickResult.Goal, KickResult.Goal);

            Assert.That(state.Phase, Is.EqualTo(Phase.SuddenDeath));
        }

        [Test]
        public void PipsFollowTheFormat()
        {
            MatchState quick = ShootoutRules.CreateMatch(Side.Home, ShootoutFormat.Quick);
            Assert.That(ShootoutRules.PipsFor(quick, Side.Home).Count, Is.EqualTo(3));

            MatchState standard = ShootoutRules.CreateMatch(Side.Home);
            Assert.That(ShootoutRules.PipsFor(standard, Side.Home).Count, Is.EqualTo(5));
        }

        [Test]
        public void TheFormatSurvivesEveryTransition()
        {
            // Threading a value through a chain of immutable copies is exactly the
            // kind of thing that gets dropped in one branch and never noticed.
            MatchState state = Play(ShootoutFormat.Quick,
                KickResult.Goal, KickResult.Goal,
                KickResult.Saved, KickResult.Saved,
                KickResult.Goal, KickResult.Goal);
            Assert.That(state.Format, Is.EqualTo(ShootoutFormat.Quick), "after sudden death");

            state = ShootoutRules.RecordKick(state, KickResult.Goal);
            state = ShootoutRules.RecordKick(state, KickResult.Saved);
            Assert.That(state.Phase, Is.EqualTo(Phase.Complete));
            Assert.That(state.Format, Is.EqualTo(ShootoutFormat.Quick), "after completion");
        }
    }

    [TestFixture]
    public class MatchReplayTests
    {
        /// <summary>Play a match with a fixed script, recording as it goes.</summary>
        private static (MatchRecording Recording, List<KickResult> Live) PlayAndRecord(int seed)
        {
            var director = new MatchDirector(seed);
            var recorder = new MatchRecorder(seed);
            var memory = new TendencyMemory();
            var live = new List<KickResult>();

            var targets = new[] { (2.9, 1.8), (-3.0, 0.6), (0.4, 2.0), (-1.4, 1.3), (3.2, 0.9) };
            int kicks = 0;

            for (int guard = 0; guard < 200000 && !GameStates.IsTerminal(director.State); guard++)
            {
                if (!director.CanStrike)
                {
                    director.Tick(1.0 / 60.0);
                    continue;
                }

                (double tx, double ty) = targets[kicks % targets.Length];
                var input = new KickInput(tx, ty, 74, kicks % 2 == 0 ? 0.5 : -0.35, kicks % 4 == 0,
                    Stance.Driven);

                KeeperCommit commit = GoalkeeperAi.DecideDive(
                    memory, Difficulty.Hard, KeeperTrait.Patient, director.Rng);

                director.BeginShot();
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

                recorder.Record(input, commit);
                live.Add(result);

                director.ResolveKick(new KickAttempt(
                    director.Striker, input, commit, flight.Outcome, result, flight.Position, margin));
                kicks++;
            }

            return (recorder.Build(), live);
        }

        [Test]
        public void AReplayReproducesTheLiveMatchExactly()
        {
            // The keystone test. Everything in this architecture — the analytic
            // flight, the seeded Rng, the fixed step, the doubles in Vec3 — exists
            // to make this pass. If it ever fails, replays are wrong and §40's
            // server validation would reject honest players.
            (MatchRecording recording, List<KickResult> live) = PlayAndRecord(20260910);

            IReadOnlyList<KickAttempt> replayed = MatchReplay.Replay(recording);

            Assert.That(replayed.Count, Is.EqualTo(live.Count));
            for (int i = 0; i < live.Count; i++)
            {
                Assert.That(replayed[i].Result, Is.EqualTo(live[i]), $"kick {i + 1} differs on replay");
            }
        }

        [Test]
        public void AReplayReproducesTheCrossingPointToTheMillimetre()
        {
            (MatchRecording recording, _) = PlayAndRecord(777);

            IReadOnlyList<KickAttempt> first = MatchReplay.Replay(recording);
            IReadOnlyList<KickAttempt> second = MatchReplay.Replay(recording);

            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].Crossing.X, Is.EqualTo(first[i].Crossing.X),
                    "replays must be bit-identical, not merely close");
                Assert.That(second[i].Crossing.Y, Is.EqualTo(first[i].Crossing.Y));
                Assert.That(second[i].SaveMargin, Is.EqualTo(first[i].SaveMargin));
            }
        }

        [Test]
        public void AReplayReachesTheSameWinner()
        {
            (MatchRecording recording, _) = PlayAndRecord(4242);
            MatchState final = MatchReplay.FinalState(recording);

            Assert.That(final.Phase, Is.EqualTo(Phase.Complete));
            Assert.That(final.Winner, Is.Not.Null);
        }

        [Test]
        public void ARecordingStoresNoOutcomes()
        {
            // If an outcome were stored, a replay could agree with the live match by
            // reading it back rather than by re-deriving it — and the validation
            // property §40 depends on would be worthless.
            var allowed = new[] { "Input", "Keeper" };
            string[] actual = typeof(RecordedKick)
                .GetFields(System.Reflection.BindingFlags.Public
                           | System.Reflection.BindingFlags.Instance)
                .Select(f => f.Name)
                .ToArray();

            Assert.That(actual, Is.EquivalentTo(allowed),
                "a recording holds inputs only — outcomes must be re-derived");
        }

        [Test]
        public void ATamperedRecordingProducesADifferentResult()
        {
            // The property that makes server validation possible: change what was
            // claimed to have been done, and the derived outcome changes with it.
            (MatchRecording honest, _) = PlayAndRecord(31337);

            var tampered = new MatchRecording(honest.Seed, honest.Format,
                honest.Kicks.Select(k => new RecordedKick(
                    new KickInput(k.Input.TargetX, k.Input.TargetY, 100, k.Input.Curl,
                        true, k.Input.Stance),
                    // Send the keeper the wrong way, as a cheating client would.
                    new KeeperCommit(DiveDirection.LeftHigh, 5.0, k.Keeper.LinePosition)))
                .ToArray());

            IReadOnlyList<KickAttempt> honestPlay = MatchReplay.Replay(honest);
            IReadOnlyList<KickAttempt> tamperedPlay = MatchReplay.Replay(tampered);

            bool differs = honestPlay.Count != tamperedPlay.Count;
            for (int i = 0; i < honestPlay.Count && !differs; i++)
            {
                if (honestPlay[i].Result != tamperedPlay[i].Result) differs = true;
            }

            Assert.That(differs, Is.True, "tampering must be detectable by re-running");
        }
    }
}
