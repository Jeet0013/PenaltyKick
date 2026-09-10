using System;
using System.Collections.Generic;
using System.Linq;
using CyberGoal.Core.Ai;
using CyberGoal.Core.Keeper;
using CyberGoal.Core.Physics;
using CyberGoal.Core.Util;
using NUnit.Framework;

namespace CyberGoal.Core.Tests
{
    [TestFixture]
    public class GoalkeeperAiTests
    {
        [Test]
        public void NoDifficultyEverPredictsWithCertainty()
        {
            // §15's hard rule, checked at the data rather than in behaviour. A cap
            // of 1.0 would let a player who hit the same corner repeatedly be read
            // with certainty — which, from the player's chair, is indistinguishable
            // from the game reading their input.
            foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)).Cast<Difficulty>())
            {
                DifficultyProfile profile = GoalkeeperAi.Profiles[difficulty];
                Assert.That(profile.PredictionCap, Is.LessThan(1.0),
                    $"{difficulty} may never be certain");
                Assert.That(profile.PredictionCap, Is.GreaterThanOrEqualTo(0));
            }
        }

        [Test]
        public void EvenAgainstATotallyPredictablePlayerTheKeeperSometimesGuessesWrong()
        {
            // Feed CyberLegend — the hardest keeper — a player who has hit the same
            // corner twenty times, then take a hundred decisions. If every one of
            // them dives that way, the keeper is not predicting, it is peeking.
            var memory = new TendencyMemory();
            for (int i = 0; i < 20; i++) memory.Record(-2.8, 0.4);

            var rng = new Rng(4242);
            int wrongWay = 0;
            for (int i = 0; i < 100; i++)
            {
                KeeperCommit commit = GoalkeeperAi.DecideDive(
                    memory, Difficulty.CyberLegend, KeeperTrait.Patient, rng);
                if (!commit.Direction.HasValue || Goalkeeper.Lateral(commit.Direction.Value) != -1)
                {
                    wrongWay++;
                }
            }

            Assert.That(wrongWay, Is.GreaterThan(0),
                "a keeper that is never wrong is a keeper that can see the shot");
        }

        [Test]
        public void ButItDoesLearnSomething()
        {
            // The other side of the same coin: if the cap made no difference, the
            // difficulty levels would be decoration.
            var memory = new TendencyMemory();
            for (int i = 0; i < 20; i++) memory.Record(-2.8, 0.4);

            int LeftDives(Difficulty difficulty, int seed)
            {
                var rng = new Rng(seed);
                int count = 0;
                for (int i = 0; i < 400; i++)
                {
                    KeeperCommit c = GoalkeeperAi.DecideDive(
                        memory, difficulty, KeeperTrait.Patient, rng);
                    if (c.Direction.HasValue && Goalkeeper.Lateral(c.Direction.Value) == -1) count++;
                }
                return count;
            }

            Assert.That(LeftDives(Difficulty.CyberLegend, 7),
                Is.GreaterThan(LeftDives(Difficulty.Easy, 7)),
                "a harder keeper should read the pattern more often");
        }

        [Test]
        public void TendencyMemoryIgnoresASingleKick()
        {
            // Two kicks is a coincidence, five is a habit. Without the confidence
            // ramp the keeper reads a pattern into the very first shot of a match.
            var memory = new TendencyMemory();
            memory.Record(-2.8, 0.4);

            (int _, double strength) = memory.FavouredHorizontal();
            Assert.That(strength, Is.LessThan(0.35),
                "one data point is not a tendency");
        }

        [Test]
        public void AnEmptyMemoryFavoursNothing()
        {
            var memory = new TendencyMemory();
            (int _, double strength) = memory.FavouredHorizontal();
            Assert.That(strength, Is.EqualTo(0));
        }

        [Test]
        public void DecidingIsReproducibleForASeed()
        {
            // Same seed, same match — the property replays and a future server
            // both depend on.
            var memory = new TendencyMemory();
            KeeperCommit First(int seed) => GoalkeeperAi.DecideDive(
                memory, Difficulty.Hard, KeeperTrait.Patient, new Rng(seed));

            KeeperCommit a = First(99);
            KeeperCommit b = First(99);
            Assert.That(a.Direction, Is.EqualTo(b.Direction));
            Assert.That(a.CommittedAt, Is.EqualTo(b.CommittedAt));
            Assert.That(a.LinePosition, Is.EqualTo(b.LinePosition));
        }

        [Test]
        public void TraitsChangeWhenTheKeeperCommits()
        {
            double MeanCommit(KeeperTrait trait)
            {
                var rng = new Rng(1234);
                var memory = new TendencyMemory();
                var times = new List<double>();
                for (int i = 0; i < 300; i++)
                {
                    KeeperCommit c = GoalkeeperAi.DecideDive(memory, Difficulty.Normal, trait, rng);
                    if (c.Direction.HasValue) times.Add(c.CommittedAt);
                }
                return times.Average();
            }

            Assert.That(MeanCommit(KeeperTrait.Aggressive), Is.LessThan(MeanCommit(KeeperTrait.Patient)),
                "aggressive keepers go early, patient ones wait");
        }

        [Test]
        public void AStubbornKeeperStandsUpMoreOften()
        {
            int Stands(KeeperTrait trait)
            {
                var rng = new Rng(555);
                var memory = new TendencyMemory();
                int count = 0;
                for (int i = 0; i < 400; i++)
                {
                    if (!GoalkeeperAi.DecideDive(memory, Difficulty.Normal, trait, rng).Direction.HasValue)
                    {
                        count++;
                    }
                }
                return count;
            }

            Assert.That(Stands(KeeperTrait.Stubborn), Is.GreaterThan(Stands(KeeperTrait.Aggressive)));
        }

        [Test]
        public void TheKeeperNeverStartsBeyondItsLineLimit()
        {
            var rng = new Rng(2024);
            var memory = new TendencyMemory();
            for (int i = 0; i < 500; i++)
            {
                KeeperCommit c = GoalkeeperAi.DecideDive(memory, Difficulty.CyberLegend, KeeperTrait.Aggressive, rng);
                Assert.That(Math.Abs(c.LinePosition), Is.LessThanOrEqualTo(Goalkeeper.Reach.LineLimit));
            }
        }

        [Test]
        public void AiShotsStayOnThePitch()
        {
            var rng = new Rng(8080);
            for (int i = 0; i < 400; i++)
            {
                KickInput shot = GoalkeeperAi.DecideShot(Difficulty.CyberLegend, rng);
                Assert.That(shot.TargetY, Is.GreaterThan(0));
                Assert.That(Math.Abs(shot.TargetX), Is.LessThan(8));
                Assert.That(shot.Power, Is.InRange(0, 100));
                Assert.That(Math.Abs(shot.Curl), Is.LessThanOrEqualTo(1));
            }
        }

        [Test]
        public void HarderAiStrikersScoreMoreOften()
        {
            int Goals(Difficulty difficulty)
            {
                var rng = new Rng(31337);
                int scored = 0;
                for (int i = 0; i < 300; i++)
                {
                    BallState end = BallPhysics.Simulate(GoalkeeperAi.DecideShot(difficulty, rng));
                    if (end.Outcome == Outcome.Goal) scored++;
                }
                return scored;
            }

            Assert.That(Goals(Difficulty.CyberLegend), Is.GreaterThan(Goals(Difficulty.Easy)));
        }
    }
}
