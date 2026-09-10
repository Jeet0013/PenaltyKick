using UnityEngine;

namespace CyberGoal.Unity.Characters
{
    /// <summary>
    /// The handle every system uses to pose a character.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The seam between gameplay and art. Nothing outside this file knows whether
    /// the body underneath is a generated skinned mesh or a rigged, scanned
    /// humanoid with finger bones — callers ask for a pose and this decides how to
    /// produce it.
    /// </para>
    /// <para>
    /// Poses are expressed as <em>intent</em> ("dive this far, this way"), never
    /// as joint angles. A rigged character satisfies them with animator
    /// parameters; the generated body satisfies them by rotating bones. Callers
    /// never learn which, which is what makes §23's character swap a one-field
    /// change rather than a rewrite of every system that moves a person.
    /// </para>
    /// </remarks>
    public sealed class CharacterVisual : MonoBehaviour
    {
        [Tooltip("True while this is a generated body rather than final art.")]
        public bool isPlaceholder = true;

        [Tooltip("Indexed by Bone. Null when an Animator is driving instead.")]
        public Transform[] bones;

        private Animator _animator;
        private float _breathePhase;

        /// <summary>Whether a real humanoid rig is driving this body.</summary>
        public bool IsRigged => _animator != null && _animator.isHuman;

        public void Bind()
        {
            _animator = GetComponentInChildren<Animator>();
            _breathePhase = Random.value * 10f;
        }

        private Transform Get(Bone bone)
        {
            if (bones == null) return null;
            int i = (int)bone;
            return i >= 0 && i < bones.Length ? bones[i] : null;
        }

        // ── Pose API ─────────────────────────────────────────────────────────

        /// <summary>Idle breathing, so a waiting figure is not a statue.</summary>
        public void Idle(float deltaTime)
        {
            if (IsRigged)
            {
                _animator.SetFloat(AnimatorParams.Speed, 0f);
                return;
            }

            _breathePhase += deltaTime * 1.8f;
            float breath = Mathf.Sin(_breathePhase);

            // The chest lifts and the shoulders follow. Moving only the chest looks
            // like a bellows; moving the shoulders with it looks like breathing.
            Rotate(Bone.Chest, breath * 1.4f, 0f, 0f);
            Rotate(Bone.Spine, breath * 0.7f, 0f, 0f);

            // Arms hang slightly out from the body, not flat against it.
            Rotate(Bone.UpperArmL, 0f, 0f, 5f + breath * 0.6f);
            Rotate(Bone.UpperArmR, 0f, 0f, -5f - breath * 0.6f);
            Rotate(Bone.ForearmL, 6f, 0f, 0f);
            Rotate(Bone.ForearmR, 6f, 0f, 0f);
            Rotate(Bone.ThighL, 0f, 0f, 0f);
            Rotate(Bone.ThighR, 0f, 0f, 0f);
        }

        /// <summary>The keeper's ready stance: crouched, arms out and forward.</summary>
        public void ReadyStance()
        {
            if (IsRigged)
            {
                _animator.SetBool(AnimatorParams.Ready, true);
                return;
            }

            // Weight low and arms wide. §16 calls the ready stance most of what
            // makes a keeper look like a keeper rather than a person in a goal.
            Rotate(Bone.Hips, 8f, 0f, 0f);
            Rotate(Bone.Spine, 6f, 0f, 0f);
            Rotate(Bone.ThighL, -14f, 0f, -6f);
            Rotate(Bone.ThighR, -14f, 0f, 6f);
            Rotate(Bone.ShinL, 26f, 0f, 0f);
            Rotate(Bone.ShinR, 26f, 0f, 0f);
            Rotate(Bone.UpperArmL, -18f, 0f, 62f);
            Rotate(Bone.UpperArmR, -18f, 0f, -62f);
            Rotate(Bone.ForearmL, -28f, 0f, 0f);
            Rotate(Bone.ForearmR, -28f, 0f, 0f);
        }

        /// <summary>
        /// A dive, expressed as the extension the rules computed.
        /// </summary>
        /// <param name="extension">
        /// 0-1, straight from <c>Goalkeeper.ExtensionAt</c>. Passing the rules'
        /// own number rather than running a separate animation timeline is what
        /// stops the keeper visibly touching a ball that was scored as a goal.
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
            // Rotating into the dive is what makes it read as a dive rather than a
            // figure sliding sideways.
            transform.rotation = Quaternion.Euler(0f, 0f, -lateral * extension * 82f);

            // The leading arm reaches; the trailing arm counterbalances. Both fully
            // extended looks like a skydiver.
            float reach = 78f + extension * 24f;
            if (lateral < 0)
            {
                Rotate(Bone.UpperArmL, 0f, 0f, reach);
                Rotate(Bone.UpperArmR, -30f, 0f, -34f);
            }
            else
            {
                Rotate(Bone.UpperArmR, 0f, 0f, -reach);
                Rotate(Bone.UpperArmL, -30f, 0f, 34f);
            }
            Rotate(Bone.ForearmL, -8f, 0f, 0f);
            Rotate(Bone.ForearmR, -8f, 0f, 0f);

            // Legs trail on a low dive and tuck on a high one.
            float leg = high ? 34f : -18f;
            Rotate(Bone.ThighL, leg, 0f, 0f);
            Rotate(Bone.ThighR, leg * 0.6f, 0f, 0f);
            Rotate(Bone.ShinL, high ? 46f : 12f, 0f, 0f);
            Rotate(Bone.ShinR, high ? 40f : 8f, 0f, 0f);
        }

        /// <summary>The striker's approach. 0 at the mark, 1 at contact.</summary>
        public void RunUp(float runUp)
        {
            if (IsRigged)
            {
                _animator.SetFloat(AnimatorParams.Speed, Mathf.Clamp01(runUp) * 4f);
                return;
            }

            float stride = Mathf.Sin(runUp * Mathf.PI * 3.2f);

            // Contralateral: the left arm swings with the right leg. Swinging them
            // together is the single clearest sign of a body that was animated by
            // someone not watching a person walk.
            Rotate(Bone.ThighL, stride * 42f, 0f, 0f);
            Rotate(Bone.ThighR, -stride * 42f, 0f, 0f);
            Rotate(Bone.ShinL, Mathf.Max(0f, -stride) * 46f, 0f, 0f);
            Rotate(Bone.ShinR, Mathf.Max(0f, stride) * 46f, 0f, 0f);
            Rotate(Bone.UpperArmL, -stride * 34f, 0f, 12f);
            Rotate(Bone.UpperArmR, stride * 34f, 0f, -12f);
            Rotate(Bone.ForearmL, 34f, 0f, 0f);
            Rotate(Bone.ForearmR, 34f, 0f, 0f);
            // Lean into the run.
            Rotate(Bone.Hips, runUp * 7f, 0f, 0f);
        }

        /// <summary>The strike: plant, swing, follow through.</summary>
        public void Strike(float progress)
        {
            if (IsRigged)
            {
                _animator.SetTrigger(AnimatorParams.Kick);
                return;
            }

            float p = Mathf.Clamp01(progress);
            // Overshoot and settle. The follow-through sells contact far more than
            // the contact frame does.
            float swing = Mathf.Sin(p * Mathf.PI) * 96f;

            Rotate(Bone.ThighR, -swing, 0f, 0f);
            Rotate(Bone.ShinR, Mathf.Max(0f, Mathf.Sin(p * Mathf.PI - 0.6f)) * 52f, 0f, 0f);
            // The plant leg stays under the body and slightly bent.
            Rotate(Bone.ThighL, 12f, 0f, 0f);
            Rotate(Bone.ShinL, 18f, 0f, 0f);
            // Torso counter-rotates against the kicking leg — that is where the
            // power visibly comes from.
            Rotate(Bone.Hips, 10f, -swing * 0.12f, 0f);
            Rotate(Bone.Chest, 6f, swing * 0.16f, 0f);
            Rotate(Bone.UpperArmL, -swing * 0.4f, 0f, 42f);
            Rotate(Bone.UpperArmR, swing * 0.25f, 0f, -22f);
        }

        /// <summary>One arm straight up — §18's visual whistle cue.</summary>
        public void RaiseWhistleArm(float raised)
        {
            if (IsRigged)
            {
                _animator.SetFloat(AnimatorParams.SignalRaise, raised);
                return;
            }

            Rotate(Bone.UpperArmR, 0f, 0f, -raised * 168f);
            Rotate(Bone.ForearmR, -raised * 14f, 0f, 0f);
            Rotate(Bone.UpperArmL, 0f, 0f, 8f);
            // Looks up at the striker as the arm rises.
            Rotate(Bone.Head, -raised * 8f, 0f, 0f);
        }

        /// <summary>Cheering, for crowd figures.</summary>
        public void Cheer(float intensity, float phase)
        {
            if (IsRigged) return;
            float wave = Mathf.Sin(phase) * intensity;
            Rotate(Bone.UpperArmL, 0f, 0f, 120f + wave * 34f);
            Rotate(Bone.UpperArmR, 0f, 0f, -120f - wave * 34f);
            Rotate(Bone.Chest, wave * 5f, 0f, 0f);
        }

        public void ResetPose()
        {
            transform.rotation = Quaternion.identity;
            if (bones == null) return;
            for (int i = 0; i < bones.Length; i++)
            {
                if (i == (int)Bone.Root) continue;
                if (bones[i] != null) bones[i].localRotation = Quaternion.identity;
            }
        }

        private void Rotate(Bone bone, float x, float y, float z)
        {
            Transform t = Get(bone);
            if (t != null) t.localRotation = Quaternion.Euler(x, y, z);
        }
    }

    /// <summary>Animator parameter hashes, named once.</summary>
    /// <remarks>
    /// Hashed rather than string-compared because these are set every frame during
    /// a dive, and listed in one place so wiring a real rig means matching this
    /// table rather than grepping for string literals.
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
