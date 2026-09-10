using System;
using System.Collections.Generic;
using CyberGoal.Core.Keeper;
using CyberGoal.Core.Physics;
using CyberGoal.Core.Util;

namespace CyberGoal.Core.Rules
{
    /// <summary>Everything known about one completed attempt.</summary>
    public readonly struct KickAttempt
    {
        public readonly Side Striker;
        public readonly KickInput Input;
        public readonly KeeperCommit Keeper;
        public readonly Outcome Outcome;
        public readonly KickResult Result;
        /// <summary>Where it crossed the goal plane, for the replay camera (§26).</summary>
        public readonly Vec3 Crossing;
        /// <summary>How close the save was, in metres. Negative means saved.</summary>
        public readonly double SaveMargin;

        public KickAttempt(Side striker, KickInput input, KeeperCommit keeper,
            Outcome outcome, KickResult result, Vec3 crossing, double saveMargin)
        {
            Striker = striker;
            Input = input;
            Keeper = keeper;
            Outcome = outcome;
            Result = result;
            Crossing = crossing;
            SaveMargin = saveMargin;
        }

        /// <summary>
        /// A save worth the §24 "CYBER SAVE" treatment: reached, but only just.
        /// </summary>
        /// <remarks>
        /// 25 cm. Wide enough that a genuinely full-stretch save qualifies, tight
        /// enough that a keeper standing where the ball was going does not — §24
        /// says slow motion must stay special, and the fastest way to spend that
        /// is to trigger it on every routine stop.
        /// </remarks>
        public bool IsSpectacularSave => Result == KickResult.Saved && SaveMargin > -0.25;
    }

    /// <summary>
    /// Drives one match through the §49 states.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Holds no UnityEngine reference of any kind, which is what lets a whole
    /// match be played in a unit test in milliseconds — and, later, lets a server
    /// run the authoritative copy.
    /// </para>
    /// <para>
    /// <b>Time only advances waiting states.</b> <see cref="Aiming"/> is never
    /// ended by <see cref="Tick"/> alone; it ends when the striker acts or the
    /// shot clock expires, and both of those are decisions rather than the
    /// passage of time. Anything else would let a dropped frame take someone's
    /// penalty for them.
    /// </para>
    /// </remarks>
    public sealed class MatchDirector
    {
        /// <summary>How long each non-interactive state holds, in seconds.</summary>
        public static class Timing
        {
            public const double MatchIntro = 2.2;
            public const double PrePenalty = 1.2;
            public const double RefereeReady = 0.9;
            public const double Whistle = 0.7;
            /// <summary>The striker's clock (§57).</summary>
            public const double ShotClock = Shootout.ShotClockSeconds;
            /// <summary>Run-up and strike before the ball is live.</summary>
            public const double Shooting = 0.75;
            /// <summary>Camera follows the ball. Ends early when the ball resolves.</summary>
            public const double BallInPlay = 2.5;
            public const double Result = 1.1;
            /// <summary>The celebration ceiling (§22). Never longer.</summary>
            public const double Celebration = 3.4;
            public const double NextPenalty = 0.9;
            public const double MatchEnd = 1.4;
        }

        private readonly List<KickAttempt> _attempts = new List<KickAttempt>();

        public GameState State { get; private set; } = GameState.MatchIntro;
        public MatchState Match { get; private set; }
        public Rng Rng { get; }
        /// <summary>Seconds spent in the current state.</summary>
        public double Elapsed { get; private set; }
        public KickAttempt? LastAttempt { get; private set; }
        public IReadOnlyList<KickAttempt> Attempts => _attempts;

        public event Action<GameState> StateChanged;

        public MatchDirector(int seed, ShootoutFormat format = ShootoutFormat.Standard)
        {
            Seed = seed;
            Rng = new Rng(seed);
            // The coin toss is the first thing the seed decides, so replaying a
            // seed replays the whole match including who went first.
            Match = ShootoutRules.CreateMatch(Rng.Chance(0.5) ? Side.Home : Side.Away, format);
        }

        /// <summary>The seed this match was created with, for replay (§26).</summary>
        public int Seed { get; }

        public Side Striker => ShootoutRules.SideToKick(Match);
        public Side Keeper => Shootout.Other(Striker);
        public bool CanStrike => GameStates.CanStrike(State);

        /// <summary>Seconds left on the striker's clock, or null when it is not running.</summary>
        public double? ShotClockRemaining
            => State == GameState.Aiming ? Math.Max(0, Timing.ShotClock - Elapsed) : (double?)null;

        /// <summary>Feed real time in.</summary>
        public void Tick(double deltaSeconds)
        {
            Elapsed += deltaSeconds;

            double? hold = HoldFor(State);
            if (hold.HasValue && Elapsed >= hold.Value) Advance();
        }

        /// <summary>
        /// Move to the next state. The only transition function.
        /// </summary>
        /// <remarks>
        /// Deliberately explicit rather than something that happens implicitly when
        /// a timer fires: §40 requires that only the server may advance state
        /// online, and that is only enforceable if advancing is a single named
        /// operation.
        /// </remarks>
        public void Advance()
        {
            switch (State)
            {
                case GameState.MatchIntro:
                case GameState.NextPenalty:
                case GameState.SuddenDeath:
                    Enter(GameState.PrePenalty);
                    break;

                case GameState.PrePenalty:
                    Enter(GameState.RefereeReady);
                    break;

                case GameState.RefereeReady:
                    Enter(GameState.Whistle);
                    break;

                case GameState.Whistle:
                    Enter(GameState.Aiming);
                    break;

                case GameState.Aiming:
                    // Time alone cannot end a live kick; only a shot or an expiry.
                    ExpireShotClock();
                    break;

                case GameState.Shooting:
                    Enter(GameState.BallInPlay);
                    break;

                case GameState.BallInPlay:
                    // The flight should have resolved by now. If it has not, treat
                    // it as off target rather than hanging the match — a stuck ball
                    // must never be able to stop a shootout.
                    if (LastAttempt == null) ResolveExpiry(KickResult.OffTarget);
                    break;

                case GameState.GoalResult:
                    Enter(GameState.Celebration);
                    break;

                case GameState.SaveResult:
                case GameState.MissResult:
                    ProceedAfterResult();
                    break;

                case GameState.Celebration:
                    ProceedAfterResult();
                    break;

                case GameState.MatchEnd:
                    Enter(Match.Winner == Side.Home ? GameState.Victory : GameState.Defeat);
                    break;

                case GameState.Victory:
                case GameState.Defeat:
                    break;

                default:
                    break;
            }
        }

        /// <summary>The striker released a swipe. Moves out of Aiming immediately.</summary>
        public void BeginShot()
        {
            if (State != GameState.Aiming) return;
            Enter(GameState.Shooting);
        }

        /// <summary>
        /// Resolve a kick.
        /// </summary>
        /// <remarks>
        /// Called with an already-simulated outcome rather than simulating here, so
        /// the same method serves a local kick, an AI kick and — later — one a
        /// server has validated. That is the seam §40 needs.
        /// </remarks>
        public void ResolveKick(KickAttempt attempt)
        {
            if (State != GameState.Shooting && State != GameState.BallInPlay)
            {
                throw new InvalidOperationException($"ResolveKick: ball is not live (state is {State})");
            }

            _attempts.Add(attempt);
            LastAttempt = attempt;
            Match = ShootoutRules.RecordKick(Match, attempt.Result);
            Enter(GameStates.ResultFor(attempt.Result));
        }

        /// <summary>The striker let the clock run out. A miss, and the turn passes.</summary>
        public void ExpireShotClock()
        {
            if (State != GameState.Aiming) return;
            ResolveExpiry(KickResult.Expired);
        }

        private void ResolveExpiry(KickResult result)
        {
            LastAttempt = null;
            Match = ShootoutRules.RecordKick(Match, result);
            Enter(GameStates.ResultFor(result));
        }

        /// <summary>
        /// After a result: end the match, open sudden death, or set up the next kick.
        /// </summary>
        /// <remarks>
        /// The order is the rule. A completed match must reach <see
        /// cref="GameState.MatchEnd"/> even if the rules also just promoted the
        /// phase, and a match that has *entered* sudden death gets its own state so
        /// the HUD and the music can say so before the next kick is set up.
        /// </remarks>
        private void ProceedAfterResult()
        {
            if (Match.Phase == Phase.Complete)
            {
                Enter(GameState.MatchEnd);
                return;
            }

            bool enteringSuddenDeath =
                Match.Phase == Phase.SuddenDeath
                && ShootoutRules.KicksTakenBy(Match, Side.Home) == Match.KicksPerSide
                && ShootoutRules.KicksTakenBy(Match, Side.Away) == Match.KicksPerSide;

            Enter(enteringSuddenDeath ? GameState.SuddenDeath : GameState.NextPenalty);
        }

        private static double? HoldFor(GameState state) => state switch
        {
            GameState.MatchIntro => Timing.MatchIntro,
            GameState.PrePenalty => Timing.PrePenalty,
            GameState.RefereeReady => Timing.RefereeReady,
            GameState.Whistle => Timing.Whistle,
            GameState.Aiming => Timing.ShotClock,
            GameState.Shooting => Timing.Shooting,
            GameState.BallInPlay => Timing.BallInPlay,
            GameState.GoalResult => Timing.Result,
            GameState.SaveResult => Timing.Result,
            GameState.MissResult => Timing.Result,
            GameState.Celebration => Timing.Celebration,
            GameState.NextPenalty => Timing.NextPenalty,
            GameState.SuddenDeath => Timing.NextPenalty,
            GameState.MatchEnd => Timing.MatchEnd,
            // Menus and terminal states wait for input, not for a clock.
            _ => null
        };

        private void Enter(GameState state)
        {
            State = state;
            Elapsed = 0;
            StateChanged?.Invoke(state);
        }
    }
}
