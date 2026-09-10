using System.Collections.Generic;
using CyberGoal.Core.Physics;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberGoal.Unity.Environment
{
    /// <summary>
    /// Spectator phone and camera flashes popping in the stands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this effect.</b> A night stadium with a still crowd reads as a
    /// photograph. Flashes are the one thing a real crowd does that is visible
    /// from the pitch at any distance, and they are the cheapest available proof
    /// that thousands of people are out there, each holding something.
    /// </para>
    /// <para>
    /// <b>Why a pool of live flashes rather than a timer per seat.</b> Giving
    /// every seat a phase and asking "are you lit?" each frame costs the whole
    /// budget per frame for a handful of hits, and it makes every seat fire on a
    /// fixed cycle — which the eye picks out as a pattern within seconds. Instead
    /// a spawn rate lights random seats; only the live ones (a few dozen at the
    /// peak) are touched per frame, and no seat ever repeats on a schedule.
    /// </para>
    /// <para>
    /// <b>Why additive.</b> Two flashes overlapping on screen should sum toward
    /// white, and a flash's decay should fade to nothing rather than to a dark
    /// square. Additive blending gives both for free; alpha blending gives
    /// neither without an alpha ramp in the colour.
    /// </para>
    /// <para>
    /// The per-instance colours exceed 1.0 deliberately, so a bloom pass — which
    /// thresholds on HDR values — treats them as light sources rather than as
    /// bright white paint.
    /// </para>
    /// </remarks>
    public sealed class CameraFlashes : MonoBehaviour
    {
        private const float GoalZ = -(float)BallPhysics.Field.SpotToGoal;
        /// <summary>Unity's instanced draw call takes at most 1023 matrices.</summary>
        private const int BatchLimit = 1023;

        /// <summary>
        /// Most flashes lit at once. At the frenzy rate a flash lives about a
        /// fifth of a second, so this sits comfortably above the steady state;
        /// the cap is there so a frame hitch cannot turn accumulated spawn debt
        /// into a wall of light on the next frame.
        /// </summary>
        private const int PoolLimit = 256;

        /// <summary>Flashes per second across the whole bowl, at intensity 0 and 1.</summary>
        private const float IdleRate = 3f;
        private const float FrenzyRate = 420f;

        /// <summary>The fade after the pop. Short, because a phone LED does not linger.</summary>
        private const float Tail = 0.14f;

        /// <summary>HDR multiplier at full brightness. Past the usual bloom threshold with room to spare.</summary>
        private const float Peak = 3.5f;

        /// <summary>Half-extent of a flash in metres. A glint at 30–45 m, not a lamp.</summary>
        private const float Size = 0.3f;

        // The stand geometry these sit in. Mirrors CrowdSystem's seating so the
        // flashes come from where the people are. Kept in step by hand: if the
        // crowd's rows move, flashes will hang in the air where seats used to be.
        private const int CrowdRows = 11;
        private const float FirstRing = 25.5f;
        private const float RingStep = 1.75f;
        private const float FirstHeight = 2.6f;
        private const float HeightStep = 1.28f;

        // Cool white for LED phone flashes, with a minority of warmer ones so
        // the bowl does not sparkle in exactly one colour.
        private static readonly Color Cool = new Color(0.82f, 0.90f, 1.0f);
        private static readonly Color Warm = new Color(1.0f, 0.86f, 0.66f);

        private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorProperty = Shader.PropertyToID("_Color");

        private Vector3[] _seatPosition;
        private Color[] _seatColour;

        private int[] _liveSeat;
        private float[] _liveStart;
        private float[] _liveHold;
        private int _liveCount;

        private Matrix4x4[] _matrices;
        private Vector4[] _colours;
        private MaterialPropertyBlock _block;

        private Mesh _mesh;
        private Material _material;
        private Camera _camera;

        private float _intensity;
        private float _target;
        private float _spawnDebt;
        private uint _spawnSeed;

        /// <summary>
        /// Lay out <paramref name="budget"/> possible flash positions in the
        /// stands. The camera is optional; without one, <see cref="Camera.main"/>
        /// is looked up lazily so the flashes can face whatever ends up rendering.
        /// </summary>
        public void Build(int budget, Camera camera = null)
        {
            _camera = camera;
            _mesh = Star();

            _material = new Material(ShaderLibrary.Unlit) { color = Color.white, enableInstancing = true };
            ShaderLibrary.MakeTransparent(_material);
            _material.SetInt("_SrcBlend", (int)BlendMode.One);
            _material.SetInt("_DstBlend", (int)BlendMode.One);

            budget = Mathf.Max(0, budget);
            _seatPosition = new Vector3[budget];
            _seatColour = new Color[budget];

            for (int i = 0; i < budget; i++)
            {
                uint s = (uint)i * 4u;
                int row = Mathf.Min(CrowdRows - 1, (int)(Unit(s) * CrowdRows));
                float angle = Unit(s + 1u) * Mathf.PI * 2f;

                // Pulled slightly in from the seat and lifted to head height: a
                // phone is held up in front of the face, not at the hip.
                float ring = FirstRing + row * RingStep - 0.4f;
                float height = FirstHeight + row * HeightStep + 1.0f + (Unit(s + 2u) - 0.5f) * 0.3f;

                _seatPosition[i] = new Vector3(
                    Mathf.Cos(angle) * ring,
                    height,
                    Mathf.Sin(angle) * ring + GoalZ + 8f);
                _seatColour[i] = Unit(s + 3u) < 0.18f ? Warm : Cool;
            }

            int pool = Mathf.Min(PoolLimit, Mathf.Min(BatchLimit, budget));
            _liveSeat = new int[pool];
            _liveStart = new float[pool];
            _liveHold = new float[pool];
            _liveCount = 0;

            // Fixed-size arrays for the whole lifetime. A property block locks
            // the length of a vector array the first time it is set, so growing
            // these later would silently truncate to the original size.
            _matrices = new Matrix4x4[pool];
            _colours = new Vector4[pool];
            _block = new MaterialPropertyBlock();
        }

        /// <summary>0 is an idle trickle, 1 is the goal frenzy.</summary>
        public void SetIntensity(float intensity) => _target = Mathf.Clamp01(intensity);

        private void Update()
        {
            if (_mesh == null || _seatPosition == null || _seatPosition.Length == 0) return;

            float dt = Time.deltaTime;
            _intensity = Mathf.MoveTowards(_intensity, _target, dt * 2.5f);

            // Squared so that half intensity is a busy crowd rather than half a
            // storm; the rate has to span two orders of magnitude and a linear
            // blend spends most of that range already looking like a goal.
            float rate = Mathf.Lerp(IdleRate, FrenzyRate, _intensity * _intensity);
            _spawnDebt = Mathf.Min(_spawnDebt + rate * dt, PoolLimit * 0.25f);
            while (_spawnDebt >= 1f)
            {
                _spawnDebt -= 1f;
                if (_liveCount < _matrices.Length) Spawn();
            }

            Quaternion facing = Facing();
            float now = Time.time;

            for (int i = 0; i < _liveCount; i++)
            {
                float age = now - _liveStart[i];
                float hold = _liveHold[i];

                float brightness;
                if (age <= hold)
                {
                    brightness = 1f;
                }
                else
                {
                    float t = (age - hold) / Tail;
                    if (t >= 1f)
                    {
                        Retire(i);
                        i--;
                        continue;
                    }
                    // Quadratic so most of the tail is spent dim: a linear fade
                    // reads as a slow dimmer rather than a flash.
                    brightness = (1f - t) * (1f - t);
                }

                int seat = _liveSeat[i];
                float scale = Size * (0.55f + 0.45f * brightness);
                _matrices[i] = Matrix4x4.TRS(_seatPosition[seat], facing, Vector3.one * scale);
                _colours[i] = _seatColour[seat] * (brightness * Peak);
            }

            if (_liveCount == 0) return;

            _block.SetVectorArray(ColorProperty, _colours);
            _block.SetVectorArray(LegacyColorProperty, _colours);
            Graphics.DrawMeshInstanced(_mesh, 0, _material, _matrices, _liveCount, _block);
        }

        private void Spawn()
        {
            _spawnSeed++;
            uint h = Scramble(_spawnSeed);

            int i = _liveCount++;
            _liveSeat[i] = (int)(h % (uint)_seatPosition.Length);
            _liveStart[i] = Time.time;
            _liveHold[i] = 0.06f + Unit(h) * 0.05f;
        }

        /// <summary>Swap-remove: order does not matter, and it keeps the live set contiguous for the draw.</summary>
        private void Retire(int i)
        {
            int last = --_liveCount;
            _liveSeat[i] = _liveSeat[last];
            _liveStart[i] = _liveStart[last];
            _liveHold[i] = _liveHold[last];
        }

        /// <summary>
        /// One screen-aligned rotation for every flash this frame.
        /// </summary>
        /// <remarks>
        /// Per-flash "look at the camera" rotations would be the most expensive
        /// thing in the loop, and at 30 m the difference between screen-aligned
        /// and camera-facing is a fraction of a degree. The mesh is double-sided,
        /// so even a wrong-way rotation draws rather than vanishes.
        /// </remarks>
        private Quaternion Facing()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return Quaternion.identity;

            Transform t = _camera.transform;
            return Quaternion.LookRotation(-t.forward, t.up);
        }

        /// <summary>
        /// A four-point star in the XY plane, one unit across.
        /// </summary>
        /// <remarks>
        /// A plain square reads as a pixel; a star with drawn-in flanks reads as a
        /// lens glint, which is what a flash looks like from the far side of a
        /// pitch. Both windings are emitted so back-face culling cannot hide it
        /// whichever way the billboard maths ends up pointing — cheaper than a
        /// two-sided material variant we cannot author from here.
        /// </remarks>
        private static Mesh Star()
        {
            const int points = 4;
            const int rim = points * 2;

            var vertices = new List<Vector3> { Vector3.zero };
            var normals = new List<Vector3> { Vector3.forward };
            var triangles = new List<int>();

            for (int i = 0; i < rim; i++)
            {
                float angle = i / (float)rim * Mathf.PI * 2f;
                float radius = i % 2 == 0 ? 1f : 0.32f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                normals.Add(Vector3.forward);
            }

            for (int i = 0; i < rim; i++)
            {
                int a = 1 + i;
                int b = 1 + (i + 1) % rim;
                triangles.Add(0); triangles.Add(b); triangles.Add(a);
                triangles.Add(0); triangles.Add(a); triangles.Add(b);
            }

            var mesh = new Mesh { name = "CameraFlash" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Integer hash, because the spawn counter never stops growing.
        /// </summary>
        /// <remarks>
        /// The <c>sin(x * 12.9898) * 43758.5453</c> trick used for static layout
        /// elsewhere loses its randomness once <c>x</c> is large enough that a
        /// float cannot resolve the fractional part of the sine's argument — a
        /// few hundred thousand spawns in, flashes would start favouring a
        /// handful of seats. An integer mix has no such drift.
        /// </remarks>
        private static uint Scramble(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352dU;
            x ^= x >> 15;
            x *= 0x846ca68bU;
            x ^= x >> 16;
            return x;
        }

        /// <summary>Uniform in [0, 1) from a seed.</summary>
        private static float Unit(uint seed) => (Scramble(seed) & 0xFFFFFFu) / 16777216f;
    }
}
