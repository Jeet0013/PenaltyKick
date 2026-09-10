using CyberGoal.Unity.Environment;
using UnityEngine;

namespace CyberGoal.Unity.Characters
{
    /// <summary>
    /// Produces the striker, keeper and referee.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Builds a skinned humanoid — a real tapered body on a 20-bone rig — unless a
    /// rigged prefab is assigned to <see cref="rigged"/>, in which case that is
    /// used instead and nothing is generated.
    /// </para>
    /// <para>
    /// The generated body is a large step up from the capsules it replaces, and it
    /// is still not final art: no face, no hair, no cloth, no scanned detail. §2
    /// asks for detailed faces and hands at hero quality, so `docs/STATUS.md`
    /// keeps listing characters as PLACEHOLDER until a MakeHuman export lands.
    /// What has changed is that the rig, the proportions and the pose API are now
    /// the ones a real model will use, so the swap is genuinely one field.
    /// </para>
    /// </remarks>
    public sealed class HumanoidFactory : MonoBehaviour
    {
        [Header("Real character (leave empty to generate one)")]
        [Tooltip("A rigged humanoid prefab. See docs/CHARACTER-PIPELINE.md. " +
                 "When set, nothing is generated.")]
        public GameObject rigged;

        [Header("Kit")]
        public Color primary = new Color(0.11f, 0.17f, 0.29f);
        public Color neon = new Color(0.22f, 0.84f, 1f);
        public Color skin = new Color(0.55f, 0.35f, 0.23f);

        [Header("Build")]
        public bool castShadows = true;

        public static float Height => Skeleton.Height;

        public CharacterVisual Create(Transform parent, string label)
            => Create(parent, label, BodyShape.Striker);

        public CharacterVisual Create(Transform parent, string label, BodyShape shape)
        {
            if (rigged != null)
            {
                GameObject instance = Instantiate(rigged, parent);
                instance.name = label;
                CharacterVisual real = instance.GetComponent<CharacterVisual>()
                                       ?? instance.AddComponent<CharacterVisual>();
                real.isPlaceholder = false;
                real.Bind();
                return real;
            }

            return BuildSkinned(parent, label, shape);
        }

        private CharacterVisual BuildSkinned(Transform parent, string label, BodyShape shape)
        {
            var root = new GameObject(label);
            root.transform.SetParent(parent, false);

            HumanoidMeshBuilder.Built built = HumanoidMeshBuilder.Build(root.transform, shape, label);

            var rendererGo = new GameObject(label + "_Mesh");
            rendererGo.transform.SetParent(root.transform, false);

            var skin = rendererGo.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = built.Mesh;
            skin.bones = built.Bones;
            skin.rootBone = built.Root;
            skin.sharedMaterial = BuildKitMaterial();
            skin.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            // Without this the mesh is culled whenever its origin leaves the
            // frustum — which for a diving keeper is exactly when you are looking
            // at it. Generous bounds cost nothing at this object count.
            skin.localBounds = new Bounds(
                new Vector3(0f, Skeleton.Height * 0.5f, 0f),
                new Vector3(3f, Skeleton.Height + 1f, 3f));

            var visual = root.AddComponent<CharacterVisual>();
            visual.isPlaceholder = true;
            visual.bones = built.Bones;
            visual.Bind();
            return visual;
        }

        /// <summary>
        /// One material for the whole body.
        /// </summary>
        /// <remarks>
        /// A single draw call per character. Splitting skin, kit and boots into
        /// three materials would triple that for detail that is invisible at
        /// gameplay distance — and §19 of the art direction is explicit that human
        /// quality has to be balanced against mobile performance.
        /// </remarks>
        private Material BuildKitMaterial()
        {
            var material = new Material(ShaderLibrary.Lit) { color = primary };
            ShaderLibrary.SetSmoothness(material, 0.28f);
            // A low emissive in the team's neon keeps the figure separated from a
            // dark pitch without making it glow.
            ShaderLibrary.SetEmission(material, neon * 0.12f);
            return material;
        }
    }
}
