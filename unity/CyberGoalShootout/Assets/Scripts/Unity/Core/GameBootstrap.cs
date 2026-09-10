using CyberGoal.Core.Physics;
using CyberGoal.Core.Rules;
using CyberGoal.Core.Teams;
using CyberGoal.Unity.CameraWork;
using CyberGoal.Unity.Characters;
using CyberGoal.Unity.Environment;
using CyberGoal.Unity.Gameplay;
using CyberGoal.Unity.Input;
using CyberGoal.Unity.UI;
using UnityEngine;

namespace CyberGoal.Unity.Core
{
    /// <summary>
    /// Builds and wires the whole game at runtime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why there is no .unity scene file.</b> A Unity scene is YAML full of
    /// file IDs and asset GUIDs, and one wrong reference produces a scene that
    /// opens broken with an error that names a number rather than a cause. This
    /// project was authored without an editor available to generate or validate
    /// one. Constructing the scene in code means it opens correctly in any Unity
    /// version, is diffable in review, and needs no asset the repository does not
    /// contain.
    /// </para>
    /// <para>
    /// <see cref="Boot"/> carries <c>RuntimeInitializeOnLoadMethod</c>, so the game
    /// starts in <em>whatever</em> scene is loaded, including the empty default one
    /// a new project ships with. There is nothing to set up: press Play.
    /// </para>
    /// <para>
    /// When the art pipeline arrives this becomes the thing that loads a scene
    /// instead of building one, and the wiring below becomes the prefab's
    /// inspector references. The order of operations is the part worth keeping.
    /// </para>
    /// </remarks>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private MatchRunner _runner;
        private PenaltyCycle _cycle;
        private CameraDirector _camera;
        private MatchHud _hud;
        private BallView _ball;
        private QualityController _quality;

        private Team _home;
        private Team _away;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("[CyberGoal]");
            go.AddComponent<GameBootstrap>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            // §2: landscape is the gameplay orientation, and the camera framing and
            // HUD layout are both built for it.
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _quality = gameObject.AddComponent<QualityController>();

            _home = TeamCatalogue.DefaultHome;
            _away = TeamCatalogue.DefaultAway;

            BuildWorld();
            BuildActors();
            BuildUi();
            Wire();

            Debug.Log($"[CyberGoal] {_home.Name} vs {_away.Name} · build {Application.version}");
        }

        private void BuildWorld()
        {
            var stadiumGo = new GameObject("Stadium");
            var stadium = stadiumGo.AddComponent<StadiumBlockout>();
            stadium.homeNeon = ToColor(_home.Neon);
            stadium.awayNeon = ToColor(_away.Neon);
            stadium.Build(_quality.Preset);

            var cameraGo = new GameObject("MainCamera");
            cameraGo.tag = "MainCamera";
            _camera = cameraGo.AddComponent<CameraDirector>();
            cameraGo.AddComponent<AudioListener>();
        }

        private void BuildActors()
        {
            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "Ball";
            ballGo.transform.localScale = Vector3.one * (float)BallPhysics.Field.BallRadius * 2f;
            // No collider and no Rigidbody: the flight is solved in Core, and a
            // second physics opinion is the fastest way to make a goal ambiguous.
            DestroyImmediate(ballGo.GetComponent<Collider>());
            _ball = ballGo.AddComponent<BallView>();
            _ball.ResetToSpot();

            CharacterVisual striker = MakeCharacter("Striker", _home,
                new Vector3(-1.6f, 0f, 5.4f), 180f);
            CharacterVisual keeper = MakeCharacter("Goalkeeper", _away,
                new Vector3(0f, 0f, -(float)BallPhysics.Field.SpotToGoal + 0.35f), 0f);
            // The referee stands out by the edge of the box. Closer than this and
            // he clips the edge of a 30-degree lens and reads as an object stuck to
            // the camera rather than a person on the pitch.
            CharacterVisual referee = MakeReferee(new Vector3(-8.2f, 0f, -5.6f));

            var runnerGo = new GameObject("Match");
            _runner = runnerGo.AddComponent<MatchRunner>();

            var swipe = runnerGo.AddComponent<SwipeInput>();

            _cycle = runnerGo.AddComponent<PenaltyCycle>();
            _cycle.runner = _runner;
            _cycle.ball = _ball;
            _cycle.swipe = swipe;
            _cycle.striker = striker;
            _cycle.keeper = keeper;
            _cycle.referee = referee;
        }

        private CharacterVisual MakeCharacter(string name, Team team, Vector3 position, float yaw)
        {
            var host = new GameObject(name + "Rig");
            host.transform.position = position;
            host.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var factory = host.AddComponent<HumanoidFactory>();
            factory.primary = ToColor(team.Primary);
            factory.neon = ToColor(team.Neon);
            return factory.Create(host.transform, name);
        }

        private CharacterVisual MakeReferee(Vector3 position)
        {
            var host = new GameObject("RefereeRig");
            host.transform.position = position;
            host.transform.rotation = Quaternion.Euler(0f, 130f, 0f);

            var factory = host.AddComponent<HumanoidFactory>();
            factory.primary = new Color(0.08f, 0.09f, 0.11f);
            factory.neon = new Color(1f, 0.82f, 0.25f);
            return factory.Create(host.transform, "Referee");
        }

        private void BuildUi()
        {
            var hudGo = new GameObject("UI");
            _hud = hudGo.AddComponent<MatchHud>();
            _hud.Build(_home, _away);
        }

        private void Wire()
        {
            _runner.StateChanged += OnStateChanged;
            _cycle.KickResolved += OnKickResolved;
            _hud.Refresh(_runner.Director.Match);
        }

        private void OnDestroy()
        {
            if (_runner != null) _runner.StateChanged -= OnStateChanged;
            if (_cycle != null) _cycle.KickResolved -= OnKickResolved;
        }

        private void OnStateChanged(GameState state)
        {
            _camera.FollowState(state);
            _hud.Refresh(_runner.Director.Match);
            _hud.ShowTiming(state == GameState.Aiming);

            switch (state)
            {
                case GameState.MatchIntro:
                    _hud.Say($"{_home.Name} vs {_away.Name}");
                    break;
                case GameState.PrePenalty:
                    _hud.SetPrompt(_runner.Director.Striker == Side.Home
                        ? $"{_home.Name} to take it"
                        : $"{_away.Name} to take it");
                    _hud.Say(string.Empty);
                    break;
                case GameState.RefereeReady:
                    _hud.Say("Referee ready");
                    break;
                case GameState.Whistle:
                    _hud.Say("Whistle");
                    break;
                case GameState.Aiming:
                    _hud.SetPrompt("Swipe to shoot");
                    _hud.Say(string.Empty);
                    break;
                case GameState.SuddenDeath:
                    _hud.Say("Sudden death");
                    break;
                case GameState.Victory:
                case GameState.Defeat:
                {
                    (int home, int away) = ShootoutRules.Scoreline(_runner.Director.Match);
                    Team winner = _runner.Director.Match.Winner == Side.Home ? _home : _away;
                    _hud.SetPrompt(string.Empty);
                    _hud.Say($"{winner.Name} win {Mathf.Max(home, away)}-{Mathf.Min(home, away)}");
                    break;
                }
            }
        }

        private void OnKickResolved(KickAttempt attempt)
        {
            _hud.Refresh(_runner.Director.Match);
            _hud.SetPrompt(string.Empty);

            switch (attempt.Result)
            {
                case KickResult.Goal:
                    _hud.Say("GOAL");
                    _camera.Impulse(0.18f);
                    break;
                case KickResult.Saved:
                    _hud.Say(attempt.IsSpectacularSave ? "CYBER SAVE" : "Saved");
                    _camera.Impulse(0.12f);
                    break;
                case KickResult.Woodwork:
                    _hud.Say("Off the frame");
                    _camera.Impulse(0.14f);
                    break;
                case KickResult.Expired:
                    _hud.Say("Too slow");
                    break;
                default:
                    _hud.Say("Wide");
                    break;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            GameState state = _runner.Director.State;

            _camera.UpdateCamera(dt, state == GameState.BallInPlay ? _ball.transform.position : (Vector3?)null);
            _hud.SetClock(_runner.Director.ShotClockRemaining);
            // §27: the HUD steps back once the ball is live, because that is the
            // moment the player most wants to watch.
            _hud.SetDimmed(state == GameState.BallInPlay || state == GameState.Celebration, dt);

            if (state == GameState.Aiming)
            {
                _hud.SetTimingMarker((float)CyberGoal.Core.Input.TimingWindow.MarkerAt(
                    _runner.Director.Elapsed));
            }
        }

        private static Color ToColor(Rgb rgb) => new Color(rgb.R / 255f, rgb.G / 255f, rgb.B / 255f);
    }
}
