using System;
using System.Collections.Generic;
using CyberGoal.Core.Keeper;
using CyberGoal.Core.Physics;
using CyberGoal.Core.Util;

namespace CyberGoal.Core.Ai
{
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
        CyberLegend
    }

    /// <summary>Personality, so two keepers on the same difficulty do not feel identical.</summary>
    public enum KeeperTrait
    {
        /// <summary>Waits, dives late, short reach but hard to read.</summary>
        Patient,
        /// <summary>Commits early. Big reach, and readable.</summary>
        Aggressive,
        /// <summary>Stays up more often than anyone expects.</summary>
        Stubborn
    }

    public readonly struct DifficultyProfile
    {
        /// <summary>Earliest commit, seconds after the whistle.</summary>
        public readonly double MinCommit;
        /// <summary>Latest commit. Later is harder to read but reaches less.</summary>
        public readonly double MaxCommit;
        /// <summary>
        /// How much weight tendency history is allowed to carry, 0-1.
        /// </summary>
        /// <remarks>
        /// Never 1, at any difficulty. §15 forbids the AI knowing the shot before
        /// it is taken, and a cap of 1 would let a player who hit the same corner
        /// three times be read with certainty — indistinguishable, from the
        /// player's chair, from the game cheating.
        /// </remarks>
        public readonly double PredictionCap;
        /// <summary>Chance of standing up instead of diving.</summary>
        public readonly double StandChance;

        public DifficultyProfile(double minCommit, double maxCommit, double predictionCap, double standChance)
        {
            MinCommit = minCommit;
            MaxCommit = maxCommit;
            PredictionCap = predictionCap;
            StandChance = standChance;
        }
    }

    /// <summary>
    /// What the keeper has noticed about where this striker likes to shoot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three horizontal buckets and two vertical, which is the resolution a human
    /// keeper actually works at: "he goes to my left, and he goes low". Finer
    /// buckets would let the AI build a picture no person could hold, and would
    /// take five kicks to populate — by which point the shootout is over.
    /// </para>
    /// <para>
    /// Crucially this records only <em>past, completed</em> kicks. There is no
    /// method here that takes the current shot, which is the structural reason
    /// the AI cannot cheat: it has no way to ask.
    /// </para>
    /// </remarks>
    public sealed class TendencyMemory
    {
        private readonly int[] _horizontal = new int[3];
        private readonly int[] _vertical = new int[2];

        public int Count { get; private set; }

        public void Record(double targetX, double targetY)
        {
            _horizontal[BucketX(targetX)]++;
            _vertical[targetY >= 1.15 ? 1 : 0]++;
            Count++;
        }

        private static int BucketX(double x) => x < -0.9 ? 0 : x > 0.9 ? 2 : 1;

        /// <summary>The favoured horizontal bucket and how strongly, 0-1.</summary>
        public (int Bucket, double Strength) FavouredHorizontal() => Favoured(_horizontal);

        public (int Bucket, double Strength) FavouredVertical() => Favoured(_vertical);

        private (int, double) Favoured(int[] counts)
        {
            if (Count == 0) return (counts.Length / 2, 0);

            int best = 0;
            for (int i = 1; i < counts.Length; i++)
            {
                if (counts[i] > counts[best]) best = i;
            }

            double share = counts[best] / (double)Count;
            double even = 1.0 / counts.Length;
            // How far above chance this bucket sits, normalised so an even spread
            // gives 0 and total commitment to one bucket gives 1.
            double strength = MathHelp.Clamp((share - even) / (1 - even), 0, 1);

            // Confidence grows with sample size. Two kicks is a coincidence; five
            // is a habit. Without this the keeper reads a pattern into the very
            // first shot of a match.
            double confidence = MathHelp.Clamp(Count / 5.0, 0, 1);
            return (best, strength * confidence);
        }
    }

    /// <summary>
    /// The goalkeeper AI (§15).
    /// </summary>
    /// <remarks>
    /// <b>The AI cannot see the shot.</b> Not by policy but by construction:
    /// <see cref="DecideDive"/> takes no argument describing the current kick, and
    /// is called <em>before</em> the ball is launched. There is no code path by
    /// which the answer could reach it. That is the only version of "the AI must
    /// never cheat" that survives someone refactoring in a hurry a year from now.
    /// </remarks>
    public static class GoalkeeperAi
    {
        public static readonly IReadOnlyDictionary<Difficulty, DifficultyProfile> Profiles =
            new Dictionary<Difficulty, DifficultyProfile>
            {
                // Commits early and obviously, and stands up often enough to be
                // beaten by a firm shot anywhere.
                [Difficulty.Easy] = new DifficultyProfile(0.02, 0.16, 0.10, 0.26),
                [Difficulty.Normal] = new DifficultyProfile(0.06, 0.26, 0.32, 0.16),
                [Difficulty.Hard] = new DifficultyProfile(0.10, 0.34, 0.55, 0.10),
                // Reads patterns hard and commits late — but 0.78, never 1.
                [Difficulty.CyberLegend] = new DifficultyProfile(0.14, 0.40, 0.78, 0.07),
            };

        /// <summary>Choose a dive, before the ball is struck.</summary>
        public static KeeperCommit DecideDive(
            TendencyMemory memory, Difficulty difficulty, KeeperTrait trait, Rng rng)
        {
            DifficultyProfile profile = Profiles[difficulty];

            double standChance = profile.StandChance * trait switch
            {
                KeeperTrait.Stubborn => 2.1,
                KeeperTrait.Aggressive => 0.5,
                _ => 1.0
            };

            // Where along the line they set themselves. Small, and never enough to
            // cover a corner on its own.
            double linePosition = (rng.NextDouble() - 0.5) * 0.5;

            if (rng.Chance(MathHelp.Clamp(standChance, 0, 0.6)))
            {
                return new KeeperCommit(null, 0, linePosition);
            }

            (int hBucket, double hStrength) = memory.FavouredHorizontal();
            (int vBucket, double vStrength) = memory.FavouredVertical();

            // Read the tendency only as far as the difficulty allows, then roll.
            bool followsHorizontal = rng.Chance(hStrength * profile.PredictionCap);
            bool followsVertical = rng.Chance(vStrength * profile.PredictionCap);

            int chosenH = followsHorizontal ? hBucket : rng.NextInt(0, 3);
            bool chosenHigh = followsVertical ? vBucket == 1 : rng.Chance(0.42);

            DiveDirection direction = chosenH switch
            {
                0 => chosenHigh ? DiveDirection.LeftHigh : DiveDirection.LeftLow,
                2 => chosenHigh ? DiveDirection.RightHigh : DiveDirection.RightLow,
                _ => chosenHigh ? DiveDirection.CentreHigh : DiveDirection.CentreLow
            };

            double spread = profile.MaxCommit - profile.MinCommit;
            double committedAt = profile.MinCommit + rng.NextDouble() * spread;
            committedAt *= trait switch
            {
                KeeperTrait.Aggressive => 0.6,
                KeeperTrait.Patient => 1.3,
                _ => 1.0
            };

            return new KeeperCommit(direction, committedAt, linePosition);
        }

        /// <summary>
        /// Choose a shot for an AI striker (§5's AI MATCH).
        /// </summary>
        /// <remarks>
        /// Aims at a corner with error scaled by difficulty, and never at a
        /// mathematically unsaveable point — an AI that always finds the top corner
        /// is not hard, it is unplayable.
        /// </remarks>
        public static KickInput DecideShot(Difficulty difficulty, Rng rng)
        {
            double accuracy = difficulty switch
            {
                Difficulty.Easy => 0.55,
                Difficulty.Normal => 0.72,
                Difficulty.Hard => 0.85,
                _ => 0.93
            };

            double half = BallPhysics.Field.GoalWidth / 2;
            // Aim between a third and nine-tenths of the way to a post.
            int side = rng.Chance(0.5) ? -1 : 1;
            double idealX = side * half * (0.34 + rng.NextDouble() * 0.56);
            double idealY = rng.Chance(0.4)
                ? 1.35 + rng.NextDouble() * 0.75
                : 0.25 + rng.NextDouble() * 0.55;

            // Error shrinks with accuracy and is symmetric, so a poor AI misses
            // both ways rather than consistently pulling to one side.
            double error = (1 - accuracy) * 1.9;
            double targetX = idealX + (rng.NextDouble() - 0.5) * 2 * error;
            double targetY = Math.Max(0.16, idealY + (rng.NextDouble() - 0.5) * error);

            Stance stance = rng.NextInt(0, 100) switch
            {
                < 45 => Stance.Driven,
                < 72 => Stance.Placed,
                < 92 => Stance.Finesse,
                _ => Stance.Chip
            };

            return new KickInput(
                targetX, targetY,
                62 + rng.NextDouble() * 34,
                (rng.NextDouble() - 0.5) * 1.4,
                rng.Chance(accuracy * 0.35),
                stance);
        }
    }
}
