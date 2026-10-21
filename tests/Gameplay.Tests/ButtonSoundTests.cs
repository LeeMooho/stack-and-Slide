// Uses event/audio API substitutes to exercise the runtime sound component, not UnityEditor.
using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Plugins.ButtonSoundsEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

internal static class ButtonSoundTests
{
    public static void Register(Action<string, Action> run)
    {
        run("Button sound tolerates missing audio source and clip", MissingBindings);
        run("Button click plays its assigned clip once", ButtonClick);
        run("Disabling and reenabling sound does not retain duplicate listeners", ButtonLifecycle);
        run("Pointer-click triggers play sound without a Button", EventTriggerClick);
        run("Duplicate pointer-click entries register sound only once", DuplicateTriggerEntries);
        run("Objects with both Button and EventTrigger play sound only once", CombinedInput);
    }

    private static void Invoke(ButtonClickSound sound, string method)
    {
        typeof(ButtonClickSound).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sound, null);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual))
            throw new Exception("Expected " + expected + ", got " + actual);
    }

    private static ButtonClickSound CreateSound(GameObject go)
    {
        var sound = go.AddComponent<ButtonClickSound>();
        sound.AudioSource = new GameObject().AddComponent<AudioSource>();
        sound.ClickSound = new AudioClip();
        return sound;
    }

    private static EventTrigger.Entry AddClickEntry(EventTrigger trigger)
    {
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        trigger.triggers.Add(entry);
        return entry;
    }

    private static void MissingBindings()
    {
        var go = new GameObject();
        var button = go.AddComponent<Button>();
        var sound = go.AddComponent<ButtonClickSound>();
        Invoke(sound, "OnEnable");
        button.onClick.Invoke();
        sound.AudioSource = new GameObject().AddComponent<AudioSource>();
        button.onClick.Invoke();
        Equal(0, sound.AudioSource.PlayCount);
        sound.AudioSource = null;
        sound.ClickSound = new AudioClip();
        button.onClick.Invoke();
    }

    private static void ButtonClick()
    {
        var go = new GameObject();
        var button = go.AddComponent<Button>();
        var sound = CreateSound(go);
        Invoke(sound, "OnEnable");
        button.onClick.Invoke();
        Equal(1, sound.AudioSource.PlayCount);
        Equal(sound.ClickSound, sound.AudioSource.LastClip);
    }

    private static void ButtonLifecycle()
    {
        var go = new GameObject();
        var button = go.AddComponent<Button>();
        var sound = CreateSound(go);
        for (int i = 0; i < 3; i++)
        {
            Invoke(sound, "OnEnable");
            button.onClick.Invoke();
            Equal(i + 1, sound.AudioSource.PlayCount);
            Invoke(sound, "OnDisable");
            button.onClick.Invoke();
            Equal(i + 1, sound.AudioSource.PlayCount);
        }
    }

    private static void EventTriggerClick()
    {
        var go = new GameObject();
        var trigger = go.AddComponent<EventTrigger>();
        var entry = AddClickEntry(trigger);
        var sound = CreateSound(go);
        Invoke(sound, "OnEnable");
        entry.callback.Invoke(new BaseEventData());
        Equal(1, sound.AudioSource.PlayCount);
        Invoke(sound, "OnDisable");
        entry.callback.Invoke(new BaseEventData());
        Equal(1, sound.AudioSource.PlayCount);
        Invoke(sound, "OnEnable");
        entry.callback.Invoke(new BaseEventData());
        Equal(2, sound.AudioSource.PlayCount);
    }

    private static void DuplicateTriggerEntries()
    {
        var go = new GameObject();
        var trigger = go.AddComponent<EventTrigger>();
        trigger.triggers.Add(null);
        var first = AddClickEntry(trigger);
        var second = AddClickEntry(trigger);
        var sound = CreateSound(go);
        Invoke(sound, "OnEnable");
        first.callback.Invoke(new BaseEventData());
        second.callback.Invoke(new BaseEventData());
        Equal(1, sound.AudioSource.PlayCount);
    }

    private static void CombinedInput()
    {
        var go = new GameObject();
        var button = go.AddComponent<Button>();
        var trigger = go.AddComponent<EventTrigger>();
        var entry = AddClickEntry(trigger);
        var sound = CreateSound(go);
        Invoke(sound, "OnEnable");
        button.onClick.Invoke();
        entry.callback.Invoke(new BaseEventData());
        Equal(1, sound.AudioSource.PlayCount);
        Invoke(sound, "OnDisable");
        button.onClick.Invoke();
        entry.callback.Invoke(new BaseEventData());
        Equal(1, sound.AudioSource.PlayCount);
    }
}

namespace UnityEngine
{
    public class AudioClip : Object { }
    public class AudioSource : Component
    {
        public int PlayCount;
        public AudioClip LastClip;
        public void PlayOneShot(AudioClip clip) { PlayCount++; LastClip = clip; }
    }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T>(T value);
    public class UnityEvent
    {
        private readonly List<UnityAction> listeners = new List<UnityAction>();
        public void AddListener(UnityAction listener) { listeners.Add(listener); }
        public void RemoveListener(UnityAction listener) { listeners.RemoveAll(x => x == listener); }
        public void Invoke() { foreach (var listener in listeners.ToArray()) listener(); }
    }
    public class UnityEvent<T>
    {
        private readonly List<UnityAction<T>> listeners = new List<UnityAction<T>>();
        public void AddListener(UnityAction<T> listener) { listeners.Add(listener); }
        public void RemoveListener(UnityAction<T> listener) { listeners.RemoveAll(x => x == listener); }
        public void Invoke(T value) { foreach (var listener in listeners.ToArray()) listener(value); }
    }
}

namespace UnityEngine.UI
{
    public class Button : Component { public UnityEngine.Events.UnityEvent onClick = new UnityEngine.Events.UnityEvent(); }
}

namespace UnityEngine.EventSystems
{
    public class BaseEventData { }
    public enum EventTriggerType { PointerClick }
    public class EventTrigger : Component
    {
        public readonly List<Entry> triggers = new List<Entry>();
        public class Entry
        {
            public EventTriggerType eventID;
            public UnityEngine.Events.UnityEvent<BaseEventData> callback = new UnityEngine.Events.UnityEvent<BaseEventData>();
        }
    }
}
