// =============================================================================
//  COMPILE-CHECK HARNESS - NOT PART OF THE UNITY PROJECT
// =============================================================================
//  Minimal stubs of the Unity API surface that Divergent Genesis uses, so the
//  whole game can be type-checked with `dotnet build` on a machine with no
//  Unity install. Catches typos, bad signatures, missing usings and logic-level
//  type errors long before a Unity CI run would.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IEnumerator = System.Collections.IEnumerator;
using IEnumerable = System.Collections.IEnumerable;

namespace UnityEngine
{
    public enum HideFlags { None = 0, HideAndDontSave = 61 }
    public enum RuntimeInitializeLoadType { AfterSceneLoad = 0, BeforeSceneLoad = 1, BeforeSplashScreen = 2, SubsystemRegistration = 3, AfterAssembliesLoaded = 4 }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum FilterMode { Point = 0, Bilinear = 1, Trilinear = 2 }
    public enum TextureFormat { RGBA32 = 4, RGB24 = 3, ARGB32 = 5 }
    public enum WrapMode { Repeat, Clamp, Mirror }
    public enum SpriteMeshType { FullRect = 0, Tight = 1 }
    public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 }
    public enum ShadowQuality { Disable = 0, HardOnly = 1, All = 2 }
    public enum MotionVectorGenerationMode { Object = 0, Camera = 1, ForceNoMotion = 2 }
    public enum RuntimePlatform { WindowsPlayer = 2, Android = 11, IPhonePlayer = 8, LinuxPlayer = 13 }
    public enum ScreenOrientation { Portrait, PortraitUpsideDown, LandscapeLeft, LandscapeRight, AutoRotation }
    public enum TouchPhase { Began, Moved, Ended, Canceled, Stationary }
    public enum KeyCode { None = 0, Space = 32, Escape = 27, E = 101, F = 102, F1 = 282, LeftShift = 304, RightShift = 303, LeftControl = 306, RightControl = 305, Alpha0 = 320, Alpha1 = 321, Alpha9 = 329 }
    public enum RenderMode { ScreenSpaceOverlay = 0, ScreenSpaceCamera = 1, WorldSpace = 2 }
    public enum LogType { Error = 0, Assert = 1, Warning = 2, Log = 3, Exception = 4 }
    public enum SendMessageOptions { RequireReceiver, DontRequireReceiver }

    public abstract class PropertyAttribute : Attribute { }
    public sealed class SerializeField : Attribute { }
    public sealed class HideInInspector : Attribute { }
    public sealed class HeaderAttribute : PropertyAttribute { public HeaderAttribute(string h) { } }
    public sealed class TooltipAttribute : PropertyAttribute { public TooltipAttribute(string t) { } }
    public enum GraphicsDeviceType { Null = 4, OpenGLES2 = 8, OpenGLES3 = 11, Metal = 16, OpenGLCore = 17, Direct3D11 = 2, Vulkan = 21, PlayStation3 = 13 }
    public enum SystemLanguage { English = 10, Unknown = 42 }
    public static class SystemInfo
    {
        public static string deviceName { get { return "device"; } }
        public static string deviceModel { get { return "model"; } }
        public static string deviceType { get { return "Handheld"; } }
        public static string operatingSystem { get { return "Unknown"; } }
        public static string graphicsDeviceName { get { return "stub"; } }
        public static string graphicsDeviceVendor { get { return "stub"; } }
        public static int graphicsMemorySize { get { return 2048; } }
        public static GraphicsDeviceType graphicsDeviceType { get { return GraphicsDeviceType.Vulkan; } }
        public static int processorCount { get { return 8; } }
        public static int systemMemorySize { get { return 8192; } }
        public static float batteryLevel { get { return 1f; } }
        public static bool supportsInstancing { get { return true; } }
        public static bool supportsComputeShaders { get { return false; } }
        public static int maxTextureSize { get { return 4096; } }
        public static SystemLanguage systemLanguage { get { return SystemLanguage.English; } }
    }

    public sealed class MinAttribute : PropertyAttribute { public MinAttribute(float min) { } public MinAttribute(int min) { } }
    public sealed class SpaceAttribute : PropertyAttribute { public SpaceAttribute() { } public SpaceAttribute(float h) { } }
    public sealed class MultilineAttribute : PropertyAttribute { public MultilineAttribute() { } public MultilineAttribute(int lines) { } }
    public sealed class TextAreaAttribute : PropertyAttribute { public TextAreaAttribute() { } public TextAreaAttribute(int min, int max) { } }
    public sealed class SerializeReference : System.Attribute { }
    public sealed class RangeAttribute : PropertyAttribute { public RangeAttribute(float min, float max) { Min = min; Max = max; } public float Min; public float Max; }
    public sealed class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    public sealed class AddComponentMenu : Attribute { public AddComponentMenu(string m) { } }
    public sealed class DisallowMultipleComponent : Attribute { }
    public sealed class ExecuteInEditMode : Attribute { }
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int o) { } }
    public sealed class ContextMenu : Attribute { public ContextMenu(string n) { } }
    public sealed class ColorUsageAttribute : PropertyAttribute { }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute() { } public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }

    public struct Rect { public float x, y, width, height; public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; } public Vector2 size { get { return new Vector2(width, height); } set { width = value.x; height = value.y; } } public float xMin { get { return x; } } public float yMin { get { return y; } } public float xMax { get { return x + width; } } public float yMax { get { return y + height; } } }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
        public static Vector2 up { get { return new Vector2(0, 1); } }
        public static Vector2 down { get { return new Vector2(0, -1); } }
        public static Vector2 left { get { return new Vector2(-1, 0); } }
        public static Vector2 right { get { return new Vector2(1, 0); } }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y); } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector2(x / m, y / m) : zero; } }
        public float this[int i] { get { return i == 0 ? x : y; } set { if (i == 0) x = value; else y = value; } }
        public static float Distance(Vector2 a, Vector2 b) { return (a - b).magnitude; }
        public static float Dot(Vector2 a, Vector2 b) { return a.x * b.x + a.y * b.y; }
        public static Vector2 ClampMagnitude(Vector2 v, float max) { return v.sqrMagnitude > max * max ? v.normalized * max : v; }
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { return a + (b - a) * Mathf.Clamp01(t); }
        public static Vector2 Min(Vector2 a, Vector2 b) { return new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y)); }
        public static Vector2 Max(Vector2 a, Vector2 b) { return new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)); }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator -(Vector2 a) { return new Vector2(-a.x, -a.y); }
        public static Vector2 operator *(Vector2 a, float s) { return new Vector2(a.x * s, a.y * s); }
        public static Vector2 operator *(float s, Vector2 a) { return a * s; }
        public static Vector2 operator /(Vector2 a, float s) { return new Vector2(a.x / s, a.y / s); }
        public static bool operator ==(Vector2 a, Vector2 b) { return (a - b).sqrMagnitude < 9.9e-11f; }
        public static bool operator !=(Vector2 a, Vector2 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector2 && this == (Vector2)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2); }
        public override string ToString() { return "(" + x + ", " + y + ")"; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0f; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 down { get { return new Vector3(0, -1, 0); } }
        public static Vector3 left { get { return new Vector3(-1, 0, 0); } }
        public static Vector3 right { get { return new Vector3(1, 0, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 back { get { return new Vector3(0, 0, -1); } }
        public static Vector3 positiveInfinity { get { return new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity); } }
        public static Vector3 negativeInfinity { get { return new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity); } }
        public float this[int i]
        {
            get { if (i == 0) return x; if (i == 1) return y; return z; }
            set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; }
        }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y + z * z); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector3(x / m, y / m, z / m) : zero; } }
        public static Vector3 Normalize(Vector3 v) { return v.normalized; }
        public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
        public static Vector3 Scale(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
        public static Vector3 Min(Vector3 a, Vector3 b) { return new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z)); }
        public static Vector3 Max(Vector3 a, Vector3 b) { return new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z)); }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) { return a + (b - a) * t; }
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d)
        {
            Vector3 v = b - a; float m = v.magnitude;
            if (m <= d || m < 1e-6f) return b;
            return a + v / m * d;
        }
        public static Vector3 Project(Vector3 v, Vector3 n) { return n * Vector3.Dot(v, n); }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) { return v - n * Vector3.Dot(v, n); }
        public static Vector3 SmoothDamp(Vector3 a, Vector3 b, ref Vector3 vel, float t) { return Lerp(a, b, 1f - Mathf.Exp(-t)); }
        public void Normalize() { this = normalized; }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float s) { return new Vector3(a.x * s, a.y * s, a.z * s); }
        public static Vector3 operator *(float s, Vector3 a) { return a * s; }
        public static Vector3 operator /(Vector3 a, float s) { return new Vector3(a.x / s, a.y / s, a.z / s); }
        public static bool operator ==(Vector3 a, Vector3 b) { return (a - b).sqrMagnitude < 9.9e-11f; }
        public static bool operator !=(Vector3 a, Vector3 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector3 && this == (Vector3)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2); }
        public override string ToString() { return "(" + x + ", " + y + ", " + z + ")"; }
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Vector4(float x, float y, float z) { this.x = x; this.y = y; this.z = z; w = 0f; }
        public static implicit operator Vector4(Vector3 v) { return new Vector4(v.x, v.y, v.z, 0f); }
        public static implicit operator Vector3(Vector4 v) { return new Vector3(v.x, v.y, v.z); }
        public static Vector4 zero { get { return new Vector4(0, 0, 0, 0); } }
        public static Vector4 one { get { return new Vector4(1, 1, 1, 1); } }
        public float this[int i] { get { if (i == 0) return x; if (i == 1) return y; if (i == 2) return z; return w; } set { if (i == 0) x = value; else if (i == 1) y = value; else if (i == 2) z = value; else w = value; } }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y + z * z + w * w); } }
        public static bool operator ==(Vector4 a, Vector4 b) { return (a.x == b.x) && (a.y == b.y) && (a.z == b.z) && (a.w == b.w); }
        public static bool operator !=(Vector4 a, Vector4 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector4 && this == (Vector4)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode() ^ w.GetHashCode(); }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color black { get { return new Color(0, 0, 0, 1); } }
        public static Color clear { get { return new Color(0, 0, 0, 0); } }
        public static Color gray { get { return new Color(0.5f, 0.5f, 0.5f, 1); } }
        public static Color grey { get { return gray; } }
        public static Color red { get { return new Color(1, 0, 0, 1); } }
        public static Color green { get { return new Color(0, 1, 0, 1); } }
        public static Color blue { get { return new Color(0, 0, 1, 1); } }
        public static Color yellow { get { return new Color(1, 0.92f, 0.016f, 1); } }
        public static Color cyan { get { return new Color(0, 1, 1, 1); } }
        public static Color magenta { get { return new Color(1, 0, 1, 1); } }
        public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
        public static Color operator *(Color a, float s) { return new Color(a.r * s, a.g * s, a.b * s, a.a * s); }
        public static Color operator *(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); }
        public static Color operator +(Color a, Color b) { return new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a); }
        public static Color operator -(Color a, Color b) { return new Color(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a); }
        public static implicit operator Color(Color32 c) { return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); }
        public static implicit operator Color32(Color c)
        {
            return new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                               (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                               (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                               (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255));
        }
        public static bool operator ==(Color a, Color b) { return (Vector4)a == (Vector4)b; }
        public static bool operator !=(Color a, Color b) { return !(a == b); }
        public override bool Equals(object o) { return o is Color && this == (Color)o; }
        public override int GetHashCode() { return ((Vector4)this).GetHashCode(); }
        public static implicit operator Vector4(Color c) { return new Vector4(c.r, c.g, c.b, c.a); }
        public static implicit operator Color(Vector4 v) { return new Color(v.x, v.y, v.z, v.w); }
        public override string ToString() { return "RGBA(" + r + ", " + g + ", " + b + ", " + a + ")"; }
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color32(Color c) { r = (byte)(c.r * 255f); g = (byte)(c.g * 255f); b = (byte)(c.b * 255f); a = (byte)(c.a * 255f); }
        public static Color32 Lerp(Color32 x, Color32 y, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32((byte)(x.r + (y.r - x.r) * t), (byte)(x.g + (y.g - x.g) * t),
                               (byte)(x.b + (y.b - x.b) * t), (byte)(x.a + (y.a - x.a) * t));
        }
        public static implicit operator Vector4(Color32 c) { return new Vector4(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); }
        public static bool operator ==(Color32 a, Color32 b) { return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a; }
        public static bool operator !=(Color32 a, Color32 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Color32 && this == (Color32)o; }
        public override int GetHashCode() { return (r << 24) | (g << 16) | (b << 8) | a; }
        public override string ToString() { return "RGBA(" + r + ", " + g + ", " + b + ", " + a + ")"; }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity { get { return new Quaternion(0, 0, 0, 1); } }
        public static Quaternion Euler(float x, float y, float z) { return identity; }
        public static Quaternion Euler(Vector3 e) { return identity; }
        public static Quaternion LookRotation(Vector3 f) { return identity; }
        public static Quaternion LookRotation(Vector3 f, Vector3 up) { return identity; }
        public static Quaternion AngleAxis(float a, Vector3 axis) { return identity; }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return identity; }
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) { return identity; }
        public static Quaternion Inverse(Quaternion q) { return q; }
        public static float Angle(Quaternion a, Quaternion b) { return 0f; }
        public Vector3 eulerAngles { get { return Vector3.zero; } set { } }
        public static Quaternion operator *(Quaternion a, Quaternion b) { return a; }
        public static Vector3 operator *(Quaternion q, Vector3 v) { return v; }
        public override string ToString() { return "(" + x + ", " + y + ", " + z + ", " + w + ")"; }
    }

    public struct Matrix4x4
    {
        public float m00, m10, m20, m30, m01, m11, m21, m31, m02, m12, m22, m32, m03, m13, m23, m33;
        public static Matrix4x4 identity { get { return new Matrix4x4(); } }
        public static Matrix4x4 zero { get { return new Matrix4x4(); } }
        public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s) { return new Matrix4x4(); }
        public static Matrix4x4 Translate(Vector3 t) { return new Matrix4x4(); }
        public static Matrix4x4 Rotate(Quaternion q) { return new Matrix4x4(); }
        public static Matrix4x4 Scale(Vector3 s) { return new Matrix4x4(); }
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) { return a; }
        public Vector3 MultiplyPoint3x4(Vector3 p) { return p; }
        public Vector3 MultiplyPoint(Vector3 p) { return p; }
        public Vector3 MultiplyVector(Vector3 v) { return v; }
        public Quaternion rotation { get { return Quaternion.identity; } set { } }
        public Vector3 lossyScale { get { return Vector3.one; } }
        public Vector3 GetColumn(int i) { return Vector3.zero; }
        public Vector3 GetPosition() { return Vector3.zero; }
    }

    public struct Bounds
    {
        public Vector3 center, extents, size, min, max;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; extents = size * 0.5f; min = center - extents; max = center + extents; }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public const float Epsilon = 1.401298E-45f;

        public static float Abs(float f) { return Math.Abs(f); }
        public static int Abs(int f) { return Math.Abs(f); }
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
        public static float Sin(float f) { return (float)Math.Sin(f); }
        public static float Cos(float f) { return (float)Math.Cos(f); }
        public static float Tan(float f) { return (float)Math.Tan(f); }
        public static float Asin(float f) { return (float)Math.Asin(f); }
        public static float Acos(float f) { return (float)Math.Acos(f); }
        public static float Atan(float f) { return (float)Math.Atan(f); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Pow(float f, float p) { return (float)Math.Pow(f, p); }
        public static float Exp(float p) { return (float)Math.Exp(p); }
        public static float Log(float f) { return (float)Math.Log(f); }
        public static float Log(float f, float b) { return (float)Math.Log(f, b); }
        public static float Log10(float f) { return (float)Math.Log10(f); }
        public static float Ceil(float f) { return (float)Math.Ceiling(f); }
        public static float Floor(float f) { return (float)Math.Floor(f); }
        public static int CeilToInt(float f) { return (int)Math.Ceiling(f); }
        public static int FloorToInt(float f) { return (int)Math.Floor(f); }
        public static int RoundToInt(float f) { return (int)Math.Round(f, MidpointRounding.AwayFromZero); }
        public static float Round(float f) { return (float)Math.Round(f, MidpointRounding.AwayFromZero); }
        public static float Sign(float f) { return f >= 0f ? 1f : -1f; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Clamp(float v, float min, float max) { return v < min ? min : (v > max ? max : v); }
        public static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }
        public static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float LerpUnclamped(float a, float b, float t) { return a + (b - a) * t; }
        public static float InverseLerp(float a, float b, float v) { return a == b ? 0f : Clamp01((v - a) / (b - a)); }
        public static float MoveTowards(float a, float b, float d) { return Abs(b - a) <= d ? b : a + Sign(b - a) * d; }
        public static float SmoothStep(float from, float to, float t) { t = Clamp01(t); t = -2f * t * t * t + 3f * t * t; return to * t + from * (1f - t); }
        public static float Repeat(float t, float length) { return Clamp(t - Floor(t / length) * length, 0f, length); }
        public static float PingPong(float t, float length) { t = Repeat(t, length * 2f); return length - Abs(t - length); }
        public static float DeltaAngle(float a, float b) { return Repeat(b - a, 360f); }
        public static float LerpAngle(float a, float b, float t) { return a + DeltaAngle(a, b) * Clamp01(t); }
        public static bool Approximately(float a, float b) { return Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), Epsilon * 8f); }
        public static int NextPowerOfTwo(int v) { int p = 1; while (p < v) p <<= 1; return p; }
        public static float PerlinNoise(float x, float y) { return 0f; }
    }

    public static class Random
    {
        private static readonly System.Random R = new System.Random(12345);
        public static float value { get { return (float)R.NextDouble(); } }
        public static float Range(float min, float max) { return min + (float)R.NextDouble() * (max - min); }
        public static int Range(int min, int max) { return R.Next(min, max); }
        public static Vector2 insideUnitCircle { get { return Vector2.zero; } }
        public static Color ColorHSV(float h, float s, float v) { return Color.white; }
        public static void InitState(int seed) { }
    }

    public class Object
    {
        public string name { get; set; }
        public HideFlags hideFlags { get; set; }
        public int GetInstanceID() { return _id; }
        private static int _next = 1;
        internal int _id = _next++;
        public static void Destroy(Object o) { if (o is GameObject) Registry.Remove((GameObject)o); }
        public static void Destroy(Object o, float t) { Destroy(o); }
        public static void DestroyImmediate(Object o) { Destroy(o); }
        public static void DontDestroyOnLoad(Object o) { }
        public static T FindAnyObjectByType<T>() where T : Object { return null; }
        public static T FindFirstObjectByType<T>() where T : Object { return null; }
        public static T FindObjectOfType<T>() where T : Object
        {
            foreach (var go in Registry.AllItems()) { var c = go.GetComponent<T>(); if (c != null) return c; }
            return null;
        }
        public static T[] FindObjectsOfType<T>() where T : Object
        {
            var list = new List<T>();
            foreach (var go in Registry.AllItems()) { var c = go.GetComponent<T>(); if (c != null) list.Add(c); }
            return list.ToArray();
        }
        public static implicit operator bool(Object o) { return !ReferenceEquals(o, null); }
    }

    internal static class Registry
    {
        private static readonly List<GameObject> All = new List<GameObject>();
        public static void Add(GameObject g) { All.Add(g); }
        public static void Remove(GameObject g) { All.Remove(g); }
        public static List<GameObject> AllObjects() { return All; }
        public static IEnumerable<GameObject> AllItems() { return All; }
    }

    public class Component : Object
    {
        internal GameObject _go;
        public GameObject gameObject { get { return _go; } }
        public Transform transform { get { return _go != null ? _go.transform : null; } }
        public string tag { get { return _go != null ? _go.tag : "Untagged"; } }
        public T GetComponent<T>() where T : class { return _go != null ? _go.GetComponent<T>() : null; }
        public T GetComponentInChildren<T>() where T : class { return _go != null ? _go.GetComponentInChildren<T>() : null; }
        public T GetComponentInParent<T>() where T : class { return _go != null ? _go.GetComponent<T>() : null; }
        public T[] GetComponentsInChildren<T>() where T : class { return _go != null ? _go.GetComponentsInChildren<T>() : new T[0]; }
        public T AddComponent<T>() where T : Component { return _go.AddComponent<T>(); }
        public bool CompareTag(string t) { return tag == t; }
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
        public bool isActiveAndEnabled { get { return enabled && _go != null && _go.activeInHierarchy; } }
    }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator e) { return null; }
        public void StopCoroutine(Coroutine c) { }
        public void StopAllCoroutines() { }
        public void CancelInvoke() { }
        public void Invoke(string m, float t) { }
        public void InvokeRepeating(string m, float t) { }
    }

    public class Transform : Component, IEnumerable<Transform>
    {
        private readonly List<Transform> _children = new List<Transform>();
        private Transform _parent;
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 localPosition { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 eulerAngles { get; set; }
        public Vector3 localEulerAngles { get; set; }
        public Transform parent { get { return _parent; } set { SetParent(value); } }
        public int childCount { get { return _children.Count; } }
        public Vector3 forward { get { return rotation * Vector3.forward; } set { } }
        public Vector3 right { get { return rotation * Vector3.right; } set { } }
        public Vector3 up { get { return rotation * Vector3.up; } set { } }
        public Vector3 lossyScale { get { return localScale; } }
        public Vector3 lossyPosition { get { return position; } }
        public Vector3 lossyscale { get { return localScale; } }

        public Transform() { localScale = Vector3.one; }
        public void SetParent(Transform p) { SetParent(p, true); }
        public void SetParent(Transform p, bool worldPositionStays)
        {
            if (_parent != null) _parent._children.Remove(this);
            _parent = p;
            if (p != null) p._children.Add(this);
        }
        public Transform GetChild(int i) { return _children[i]; }
        public Transform Find(string n) { return _children.FirstOrDefault(c => c.gameObject != null && c.gameObject.name == n); }
        public void Translate(Vector3 d) { position += d; }
        public void Translate(Vector3 d, Space s) { position += d; }
        public void Translate(float x, float y, float z) { position += new Vector3(x, y, z); }
        public void Rotate(Vector3 e) { }
        public void LookAt(Vector3 t) { }
        public Vector3 TransformPoint(Vector3 p) { return p; }
        public Vector3 InverseTransformPoint(Vector3 p) { return p; }
        public void DetachChildren() { _children.Clear(); }
        public IEnumerator<Transform> GetEnumerator() { return _children.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return _children.GetEnumerator(); }
    }

    public enum Space { World, Self }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Rect rect { get { return new Rect(0, 0, sizeDelta.x, sizeDelta.y); } }
        public void SetSizeWithCurrentAnchors(int axis, Vector2 v) { sizeDelta = v; }
    }

    public class GameObject : Object
    {
        private readonly List<Component> _components = new List<Component>();
        private Transform _transform;
        public int layer { get; set; }
        private string _tag = "Untagged";
        public string tag { get { return _tag; } set { _tag = value; } }
        public bool activeSelf { get; private set; }
        public Transform transform { get { return _transform; } }
        public SceneManagement.Scene scene { get; private set; }
        internal GameObject() { _transform = new RectTransform(); _transform._go = this; _components.Add(_transform); activeSelf = true; Registry.Add(this); }
        public GameObject(string goName) : this() { name = goName; }
        public GameObject(string goName, params Type[] comps) : this()
        {
            name = goName;
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == typeof(RectTransform)) continue;
                var c = (Component)Activator.CreateInstance(comps[i]);
                Attach(c);
            }
        }
        public bool activeInHierarchy
        {
            get
            {
                var t = _transform;
                while (t != null)
                {
                    if (!t.gameObject.activeSelf) return false;
                    t = t.parent;
                }
                return activeSelf;
            }
        }
        public void SetActive(bool v) { activeSelf = v; }
        private void Attach(Component c)
        {
            c._go = this;
            _components.Add(c);
        }
        public T AddComponent<T>() where T : Component
        {
            var c = (T)Activator.CreateInstance(typeof(T));
            Attach(c);
            return c;
        }
        public Component AddComponent(Type t)
        {
            var c = (Component)Activator.CreateInstance(t);
            Attach(c);
            return c;
        }
        public T GetComponent<T>() where T : class
        {
            for (int i = 0; i < _components.Count; i++) { var c = _components[i] as T; if (c != null) return c; }
            return null;
        }
        public Component GetComponent(Type t) { return GetComponent(t.FullName) as Component; }
        private object GetComponent(string fullName)
        {
            for (int i = 0; i < _components.Count; i++)
                if (_components[i].GetType().FullName == fullName || _components[i].GetType().Name == fullName) return _components[i];
            return null;
        }
        public T[] GetComponents<T>() where T : class
        {
            var list = new List<T>();
            for (int i = 0; i < _components.Count; i++) { var c = _components[i] as T; if (c != null) list.Add(c); }
            return list.ToArray();
        }
        public T GetComponentInChildren<T>() where T : class
        {
            var c = GetComponent<T>(); if (c != null) return c;
            for (int i = 0; i < _transform.childCount; i++) { var r = _transform.GetChild(i).gameObject.GetComponentInChildren<T>(); if (r != null) return r; }
            return null;
        }
        public T[] GetComponentsInChildren<T>() where T : class
        {
            var list = new List<T>(); Collect(this, list); return list.ToArray();
        }
        private static void Collect<T>(GameObject go, List<T> list) where T : class
        {
            var c = go.GetComponent<T>(); if (c != null) list.Add(c);
            for (int i = 0; i < go.transform.childCount; i++) Collect(go.transform.GetChild(i).gameObject, list);
        }
        public static GameObject Find(string n) { return Registry.AllObjects().FirstOrDefault(g => g.name == n); }
        public static GameObject FindWithTag(string t) { return Registry.AllObjects().FirstOrDefault(g => g.tag == t); }
        internal static IEnumerable<GameObject> AllItems() { return Registry.AllItems(); }
    }

    public class ScriptableObject : Object { }

    public class Coroutine { }
    public class YieldInstruction { }
    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } public WaitForSeconds() { } }
    public class WaitForEndOfFrame : YieldInstruction { }
    public class WaitForFixedUpdate : YieldInstruction { }
    public class CustomYieldInstruction : IEnumerator { public virtual bool keepWaiting { get { return false; } } public object Current { get { return null; } } public bool MoveNext() { return false; } public void Reset() { } }

    public class Material : Object
    {
        public Shader shader { get; set; }
        public Color color { get; set; }
        public bool enableInstancing { get; set; }
        public int renderQueue { get; set; }
        public Texture mainTexture { get; set; }
        private readonly Dictionary<string, object> _props = new Dictionary<string, object>();
        public Material(Shader s) { shader = s; color = Color.white; }
        public Material(Material m) { shader = m.shader; }
        public bool HasProperty(string n) { return true; }
        public bool HasProperty(int n) { return true; }
        public void SetColor(string n, Color c) { _props[n] = c; }
        public void SetColor(int n, Color c) { _props["id" + n] = c; }
        public Color GetColor(string n) { return Color.white; }
        public void SetFloat(string n, float f) { _props[n] = f; }
        public float GetFloat(string n) { return 0f; }
        public void SetInt(string n, int i) { _props[n] = i; }
        public void SetVector(string n, Vector4 v) { _props[n] = v; }
        public Vector4 GetVector(string n) { return Vector4.zero; }
        public void SetTexture(string n, Texture t) { _props[n] = t; }
        public void SetPass(int i) { }
        public void CopyPropertiesFromMaterial(Material m) { }
    }

    public class Shader : Object
    {
        public static Shader Find(string n) { return new Shader { name = n }; }
    }

    public class Texture : Object
    {
        public int width { get; set; }
        public int height { get; set; }
        public FilterMode filterMode { get; set; }
        public WrapMode wrapMode { get; set; }
    }

    public class Texture2D : Texture
    {
        public Texture2D(int w, int h) { width = w; height = h; }
        public Texture2D(int w, int h, TextureFormat f, bool mips) { width = w; height = h; }
        public void SetPixels32(Color32[] px) { }
        public void SetPixels(Color[] px) { }
        public Color[] GetPixels() { return new Color[width * height]; }
        public Color GetPixel(int x, int y) { return Color.white; }
        public void SetPixel(int x, int y, Color c) { }
        public void Apply() { }
        public void Apply(bool mips) { }
        public void Resize(int w, int h) { width = w; height = h; }
    }

    public class Sprite : Object
    {
        public Texture2D texture { get; set; }
        public Rect rect { get; set; }
        public Vector2 pivot { get; set; }
        public static Sprite Create(Texture2D t, Rect r, Vector2 p) { return new Sprite { texture = t, rect = r, pivot = p }; }
        public static Sprite Create(Texture2D t, Rect r, Vector2 p, float ppu) { return new Sprite { texture = t, rect = r, pivot = p }; }
        public static Sprite Create(Texture2D t, Rect r, Vector2 p, float ppu, uint ext, SpriteMeshType mt) { return new Sprite { texture = t, rect = r, pivot = p }; }
        public static Sprite Create(Texture2D t, Rect r, Vector2 p, float ppu, uint ext, SpriteMeshType mt, Vector4 border) { return new Sprite { texture = t, rect = r, pivot = p }; }
    }

    public class Font : Object
    {
        public int fontSize { get; set; }
        public static Font CreateDynamicFontFromOSFont(string n, int s) { return new Font { name = n }; }
    }

    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }

    public class Mesh : Object
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Color32> _c = new List<Color32>();
        private readonly List<int> _t = new List<int>();
        public Rendering.IndexFormat indexFormat { get; set; }
        public int subMeshCount { get; set; }
        public Bounds bounds { get; set; }
        public Vector3[] vertices { get { return _v.ToArray(); } set { _v.Clear(); _v.AddRange(value); } }
        public int[] triangles { get { return _t.ToArray(); } set { _t.Clear(); _t.AddRange(value); } }
        public Color32[] colors { get { return _c.ToArray(); } set { _c.Clear(); _c.AddRange(value); } }
        public int vertexCount { get { return _v.Count; } }
        public void Clear() { _v.Clear(); _c.Clear(); _t.Clear(); subMeshCount = 0; }
        public void SetVertices(List<Vector3> v) { _v.Clear(); _v.AddRange(v); }
        public void SetVertices(Vector3[] v) { _v.Clear(); _v.AddRange(v); }
        public void SetNormals(List<Vector3> v) { }
        public void SetNormals(Vector3[] v) { }
        public void SetColors(List<Color32> c) { _c.Clear(); _c.AddRange(c); }
        public void SetColors(Color32[] c) { _c.Clear(); _c.AddRange(c); }
        public void SetColors(List<Color> c) { }
        public void SetUVs(int ch, List<Vector2> u) { }
        public void SetUVs(int ch, Vector2[] u) { }
        public void SetTriangles(List<int> t, int sub, bool calc) { if (sub == 0) { _t.Clear(); _t.AddRange(t); } }
        public void SetTriangles(int[] t, int sub) { if (sub == 0) { _t.Clear(); _t.AddRange(t); } }
        public void SetTriangles(List<int> t, int sub) { if (sub == 0) { _t.Clear(); _t.AddRange(t); } }
        public void RecalculateBounds() { }
        public void RecalculateNormals() { }
        public void RecalculateTangents() { }
        public void MarkDynamic() { }
        public void UploadMeshData(bool m) { }
        public void Optimize() { }
    }

    public class MeshFilter : Component
    {
        public Mesh mesh { get; set; }
        public Mesh sharedMesh { get; set; }
    }

    public class Renderer : Component
    {
        public Material sharedMaterial { get; set; }
        public Material material { get; set; }
        public Material[] sharedMaterials { get; set; }
        public bool enabled { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public bool receiveShadows { get; set; }
        public Rendering.LightProbeUsage lightProbeUsage { get; set; }
        public Rendering.ReflectionProbeUsage reflectionProbeUsage { get; set; }
        public MotionVectorGenerationMode motionVectorGenerationMode { get; set; }
        public bool allowOcclusionWhenDynamic { get; set; }
        public string sortingLayerName { get; set; }
        public int sortingOrder { get; set; }
        public Bounds bounds { get; set; }
    }

    public class MeshRenderer : Renderer { }
    public class SkinnedMeshRenderer : Renderer { }
    public class LineRenderer : Renderer { }
    public class TrailRenderer : Renderer { }
    public class ParticleSystemRenderer : Renderer { }

    public class MultiMesh : Object
    {
        public Mesh mesh { get; set; }
        public int instanceCount { get; set; }
        public void Clear() { instanceCount = 0; }
        public void SetTransforms(Matrix4x4[] m) { }
        public void SetTransforms(List<Matrix4x4> m) { }
        public void SetColors(Color32[] c) { }
        public void SetColors(List<Color32> c) { }
    }

    public class MultiMeshRenderer : Renderer { }

    public class Camera : Behaviour
    {
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public bool allowHDR { get; set; }
        public bool allowMSAA { get; set; }
        public bool allowDynamicResolution { get; set; }
        public Color backgroundColor { get; set; }
        public int cullingMask { get; set; }
        public int depth { get; set; }
        public bool orthographic { get; set; }
        public static Camera main { get { return null; } }
        public Ray ScreenPointToRay(Vector3 p) { return new Ray(); }
        public void Render() { }
    }

    public struct Ray
    {
        public Vector3 origin, direction;
        public Ray(Vector3 o, Vector3 d) { origin = o; direction = d; }
        public Vector3 GetPoint(float d) { return origin + direction * d; }
    }

    public struct RaycastHit
    {
        public Vector3 point, normal;
        public float distance;
        public Collider collider;
    }

    public class Collider : Component { public bool isTrigger { get; set; } public Bounds bounds { get; set; } }
    public class BoxCollider : Collider { public Vector3 center, size; }
    public class SphereCollider : Collider { public Vector3 center; public float radius; }
    public class CapsuleCollider : Collider { public Vector3 center; public float radius, height; }
    public class MeshCollider : Collider { public Mesh sharedMesh { get; set; } public bool convex { get; set; } }
    public class CharacterController : Collider
    {
        public float radius { get; set; } = 0.5f;
        public float height { get; set; } = 2f;
        public Vector3 center { get; set; }
        public bool isGrounded { get; set; }
        public Vector3 velocity { get; set; }
    }
    public struct Rigidbody { public Vector3 velocity; public bool isKinematic; }

    public static class Physics
    {
        public static Vector3 gravity { get; set; }
        public static int defaultSolverIterations { get; set; }
        public static int defaultSolverVelocityIterations { get; set; }
        public static bool autoSyncTransforms { get; set; }
        public static bool reuseCollisionCallbacks { get; set; }
        public static float bounceThreshold { get; set; }
        public static bool Raycast(Vector3 o, Vector3 d, out RaycastHit h, float m) { h = default(RaycastHit); return false; }
        public static bool SphereCast(Vector3 o, float r, Vector3 d, out RaycastHit h, float m) { h = default(RaycastHit); return false; }
    }

    public class Light : Behaviour
    {
        public Color color { get; set; }
        public float intensity { get; set; }
        public LightShadows shadows { get; set; }
        public void Shunck() { }
    }
    public enum LightShadows { None, Hard, Soft }

    public class AudioClip : Object
    {
        public int samples { get; set; }
        public int channels { get; set; }
        public int frequency { get; set; }
        public float length { get; set; }
        public static AudioClip Create(string name, int lenSamples, int channels, int frequency, bool stream) { return new AudioClip { name = name, samples = lenSamples, channels = channels, frequency = frequency, length = (float)lenSamples / frequency }; }
        public bool SetData(float[] data, int offsetSamples) { return true; }
        public float[] GetData() { return new float[0]; }
    }

    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public bool playOnAwake { get; set; }
        public bool loop { get; set; }
        public float volume { get; set; }
        public float pitch { get; set; }
        public float spatialBlend { get; set; }
        public bool mute { get; set; }
        public void Play() { }
        public void Stop() { }
        public void PlayOneShot(AudioClip c) { }
        public void PlayOneShot(AudioClip c, float v) { }
    }

    public class AudioListener : Behaviour { }

    public static class Time
    {
        public static float deltaTime { get { return 0.0166f; } }
        public static float fixedDeltaTime { get; set; }
        public static float time { get { return 0f; } }
        public static float timeScale { get; set; }
        public static int frameCount { get { return 0; } }
        public static float realtimeSinceStartup { get { return 0f; } }
        public static float unscaledDeltaTime { get { return 0.0166f; } }
        public static float maximumDeltaTime { get { return 0.33f; } }
    }

    public struct Touch
    {
        public int fingerId;
        public Vector2 position, deltaPosition, startPosition;
        public TouchPhase phase;
        public static Touch GetTouch(int i) { return default(Touch); }
    }

    public static class Input
    {
        private static readonly HashSet<KeyCode> Down = new HashSet<KeyCode>();
        public static bool GetKey(KeyCode k) { return Down.Contains(k); }
        public static bool GetKeyDown(KeyCode k) { return false; }
        public static bool GetKeyUp(KeyCode k) { return false; }
        public static bool GetMouseButton(int b) { return false; }
        public static bool GetMouseButtonDown(int b) { return false; }
        public static bool GetMouseButtonUp(int b) { return false; }
        public static float GetAxisRaw(string a) { return 0f; }
        public static float GetAxis(string a) { return 0f; }
        public static bool anyKeyDown { get { return false; } }
        public static Vector3 mousePosition { get { return Vector3.zero; } }
        public static int touchCount { get { return 0; } }
        public static Touch GetTouch(int i) { return default(Touch); }
        public static void SimulateKey(KeyCode k, bool down) { if (down) Down.Add(k); else Down.Remove(k); }
    }

    public static class Screen
    {
        public static int width { get { return 1920; } }
        public static int height { get { return 1080; } }
        public static float dpi { get { return 400f; } }
        public static ScreenOrientation orientation { get { return ScreenOrientation.LandscapeLeft; } }
    }

    public static class Application
    {
        public static bool isEditor { get { return true; } }
        public static bool isPlaying { get { return true; } }
        public static bool isMobilePlatform { get { return true; } }
        public static bool isDebugBuild { get { return true; } }
        public static int targetFrameRate { get; set; }
        public static string persistentDataPath { get { return "/tmp/dg"; } }
        public static string dataPath { get { return "/tmp/dg"; } }
        public static RuntimePlatform platform { get { return RuntimePlatform.Android; } }
        public static bool runInBackground { get; set; }
        public static bool isBatchMode { get { return true; } }
        public static string identifier { get { return "com.divergentgenesis.game"; } }
        public static void Quit() { }
        public static void OpenURL(string u) { }
    }

    public static class Debug
    {
        public static bool isDebugBuild { get { return true; } }
        public static void Log(object o) { System.Console.WriteLine(o); }
        public static void LogWarning(object o) { }
        public static void LogError(object o) { System.Console.WriteLine("ERR " + o); }
        public static void LogException(Exception e) { }
        public static void DrawLine(Vector3 a, Vector3 b, Color c) { }
        public static void DrawLine(Vector3 a, Vector3 b, Color c, float d) { }
    }

    public static class JsonUtility
    {
        public static string ToJson(object o) { return "{}"; }
        public static string ToJson(object o, bool pretty) { return "{}"; }
        public static T FromJson<T>(string json) { return default(T); }
        public static void FromJsonOverwrite(string json, object o) { }
    }

    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, object> Store = new Dictionary<string, object>();
        public static int GetInt(string k, int d) { return Store.ContainsKey(k) ? (int)Store[k] : d; }
        public static void SetInt(string k, int v) { Store[k] = v; }
        public static float GetFloat(string k, float d) { return Store.ContainsKey(k) ? (float)Store[k] : d; }
        public static void SetFloat(string k, float v) { Store[k] = v; }
        public static string GetString(string k, string d) { return Store.ContainsKey(k) ? (string)Store[k] : d; }
        public static void SetString(string k, string v) { Store[k] = v; }
        public static bool HasKey(string k) { return Store.ContainsKey(k); }
        public static void DeleteKey(string k) { Store.Remove(k); }
        public static void DeleteAll() { Store.Clear(); }
        public static void Save() { }
    }

    public static class QualitySettings
    {
        public static ShadowQuality shadows { get; set; }
        public static int antiAliasing { get; set; }
        public static int vSyncCount { get; set; }
        public static bool realtimeGI { get; set; }
        public static bool realtimeReflectionProbes { get; set; }
        public static int pixelLightCount { get; set; }
        public static bool billboardsFaceCameraPosition { get; set; }
        public static float shadowDistance { get; set; }
        public static int maximumLODLevel { get; set; }
        public static int anisotropicFiltering { get; set; }
        public static string[] names { get { return new[] { "Low", "High" }; } }
        public static int GetQualityLevel() { return 0; }
        public static void SetQualityLevel(int i, bool b) { }
    }

    public struct LayerMask
    {
        public int value;
        public static int NameToLayer(string n) { return 5; }
        public static string LayerToName(int i) { return "UI"; }
        public static implicit operator int(LayerMask m) { return m.value; }
        public static implicit operator LayerMask(int v) { return new LayerMask { value = v }; }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object { return (T)Activator.CreateInstance(typeof(T)); }
        public static T GetBuiltinResource<T>(string path) where T : Object { var o = (T)Activator.CreateInstance(typeof(T)); o.name = path; return o; }
        public static void UnloadUnusedAssets() { }
    }

    public static class Gizmos
    {
        public static Color color { get; set; }
        public static void DrawWireSphere(Vector3 c, float r) { }
        public static void DrawLine(Vector3 a, Vector3 b) { }
        public static void DrawCube(Vector3 c, Vector3 s) { }
    }

    public static class RenderSettings
    {
        public static Color ambientLight { get; set; }
        public static Color fogColor { get; set; }
        public static bool fog { get; set; }
        public static FogMode fogMode { get; set; }
        public static float fogDensity { get; set; }
        public static float fogStartDistance { get; set; }
        public static float fogEndDistance { get; set; }
    }
    public enum FogMode { Linear, Exponential, ExponentialSquared }

    public static class ShaderPropertyToID
    {
        public static int Get(string n) { return n.GetHashCode(); }
    }
}

namespace UnityEngine.Rendering
{
    public enum IndexFormat { UInt16 = 0, UInt32 = 1 }
    public enum ShadowCastingMode { Off = 0, On = 1, TwoSided = 2, ShadowsOnly = 3 }
    public enum LightProbeUsage { Off = 0, BlendProbes = 1, BlendProbesAndHD = 2, UseCustomProvided = 4 }
    public enum ReflectionProbeUsage { Off = 0, BlendProbes = 1, Simple = 2 }
    public enum CompareFunction { Never = 0, Less = 1, Equal = 2, LessEqual = 3, Greater = 4, NotEqual = 5, GreaterEqual = 6, Always = 7 }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T>(T arg);
    public delegate void UnityAction<T0, T1>(T0 a, T1 b);

    public abstract class UnityEventBase { }
    public class UnityEvent : UnityEventBase
    {
        private readonly List<UnityAction> _list = new List<UnityAction>();
        public void AddListener(UnityAction a) { _list.Add(a); }
        public void RemoveListener(UnityAction a) { _list.Remove(a); }
        public void RemoveAllListeners() { _list.Clear(); }
        public void Invoke() { foreach (var a in _list.ToArray()) a(); }
    }
    public class UnityEvent<T> : UnityEventBase
    {
        private readonly List<UnityAction<T>> _list = new List<UnityAction<T>>();
        public void AddListener(UnityAction<T> a) { _list.Add(a); }
        public void RemoveListener(UnityAction<T> a) { _list.Remove(a); }
        public void RemoveAllListeners() { _list.Clear(); }
        public void Invoke(T arg) { foreach (var a in _list.ToArray()) a(arg); }
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name { get { return "Main"; } }
        public bool IsValid() { return true; }
        public bool isLoaded { get { return true; } }
        public int buildIndex { get { return 0; } }
    }
    public static class SceneManager
    {
        public static Scene GetActiveScene() { return new Scene(); }
        public static int sceneCount { get { return 1; } }
        public static void LoadScene(int i) { }
        public static void LoadScene(string n) { }
    }
}

namespace UnityEngine.EventSystems
{
    public class BaseInputModule : UIBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
    public class InputSystemUIInputModule : BaseInputModule { }

    public abstract class UIBehaviour : MonoBehaviour { }

    public class EventSystem : UIBehaviour
    {
        public static EventSystem current { get; set; }
        public BaseInputModule currentInputModule { get; set; }
        public bool IsPointerOverGameObject() { return false; }
        public bool IsPointerOverGameObject(int id) { return false; }
        public GameObject currentSelectedGameObject { get; set; }
        public void SetSelectedGameObject(GameObject go) { }
    }

    public class AbstractEventData { }
    public class BaseEventData : AbstractEventData { public virtual void Use() { } }

    public class PointerEventData : BaseEventData
    {
        public int pointerId { get; set; }
        public Vector2 position { get; set; }
        public Vector2 delta { get; set; }
        public Vector2 pressPosition { get; set; }
        public Camera pressEventCamera { get; set; }
        public GameObject pointerPress { get; set; }
        public GameObject pointerCurrentRaycast { get; set; }
        public bool IsPointerOverGameObject() { return false; }
    }

    public interface IPointerDownHandler { void OnPointerDown(PointerEventData e); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData e); }
    public interface IPointerClickHandler { void OnPointerClick(PointerEventData e); }
    public interface IBeginDragHandler { void OnBeginDrag(PointerEventData e); }
    public interface IDragHandler { void OnDrag(PointerEventData e); }
    public interface IEndDragHandler { void OnEndDrag(PointerEventData e); }
    public interface IScrollHandler { void OnScroll(PointerEventData e); }
    public interface IDropHandler { void OnDrop(PointerEventData e); }
    public interface IInitializePotentialDragHandler { void OnInitializePotentialDrag(PointerEventData e); }
}

namespace UnityEngine
{
    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 local)
        {
            local = screenPoint;
            return true;
        }
    }
}

namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;

    public class Graphic : UIBehaviour
    {
        public Color color { get; set; } = Color.white;
        public bool raycastTarget { get; set; }
        public Material material { get; set; }
        public RectTransform rectTransform { get { return transform as RectTransform; } }
    }

    public class MaskableGraphic : Graphic { public bool maskable { get; set; } }

    public struct ColorBlock
    {
        public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
        public float colorMultiplier, fadeDuration;
        public SpriteState spriteState;
        public ColorBlock(Color c)
        {
            normalColor = highlightedColor = pressedColor = selectedColor = c;
            disabledColor = new Color(c.r, c.g, c.b, 0.5f);
            colorMultiplier = 1f; fadeDuration = 0.1f; spriteState = default(SpriteState);
        }
    }
    public struct SpriteState { public Sprite highlightedSprite, pressedSprite, selectedSprite, disabledSprite; }

    public class Selectable : UIBehaviour
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }
        public Transition transition { get; set; }
        public ColorBlock colors { get; set; }
        public Graphic targetGraphic { get; set; }
        public bool interactable { get; set; } = true;
        public SpriteState spriteState { get; set; }
        public Navigation navigation { get; set; }
    }
    public struct Navigation { public Mode mode; public Selectable selectOnUp, selectOnDown, selectOnLeft, selectOnRight; public enum Mode { None, Horizontal, Vertical, Automatic, Explicit } }

    public class Button : Selectable
    {
        [Serializable]
        public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { }
        public ButtonClickedEvent onClick { get; } = new ButtonClickedEvent();
        public Image image { get; set; }
    }

    public class Toggle : Selectable
    {
        public class ToggleEvent : UnityEngine.Events.UnityEvent<bool> { }
        public bool isOn { get; set; }
        public ToggleEvent onValueChanged { get; } = new ToggleEvent();
    }

    public class Slider : Selectable
    {
        public class SliderEvent : UnityEngine.Events.UnityEvent<float> { }
        public float value { get; set; }
        public float minValue { get; set; }
        public float maxValue { get; set; }
        public SliderEvent onValueChanged { get; } = new SliderEvent();
    }

    public class Dropdown : Selectable
    {
        public class DropdownEvent : UnityEngine.Events.UnityEvent<int> { }
        public int value { get; set; }
        public DropdownEvent onValueChanged { get; } = new DropdownEvent();
    }

    public class InputField : Selectable
    {
        public class SubmitEvent : UnityEngine.Events.UnityEvent<string> { }
        public class OnChangeEvent : UnityEngine.Events.UnityEvent<string> { }
        public string text { get; set; }
        public Text textComponent { get; set; }
        public SubmitEvent onEndEdit { get; } = new SubmitEvent();
        public OnChangeEvent onValueChanged { get; } = new OnChangeEvent();
    }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public enum OriginHorizontal { Left, Right }
        public enum OriginVertical { Bottom, Top }
        public Sprite sprite { get; set; }
        public Type type { get; set; }
        public FillMethod fillMethod { get; set; }
        public int fillOrigin { get; set; }
        public float fillAmount { get; set; } = 1f;
        public float fillClockwise { get; set; }
        public bool preserveAspect { get; set; }
        public float pixelsPerUnitMultiplier { get; set; } = 1f;
    }

    public class RawImage : MaskableGraphic { public Texture texture { get; set; } public Rect uvRect { get; set; } }

    public class Text : MaskableGraphic
    {
        public string text { get; set; } = "";
        public Font font { get; set; }
        public int fontSize { get; set; } = 14;
        public TextAnchor alignment { get; set; }
        public FontStyle fontStyle { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public bool supportRichText { get; set; } = true;
        public bool resizeTextForBestFit { get; set; }
        public float lineSpacing { get; set; } = 1f;
        public int preferredHeight { get { return fontSize; } }
    }

    public class CanvasGroup : Behaviour
    {
        public float alpha { get; set; } = 1f;
        public bool interactable { get; set; } = true;
        public bool blocksRaycasts { get; set; } = true;
    }

    public class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
        public int overrideSorting { get; set; }
        public float planeDistance { get; set; } = 100f;
        public Camera worldCamera { get; set; }
        public static void ForceUpdateCanvases() { }
    }

    public class CanvasScaler : Behaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public Vector2 referenceResolution { get; set; } = new Vector2(1920, 1080);
        public float matchWidthOrHeight { get; set; } = 0.5f;
        public float scaleFactor { get; set; } = 1f;
        public float referencePixelsPerUnit { get; set; } = 100f;
    }

    public class GraphicRaycaster : Behaviour
    {
        public bool ignoreReversedGraphics { get; set; }
        public int blockingObjects { get; set; }
        public enum BlockingObjects { None, Graphics, All }
    }

    public class RectOffset
    {
        public int left, right, top, bottom;
        public RectOffset() { }
        public RectOffset(int l, int r, int t, int b) { left = l; right = r; top = t; bottom = b; }
        public int horizontal { get { return left + right; } }
        public int vertical { get { return top + bottom; } }
        public Vector2 min { get { return new Vector2(left, bottom); } }
        public Vector2 max { get { return new Vector2(right, top); } }
    }

    public class LayoutElement : Behaviour
    {
        public float minWidth { get; set; }
        public float minHeight { get; set; }
        public float preferredWidth { get; set; }
        public float preferredHeight { get; set; }
        public float flexibleWidth { get; set; }
        public float flexibleHeight { get; set; }
        public bool ignoreLayout { get; set; }
    }

    public class LayoutGroup : Behaviour
    {
        public RectOffset padding { get; set; } = new RectOffset();
        public TextAnchor childAlignment { get; set; }
        public bool childForceExpandWidth { get; set; }
        public bool childForceExpandHeight { get; set; }
        public bool childControlWidth { get; set; }
        public bool childControlHeight { get; set; }
        public bool childScaleWidth { get; set; }
        public bool childScaleHeight { get; set; }
        public bool reverseLayout { get; set; }
        public void SetLayoutHorizontal() { }
        public void SetLayoutVertical() { }
    }

    public class HorizontalOrVerticalLayoutGroup : LayoutGroup { public float spacing { get; set; } }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class GridLayoutGroup : LayoutGroup
    {
        public Vector2 cellSize { get; set; }
        public Vector2 spacing { get; set; }
        public int constraint { get; set; }
        public int constraintCount { get; set; }
        public Corner? startCorner { get; set; }
        public enum Corner { UpperLeft, UpperRight, LowerLeft, LowerRight }
    }
    public class ContentSizeFitter : Behaviour
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode horizontalFit { get; set; }
        public FitMode verticalFit { get; set; }
    }
    public class Mask : UIBehaviour { public bool showMaskGraphic { get; set; } }
    public class RectMask2D : UIBehaviour { }
    public class ScrollRect : UIBehaviour
    {
        public RectTransform content { get; set; }
        public bool horizontal { get; set; }
        public bool vertical { get; set; }
        public Vector2 normalizedPosition { get; set; }
        public float verticalNormalizedPosition { get; set; }
        public float horizontalNormalizedPosition { get; set; }
    }
}

namespace UnityEditor
{
    using UnityEngine;

    public enum BuildTarget { StandaloneWindows64 = 19, StandaloneLinux64 = 24, Android = 13, iOS = 9, WebGL = 20, NoTarget = -2 }
    public enum BuildTargetGroup { Unknown = 0, Standalone = 1, iOS = 4, Android = 7, WebGL = 13, WSA = 14 }
    public enum ScriptingImplementation { Mono2x = 0, IL2CPP = 1, WinRTDotNET = 2 }
    public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, X86 = 4, X86_64 = 8, All = 0xFFFFFFF }
    public enum AndroidSdkVersions { AndroidApiLevelAuto = 0, AndroidApiLevel21 = 21, AndroidApiLevel24 = 24, AndroidApiLevel33 = 33 }
    public enum UIOrientation { Portrait, PortraitUpsideDown, LandscapeRight, LandscapeLeft, AutoRotation }
    public enum ManagedStrippingLevel { Disabled = 0, Low = 1, Medium = 2, High = 3 }
    public enum Il2CppCodeGeneration { OptimizeSpeed = 0, OptimizeSize = 1 }
    public enum BuildResult { Unknown = 0, Succeeded = 1, Failed = 2, Cancelled = 3 }

    public sealed class MenuItem : Attribute { public MenuItem(string path) { } public MenuItem(string path, bool validate) { } }
    public sealed class InitializeOnLoadMethodAttribute : Attribute { }
    public sealed class InitializeOnLoadAttribute : Attribute { }
    public sealed class CustomEditor : Attribute { public CustomEditor(Type t) { } }
    public sealed class HelpURLAttribute : Attribute { public HelpURLAttribute(string u) { } }

    public static class PlayerSettings
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static string bundleVersion { get; set; }
        public static ColorSpace colorSpace { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; }
        public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }
        public static bool useAnimatedAutorotation { get; set; }
        public static bool gcIncremental { get; set; }
        public static bool stripEngineCode { get; set; }
        public static bool MTRendering { get; set; }
        public static bool graphicsJobs { get; set; }
        public static bool androidRenderOutsideSafeArea { get; set; }
        public static void SetApplicationIdentifier(BuildTargetGroup g, string id) { }
        public static void SetScriptingBackend(BuildTargetGroup g, ScriptingImplementation s) { }
        public static void SetManagedStrippingLevel(BuildTargetGroup g, ManagedStrippingLevel l) { }
        public static void SetIl2CppCodeGeneration(BuildTargetGroup g, Il2CppCodeGeneration c) { }
        public static string GetApplicationIdentifier(BuildTargetGroup g) { return ""; }
        public static class Android
        {
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static AndroidSdkVersions minSdkVersion { get; set; }
            public static AndroidSdkVersions targetSdkVersion { get; set; }
            public static bool forceSDCardPermission { get; set; }
            public static bool androidTVCompatibility { get; set; }
            public static bool startInFullscreen { get; set; }
            public static bool renderOutsideSafeArea { get; set; }
            public static int bundleVersionCode { get; set; }
            public static string useCustomKeystore { get; set; }
        }
    }

    public class EditorBuildSettingsScene { public string path; public bool enabled; public EditorBuildSettingsScene(string p, bool e) { path = p; enabled = e; } }

    public static class EditorBuildSettings
    {
        private static EditorBuildSettingsScene[] _scenes = new EditorBuildSettingsScene[0];
        public static EditorBuildSettingsScene[] scenes { get { return _scenes; } set { _scenes = value ?? new EditorBuildSettingsScene[0]; } }
    }

    public struct BuildPlayerOptions
    {
        public string[] scenes;
        public string locationPathName;
        public BuildTarget target;
        public BuildTargetGroup targetGroup;
        public BuildOptions options;
        public string assetBundleManifestPath;
    }
    public enum BuildOptions { None = 0, Development = 1, AutoRunPlayer = 4, CompressWithLz4HC = 256, ConnectWithProfiler = 256 }

    public static class BuildPipeline
    {
        public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions o)
        {
            return new UnityEditor.Build.Reporting.BuildReport();
        }
        public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions o, out UnityEditor.Build.Reporting.BuildSummary s)
        {
            s = new UnityEditor.Build.Reporting.BuildSummary();
            return new UnityEditor.Build.Reporting.BuildReport();
        }
    }

    public static class EditorApplication
    {
        public static bool isPlaying { get { return false; } }
        public static void Exit(int code) { }
    }

    public static class AssetDatabase
    {
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static void ImportAsset(string p) { }
        public static void CreateFolder(string p, string n) { }
        public static string AssetPathToGUID(string p) { return ""; }
    }

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object o) { }
        public static bool DisplayDialog(string t, string m, string ok) { return true; }
    }
}

namespace UnityEditor.Build.Reporting
{
    using UnityEditor;
    using UnityEngine;

    public struct BuildSummary
    {
        public BuildResult result { get { return BuildResult.Succeeded; } }
        public ulong totalSize { get { return 0; } }
        public System.TimeSpan totalTime { get { return System.TimeSpan.Zero; } }
        public int totalErrors { get { return 0; } }
        public int totalWarnings { get { return 0; } }
        public string outputPath { get { return string.Empty; } }
    }

    public class BuildReport : UnityEngine.Object
    {
        public BuildSummary summary { get { return new BuildSummary(); } }
    }
}

namespace UnityEditor.SceneManagement
{
    using UnityEngine.SceneManagement;

    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public enum OpenSceneMode { Single, Additive }

    public static class EditorSceneManager
    {
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) { return new Scene(); }
        public static bool SaveScene(Scene s) { return true; }
        public static bool SaveScene(Scene s, string path) { return true; }
        public static Scene OpenScene(string path, OpenSceneMode mode) { return new Scene(); }
    }
}
