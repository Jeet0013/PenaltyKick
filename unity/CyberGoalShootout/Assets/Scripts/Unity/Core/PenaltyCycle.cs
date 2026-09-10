using CyberGoal.Core.Ai;
using CyberGoal.Core.Input;
using CyberGoal.Core.Keeper;
using CyberGoal.Core.Physics;
using CyberGoal.Core.Rules;
using CyberGoal.Core.Util;
using CyberGoal.Unity.Characters;
using CyberGoal.Unity.Gameplay;
using CyberGoal.Unity.Input;
using UnityEngine;
using CoreVec3 = CyberGoal.Core.Util.Vec3;

namespace CyberGoal.Unity.Core
{
    /// <summary>
    /// One complete penalty, end to end (§54, §60).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Whistle → swipe → run-up → strike → flight → keeper → result → next kick.
    /// This is the class the whole vertical slice exists to make work, and it is
    /// deliberately thin: the rules, the flight, the save and the AI all live in
    /// Core and are already tested. What is left here is scheduling and display.
    /// </para>
    /// <para>
    /// <b>The keeper decides before the ball exists.</b> <see cref="TakeShot"/>
    /// calls <c>DecideDive</c> and only then calls <c>Launch</c>. That ordering is
    /// the enforcement of §15's "the AI must never know the shot direction before
    /// the player performs it" — not a comment asking future code to behave, but a
    /// sequence in which the information is not yet available to be misused.
    /// </para>
    /// </remarks>
    public sealed class PenaltyCycle : MonoBehaviour
    {
        [Header("Wiring")]
        public MatchRunner runner;
        public BallView ball;
        public SwipeInput swipe;
        public CharacterVisual striker;
        public CharacterVisual keeper;
        public CharacterVisual referee;

        [Header("Difficulty")]
        public Difficulty difficulty = Difficulty.Normal;
        public KeeperTrait keeperTrait = KeeperTrait.Patient;

        /// <summary>Raised when a kick resolves, for the HUD, audio and camera.</summary>
        public event System.Action<KickAttempt> KickResolved;

        private readonly TendencyMemory _memory = new TendencyMemory();
        private MatchRecorder _recorder;

        /// <summary>
        /// The match so far, as inputs only (§26).
        /// </summary>
        /// <remarks>
        /// Built as the match is played rather than reconstructed afterwards,
        /// because the keeper's commit is not recoverable from the result — a dive
        /// that missed leaves no trace in the score.
        /// </remarks>
        public MatchRecording Recording => _recorder?.Build();

        private BallState _flight;
        private bool _inFlight;
        private double _accumulator;
        private KeeperCommit _commit;
        private KickInput _input;
        private Stance _stance = Stance.Driven;

        private float _gestureStartedAt = -1f;
        private float _strikeStartedAt = -1f;

        private static readonly Vector3 KeeperOrigin =
            new Vector3(0f, 0f, -(float)BallPhysics.Field.SpotToGoal + 0.35f);

        private void OnEnable()
        {
            if (swipe != null)
            {
                swipe.SwipeCompleted += OnSwipe;
                swipe.GestureBegan += OnGestureBegan;
            }
            if (runner != null)
            {
                runner.StateChanged += OnStateChanged;
                _recorder = new MatchRecorder(runner.Director.Seed, runner.Director.Match.Format);
            }
        }

        private void OnDisable()
        {
            if (swipe != null)
            {
                swipe.SwipeCompleted -= OnSwipe;
                swipe.GestureBegan -= OnGestureBegan;
            }
            if (runner != null) runner.StateChanged -= OnStateChanged;
        }

        private void OnGestureBegan() => _gestureStartedAt = Time.unscaledTime;

        public void SetStance(Stance stance) => _stance = stance;

        private void OnStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.PrePenalty:
                    ball.ResetToSpot();
                    _inFlight = false;
                    _gestureStartedAt = -1f;
                    _strikeStartedAt = -1f;
                    striker.ResetPose();
                    keeper.ResetPose();
                    keeper.transform.position = KeeperOrigin;
                    keeper.ReadyStance();
                    break;

                case GameState.Aiming:
                    swipe.SetEnabled(true);
                    break;

                case GameState.Shooting:
                    swipe.SetEnabled(false);
                    _strikeStartedAt = Time.time;
                    break;

                default:
                    swipe.SetEnabled(false);
                    break;
            }
        }

        private void OnSwipe(SwipeResult result)
        {
            if (runner == null || !runner.Director.CanStrike) return;
            TakeShot(result);
        }

        /// <summary>Turn a parsed swipe into a resolved kick.</summary>
        private void TakeShot(SwipeResult swipeResult)
        {
            MatchDirector director = runner.Director;

            // §11: read the timing meter at the moment the gesture began. A player
            // who never engaged with it gets a clean average shot rather than a
            // penalty, because the brief calls the mechanic optional.
            TimingResult timing = _gestureStartedAt < 0
                ? TimingWindow.Unused()
                : TimingWindow.Evaluate(Time.unscaledTime - _gestureStartedAt);

            double accuracy = BallPhysics.Stances[_stance].Accuracy;
            (double aimX, double aimY) = TimingWindow.ApplyAccuracy(
                swipeResult.TargetX, swipeResult.TargetY, timing, accuracy);

            _input = new KickInput(aimX, aimY, swipeResult.Power, swipeResult.Curl,
                timing.IsPerfect, _stance);

            // ── The order below is the rule, not a preference ─────────────────
            // The keeper commits from tendency history alone, before the ball has
            // been launched and therefore before any code could tell it where the
            // shot is going.
            _commit = GoalkeeperAi.DecideDive(_memory, difficulty, keeperTrait, director.Rng);

            director.BeginShot();

            _flight = BallPhysics.Launch(_input);
            _inFlight = true;
            _accumulator = 0;
            ball.SetTrail(true);

            _memory.Record(_input.TargetX, _input.TargetY);
            _recorder?.Record(_input, _commit);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            AnimateCharacters(dt);

            if (!_inFlight) return;

            // Fixed-step accumulation. Real time in, whole steps out — a 120 Hz
            // device and a 30 Hz device must produce the same flight, so the
            // renderer's frame rate never reaches the simulation.
            _accumulator += dt;
            while (_accumulator >= BallPhysics.Tuning.TimeStep && _flight.Outcome == Outcome.InFlight)
            {
                _accumulator -= BallPhysics.Tuning.TimeStep;
                _flight = BallPhysics.Step(_flight);
            }

            ball.Apply(_flight, dt);

            if (_flight.Outcome != Outcome.InFlight) Finish();
        }

        private void AnimateCharacters(float dt)
        {
            MatchDirector director = runner != null ? runner.Director : null;
            if (director == null) return;

            // Referee: raises the arm through RefereeReady, holds it up for the
            // whistle. §18 requires a visible cue as well as a sound, for players
            // who cannot hear it.
            if (referee != null)
            {
                float raised = director.State switch
                {
                    GameState.RefereeReady =>
                        Mathf.Clamp01((float)(director.Elapsed / MatchDirector.Timing.RefereeReady)),
                    GameState.Whistle => 1f,
                    _ => 0f
                };
                referee.RaiseWhistleArm(raised);
            }

            if (striker != null)
            {
                if (director.State == GameState.Shooting && _strikeStartedAt >= 0)
                {
                    float progress = (Time.time - _strikeStartedAt)
                                     / (float)MatchDirector.Timing.Shooting;
                    // The run-up occupies the first two thirds; the swing the rest.
                    if (progress < 0.66f) striker.RunUp(progress / 0.66f);
                    else striker.Strike((progress - 0.66f) / 0.34f);
                }
                else if (director.State != GameState.BallInPlay)
                {
                    striker.Idle(dt);
                }
            }

            if (keeper != null && _inFlight && _commit.Direction.HasValue)
            {
                // The dive is drawn from the rules' own extension number, so the
                // animation cannot claim a reach the save calculation did not grant.
                float extension = (float)Goalkeeper.ExtensionAt(_commit, _flight.Elapsed);
                keeper.Dive(
                    Goalkeeper.Lateral(_commit.Direction.Value),
                    Goalkeeper.IsHigh(_commit.Direction.Value),
                    extension,
                    KeeperOrigin + new Vector3((float)_commit.LinePosition, 0, 0));
            }
        }

        /// <summary>Turn a finished flight plus the keeper's dive into a rules result.</summary>
        private void Finish()
        {
            _inFlight = false;
            ball.SetTrail(false);

            KickResult result;
            double margin = 0;

            if (_flight.Outcome == Outcome.Goal)
            {
                SaveAttempt save = Goalkeeper.AttemptSave(_flight.Position, _commit, _flight.Elapsed);
                result = save.Saved ? KickResult.Saved : KickResult.Goal;
                margin = save.Margin;
            }
            else if (_flight.Outcome == Outcome.Woodwork)
            {
                result = KickResult.Woodwork;
            }
            else
            {
                result = KickResult.OffTarget;
            }

            MatchDirector director = runner.Director;
            var attempt = new KickAttempt(
                director.Striker, _input, _commit, _flight.Outcome, result,
                new CoreVec3(_flight.Position.X, _flight.Position.Y, _flight.Position.Z),
                margin);

            director.ResolveKick(attempt);
            KickResolved?.Invoke(attempt);
        }
    }
}
