// Minimal UnityEngine surface, for compile-checking only. See README.md.
// Not shipped, never referenced by the Unity project.
#pragma warning disable CA1050, IDE0060, CS0067, CS0649

using System;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }
        public static void Destroy(Object o) { }
        public static void DestroyImmediate(Object o) { }
        public static void DontDestroyOnLoad(Object o) { }
        // Unity declares Instantiate on Object, so it is in scope unqualified
        // inside any MonoBehaviour. The first version of this stub put it on
        // GameObject and produced a compile error that did not exist in Unity.
        public static T Instantiate<T>(T original) where T : Object => original;
        public static T Instantiate<T>(T original, Transform parent) where T : Object => original;
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => 0;
    }

    public struct Vector2
    {
        public float x, y;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 right => new Vector2(1, 0);
        public static Vector2 up => new Vector2(0, 1);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.x * s, a.y * s);
    }

    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a;
        public static Vector3 Scale(Vector3 a, Vector3 b) => a;
        public static Vector3 Cross(Vector3 a, Vector3 b) => a;
        public static float Dot(Vector3 a, Vector3 b) => 0;
        public static Vector3 forward => new Vector3(0, 0, 1);
        public Vector3 normalized => this;
        public float sqrMagnitude => 0;
        public float magnitude => 0;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        // Unity provides this implicit narrowing, which is what makes
        // `Vector2 p = Input.mousePosition;` legal.
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
    }

    public struct Quaternion
    {
        public static Quaternion identity => default;
        public static Quaternion Euler(float x, float y, float z) => default;
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) => default;
    }

    public struct Matrix4x4
    {
        public static Matrix4x4 identity => default;
        public static Matrix4x4 TRS(Vector3 pos, Quaternion rot, Vector3 scale) => default;
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => a;
    }

    public struct Bounds
    {
        public Bounds(Vector3 centre, Vector3 size) { }
    }

    public struct BoneWeight
    {
        public int boneIndex0, boneIndex1, boneIndex2, boneIndex3;
        public float weight0, weight1, weight2, weight3;
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public static implicit operator Vector4(Color c) => default;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color operator *(Color c, float s) => new Color(c.r * s, c.g * s, c.b * s, c.a);
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        // Unity provides both directions implicitly, which is what makes
        // `Color32[] pixels; pixels[i] = someColor;` legal.
        public static implicit operator Color32(Color c) => default;
        public static implicit operator Color(Color32 c) => default;
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Vector2 position => new Vector2(x, y);
        public Vector2 size => new Vector2(width, height);
        public static bool operator ==(Rect a, Rect b) => false;
        public static bool operator !=(Rect a, Rect b) => true;
        public override bool Equals(object o) => false;
        public override int GetHashCode() => 0;
    }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public static float Floor(float f) => 0;
        public static float Abs(float f) => f;
        public static float SmoothStep(float a, float b, float t) => a;
        public static float Sqrt(float f) => 0;
        public static float Sin(float f) => 0;
        public static float Cos(float f) => 0;
        public static float Acos(float f) => 0;
        public static float Exp(float f) => 0;
        public static float Min(float a, float b) => a;
        public static int Min(int a, int b) => a;
        public static float Max(float a, float b) => a;
        public static int Max(int a, int b) => a;
        public static float Clamp(float v, float a, float b) => v;
        public static float Clamp01(float v) => v;
        public static float Lerp(float a, float b, float t) => a;
        public static float MoveTowards(float a, float b, float d) => a;
        public static int RoundToInt(float f) => 0;
        public static int CeilToInt(float f) => 0;
    }

    public static class Random
    {
        public static float value => 0.5f;
    }

    public static class Time
    {
        public static float deltaTime => 0.016f;
        public static float unscaledDeltaTime => 0.016f;
        public static float time => 0f;
        public static float unscaledTime => 0f;
        public static float timeScale { get; set; }
    }

    public enum ScreenOrientation { AutoRotation, LandscapeLeft }
    public enum SleepTimeout2 { }
    public static class SleepTimeout { public static int NeverSleep => 0; }

    public static class Screen
    {
        public static int width => 1920;
        public static int height => 1080;
        public static float dpi => 320f;
        public static Rect safeArea => default;
        public static ScreenOrientation orientation { get; set; }
        public static bool autorotateToLandscapeLeft { get; set; }
        public static bool autorotateToLandscapeRight { get; set; }
        public static bool autorotateToPortrait { get; set; }
        public static bool autorotateToPortraitUpsideDown { get; set; }
        public static int sleepTimeout { get; set; }
    }

    public static class Application
    {
        public static int targetFrameRate { get; set; }
        public static string version => "0.1.0";
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
    }

    public static class SystemInfo
    {
        public static int systemMemorySize => 8000;
        public static int graphicsMemorySize => 4000;
        public static int processorCount => 8;
    }

    public class Font : Object { }

    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object => default;
    }

    public class Shader : Object
    {
        public static Shader Find(string name) => null;
        public static int PropertyToID(string name) => 0;
    }

    public class Texture : Object { }

    public enum TextureFormat { RGBA32 }
    public enum TextureWrapMode { Clamp, Repeat }

    public class Texture2D : Texture
    {
        public TextureWrapMode wrapMode { get; set; }
        public int anisoLevel { get; set; }
        public Texture2D(int w, int h, TextureFormat f, bool mips) { }
        public void SetPixels32(Color32[] p, int mip) { }
        public void SetPixels32(Color32[] p) { }
        public void Apply() { }
    }

    public class Material : Object
    {
        public Material(Shader s) { }
        public Color color { get; set; }
        public Texture mainTexture { get; set; }
        public void SetTexture(string n, Texture t) { }
        public int renderQueue { get; set; }
        public bool HasProperty(string n) => true;
        public void SetFloat(string n, float v) { }
        public void SetInt(string n, int v) { }
        public void SetColor(string n, Color v) { }
        public void EnableKeyword(string k) { }
        public bool enableInstancing { get; set; }
    }

    public class Mesh : Object
    {
        public Vector3[] vertices { get; set; }
        public Vector3[] normals { get; set; }
        public Vector2[] uv { get; set; }
        public int[] triangles { get; set; }
        public BoneWeight[] boneWeights { get; set; }
        public Matrix4x4[] bindposes { get; set; }
        public void SetVertices(System.Collections.Generic.List<Vector3> v) { }
        public void SetNormals(System.Collections.Generic.List<Vector3> n) { }
        public void SetUVs(int channel, System.Collections.Generic.List<Vector2> uvs) { }
        public void SetTriangles(System.Collections.Generic.List<int> t, int submesh) { }
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
    }

    public class SkinnedMeshRenderer : Renderer
    {
        public Mesh sharedMesh { get; set; }
        public Transform[] bones { get; set; }
        public Transform rootBone { get; set; }
        public Bounds localBounds { get; set; }
    }

    public class MaterialPropertyBlock
    {
        public void SetVectorArray(int id, Vector4[] values) { }
        public void SetColor(int id, Color c) { }
        public void SetFloat(int id, float f) { }
    }

    public static class Graphics
    {
        public static void DrawMeshInstanced(Mesh mesh, int submesh, Material material,
            Matrix4x4[] matrices, int count, MaterialPropertyBlock block) { }
    }

    public class Component : Object
    {
        public Transform transform => null;
        public GameObject gameObject => null;
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
        public T AddComponent<T>() where T : Component => default;
    }

    public class Behaviour : Component { public bool enabled { get; set; } }
    public class MonoBehaviour : Behaviour { }

    public class Transform : Component
    {
        public Matrix4x4 worldToLocalMatrix => default;
        public Matrix4x4 localToWorldMatrix => default;
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 up { get; set; }
        public int childCount => 0;
        public Transform GetChild(int i) => null;
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetAsFirstSibling() { }
        public void LookAt(Vector3 target) { }
        public void Rotate(float x, float y, float z, Space space) { }
    }

    public enum Space { World, Self }
    public enum PrimitiveType { Sphere, Capsule, Cube, Plane, Cylinder, Quad }

    public class GameObject : Object
    {
        public Transform transform => null;
        public string tag { get; set; }
        public GameObject() { }
        public GameObject(string name) { }
        public GameObject(string name, params Type[] components) { }
        public static GameObject CreatePrimitive(PrimitiveType t) => null;
        public T AddComponent<T>() where T : Component => default;
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
        public void SetActive(bool active) { }
    }

    public class Collider : Component { }
    public class Renderer : Component
    {
        public Material sharedMaterial { get; set; }
        public Material material { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public bool receiveShadows { get; set; }
    }
    public class MeshRenderer : Renderer { }
    public class MeshFilter : Component { public Mesh sharedMesh { get; set; } }
    public class LineRenderer : Renderer
    {
        public bool useWorldSpace { get; set; }
        public float widthMultiplier { get; set; }
        public int positionCount { get; set; }
        public void SetPositions(Vector3[] p) { }
    }
    public class TrailRenderer : Renderer
    {
        public bool emitting { get; set; }
        public void Clear() { }
    }

    public enum LightType { Directional, Point, Spot }
    public enum LightShadows { None, Hard, Soft }
    public class Light : Behaviour
    {
        public LightType type { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public LightShadows shadows { get; set; }
    }

    public enum FogMode { Linear, Exponential }
    public static class RenderSettings
    {
        public static Rendering.AmbientMode ambientMode { get; set; }
        public static Color ambientLight { get; set; }
        public static bool fog { get; set; }
        public static Color fogColor { get; set; }
        public static FogMode fogMode { get; set; }
        public static float fogStartDistance { get; set; }
        public static float fogEndDistance { get; set; }
    }

    public enum ShadowQuality { Disable, HardOnly, All }
    public enum ShadowResolution { Low, Medium, High, VeryHigh }
    public static class QualitySettings
    {
        public static int vSyncCount { get; set; }
        public static ShadowQuality shadows { get; set; }
        public static ShadowResolution shadowResolution { get; set; }
    }

    public class Camera : Behaviour
    {
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
    }

    public class AudioListener : Behaviour { }

    public class Animator : Behaviour
    {
        public bool isHuman => true;
        public static int StringToHash(string s) => 0;
        public void SetFloat(int id, float v) { }
        public void SetBool(int id, bool v) { }
        public void SetTrigger(int id) { }
    }

    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public struct Touch
    {
        public Vector2 position => default;
        public TouchPhase phase => TouchPhase.Began;
    }

    public static class Input
    {
        public static int touchCount => 0;
        public static Touch GetTouch(int i) => default;
        public static Vector3 mousePosition => default;
        public static bool GetMouseButton(int b) => false;
        public static bool GetMouseButtonDown(int b) => false;
        public static bool GetMouseButtonUp(int b) => false;
    }

    public enum TextAnchor { UpperCenter, LowerCenter, MiddleCenter }
    public enum RuntimeInitializeLoadType { BeforeSceneLoad, AfterSceneLoad }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RequireComponentAttribute : Attribute { public RequireComponentAttribute(Type t) { } }

    namespace Rendering
    {
        public class RenderPipelineAsset : Object { }
        public static class GraphicsSettings
        {
            public static RenderPipelineAsset currentRenderPipeline => null;
        }
        public enum ShadowCastingMode { Off, On }
        public enum BlendMode { SrcAlpha, OneMinusSrcAlpha, One, Zero }
        public enum RenderQueue { Transparent = 3000 }
        public enum AmbientMode { Skybox, Flat }
    }

    namespace UI
    {
        public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
        public enum HorizontalWrapMode { Wrap, Overflow }
        public enum VerticalWrapMode { Truncate, Overflow }

        public class Graphic : Component { public Color color { get; set; } }
        public class Image : Graphic { }
        public class Text : Graphic
        {
            public string text { get; set; }
            public Font font { get; set; }
            public int fontSize { get; set; }
            public TextAnchor alignment { get; set; }
            public HorizontalWrapMode horizontalOverflow { get; set; }
            public VerticalWrapMode verticalOverflow { get; set; }
        }
        public class CanvasScaler : Component
        {
            public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize }
            public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
            public ScaleMode uiScaleMode { get; set; }
            public Vector2 referenceResolution { get; set; }
            public ScreenMatchMode screenMatchMode { get; set; }
            public float matchWidthOrHeight { get; set; }
        }
        public class GraphicRaycaster : Component { }
    }

    public class Canvas : Component { public UI.RenderMode renderMode { get; set; } }
    public class CanvasGroup : Component
    {
        public float alpha { get; set; }
        public bool interactable { get; set; }
        public bool blocksRaycasts { get; set; }
    }
    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
    }
}
