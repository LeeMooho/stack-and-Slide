// Minimal API substitutes for isolated gameplay tests. These do not simulate Unity physics,
// asset import, rendering, destroyed-object semantics, or lifecycle scheduling.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    // Asset definitions participate in data tests without invoking Unity's importer.
    // This constructs managed fields only; asset serialization is still an editor check.
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() { return new T(); }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class CreateAssetMenuAttribute : Attribute
    {
        public string menuName;
        public string fileName;
    }

    public class Object
    {
        // Record requests so tests can assert ownership and whole-root cleanup.
        // Recording Destroy does not implement Unity's deferred destruction or fake-null.
        public static readonly List<Spawn> Spawns = new List<Spawn>();
        public static readonly List<Object> Destroyed = new List<Object>();
        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            // Clone just the hierarchy and component types used by the fixtures.
            // Production Initialize/Bind methods still wire the resulting controller.
            var clone = prefab.Clone();
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            Spawns.Add(new Spawn { Prefab = prefab, Clone = clone });
            return clone;
        }

        public static void Destroy(Object value) { Destroyed.Add(value); }
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
        public T GetComponentInChildren<T>() where T : Component { return gameObject.GetComponentInChildren<T>(); }
        public bool CompareTag(string tag) { return gameObject.CompareTag(tag); }
    }

    public class MonoBehaviour : Component { public bool enabled = true; }

    public class GameObject : Object
    {
        // Registries and exact-type lookup keep the substitute intentionally small.
        // They are not a replacement for Unity's scene or component serialization.
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
            // Lifecycle callbacks are invoked explicitly by tests, never by this helper.
            var component = new T { gameObject = this };
            components[typeof(T)] = component;
            return component;
        }

        public T GetComponent<T>() where T : Component
        {
            Component component;
            return components.TryGetValue(typeof(T), out component) ? (T)component : null;
        }

        public T GetComponentInChildren<T>() where T : Component
        {
            // Search the same child-controller shape as the game's prefabs. Derived
            // component lookup and all inactive-object rules are outside this substitute.
            T own = GetComponent<T>();
            if (own != null) return own;
            foreach (Transform child in transform.Children)
            {
                if (!child.gameObject.activeSelf) continue;
                T found = child.gameObject.GetComponentInChildren<T>();
                if (found != null) return found;
            }
            return null;
        }

        internal GameObject Clone()
        {
            // Do not copy arbitrary serialized state or schedule callbacks. Tests only
            // depend on tags, component types and parent/child ownership at this boundary.
            var clone = new GameObject(name + "(Clone)") { tag = tag, activeSelf = activeSelf };
            foreach (var entry in components)
            {
                if (entry.Key == typeof(Transform)) continue;
                var component = (Component)Activator.CreateInstance(entry.Key);
                component.gameObject = clone;
                clone.components[entry.Key] = component;
            }
            foreach (Transform child in transform.Children)
                child.gameObject.Clone().transform.parent = clone.transform;
            return clone;
        }

        public void SetActive(bool active) { activeSelf = active; }
        public bool CompareTag(string value) { return tag == value; }
        public static GameObject Find(string value) { return All.Find(x => x.activeSelf && x.name == value); }
        public static GameObject FindWithTag(string value) { return All.Find(x => x.activeSelf && x.tag == value); }
        public static GameObject FindGameObjectWithTag(string value) { return FindWithTag(value); }
    }

    public class Transform : Component
    {
        // Position and up are independent test inputs. This substitute does not derive
        // world transforms from parent matrices or compute quaternion rotations.
        public Vector3 position;
        public Quaternion rotation;
        private Transform parentValue;
        public readonly List<Transform> Children = new List<Transform>();
        public Transform parent
        {
            get { return parentValue; }
            set
            {
                // Maintain traversal links so root and child cleanup can be distinguished.
                if (parentValue != null) parentValue.Children.Remove(this);
                parentValue = value;
                if (value != null) value.Children.Add(this);
            }
        }
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
        // Match Unity's default mask value, but never infer collisions from this mask.
        public const int DefaultRaycastLayers = -5;
        public static readonly Queue<RaycastHit?> Results = new Queue<RaycastHit?>();
        public static readonly Queue<RaycastHit[]> AllResults = new Queue<RaycastHit[]>();
        public static readonly List<Vector3> Directions = new List<Vector3>();
        public static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float distance,
            int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            // Playable colliders are triggers too. Enforce the requested query policy
            // while leaving geometric intersection entirely under each test's control.
            if (queryTriggerInteraction != QueryTriggerInteraction.Collide)
                throw new InvalidOperationException("Projection must include block trigger colliders.");
            Directions.Add(direction);
            // An empty queue is an unexpected query, so it must fail rather than
            // supplying an easy default miss that could conceal a broken call sequence.
            return AllResults.Dequeue();
        }
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance)
        {
            Directions.Add(direction);
            var result = Results.Dequeue();
            hit = result.GetValueOrDefault();
            return result.HasValue;
        }
    }

    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }

    public enum KeyCode { LeftArrow, RightArrow, UpArrow, DownArrow, Z, X, C, A, S }
    public static class Input
    {
        // A test supplies the keys for one control pass and clears them before the next.
        // Key repeat, event timing and the platform input backend are not modeled.
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
    public class MeshRenderer : Component
    {
        // Both properties expose one test material. Real shared/instanced material
        // allocation is an engine concern; these tests only assert color values.
        public Material material = new Material();
        public Material sharedMaterial { get { return material; } }
    }
    public static class Debug
    {
        // Capture the result message without treating a log as proof of a rendered scene.
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
        // Minimal display API surface keeps the real FPS script in the compile check;
        // no GUI drawing or allocation behavior is validated by these fields.
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
