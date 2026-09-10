using System;
using CyberGoal.Core.Physics;
using CyberGoal.Core.Util;

namespace CyberGoal.Core.Keeper
{
    public enum DiveDirection
    {
        LeftHigh,
        LeftLow,
        CentreHigh,
        CentreLow,
        RightHigh,
        RightLow
    }

    public enum ContactPart
    {
        None,
        Hands,
        Body,
        Legs
    }

    /// <summary>What the keeper committed to, and when.</summary>
    public readonly struct KeeperCommit
    {
        /// <summary>Null when they never committed — they stand up instead.</summary>
        public readonly DiveDirection? Direction;

        /// <summary>
        /// Seconds after the whistle that the dive was launched.
        /// </summary>
        /// <remarks>
        /// Not seconds before impact. The keeper does not know when impact will
        /// be — that is the entire problem they are solving, and storing it the
        /// other way round would quietly hand them the answer.
        /// </remarks>
        public readonly double CommittedAt;

        /// <summary>Where along the goal line they stood, in metres from centre.</summary>
        public readonly double LinePosition;

        public KeeperCommit(DiveDirection? direction, double committedAt, double linePosition)
        {
            Direction = direction;
            CommittedAt = committedAt;
            LinePosition = linePosition;
        }
    }

    public readonly struct SaveAttempt
    {
        public readonly bool Saved;
        /// <summary>How close it was, in metres. Drives the near-miss camera and the crowd.</summary>
        public readonly double Margin;
        public readonly ContactPart Contact;

        public SaveAttempt(bool saved, double margin, ContactPart contact)
        {
            Saved = saved;
            Margin = margin;
            Contact = contact;
        }
    }

    /// <summary>
    /// The goalkeeper: one committed dive, and whether it reaches the ball.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The shape of the decision.</b> The keeper picks a direction and a
    /// moment, and the two trade against each other. Dive early and the body is
    /// fully extended when the ball arrives — maximum reach, but the striker can
    /// see it and roll the ball the other way. Dive late and the striker cannot
    /// read it, but the keeper is still extending as the ball crosses, so the
    /// reach is short.
    /// </para>
    /// <para>
    /// That trade is the whole skill of keeping, and it is why reach here is a
    /// function of <em>time since commitment</em> rather than a fixed radius per
    /// direction. §15 forbids impossible saves; this is the model that makes
    /// "impossible" mean something specific rather than being a promise.
    /// </para>
    /// <para>
    /// <b>Why not animate limbs and test colliders.</b> A save is a question
    /// asked once, at the instant the ball crosses the goal plane: is any part of
    /// the keeper within reach of that point? Simulating limbs would make the
    /// answer prettier and non-deterministic. Resolving it with hit volumes at
    /// the crossing moment means a server re-running the same kick and the same
    /// commit gets the same save. The animation still plays — it is driven
    /// <em>from</em> this result, so what the player sees and what the rules
    /// decided cannot disagree.
    /// </para>
    /// </remarks>
    public static class Goalkeeper
    {
        public static class Reach
        {
            /// <summary>How far a fully extended dive reaches from the standing spot.</summary>
            public const double FullReach = 2.45;
            /// <summary>Vertical half-extent of a diving hand's hit volume.</summary>
            public const double HandReach = 0.62;
            /// <summary>Seconds of extension before a dive is at full stretch.</summary>
            public const double ExtendSeconds = 0.42;

            /// <summary>
            /// Reach of a keeper who never committed.
            /// </summary>
            /// <remarks>
            /// Deliberately small. A standing block must beat a shot down the middle
            /// and lose to everything else, or not committing becomes the correct
            /// play and the keeper stops being a decision at all.
            /// </remarks>
            public const double StandingReach = 0.78;
            public const double StandingHeight = 1.95;
            /// <summary>How far the keeper may roam from centre before the whistle.</summary>
            public const double LineLimit = 2.6;
        }

        public static readonly DiveDirection[] AllDives =
        {
            DiveDirection.LeftHigh, DiveDirection.LeftLow,
            DiveDirection.CentreHigh, DiveDirection.CentreLow,
            DiveDirection.RightHigh, DiveDirection.RightLow
        };

        /// <summary>Which way a dive goes, as a sign. Centre is 0.</summary>
        public static int Lateral(DiveDirection direction) => direction switch
        {
            DiveDirection.LeftHigh or DiveDirection.LeftLow => -1,
            DiveDirection.RightHigh or DiveDirection.RightLow => 1,
            _ => 0
        };

        public static bool IsHigh(DiveDirection direction) =>
            direction == DiveDirection.LeftHigh
            || direction == DiveDirection.CentreHigh
            || direction == DiveDirection.RightHigh;

        /// <summary>The height band a dive covers, in metres from the ground.</summary>
        private static (double Low, double High) Band(DiveDirection direction)
            => IsHigh(direction)
                ? (0.95, BallPhysics.Field.GoalHeight + 0.15)
                : (0.0, 1.15);

        /// <summary>
        /// How far through its extension the dive is when the ball arrives.
        /// </summary>
        /// <remarks>
        /// Eased, not linear. A keeper leaves the ground quickly and slows as they
        /// stretch, so a dive committed slightly late still covers most of the
        /// distance. A linear ramp makes late dives uselessly short and pushes
        /// every keeper into diving early, which flattens the mechanic into a
        /// guess made before the striker has done anything.
        /// </remarks>
        public static double ExtensionAt(KeeperCommit commit, double ballArrivalTime)
        {
            if (!commit.Direction.HasValue) return 0;
            double airborne = ballArrivalTime - commit.CommittedAt;
            if (airborne <= 0) return 0;
            double t = Math.Min(1.0, airborne / Reach.ExtendSeconds);
            // Ease-out: fast off the mark, slowing into full stretch.
            return 1 - (1 - t) * (1 - t);
        }

        /// <summary>Does the keeper reach it?</summary>
        /// <param name="crossing">Where the ball crossed the goal plane.</param>
        /// <param name="commit">What the keeper committed to.</param>
        /// <param name="arrivalTime">Seconds from the whistle to that crossing.</param>
        public static SaveAttempt AttemptSave(Vec3 crossing, KeeperCommit commit, double arrivalTime)
        {
            double standing = commit.LinePosition;

            if (!commit.Direction.HasValue)
            {
                // Standing block: a narrow column where the keeper is.
                double dxStand = Math.Abs(crossing.X - standing);
                bool withinHeight = crossing.Y <= Reach.StandingHeight;
                double marginStand = dxStand - Reach.StandingReach;
                bool savedStand = withinHeight && marginStand <= 0;
                return new SaveAttempt(
                    savedStand,
                    marginStand,
                    savedStand ? (crossing.Y < 0.7 ? ContactPart.Legs : ContactPart.Body) : ContactPart.None);
            }

            double extension = ExtensionAt(commit, arrivalTime);
            int side = Lateral(commit.Direction.Value);
            (double low, double high) = Band(commit.Direction.Value);

            // Where the reaching hand is when the ball crosses.
            double handX = standing + side * Reach.FullReach * extension;

            // The covered span runs from the keeper's body out to the hand, because
            // the arm is in between. A shot at the keeper's chest during a dive is
            // still a save, which is why this is a span and not a point.
            double near = Math.Min(standing, handX);
            double far = Math.Max(standing, handX);

            bool withinBand = crossing.Y >= low - 0.1 && crossing.Y <= high;

            double dx;
            if (crossing.X < near) dx = near - crossing.X;
            else if (crossing.X > far) dx = crossing.X - far;
            else dx = 0;

            // The hand itself has a hit volume beyond the span's end, and it grows
            // as the dive extends — a half-extended arm is not a fully open hand.
            double margin = dx - Reach.HandReach * (0.45 + 0.55 * extension);
            bool saved = withinBand && margin <= 0;

            ContactPart contact = ContactPart.None;
            if (saved)
            {
                bool towardHand = Math.Abs(crossing.X - handX) < Reach.HandReach;
                contact = towardHand ? ContactPart.Hands
                    : crossing.Y < 0.7 ? ContactPart.Legs : ContactPart.Body;
            }

            return new SaveAttempt(saved, margin, contact);
        }

        /// <summary>
        /// The dive a given shot would have needed.
        /// </summary>
        /// <remarks>
        /// Used by the AI to reason about tendencies and by a future practice mode
        /// to show what would have worked. Never used to change an outcome after
        /// the fact — §15 forbids the keeper knowing the answer, and this is the
        /// function that would be most tempting to misuse.
        /// </remarks>
        public static DiveDirection DiveThatCovers(Vec3 crossing)
        {
            bool high = crossing.Y >= 1.15;
            if (crossing.X < -0.9) return high ? DiveDirection.LeftHigh : DiveDirection.LeftLow;
            if (crossing.X > 0.9) return high ? DiveDirection.RightHigh : DiveDirection.RightLow;
            return high ? DiveDirection.CentreHigh : DiveDirection.CentreLow;
        }
    }
}
