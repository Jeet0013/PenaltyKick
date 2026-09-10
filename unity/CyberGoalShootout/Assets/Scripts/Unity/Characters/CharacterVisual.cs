using UnityEngine;

namespace CyberGoal.Unity.Characters
{
    /// <summary>
    /// The handle every system uses to pose a character.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The seam between gameplay and art. Nothing outside this file knows whether
    /// the body underneath is four capsules or a rigged, skinned humanoid with
    /// finger bones — callers ask for a pose and this decides how to produce it.
    /// </para>
    /// <para>
    /// That matters because §23 requires real characters before the art is
    /// considered done, and a codebase where the striker's run-up reaches into
    /// specific capsule transforms cannot accept one. With this in place, the
    /// swap is: assign a prefab, set an Animator, delete nothing.
    /// </para>
    /// </remarks>
    public sealed class CharacterVisual : MonoBehaviour
    {
        [Tooltip("True while this is procedural stand-in geometry rather than art.")]
        public bool isPlaceholder = true;

        [Header("Placeholder joints (unused when an Animator is present)")]
        public Transform torso;
        public Transform head;
        public Transform leftArm;
        public Transform rightArm;
        public Transform leftLeg;
        public Transform rightLeg;

        private Animator _animator;
        private float _breathePhase;

        /// <summary>Whether a real rig is driving this body.</summary>
        public bool IsRigged => _animator != null && _animator.isHuman;

        public void Bind()
        {
            _animator = GetComponentInChildren<Animator>();
            _breathePhase = Random.value * 10f;
        }

        // ── Pose API ─────────────────────────────────────────────────────────
        //
        // Deliberately expressed as intent ("dive this far, this way") rather
        // than as joint angles. A rigged character satisfies these with animator
        // parameters; the placeholder satisfies them by rotating pivots. Callers
        // never learn which.

        /// <summary>Idle breathing, so a waiting figure is not a statue.</summary>
        public void Idle(float deltaTime)
        {
            if (IsRigged)
            {
                _animator.SetFloat(AnimatorParams.Speed, 0f);
                return;
            }

            _breathePhase += deltaTime * 1.8f;
            if (torso != null)
            {
                Vector3 scale = torso.localScale;
                scale.y = torso.localScale.y; // preserved; breathing rides on position
                torso.localScale = scale;
                torso.localPosition = new Vector3(0, 1.27f + Mathf.Sin(_breathePhase) * 0.008f, 0);
            }
            SetLimbs(4f, 4f, 0f, 0f);
        }

        /// <summary>The keeper's ready stance: low, arms out.</summary>
        public void ReadyStance()
        {
            if (IsRigged)
            {
                _animator.SetBool(AnimatorParams.Ready, true);
                return;
            }
            SetLimbs(66f, 66f, 6f, -6f);
        }

        /// <summary>
        /// A dive, expressed as the extension the rules computed.
        /// </summary>
        /// <param name="lateral">-1 left, 0 centre, +1 right.</param>
        /// <param name="high">Whether it is a high dive.</param>
        /// <param name="extension">
        /// 0-1, straight from <c>Goalkeeper.ExtensionAt</c>. Passing the rules'
        /// own number rather than a separate animation timeline is what keeps
        /// what the player sees and what was judged from disagreeing.
        /// </param>
        public void Dive(int lateral, bool high, float extension, Vector3 origin)
        {
            if (IsRigged)
            {
                _animator.SetFloat(AnimatorParams.DiveSide, lateral);
                _animator.SetFloat(AnimatorParams.DiveHeight, high ? 1f : 0f);
                _animator.SetFloat(AnimatorParams.DiveExtension, extension);
                _animator.SetTrigger(AnimatorParams.Dive);
                return;
            }

            transform.position = origin + new Vector3(
                lateral * 2.45f * extension,
                (high ? 1.15f : 0.22f) * extension,
                0f);
            // Rotating into the dive is what makes it read as a dive rather than
            // as a figure sliding sideways.
            transform.rotation = Quaternion.Euler(0, 0, -lateral * extension * 82f);
            SetLimbs(92f, 92f, high ? 28f : -12f, high ? -28f : 12f);
        }

        /// <summary>The striker's approach. <paramref name="runUp"/> is 0 at the mark, 1 at contact.</summary>
        public void RunUp(float runUp)
        {
            if (IsRigged)
            {
                _animator.SetFloat(AnimatorParams.Speed, Mathf.Clamp01(runUp) * 4f);
                return;
            }

            float stride = Mathf.Sin(runUp * Mathf.PI * 3.2f);
            SetLimbs(stride * 29f, -stride * 29f, stride * 52f, -stride * 52f);
        }

        /// <summary>The strike. <paramref name="progress"/> runs 0-1 through the swing.</summary>
        public void Strike(float progress)
        {
            if (IsRigged)
            {
                _animator.SetTrigger(AnimatorParams.Kick);
                return;
            }

            // Overshoot and settle: the follow-through sells contact more than the
            // contact frame does.
            float swing = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) * 86f;
            SetLimbs(34f, -17f, -swing, swing * 0.3f);
        }

        /// <summary>One arm straight up — §18's visual whistle cue.</summary>
        public void RaiseWhistleArm(float raised)
        {
            if (IsRigged)
            {
                _animator.SetFloat(AnimatorParams.SignalRaise, raised);
                return;
            }
            SetLimbs(6f, raised * 166f, 0f, 0f);
        }

        public void ResetPose()
        {
            transform.rotation = Quaternion.identity;
            SetLimbs(4f, 4f, 0f, 0f);
        }

        /// <summary>Angles in degrees. Arms swing outward (Z), legs fore and aft (X).</summary>
        private void SetLimbs(float leftArmDeg, float rightArmDeg, float leftLegDeg, float rightLegDeg)
        {
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(0, 0, leftArmDeg);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(0, 0, -rightArmDeg);
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(leftLegDeg, 0, 0);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(rightLegDeg, 0, 0);
        }
    }

    /// <summary>
    /// Animator parameter hashes, named once.
    /// </summary>
    /// <remarks>
    /// Hashed rather than string-compared because these are set every frame during
    /// a dive, and named in one place so that wiring a real rig means matching
    /// this list rather than grepping for string literals.
    /// </remarks>
    public static class AnimatorParams
    {
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int Ready = Animator.StringToHash("Ready");
        public static readonly int Dive = Animator.StringToHash("Dive");
        public static readonly int DiveSide = Animator.StringToHash("DiveSide");
        public static readonly int DiveHeight = Animator.StringToHash("DiveHeight");
        public static readonly int DiveExtension = Animator.StringToHash("DiveExtension");
        public static readonly int Kick = Animator.StringToHash("Kick");
        public static readonly int SignalRaise = Animator.StringToHash("SignalRaise");
    }
}
