using System.Collections.Generic;
using UnityEngine;

namespace CyberGoal.Unity.Environment
{
    /// <summary>
    /// A small cheerleading squad performing on the touchline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where they stand.</b> Beside the pitch, level with the penalty area,
    /// facing in. Anywhere in the goalmouth or along the shooter's line of sight
    /// would compete with the only thing the player is meant to be looking at.
    /// </para>
    /// <para>
    /// <b>Why five meshes and five draw calls.</b> Each performer is a head, a
    /// body, two arms, two legs and two pom-poms, all rigid parts. A GameObject
    /// hierarchy per performer is dozens of transforms and renderers for a
    /// background prop, so instead each part is one shared mesh drawn with
    /// <see cref="Graphics.DrawMeshInstanced"/>, and the matrices are recomputed
    /// each frame from the routine — a few dozen multiplies for the whole squad.
    /// Arms and legs are authored with the joint at their origin, so a rotation
    /// in the matrix swings them from the shoulder or hip rather than about the
    /// middle of the limb.
    /// </para>
    /// <para>
    /// <b>Pom-poms are unlit.</b> A figure with its arms up is a celebrating fan;
    /// a figure with its arms up holding two bright clusters is a cheerleader.
    /// The pom-poms are therefore the brightest thing on the touchline: unlit,
    /// in the team colour, pushed past 1.0 so bloom catches them.
    /// </para>
    /// <para>
    /// Not final art (§21): no faces, no hair beyond a bow, no cloth. What it is
    /// is a proportioned person with shoulders, a waist and a skirt, which is
    /// what carries at fifteen metres.
    /// </para>
    /// </remarks>
    public sealed class CheerleaderSquad : MonoBehaviour
    {
        /// <summary>Unity's instanced draw call takes at most 1023 matrices; arms come in pairs.</summary>
        private const int BatchLimit = 1023;

        /// <summary>Vertices around each lofted ring. Eight is plenty at this distance.</summary>
        private const int Segments = 8;

        // Figure-space joints in metres: standing at the origin, +Y up, facing
        // +Z, 1.68 m tall. The arm and leg meshes hang from the origin, so these
        // are also the pivots.
        private static readonly Vector3 ShoulderL = new Vector3(-0.20f, 1.40f, 0f);
        private static readonly Vector3 ShoulderR = new Vector3(0.20f, 1.40f, 0f);
        private static readonly Vector3 HipL = new Vector3(-0.09f, 0.86f, 0f);
        private static readonly Vector3 HipR = new Vector3(0.09f, 0.86f, 0f);
        private const float ArmLength = 0.56f;
        private const float LegLength = 0.80f;

        // Four tones is enough variety for a squad of eight; nobody is inspecting
        // them, but a single tone across the line reads as clones.
        private static readonly Color[] SkinTones =
        {
            new Color(0.87f, 0.68f, 0.56f),
            new Color(0.76f, 0.56f, 0.42f),
            new Color(0.55f, 0.38f, 0.27f),
            new Color(0.36f, 0.24f, 0.17f)
        };

        private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorProperty = Shader.PropertyToID("_Color");

        private struct Performer
        {
            public Vector3 Position;
            public Quaternion Facing;
            /// <summary>Offset into the routine, in beats.</summary>
            public float Phase;
        }

        private Performer[] _performers;

        private Mesh _bodyMesh;
        private Mesh _headMesh;
        private Mesh _armMesh;
        private Mesh _legMesh;
        private Mesh _pomMesh;

        private Material _litMaterial;
        private Material _pomMaterial;

        private Matrix4x4[] _body;
        private Matrix4x4[] _head;
        private Matrix4x4[] _arm;
        private Matrix4x4[] _leg;
        private Matrix4x4[] _pom;

        private MaterialPropertyBlock _bodyBlock;
        private MaterialPropertyBlock _headBlock;
        private MaterialPropertyBlock _limbBlock;
        private MaterialPropertyBlock _pomBlock;

        private float _intensity;
        private float _target;
        private float _beat;

        /// <summary>
        /// Build <paramref name="count"/> performers split across both touchlines,
        /// in kit and pom-poms of <paramref name="teamColour"/>.
        /// </summary>
        public void Build(int count, Color teamColour)
        {
            count = Mathf.Max(0, Mathf.Min(count, BatchLimit / 2));

            _bodyMesh = BodyMesh();
            _headMesh = HeadMesh();
            _armMesh = ArmMesh();
            _legMesh = LegMesh();
            _pomMesh = PomPomMesh();

            _litMaterial = new Material(ShaderLibrary.Lit) { color = Color.white, enableInstancing = true };
            ShaderLibrary.SetSmoothness(_litMaterial, 0.2f);
            _pomMaterial = new Material(ShaderLibrary.Unlit) { color = Color.white, enableInstancing = true };

            _performers = new Performer[count];
            _body = new Matrix4x4[count];
            _head = new Matrix4x4[count];
            _arm = new Matrix4x4[count * 2];
            _leg = new Matrix4x4[count * 2];
            _pom = new Matrix4x4[count * 2];

            var bodyColours = new Vector4[count];
            var headColours = new Vector4[count];
            var limbColours = new Vector4[count * 2];
            var pomColours = new Vector4[count * 2];

            // Half on each side. The two lines mirror each other across the
            // pitch, which is how a real squad splits when there are two ends
            // to play to.
            int left = count / 2;
            const int perRow = 8;

            for (int i = 0; i < count; i++)
            {
                bool onLeft = i < left;
                int slot = onLeft ? i : i - left;
                int side = onLeft ? -1 : 1;
                int row = slot / perRow;
                int place = slot % perRow;

                // Spread along the touchline from z = -2 to z = +6, so the line
                // sits level with the run-up rather than behind the goal. Extra
                // rows step outward, away from the pitch.
                float z = -2f + (place + 0.5f) * (8f / perRow) + (Hash(i * 2.3f) - 0.5f) * 0.2f;
                float x = side * (14f + row * 1.2f + (Hash(i * 4.1f) - 0.5f) * 0.3f);

                _performers[i] = new Performer
                {
                    Position = new Vector3(x, 0f, z),
                    Facing = Quaternion.LookRotation(new Vector3(-side, 0f, 0f), Vector3.up),
                    // A ripple along the line plus a little personal error. A pure
                    // ripple is a Mexican wave; pure random is a crowd. Mostly
                    // ripple, slightly late, is a routine performed by people.
                    Phase = place * 0.12f + (Hash(i * 7.7f) - 0.5f) * 0.06f
                };

                Color skin = SkinTones[Mathf.Min(SkinTones.Length - 1, (int)(Hash(i * 3.3f) * SkinTones.Length))];
                Color kit = teamColour * (0.88f + Hash(i * 5.1f) * 0.24f);
                // Lifted toward white before the HDR push so a dark team colour
                // still gives a bright pom-pom rather than a bright shadow.
                Color pom = Color.Lerp(teamColour, Color.white, 0.35f) * 2.2f;

                bodyColours[i] = kit;
                headColours[i] = skin;
                limbColours[i * 2] = skin;
                limbColours[i * 2 + 1] = skin;
                pomColours[i * 2] = pom;
                pomColours[i * 2 + 1] = pom;
            }

            _bodyBlock = Block(bodyColours);
            _headBlock = Block(headColours);
            // Arms and legs share one block: same colours in the same order, and
            // a block is only read at draw time, so two draws can use it.
            _limbBlock = Block(limbColours);
            _pomBlock = Block(pomColours);
        }

        /// <summary>0 is a gentle sway between kicks, 1 is the full routine.</summary>
        public void SetIntensity(float intensity) => _target = Mathf.Clamp01(intensity);

        private void Update()
        {
            if (_performers == null || _performers.Length == 0) return;

            float dt = Time.deltaTime;
            _intensity = Mathf.MoveTowards(_intensity, _target, dt * 1.8f);

            // The beat is accumulated rather than taken as time × tempo, so a
            // tempo change speeds the routine up from where it is instead of
            // teleporting every performer to a new point in the phrase.
            float tempo = Mathf.Lerp(1.4f, 2.6f, _intensity);
            _beat += tempo * dt;

            float raise = Mathf.Lerp(45f, 150f, _intensity);
            float stepAmplitude = Mathf.Lerp(0.04f, 0.26f, _intensity);
            float hopAmplitude = 0.08f * _intensity * _intensity;
            float kick = Mathf.Lerp(4f, 48f, _intensity * _intensity);
            float swing = Mathf.Lerp(20f, 8f, _intensity);
            // At idle the arms alternate like a casual sway; at full intensity
            // they punch up together. The split between them closes with
            // intensity rather than switching, so there is no visible mode change.
            float armSplit = Mathf.Lerp(0.5f, 0f, _intensity);

            for (int i = 0; i < _performers.Length; i++)
            {
                Performer p = _performers[i];
                float b = _beat + p.Phase;

                // One side-step per two beats, one hop per beat. Both in the
                // performer's own frame, so the line steps along the touchline
                // whichever side of the pitch it is on.
                float stride = Mathf.Sin(b * Mathf.PI);
                var offset = new Vector3(stride * stepAmplitude, Mathf.Abs(stride) * hopAmplitude, 0f);
                Matrix4x4 root = Matrix4x4.TRS(p.Position + p.Facing * offset, p.Facing, Vector3.one);

                _body[i] = root;
                _head[i] = root;

                for (int s = 0; s < 2; s++)
                {
                    int side = s == 0 ? -1 : 1;
                    int k = i * 2 + s;

                    // Positive Z rotation carries a hanging limb toward +X, so the
                    // outward direction is the sign of the shoulder's x.
                    float lift = 0.5f - 0.5f * Mathf.Cos((b + (s == 0 ? armSplit : 0f)) * Mathf.PI * 2f);
                    Quaternion armRotation = Quaternion.Euler(stride * swing * side, 0f, side * (12f + lift * raise));
                    Matrix4x4 arm = root * Matrix4x4.TRS(s == 0 ? ShoulderL : ShoulderR, armRotation, Vector3.one);
                    _arm[k] = arm;
                    _pom[k] = arm * Matrix4x4.TRS(new Vector3(0f, -ArmLength - 0.03f, 0f), Quaternion.identity, Vector3.one);

                    // Legs alternate beats. Negative X rotation swings a hanging
                    // limb forward (+Z), which is the direction a kick goes.
                    float legLift = Mathf.Max(0f, Mathf.Sin((b + (s == 0 ? 1f : 0f)) * Mathf.PI));
                    Quaternion legRotation = Quaternion.Euler(-legLift * kick, 0f, 0f);
                    _leg[k] = root * Matrix4x4.TRS(s == 0 ? HipL : HipR, legRotation, Vector3.one);
                }
            }

            Graphics.DrawMeshInstanced(_bodyMesh, 0, _litMaterial, _body, _body.Length, _bodyBlock);
            Graphics.DrawMeshInstanced(_headMesh, 0, _litMaterial, _head, _head.Length, _headBlock);
            Graphics.DrawMeshInstanced(_armMesh, 0, _litMaterial, _arm, _arm.Length, _limbBlock);
            Graphics.DrawMeshInstanced(_legMesh, 0, _litMaterial, _leg, _leg.Length, _limbBlock);
            Graphics.DrawMeshInstanced(_pomMesh, 0, _pomMaterial, _pom, _pom.Length, _pomBlock);
        }

        private static MaterialPropertyBlock Block(Vector4[] colours)
        {
            var block = new MaterialPropertyBlock();
            block.SetVectorArray(ColorProperty, colours);
            block.SetVectorArray(LegacyColorProperty, colours);
            return block;
        }

        // ---- Meshes -------------------------------------------------------

        /// <summary>
        /// Skirt, torso, neck and a hair bow, all in kit colour.
        /// </summary>
        /// <remarks>
        /// The waist is the narrowest ring and the skirt flares below it. Without
        /// that pinch the torso-plus-skirt is one cone, and a cone with a head on
        /// it is a traffic bollard. The bow is the cheapest single cue that says
        /// cheerleader rather than fan, and it is in the body mesh because it
        /// needs to be kit colour, not skin colour.
        /// </remarks>
        private static Mesh BodyMesh()
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();

            // Skirt: hem to waist.
            Tube(v, n, t, new Vector3(0f, 0.60f, 0f), new Vector3(0f, 0.88f, 0f),
                new Vector2(0.27f, 0.21f), new Vector2(0.17f, 0.125f), capBottom: true, capTop: false);
            // Torso: waist pinch, then chest, then a slight taper into the shoulders.
            Tube(v, n, t, new Vector3(0f, 0.88f, 0f), new Vector3(0f, 1.04f, 0f),
                new Vector2(0.17f, 0.125f), new Vector2(0.145f, 0.10f), capBottom: false, capTop: false);
            Tube(v, n, t, new Vector3(0f, 1.04f, 0f), new Vector3(0f, 1.26f, 0f),
                new Vector2(0.145f, 0.10f), new Vector2(0.19f, 0.115f), capBottom: false, capTop: false);
            Tube(v, n, t, new Vector3(0f, 1.26f, 0f), new Vector3(0f, 1.40f, 0f),
                new Vector2(0.19f, 0.115f), new Vector2(0.185f, 0.10f), capBottom: false, capTop: true);
            // Neck. Uncapped: the head sits over the top of it.
            Tube(v, n, t, new Vector3(0f, 1.40f, 0f), new Vector3(0f, 1.50f, 0f),
                new Vector2(0.05f, 0.05f), new Vector2(0.05f, 0.05f), capBottom: false, capTop: false);
            // Bow at the back of the head, with ribbon tails hanging below it.
            Box(v, n, t, new Vector3(0f, 1.60f, -0.12f), new Vector3(0.065f, 0.035f, 0.03f));
            Tube(v, n, t, new Vector3(0f, 1.32f, -0.17f), new Vector3(0f, 1.58f, -0.12f),
                new Vector2(0.025f, 0.02f), new Vector2(0.04f, 0.03f), capBottom: true, capTop: false);

            return Finish("CheerleaderBody", v, n, t);
        }

        /// <summary>A skull-shaped ellipsoid: taller than wide, deeper than wide.</summary>
        private static Mesh HeadMesh()
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            Ellipsoid(v, n, t, new Vector3(0f, 1.56f, 0f), new Vector3(0.082f, 0.102f, 0.092f), 6);
            return Finish("CheerleaderHead", v, n, t);
        }

        /// <summary>An arm hanging from the origin, tapering toward the wrist. The pom-pom covers the hand.</summary>
        private static Mesh ArmMesh()
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            Tube(v, n, t, new Vector3(0f, -ArmLength, 0f), Vector3.zero,
                new Vector2(0.028f, 0.028f), new Vector2(0.05f, 0.05f), capBottom: true, capTop: true);
            return Finish("CheerleaderArm", v, n, t);
        }

        /// <summary>A leg hanging from the origin, with a boot forward of the ankle so the figure has a stance.</summary>
        private static Mesh LegMesh()
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            Tube(v, n, t, new Vector3(0f, -LegLength, 0f), Vector3.zero,
                new Vector2(0.038f, 0.038f), new Vector2(0.075f, 0.07f), capBottom: false, capTop: false);
            Box(v, n, t, new Vector3(0f, -LegLength - 0.035f, 0.035f), new Vector3(0.045f, 0.045f, 0.10f));
            return Finish("CheerleaderLeg", v, n, t);
        }

        /// <summary>
        /// A cluster of overlapping lobes centred on the origin.
        /// </summary>
        /// <remarks>
        /// A single sphere at the end of an arm is a ball, and a ball in each
        /// hand is a juggler. Five lobes give the lumpy, slightly irregular
        /// silhouette of a pom-pom, and at 200 vertices the cluster costs nothing.
        /// </remarks>
        private static Mesh PomPomMesh()
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();

            Ellipsoid(v, n, t, Vector3.zero, new Vector3(0.10f, 0.095f, 0.10f), 4);
            Vector3[] lobes =
            {
                new Vector3(0.06f, 0.03f, 0.02f), new Vector3(-0.055f, 0.04f, -0.03f),
                new Vector3(0.01f, -0.06f, 0.05f), new Vector3(-0.02f, -0.02f, -0.06f)
            };
            foreach (Vector3 lobe in lobes)
            {
                Ellipsoid(v, n, t, lobe, new Vector3(0.07f, 0.065f, 0.07f), 4);
            }

            return Finish("CheerleaderPomPom", v, n, t);
        }

        private static Mesh Finish(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Winding throughout follows Unity's rule that a front face is clockwise
        // when seen from outside. Rings run from +X toward +Z, so seen from
        // outside the next ring vertex is to the right and the ring above is up;
        // (here, up, right) is clockwise.

        /// <summary>Loft an elliptical tube between two ring centres, optionally closed at either end.</summary>
        private static void Tube(List<Vector3> v, List<Vector3> n, List<int> t,
            Vector3 bottom, Vector3 top, Vector2 bottomRadius, Vector2 topRadius, bool capBottom, bool capTop)
        {
            int b = v.Count;
            for (int ring = 0; ring < 2; ring++)
            {
                Vector3 centre = ring == 0 ? bottom : top;
                Vector2 radius = ring == 0 ? bottomRadius : topRadius;
                for (int s = 0; s < Segments; s++)
                {
                    float angle = s / (float)Segments * Mathf.PI * 2f;
                    var unit = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    v.Add(centre + new Vector3(unit.x * radius.x, 0f, unit.z * radius.y));
                    n.Add(unit);
                }
            }

            for (int s = 0; s < Segments; s++)
            {
                int next = (s + 1) % Segments;
                int i0 = b + s;
                int i1 = b + next;
                int i2 = b + Segments + s;
                int i3 = b + Segments + next;
                t.Add(i0); t.Add(i2); t.Add(i1);
                t.Add(i1); t.Add(i2); t.Add(i3);
            }

            if (capBottom) Cap(v, n, t, bottom, bottomRadius, false);
            if (capTop) Cap(v, n, t, top, topRadius, true);
        }

        /// <summary>
        /// A fan closing one end of a tube.
        /// </summary>
        /// <remarks>
        /// The ring is duplicated with the cap's normal rather than shared with
        /// the wall, so the lit edge stays crisp instead of smearing the wall's
        /// shading across the top of a shoulder.
        /// </remarks>
        private static void Cap(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 centre, Vector2 radius, bool up)
        {
            Vector3 normal = up ? Vector3.up : -Vector3.up;
            int c = v.Count;
            v.Add(centre);
            n.Add(normal);
            for (int s = 0; s < Segments; s++)
            {
                float angle = s / (float)Segments * Mathf.PI * 2f;
                v.Add(centre + new Vector3(Mathf.Cos(angle) * radius.x, 0f, Mathf.Sin(angle) * radius.y));
                n.Add(normal);
            }

            for (int s = 0; s < Segments; s++)
            {
                int a = c + 1 + s;
                int d = c + 1 + (s + 1) % Segments;
                // Seen from above, +Z is up the screen and the ring runs
                // anticlockwise, so the fan is wound backwards for the top face.
                if (up) { t.Add(c); t.Add(d); t.Add(a); }
                else { t.Add(c); t.Add(a); t.Add(d); }
            }
        }

        private static void Ellipsoid(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 centre, Vector3 radius, int stacks)
        {
            int b = v.Count;
            for (int y = 0; y <= stacks; y++)
            {
                float phi = y / (float)stacks * Mathf.PI;
                for (int s = 0; s <= Segments; s++)
                {
                    float theta = s / (float)Segments * Mathf.PI * 2f;
                    var unit = new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));
                    v.Add(centre + Vector3.Scale(unit, radius));
                    n.Add(unit);
                }
            }

            // Stacks run downward from the crown, so the "next row" is below:
            // the opposite of the tube, hence the opposite winding.
            int stride = Segments + 1;
            for (int y = 0; y < stacks; y++)
            {
                for (int s = 0; s < Segments; s++)
                {
                    int i0 = b + y * stride + s;
                    int i1 = i0 + 1;
                    int i2 = i0 + stride;
                    int i3 = i2 + 1;
                    t.Add(i0); t.Add(i1); t.Add(i2);
                    t.Add(i1); t.Add(i3); t.Add(i2);
                }
            }
        }

        private static void Box(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 centre, Vector3 half)
        {
            int b = v.Count;
            Vector3[] corners =
            {
                new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1),
                new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1)
            };
            foreach (Vector3 c in corners)
            {
                v.Add(centre + Vector3.Scale(c, half));
                n.Add(c.normalized);
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
            foreach (int i in faces) t.Add(b + i);
        }

        private static float Hash(float v)
        {
            float s = Mathf.Sin(v * 12.9898f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
    }
}
