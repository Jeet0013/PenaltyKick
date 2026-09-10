using System.Collections.Generic;
using CyberGoal.Core.Physics;
using CyberGoal.Unity.Characters;
using UnityEngine;

namespace CyberGoal.Unity.Environment
{
    /// <summary>How the crowd is behaving (§19).</summary>
    public enum CrowdMood
    {
        Idle,
        Tension,
        Goal,
        Save,
        Victory
    }

    /// <summary>
    /// The stadium audience, as instanced low-poly humanoids.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why not billboards.</b> §6 of the art direction rules out flat cutouts
    /// and colour dots: a crowd has to read as human figures with heads, torsos
    /// and arms. The first version drew textureless quads and looked exactly like
    /// the confetti the brief warns against.
    /// </para>
    /// <para>
    /// <b>Why colour buckets rather than per-instance tints.</b> The obvious
    /// approach is one material plus a <see cref="MaterialPropertyBlock"/> holding
    /// a colour array, and it silently does not work: per-instance properties are
    /// only read if the shader declares them inside an instancing buffer, and
    /// stock URP/Lit declares <c>_BaseColor</c> in a plain per-material CBUFFER.
    /// Nothing errors — every spectator simply comes out the same colour, and the
    /// cause is invisible from the C# side.
    /// </para>
    /// <para>
    /// So spectators are grouped into a small set of pre-tinted materials and each
    /// group is drawn separately. A few dozen draw calls instead of one, which is
    /// still nothing, and it works on any shader without writing a custom one.
    /// </para>
    /// <para>
    /// <b>Why one mesh drawn many times.</b> §48 wants a stadium that looks full
    /// without destroying mobile performance, and a thousand GameObjects with
    /// their own meshes is a thousand draw calls plus a thousand transforms to
    /// update.
    /// </para>
    /// </remarks>
    public sealed class CrowdSystem : MonoBehaviour
    {
        private const float GoalZ = -(float)BallPhysics.Field.SpotToGoal;

        /// <summary>Unity's instanced draw call takes at most 1023 matrices.</summary>
        private const int BatchLimit = 1023;

        /// <summary>Body silhouettes. Four is past the point anyone can tell at 30 m.</summary>
        private const int Variants = 4;

        /// <summary>Tint groups: home, away and neutral, each in three values.</summary>
        private const int Buckets = 9;

        /// <summary>One drawable group: a mesh, a tinted material, and its instances.</summary>
        private sealed class Batch
        {
            public Mesh Mesh;
            public Material Material;
            public Matrix4x4[] Matrices;
            public Vector3[] Rest;
            public Quaternion[] Rotations;
            public float[] Phases;
        }

        private readonly List<Batch> _batches = new List<Batch>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Material> _materials = new List<Material>();

        private CrowdMood _mood = CrowdMood.Idle;
        private float _moodBlend;
        private float _clock;

        public void Build(int count, Color home, Color away)
        {
            for (int v = 0; v < Variants; v++)
            {
                _meshes.Add(SeatedFigure(BodyShape.Spectator(v * 7.3f + 1.7f), v));
            }

            for (int b = 0; b < Buckets; b++) _materials.Add(BucketMaterial(b, home, away));

            // Gather per (variant, bucket) before batching, so each draw call is
            // one mesh and one tint.
            var groups = new List<(Vector3 Position, Quaternion Rotation, float Phase)>[Variants * Buckets];
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i] = new List<(Vector3, Quaternion, float)>();
            }

            const int rows = 11;
            int perRow = Mathf.Max(1, Mathf.CeilToInt(count / (float)rows));

            for (int i = 0; i < count; i++)
            {
                int row = i / perRow;
                int seat = i % perRow;

                // Seated in rows, with half a seat of stagger so rows interlock the
                // way real seating does. Taking the row from `i % rows` instead
                // makes consecutive spectators zig-zag between heights and produces
                // a lattice of confetti rather than a crowd — which is exactly what
                // the first version did.
                float angle = (seat + (row % 2) * 0.5f) / perRow * Mathf.PI * 2f;
                float jitter = (Hash(i * 3.1f) - 0.5f) * 0.014f;

                float ring = 25.5f + row * 1.75f;
                float height = 2.6f + row * 1.28f;

                var position = new Vector3(
                    Mathf.Cos(angle + jitter) * ring,
                    height,
                    Mathf.Sin(angle + jitter) * ring + GoalZ + 8f);

                // Everyone faces the penalty spot, which is where the action is.
                Vector3 toCentre = new Vector3(-position.x, 0f, (GoalZ + 4f) - position.z);
                Quaternion rotation = Quaternion.LookRotation(toCentre.normalized, Vector3.up);

                int variant = i % Variants;
                // Split the bowl between the sides, with a scatter of neutrals.
                float side = Hash(i * 1.7f);
                int family = side < 0.32f ? 2 : (angle < Mathf.PI ? 0 : 1);
                int value = Mathf.Min(2, (int)(Hash(i * 5.9f) * 3f));
                int bucket = family * 3 + value;

                // Each spectator gets their own phase, so a celebrating crowd is a
                // boil of individual people rather than one body moving in
                // lockstep. §18 of the art direction asks for exactly this.
                float phase = Hash(i * 2.3f) * Mathf.PI * 2f;

                groups[variant * Buckets + bucket].Add((position, rotation, phase));
            }

            for (int v = 0; v < Variants; v++)
            {
                for (int b = 0; b < Buckets; b++)
                {
                    Split(_meshes[v], _materials[b], groups[v * Buckets + b]);
                }
            }
        }

        private void Split(Mesh mesh, Material material,
            List<(Vector3 Position, Quaternion Rotation, float Phase)> members)
        {
            for (int start = 0; start < members.Count; start += BatchLimit)
            {
                int size = Mathf.Min(BatchLimit, members.Count - start);
                var batch = new Batch
                {
                    Mesh = mesh,
                    Material = material,
                    Matrices = new Matrix4x4[size],
                    Rest = new Vector3[size],
                    Rotations = new Quaternion[size],
                    Phases = new float[size]
                };

                for (int i = 0; i < size; i++)
                {
                    (Vector3 position, Quaternion rotation, float phase) = members[start + i];
                    batch.Rest[i] = position;
                    batch.Rotations[i] = rotation;
                    batch.Phases[i] = phase;
                    batch.Matrices[i] = Matrix4x4.TRS(position, rotation, Vector3.one);
                }

                _batches.Add(batch);
            }
        }

        /// <summary>A tinted material per group. Nine of them, made once.</summary>
        private static Material BucketMaterial(int bucket, Color home, Color away)
        {
            int family = bucket / 3;
            int value = bucket % 3;

            Color baseColour = family switch
            {
                0 => home,
                1 => away,
                _ => new Color(0.42f, 0.46f, 0.52f)
            };

            // Kept dark. These are unlit-ish figures 30 m away in a night stadium;
            // at full value they glow brighter than the pitch and the eye reads the
            // stands rather than the goal.
            float scale = 0.32f + value * 0.22f;

            var material = new Material(ShaderLibrary.Lit) { color = baseColour * scale };
            ShaderLibrary.SetSmoothness(material, 0.15f);
            material.enableInstancing = true;
            return material;
        }

        public void SetMood(CrowdMood mood) => _mood = mood;

        private void Update()
        {
            // Mood changes ease rather than snap. A crowd that switches from calm
            // to ecstatic in one frame reads as a light being turned on.
            float target = _mood switch
            {
                CrowdMood.Idle => 0.12f,
                CrowdMood.Tension => 0.30f,
                CrowdMood.Save => 0.75f,
                CrowdMood.Goal => 1f,
                _ => 1f
            };
            _moodBlend = Mathf.MoveTowards(_moodBlend, target, Time.deltaTime * 2.2f);
            _clock += Time.deltaTime;

            Animate();

            foreach (Batch batch in _batches)
            {
                Graphics.DrawMeshInstanced(
                    batch.Mesh, 0, batch.Material, batch.Matrices, batch.Matrices.Length);
            }
        }

        /// <summary>
        /// Move the crowd.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Spectators <em>jump</em> on a goal — they do not sway harder. §19 lists
        /// jumping and raised arms as the goal reaction, and vertical motion is
        /// also the only thing that reads at 40 m through a 30-degree lens: a
        /// sideways shuffle at that distance is invisible, but a bowl of people
        /// leaving their seats is unmistakable.
        /// </para>
        /// <para>
        /// The bounce is rectified — <c>Abs(Sin)</c> rather than <c>Sin</c> — so
        /// figures spring up from a resting height and fall back to it, instead of
        /// sinking below the seat on the down phase and appearing to stand in a
        /// hole.
        /// </para>
        /// </remarks>
        private void Animate()
        {
            // Below this the crowd is at rest and the matrices already hold the
            // resting pose, so rebuilding thousands of them would buy nothing.
            if (_moodBlend <= 0.13f) return;

            float amplitude = Mathf.SmoothStep(0f, 0.42f, _moodBlend);
            float tempo = 3.2f + _moodBlend * 5.5f;

            foreach (Batch batch in _batches)
            {
                for (int i = 0; i < batch.Matrices.Length; i++)
                {
                    float bounce = Mathf.Abs(Mathf.Sin(_clock * tempo + batch.Phases[i])) * amplitude;
                    Vector3 p = batch.Rest[i];
                    batch.Matrices[i] = Matrix4x4.TRS(
                        new Vector3(p.x, p.y + bounce, p.z), batch.Rotations[i], Vector3.one);
                }
            }
        }

        /// <summary>
        /// A seated person: torso, head, and arms folded or raised.
        /// </summary>
        /// <remarks>
        /// Built once per variant and drawn thousands of times, so a few hundred
        /// triangles here is free. Legs are omitted deliberately — every spectator
        /// is behind a seat back or a barrier, and nobody has ever noticed a
        /// crowd's shins.
        /// </remarks>
        private static Mesh SeatedFigure(BodyShape shape, int variant)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            float g = shape.Girth;
            float h = shape.Height;

            Box(vertices, normals, triangles,
                new Vector3(0f, 0.30f * h, 0f),
                new Vector3(0.20f * g, 0.30f * h, 0.13f * g));

            Box(vertices, normals, triangles,
                new Vector3(0f, 0.68f * h, 0f),
                new Vector3(0.088f, 0.105f, 0.092f));

            // Variants alternate between resting and raised arms, so even a still
            // crowd has silhouette variety.
            bool raised = variant % 2 == 1;
            float armY = raised ? 0.62f * h : 0.34f * h;
            float armOut = raised ? 0.30f : 0.24f;
            foreach (int side in new[] { -1, 1 })
            {
                Box(vertices, normals, triangles,
                    new Vector3(side * armOut * g, armY, raised ? 0.02f : 0.06f),
                    new Vector3(0.048f, raised ? 0.16f : 0.055f, 0.055f));
            }

            var mesh = new Mesh { name = $"Spectator{variant}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// An axis-aligned box.
        /// </summary>
        /// <remarks>
        /// Wound so that <c>Cross(b-a, c-a)</c> points outward for every face,
        /// which is Unity's front-facing rule. Getting this backwards produces a
        /// figure that is culled from the outside and visible from within — and it
        /// looks like a modelling error rather than a winding one.
        /// </remarks>
        private static void Box(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            Vector3 centre, Vector3 half)
        {
            int b = vertices.Count;
            Vector3[] corners =
            {
                new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1),
                new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1)
            };
            foreach (Vector3 c in corners)
            {
                vertices.Add(centre + Vector3.Scale(c, half));
                normals.Add(c.normalized);
            }

            int[] faces =
            {
                0,2,1, 0,3,2,   // -Z
                4,5,6, 4,6,7,   // +Z
                0,1,5, 0,5,4,   // -Y
                3,7,6, 3,6,2,   // +Y
                0,4,7, 0,7,3,   // -X
                1,2,6, 1,6,5    // +X
            };
            foreach (int i in faces) triangles.Add(b + i);
        }

        private static float Hash(float v)
        {
            float s = Mathf.Sin(v * 12.9898f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
    }
}
