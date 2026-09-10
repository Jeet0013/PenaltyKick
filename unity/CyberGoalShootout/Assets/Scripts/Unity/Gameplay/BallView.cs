using CyberGoal.Core.Physics;
using UnityEngine;

namespace CyberGoal.Unity.Gameplay
{
    /// <summary>
    /// Draws the ball wherever the deterministic simulation says it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Note what this does <em>not</em> have: a Rigidbody. The flight is solved in
    /// <see cref="BallPhysics"/> and this transform is told the answer. Letting
    /// PhysX move the ball would make the result depend on frame rate and platform,
    /// which §40's server-side re-run cannot tolerate — and would give the game two
    /// disagreeing opinions about whether something was a goal.
    /// </para>
    /// <para>
    /// The spin is cosmetic and derived from velocity, so it costs nothing to get
    /// slightly wrong and would cost determinism to simulate properly.
    /// </para>
    /// </remarks>
    public sealed class BallView : MonoBehaviour
    {
        [Tooltip("Purely visual. Rolling rotation is derived from velocity.")]
        public float spinScale = 0.35f;

        private TrailRenderer _trail;

        private void Awake()
        {
            _trail = GetComponentInChildren<TrailRenderer>();
            SetTrail(false);
        }

        /// <summary>Place the ball for a state produced by the Core simulation.</summary>
        public void Apply(BallState state, float deltaTime)
        {
            transform.position = new Vector3(
                (float)state.Position.X,
                (float)state.Position.Y,
                (float)state.Position.Z);

            transform.Rotate(
                (float)(-state.Velocity.Z / BallPhysics.Field.BallRadius) * deltaTime * spinScale,
                0f,
                (float)(state.Velocity.X / BallPhysics.Field.BallRadius) * deltaTime * spinScale,
                Space.World);
        }

        public void ResetToSpot()
        {
            transform.position = new Vector3(0f, (float)BallPhysics.Field.BallRadius, 0f);
            transform.rotation = Quaternion.identity;
            SetTrail(false);
        }

        public void SetTrail(bool on)
        {
            if (_trail == null) return;
            _trail.emitting = on;
            if (!on) _trail.Clear();
        }
    }
}
