using System;
using CyberGoal.Core.Util;

namespace CyberGoal.Core.Input
{
    /// <summary>The five zones of §11's timing indicator.</summary>
    public enum TimingZone
    {
        BadEarly,
        GoodEarly,
        Perfect,
        GoodLate,
        BadLate
    }

    public readonly struct TimingResult
    {
        public readonly TimingZone Zone;
        /// <summary>0-1 quality, feeding accuracy and power efficiency.</summary>
        public readonly double Quality;
        /// <summary>Where the marker was, 0-1 across the meter. For the HUD.</summary>
        public readonly double Position;

        public TimingResult(TimingZone zone, double quality, double position)
        {
            Zone = zone;
            Quality = quality;
            Position = position;
        }

        public bool IsPerfect => Zone == TimingZone.Perfect;
    }

    /// <summary>
    /// The optional skill-based timing mechanic (§11).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A marker sweeps the meter; the moment the player begins the shot decides
    /// the zone. §11 is explicit on the two things that matter and are easy to
    /// get wrong:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Perfect must not guarantee a goal.</b> It improves accuracy,
    /// control and power efficiency, and then the keeper still gets their dive.
    /// A mechanic that ends the contest the moment it is executed is not a skill
    /// check, it is a cutscene trigger.</item>
    /// <item><b>It is optional.</b> A player who ignores the meter entirely gets
    /// <see cref="Unused"/> — a clean, average shot. Not a penalty. §11 calls
    /// this optional, and a mechanic you are punished for declining is not.</item>
    /// </list>
    /// <para>
    /// The sweep is a triangle wave rather than a sawtooth, so the marker travels
    /// back through the same zones instead of teleporting from one end to the
    /// other. A discontinuity in the middle of a timing bar is unreadable at
    /// speed and feels like the game cheated.
    /// </para>
    /// </remarks>
    public static class TimingWindow
    {
        public static class Tuning
        {
            /// <summary>Full sweeps per second. Fast enough to matter, slow enough to read.</summary>
            public const double SweepsPerSecond = 0.85;

            /// <summary>Half-width of the perfect band, as a fraction of the meter.</summary>
            public const double PerfectHalfWidth = 0.06;
            /// <summary>Half-width of the surrounding good band.</summary>
            public const double GoodHalfWidth = 0.20;

            /// <summary>Where the perfect band sits. Centre, so it is symmetric.</summary>
            public const double PerfectCentre = 0.5;

            /// <summary>Quality awarded to a player who never engages the meter.</summary>
            public const double NeutralQuality = 0.72;
        }

        /// <summary>Where the marker is at a given time since the meter appeared.</summary>
        /// <remarks>Triangle wave in [0, 1]. Continuous, including at the turns.</remarks>
        public static double MarkerAt(double secondsSinceStart)
        {
            double phase = secondsSinceStart * Tuning.SweepsPerSecond;
            double wrapped = phase - Math.Floor(phase);
            // Up for the first half, back down for the second.
            return wrapped < 0.5 ? wrapped * 2 : (1 - wrapped) * 2;
        }

        /// <summary>Read the meter at the instant the player committed.</summary>
        public static TimingResult Evaluate(double secondsSinceStart)
            => EvaluateAt(MarkerAt(secondsSinceStart));

        /// <summary>Read a marker position directly. Separated so tests need no clock.</summary>
        public static TimingResult EvaluateAt(double marker)
        {
            double position = MathHelp.Clamp(marker, 0, 1);
            double offset = position - Tuning.PerfectCentre;
            double distance = Math.Abs(offset);

            if (distance <= Tuning.PerfectHalfWidth)
            {
                // Quality tapers across the perfect band rather than being flat, so
                // dead centre is meaningfully better than its edge.
                double within = 1 - distance / Tuning.PerfectHalfWidth;
                return new TimingResult(TimingZone.Perfect, 0.94 + 0.06 * within, position);
            }

            if (distance <= Tuning.GoodHalfWidth)
            {
                double t = (distance - Tuning.PerfectHalfWidth)
                           / (Tuning.GoodHalfWidth - Tuning.PerfectHalfWidth);
                return new TimingResult(
                    offset < 0 ? TimingZone.GoodEarly : TimingZone.GoodLate,
                    MathHelp.Lerp(0.9, 0.7, t),
                    position);
            }

            double far = (distance - Tuning.GoodHalfWidth) / (0.5 - Tuning.GoodHalfWidth);
            return new TimingResult(
                offset < 0 ? TimingZone.BadEarly : TimingZone.BadLate,
                MathHelp.Lerp(0.62, 0.4, MathHelp.Clamp(far, 0, 1)),
                position);
        }

        /// <summary>
        /// The result for a player who never touched the meter.
        /// </summary>
        /// <remarks>
        /// Reported as <see cref="TimingZone.GoodEarly"/> rather than a zone of its
        /// own so that no UI has to special-case it, and priced just below a real
        /// "good" so engaging with the mechanic is worth something without
        /// declining it being a punishment.
        /// </remarks>
        public static TimingResult Unused()
            => new TimingResult(TimingZone.GoodEarly, Tuning.NeutralQuality, Tuning.PerfectCentre);

        /// <summary>
        /// Apply timing to a shot's aim.
        /// </summary>
        /// <remarks>
        /// Bad timing does not redirect the shot somewhere arbitrary — it widens
        /// the gap between where the player aimed and where the ball goes, pulling
        /// toward the middle of the goal mouth. That reads as a scuffed contact
        /// rather than as the game overriding the input, and it keeps the failure
        /// legible: you can see you did not get hold of it.
        /// </remarks>
        public static (double X, double Y) ApplyAccuracy(
            double targetX, double targetY, TimingResult timing, double stanceAccuracy)
        {
            double fidelity = MathHelp.Clamp(timing.Quality * stanceAccuracy, 0, 1);
            // At fidelity 1 the shot lands exactly where aimed; at 0.4 it drifts
            // 60% of the way back toward the centre of the goal.
            double pullToCentre = 1 - fidelity;
            return (
                targetX * (1 - pullToCentre * 0.55),
                MathHelp.Lerp(targetY, 1.05, pullToCentre * 0.45));
        }
    }
}
