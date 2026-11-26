// Minimal UnityEngine + UnityEditor stubs so the Unity-facing scripts can be type-checked in CI
// without a Unity install. NOT shipped into the game -- compiled only by tools/GRZeroDay.CompileCheck.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public static class Mathf
    {
        public static int RoundToInt(float v) => (int)System.Math.Round(v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;

        // Added 2026-11-25 when the merged Game layer landed: the editor compiles these, so the stub must
        // have them or the gate fails on code Unity accepts. Every one is a real UnityEngine.Mathf member.
        public static int CeilToInt(float v) => (int)System.Math.Ceiling(v);
        public static int FloorToInt(float v) => (int)System.Math.Floor(v);
        public static float Ceil(float v) => (float)System.Math.Ceiling(v);
        public static float Floor(float v) => (float)System.Math.Floor(v);
        public static float Round(float v) => (float)System.Math.Round(v);
        public static float Abs(float v) => v < 0f ? -v : v;
        public static int Abs(int v) => v < 0 ? -v : v;
        public static float Sign(float v) => v < 0f ? -1f : 1f;
        public static float Sqrt(float v) => (float)System.Math.Sqrt(v);
        public static float Pow(float a, float b) => (float)System.Math.Pow(a, b);
        public static float Sin(float v) => (float)System.Math.Sin(v);
        public static float Cos(float v) => (float)System.Math.Cos(v);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float Repeat(float t, float length) => t - (float)System.Math.Floor(t / length) * length;
        public static float MoveTowards(float a, float b, float d) => System.Math.Abs(b - a) <= d ? b : a + Sign(b - a) * d;
    }

    public class Object {
        public string name;
        public static void DestroyImmediate(Object o) { }
        public static void Destroy(Object o) { }
        // Unity 6 still has these (obsolete, not removed); GameBoot.cs and DeskSession use them.
        public static T FindFirstObjectByType<T>() where T : Object { return null; }
        public static T FindAnyObjectByType<T>() where T : Object { return null; }
        public static T FindObjectOfType<T>() where T : Object { return null; }   // obsolete in Unity 6
        public static T[] FindObjectsOfType<T>() where T : Object { return new T[0]; }
    }
    public class Component : Object
    {
        public Transform transform; public GameObject gameObject;
        public T GetComponent<T>() where T : Component { return null; }
    }
    public class Behaviour : Component { public bool enabled; }
    public enum FindObjectsSortMode { None, InstanceID }

    public class MonoBehaviour : Behaviour
    {
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Component { return new T[0]; }
        public T GetComponent<T>() where T : Component { return null; }
        public T AddComponent<T>() where T : Component { return null; }
        public static T FindObjectOfType<T>() where T : Component { return null; }
        public static T[] FindObjectsOfType<T>() where T : Component { return new T[0]; }
        public T[] GetComponentsInChildren<T>() where T : Component { return new T[0]; }
        public T[] GetComponentsInParent<T>() where T : Component { return new T[0]; }
        public T GetComponentInChildren<T>() where T : Component { return null; }
        public T GetComponentInChildren<T>(bool includeInactive) where T : Component { return null; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component { return new T[0]; }
        public T GetComponentInParent<T>() where T : Component { return null; }
    }
    public class Transform : Component
    {
        public Vector3 lossyScale;          // Unity 6: Transform.lossyScale
        public Vector3 eulerAngles;
        public Vector3 localEulerAngles;
        public int childCount;
        public void SetAsLastSibling() { }
        public void SetAsFirstSibling() { }
        public void SetSiblingIndex(int i) { }

        public Vector3 position, localPosition;
        public Vector3 localScale = new Vector3(1f, 1f, 1f);
        public Quaternion localRotation;
        public Quaternion rotation;
        public void SetParent(Transform p, bool worldStays) { }
        public Transform Find(string n) { return null; }
    }
    public class GameObject : Object
    {
        public T GetComponent<T>() where T : Component { return null; }
        public bool activeInHierarchy;
        public Transform transform = new Transform();
        public string tag;
        public GameObject() { }
        public GameObject(string n) { name = n; }
        public bool activeSelf;
        public void SetActive(bool v) { activeSelf = v; }
        public T AddComponent<T>() where T : Component, new() { return new T(); }
        public Component AddComponent(Type t) { return null; }
    }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 zero { get { return new Vector2(0f, 0f); } } public static Vector2 one { get { return new Vector2(1f, 1f); } } }
    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion identity { get { return new Quaternion(); } }
        public static Quaternion Euler(float x, float y, float z) { return new Quaternion(); }
    }

    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public static Vector3 zero { get { return new Vector3(0f, 0f, 0f); } } public static Vector3 one { get { return new Vector3(1f, 1f, 1f); } } }
    public struct Vector4 { public float x, y, z, w; public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } public static Vector4 zero; }
    public struct Rect
    {
        public float x, y, width, height;
        // centre, y-up, same convention as the rest of the kit
        public Vector2 center { get { return new Vector2(x + width * 0.5f, y + height * 0.5f); } }
        public float xMin { get { return x; } }
        public float xMax { get { return x + width; } }
        public float yMin { get { return y; } }
        public float yMax { get { return y + height; } }
        public bool Contains(Vector2 p) { return p.x >= x && p.x <= x + width && p.y >= y && p.y <= y + height; }
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
    }
    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1f, 1f, 1f, 1f); } }
        public static Color black { get { return new Color(0f, 0f, 0f, 1f); } }
        public static Color clear { get { return new Color(0f, 0f, 0f, 0f); } }
        public static implicit operator Color(Color32 c) { return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); }
    }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public enum TextureImporterType { Default, Sprite, NormalMap }
    public enum TextureImporterCompression { Uncompressed, Compressed, CompressedHQ, CompressedLQ }
    public enum SpriteImportMode { None, Single, Multiple, Polygon }
    public enum TextureFormat { RGBA32, RGB24, Alpha8 }
    public enum SpriteMeshType { FullRect, Tight }
    public enum SpriteAlignment { Center, TopLeft, TopCenter, TopRight, LeftCenter, RightCenter, BottomLeft, BottomCenter, BottomRight, Custom }
    public enum CameraClearFlags { Skybox, SolidColor, Depth, Nothing }
    public class Texture2D : Object
    {
        public int width, height;
        public FilterMode filterMode;
        public Texture2D() { }
        public Texture2D(int w, int h, TextureFormat f, bool mips) { width = w; height = h; }
        public Color32[] GetPixels32() { return new Color32[width * height]; }
        // Real Unity 6 API. Without Read/Write Enabled the getter is FALSE and GetPixels32 throws, which
        // is exactly the failure the player hit -- so the stub has to expose the flag.
        public bool isReadable;
        public void SetPixels32(Color32[] px) { }
        public void Apply(bool mips) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
        // real Unity signatures: GetPixels(x, y, w, h) is bottom-left based and returns Color[]
        public Color[] GetPixels(int x, int y, int w, int h) { return new Color[w * h]; }
        public void SetPixels(Color[] px) { }
        public bool LoadImage(byte[] data) { return true; }
        public byte[] EncodeToPNG() { return new byte[0]; }
    }
    public class Sprite : Object
    {
        public Rect rect; public Texture2D texture;
        public static Sprite Create(Texture2D tex, Rect r, Vector2 pivot, float ppu)
        { return new Sprite { texture = tex, rect = r }; }
    }
    public class TextAsset : Object { public string text; }
    public class Camera : Behaviour
    {
        public bool orthographic; public float orthographicSize; public CameraClearFlags clearFlags;
        public Color backgroundColor;
        public static Camera main;
        public Vector3 ScreenToWorldPoint(Vector3 screen) { return new Vector3(); }
    }

    // TextMesh is a Component in Unity -- NOT a Renderer. Its sort order lives on the MeshRenderer Unity
    // attaches beside it. Discovered the hard way twice (BUG-042, BUG-048); do not move this to Renderer.
    public class TextMesh : Component
    {
        public string text;
        public int fontSize;
        public Color color;
        public float characterSize;      // Unity 6: TextMesh.characterSize (real API)
        public Font font;                // Unity 6: TextMesh.font
        public TextAlignment alignment;
        public TextAnchor anchor;
        public float offsetZ;
        // NO sortingOrder HERE, ON PURPOSE. TextMesh does not have one in Unity -- it lives on the
        // Renderer -- and inventing it made the editor fail with CS1061 and drop into Safe Mode.
        // A sort order must go through GetComponent<MeshRenderer>() (see below). ASSEMBLY-RULES.md.
    }

    public enum TextAlignment { Left = 0, Center = 1, Right = 2 }
    public enum TextAnchor { UpperLeft = 0, UpperCenter = 1, UpperRight = 2,
                             MiddleLeft = 3, MiddleCenter = 4, MiddleRight = 5,
                             LowerLeft = 6, LowerCenter = 7, LowerRight = 8 }

    /// <summary>Sorting order lives here, and only here -- this is what Unity actually does.</summary>
    public class Renderer : Component
    {
        public int sortingOrder;
        public string sortingLayerName;
        public int sortingLayerID;
        public bool enabled;
    }

    public class MeshRenderer : Renderer { }
    public class SpriteRenderer : Renderer { public Sprite sprite; public Color color; }

    public class Font : Object { }

    public class Collider2D : Component { public bool isTrigger; }
    public class BoxCollider2D : Collider2D { public Vector2 size; }
    public class Resources
    {
        public static T Load<T>(string path) where T : Object { return null; }
        public static T[] LoadAll<T>(string path) where T : Object { return null; }
        public static T GetBuiltinResource<T>(string path) where T : Object { return null; }
    }
    public static class Screen
    {
        public static int width = 480;
        public static int height = 270;
    }

    // UnityEditor build-settings surface. The editor menu (merged layer) registers the Desk scene in
    // Build Settings; these are real UnityEditor types, so they belong in the editor stubs.
    public class EditorBuildSettingsScene
    {
        public string path;
        public bool enabled;
        public EditorBuildSettingsScene(string p, bool on) { path = p; enabled = on; }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[0];
    }

    public static class Debug
    {
        public static void Log(object m) { }
        public static void LogError(object m) { }
    }
    public static class Input
    {
        public static Vector3 mousePosition;
        public static bool GetMouseButtonDown(int button) { return false; }
        public static bool GetMouseButton(int button) { return false; }
        public static int touchCount;
        public static bool GetKeyDown(KeyCode k) { return false; }
    }

    public enum KeyCode { None, Escape, Space, Return }

    public static class Time { public static float unscaledDeltaTime; public static float deltaTime; }
    public static class JsonUtility
    {
        public static T FromJson<T>(string json) { return default(T); }
    }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }
    // Runtime initialisation hook. UnityEngine type, always available -- NOT a package type, so it
    // belongs here. Missing it was a real compile break (CS0246) introduced by the merged GameBoot.cs.
    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }
    }

    public enum RuntimeInitializeLoadType
    {
        AfterSceneLoad = 0, BeforeSceneLoad = 1, AfterAssembliesLoaded = 2,
        BeforeSplashScreen = 3, SubsystemRegistration = 4,
    }

    [AttributeUsage(AttributeTargets.Field)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Field)] public class SerializeFieldAttribute : Attribute { }
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
}

namespace UnityEditor
{
    using UnityEngine;
    public class AssetImporter : Object
    {
        public static AssetImporter GetAtPath(string p) { return null; }
        public void SaveAndReimport() { }
    }
    public class TextureImporter : AssetImporter
    {
        public TextureImporterType textureType;
        public float spritePixelsPerUnit;
        public FilterMode filterMode;
        public TextureImporterCompression textureCompression;
        public bool mipmapEnabled;
        public bool alphaIsTransparency;
        public bool isReadable;
        public TextureWrapMode wrapMode;
        public SpriteImportMode spriteImportMode;
        public Vector4 spriteBorder;
        public void ReadTextureSettings(TextureImporterSettings s) { }
        public void SetTextureSettings(TextureImporterSettings s) { }
    }
    public class TextureImporterSettings { public SpriteMeshType spriteMeshType; }
    public static class AssetDatabase
    {
        public static void StartAssetEditing() { }
        public static void StopAssetEditing() { }
        public static void Refresh() { }
        public static void ImportAsset(string p) { }
        public static T LoadAssetAtPath<T>(string p) where T : UnityEngine.Object { return null; }
    }
    public static class EditorUtility
    {
        public static bool DisplayDialog(string a, string b, string c) { return true; }
    }
    public class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
    }
}

namespace UnityEditor.SceneManagement
{
    using UnityEngine;
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public enum OpenSceneMode { Single, Additive }
    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup s, NewSceneMode m) { return default(UnityEngine.SceneManagement.Scene); }
        public static bool SaveScene(UnityEngine.SceneManagement.Scene s, string path) { return true; }
        public static UnityEngine.SceneManagement.Scene OpenScene(string path, OpenSceneMode m) { return default(UnityEngine.SceneManagement.Scene); }
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene { }
}

