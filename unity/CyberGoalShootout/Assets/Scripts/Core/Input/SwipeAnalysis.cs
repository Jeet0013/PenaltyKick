using System;
using System.Collections.Generic;
using CyberGoal.Core.Physics;
using CyberGoal.Core.Util;

namespace CyberGoal.Core.Input
{
    /// <summary>One sampled point of a swipe, in normalised screen space.</summary>
    /// <remarks>
    /// Normalised (0-1 across the screen in both axes) rather than pixels,
    /// because §2 requires the same gesture to mean the same thing on a 4:3
    /// tablet and a 20:9 phone. A swipe measured in pixels is a different swipe
    /// on every device.
    /// </remarks>
    public readonly struct SwipeSample
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Time;

        public SwipeSample(double x, double y, double time)
        {
            X = x;
            Y = y;
            Time = time;
        }
    }

    public readonly struct SwipeResult
    {
        public readonly bool Valid;
        /// <summary>Where in the goal plane this swipe aims, metres from centre.</summary>
        public readonly double TargetX;
        public readonly double TargetY;
        /// <summary>0-100.</summary>
        public readonly double Power;
        /// <summary>-1 (bends left) to +1 (bends right).</summary>
        public readonly double Curl;
        /// <summary>Why it was rejected, for tuning and for tests.</summary>
        public readonly string Rejection;

        public SwipeResult(bool valid, double targetX, double targetY, double power, double curl, string rejection)
        {
            Valid = valid;
            TargetX = targetX;
            TargetY = targetY;
            Power = power;
            Curl = curl;
            Rejection = rejection;
        }

        public static SwipeResult Reject(string why) => new SwipeResult(false, 0, 0, 0, 0, why);
    }

    /// <summary>
    /// Turns a finger path into a shot (§9).
    /// </summary>
    /// <remarks>
    /// <para>
    /// §9 asks for one gesture to carry three things: direction, power and curve.
    /// The mapping has to be guessable in the first thirty seconds and still have
    /// depth after a hundred matches, so each of the three reads a different,
    /// independent property of the path:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Direction</b> — where the swipe <em>ends</em>, not its angle. A
    /// player aiming at the top left corner points at the top left corner. Angle
    /// would be more elegant and is consistently guessed wrong.</item>
    /// <item><b>Power</b> — the speed of the swipe, not its length. Length is
    /// bounded by the screen and by thumb reach, which would make power a
    /// function of hand size. Speed is not.</item>
    /// <item><b>Curve</b> — how far the path bows away from the straight line
    /// between its endpoints, signed. A straight swipe bends nothing; a swipe
    /// that arcs right bends right. This is the part players discover rather
    /// than are told, which is exactly what §9's "easy to learn, hard to master"
    /// asks for.</item>
    /// </list>
    /// <para>
    /// Deliberately not a MonoBehaviour and deliberately not reading Unity's
    /// Input: it is a pure function from samples to a shot, so the whole control
    /// scheme is unit-testable without a device in the room. Every tuning
    /// constant that follows was chosen to be legible here rather than buried in
    /// an inspector field nobody can diff.
    /// </para>
    /// </remarks>
    public static class SwipeAnalysis
    {
        public static class Tuning
        {
            /// <summary>Below this the gesture is a tap, not a swipe.</summary>
            public const double MinDistance = 0.045;
            /// <summary>Longer than this and the player is dragging, not flicking.</summary>
            public const double MaxDuration = 1.2;
            /// <summary>Swipes must travel up the screen, toward the goal.</summary>
            public const double MinUpwardFraction = 0.15;

            /// <summary>Normalised-units-per-second that counts as full power.</summary>
            public const double FullPowerSpeed = 2.6;
            /// <summary>Slowest swipe that still produces a shot worth taking.</summary>
            public const double MinPowerSpeed = 0.35;

            /// <summary>Bow depth, as a fraction of chord length, for full curl.</summary>
            public const double FullCurlBow = 0.22;

            /// <summary>
            /// How far past the posts a swipe may aim.
            /// </summary>
            /// <remarks>
            /// Non-zero on purpose: a player who cannot physically miss is a player
            /// whose aim does not matter. §11 makes the same point about perfect
            /// timing not guaranteeing a goal.
            /// </remarks>
            public const double OvershootMetres = 1.1;
        }

        /// <summary>
        /// Read a completed swipe.
        /// </summary>
        /// <param name="samples">
        /// The path, oldest first, in normalised screen space with Y up.
        /// </param>
        public static SwipeResult Analyse(IReadOnlyList<SwipeSample> samples)
        {
            if (samples == null || samples.Count < 2) return SwipeResult.Reject("too few samples");

            SwipeSample start = samples[0];
            SwipeSample end = samples[samples.Count - 1];

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double duration = end.Time - start.Time;

            if (distance < Tuning.MinDistance) return SwipeResult.Reject("too short");
            if (duration <= 0) return SwipeResult.Reject("zero duration");
            if (duration > Tuning.MaxDuration) return SwipeResult.Reject("too slow — that is a drag");
            if (dy < Tuning.MinUpwardFraction * distance) return SwipeResult.Reject("not toward the goal");

            // ── Power: speed along the path, not the straight-line distance ────
            //
            // Path length, so a player who swipes in a big arc is not punished for
            // the detour: they moved their thumb that far, that fast, and the shot
            // should reflect the effort rather than the displacement.
            double pathLength = 0;
            for (int i = 1; i < samples.Count; i++)
            {
                double sx = samples[i].X - samples[i - 1].X;
                double sy = samples[i].Y - samples[i - 1].Y;
                pathLength += Math.Sqrt(sx * sx + sy * sy);
            }

            double speed = pathLength / duration;
            double powerFraction = MathHelp.Clamp(
                (speed - Tuning.MinPowerSpeed) / (Tuning.FullPowerSpeed - Tuning.MinPowerSpeed),
                0, 1);
            double power = 12 + powerFraction * 88;

            // ── Direction: where the swipe ends ───────────────────────────────
            //
            // The end point maps across the goal mouth. Mapping the *angle*
            // instead reads better on paper and is consistently guessed wrong:
            // players point at the corner they want.
            double halfWidth = BallPhysics.Field.GoalWidth / 2 + Tuning.OvershootMetres;
            double targetX = MathHelp.Clamp((end.X - 0.5) * 2 * halfWidth, -halfWidth, halfWidth);

            // Vertical travel maps to height. A short flick stays low; a long
            // sweep to the top of the screen goes over the bar, which it should.
            double topOfRange = BallPhysics.Field.GoalHeight + Tuning.OvershootMetres;
            double targetY = MathHelp.Clamp(dy * 2.4 * topOfRange, 0.12, topOfRange);

            // ── Curve: signed bow away from the chord ─────────────────────────
            double curl = MathHelp.Clamp(SignedBow(samples) / Tuning.FullCurlBow, -1, 1);

            return new SwipeResult(true, targetX, targetY, power, curl, null);
        }

        /// <summary>
        /// How far the path bows off the straight line between its endpoints, as a
        /// fraction of that line's length. Positive bows right.
        /// </summary>
        /// <remarks>
        /// Uses the signed perpendicular offset of the point that deviates most,
        /// rather than an average. An average cancels an S-shaped path to nearly
        /// zero, and an S-shaped swipe genuinely should not curl — but it also
        /// washes out a clean single arc, which genuinely should. Taking the
        /// extreme keeps the clear case strong and still cancels the S, because
        /// the two lobes of an S have opposite signs and the larger one wins by
        /// only its own margin.
        /// </remarks>
        private static double SignedBow(IReadOnlyList<SwipeSample> samples)
        {
            SwipeSample a = samples[0];
            SwipeSample b = samples[samples.Count - 1];

            double abx = b.X - a.X;
            double aby = b.Y - a.Y;
            double chord = Math.Sqrt(abx * abx + aby * aby);
            if (chord < 1e-6) return 0;

            double most = 0;
            for (int i = 1; i < samples.Count - 1; i++)
            {
                double apx = samples[i].X - a.X;
                double apy = samples[i].Y - a.Y;
                // 2D cross product: positive when the point lies to the right of
                // the travel direction.
                double cross = abx * apy - aby * apx;
                double offset = cross / chord;
                if (Math.Abs(offset) > Math.Abs(most)) most = offset;
            }

            // Negated because a path bowing to the *left* of the direction of
            // travel is what a player reads as "I curled it right" — the ball
            // finishes right of where it was pointed.
            return -most / chord;
        }
    }
}
