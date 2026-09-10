using UnityEngine;

namespace CyberGoal.Unity.Characters
{
    /// <summary>
    /// Produces the striker, keeper and referee bodies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Everything this class builds procedurally is PLACEHOLDER.</b> The art
    /// direction is unambiguous: §21 forbids shipping capsules, cubes, spheres or
    /// stick figures as final artwork, and §2 requires hero characters with real
    /// anatomy, faces, hands and PBR materials. Nothing generated here meets that
    /// bar and nothing here is intended to.
    /// </para>
    /// <para>
    /// What it does instead is make the swap trivial. Assign a rigged humanoid
    /// prefab to <see cref="rigged"/> and this component instantiates it and never
    /// builds a primitive again. Every other system — animation, the camera, the
    /// save resolution — talks to <see cref="CharacterVisual"/>, not to geometry,
    /// so replacing the model changes this file and nothing else.
    /// </para>
    /// <para>
    /// The proportions are not arbitrary. They describe a 1.82 m adult: hips at
    /// 0.92, shoulders at 1.52, eyes just under 1.70. The web prototype's first
    /// attempt used a 0.54 m leg whose foot stopped 28 cm above the turf, and
    /// every figure read as a capsule hovering over the pitch — no amount of
    /// animation could fix it. Getting the skeleton right now means a real model
    /// dropped in later lands at the same scale as the placeholder it replaces,
    /// and the cameras framed against one frame the other.
    /// </para>
    /// </remarks>
    public sealed class HumanoidFactory : MonoBehaviour
    {
        [Header("Real character (leave empty for placeholder)")]
        [Tooltip("A rigged humanoid prefab. See docs/CHARACTER-PIPELINE.md. " +
                 "When set, no placeholder geometry is created.")]
        public GameObject rigged;

        [Header("Kit")]
        public Color primary = new Color(0.11f, 0.17f, 0.29f);
        public Color neon = new Color(0.22f, 0.84f, 1f);
        public Color skin = new Color(0.55f, 0.35f, 0.23f);

        /// <summary>Standing height of the body this factory builds, in metres.</summary>
        public const float Height = 1.82f;
        public const float HipY = 0.92f;
        public const float ShoulderY = 1.52f;

        /// <summary>Build the body and return the handle every other system uses.</summary>
        public CharacterVisual Create(Transform parent, string label)
        {
            if (rigged != null)
            {
                GameObject instance = Instantiate(rigged, parent);
                instance.name = label;
                var real = instance.GetComponent<CharacterVisual>();
                if (real == null) real = instance.AddComponent<CharacterVisual>();
                real.isPlaceholder = false;
                real.Bind();
                return real;
            }

            return BuildPlaceholder(parent, label);
        }

        /// <summary>
        /// A jointed stand-in with correct proportions.
        /// </summary>
        /// <remarks>
        /// Limbs hang from pivots at the joint rather than being rotated about
        /// their own centres. A capsule spun about its middle scissors through the
        /// torso and swings its shoulder end backwards, which is why the naive
        /// version of this looks like a broken puppet no matter how good the
        /// animation curve driving it is.
        /// </remarks>
        private CharacterVisual BuildPlaceholder(Transform parent, string label)
        {
            var root = new GameObject(label);
            root.transform.SetParent(parent, false);

            Material cloth = MakeMaterial(primary, 0.72f);
            Material trim = MakeMaterial(neon, 0.45f, emissive: neon * 0.6f);
            Material flesh = MakeMaterial(skin, 0.78f);
            Material boot = MakeMaterial(new Color(0.07f, 0.08f, 0.1f), 0.4f);

            // Torso: hips to neck.
            Transform torso = Primitive(PrimitiveType.Capsule, root.transform, "Torso", cloth);
            torso.localPosition = new Vector3(0, 1.27f, 0);
            torso.localScale = new Vector3(0.31f, 0.36f, 0.22f);

            Transform head = Primitive(PrimitiveType.Sphere, root.transform, "Head", flesh);
            head.localPosition = new Vector3(0, 1.70f, 0);
            head.localScale = Vector3.one * 0.23f;

            var visual = root.AddComponent<CharacterVisual>();
            visual.isPlaceholder = true;

            visual.leftArm = Limb(root.transform, "ArmL", trim, new Vector3(-0.20f, ShoulderY, 0), 0.634f, 0.104f);
            visual.rightArm = Limb(root.transform, "ArmR", trim, new Vector3(0.20f, ShoulderY, 0), 0.634f, 0.104f);
            visual.leftLeg = Limb(root.transform, "LegL", cloth, new Vector3(-0.093f, HipY, 0), 0.92f, 0.144f, boot);
            visual.rightLeg = Limb(root.transform, "LegR", cloth, new Vector3(0.093f, HipY, 0), 0.92f, 0.144f, boot);
            visual.torso = torso;
            visual.head = head;

            visual.Bind();
            return visual;
        }

        /// <summary>A limb: a pivot at the joint, with the segment hung below it.</summary>
        private static Transform Limb(Transform parent, string name, Material material,
            Vector3 joint, float length, float thickness, Material shoeMaterial = null)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = joint;

            Transform segment = Primitive(PrimitiveType.Capsule, pivot.transform, name + "Segment", material);
            // Unity's capsule is 2 units tall at scale 1, so a scale of length/2
            // gives the requested length. Hung half its length below the joint so
            // rotating the pivot swings it from the shoulder or hip.
            segment.localScale = new Vector3(thickness, length / 2f, thickness);
            segment.localPosition = new Vector3(0, -length / 2f, 0);

            if (shoeMaterial != null)
            {
                Transform shoe = Primitive(PrimitiveType.Cube, pivot.transform, name + "Boot", shoeMaterial);
                shoe.localScale = new Vector3(0.11f, 0.06f, 0.24f);
                shoe.localPosition = new Vector3(0, -length + 0.03f, 0.05f);
            }

            return pivot.transform;
        }

        private static Transform Primitive(PrimitiveType type, Transform parent, string name, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);

            // Placeholder bodies never take part in physics: the ball flight and
            // the save are both resolved analytically in Core, and a stray collider
            // here would introduce a second, non-deterministic opinion about them.
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return go.transform;
        }

        private static Material MakeMaterial(Color colour, float roughness, Color? emissive = null)
        {
            // URP/Lit, falling back to whatever the pipeline offers if the shader is
            // missing — a pink scene is a worse failure than a slightly wrong one.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.color = colour;

            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f - roughness);
            else if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 1f - roughness);

            if (emissive.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emissive.Value);
            }
            return material;
        }
    }
}
