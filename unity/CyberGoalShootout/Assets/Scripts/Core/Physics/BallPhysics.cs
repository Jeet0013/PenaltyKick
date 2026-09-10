using System;
using System.Collections.Generic;
using CyberGoal.Core.Util;

namespace CyberGoal.Core.Physics
{
    /// <summary>Where a flight ended, and why.</summary>
    public enum Outcome
    {
        InFlight,
        Goal,
        /// <summary>Crossed the goal plane outside the frame.</summary>
        Wide,
        /// <summary>Hit a post or the bar. Whether it then goes in is decided after.</summary>
        Woodwork,
        Saved,
        /// <summary>Came to rest without ever reaching the goal plane.</summary>
        Short
    }

    /// <summary>How the striker chose to hit it (§10).</summary>
    public enum Stance
    {
        Driven,
        Placed,
        Finesse,
        Chip
    }

    public readonly struct BallState
    {
        public readonly Vec3 Position;
        public readonly Vec3 Velocity;
        /// <summary>Radians per second about the vertical axis — the source of curl.</summary>
        public readonly double Spin;
        public readonly Outcome Outcome;
        /// <summary>Seconds since the strike.</summary>
        public readonly double Elapsed;

        public BallState(Vec3 position, Vec3 velocity, double spin, Outcome outcome, double elapsed)
        {
            Position = position;
            Velocity = velocity;
            Spin = spin;
            Outcome = outcome;
            Elapsed = elapsed;
        }

        public BallState With(Outcome outcome) => new BallState(Position, Velocity, Spin, outcome, Elapsed);
    }

    /// <summary>Everything the striker's input decides. No randomness enters after this.</summary>
    public readonly struct KickInput
    {
        /// <summary>Where in the goal plane they aimed, in metres from goal centre.</summary>
        public readonly double TargetX;
        public readonly double TargetY;
        /// <summary>0-100.</summary>
        public readonly double Power;
        /// <summary>-1 (left) to +1 (right).</summary>
        public readonly double Curl;
        /// <summary>Whether the strike landed in the perfect-contact window (§11).</summary>
        public readonly bool Perfect;
        public readonly Stance Stance;

        public KickInput(double targetX, double targetY, double power, double curl, bool perfect, Stance stance)
        {
            TargetX = targetX;
            TargetY = targetY;
            Power = power;
            Curl = curl;
            Perfect = perfect;
            Stance = stance;
        }
    }

    /// <summary>
    /// Ball flight, as a deterministic fixed-step simulation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is not Unity's PhysX.</b> A penalty is one rigid body on a
    /// known trajectory for about a second, hitting at most a post, a bar, a
    /// keeper or a net. That is small enough to solve exactly, and solving it
    /// exactly buys the one property a general engine cannot promise: the same
    /// kick replays identically, on any device.
    /// </para>
    /// <para>
    /// §40 requires a server to be able to re-run a client's kick, or a goal is
    /// whatever the client says it is. PhysX is excellent and still the wrong
    /// tool here — its results are not guaranteed identical across platforms,
    /// and it cannot run without a Unity player at all, which puts it out of
    /// reach of both the test suite and any future match server.
    /// </para>
    /// <para>
    /// Metres, seconds, kilograms. The penalty spot is the origin, +X is right
    /// from the striker, +Y is up, and −Z is toward the goal.
    /// </para>
    /// </remarks>
    public static class BallPhysics
    {
        public static class Field
        {
            public const double GoalWidth = 7.32;
            public const double GoalHeight = 2.44;
            /// <summary>Penalty spot to goal line. Regulation, per §13.</summary>
            public const double SpotToGoal = 11.0;
            public const double PostRadius = 0.06;
            public const double BallRadius = 0.11;
        }

        public static class Tuning
        {
            /// <summary>Fixed step. Everything is integrated at exactly this rate, always.</summary>
            public const double TimeStep = 1.0 / 120.0;
            public const double Gravity = -9.81;

            /// <summary>
            /// Combined linear drag.
            /// </summary>
            /// <remarks>
            /// A real ball's drag varies with speed and surface. This is a single
            /// linear term because the difference over an 11 m flight is smaller
            /// than a player would notice, and a simpler model replays identically
            /// without anyone having to be careful about it.
            /// </remarks>
            public const double Drag = 0.06;

            /// <summary>
            /// Magnus strength: sideways acceleration per unit spin per unit speed.
            /// </summary>
            /// <remarks>
            /// Deliberately modest. §10 wants curl that is powerful but readable,
            /// and an uncapped Magnus term produces shots that bend so late no
            /// keeper could react — which stops being skill and becomes a lottery.
            /// </remarks>
            public const double Magnus = 0.00042;

            /// <summary>Speed in m/s at full power, before stance adjustment.</summary>
            public const double MaxLaunchSpeed = 31.0;

            public const double WoodworkBounce = 0.55;

            /// <summary>Vertical restitution off the turf.</summary>
            /// <remarks>
            /// A match ball on wet synthetic turf returns a little over half its
            /// drop height. Higher and low shots skip like a stone; lower and a
            /// bobbling ball dies where it lands, which makes every scuffed
            /// penalty look identical.
            /// </remarks>
            public const double GroundBounce = 0.58;

            /// <summary>Horizontal speed retained through a bounce.</summary>
            public const double GroundFriction = 0.78;

            /// <summary>Horizontal drag while rolling, per second.</summary>
            public const double RollingResistance = 1.15;
        }

        public readonly struct StanceProfile
        {
            public readonly double Speed;
            public readonly double CurlScale;
            public readonly double Accuracy;

            public StanceProfile(double speed, double curlScale, double accuracy)
            {
                Speed = speed;
                CurlScale = curlScale;
                Accuracy = accuracy;
            }
        }

        /// <summary>
        /// How each stance shapes the strike. All four are available to every
        /// player — §36 forbids pay-to-win, so these are choices, not unlocks.
        /// </summary>
        public static readonly IReadOnlyDictionary<Stance, StanceProfile> Stances =
            new Dictionary<Stance, StanceProfile>
            {
                // Fast and flat: the keeper has least time, the curl window is smallest.
                [Stance.Driven] = new StanceProfile(1.00, 0.45, 0.82),
                // Slower and truer. The keeper gets longer to read it — that is the trade.
                [Stance.Placed] = new StanceProfile(0.78, 0.70, 1.00),
                // The most bend, and the easiest to push wide.
                [Stance.Finesse] = new StanceProfile(0.85, 1.00, 0.88),
                // High arc. Punishes an early dive; humiliating if underhit.
                [Stance.Chip] = new StanceProfile(0.62, 0.55, 0.90),
            };

        /// <summary>
        /// Turn a kick into an initial ball state.
        /// </summary>
        /// <remarks>
        /// Everything the player chose is consumed here and nothing random is
        /// added. Once this returns, the flight is fully determined — the property
        /// that makes a replay (§26) a replay rather than a re-roll.
        /// </remarks>
        public static BallState Launch(KickInput input)
        {
            StanceProfile stance = Stances[input.Stance];

            double power = MathHelp.Clamp(input.Power, 0, 100) / 100.0;

            // Perfect contact is a bonus, not a gate. §11 is explicit that perfect
            // timing must not guarantee a goal, so it is worth a few percent of
            // pace rather than the whole shot.
            double speed = Tuning.MaxLaunchSpeed * stance.Speed * (0.55 + 0.45 * power)
                           * (input.Perfect ? 1.06 : 1.0);

            double dz = -Field.SpotToGoal;
            double dx = input.TargetX;
            double flightTime = Math.Abs(dz) / Math.Max(speed, 1.0);

            // The vertical velocity that *arrives* at the aimed height under gravity.
            double dy = input.TargetY - Field.BallRadius;
            double vy = (dy - 0.5 * Tuning.Gravity * flightTime * flightTime) / flightTime;

            double horizontal = Math.Sqrt(dx * dx + dz * dz);
            double scale = speed / Math.Max(horizontal, 0.001);

            return new BallState(
                position: new Vec3(0, Field.BallRadius, 0),
                velocity: new Vec3(
                    dx * scale,
                    // Exactly the velocity that reaches targetY — no stance multiplier.
                    //
                    // The web prototype had one, and it broke aiming outright: vy is
                    // *solved* to arrive at targetY, so scaling it afterwards sends the
                    // ball somewhere else. Aiming at 4.44 m put the ball across the line
                    // at 2.21 m — a reticle that lies about where the shot is going.
                    //
                    // The arc still differs by stance, now for the right reason: a chip
                    // flies at 62% speed, so it is airborne far longer, so it needs far
                    // more lift to reach the same point. The high arc falls out of the
                    // physics instead of being pasted on top of it.
                    vy,
                    dz * scale),
                spin: MathHelp.Clamp(input.Curl, -1, 1) * stance.CurlScale * 34.0,
                outcome: Outcome.InFlight,
                elapsed: 0);
        }

        /// <summary>
        /// Advance one fixed step.
        /// </summary>
        /// <remarks>
        /// Never called with a variable delta. A device running at 120 Hz and one
        /// running at 30 Hz must produce the same flight, so the caller accumulates
        /// real time and calls this a whole number of times. This is why
        /// <c>Time.deltaTime</c> never reaches the Core.
        /// </remarks>
        public static BallState Step(BallState ball)
        {
            if (ball.Outcome != Outcome.InFlight) return ball;

            double dt = Tuning.TimeStep;
            Vec3 v = ball.Velocity;
            double speed = v.Magnitude;

            // Magnus: perpendicular to travel, in the horizontal plane. Spin about
            // the vertical axis pushes the ball sideways relative to where it is
            // going, which is why a curled ball bends more the faster it moves.
            double magnus = Tuning.Magnus * ball.Spin * speed;
            double safeSpeed = Math.Max(speed, 0.001);

            double ax = -Tuning.Drag * v.X + magnus * (-v.Z / safeSpeed);
            double ay = Tuning.Gravity - Tuning.Drag * v.Y;
            double az = -Tuning.Drag * v.Z + magnus * (v.X / safeSpeed);

            var next = new BallState(
                position: new Vec3(
                    ball.Position.X + v.X * dt,
                    ball.Position.Y + v.Y * dt,
                    ball.Position.Z + v.Z * dt),
                velocity: new Vec3(v.X + ax * dt, v.Y + ay * dt, v.Z + az * dt),
                spin: ball.Spin,
                outcome: Outcome.InFlight,
                elapsed: ball.Elapsed + dt);

            next = MeetGround(next);

            return Resolve(ball, next);
        }

        /// <summary>
        /// Bounce, or roll, when the ball meets the turf.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Omitting this is not a simplification, it is a bug, and a quiet one: a
        /// ball with no ground to stop it keeps descending, crosses the goal line
        /// below turf level, and is scored <see cref="Outcome.Wide"/> because its
        /// centre sits under the ball-radius floor the goal test uses. A weak
        /// penalty that should trickle over the line was being called a miss, and
        /// nothing about the symptom pointed at the cause.
        /// </para>
        /// <para>
        /// Below a threshold the ball stops bouncing and starts rolling. Without
        /// that, restitution produces infinitely many infinitely small bounces and
        /// the ball never comes to rest, so <see cref="Outcome.Short"/> never
        /// fires and the flight runs to its iteration limit.
        /// </para>
        /// </remarks>
        private static BallState MeetGround(BallState ball)
        {
            double floor = Field.BallRadius;
            if (ball.Position.Y > floor) return ball;

            Vec3 v = ball.Velocity;

            // Slow and low: rolling, not bouncing.
            if (Math.Abs(v.Y) < 0.45)
            {
                double decay = Math.Max(0, 1 - Tuning.RollingResistance * Tuning.TimeStep);
                return new BallState(
                    new Vec3(ball.Position.X, floor, ball.Position.Z),
                    new Vec3(v.X * decay, 0, v.Z * decay),
                    ball.Spin * decay, ball.Outcome, ball.Elapsed);
            }

            return new BallState(
                new Vec3(ball.Position.X, floor, ball.Position.Z),
                new Vec3(
                    v.X * Tuning.GroundFriction,
                    Math.Abs(v.Y) * Tuning.GroundBounce,
                    v.Z * Tuning.GroundFriction),
                // A bounce scrubs spin: the turf grips the ball for the moment it
                // is in contact, which is why a curling shot straightens after it
                // lands.
                ball.Spin * 0.7,
                ball.Outcome, ball.Elapsed);
        }

        /// <summary>
        /// Decide whether this step ended the flight.
        /// </summary>
        /// <remarks>
        /// Takes both the previous and the new state because the goal line is a
        /// plane the ball crosses <em>between</em> steps. Testing only the new
        /// position lets a fast shot tunnel straight through: at 31 m/s the ball
        /// travels 26 cm per step, which is more than four times the width of a
        /// post. Interpolating to the exact crossing is the difference between a
        /// post and a goal.
        /// </remarks>
        private static BallState Resolve(BallState previous, BallState next)
        {
            const double goalZ = -Field.SpotToGoal;

            if (next.Position.Z > goalZ)
            {
                // Rolled to a stop without getting there.
                if (next.Position.Y <= Field.BallRadius && next.Velocity.HorizontalMagnitude < 0.6)
                {
                    return next.With(Outcome.Short);
                }
                return next;
            }

            double span = previous.Position.Z - next.Position.Z;
            double t = span > 0 ? (previous.Position.Z - goalZ) / span : 0;
            double crossX = previous.Position.X + (next.Position.X - previous.Position.X) * t;
            double crossY = previous.Position.Y + (next.Position.Y - previous.Position.Y) * t;

            const double halfWidth = Field.GoalWidth / 2.0;
            const double inner = halfWidth - Field.PostRadius;
            const double under = Field.GoalHeight - Field.PostRadius;
            const double edge = Field.BallRadius;

            var at = new BallState(
                new Vec3(crossX, crossY, goalZ),
                next.Velocity, next.Spin, Outcome.InFlight, next.Elapsed);

            // The ball is a sphere, so its edge decides, not its centre.
            if (Math.Abs(crossX) <= inner - edge && crossY <= under - edge && crossY >= edge)
            {
                return at.With(Outcome.Goal);
            }

            bool hitsPost = Math.Abs(crossX) <= halfWidth + edge && Math.Abs(crossX) >= inner - edge;
            bool hitsBar = crossY <= Field.GoalHeight + edge && crossY >= under - edge;
            if ((hitsPost || hitsBar) && crossY <= Field.GoalHeight + edge)
            {
                return new BallState(
                    at.Position,
                    new Vec3(
                        -next.Velocity.X * Tuning.WoodworkBounce,
                        next.Velocity.Y * Tuning.WoodworkBounce,
                        -next.Velocity.Z * Tuning.WoodworkBounce),
                    next.Spin, Outcome.Woodwork, next.Elapsed);
            }

            return at.With(Outcome.Wide);
        }

        /// <summary>
        /// Run a kick to its conclusion. Bounded so a bug cannot hang a caller.
        /// </summary>
        public static BallState Simulate(KickInput input, double maxSeconds = 4.0)
        {
            BallState ball = Launch(input);
            int limit = (int)Math.Ceiling(maxSeconds / Tuning.TimeStep);
            for (int i = 0; i < limit && ball.Outcome == Outcome.InFlight; i++)
            {
                ball = Step(ball);
            }
            return ball;
        }
    }
}
