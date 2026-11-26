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
    }

    public class Object { public string name; public static void DestroyImmediate(Object o) { } }
    public class Component : Object
    {
        public Transform transform; public GameObject gameObject;
        public T GetComponent<T>() where T : Component { return null; }
    }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour { }
    public class Transform : Component
    {
        public Vector3 position, localPosition;
        public Vector3 localScale = new Vector3(1f, 1f, 1f);
        public void SetParent(Transform p, bool worldStays) { }
        public Transform Find(string n) { return null; }
    }
    public class GameObject : Object
    {
        public Transform transform = new Transform();
        public string tag;
        public GameObject() { }
        public GameObject(string n) { name = n; }
        public T AddComponent<T>() where T : Component, new() { return new T(); }
        public Component AddComponent(Type t) { return null; }
    }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 zero; }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public static Vector3 zero; }
    public struct Vector4 { public float x, y, z, w; public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } public static Vector4 zero; }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
    }
    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
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
        public void SetPixels32(Color32[] px) { }
        public void Apply(bool mips) { }
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
    }
    public class SpriteRenderer : Component { public Sprite sprite; public int sortingOrder; }
    public class Collider2D : Component { public bool isTrigger; }
    public class BoxCollider2D : Collider2D { public Vector2 size; }
    public class Resources
    {
        public static T Load<T>(string path) where T : Object { return null; }
        public static T[] LoadAll<T>(string path) where T : Object { return null; }
    }
    public static class Debug
    {
        public static void Log(object m) { }
        public static void LogError(object m) { }
    }
    public static class Time { public static float unscaledDeltaTime; }
    public static class JsonUtility
    {
        public static T FromJson<T>(string json) { return default(T); }
    }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }
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
        public TextureWrapMode wrapMode;
        public SpriteImportMode spriteImportMode;
        public Vector4 spriteBorder;
        public SpriteMetaData[] spritesheet;
        public void ReadTextureSettings(TextureImporterSettings s) { }
        public void SetTextureSettings(TextureImporterSettings s) { }
    }
    public class TextureImporterSettings { public SpriteMeshType spriteMeshType; }
    public struct SpriteMetaData { public string name; public int alignment; public Vector2 pivot; public Rect rect; public Vector4 border; }
    public static class AssetDatabase
    {
        public static void StartAssetEditing() { }
        public static void StopAssetEditing() { }
        public static void Refresh() { }
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
