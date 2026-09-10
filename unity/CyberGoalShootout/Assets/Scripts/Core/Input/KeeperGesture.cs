using System;
using CyberGoal.Core.Keeper;
using CyberGoal.Core.Util;

namespace CyberGoal.Core.Input
{
    public readonly struct KeeperGestureResult
    {
        public readonly bool Valid;
        /// <summary>Null means a centre block — the keeper stayed up.</summary>
        public readonly DiveDirection? Direction;
        public readonly string Rejection;

        public KeeperGestureResult(bool valid, DiveDirection? direction, string rejection)
        {
            Valid = valid;
            Direction = direction;
            Rejection = rejection;
        }

        public static KeeperGestureResult Reject(string why)
            => new KeeperGestureResult(false, null, why);
    }

    /// <summary>
    /// The human goalkeeper's controls (§14).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A different problem from the striker's swipe, and it needs a different
    /// solution. §14 notes that keeper reaction time is critical, and the ball is
    /// in the air for well under a second — so this reads the <em>earliest</em>
    /// part of the gesture it can commit to rather than waiting for the finger to
    /// lift. A keeper who has to complete a stroke before the dive begins has
    /// already conceded.
    /// </para>
    /// <para>
    /// It is therefore deliberately coarse. Six directions and a tap, decided from
    /// a short flick. Anything finer would demand precision nobody has in the time
    /// available, and the interesting decision here is <em>when</em> and
    /// <em>where</em>, not how neatly the arc was drawn.
    /// </para>
    /// <para>
    /// The vertical split sits well above the midpoint on purpose: a flick is
    /// mostly horizontal even when the player means "up", because thumbs travel
    /// sideways more easily than they reach. Splitting at 45 degrees would make
    /// high dives nearly impossible to ask for.
    /// </para>
    /// </remarks>
    public static class KeeperGesture
    {
        public static class Tuning
        {
            /// <summary>Below this, the gesture is a tap: stay up and block the middle.</summary>
            public const double TapDistance = 0.035;

            /// <summary>
            /// Vertical component, as a fraction of the gesture, that means "high".
            /// </summary>
            /// <remarks>
            /// 0.42 rather than the 0.71 a 45-degree split would give. A thumb flick
            /// is mostly horizontal even when the player intends up, so an even
            /// split makes high dives almost impossible to request.
            /// </remarks>
            public const double HighFraction = 0.42;

            /// <summary>
            /// Horizontal component below which a dive counts as central.
            /// </summary>
            public const double CentreFraction = 0.30;

            /// <summary>Minimum travel before a direction can be committed to.</summary>
            public const double CommitDistance = 0.05;
        }

        /// <summary>
        /// Read a gesture from its start and its current point.
        /// </summary>
        /// <remarks>
        /// Takes the live point rather than a completed path, so the caller can
        /// poll every frame and commit the instant it becomes decidable.
        /// </remarks>
        public static KeeperGestureResult Read(SwipeSample start, SwipeSample current)
        {
            double dx = current.X - start.X;
            double dy = current.Y - start.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            // A tap in place: stand up and block the middle. Reported as valid with
            // a null direction, which is exactly what KeeperCommit expects.
            if (distance < Tuning.TapDistance)
            {
                return new KeeperGestureResult(true, null, null);
            }

            if (distance < Tuning.CommitDistance)
            {
                return KeeperGestureResult.Reject("not yet decidable");
            }

            double horizontal = Math.Abs(dx) / distance;
            double vertical = dy / distance;

            bool high = vertical >= Tuning.HighFraction;
            bool central = horizontal < Tuning.CentreFraction;

            if (central)
            {
                // Straight up is a high central save; straight down is not a dive
                // anyone means, so it becomes a low central block.
                return new KeeperGestureResult(
                    true,
                    high ? DiveDirection.CentreHigh : DiveDirection.CentreLow,
                    null);
            }

            bool right = dx > 0;
            DiveDirection direction = right
                ? (high ? DiveDirection.RightHigh : DiveDirection.RightLow)
                : (high ? DiveDirection.LeftHigh : DiveDirection.LeftLow);

            return new KeeperGestureResult(true, direction, null);
        }

        /// <summary>
        /// Build the commit the rules take.
        /// </summary>
        /// <param name="direction">From <see cref="Read"/>.</param>
        /// <param name="secondsSinceWhistle">
        /// When the player committed. Fed straight into the reach model, so a human
        /// keeper is bound by exactly the same early-reach/late-disguise trade as
        /// the AI — §14 and §15 describe one mechanic, not two.
        /// </param>
        /// <param name="linePosition">Where along the line they were standing.</param>
        public static KeeperCommit ToCommit(
            DiveDirection? direction, double secondsSinceWhistle, double linePosition)
            => new KeeperCommit(
                direction,
                Math.Max(0, secondsSinceWhistle),
                MathHelp.Clamp(linePosition, -Goalkeeper.Reach.LineLimit, Goalkeeper.Reach.LineLimit));
    }
}
