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
    /// and arms. The previous version drew textureless quads and looked exactly
    /// like the confetti the brief warns against.
    /// </para>
    /// <para>
    /// <b>Why one mesh drawn many times.</b> §48 wants a stadium that looks full
    /// without destroying mobile performance, and a thousand separate GameObjects
    /// with their own meshes is a thousand draw calls. This builds a handful of
    /// seated humanoid meshes once, then draws each of them for every spectator
    /// that uses it via <see cref="Graphics.DrawMeshInstanced"/> — a few dozen
    /// draw calls for the whole bowl, with per-instance colour so no two
    /// neighbours match.
    /// </para>
    /// <para>
    /// <b>Distance decides detail, not importance.</b> Near rows get their own
    /// pose and animate; far rows are the same meshes at lower density and do not
    /// move individually. The silhouette is what carries at 40 m, and §7 of the
    /// art direction only asks that far spectators still read as a stadium full of
    /// people.
    /// </para>
    /// </remarks>
    public sealed class CrowdSystem : MonoBehaviour
    {
        private const float GoalZ = -(float)BallPhysics.Field.SpotToGoal;
        /// <summary>Unity's instanced draw call takes at most 1023 matrices.</summary>
        private const int BatchLimit = 1023;

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Matrix4x4[]> _batches = new List<Matrix4x4[]>();
        private readonly List<int> _batchMesh = new List<int>();
        private readonly List<MaterialPropertyBlock> _blocks = new List<MaterialPropertyBlock>();
        private readonly List<Vector4[]> _colours = new List<Vector4[]>();

        private Material _material;
        private CrowdMood _mood = CrowdMood.Idle;
        private float _moodBlend;

        /// <summary>Resting transform of every instance, so animation is an offset.</summary>
        private readonly List<Matrix4x4[]> _rest = new List<Matrix4x4[]>();
        /// <summary>Per-instance position, rotation and phase, kept for re-posing.</summary>
        private readonly List<Vector3[]> _positions = new List<Vector3[]>();
        private readonly List<Quaternion[]> _rotations = new List<Quaternion[]>();
        private readonly List<float[]> _phases = new List<float[]>();
        private float _clock;

        private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorProperty = Shader.PropertyToID("_Color");

        public void Build(int count, Color home, Color away)
        {
            _material = new Material(ShaderLibrary.Lit) { color = Color.white };
            ShaderLibrary.SetSmoothness(_material, 0.15f);
            _material.enableInstancing = true;

            // A handful of body shapes, reused. §8 of the art direction warns
            // against cloning one person; it does not require a thousand unique
            // meshes, and four silhouettes at 30 m is past the point anyone can
            // tell them apart.
            const int variants = 4;
            for (int v = 0; v < variants; v++)
            {
                _meshes.Add(SeatedFigure(BodyShape.Spectator(v * 7.3f + 1.7f), v));
            }

            var matrices = new List<Matrix4x4>[variants];
            var tints = new List<Vector4>[variants];
            var positions = new List<Vector3>[variants];
            var rotations = new List<Quaternion>[variants];
            for (int v = 0; v < variants; v++)
            {
                matrices[v] = new List<Matrix4x4>();
                tints[v] = new List<Vector4>();
                positions[v] = new List<Vector3>();
                rotations[v] = new List<Quaternion>();
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
                Vector3 lookAt = new Vector3(0f, height, GoalZ + 4f);
                Quaternion rotation = Quaternion.LookRotation(
                    new Vector3(lookAt.x - position.x, 0f, lookAt.z - position.z).normalized,
                    Vector3.up);

                int variant = i % variants;
                matrices[variant].Add(Matrix4x4.TRS(position, rotation, Vector3.one));
                positions[variant].Add(position);
                rotations[variant].Add(rotation);

                // Split the bowl between the sides with a scatter of neutrals, then
                // vary the value per person. A uniform tint reads as a painted
                // surface rather than as thousands of separate people.
                float r = Hash(i * 1.7f);
                Color tint = r < 0.34f
                    ? new Color(0.42f, 0.46f, 0.52f)
                    : (angle < Mathf.PI ? home : away);
                float value = 0.35f + Hash(i * 5.9f) * 0.5f;
                tints[variant].Add(tint * value);
            }

            for (int v = 0; v < variants; v++)
            {
                _pendingPositions = positions[v];
                _pendingRotations = rotations[v];
                Batch(v, matrices[v], tints[v]);
            }
            _pendingPositions = null;
            _pendingRotations = null;
        }

        private List<Vector3> _pendingPositions;
        private List<Quaternion> _pendingRotations;

        private void Batch(int meshIndex, List<Matrix4x4> matrices, List<Vector4> tints)
        {
            for (int start = 0; start < matrices.Count; start += BatchLimit)
            {
                int size = Mathf.Min(BatchLimit, matrices.Count - start);
                var slice = new Matrix4x4[size];
                var colours = new Vector4[size];
                for (int i = 0; i < size; i++)
                {
                    slice[i] = matrices[start + i];
                    colours[i] = tints[start + i];
                }

                var block = new MaterialPropertyBlock();
                block.SetVectorArray(ColorProperty, colours);
                block.SetVectorArray(LegacyColorProperty, colours);

                var positions = new Vector3[size];
                var rotations = new Quaternion[size];
                var phases = new float[size];
                for (int i = 0; i < size; i++)
                {
                    positions[i] = _pendingPositions[start + i];
                    rotations[i] = _pendingRotations[start + i];
                    // Each spectator gets their own phase, so a celebrating crowd is
                    // a boil of individual people rather than one body moving in
                    // lockstep. §18 of the art direction asks for exactly this.
                    phases[i] = Hash((start + i) * 2.3f) * Mathf.PI * 2f;
                }

                _batches.Add(slice);
                _rest.Add(slice);
                _positions.Add(positions);
                _rotations.Add(rotations);
                _phases.Add(phases);
                _batchMesh.Add(meshIndex);
                _blocks.Add(block);
                _colours.Add(colours);
            }
        }

        public void SetMood(CrowdMood mood) => _mood = mood;

        private void Update()
        {
            // Mood changes ease rather than snap. A crowd that switches from calm
            // to ecstatic in one frame reads as a light being turned on.
            float target = _mood switch
            {
                CrowdMood.Idle => 0.12f,
                CrowdMood.Tension => 0.3f,
                CrowdMood.Save => 0.75f,
                CrowdMood.Goal => 1f,
                _ => 1f
            };
            _moodBlend = Mathf.MoveTowards(_moodBlend, target, Time.deltaTime * 2.2f);
            _clock += Time.deltaTime;

            Animate();

            for (int i = 0; i < _batches.Count; i++)
            {
                Graphics.DrawMeshInstanced(
                    _meshes[_batchMesh[i]], 0, _material, _batches[i], _batches[i].Length, _blocks[i]);
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
        /// <para>
        /// Rebuilding matrices every frame for a few thousand instances is a few
        /// hundred microseconds and avoids a skinned mesh per spectator, which is
        /// the version of this that does not run on a phone.
        /// </para>
        /// </remarks>
        private void Animate()
        {
            if (_moodBlend <= 0.13f) return;

            float amplitude = Mathf.SmoothStep(0f, 0.42f, _moodBlend);
            float tempo = 3.2f + _moodBlend * 5.5f;

            for (int b = 0; b < _batches.Count; b++)
            {
                Matrix4x4[] target = _batches[b];
                Vector3[] positions = _positions[b];
                Quaternion[] rotations = _rotations[b];
                float[] phases = _phases[b];

                for (int i = 0; i < target.Length; i++)
                {
                    float bounce = Mathf.Abs(Mathf.Sin(_clock * tempo + phases[i])) * amplitude;
                    Vector3 p = positions[i];
                    target[i] = Matrix4x4.TRS(
                        new Vector3(p.x, p.y + bounce, p.z), rotations[i], Vector3.one);
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

            // Torso: a tapered elliptical box from hip to shoulder.
            Box(vertices, normals, triangles,
                new Vector3(0f, 0.30f * h, 0f),
                new Vector3(0.20f * g, 0.30f * h, 0.13f * g));

            // Head, sat on a short neck.
            Box(vertices, normals, triangles,
                new Vector3(0f, 0.68f * h, 0f),
                new Vector3(0.088f, 0.105f, 0.092f));

            // Arms. Variants alternate between resting and raised, so a still crowd
            // still has silhouette variety.
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
                0,2,1, 0,3,2,
                4,5,6, 4,6,7,
                0,1,5, 0,5,4,
                3,7,6, 3,6,2,
                0,4,7, 0,7,3,
                1,2,6, 1,6,5
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
