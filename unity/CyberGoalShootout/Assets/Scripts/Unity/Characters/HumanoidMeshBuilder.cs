using System.Collections.Generic;
using UnityEngine;

namespace CyberGoal.Unity.Characters
{
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

        public static BodyShape Striker => new BodyShape
            { Height = 1.00f, Shoulders = 1.00f, Girth = 0.94f, Limbs = 1.02f };

        /// <summary>Keepers are the tallest people on the pitch, and it matters.</summary>
        public static BodyShape Goalkeeper => new BodyShape
            { Height = 1.05f, Shoulders = 1.08f, Girth = 1.00f, Limbs = 1.05f };

        /// <summary>Older, heavier, shorter — reads as a different role at a glance.</summary>
        public static BodyShape Referee => new BodyShape
            { Height = 0.96f, Shoulders = 0.94f, Girth = 1.16f, Limbs = 0.98f };

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
                Limbs = 0.90f + a * 0.22f
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
    /// the chest, limbs that thin toward the wrist and ankle, a neck, hands and
    /// boots.
    /// </para>
    /// <para>
    /// <b>Elliptical cross-sections do most of the work.</b> A human torso is far
    /// wider than it is deep — roughly 0.19 m against 0.11 m at the chest. Loft it
    /// with circular rings and you get a tube-person no amount of texturing
    /// rescues. Two radii per ring is the single cheapest thing that makes a
    /// generated body look human.
    /// </para>
    /// <para>
    /// <b>It is still not final art.</b> There is no face, no hair, no cloth
    /// simulation and no scanned detail, so §2's "detailed face, eyes, hair"
    /// remains unmet and `docs/STATUS.md` still lists characters as PLACEHOLDER.
    /// What changes is the floor: this is a rigged humanoid with believable
    /// proportions rather than four primitives, it animates through the same
    /// <see cref="CharacterVisual"/> API a MakeHuman export will use, and it is
    /// bound to the same skeleton — so replacing it is still a single field.
    /// </para>
    /// </remarks>
    public static class HumanoidMeshBuilder
    {
        /// <summary>Vertices around each lofted ring. 10 is enough at gameplay distance.</summary>
        private const int RadialSegments = 10;

        public sealed class Built
        {
            public Mesh Mesh;
            public Transform[] Bones;
            public Transform Root;
        }

        /// <summary>A segment of the body, lofted between two joints.</summary>
        private readonly struct Limb
        {
            public readonly Bone From;
            public readonly Bone To;
            /// <summary>Half-width (x) and half-depth (z) at the start.</summary>
            public readonly Vector2 StartRadius;
            public readonly Vector2 EndRadius;
            public readonly int Rings;

            public Limb(Bone from, Bone to, Vector2 startRadius, Vector2 endRadius, int rings = 3)
            {
                From = from;
                To = to;
                StartRadius = startRadius;
                EndRadius = endRadius;
                Rings = rings;
            }
        }

        /// <summary>Build the mesh, the bone transforms, and bind them together.</summary>
        public static Built Build(Transform parent, BodyShape shape, string name)
        {
            Transform[] bones = BuildBones(parent, shape, name);
            Mesh mesh = BuildMesh(shape, bones);
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

        private static Mesh BuildMesh(BodyShape shape, Transform[] bones)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var weights = new List<BoneWeight>();

            float g = shape.Girth;
            float l = shape.Limbs;

            // Half-width and half-depth at each level. Width always exceeds depth,
            // which is the whole reason these read as bodies rather than pipes.
            var limbs = new[]
            {
                // Torso: hips → waist → chest → neck. Narrow at the waist, broad and
                // shallow at the chest.
                new Limb(Bone.Hips, Bone.Spine, new Vector2(0.165f * g, 0.108f * g),
                    new Vector2(0.148f * g, 0.100f * g), 3),
                new Limb(Bone.Spine, Bone.Chest, new Vector2(0.148f * g, 0.100f * g),
                    new Vector2(0.192f * g, 0.112f * g), 4),
                new Limb(Bone.Chest, Bone.Neck, new Vector2(0.192f * g, 0.112f * g),
                    new Vector2(0.062f, 0.058f), 4),
                new Limb(Bone.Neck, Bone.Head, new Vector2(0.062f, 0.058f),
                    new Vector2(0.082f, 0.080f), 2),

                // Arms. The deltoid is the widest point and sits outside the chest.
                new Limb(Bone.ShoulderL, Bone.UpperArmL, new Vector2(0.070f * l, 0.068f * l),
                    new Vector2(0.056f * l, 0.055f * l), 2),
                new Limb(Bone.UpperArmL, Bone.ForearmL, new Vector2(0.056f * l, 0.055f * l),
                    new Vector2(0.044f * l, 0.043f * l), 3),
                new Limb(Bone.ForearmL, Bone.HandL, new Vector2(0.044f * l, 0.043f * l),
                    new Vector2(0.031f * l, 0.028f * l), 3),

                new Limb(Bone.ShoulderR, Bone.UpperArmR, new Vector2(0.070f * l, 0.068f * l),
                    new Vector2(0.056f * l, 0.055f * l), 2),
                new Limb(Bone.UpperArmR, Bone.ForearmR, new Vector2(0.056f * l, 0.055f * l),
                    new Vector2(0.044f * l, 0.043f * l), 3),
                new Limb(Bone.ForearmR, Bone.HandR, new Vector2(0.044f * l, 0.043f * l),
                    new Vector2(0.031f * l, 0.028f * l), 3),

                // Legs. A thigh is nearly twice the thickness of an ankle.
                new Limb(Bone.ThighL, Bone.ShinL, new Vector2(0.090f * l, 0.088f * l),
                    new Vector2(0.062f * l, 0.060f * l), 3),
                new Limb(Bone.ShinL, Bone.FootL, new Vector2(0.062f * l, 0.060f * l),
                    new Vector2(0.038f * l, 0.038f * l), 3),

                new Limb(Bone.ThighR, Bone.ShinR, new Vector2(0.090f * l, 0.088f * l),
                    new Vector2(0.062f * l, 0.060f * l), 3),
                new Limb(Bone.ShinR, Bone.FootR, new Vector2(0.062f * l, 0.060f * l),
                    new Vector2(0.038f * l, 0.038f * l), 3),
            };

            foreach (Limb limb in limbs)
            {
                AppendLimb(limb, shape, vertices, normals, uvs, triangles, weights);
            }

            // The head is a shaped ellipsoid rather than a lofted tube: a lofted
            // head has a flat crown, which is the most obviously wrong thing a
            // generated body can have.
            AppendHead(shape, vertices, normals, uvs, triangles, weights);

            AppendFoot(Bone.FootL, shape, vertices, normals, uvs, triangles, weights);
            AppendFoot(Bone.FootR, shape, vertices, normals, uvs, triangles, weights);

            var mesh = new Mesh { name = "Humanoid" };
            // 16-bit indices cap at 65535 vertices. This body is around 1,400, so
            // the default is fine and cheaper on mobile.
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = BindPoses(bones);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Loft a tapered elliptical tube between two joints.</summary>
        private static void AppendLimb(Limb limb, BodyShape shape,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, List<BoneWeight> weights)
        {
            Vector3 a = Scaled(Skeleton.Joints[(int)limb.From], shape);
            Vector3 b = Scaled(Skeleton.Joints[(int)limb.To], shape);
            Vector3 axis = (b - a).normalized;
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.up;

            // A stable frame perpendicular to the limb. Cross with 'right' first
            // because most limbs run vertically, and crossing near-parallel vectors
            // gives a degenerate frame.
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.right)) < 0.9f
                ? Vector3.right
                : Vector3.forward;
            Vector3 side = Vector3.Cross(reference, axis).normalized;
            Vector3 front = Vector3.Cross(axis, side).normalized;

            int baseIndex = vertices.Count;
            int rings = Mathf.Max(2, limb.Rings);

            for (int r = 0; r < rings; r++)
            {
                float t = r / (float)(rings - 1);
                Vector3 centre = Vector3.Lerp(a, b, t);
                Vector2 radius = Vector2.Lerp(limb.StartRadius, limb.EndRadius, t);

                for (int s = 0; s < RadialSegments; s++)
                {
                    float angle = s / (float)RadialSegments * Mathf.PI * 2f;
                    // Elliptical: separate x and z radii.
                    Vector3 offset = side * (Mathf.Cos(angle) * radius.x)
                                     + front * (Mathf.Sin(angle) * radius.y);

                    vertices.Add(centre + offset);
                    normals.Add(offset.normalized);
                    uvs.Add(new Vector2(s / (float)RadialSegments, t));
                    weights.Add(Blend(limb.From, limb.To, t));
                }
            }

            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < RadialSegments; s++)
                {
                    int next = (s + 1) % RadialSegments;
                    int i0 = baseIndex + r * RadialSegments + s;
                    int i1 = baseIndex + r * RadialSegments + next;
                    int i2 = baseIndex + (r + 1) * RadialSegments + s;
                    int i3 = baseIndex + (r + 1) * RadialSegments + next;

                    triangles.Add(i0); triangles.Add(i2); triangles.Add(i1);
                    triangles.Add(i1); triangles.Add(i2); triangles.Add(i3);
                }
            }
        }

        private static void AppendHead(BodyShape shape,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, List<BoneWeight> weights)
        {
            Vector3 centre = Scaled(Skeleton.Joints[(int)Bone.Head], shape) + Vector3.up * 0.075f;
            // Taller than wide, and deeper than wide — a human skull, not a ball.
            var radius = new Vector3(0.085f, 0.108f, 0.096f);

            const int stacks = 8;
            int baseIndex = vertices.Count;

            for (int y = 0; y <= stacks; y++)
            {
                float v = y / (float)stacks;
                float phi = v * Mathf.PI;
                for (int s = 0; s <= RadialSegments; s++)
                {
                    float u = s / (float)RadialSegments;
                    float theta = u * Mathf.PI * 2f;

                    var unit = new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));

                    vertices.Add(centre + Vector3.Scale(unit, radius));
                    normals.Add(unit);
                    uvs.Add(new Vector2(u, v));
                    weights.Add(Single(Bone.Head));
                }
            }

            int stride = RadialSegments + 1;
            for (int y = 0; y < stacks; y++)
            {
                for (int s = 0; s < RadialSegments; s++)
                {
                    int i0 = baseIndex + y * stride + s;
                    int i1 = i0 + 1;
                    int i2 = i0 + stride;
                    int i3 = i2 + 1;

                    triangles.Add(i0); triangles.Add(i2); triangles.Add(i1);
                    triangles.Add(i1); triangles.Add(i2); triangles.Add(i3);
                }
            }
        }

        /// <summary>A boot: a box forward of the ankle, so the figure has a stance.</summary>
        private static void AppendFoot(Bone foot, BodyShape shape,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, List<BoneWeight> weights)
        {
            Vector3 ankle = Scaled(Skeleton.Joints[(int)foot], shape);
            // Sole on the ground, toes forward (-Z is toward the goal).
            Vector3 centre = ankle + new Vector3(0f, -0.035f, -0.07f);
            var half = new Vector3(0.052f, 0.038f, 0.135f);

            int baseIndex = vertices.Count;
            Vector3[] corners =
            {
                new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1),
                new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1)
            };

            foreach (Vector3 c in corners)
            {
                vertices.Add(centre + Vector3.Scale(c, half));
                normals.Add(c.normalized);
                uvs.Add(new Vector2(c.x * 0.5f + 0.5f, c.z * 0.5f + 0.5f));
                weights.Add(Single(foot));
            }

            int[] faces =
            {
                0,2,1, 0,3,2,  // back
                4,5,6, 4,6,7,  // front
                0,1,5, 0,5,4,  // bottom
                3,7,6, 3,6,2,  // top
                0,4,7, 0,7,3,  // left
                1,2,6, 1,6,5   // right
            };
            foreach (int i in faces) triangles.Add(baseIndex + i);
        }

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
