using System;
using CyberGoal.Core.Physics;

namespace CyberGoal.Core.Rules
{
    /// <summary>How a goal is announced (§23).</summary>
    public enum StrikeGrade
    {
        Standard,
        /// <summary>Into a corner the keeper could not have covered. "PRECISION STRIKE".</summary>
        TopCorner,
        /// <summary>Hit hard enough that pace alone beat the dive. "POWER STRIKE".</summary>
        Power,
        /// <summary>Bent around the keeper. "CYBER CURVE".</summary>
        Curve,
        /// <summary>Timing, direction and power all together. "PERFECT CYBER STRIKE".</summary>
        Perfect
    }

    /// <summary>
    /// Grades a finished kick (§23, §24).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The scarcity is the feature.</b> §23 and §24 both ask for slow motion
    /// and heavy effects on exceptional moments, and §24 says outright that slow
    /// motion must not be overused. A classifier that hands out PERFECT CYBER
    /// STRIKE for a comfortable side-foot has not made the game more exciting, it
    /// has made the label meaningless — and the fastest way to make every goal
    /// feel ordinary is to celebrate all of them identically.
    /// </para>
    /// <para>
    /// So each grade above Standard requires something a player could have
    /// deliberately done and could equally have failed to do, and
    /// <see cref="Grade"/> checks them strongest-first so that a shot which
    /// qualifies for several is announced by its most impressive.
    /// </para>
    /// </remarks>
    public static class StrikeClassifier
    {
        public static class Thresholds
        {
            /// <summary>Within this of a post to count as a corner, in metres.</summary>
            public const double CornerInsetX = 1.05;
            /// <summary>Above this height to count as a top corner, in metres.</summary>
            public const double CornerHeight = 1.55;

            /// <summary>Power above which a shot is graded on pace.</summary>
            public const double PowerFloor = 88.0;

            /// <summary>Absolute curl above which a shot is graded on bend.</summary>
            public const double CurlFloor = 0.62;

            /// <summary>
            /// How far past the keeper's reach a goal must be for "the keeper
            /// never had it", in metres.
            /// </summary>
            public const double UnreachableMargin = 0.35;
        }

        /// <summary>Grade a goal. Anything that is not a goal is <see cref="StrikeGrade.Standard"/>.</summary>
        public static StrikeGrade Grade(KickAttempt attempt)
        {
            if (attempt.Result != KickResult.Goal) return StrikeGrade.Standard;

            bool unreachable = attempt.SaveMargin > Thresholds.UnreachableMargin;

            bool corner =
                Math.Abs(attempt.Crossing.X) >= BallPhysics.Field.GoalWidth / 2 - Thresholds.CornerInsetX
                && attempt.Crossing.Y >= Thresholds.CornerHeight;

            // Perfect is the rarest and needs everything: the timing window, a
            // corner, and a keeper who was never going to reach it. Requiring the
            // keeper to have been beaten cleanly is what stops it firing on a
            // well-struck shot that the keeper simply guessed wrong on.
            if (attempt.Input.Perfect && corner && unreachable) return StrikeGrade.Perfect;

            if (corner && unreachable) return StrikeGrade.TopCorner;

            // Curl before power: if a shot did both, the bend is the more
            // impressive thing to have done on purpose.
            if (Math.Abs(attempt.Input.Curl) >= Thresholds.CurlFloor) return StrikeGrade.Curve;

            if (attempt.Input.Power >= Thresholds.PowerFloor) return StrikeGrade.Power;

            return StrikeGrade.Standard;
        }

        /// <summary>The on-screen label for a grade, per §23.</summary>
        public static string LabelFor(StrikeGrade grade) => grade switch
        {
            StrikeGrade.Perfect => "PERFECT CYBER STRIKE",
            StrikeGrade.TopCorner => "PRECISION STRIKE",
            StrikeGrade.Power => "POWER STRIKE",
            StrikeGrade.Curve => "CYBER CURVE",
            _ => "GOAL"
        };

        /// <summary>
        /// Whether this moment earns slow motion.
        /// </summary>
        /// <remarks>
        /// The single gate for §23 and §24's slow motion, so that "is this
        /// special?" is answered in one place. Only the two rarest goal grades and
        /// a genuine fingertip save qualify — a comfortable stop and a tidy
        /// side-foot both play at normal speed, which is what keeps the effect
        /// worth having.
        /// </remarks>
        public static bool DeservesSlowMotion(KickAttempt attempt)
        {
            if (attempt.IsSpectacularSave) return true;
            StrikeGrade grade = Grade(attempt);
            return grade == StrikeGrade.Perfect || grade == StrikeGrade.TopCorner;
        }
    }
}
