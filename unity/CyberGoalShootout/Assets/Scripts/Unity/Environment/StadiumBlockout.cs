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

        /// <summary>The crowd, so the match can drive its mood (§19).</summary>
        public CrowdSystem Crowd { get; private set; }

        public void Build(QualityPreset quality)
        {
            BuildTurf();
            BuildMarkings();
            BuildGoal();
            BuildStands();
            BuildCrowd(quality.CrowdCount);
            BuildLighting(quality);
        }

        /// <summary>
        /// The playing surface.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A flat green plane is the clearest sign of a generated scene, and the
        /// fix is not more polygons — it is <em>variation</em>. Three things do
        /// almost all the work here, in this order of importance:
        /// </para>
        /// <list type="number">
        /// <item><b>Mown stripes.</b> The single strongest cue that a surface is a
        /// maintained sports pitch. Real stripes are the same grass bent in
        /// opposite directions, so the difference in value must be small — felt
        /// rather than seen. Overdo it and it reads as painted bands.</item>
        /// <item><b>Blade noise.</b> Breaks the flat fill at close range. Without
        /// it the turf is a solid colour and the eye reads "digital" immediately.</item>
        /// <item><b>Wear.</b> A penalty area is scuffed where players stand and
        /// worn around the spot. Perfectly even grass is a texture; uneven grass
        /// is a place.</item>
        /// </list>
        /// <para>
        /// Kept matte. An earlier version had a strong clearcoat, and the
        /// floodlight reflected as a white river down the middle of the penalty
        /// area — the whole pitch read as water rather than grass. Wet turf is
        /// glossier than dry turf by a little, not by a factor of four.
        /// </para>
        /// </remarks>
        private void BuildTurf()
        {
            var turf = GameObject.CreatePrimitive(PrimitiveType.Plane);
            turf.name = "Turf";
            turf.transform.SetParent(transform, false);
            // Unity's plane is 10 units across at scale 1.
            turf.transform.localScale = new Vector3(9f, 1f, 12f);
            turf.transform.position = new Vector3(0f, 0f, GoalZ + 30f);
            DestroyImmediate(turf.GetComponent<Collider>());

            Material material = Materials.Lit(Color.white, smoothness: 0.06f);
            material.mainTexture = TurfTexture();
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", material.mainTexture);
            // Tiled across the pitch. The stripes are baked at pitch scale so they
            // must not repeat within it; the noise can.
            if (material.HasProperty("_BaseMap_ST")) { }
            turf.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>Grass, stripes and wear, painted into one texture.</summary>
        private static Texture2D TurfTexture()
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var pixels = new Color32[size * size];

            // Deep pitch green. It has to carry more green than looks right in
            // isolation: a warm floodlight and a cyan rim both pull it away.
            var baseColour = new Color(0.075f, 0.26f, 0.115f);

            for (int y = 0; y < size; y++)
            {
                // 14 stripes across the pitch, alternating direction of cut.
                int stripe = (y * 14) / size;
                float band = stripe % 2 == 0 ? 1.055f : 0.945f;

                for (int x = 0; x < size; x++)
                {
                    // Blade noise: fine, high-frequency, low-amplitude.
                    float n = Hash01(x * 0.7f + y * 3.1f) * 0.16f - 0.08f;
                    // Broad wear: slow variation that lightens patches.
                    float wear = Mathf.Sin(x * 0.021f) * Mathf.Cos(y * 0.017f) * 0.05f;

                    float k = band + n + wear;
                    var c = new Color(
                        Mathf.Clamp01(baseColour.r * k),
                        Mathf.Clamp01(baseColour.g * k),
                        Mathf.Clamp01(baseColour.b * k));
                    pixels[y * size + x] = c;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.anisoLevel = 8;
            return texture;
        }

        private static float Hash01(float v)
        {
            float s = Mathf.Sin(v * 12.9898f) * 43758.5453f;
            return s - Mathf.Floor(s);
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

        /// <summary>Spectators, as instanced low-poly humanoids (§19, §48).</summary>
        private void BuildCrowd(int count)
        {
            var go = new GameObject("Crowd");
            go.transform.SetParent(transform, false);
            Crowd = go.AddComponent<CrowdSystem>();
            Crowd.Build(count, homeNeon, awayNeon);
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
