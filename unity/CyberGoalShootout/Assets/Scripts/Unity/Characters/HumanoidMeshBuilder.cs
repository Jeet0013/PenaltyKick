using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyberGoal.Unity.Characters
{
    /// <summary>Hair, as a shell over the scalp. Three cuts, so three heroes are three people.</summary>
    public enum HairStyle
    {
        /// <summary>Short all over. Zero, so a default-constructed shape is not bald.</summary>
        Crop = 0,
        /// <summary>Shaved sides, volume on top swept forward. The footballer's cut.</summary>
        Undercut,
        /// <summary>A band round the sides and back, bare crown.</summary>
        Receding,
        /// <summary>No hair shell at all.</summary>
        None
    }

    /// <summary>
    /// The colours baked into the body's vertex-colour channel.
    /// </summary>
    /// <remarks>
    /// The body is one mesh under one material (§19: human quality balanced
    /// against mobile performance), so shirt, shorts, socks, boots and skin cannot
    /// be separate materials. They are separate vertex colours instead, and the
    /// hems between them are placed on ring boundaries so they stay crisp rather
    /// than smearing across a triangle. Whether the material honours the channel
    /// is decided where the material is built, not here.
    /// </remarks>
    public struct KitColours
    {
        public Color Shirt;
        public Color Shorts;
        public Color Socks;
        public Color Boots;
        public Color Skin;
        public Color Hair;
        /// <summary>Collar, cuffs and sock tops. A thin neon line is most of what makes a kit read as a kit.</summary>
        public Color Trim;
        /// <summary>Skin for most people; glove colour for the keeper.</summary>
        public Color Hands;

        /// <summary>False for a default-constructed struct, whose colours are all transparent black.</summary>
        public bool IsSet => Skin.a > 0f;

        public static KitColours Striker => new KitColours
        {
            Shirt = new Color(0.10f, 0.62f, 0.95f),
            Shorts = new Color(0.06f, 0.08f, 0.16f),
            Socks = new Color(0.10f, 0.62f, 0.95f),
            Boots = new Color(0.05f, 0.05f, 0.07f),
            Skin = new Color(0.55f, 0.35f, 0.23f),
            Hair = new Color(0.08f, 0.06f, 0.05f),
            Trim = new Color(0.22f, 0.84f, 1.00f),
            Hands = new Color(0.55f, 0.35f, 0.23f)
        };

        /// <summary>Keepers wear a different colour to everyone, by law, and white gloves.</summary>
        public static KitColours Goalkeeper => new KitColours
        {
            Shirt = new Color(0.90f, 0.25f, 0.55f),
            Shorts = new Color(0.10f, 0.06f, 0.12f),
            Socks = new Color(0.90f, 0.25f, 0.55f),
            Boots = new Color(0.06f, 0.05f, 0.08f),
            Skin = new Color(0.82f, 0.62f, 0.50f),
            Hair = new Color(0.30f, 0.20f, 0.12f),
            Trim = new Color(1.00f, 0.85f, 0.30f),
            Hands = new Color(0.95f, 0.95f, 0.98f)
        };

        /// <summary>Black with a yellow line, and grey hair — the referee should never be mistaken for a player.</summary>
        public static KitColours Referee => new KitColours
        {
            Shirt = new Color(0.08f, 0.08f, 0.09f),
            Shorts = new Color(0.07f, 0.07f, 0.08f),
            Socks = new Color(0.08f, 0.08f, 0.09f),
            Boots = new Color(0.05f, 0.05f, 0.06f),
            Skin = new Color(0.72f, 0.52f, 0.40f),
            Hair = new Color(0.62f, 0.62f, 0.64f),
            Trim = new Color(0.95f, 0.95f, 0.30f),
            Hands = new Color(0.72f, 0.52f, 0.40f)
        };

        public static KitColours Default => Striker;
    }

    /// <summary>Body shape, so the three hero characters are not the same person.</summary>
    public struct BodyShape
    {
        /// <summary>Scales the whole figure. 1.0 is 1.82 m.</summary>
        public float Height;
        /// <summary>Shoulder width multiplier. Athletes are wide here.</summary>
        public float Shoulders;
        /// <summary>Waist and midsection multiplier.</summary>
        public float Girth;
        /// <summary>Limb thickness multiplier.</summary>
        public float Limbs;
        public HairStyle Hair;
        /// <summary>Left unset, <see cref="HumanoidMeshBuilder.Build(Transform, BodyShape, string)"/> uses <see cref="KitColours.Default"/>.</summary>
        public KitColours Kit;

        public static BodyShape Striker => new BodyShape
        {
            Height = 1.00f, Shoulders = 1.00f, Girth = 0.94f, Limbs = 1.02f,
            Hair = HairStyle.Crop, Kit = KitColours.Striker
        };

        /// <summary>Keepers are the tallest people on the pitch, and it matters.</summary>
        public static BodyShape Goalkeeper => new BodyShape
        {
            Height = 1.05f, Shoulders = 1.08f, Girth = 1.00f, Limbs = 1.05f,
            Hair = HairStyle.Undercut, Kit = KitColours.Goalkeeper
        };

        /// <summary>Older, heavier, shorter — reads as a different role at a glance.</summary>
        public static BodyShape Referee => new BodyShape
        {
            Height = 0.96f, Shoulders = 0.94f, Girth = 1.16f, Limbs = 0.98f,
            Hair = HairStyle.Receding, Kit = KitColours.Referee
        };

        public static BodyShape Spectator(float seed)
        {
            // Deterministic per-spectator variation, so a crowd is people rather
            // than one person copied a thousand times (§8 of the art direction).
            float a = Frac(seed * 12.9898f);
            float b = Frac(seed * 78.233f);
            float c = Frac(seed * 37.719f);
            return new BodyShape
            {
                Height = 0.88f + a * 0.22f,
                Shoulders = 0.88f + b * 0.26f,
                Girth = 0.85f + c * 0.40f,
                Limbs = 0.90f + a * 0.22f,
                Hair = (HairStyle)(int)(Frac(seed * 5.417f) * 4f)
            };
        }

        private static float Frac(float v) => v - Mathf.Floor(v);
    }

    /// <summary>
    /// Generates a skinned humanoid mesh and the bones that drive it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why generate a mesh instead of using capsules.</b> §21 forbids shipping
    /// capsules, cubes and spheres as final artwork, and the reason is not
    /// snobbery: a capsule has no shoulders, so a figure made of them cannot read
    /// as a person at any distance. What follows builds an actual body — a
    /// tapered torso that is wider than it is deep, deltoids that stand proud of
    /// the chest, limbs that thin toward the wrist and ankle, a neck, a face,
    /// hair, hands with fingers, and boots.
    /// </para>
    /// <para>
    /// <b>Elliptical cross-sections do most of the work.</b> A human torso is far
    /// wider than it is deep — roughly 0.19 m against 0.11 m at the chest. Loft it
    /// with circular rings and you get a tube-person no amount of texturing
    /// rescues. Two radii per ring is the single cheapest thing that makes a
    /// generated body look human. Rings are superellipses rather than pure
    /// ellipses where the real cross-section is boxy (chest, palm, boot).
    /// </para>
    /// <para>
    /// <b>The face is a displaced ellipsoid, not extra meshes.</b> A head that is
    /// a featureless egg is what makes a figure read as a mannequin, however good
    /// the body. Brow, eye sockets, nose, cheekbones, mouth and chin are
    /// displacements of the skull ellipsoid's own vertices along an anatomical
    /// profile, with eyebrows, eyes and lips tinted in the vertex colour. That
    /// costs no vertices beyond the skull itself.
    /// </para>
    /// <para>
    /// <b>One mesh, one material, one draw call.</b> Kit zones are vertex
    /// colours. About 3,000 vertices per character, all with a bone weight and a
    /// bind pose, so hair, fingers and face follow the animation. It is still
    /// generated rather than scanned art, and animates through the same
    /// <see cref="CharacterVisual"/> API a MakeHuman export will use, bound to
    /// the same skeleton — so replacing it remains a single field.
    /// </para>
    /// </remarks>
    public static class HumanoidMeshBuilder
    {
        // Segment counts per ring. The torso and head carry the silhouette so they
        // get the most; a finger at gameplay distance is two pixels wide.
        private const int TorsoSegments = 18;
        private const int LimbSegments = 14;
        private const int HeadSegments = 28;
        private const int HeadStacks = 18;
        private const int HairRows = 8;
        private const int EarSegments = 8;
        private const int EarStacks = 4;
        private const int PalmSegments = 12;
        private const int FingerSegments = 8;
        private const int BootSegments = 12;

        /// <summary>Toward the goal. The face, toes and thumbs point this way.</summary>
        private static readonly Vector3 Forward = new Vector3(0f, 0f, -1f);

        public sealed class Built
        {
            public Mesh Mesh;
            public Transform[] Bones;
            public Transform Root;
        }

        /// <summary>Build the mesh, the bone transforms, and bind them together.</summary>
        public static Built Build(Transform parent, BodyShape shape, string name)
            => Build(parent, shape, name, shape.Kit.IsSet ? shape.Kit : KitColours.Default);

        /// <summary>As <see cref="Build(Transform, BodyShape, string)"/>, with the kit colours given explicitly.</summary>
        public static Built Build(Transform parent, BodyShape shape, string name, KitColours kit)
        {
            Transform[] bones = BuildBones(parent, shape, name);
            Mesh mesh = BuildMesh(shape, kit, bones);
            return new Built { Mesh = mesh, Bones = bones, Root = bones[(int)Bone.Root] };
        }

        private static Transform[] BuildBones(Transform parent, BodyShape shape, string name)
        {
            var bones = new Transform[(int)Bone.Count];

            for (int i = 0; i < (int)Bone.Count; i++)
            {
                var go = new GameObject($"{name}_{Skeleton.NameOf((Bone)i)}");
                bones[i] = go.transform;
            }

            for (int i = 0; i < (int)Bone.Count; i++)
            {
                var bone = (Bone)i;
                Transform target = bone == Bone.Root ? parent : bones[(int)Skeleton.Parents[i]];
                bones[i].SetParent(target, false);
            }

            // Positions are set as local offsets from the parent joint, which is
            // what makes rotating a bone swing everything below it.
            for (int i = 0; i < (int)Bone.Count; i++)
            {
                var bone = (Bone)i;
                Vector3 world = Scaled(Skeleton.Joints[i], shape);
                if (bone == Bone.Root)
                {
                    bones[i].localPosition = Vector3.zero;
                }
                else
                {
                    Vector3 parentWorld = Scaled(Skeleton.Joints[(int)Skeleton.Parents[i]], shape);
                    bones[i].localPosition = world - parentWorld;
                }
                bones[i].localRotation = Quaternion.identity;
            }

            return bones;
        }

        private static Vector3 Scaled(Vector3 joint, BodyShape shape)
            => new Vector3(joint.x * shape.Shoulders, joint.y * shape.Height, joint.z);

        private static Vector3 Joint(Bone bone, BodyShape shape) => Scaled(Skeleton.Joints[(int)bone], shape);

        private static float JointY(Bone bone) => Skeleton.Joints[(int)bone].y;

        // ------------------------------------------------------------------
        // Mesh assembly
        // ------------------------------------------------------------------

        private static Mesh BuildMesh(BodyShape shape, KitColours kit, Transform[] bones)
        {
            var m = new MeshData();

            AppendTorso(m, shape, kit);
            AppendNeck(m, shape, kit);

            AppendArm(m, shape, kit, Bone.ShoulderL, Bone.UpperArmL, Bone.ForearmL, Bone.HandL);
            AppendArm(m, shape, kit, Bone.ShoulderR, Bone.UpperArmR, Bone.ForearmR, Bone.HandR);
            AppendHand(m, shape, kit, Bone.ForearmL, Bone.HandL, +1f);
            AppendHand(m, shape, kit, Bone.ForearmR, Bone.HandR, -1f);

            AppendLeg(m, shape, kit, Bone.ThighL, Bone.ShinL, Bone.FootL);
            AppendLeg(m, shape, kit, Bone.ThighR, Bone.ShinR, Bone.FootR);
            AppendBoot(m, shape, kit, Bone.FootL);
            AppendBoot(m, shape, kit, Bone.FootR);

            AppendHead(m, shape, kit);
            AppendEar(m, shape, kit, -1f);
            AppendEar(m, shape, kit, +1f);
            AppendHair(m, shape, kit);

            var mesh = new Mesh { name = "Humanoid" };
            // 16-bit indices cap at 65535 vertices; this body is around 3,000, so
            // the default index format is fine and cheaper on mobile.
            mesh.SetVertices(m.Vertices);
            mesh.SetUVs(0, m.Uvs);
            mesh.SetColors(m.Colors);
            mesh.SetTriangles(m.Triangles, 0);
            mesh.boneWeights = m.Weights.ToArray();
            mesh.bindposes = BindPoses(bones);
            // Normals come from the triangles rather than from analytic ring
            // normals: the displaced face and the calf bulge have no closed-form
            // normal, and every ring here is welded so the result is smooth.
            // Hem rings are duplicated and unstitched, so a hem gets a crease —
            // which is what a hem looks like.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------
        // Torso, neck, limbs
        // ------------------------------------------------------------------

        private static void AppendTorso(MeshData m, BodyShape shape, KitColours kit)
        {
            float g = shape.Girth;
            // Girth above 1 goes on as a paunch — extra depth, pushed forward —
            // rather than uniform scaling, which would just make a bigger athlete.
            float paunch = Mathf.Max(0f, g - 1f);
            Vector3 u = Vector3.right;
            Vector3 v = Forward;

            var rings = new List<RingSpec>();

            void Row(float y, float halfWidth, float halfDepth, float girthShare, float belly,
                Color colour, float exponent, bool split = false)
            {
                float k = 1f + (g - 1f) * girthShare;
                float depth = halfDepth * k + paunch * 0.18f * belly;
                Vector3 centre = new Vector3(0f, y * shape.Height, 0f) + Forward * (paunch * 0.09f * belly);
                rings.Add(new RingSpec(centre, u, v, new Vector2(halfWidth * k, depth), exponent,
                    colour, TorsoWeight(y), split));
            }

            // Hips → waist → chest → shoulder line. Broadest across the clavicles,
            // narrower at the sternum, narrowest at the waist; a torso lofted as
            // one cone from hips to neck has no chest at all.
            Row(0.840f, 0.150f, 0.098f, 1.00f, 0.20f, kit.Shorts, 2.2f);
            Row(0.920f, 0.176f, 0.106f, 1.00f, 0.50f, kit.Shorts, 2.2f);
            Row(1.000f, 0.160f, 0.100f, 1.00f, 0.90f, kit.Shorts, 2.3f, split: true);
            Row(1.000f, 0.160f, 0.100f, 1.00f, 0.90f, kit.Shirt, 2.3f);
            Row(1.060f, 0.150f, 0.098f, 1.00f, 1.00f, kit.Shirt, 2.3f);
            Row(1.130f, 0.154f, 0.102f, 1.00f, 0.90f, kit.Shirt, 2.3f);
            Row(1.220f, 0.170f, 0.108f, 0.70f, 0.50f, kit.Shirt, 2.4f);
            Row(1.310f, 0.188f, 0.114f, 0.40f, 0.15f, kit.Shirt, 2.4f);
            Row(1.400f, 0.198f, 0.112f, 0.25f, 0.00f, kit.Shirt, 2.4f);
            Row(1.470f, 0.206f, 0.104f, 0.10f, 0.00f, kit.Shirt, 2.4f);
            Row(1.515f, 0.168f, 0.090f, 0.10f, 0.00f, kit.Shirt, 2.2f);
            Row(1.550f, 0.070f, 0.064f, 0.00f, 0.00f, kit.Trim, 2.0f);

            int first = m.Loft(rings, TorsoSegments).first;

            // Close the pelvis, or the camera can see up into a hollow body when
            // the keeper dives.
            int centreIndex = m.Add(new Vector3(0f, 0.835f * shape.Height, 0f), new Vector2(0.5f, 0f),
                kit.Shorts, Single(Bone.Hips));
            m.Fan(centreIndex, first, TorsoSegments, u, v, -Vector3.up);
        }

        private static void AppendNeck(MeshData m, BodyShape shape, KitColours kit)
        {
            var stations = new[]
            {
                new Station(0.00f, 0.058f, 0.060f, kit.Skin),
                new Station(0.50f, 0.054f, 0.058f, kit.Skin),
                new Station(1.00f, 0.052f, 0.058f, kit.Skin)
            };
            LoftBones(m, shape, Bone.Neck, Bone.Head, stations, 2f, LimbSegments);
        }

        private static void AppendArm(MeshData m, BodyShape shape, KitColours kit,
            Bone shoulder, Bone upperArm, Bone forearm, Bone hand)
        {
            float l = shape.Limbs;

            // The deltoid is the widest point of the arm and sits outside the chest.
            LoftBones(m, shape, shoulder, upperArm, new[]
            {
                new Station(0.0f, 0.060f * l, 0.064f * l, kit.Shirt),
                new Station(0.5f, 0.074f * l, 0.072f * l, kit.Shirt),
                new Station(1.0f, 0.064f * l, 0.062f * l, kit.Shirt)
            }, 2f, LimbSegments);

            // Sleeve hem at mid-bicep, a cuff line just above it, bicep belly below.
            float yUpper = JointY(upperArm), yElbow = JointY(forearm), yWrist = JointY(hand);
            LoftBones(m, shape, upperArm, forearm, new[]
            {
                Station.AtHeight(1.470f, yUpper, yElbow, 0.062f * l, 0.060f * l, kit.Shirt),
                Station.AtHeight(1.420f, yUpper, yElbow, 0.060f * l, 0.059f * l, kit.Shirt),
                Station.AtHeight(1.385f, yUpper, yElbow, 0.058f * l, 0.057f * l, kit.Shirt),
                Station.AtHeight(1.370f, yUpper, yElbow, 0.057f * l, 0.056f * l, kit.Trim, split: true),
                Station.AtHeight(1.370f, yUpper, yElbow, 0.056f * l, 0.055f * l, kit.Skin),
                Station.AtHeight(1.310f, yUpper, yElbow, 0.056f * l, 0.054f * l, kit.Skin),
                Station.AtHeight(1.250f, yUpper, yElbow, 0.050f * l, 0.049f * l, kit.Skin),
                Station.AtHeight(1.190f, yUpper, yElbow, 0.046f * l, 0.047f * l, kit.Skin)
            }, 2f, LimbSegments);

            // Thickest just below the elbow, then a steady taper to a wrist that is
            // wider than it is deep. A straight forearm cone reads as a pipe.
            LoftBones(m, shape, forearm, hand, new[]
            {
                Station.AtHeight(1.190f, yElbow, yWrist, 0.047f * l, 0.048f * l, kit.Skin),
                Station.AtHeight(1.140f, yElbow, yWrist, 0.050f * l, 0.049f * l, kit.Skin),
                Station.AtHeight(1.080f, yElbow, yWrist, 0.045f * l, 0.044f * l, kit.Skin),
                Station.AtHeight(1.020f, yElbow, yWrist, 0.038f * l, 0.036f * l, kit.Skin),
                Station.AtHeight(0.970f, yElbow, yWrist, 0.032f * l, 0.028f * l, kit.Skin),
                Station.AtHeight(0.940f, yElbow, yWrist, 0.030f * l, 0.026f * l, kit.Skin)
            }, 2f, LimbSegments);
        }

        private static void AppendLeg(MeshData m, BodyShape shape, KitColours kit,
            Bone thigh, Bone shin, Bone foot)
        {
            float l = shape.Limbs;
            float yHip = JointY(thigh), yKnee = JointY(shin), yAnkle = JointY(foot);

            // Shorts hem at mid-thigh; the thigh above it is shorts-coloured, which
            // also hides where the thigh tube meets the pelvis.
            LoftBones(m, shape, thigh, shin, new[]
            {
                Station.AtHeight(0.920f, yHip, yKnee, 0.088f * l, 0.090f * l, kit.Shorts),
                Station.AtHeight(0.860f, yHip, yKnee, 0.090f * l, 0.092f * l, kit.Shorts),
                Station.AtHeight(0.780f, yHip, yKnee, 0.086f * l, 0.088f * l, kit.Shorts),
                Station.AtHeight(0.700f, yHip, yKnee, 0.080f * l, 0.082f * l, kit.Shorts),
                Station.AtHeight(0.680f, yHip, yKnee, 0.079f * l, 0.081f * l, kit.Shorts, split: true),
                Station.AtHeight(0.680f, yHip, yKnee, 0.079f * l, 0.081f * l, kit.Skin),
                Station.AtHeight(0.620f, yHip, yKnee, 0.074f * l, 0.076f * l, kit.Skin),
                Station.AtHeight(0.560f, yHip, yKnee, 0.067f * l, 0.068f * l, kit.Skin),
                Station.AtHeight(0.500f, yHip, yKnee, 0.060f * l, 0.062f * l, kit.Skin)
            }, 2f, LimbSegments);

            // Sock top just under the knee, then the calf: its peak is a third of
            // the way down and deeper than it is wide, the opposite of the thigh.
            LoftBones(m, shape, shin, foot, new[]
            {
                Station.AtHeight(0.500f, yKnee, yAnkle, 0.060f * l, 0.062f * l, kit.Skin),
                Station.AtHeight(0.455f, yKnee, yAnkle, 0.061f * l, 0.064f * l, kit.Skin, split: true),
                Station.AtHeight(0.455f, yKnee, yAnkle, 0.061f * l, 0.064f * l, kit.Trim),
                Station.AtHeight(0.420f, yKnee, yAnkle, 0.065f * l, 0.071f * l, kit.Socks),
                Station.AtHeight(0.370f, yKnee, yAnkle, 0.068f * l, 0.076f * l, kit.Socks),
                Station.AtHeight(0.310f, yKnee, yAnkle, 0.063f * l, 0.071f * l, kit.Socks),
                Station.AtHeight(0.240f, yKnee, yAnkle, 0.052f * l, 0.057f * l, kit.Socks),
                Station.AtHeight(0.160f, yKnee, yAnkle, 0.041f * l, 0.045f * l, kit.Socks),
                Station.AtHeight(0.075f, yKnee, yAnkle, 0.036f * l, 0.040f * l, kit.Socks)
            }, 2f, LimbSegments);
        }

        /// <summary>Loft a superelliptical tube between two joints through the given stations.</summary>
        private static void LoftBones(MeshData m, BodyShape shape, Bone from, Bone to,
            Station[] stations, float exponent, int segments)
        {
            Vector3 a = Joint(from, shape);
            Vector3 b = Joint(to, shape);
            Vector3 axis = (b - a).normalized;
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.up;

            // Lateral first, depth second, so a station's half-width really is
            // side-to-side. Crossing with 'right' for a vertical limb hands back a
            // frame whose first axis is depth, and the whole body comes out deeper
            // than it is wide — the exact opposite of a human.
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Forward)) < 0.9f ? Forward : Vector3.up;
            Vector3 u = Vector3.Cross(reference, axis).normalized;
            Vector3 v = Vector3.Cross(axis, u).normalized;

            var rings = new List<RingSpec>(stations.Length);
            foreach (Station s in stations)
            {
                rings.Add(new RingSpec(Vector3.Lerp(a, b, s.T), u, v, s.Radius, exponent,
                    s.Colour, Blend(from, to, s.T), s.Split));
            }
            m.Loft(rings, segments);
        }

        // ------------------------------------------------------------------
        // Head, ears, hair
        // ------------------------------------------------------------------

        /// <summary>Skull half-extents. Taller than wide, deeper than wide — a head, not a ball.</summary>
        private static readonly Vector3 SkullRadius = new Vector3(0.078f, 0.108f, 0.094f);

        private static Vector3 HeadCentre(BodyShape shape) => Joint(Bone.Head, shape) + Vector3.up * 0.075f;

        /// <summary>The result of sampling the head surface in one direction.</summary>
        private readonly struct HeadSample
        {
            /// <summary>Offset from the head centre.</summary>
            public readonly Vector3 Position;
            /// <summary>Outward direction of the underlying skull ellipsoid; hair is offset along this.</summary>
            public readonly Vector3 Normal;
            public readonly float Eyes;
            public readonly float Brows;
            public readonly float Lips;

            public HeadSample(Vector3 position, Vector3 normal, float eyes, float brows, float lips)
            {
                Position = position;
                Normal = normal;
                Eyes = eyes;
                Brows = brows;
                Lips = lips;
            }
        }

        /// <summary>
        /// The skull ellipsoid displaced into a face, for a unit direction.
        /// </summary>
        /// <remarks>
        /// Each feature is a smooth bump or dent in the direction of the ellipsoid
        /// normal, gated to the front of the head. Sizes are real: a 2 cm nose, a
        /// 1 cm eye socket, a 7 mm brow. They are small against an 8 cm skull, and
        /// that is the point — features scaled up to be visible at range make a
        /// caricature, whereas correct ones read as a face from the shading alone.
        /// </remarks>
        private static HeadSample SampleHead(Vector3 unit)
        {
            Vector3 r = SkullRadius;
            float f = -unit.z;                       // 1 at the face, -1 at the back
            float lat = Mathf.Abs(unit.x);
            float lower = Mathf.Clamp01((-unit.y - 0.05f) / 0.95f);

            // The jaw narrows toward the chin and the back of the skull tucks in
            // to meet the neck. Without this the bottom of the head is a second,
            // smaller crown.
            float jaw = 1f - 0.22f * lower * lower;
            float nape = unit.z > 0f ? 1f - 0.18f * lower : 1f;
            var p = new Vector3(unit.x * r.x * jaw, unit.y * r.y, unit.z * r.z * nape);
            Vector3 n = new Vector3(unit.x / (r.x * r.x), unit.y / (r.y * r.y), unit.z / (r.z * r.z)).normalized;

            float front = Mathf.Clamp01((f - 0.45f) / 0.40f);
            float d = 0f;

            float brow = Bell(unit.y - 0.24f, 0.10f) * Mathf.Clamp01((0.80f - lat) / 0.25f) * front;
            d += 0.007f * brow;

            float eyes = Bell(lat - 0.40f, 0.17f) * Bell(unit.y - 0.10f, 0.11f) * front;
            d -= 0.011f * eyes;

            float noseRun = Mathf.Clamp01((0.20f - unit.y) / 0.32f) * Mathf.Clamp01((unit.y + 0.32f) / 0.10f);
            d += 0.022f * noseRun * Bell(unit.x, 0.12f + 0.06f * noseRun) * Mathf.Clamp01((f - 0.70f) / 0.25f);

            d += 0.004f * Bell(lat - 0.62f, 0.16f) * Bell(unit.y + 0.02f, 0.16f) * Mathf.Clamp01((f - 0.15f) / 0.40f);

            float lips = Bell(unit.y + 0.46f, 0.05f) * Mathf.Clamp01((0.32f - lat) / 0.15f)
                         * Mathf.Clamp01((f - 0.70f) / 0.20f);
            d -= 0.003f * lips;

            d += 0.007f * Bell(unit.x, 0.28f) * Bell(unit.y + 0.74f, 0.16f) * Mathf.Clamp01((f - 0.35f) / 0.40f);

            d -= 0.004f * Bell(lat - 0.88f, 0.15f) * Bell(unit.y - 0.28f, 0.18f) * Mathf.Clamp01((f - 0.10f) / 0.40f);

            float brows = Bell(unit.y - 0.27f, 0.05f) * Bell(lat - 0.42f, 0.22f) * front;
            return new HeadSample(p + n * d, n, eyes * eyes, brows, lips);
        }

        /// <summary>Unit direction on the skull for a segment and stack, with segments crowded toward the face.</summary>
        private static Vector3 SkullDirection(int segment, float phi)
        {
            float raw = segment / (float)HeadSegments * Mathf.PI * 2f;
            // Compress the angular spacing at the front (sin θ = -1 is -Z, the
            // face) and stretch it at the back. The nose is three vertices wide
            // this way; sampled evenly it would be one, and a one-vertex nose is a
            // spike.
            const float faceTheta = Mathf.PI * 1.5f;
            float theta = raw - 0.4f * Mathf.Sin(raw - faceTheta);
            float sp = Mathf.Sin(phi);
            return new Vector3(sp * Mathf.Cos(theta), Mathf.Cos(phi), sp * Mathf.Sin(theta));
        }

        private static void AppendHead(MeshData m, BodyShape shape, KitColours kit)
        {
            Vector3 centre = HeadCentre(shape);
            BoneWeight w = Single(Bone.Head);
            var eye = new Color(0.06f, 0.05f, 0.05f);
            Color lip = Color.Lerp(kit.Skin, new Color(0.55f, 0.18f, 0.20f), 0.55f);

            Color Tint(HeadSample s)
            {
                Color c = Color.Lerp(kit.Skin, kit.Hair, s.Brows * 0.85f);
                c = Color.Lerp(c, eye, s.Eyes);
                return Color.Lerp(c, lip, s.Lips);
            }

            int top = m.Add(centre + SampleHead(Vector3.up).Position, new Vector2(0.5f, 0f), kit.Skin, w);

            int previous = -1;
            for (int k = 1; k < HeadStacks; k++)
            {
                float phi = k / (float)HeadStacks * Mathf.PI;
                int ring = m.Vertices.Count;
                for (int s = 0; s < HeadSegments; s++)
                {
                    HeadSample sample = SampleHead(SkullDirection(s, phi));
                    m.Add(centre + sample.Position, new Vector2(s / (float)HeadSegments, phi / Mathf.PI),
                        Tint(sample), w);
                }

                if (k == 1) m.Fan(top, ring, HeadSegments, Vector3.right, Vector3.forward, Vector3.up);
                else m.Stitch(previous, ring, HeadSegments, Vector3.right, Vector3.forward, -Vector3.up);
                previous = ring;
            }

            int bottom = m.Add(centre + SampleHead(-Vector3.up).Position, new Vector2(0.5f, 1f), kit.Skin, w);
            m.Fan(bottom, previous, HeadSegments, Vector3.right, Vector3.forward, -Vector3.up);
        }

        /// <summary>A small flattened ellipsoid, half sunk into the side of the skull.</summary>
        private static void AppendEar(MeshData m, BodyShape shape, KitColours kit, float side)
        {
            // Slightly behind the centre line: an ear on the equator of the skull
            // looks pasted on.
            Vector3 centre = HeadCentre(shape) + new Vector3(side * 0.075f, 0.0f, 0.010f);
            var radius = new Vector3(0.010f, 0.020f, 0.014f);
            BoneWeight w = Single(Bone.Head);

            int top = m.Add(centre + Vector3.up * radius.y, new Vector2(0.5f, 0f), kit.Skin, w);
            int previous = -1;
            for (int k = 1; k < EarStacks; k++)
            {
                float phi = k / (float)EarStacks * Mathf.PI;
                int ring = m.Vertices.Count;
                for (int s = 0; s < EarSegments; s++)
                {
                    float theta = s / (float)EarSegments * Mathf.PI * 2f;
                    var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));
                    m.Add(centre + Vector3.Scale(unit, radius),
                        new Vector2(s / (float)EarSegments, phi / Mathf.PI), kit.Skin, w);
                }
                if (k == 1) m.Fan(top, ring, EarSegments, Vector3.right, Vector3.forward, Vector3.up);
                else m.Stitch(previous, ring, EarSegments, Vector3.right, Vector3.forward, -Vector3.up);
                previous = ring;
            }
            int bottom = m.Add(centre - Vector3.up * radius.y, new Vector2(0.5f, 1f), kit.Skin, w);
            m.Fan(bottom, previous, EarSegments, Vector3.right, Vector3.forward, -Vector3.up);
        }

        /// <summary>Where the hair stops, as a height on the unit skull, given how far round toward the face we are.</summary>
        private static float Hairline(HairStyle style, float facing)
        {
            float front, side, back;
            switch (style)
            {
                case HairStyle.Undercut: front = 0.55f; side = 0.32f; back = -0.22f; break;
                // Above the crown at the front, so the forehead runs bare well
                // past the temples before the band starts.
                case HairStyle.Receding: front = 0.95f; side = 0.12f; back = -0.35f; break;
                default: front = 0.50f; side = 0.20f; back = -0.30f; break;
            }
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Abs(facing));
            return facing >= 0f ? Mathf.Lerp(side, front, k) : Mathf.Lerp(side, back, k);
        }

        /// <summary>How far the hair shell stands off the scalp at a point.</summary>
        private static float HairThickness(HairStyle style, Vector3 unit)
        {
            switch (style)
            {
                case HairStyle.Undercut:
                    // Shaved to 4 mm at the sides, 3 cm on top, and a quiff that
                    // rises toward the front. The quiff is a function of the
                    // vertex's own direction, not its column's heading: at the
                    // crown every column meets at one point, and a per-column
                    // term there spreads that point up a vertical spike.
                    return Mathf.Lerp(0.004f, 0.030f, Mathf.Clamp01((unit.y - 0.30f) / 0.40f))
                           + 0.020f * Mathf.Clamp01(-unit.z * 2.5f) * Mathf.Clamp01((unit.y - 0.45f) / 0.35f);
                case HairStyle.Receding:
                    return 0.006f;
                default:
                    return 0.012f;
            }
        }

        /// <summary>
        /// A shell over the scalp, offset along the skull normal.
        /// </summary>
        /// <remarks>
        /// Rows run from the crown (or, for a receding cut, a ring below it) down
        /// to the hairline, and the shell's thickness fades to zero on its last
        /// row so its edge sits on the skin rather than showing an open lip. A
        /// thin shell painted a different colour is enough: nobody can resolve
        /// individual hair at gameplay distance, but everyone can see a bald egg.
        /// </remarks>
        private static void AppendHair(MeshData m, BodyShape shape, KitColours kit)
        {
            HairStyle style = shape.Hair;
            if (style == HairStyle.None) return;

            Vector3 centre = HeadCentre(shape);
            BoneWeight w = Single(Bone.Head);
            bool bareCrown = style == HairStyle.Receding;
            float phiTop = bareCrown ? Mathf.Acos(0.42f) : 0f;

            int previous = -1;
            for (int row = 0; row <= HairRows; row++)
            {
                float rowT = row / (float)HairRows;
                float fade = Mathf.Min(1f, (HairRows - row) / 1.5f);
                if (bareCrown) fade = Mathf.Min(fade, row / 1.5f);

                int ring = m.Vertices.Count;
                for (int s = 0; s < HeadSegments; s++)
                {
                    // The hairline is a function of heading only, so evaluate it on
                    // the horizontal direction for this segment.
                    Vector3 heading = SkullDirection(s, Mathf.PI * 0.5f);
                    float facing = -heading.z;
                    float phiBottom = Mathf.Acos(Mathf.Clamp(Hairline(style, facing), -1f, 1f));
                    phiBottom = Mathf.Max(phiBottom, phiTop);

                    float phi = Mathf.Lerp(phiTop, phiBottom, rowT);
                    Vector3 unit = SkullDirection(s, phi);
                    HeadSample scalp = SampleHead(unit);
                    // Where the band is only a sliver tall (a receding cut, just
                    // past the temples) flatten it, or the rows stack into a fin
                    // standing off the scalp.
                    float extent = Mathf.Clamp01((phiBottom - phiTop) / 0.25f);
                    float thickness = HairThickness(style, unit) * fade * extent;

                    m.Add(centre + scalp.Position + scalp.Normal * thickness,
                        new Vector2(s / (float)HeadSegments, rowT), kit.Hair, w);
                }

                if (row > 0) m.Stitch(previous, ring, HeadSegments, Vector3.right, Vector3.forward, -Vector3.up);
                previous = ring;
            }
        }

        // ------------------------------------------------------------------
        // Hands and boots
        // ------------------------------------------------------------------

        /// <summary>
        /// A palm and five digits, hanging from the wrist.
        /// </summary>
        /// <remarks>
        /// The palm is a flattened slab — three times wider than it is deep — and
        /// that ratio is what separates a hand from a mitten. Fingers are four
        /// short tubes with a resting curl toward the palm, and the thumb leaves
        /// from the front edge, because at rest the thumb points the way the
        /// person faces. There are no finger bones; the whole hand follows
        /// <paramref name="hand"/>, which is right for every pose this game has.
        /// </remarks>
        /// <param name="medial">+1 for the left hand, -1 for the right: which way the palm faces.</param>
        private static void AppendHand(MeshData m, BodyShape shape, KitColours kit,
            Bone forearm, Bone hand, float medial)
        {
            float l = shape.Limbs;
            Vector3 wrist = Joint(hand, shape);
            Vector3 down = (wrist - Joint(forearm, shape)).normalized;
            Vector3 fwd = Forward;
            Vector3 med = Vector3.right * medial;
            Color c = kit.Hands;
            BoneWeight w = Single(hand);

            var palm = new List<RingSpec>
            {
                new RingSpec(wrist, fwd, med, new Vector2(0.030f, 0.024f) * l, 2.0f, c, w),
                new RingSpec(wrist + down * (0.030f * l), fwd, med, new Vector2(0.040f, 0.017f) * l, 2.6f, c, w),
                new RingSpec(wrist + down * (0.065f * l), fwd, med, new Vector2(0.044f, 0.016f) * l, 2.8f, c, w),
                new RingSpec(wrist + down * (0.090f * l), fwd, med, new Vector2(0.043f, 0.015f) * l, 2.8f, c, w)
            };
            int knuckles = m.Loft(palm, PalmSegments).last;
            int palmCap = m.Add(wrist + down * (0.093f * l), new Vector2(0.5f, 1f), c, w);
            m.Fan(palmCap, knuckles, PalmSegments, fwd, med, down);

            // Index at the front, little finger at the back.
            float[] offsets = { 0.031f, 0.010f, -0.010f, -0.031f };
            float[] lengths = { 0.072f, 0.080f, 0.074f, 0.058f };
            float[] radii = { 0.0085f, 0.0085f, 0.0080f, 0.0072f };
            for (int i = 0; i < 4; i++)
            {
                Vector3 root = wrist + down * (0.088f * l) + fwd * (offsets[i] * l);
                AppendDigit(m, root, down, med, fwd, lengths[i] * l, radii[i] * l, 0.30f, c, w);
            }

            Vector3 thumbRoot = wrist + down * (0.030f * l) + fwd * (0.030f * l) + med * (0.004f * l);
            Vector3 thumbDir = (down * 0.50f + fwd * 0.72f + med * 0.42f).normalized;
            Vector3 thumbU = Vector3.Cross(thumbDir, med).normalized;
            AppendDigit(m, thumbRoot, thumbDir, med, thumbU, 0.060f * l, 0.010f * l, 0.25f, c, w);
        }

        /// <summary>A short tapered tube that curls toward <paramref name="curl"/>, closed at the tip.</summary>
        private static void AppendDigit(MeshData m, Vector3 root, Vector3 dir, Vector3 curl, Vector3 u,
            float length, float radius, float curlAmount, Color colour, BoneWeight w)
        {
            const int rings = 4;
            var points = new Vector3[rings];
            for (int k = 0; k < rings; k++)
            {
                float t = k / (float)(rings - 1);
                points[k] = root + dir * (length * t) + curl * (length * curlAmount * t * t);
            }

            var specs = new List<RingSpec>(rings);
            Vector3 lastAxis = dir;
            for (int k = 0; k < rings; k++)
            {
                float t = k / (float)(rings - 1);
                Vector3 axis = k < rings - 1 ? (points[k + 1] - points[k]).normalized : lastAxis;
                lastAxis = axis;
                Vector3 v = Vector3.Cross(axis, u).normalized;
                float r = radius * (1f - 0.15f * t);
                specs.Add(new RingSpec(points[k], u, v, new Vector2(r, r * 0.9f), 2f, colour, w));
            }
            int last = m.Loft(specs, FingerSegments).last;

            int tip = m.Add(points[rings - 1] + lastAxis * (radius * 0.7f), new Vector2(0.5f, 1f), colour, w);
            m.Fan(tip, last, FingerSegments, u, Vector3.Cross(lastAxis, u).normalized, lastAxis);
        }

        /// <summary>A boot: a rounded-box loft from heel to toe, sole flat on the turf.</summary>
        private static void AppendBoot(MeshData m, BodyShape shape, KitColours kit, Bone foot)
        {
            float x = Joint(foot, shape).x;
            Color c = kit.Boots;
            BoneWeight w = Single(foot);
            Vector3 u = Vector3.right;
            Vector3 v = Vector3.up;

            // (z, half-width, half-height). Centre y equals half-height, so every
            // ring's underside is on the ground and the sole is flat.
            var rows = new[]
            {
                new Vector3(0.055f, 0.040f, 0.038f),
                new Vector3(0.010f, 0.048f, 0.055f),
                new Vector3(-0.065f, 0.050f, 0.046f),
                new Vector3(-0.135f, 0.050f, 0.030f),
                new Vector3(-0.195f, 0.036f, 0.017f)
            };
            var rings = new List<RingSpec>(rows.Length);
            foreach (Vector3 row in rows)
            {
                rings.Add(new RingSpec(new Vector3(x, row.z, row.x), u, v, new Vector2(row.y, row.z), 3f, c, w));
            }
            (int heel, int toe) = m.Loft(rings, BootSegments);

            int heelCap = m.Add(new Vector3(x, rows[0].z, rows[0].x + 0.004f), new Vector2(0.5f, 0f), c, w);
            m.Fan(heelCap, heel, BootSegments, u, v, -Forward);
            int toeCap = m.Add(new Vector3(x, rows[4].z, rows[4].x - 0.004f), new Vector2(0.5f, 1f), c, w);
            m.Fan(toeCap, toe, BootSegments, u, v, Forward);
        }

        // ------------------------------------------------------------------
        // Primitives
        // ------------------------------------------------------------------

        /// <summary>A ring's position, frame, size and shading, ready to emit.</summary>
        private readonly struct RingSpec
        {
            public readonly Vector3 Centre;
            /// <summary>Unit axis for <see cref="Radius"/>.x.</summary>
            public readonly Vector3 U;
            /// <summary>Unit axis for <see cref="Radius"/>.y.</summary>
            public readonly Vector3 V;
            public readonly Vector2 Radius;
            /// <summary>Superellipse exponent: 2 is an ellipse, higher is boxier.</summary>
            public readonly float Exponent;
            public readonly Color Colour;
            public readonly BoneWeight Weight;
            /// <summary>Do not stitch this ring to the next; the next duplicates its position with a different colour.</summary>
            public readonly bool Split;

            public RingSpec(Vector3 centre, Vector3 u, Vector3 v, Vector2 radius, float exponent,
                Color colour, BoneWeight weight, bool split = false)
            {
                Centre = centre;
                U = u;
                V = v;
                Radius = radius;
                Exponent = exponent;
                Colour = colour;
                Weight = weight;
                Split = split;
            }
        }

        /// <summary>A ring along a bone, before it has a position: fraction along the bone, half-extents, colour.</summary>
        private readonly struct Station
        {
            public readonly float T;
            public readonly Vector2 Radius;
            public readonly Color Colour;
            public readonly bool Split;

            public Station(float t, float halfWidth, float halfDepth, Color colour, bool split = false)
            {
                T = t;
                Radius = new Vector2(halfWidth, halfDepth);
                Colour = colour;
                Split = split;
            }

            /// <summary>A station at a standing height, on a bone running from <paramref name="yFrom"/> to <paramref name="yTo"/>.</summary>
            public static Station AtHeight(float y, float yFrom, float yTo, float halfWidth, float halfDepth,
                Color colour, bool split = false)
                => new Station((yFrom - y) / (yFrom - yTo), halfWidth, halfDepth, colour, split);
        }

        /// <summary>The growing vertex and index streams, with the few operations everything above is built from.</summary>
        private sealed class MeshData
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<int> Triangles = new List<int>();
            public readonly List<BoneWeight> Weights = new List<BoneWeight>();

            public int Add(Vector3 position, Vector2 uv, Color colour, BoneWeight weight)
            {
                Vertices.Add(position);
                Uvs.Add(uv);
                Colors.Add(colour);
                Weights.Add(weight);
                return Vertices.Count - 1;
            }

            /// <summary>Emit one closed ring. Returns the index of its first vertex.</summary>
            public int Ring(RingSpec spec, int segments, float tUv)
            {
                int start = Vertices.Count;
                for (int s = 0; s < segments; s++)
                {
                    float angle = s / (float)segments * Mathf.PI * 2f;
                    float px = SuperPow(Mathf.Cos(angle), spec.Exponent) * spec.Radius.x;
                    float py = SuperPow(Mathf.Sin(angle), spec.Exponent) * spec.Radius.y;
                    Add(spec.Centre + spec.U * px + spec.V * py,
                        new Vector2(s / (float)segments, tUv), spec.Colour, spec.Weight);
                }
                return start;
            }

            /// <summary>Emit the rings and stitch each to the next. Returns the first and last ring's start index.</summary>
            public (int first, int last) Loft(List<RingSpec> rings, int segments)
            {
                int first = -1, previous = -1;
                for (int i = 0; i < rings.Count; i++)
                {
                    int ring = Ring(rings[i], segments, i / (float)Mathf.Max(1, rings.Count - 1));
                    if (i == 0) first = ring;
                    if (i > 0 && !rings[i - 1].Split)
                    {
                        Stitch(previous, ring, segments, rings[i - 1].U, rings[i - 1].V,
                            rings[i].Centre - rings[i - 1].Centre);
                    }
                    previous = ring;
                }
                return (first, previous);
            }

            /// <summary>
            /// Quad-strip two rings of equal segment count, wound so the outside faces out.
            /// </summary>
            /// <remarks>
            /// Unity's rule, checked against its documented quad example: for a
            /// triangle wound (a, b, c) the front-facing normal is Cross(b - a, c - a).
            /// Which of the two strip windings satisfies that depends on the
            /// handedness of the ring frame against the direction the loft runs,
            /// so it is derived here from <paramref name="u"/>, <paramref name="v"/>
            /// and <paramref name="direction"/> rather than assumed. The first
            /// version of this file assumed, and every limb came out inside-out:
            /// back-face culling then shows the far wall of a hollow body, and the
            /// result is blamed on the shape rather than the winding.
            /// </remarks>
            public void Stitch(int a, int b, int segments, Vector3 u, Vector3 v, Vector3 direction)
            {
                bool flip = Vector3.Dot(Vector3.Cross(u, v), direction) > 0f;
                for (int s = 0; s < segments; s++)
                {
                    int next = (s + 1) % segments;
                    int i0 = a + s, i1 = a + next, i2 = b + s, i3 = b + next;
                    if (flip)
                    {
                        Triangle(i0, i1, i2);
                        Triangle(i1, i3, i2);
                    }
                    else
                    {
                        Triangle(i0, i2, i1);
                        Triangle(i1, i2, i3);
                    }
                }
            }

            /// <summary>Close a ring with a fan to <paramref name="centre"/>, its face pointing along <paramref name="outward"/>.</summary>
            /// <remarks>Same rule as <see cref="Stitch"/>: front normal = Cross(b - a, c - a), derived from the ring frame.</remarks>
            public void Fan(int centre, int ring, int segments, Vector3 u, Vector3 v, Vector3 outward)
            {
                bool alongCross = Vector3.Dot(Vector3.Cross(u, v), outward) > 0f;
                for (int s = 0; s < segments; s++)
                {
                    int next = (s + 1) % segments;
                    if (alongCross) Triangle(centre, ring + s, ring + next);
                    else Triangle(centre, ring + next, ring + s);
                }
            }

            private void Triangle(int a, int b, int c)
            {
                Triangles.Add(a);
                Triangles.Add(b);
                Triangles.Add(c);
            }
        }

        /// <summary>Signed |c|^(2/n): the superellipse's coordinate for cos or sin of the ring angle.</summary>
        private static float SuperPow(float c, float exponent)
        {
            if (exponent == 2f) return c;
            float magnitude = (float)Math.Pow(Math.Abs(c), 2.0 / exponent);
            return c < 0f ? -magnitude : magnitude;
        }

        /// <summary>A Gaussian bump of unit height, for face features.</summary>
        private static float Bell(float x, float width) => Mathf.Exp(-(x * x) / (width * width));

        // ------------------------------------------------------------------
        // Skinning
        // ------------------------------------------------------------------

        /// <summary>All weight on one bone.</summary>
        private static BoneWeight Single(Bone bone) => new BoneWeight
        {
            boneIndex0 = (int)bone,
            weight0 = 1f
        };

        /// <summary>
        /// Blend between two bones along a limb.
        /// </summary>
        /// <remarks>
        /// Smoothstepped rather than linear, and biased so the middle of a segment
        /// belongs almost entirely to one bone. A linear blend leaves the midpoint
        /// half-owned by each, and the limb then collapses inward when the joint
        /// bends — the classic "melting elbow".
        /// </remarks>
        private static BoneWeight Blend(Bone from, Bone to, float t)
        {
            float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.25f) / 0.5f));
            return new BoneWeight
            {
                boneIndex0 = (int)from,
                weight0 = 1f - w,
                boneIndex1 = (int)to,
                weight1 = w
            };
        }

        /// <summary>Which spine bones own a torso ring at a standing height (unscaled metres).</summary>
        private static BoneWeight TorsoWeight(float y)
        {
            float hips = JointY(Bone.Hips), spine = JointY(Bone.Spine);
            float chest = JointY(Bone.Chest), neck = JointY(Bone.Neck);
            if (y <= hips) return Single(Bone.Hips);
            if (y < spine) return Blend(Bone.Hips, Bone.Spine, (y - hips) / (spine - hips));
            if (y < chest) return Blend(Bone.Spine, Bone.Chest, (y - spine) / (chest - spine));
            return Blend(Bone.Chest, Bone.Neck, Mathf.Clamp01((y - chest) / (neck - chest)));
        }

        private static Matrix4x4[] BindPoses(Transform[] bones)
        {
            var poses = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                // The bind pose maps mesh space into each bone's local space. Mesh
                // vertices are authored in the root's space, so the root's
                // localToWorldMatrix is the right thing to invert against.
                poses[i] = bones[i].worldToLocalMatrix * bones[(int)Bone.Root].localToWorldMatrix;
            }
            return poses;
        }
    }
}
