// These tests exercise owned UI state and callbacks with API substitutes. They do
// not demonstrate native UGUI rendering, pixel layout, scrolling or touch dispatch.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

internal static class ExperienceHudTests
{
    public static void Register(Action<string, Action> run)
    {
        run("Experience HUD initialization keeps one canvas and one action listener", InitializeOnce);
        run("HUD rendering changes labels without emitting input events", RenderIsPassive);
        run("Challenge buttons request their verified stage identifiers", ChallengeButtons);
        run("HUD decorations cannot block gameplay input outside explicit controls", InputBoundaries);
        run("Drawer settings report explicit booleans and close through its button", SettingsButtons);
        run("Safe area follows screen rotation without moving outside normalized bounds", SafeAreaChanges);
        run("Reduced motion suppresses confetti and toast translation", ReducedMotion);
        run("Toast queue keeps recent feedback and reuses a bounded particle pool", ToastBudget);
        run("Session reset clears old toast and particle feedback", ClearFeedback);
        run("HUD disposal removes only its canvas and is repeatable", OwnedCleanup);
        run("Unity 6 selects the available modern built-in font name first", ModernFont);
    }

    private static ExperienceHud Create()
    {
        Screen.width = 800;
        Screen.height = 600;
        Screen.safeArea = new Rect(20, 30, 760, 550);
        Time.unscaledDeltaTime = 0.016f;
        Application.unityVersion = "2019.4.16f1";
        Resources.BuiltinLoader = null;
        Resources.BuiltinRequests.Clear();
        var view = new GameObject("Experience view").AddComponent<ExperienceHud>();
        view.Initialize();
        return view;
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual);
    }

    private static GameObject Find(string name)
    {
        return GameObject.All.Single(x => x.name == name);
    }

    private static void Click(string name) { Find(name).GetComponent<Button>().onClick.Invoke(); }

    private static void Tick(ExperienceHud view, float seconds)
    {
        Time.unscaledDeltaTime = seconds;
        typeof(ExperienceHud).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
    }

    private static bool Active(GameObject go)
    {
        for (Transform current = go.transform; current != null; current = current.parent)
            if (!current.gameObject.activeSelf) return false;
        return true;
    }

    private static void InitializeOnce()
    {
        var view = Create();
        int before = GameObject.All.Count;
        Equal(null, Find("Experience HUD Canvas").transform.parent);
        int checks = 0, retries = 0, menus = 0;
        view.CheckRequested += () => checks++;
        view.RetryRequested += () => retries++;
        view.MenuRequested += () => menus++;
        view.Initialize();
        Equal(before, GameObject.All.Count);
        view.Render(new ExperienceHudState { IsPlaying = true });
        Click("Check");
        Click("Retry");
        Click("Menu");
        Equal(1, checks);
        Equal(1, retries);
        Equal(1, menus);
    }

    private static void RenderIsPassive()
    {
        var view = Create();
        int requests = 0;
        view.CheckRequested += () => requests++;
        view.HintsChanged += value => requests++;
        view.ReducedMotionChanged += value => requests++;
        view.HighContrastChanged += value => requests++;
        view.Render(new ExperienceHudState
        {
            IsPlaying = true, Title = "Warm-up", Stats = "Time 0:42 | Checks 2", Record = "Best 0:30 | Stars 2",
            Result = "Missing 1 / Extra 0", Detail = "Check the floor", HintsEnabled = false,
            ReducedMotion = true, HighContrast = true
        });
        Equal("Warm-up", Find("Stage title").GetComponent<Text>().text);
        Equal("Time 0:42 | Checks 2", Find("Timer and checks").GetComponent<Text>().text);
        Equal("Best 0:30 | Stars 2", Find("Personal record").GetComponent<Text>().text);
        Equal("Missing 1 / Extra 0", Find("Projection result").GetComponent<Text>().text);
        Equal("Check the floor", Find("Hint detail").GetComponent<Text>().text);
        Equal(0, requests);
        Equal(true, Active(Find("Playing summary")));
        Equal(false, Active(Find("Choose a challenge")));
        view.Render(new ExperienceHudState());
        Equal(false, Active(Find("Playing summary")));
        Equal(true, Active(Find("Choose a challenge")));
    }

    private static void ChallengeButtons()
    {
        var view = Create();
        var ids = new List<int>();
        view.StageRequested += id => ids.Add(id);
        Click("Warm-up I");
        Click("Flat square");
        Click("Original tower");
        Equal("102,103,101", string.Join(",", ids));
        foreach (string name in new[] { "Warm-up I", "Flat square", "Original tower", "Check", "Retry", "Menu" })
        {
            var rect = (RectTransform)Find(name).transform;
            Equal(true, rect.sizeDelta.x >= 44);
            Equal(true, rect.sizeDelta.y >= 44);
        }
    }

    private static void InputBoundaries()
    {
        Create();
        foreach (GameObject go in GameObject.All)
        {
            Text text = go.GetComponent<Text>();
            if (text != null) Equal(false, text.raycastTarget);
            Image image = go.GetComponent<Image>();
            if (image != null && image.raycastTarget)
            {
                // The scroll viewport is an intentional input region inside the drawer.
                Equal(true, go.GetComponent<Button>() != null || go.name == "Achievement viewport");
            }
        }
        Equal(false, Active(Find("Close drawer backdrop")));
        Click("Achievements and settings");
        Equal(true, Active(Find("Close drawer backdrop")));
        Click("Close drawer");
        Equal(false, Active(Find("Close drawer backdrop")));
    }

    private static void SettingsButtons()
    {
        var view = Create();
        bool? hints = null, motion = null, contrast = null;
        view.HintsChanged += value => hints = value;
        view.ReducedMotionChanged += value => motion = value;
        view.HighContrastChanged += value => contrast = value;
        Click("Achievements and settings");
        Click("Hints setting");
        Click("Reduced motion setting");
        Click("High contrast setting");
        Equal((bool?)false, hints);
        Equal((bool?)true, motion);
        Equal((bool?)true, contrast);
        Equal("Hints: Off", Find("Hints setting label").GetComponent<Text>().text);
        Equal("Reduced motion: On", Find("Reduced motion setting label").GetComponent<Text>().text);
        Equal("High contrast: On", Find("High contrast setting label").GetComponent<Text>().text);
        Equal(true, Find("Check").GetComponent<Outline>().enabled);
        Equal("Missing: yellow | Extra: magenta\nMatched: cyan (see counts above)",
            Find("Target legend").GetComponent<Text>().text);
        Click("Close drawer");
        Equal(false, Active(Find("Achievements and settings drawer")));
    }

    private static void SafeAreaChanges()
    {
        var view = Create();
        var safe = (RectTransform)Find("Safe area").transform;
        Equal(new Vector2(20f / 800, 30f / 600), safe.anchorMin);
        Equal(new Vector2(780f / 800, 580f / 600), safe.anchorMax);
        Screen.width = 1200;
        Screen.height = 600;
        Screen.safeArea = new Rect(100, 20, 1000, 560);
        Tick(view, 0.01f);
        Equal(new Vector2(100f / 1200, 20f / 600), safe.anchorMin);
        Equal(new Vector2(1100f / 1200, 580f / 600), safe.anchorMax);
        Equal(Vector2.zero, safe.offsetMin);
        Equal(Vector2.zero, safe.offsetMax);
    }

    private static void ReducedMotion()
    {
        var view = Create();
        view.Render(new ExperienceHudState { IsPlaying = true, ReducedMotion = true });
        view.ShowToast("Solved", "New record", true);
        var toast = (RectTransform)Find("Feedback toast").transform;
        var before = toast.anchoredPosition;
        Tick(view, 0.5f);
        Equal(before, toast.anchoredPosition);
        Equal(false, GameObject.All.Any(x => x.name.StartsWith("Celebration particle ") && Active(x)));
    }

    private static void ToastBudget()
    {
        var view = Create();
        int objects = GameObject.All.Count;
        for (int i = 0; i <= 12; i++) view.ShowToast(i.ToString(), "Feedback", true);
        Equal("0", Find("Toast title").GetComponent<Text>().text);
        Equal(16, GameObject.All.Count(x => x.name.StartsWith("Celebration particle ")));
        Equal(objects, GameObject.All.Count);
        Tick(view, 3.3f);
        Equal("5", Find("Toast title").GetComponent<Text>().text);
        Equal(objects, GameObject.All.Count);
    }

    private static void ClearFeedback()
    {
        var view = Create();
        view.ShowToast("Old stage", "One", true);
        view.ShowToast("Old stage", "Two", true);
        view.ClearTransientFeedback();
        Tick(view, 5f);
        Equal(false, Active(Find("Feedback toast")));
        Equal(false, GameObject.All.Any(x => x.name.StartsWith("Celebration particle ") && Active(x)));
        view.ShowToast("New stage", "Ready", false);
        Equal("New stage", Find("Toast title").GetComponent<Text>().text);
    }

    private static void OwnedCleanup()
    {
        var view = Create();
        var owned = Find("Experience HUD Canvas");
        var unrelated = new GameObject("Authored game canvas");
        view.SetVisible(false);
        Equal(false, Active(owned));
        view.SetVisible(true);
        Equal(true, Active(owned));
        int before = UObject.Destroyed.Count;
        view.Dispose();
        view.Dispose();
        Equal(before + 1, UObject.Destroyed.Count);
        Equal(true, UObject.Destroyed.Contains(owned));
        Equal(false, UObject.Destroyed.Contains(unrelated));
    }

    private static void ModernFont()
    {
        Create().Dispose();
        Application.unityVersion = "6000.6.6f1";
        Resources.BuiltinRequests.Clear();
        var view = new GameObject().AddComponent<ExperienceHud>();
        view.Initialize();
        Equal("LegacyRuntime.ttf", Resources.BuiltinRequests[0]);
    }
}
