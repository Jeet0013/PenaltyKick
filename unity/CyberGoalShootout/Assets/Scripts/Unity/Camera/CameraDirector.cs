using CyberGoal.Core.Physics;
using CyberGoal.Core.Rules;
using UnityEngine;

namespace CyberGoal.Unity.CameraWork
{
    /// <summary>Named shots, per §25.</summary>
    public enum CameraShot
    {
        BehindStriker,
        SideRunUp,
        FollowBall,
        GoalCamera,
        KeeperCamera,
        Celebration,
        Winner
    }

    /// <summary>
    /// Cinematic camera states (§25).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-driven rather than Cinemachine for the vertical slice: seven fixed
    /// framings with eased transitions is a small enough problem that a virtual
    /// camera per shot would be more configuration than code, and this keeps the
    /// numbers visible in a diff. If §26's replay needs blends and noise later,
    /// each case here becomes a virtual camera and this class becomes the brain.
    /// </para>
    /// <para>
    /// <b>The main framing is a 30-degree lens 7.6 m back, not a wide one closer.</b>
    /// The goal is 11 m from the spot and 7.32 m across; on the ~50-degree lens a
    /// game engine defaults to, it occupies about a quarter of the frame width and
    /// reads as a postcard at the far end of an empty field — too small to aim at.
    /// Broadcast penalties are shot long for exactly this reason: the compression
    /// is what makes the goal look like something you could hit. §59 puts gameplay
    /// responsiveness first, and being able to see what you are aiming at is part
    /// of that.
    /// </para>
    /// <para>
    /// The camera sits at 1.15 m — below head height. Higher looks down on the
    /// pitch and flattens the goal into a floor marking; from here the crossbar
    /// sits above the horizon, which is what makes the top corners feel reachable.
    /// It also lifts the ball far enough up the frame to clear the on-screen
    /// controls, which at 1.65 m sat directly on top of it.
    /// </para>
    /// <para>
    /// §25 warns against motion discomfort, so there is no shake while aiming —
    /// only on impact. Shake during aiming does not read as power, it reads as an
    /// input problem: the player is trying to place a shot and the world is moving
    /// under them.
    /// </para>
    /// </remarks>
    public sealed class CameraDirector : MonoBehaviour
    {
        [Header("Framing")]
        [Tooltip("Vertical FOV. Long on purpose — see the class remarks.")]
        public float fieldOfView = 30f;
        public float easing = 6.5f;

        private Camera _camera;
        private CameraShot _shot = CameraShot.BehindStriker;
        private Vector3 _position;
        private Vector3 _target;
        private float _shake;

        private static readonly float GoalZ = -(float)BallPhysics.Field.SpotToGoal;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null) _camera = gameObject.AddComponent<Camera>();
            _camera.fieldOfView = fieldOfView;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 400f;

            _position = new Vector3(0.6f, 1.15f, 7.6f);
            _target = new Vector3(0f, 1.35f, GoalZ);
            transform.position = _position;
            transform.LookAt(_target);
        }

        public void SetShot(CameraShot shot) => _shot = shot;

        /// <summary>Choose a shot from the match state, so nothing else has to.</summary>
        public void FollowState(GameState state)
        {
            SetShot(state switch
            {
                GameState.PrePenalty => CameraShot.SideRunUp,
                GameState.RefereeReady => CameraShot.SideRunUp,
                GameState.BallInPlay => CameraShot.FollowBall,
                GameState.GoalResult => CameraShot.GoalCamera,
                GameState.SaveResult => CameraShot.KeeperCamera,
                GameState.Celebration => CameraShot.Celebration,
                GameState.Victory or GameState.Defeat => CameraShot.Winner,
                _ => CameraShot.BehindStriker
            });
        }

        /// <summary>Kick the camera. Only ever called on impact.</summary>
        public void Impulse(float strength) => _shake = Mathf.Min(0.35f, _shake + strength);

        /// <param name="ball">Where the ball is, when one is in flight.</param>
        public void UpdateCamera(float deltaTime, Vector3? ball)
        {
            Vector3 desired = _position;
            Vector3 lookAt = _target;

            switch (_shot)
            {
                case CameraShot.BehindStriker:
                    desired = new Vector3(0.6f, 1.15f, 7.6f);
                    lookAt = new Vector3(0f, 1.35f, GoalZ);
                    break;

                case CameraShot.SideRunUp:
                    desired = new Vector3(5.4f, 1.9f, 4.6f);
                    lookAt = new Vector3(0f, 1.0f, GoalZ * 0.45f);
                    break;

                case CameraShot.FollowBall:
                    if (ball.HasValue)
                    {
                        // Trails the ball rather than riding it: a camera locked to a
                        // fast object makes the object look stationary and the world
                        // look wrong.
                        Vector3 b = ball.Value;
                        desired = new Vector3(b.x * 0.35f, Mathf.Max(1.4f, b.y + 1.0f), b.z + 4.2f);
                        lookAt = new Vector3(b.x * 0.6f, b.y, b.z - 1.5f);
                    }
                    break;

                case CameraShot.GoalCamera:
                    desired = new Vector3(2.6f, 2.4f, GoalZ + 5.5f);
                    lookAt = new Vector3(0f, 1.2f, GoalZ);
                    break;

                case CameraShot.KeeperCamera:
                    // Behind the goal looking back, so a keeper never sees the aim
                    // hint — §14's separation, made structural.
                    desired = new Vector3(0f, 2.3f, GoalZ - 5.5f);
                    lookAt = new Vector3(0f, 1.1f, 0f);
                    break;

                case CameraShot.Celebration:
                    desired = new Vector3(-3.4f, 2.2f, GoalZ + 6.5f);
                    lookAt = new Vector3(0f, 1.4f, GoalZ + 1.5f);
                    break;

                case CameraShot.Winner:
                    desired = new Vector3(4.5f, 5.2f, GoalZ + 9f);
                    lookAt = new Vector3(0f, 1.2f, GoalZ);
                    break;
            }

            // Frame-rate independent smoothing: 1 - exp(-k*dt) rather than a fixed
            // lerp factor, so the camera settles at the same rate at 30 fps and 120.
            float ease = 1f - Mathf.Exp(-easing * deltaTime);
            _position = Vector3.Lerp(_position, desired, ease);
            _target = Vector3.Lerp(_target, lookAt, ease);

            transform.position = _position;

            if (_shake > 0.0005f)
            {
                transform.position += new Vector3(
                    (Random.value - 0.5f) * _shake,
                    (Random.value - 0.5f) * _shake,
                    0f);
                _shake *= Mathf.Exp(-7f * deltaTime);
            }
            else
            {
                _shake = 0f;
            }

            transform.LookAt(_target);
        }
    }
}
