// API substitutes support compilation and owned UI/event assertions only. They do
// not implement Canvas scaling, text shaping, graphic hit-testing or native layout.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public partial class GameObject
    {
        public GameObject(string name, params Type[] componentTypes) : this(name)
        {
            foreach (Type type in componentTypes)
            {
                Component component = (Component)Activator.CreateInstance(type);
                component.gameObject = this;
                components[type] = component;
                if (component is Transform)
                {
                    transform = (Transform)component;
                    components[typeof(Transform)] = component;
                }
            }
        }
    }

    public partial class Transform
    {
        public int childCount { get { return Children.Count; } }
        public Transform GetChild(int index) { return Children[index]; }
        public void SetParent(Transform value, bool worldPositionStays) { parent = value; }
    }

    public partial class RectTransform
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 offsetMin;
        public Vector2 offsetMax;
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
    }

    public partial class Screen
    {
        public static Rect safeArea = new Rect(0, 0, width, height);
    }

    public static class Application { public static string unityVersion = "2019.4.16f1"; }
    public class Font : Object { }
    public static class Resources
    {
        public static readonly List<string> BuiltinRequests = new List<string>();
        public static Func<string, Object> BuiltinLoader;
        public static T GetBuiltinResource<T>(string name) where T : Object, new()
        {
            BuiltinRequests.Add(name);
            return BuiltinLoader == null ? new T() : (T)BuiltinLoader(name);
        }
    }

    public enum RenderMode { ScreenSpaceOverlay }
    public class Canvas : Component
    {
        public RenderMode renderMode;
        public int sortingOrder;
        public float scaleFactor = 1f;
    }
    public class CanvasGroup : Component
    {
        public float alpha = 1f;
        public bool blocksRaycasts = true;
        public bool interactable = true;
    }
}

namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Component
    {
        public bool raycastTarget = true;
        public UnityEngine.Color color;
    }

    public class Image : Graphic { }
    public class Outline : UnityEngine.Component
    {
        public UnityEngine.Color effectColor;
        public UnityEngine.Vector2 effectDistance;
        public bool enabled;
    }
    public partial class Text
    {
        public UnityEngine.Font font;
        public int fontSize;
        public UnityEngine.TextAnchor alignment;
        public HorizontalWrapMode horizontalOverflow;
        public VerticalWrapMode verticalOverflow;
        public bool supportRichText;
        public float preferredHeight
        {
            get { return (text == null ? 1 : text.Split('\n').Length) * Math.Max(1, fontSize); }
        }
    }
    public enum HorizontalWrapMode { Wrap }
    public enum VerticalWrapMode { Truncate }
    public class CanvasScaler : UnityEngine.Component
    {
        public enum ScaleMode { ScaleWithScreenSize }
        public ScaleMode uiScaleMode;
        public UnityEngine.Vector2 referenceResolution;
        public float matchWidthOrHeight;
    }
    public class GraphicRaycaster : UnityEngine.Component { }
    public class Mask : UnityEngine.Component { public bool showMaskGraphic; }
    public class ScrollRect : UnityEngine.Component
    {
        public enum MovementType { Clamped }
        public UnityEngine.RectTransform viewport;
        public UnityEngine.RectTransform content;
        public bool horizontal;
        public bool vertical;
        public MovementType movementType;
        public float scrollSensitivity;
    }
}
