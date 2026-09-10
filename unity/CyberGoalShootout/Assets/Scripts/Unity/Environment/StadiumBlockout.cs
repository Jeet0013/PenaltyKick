using CyberGoal.Core.Physics;
using CyberGoal.Unity.Core;
using UnityEngine;

namespace CyberGoal.Unity.Environment
{
    /// <summary>
    /// The pitch, the goal, the stands and the crowd (§55 Phase A, §20).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A blockout, per Phase A, and built in code rather than authored as a scene.
    /// That is a deliberate call: a hand-written <c>.unity</c> file is YAML full of
    /// GUIDs and file IDs, and one wrong reference produces a scene that opens
    /// broken with no useful error. Generated geometry opens correctly in any
    /// editor version and is diffable, which matters more at this stage than being
    /// art-directable.
    /// </para>
    /// <para>
    /// <b>Pitch markings are the highest-value detail here, not the neon.</b>
    /// Without the goal area, penalty area, arc and spot, the eye has nothing to
    /// measure against: the goal could be any size at any distance, and a player
    /// cannot judge whether the keeper is off their line. §20 warns against
    /// overloading the scene with neon at the cost of gameplay legibility, and this
    /// is the concrete form of that warning.
    /// </para>
    /// <para>
    /// The crowd is instanced quads at this stage. §6 and §7 of the art direction
    /// require recognisable human figures at every LOD, so <b>the crowd here is
    /// PLACEHOLDER</b> and is marked as such in the status document. What is real
    /// is the tiering: near, mid and far bands with separate budgets, so dropping
    /// characters in means replacing what each band draws rather than rebuilding
    /// the bowl.
    /// </para>
    /// </remarks>
    public sealed class StadiumBlockout : MonoBehaviour
    {
        private const float GoalZ = -(float)BallPhysics.Field.SpotToGoal;
        private const float HalfWidth = (float)BallPhysics.Field.GoalWidth / 2f;
        private const float GoalHeight = (float)BallPhysics.Field.GoalHeight;
        private const float PostRadius = (float)BallPhysics.Field.PostRadius;

        public Color homeNeon = new Color(0.22f, 0.84f, 1f);
        public Color awayNeon = new Color(1f, 0.3f, 0.24f);

        public void Build(QualityPreset quality)
        {
            BuildTurf();
            BuildMarkings();
            BuildGoal();
            BuildStands();
            BuildCrowd(quality.CrowdCount);
            BuildLighting(quality);
        }

        private void BuildTurf()
        {
            var turf = GameObject.CreatePrimitive(PrimitiveType.Plane);
            turf.name = "Turf";
            turf.transform.SetParent(transform, false);
            // Unity's plane is 10 units across at scale 1.
            turf.transform.localScale = new Vector3(9f, 1f, 12f);
            turf.transform.position = new Vector3(0f, 0f, GoalZ + 30f);
            DestroyImmediate(turf.GetComponent<Collider>());

            // Deep pitch green, and matte. A glossy pitch mirrors the floodlights
            // as a white river down the middle of the penalty area and the whole
            // surface reads as water rather than grass — which is exactly what
            // happened in the prototype before the roughness was raised.
            turf.GetComponent<Renderer>().sharedMaterial =
                Materials.Lit(new Color(0.10f, 0.29f, 0.14f), smoothness: 0.06f);
        }

        /// <summary>Regulation markings, drawn into a texture rather than as geometry.</summary>
        private void BuildMarkings()
        {
            const float worldWidth = 60f;
            const float worldDepth = 24f;
            const float zFar = -14f;
            const int width = 2048;
            int height = Mathf.RoundToInt(width * worldDepth / worldWidth);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 0);

            float Px(float x) => (x + worldWidth / 2f) / worldWidth * width;
            float Py(float z) => (z - zFar) / worldDepth * height;
            // 12 cm of paint: the real width. Thinner reads as a scratch, thicker
            // as a road marking.
            float lineWidth = 0.12f / worldWidth * width;

            void HLine(float x0, float x1, float z)
            {
                int y = Mathf.RoundToInt(Py(z));
                for (int x = Mathf.RoundToInt(Px(x0)); x <= Mathf.RoundToInt(Px(x1)); x++)
                    Stamp(pixels, width, height, x, y, lineWidth);
            }

            void VLine(float x, float z0, float z1)
            {
                int px = Mathf.RoundToInt(Px(x));
                for (int y = Mathf.RoundToInt(Py(z0)); y <= Mathf.RoundToInt(Py(z1)); y++)
                    Stamp(pixels, width, height, px, y, lineWidth);
            }

            // Goal line, full width.
            HLine(-worldWidth / 2f, worldWidth / 2f, GoalZ);

            // Goal area (5.5 m, half-width 9.16) and penalty area (16.5 m, 20.16).
            foreach ((float depth, float half) in new[] { (5.5f, 9.16f), (16.5f, 20.16f) })
            {
                float z = GoalZ + depth;
                HLine(-half, half, z);
                VLine(-half, GoalZ, z);
                VLine(half, GoalZ, z);
            }

            // The penalty arc: a 9.15 m circle about the spot, clipped to the part
            // OUTSIDE the penalty area. Drawing the whole circle is a common
            // mistake and instantly wrong to anyone who has stood on a pitch.
            const float arcRadius = 9.15f;
            float boxEdge = GoalZ + 16.5f;
            float theta = Mathf.Acos(Mathf.Clamp(boxEdge / arcRadius, -1f, 1f));
            for (float a = -theta; a <= theta; a += 0.002f)
            {
                float wx = arcRadius * Mathf.Sin(a);
                float wz = arcRadius * Mathf.Cos(a);
                Stamp(pixels, width, height,
                    Mathf.RoundToInt(Px(wx)), Mathf.RoundToInt(Py(wz)), lineWidth);
            }

            // The penalty spot: 22 cm across, the same width as the ball on it.
            int spotRadius = Mathf.RoundToInt(0.11f / worldWidth * width);
            for (int dy = -spotRadius; dy <= spotRadius; dy++)
            for (int dx = -spotRadius; dx <= spotRadius; dx++)
            {
                if (dx * dx + dy * dy > spotRadius * spotRadius) continue;
                Stamp(pixels, width, height,
                    Mathf.RoundToInt(Px(0)) + dx, Mathf.RoundToInt(Py(0)) + dy, 1f);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.anisoLevel = 16;

            var decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            decal.name = "Markings";
            decal.transform.SetParent(transform, false);
            decal.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            decal.transform.localScale = new Vector3(worldWidth, worldDepth, 1f);
            // 6 mm above the turf: enough to beat z-fighting, too little to see.
            decal.transform.position = new Vector3(0f, 0.006f, (10f + zFar) / 2f);
            DestroyImmediate(decal.GetComponent<Collider>());

            Material material = Materials.Lit(Color.white, smoothness: 0.1f, transparent: true);
            material.mainTexture = texture;
            decal.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Stamp(Color32[] pixels, int width, int height, int cx, int cy, float thickness)
        {
            int r = Mathf.Max(1, Mathf.RoundToInt(thickness / 2f));
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx;
                int y = cy + dy;
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                pixels[y * width + x] = new Color32(255, 255, 255, 210);
            }
        }

        private void BuildGoal()
        {
            var goal = new GameObject("Goal");
            goal.transform.SetParent(transform, false);

            Material carbon = Materials.Lit(new Color(0.04f, 0.05f, 0.07f), smoothness: 0.66f);
            Material trim = Materials.Lit(new Color(0.04f, 0.08f, 0.09f), 0.6f, emission: homeNeon * 2.4f);

            // Posts and bar are visual only — no colliders. The goal test lives in
            // Core and interpolates the crossing point, which a trigger volume
            // cannot do: at 31 m/s the ball moves 26 cm per step, four times the
            // width of a post, so a collider would let fast shots tunnel through.
            foreach (int side in new[] { -1, 1 })
            {
                Cylinder(goal.transform, $"Post{side}", carbon,
                    new Vector3(side * HalfWidth, GoalHeight / 2f, GoalZ),
                    PostRadius, GoalHeight, Vector3.up);

                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = $"PostTrim{side}";
                strip.transform.SetParent(goal.transform, false);
                strip.transform.localScale = new Vector3(0.02f, GoalHeight, 0.03f);
                strip.transform.position = new Vector3(
                    side * (HalfWidth - PostRadius), GoalHeight / 2f, GoalZ + PostRadius);
                DestroyImmediate(strip.GetComponent<Collider>());
                strip.GetComponent<Renderer>().sharedMaterial = trim;
            }

            Cylinder(goal.transform, "Crossbar", carbon,
                new Vector3(0f, GoalHeight, GoalZ),
                PostRadius, BallPhysics.Field.GoalWidth + PostRadius * 2f, Vector3.right);

            BuildNet(goal.transform);
        }

        /// <summary>The net, as line segments. It is mostly holes; a mesh would lie.</summary>
        private void BuildNet(Transform parent)
        {
            var net = new GameObject("Net");
            net.transform.SetParent(parent, false);

            var lines = net.AddComponent<LineRenderer>();
            lines.useWorldSpace = true;
            lines.widthMultiplier = 0.012f;
            lines.material = Materials.Unlit(new Color(0.87f, 0.95f, 1f, 0.24f), transparent: true);

            const float depth = 1.9f;
            var points = new System.Collections.Generic.List<Vector3>();

            // Drawn as one continuous polyline that doubles back on itself, because
            // a LineRenderer has a single strip. The doubling-back segments run
            // along lines already drawn, so they are invisible.
            const int cells = 16;
            for (int i = 0; i <= cells; i++)
            {
                float x = -HalfWidth + (float)BallPhysics.Field.GoalWidth * i / cells;
                points.Add(new Vector3(x, 0f, GoalZ - depth));
                points.Add(new Vector3(x, GoalHeight, GoalZ - depth));
                points.Add(new Vector3(x, 0f, GoalZ - depth));
            }
            for (int i = 0; i <= 8; i++)
            {
                float y = GoalHeight * i / 8f;
                points.Add(new Vector3(-HalfWidth, y, GoalZ - depth));
                points.Add(new Vector3(HalfWidth, y, GoalZ - depth));
                points.Add(new Vector3(-HalfWidth, y, GoalZ - depth));
            }

            lines.positionCount = points.Count;
            lines.SetPositions(points.ToArray());
        }

        private void BuildStands()
        {
            var stands = new GameObject("Stands");
            stands.transform.SetParent(transform, false);

            // Lifted off pure black. At near-zero value the bowl is
            // indistinguishable from the sky behind it, and the crowd then reads as
            // confetti hanging in empty space rather than as people sitting down.
            Material material = Materials.Lit(new Color(0.10f, 0.13f, 0.16f), smoothness: 0.1f);

            foreach ((float radius, float height, float y) in
                     new[] { (34f, 9f, 3f), (44f, 15f, 8f) })
            {
                var tier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tier.name = $"Tier{radius}";
                tier.transform.SetParent(stands.transform, false);
                tier.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
                tier.transform.position = new Vector3(0f, y, GoalZ + 8f);
                DestroyImmediate(tier.GetComponent<Collider>());
                tier.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        /// <summary>
        /// The crowd, as instanced quads. PLACEHOLDER — see the class remarks.
        /// </summary>
        /// <remarks>
        /// Seated in rows. The obvious implementation walks the angle while taking
        /// the tier from <c>i % rows</c>, which makes every consecutive spectator
        /// jump to a different height and produces a zig-zag of confetti rather
        /// than a crowd. People sit in rows; filling one row at a time, with half a
        /// seat of stagger so rows interlock, is the whole difference.
        /// </remarks>
        private void BuildCrowd(int count)
        {
            var crowd = new GameObject("Crowd");
            crowd.transform.SetParent(transform, false);

            var mesh = BuildQuad();
            Material material = Materials.Unlit(Color.white, transparent: true, vertexColour: true);

            const int rows = 9;
            int perRow = Mathf.Max(1, Mathf.CeilToInt(count / (float)rows));

            for (int i = 0; i < count; i++)
            {
                int row = i / perRow;
                int seat = i % perRow;
                float angle = (seat + (row % 2) * 0.5f) / perRow * Mathf.PI * 2f;

                float ring = 26.5f + row * 1.9f;
                float height = 3.0f + row * 1.32f + (Random.value - 0.5f) * 0.18f;

                var person = new GameObject("Spectator");
                person.transform.SetParent(crowd.transform, false);
                person.transform.position = new Vector3(
                    Mathf.Cos(angle) * ring, height, Mathf.Sin(angle) * ring + GoalZ + 8f);
                person.transform.LookAt(new Vector3(0f, 2f, GoalZ));
                person.transform.localScale = new Vector3(0.24f, 0.42f, 1f);

                var filter = person.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = person.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                // Split the bowl between the sides with a scatter of neutrals, and
                // keep it dark: these are unlit cards 30 m away in a night stadium,
                // and at full value they glow brighter than the pitch.
                bool neutral = Random.value < 0.45f;
                Color tint = neutral
                    ? new Color(0.42f, 0.49f, 0.56f)
                    : angle < Mathf.PI ? homeNeon : awayNeon;
                renderer.material.color = tint * (0.14f + Random.value * 0.26f);
            }
        }

        private void BuildLighting(QualityPreset quality)
        {
            var key = new GameObject("KeyLight");
            key.transform.SetParent(transform, false);
            var light = key.AddComponent<Light>();
            light.type = LightType.Directional;
            // Warm white, not blue-white. Stadium floods are metal-halide and read
            // slightly warm on grass; a blue-white key pushes every green channel
            // past the red and leaves the pitch looking cyan.
            light.color = new Color(1f, 0.95f, 0.86f);
            light.intensity = 2.15f;
            light.shadows = quality.Shadows ? LightShadows.Soft : LightShadows.None;
            key.transform.rotation = Quaternion.Euler(52f, -34f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.11f, 0.16f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.05f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 150f;
        }

        private static void Cylinder(Transform parent, string name, Material material,
            Vector3 position, double radius, double length, Vector3 axis)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            // Unity's cylinder is 2 units tall at scale 1.
            go.transform.localScale = new Vector3((float)radius * 2f, (float)length / 2f, (float)radius * 2f);
            go.transform.position = position;
            go.transform.up = axis;
            DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "SpectatorQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            return mesh;
        }
    }

    /// <summary>Material helpers, so the URP shader name is written once.</summary>
    /// <summary>Material helpers. Shader choice lives in <see cref="ShaderLibrary"/>.</summary>
    internal static class Materials
    {
        internal static Material Lit(Color colour, float smoothness = 0.3f,
            Color? emission = null, bool transparent = false)
        {
            var material = new Material(ShaderLibrary.Lit) { color = colour };
            ShaderLibrary.SetSmoothness(material, smoothness);
            if (emission.HasValue) ShaderLibrary.SetEmission(material, emission.Value);
            if (transparent) ShaderLibrary.MakeTransparent(material);
            return material;
        }

        internal static Material Unlit(Color colour, bool transparent = false, bool vertexColour = false)
        {
            var material = new Material(ShaderLibrary.Unlit) { color = colour };
            if (transparent) ShaderLibrary.MakeTransparent(material);
            return material;
        }
    }
}
