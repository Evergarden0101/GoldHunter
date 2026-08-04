// A minimal stand-in for the slice of UnityEngine the Unity layer touches.
//
// Unity is not installed in CI, so without this the ~15 MonoBehaviour files
// would never be compiled at all and a typo or a wrong signature would only
// surface when someone opened the editor. Compiling against this stub catches
// those. It deliberately lives outside Assets/ so Unity never sees it and never
// conflicts with the real UnityEngine.
//
// It asserts nothing about behaviour — it only has to have the right shape.

using System;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => a * s;
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);
    }

    public struct Quaternion
    {
        public static Quaternion identity => default;
        public static Quaternion Euler(float x, float y, float z) => default;
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => default;
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) => default;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }

        public static Color white => new Color(1, 1, 1);
        public static Color grey => new Color(0.5f, 0.5f, 0.5f);
        public static Color red => new Color(1, 0, 0);
        public static Color Lerp(Color a, Color b, float t) => a;
        public static Color operator *(Color c, float s) => new Color(c.r * s, c.g * s, c.b * s, c.a);
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height)
        { this.x = x; this.y = y; this.width = width; this.height = height; }
    }

    public static class Mathf
    {
        public const float Deg2Rad = 0.0174532924f;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Abs(float v) => Math.Abs(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Tan(float v) => (float)Math.Tan(v);
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Floor(float v) => (float)Math.Floor(v);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int RoundToInt(float v) => (int)Math.Round(v);
    }

    public static class Random
    {
        public static float value => 0.5f;
        public static int Range(int min, int max) => min;
        public static float Range(float min, float max) => min;
    }

    public static class Time
    {
        public static float unscaledDeltaTime => 1f / 60f;
        public static float unscaledTime => 0f;
        public static float deltaTime => 1f / 60f;
    }

    public static class Screen
    {
        public static int width => 1920;
        public static int height => 1080;
    }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public enum CameraClearFlags { Skybox, SolidColor, Depth, Nothing }
    public enum LightType { Spot, Directional, Point, Area }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }

    public enum KeyCode
    {
        None = 0, Space = 32, Slash = 47, Period = 46,
        A = 97, D = 100, I = 105, J = 106, K = 107, L = 108, O = 111, P = 112,
        R = 114, S = 115, U = 117, W = 119,
        UpArrow = 273, DownArrow = 274, RightArrow = 275, LeftArrow = 276,
        Keypad0 = 256, Keypad4 = 260, Keypad5 = 261, Keypad6 = 262, Keypad8 = 264,
        KeypadPeriod = 266, LeftShift = 304,
        Joystick1Button0 = 350,
    }

    public static class Input
    {
        public static bool GetKey(KeyCode key) => false;
        public static bool GetKeyDown(KeyCode key) => false;
        public static float GetAxisRaw(string axis) => 0f;
    }

    public class Object
    {
        public string name;
        public static void Destroy(Object target) { }
        public static T FindObjectOfType<T>() where T : Object => null;
        public static Object Instantiate(Object original, Transform parent) => original;
        public static T Instantiate<T>(T original, Transform parent) where T : Object => original;
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => base.GetHashCode();
        public static implicit operator bool(Object exists) => !ReferenceEquals(exists, null);
    }

    public class Transform : Component
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; }
        public Quaternion rotation { get; set; }
        public Transform parent { get; set; }
        public void SetParent(Transform parent, bool worldPositionStays = true) { }
    }

    public class Component : Object
    {
        public Transform transform { get; }
        public GameObject gameObject { get; }
        public T GetComponent<T>() where T : Component => null;
        public T GetComponentInChildren<T>() where T : Component => null;
    }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public Transform transform { get; }
        public string tag { get; set; }
        public T AddComponent<T>() where T : Component => null;
        public T GetComponent<T>() where T : Component => null;
        public static GameObject CreatePrimitive(PrimitiveType type) => new GameObject();
    }

    public class Behaviour : Component { public bool enabled { get; set; } }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { }

    public class Material : Object { public Color color { get; set; } }
    public class Renderer : Component { public Material material { get; } }
    public class Collider : Component { }
    public class AudioListener : Behaviour { }
    public class AudioClip : Object { }

    public class AudioSource : Behaviour
    {
        public float pitch { get; set; }
        public void PlayOneShot(AudioClip clip, float volumeScale) { }
    }

    public class ParticleSystem : Component { public void Emit(int count) { } }

    public class Camera : Behaviour
    {
        public static Camera main => null;
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public bool orthographic { get; set; }
        public float fieldOfView { get; set; }
        public float farClipPlane { get; set; }
        public Vector3 WorldToScreenPoint(Vector3 position) => default;
    }

    public class Light : Behaviour
    {
        public LightType type { get; set; }
        public float intensity { get; set; }
    }

    public static class Gizmos
    {
        public static Color color { get; set; }
        public static void DrawWireSphere(Vector3 center, float radius) { }
        public static void DrawLine(Vector3 from, Vector3 to) { }
        public static void DrawWireCube(Vector3 center, Vector3 size) { }
    }

    public class GUIStyleState { public Color textColor { get; set; } }

    public class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public GUIStyleState normal { get; } = new GUIStyleState();
    }

    public class GUIContent { public static GUIContent none => new GUIContent(); }
    public class GUISkin { public GUIStyle label { get; } = new GUIStyle(); }

    public static class GUI
    {
        public static GUISkin skin => new GUISkin();
        public static Color color { get; set; }
        public static void Label(Rect rect, string text) { }
        public static void Label(Rect rect, string text, GUIStyle style) { }
        public static void Box(Rect rect, GUIContent content) { }
    }

    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : PropertyAttribute
    { public HeaderAttribute(string header) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : PropertyAttribute
    { public TooltipAttribute(string tooltip) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : PropertyAttribute
    { public RangeAttribute(float min, float max) { } }
    public abstract class PropertyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponent : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenu : Attribute
    {
        public string fileName { get; set; }
        public string menuName { get; set; }
        public int order { get; set; }
    }
}
