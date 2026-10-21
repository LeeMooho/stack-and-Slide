// Minimal API substitutes for isolated gameplay tests. These do not simulate Unity physics,
// asset import, rendering, destroyed-object semantics, or lifecycle scheduling.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public static readonly List<Spawn> Spawns = new List<Spawn>();
        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var clone = new GameObject(prefab.name + "(Clone)");
            clone.tag = prefab.tag;
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            Spawns.Add(new Spawn { Prefab = prefab, Clone = clone });
            return clone;
        }

        public static void Destroy(Object value) { }
    }

    public class Spawn
    {
        public GameObject Prefab;
        public GameObject Clone;
    }

    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() where T : Component { return gameObject.GetComponent<T>(); }
        public bool CompareTag(string tag) { return gameObject.CompareTag(tag); }
    }

    public class MonoBehaviour : Component { }

    public class GameObject : Object
    {
        public static readonly List<GameObject> All = new List<GameObject>();
        private readonly Dictionary<Type, Component> components = new Dictionary<Type, Component>();
        public string name;
        public string tag = "Untagged";
        public bool activeSelf = true;
        public Transform transform;

        public GameObject(string name = "Object")
        {
            this.name = name;
            transform = new Transform { gameObject = this };
            components[typeof(Transform)] = transform;
            All.Add(this);
        }

        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObject = this };
            components[typeof(T)] = component;
            return component;
        }

        public T GetComponent<T>() where T : Component
        {
            Component component;
            return components.TryGetValue(typeof(T), out component) ? (T)component : null;
        }

        public void SetActive(bool active) { activeSelf = active; }
        public bool CompareTag(string value) { return tag == value; }
        public static GameObject Find(string value) { return All.Find(x => x.activeSelf && x.name == value); }
        public static GameObject FindWithTag(string value) { return All.Find(x => x.activeSelf && x.tag == value); }
        public static GameObject FindGameObjectWithTag(string value) { return FindWithTag(value); }
    }

    public class Transform : Component
    {
        public Vector3 position;
        public Quaternion rotation;
        public Transform parent;
        public Vector3 up = Vector3.up;
        public Vector3 LastRotation;
        public void Translate(Vector3 delta, Space space) { position += delta; }
        public void Rotate(Vector3 delta, Space space) { LastRotation = delta; }
    }

    public class RectTransform : Transform { }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 down { get { return -up; } }
        public static Vector3 right { get { return new Vector3(1, 0, 0); } }
        public static Vector3 left { get { return -right; } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 back { get { return -forward; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return a + -b; }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float b) { return new Vector3(a.x * b, a.y * b, a.z * b); }
    }

    public struct Quaternion { public static Quaternion identity { get { return new Quaternion(); } } }
    public enum Space { World }
    public class Collider : Component { }
    public struct RaycastHit { public Collider collider; public float distance; }

    public static class Physics
    {
        public static readonly Queue<RaycastHit?> Results = new Queue<RaycastHit?>();
        public static readonly List<Vector3> Directions = new List<Vector3>();
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance)
        {
            Directions.Add(direction);
            var result = Results.Dequeue();
            hit = result.GetValueOrDefault();
            return result.HasValue;
        }
    }

    public enum KeyCode { LeftArrow, RightArrow, UpArrow, DownArrow, Z, X, C, A, S }
    public static class Input
    {
        public static readonly HashSet<KeyCode> Keys = new HashSet<KeyCode>();
        public static bool GetKeyDown(KeyCode key) { return Keys.Contains(key); }
        public static bool GetMouseButtonDown(int button) { return false; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color black { get { return new Color(0, 0, 0); } }
    }
    public class Material { public Color color = new Color(1, 1, 1); }
    public class MeshRenderer : Component { public Material material = new Material(); }
    public static class Debug
    {
        public static readonly List<string> Messages = new List<string>();
        public static void Log(object message) { Messages.Add(message.ToString()); }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    public static class Time { public static float unscaledDeltaTime; }
    public static class Screen { public static int width = 1920; public static int height = 1080; }
    public enum TextAnchor { UpperLeft }
    public class GUIStyleState { public Color textColor; }
    public class GUIStyle
    {
        public TextAnchor alignment;
        public int fontSize;
        public GUIStyleState normal = new GUIStyleState();
    }
    public struct Rect { public Rect(float x, float y, float width, float height) { } }
    public static class GUI { public static void Label(Rect rect, string text, GUIStyle style) { } }
}

namespace UnityEngine.UI
{
    public class Text : UnityEngine.Component { public string text; }
}
