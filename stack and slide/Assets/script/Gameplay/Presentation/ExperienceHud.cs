using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a self-contained UGUI overlay without changing the authored scene. Native
/// gameplay buttons remain reachable outside this view's explicit interactive areas.
/// Rendering and native event dispatch still require an editor/device smoke test.
/// </summary>
public sealed class ExperienceHud : MonoBehaviour
{
    private const int ParticleCount = 16;
    private const int MaximumPendingToasts = 8;
    private const float ToastDuration = 3.2f;
    private const float ParticleDuration = 1.5f;

    public event Action CheckRequested;
    public event Action RetryRequested;
    public event Action MenuRequested;
    public event Action<int> StageRequested;
    public event Action<bool> HintsChanged;
    public event Action<bool> ReducedMotionChanged;
    public event Action<bool> HighContrastChanged;
    public event Action<bool> DrawerVisibilityChanged;

    private sealed class Toast
    {
        public string Title;
        public string Detail;
        public bool Celebrate;
    }

    private readonly Queue<Toast> pendingToasts = new Queue<Toast>();
    private readonly List<Image> panelImages = new List<Image>();
    private readonly List<Image> buttonImages = new List<Image>();
    private readonly List<Outline> buttonOutlines = new List<Outline>();
    private readonly List<Text> labels = new List<Text>();
    private readonly Image[] particles = new Image[ParticleCount];
    private GameObject ownedCanvas;
    private Canvas canvas;
    private RectTransform safeRoot;
    private RectTransform summary;
    private RectTransform actionBar;
    private RectTransform challengeRow;
    private RectTransform optionsButton;
    private RectTransform drawerPanel;
    private RectTransform achievementViewport;
    private RectTransform achievementContent;
    private Text titleText;
    private Text statsText;
    private Text recordText;
    private Text resultText;
    private Text detailText;
    private Text legendText;
    private Text achievementText;
    private Text hintsText;
    private Text motionText;
    private Text contrastText;
    private GameObject drawer;
    private RectTransform toastPanel;
    private CanvasGroup toastGroup;
    private Text toastTitle;
    private Text toastDetail;
    private Font font;
    private bool initialized;
    private bool hintsEnabled = true;
    private bool reducedMotion;
    private bool highContrast;
    private bool toastActive;
    private bool particlesActive;
    private float toastElapsed;
    private float particleElapsed;
    private int lastWidth = -1;
    private int lastHeight = -1;
    private Rect lastSafeArea;

    /// <summary>Repeated initialization preserves exactly one owned overlay and listener set.</summary>
    public void Initialize()
    {
        if (initialized)
        {
            return;
        }

        font = LoadBuiltinFont();
        ownedCanvas = new GameObject("Experience HUD Canvas", typeof(RectTransform));
        // ScreenSpaceOverlay canvases stay at the scene root for older-engine support.
        // The host explicitly controls visibility and disposes this owned object.
        canvas = ownedCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = ownedCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);
        scaler.matchWidthOrHeight = 0.5f;
        ownedCanvas.AddComponent<GraphicRaycaster>();
        safeRoot = Rect("Safe area", ownedCanvas.transform);
        Stretch(safeRoot);

        BuildSummary();
        BuildActions();
        BuildChallenges();
        Button options = Button("Achievements and settings", safeRoot, "Achievements & settings", OpenDrawer);
        optionsButton = (RectTransform)options.transform;
        // The authored menu button occupies the first 100 pixels at top-left.
        Place(optionsButton, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -112), new Vector2(204, 44));
        BuildToast();
        BuildParticles();
        BuildDrawer();
        initialized = true;
        ApplySafeArea();
        ApplyTheme();
        Render(new ExperienceHudState());
    }

    /// <summary>Displays supplied state without emitting input events or mutating the model.</summary>
    public void Render(ExperienceHudState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException("state");
        }
        Initialize();
        hintsEnabled = state.HintsEnabled;
        bool themeChanged = highContrast != state.HighContrast;
        highContrast = state.HighContrast;
        reducedMotion = state.ReducedMotion;
        summary.gameObject.SetActive(state.IsPlaying);
        actionBar.gameObject.SetActive(state.IsPlaying);
        challengeRow.gameObject.SetActive(!state.IsPlaying);
        SetText(titleText, state.Title);
        SetText(statsText, state.Stats);
        SetText(recordText, state.Record);
        SetText(resultText, state.Result);
        SetText(detailText, state.Detail);
        UpdateSettingsLabels();
        if (themeChanged)
        {
            ApplyTheme();
        }
        if (reducedMotion)
        {
            StopParticles();
            toastPanel.anchoredPosition = new Vector2(0, -12);
        }
    }

    public void SetAchievementText(string text)
    {
        Initialize();
        SetText(achievementText, text);
        ResizeAchievementContent();
    }

    /// <summary>Hides this entire owned overlay when the host is disabled.</summary>
    public void SetVisible(bool visible)
    {
        Initialize();
        if (!visible) CloseDrawer();
        ownedCanvas.SetActive(visible);
    }

    /// <summary>Ends visual feedback belonging to an obsolete gameplay session.</summary>
    public void ClearTransientFeedback()
    {
        pendingToasts.Clear();
        toastActive = false;
        StopParticles();
        if (toastPanel != null) toastPanel.gameObject.SetActive(false);
    }

    /// <summary>Releases only the view's canvas; safe after repeated disable/destroy calls.</summary>
    public void Dispose()
    {
        if (initialized) CloseDrawer();
        ClearTransientFeedback();
        if (ownedCanvas != null)
        {
            ownedCanvas.SetActive(false);
            Destroy(ownedCanvas);
            ownedCanvas = null;
        }
        initialized = false;
        panelImages.Clear();
        buttonImages.Clear();
        buttonOutlines.Clear();
        labels.Clear();
    }

    /// <summary>Queues short feedback; decorative effects use a fixed pool and unscaled time.</summary>
    public void ShowToast(string title, string detail, bool celebrate)
    {
        Initialize();
        if (pendingToasts.Count >= MaximumPendingToasts)
        {
            pendingToasts.Dequeue();
        }
        pendingToasts.Enqueue(new Toast { Title = title ?? "", Detail = detail ?? "", Celebrate = celebrate });
        if (!toastActive)
        {
            StartNextToast();
        }
    }

    private void BuildSummary()
    {
        summary = Panel("Playing summary", safeRoot);
        Place(summary, Vector2.zero, Vector2.zero, new Vector2(12, 64), new Vector2(320, 152));
        titleText = Label("Stage title", summary, 18);
        statsText = Label("Timer and checks", summary, 14);
        recordText = Label("Personal record", summary, 14);
        resultText = Label("Projection result", summary, 15);
        detailText = Label("Hint detail", summary, 14);
        legendText = Label("Target legend", summary, 14);
        TopLine(titleText, 8, 22);
        TopLine(statsText, 32, 18);
        TopLine(recordText, 51, 18);
        TopLine(resultText, 70, 18);
        TopLine(detailText, 89, 18);
        TopLine(legendText, 109, 36);
    }

    private void BuildActions()
    {
        actionBar = Rect("Playing actions", safeRoot);
        Place(actionBar, Vector2.zero, Vector2.zero, new Vector2(12, 12), new Vector2(320, 44));
        Action[] callbacks =
        {
            () => { if (CheckRequested != null) CheckRequested(); },
            () => { if (RetryRequested != null) RetryRequested(); },
            () => { if (MenuRequested != null) MenuRequested(); }
        };
        string[] names = { "Check", "Retry", "Menu" };
        for (int i = 0; i < names.Length; i++)
        {
            Button button = Button(names[i], actionBar, names[i], callbacks[i]);
            Place((RectTransform)button.transform, Vector2.zero, Vector2.zero,
                new Vector2(i * 108, 0), new Vector2(104, 44));
        }
    }

    private void BuildChallenges()
    {
        challengeRow = Rect("Choose a challenge", safeRoot);
        Place(challengeRow, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(620, 44));
        int[] ids = { 102, 103, 101 };
        string[] names = { "Warm-up I", "Flat square", "Original tower" };
        for (int i = 0; i < ids.Length; i++)
        {
            int id = ids[i];
            Button button = Button(names[i], challengeRow, names[i], () =>
            {
                if (StageRequested != null) StageRequested(id);
            });
            Place((RectTransform)button.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(190, 44));
        }
    }

    private void BuildDrawer()
    {
        drawer = Rect("Achievements and settings drawer", safeRoot).gameObject;
        Stretch((RectTransform)drawer.transform);
        Button backdrop = Button("Close drawer backdrop", drawer.transform, "", CloseDrawer);
        Stretch((RectTransform)backdrop.transform);
        backdrop.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
        // This is the only fullscreen hit target, and exists only while the drawer is open.
        buttonImages.Remove(backdrop.GetComponent<Image>());
        buttonOutlines.Remove(backdrop.GetComponent<Outline>());
        drawerPanel = Panel("Drawer content", drawer.transform);
        Place(drawerPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 448));
        Text heading = Label("Drawer heading", drawerPanel, 20);
        heading.text = "Progress & settings";
        TopLine(heading, 14, 28);
        Button close = Button("Close drawer", drawerPanel, "X", CloseDrawer);
        Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -8), new Vector2(44, 44));
        Text achievementHeading = Label("Achievements heading", drawerPanel, 16);
        achievementHeading.text = "Achievements";
        TopLine(achievementHeading, 54, 22);

        RectTransform scrollRoot = Rect("Achievement scroll", drawerPanel);
        Stretch(scrollRoot);
        scrollRoot.offsetMin = new Vector2(16, 172);
        scrollRoot.offsetMax = new Vector2(-16, -80);
        achievementViewport = Rect("Achievement viewport", scrollRoot);
        Stretch(achievementViewport);
        Image viewportImage = achievementViewport.gameObject.AddComponent<Image>();
        // The stencil-writing graphic stays opaque; showMaskGraphic hides its pixels.
        // Alpha zero could discard its fragments and hide the entire achievement list.
        viewportImage.color = new Color(1, 1, 1, 1);
        viewportImage.raycastTarget = true;
        achievementViewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        achievementContent = Rect("Achievement content", achievementViewport);
        achievementContent.anchorMin = new Vector2(0, 1);
        achievementContent.anchorMax = new Vector2(1, 1);
        achievementContent.pivot = new Vector2(0.5f, 1);
        achievementText = Label("Achievement list", achievementContent, 15);
        Stretch((RectTransform)achievementText.transform);
        achievementText.text = "Complete challenges to unlock achievements.";
        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = achievementViewport;
        scroll.content = achievementContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        Button hints = Button("Hints setting", drawerPanel, "", ToggleHints);
        Button motion = Button("Reduced motion setting", drawerPanel, "", ToggleMotion);
        Button contrast = Button("High contrast setting", drawerPanel, "", ToggleContrast);
        hintsText = hints.GetComponentInChildren<Text>();
        motionText = motion.GetComponentInChildren<Text>();
        contrastText = contrast.GetComponentInChildren<Text>();
        BottomSetting((RectTransform)hints.transform, 112);
        BottomSetting((RectTransform)motion.transform, 64);
        BottomSetting((RectTransform)contrast.transform, 16);
        drawer.SetActive(false);
    }

    private void BuildToast()
    {
        toastPanel = Panel("Feedback toast", safeRoot);
        Place(toastPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(340, 84));
        toastGroup = toastPanel.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        toastGroup.interactable = false;
        toastTitle = Label("Toast title", toastPanel, 18);
        toastDetail = Label("Toast detail", toastPanel, 14);
        TopLine(toastTitle, 12, 26);
        TopLine(toastDetail, 42, 32);
        toastPanel.gameObject.SetActive(false);
    }

    private void BuildParticles()
    {
        for (int i = 0; i < ParticleCount; i++)
        {
            RectTransform particle = Rect("Celebration particle " + i, safeRoot);
            Place(particle, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -40),
                new Vector2(5 + i % 3 * 2, 5));
            Image image = particle.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = i % 2 == 0 ? new Color(1f, 0.78f, 0.25f) : new Color(0.2f, 0.85f, 0.85f);
            particles[i] = image;
            particle.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }
        if (lastWidth != Screen.width || lastHeight != Screen.height || !lastSafeArea.Equals(Screen.safeArea))
        {
            ApplySafeArea();
        }
        float delta = Time.unscaledDeltaTime;
        if (delta < 0f || float.IsNaN(delta) || float.IsInfinity(delta))
        {
            return;
        }
        if (toastActive)
        {
            toastElapsed += delta;
            float arrival = Math.Min(1f, toastElapsed / 0.25f);
            toastPanel.anchoredPosition = new Vector2(0, -12f + (reducedMotion ? 0f : (1f - arrival) * 12f));
            toastGroup.alpha = Math.Min(1f, Math.Max(0f, (ToastDuration - toastElapsed) / 0.4f));
            if (toastElapsed >= ToastDuration)
            {
                toastActive = false;
                toastPanel.gameObject.SetActive(false);
                StartNextToast();
            }
        }
        if (particlesActive)
        {
            particleElapsed += delta;
            if (reducedMotion || particleElapsed >= ParticleDuration)
            {
                StopParticles();
            }
            else
            {
                for (int i = 0; i < particles.Length; i++)
                {
                    float horizontal = (i - 7.5f) * 12f * particleElapsed;
                    float vertical = -40f + (35f + i % 4 * 12f) * particleElapsed - 100f * particleElapsed * particleElapsed;
                    ((RectTransform)particles[i].transform).anchoredPosition = new Vector2(horizontal, vertical);
                }
            }
        }
    }

    private void StartNextToast()
    {
        StopParticles();
        if (pendingToasts.Count == 0)
        {
            return;
        }
        Toast toast = pendingToasts.Dequeue();
        SetText(toastTitle, toast.Title);
        SetText(toastDetail, toast.Detail);
        toastElapsed = 0f;
        toastActive = true;
        toastGroup.alpha = 1f;
        toastPanel.anchoredPosition = new Vector2(0, reducedMotion ? -12f : 0f);
        toastPanel.gameObject.SetActive(true);
        if (toast.Celebrate && !reducedMotion)
        {
            particlesActive = true;
            particleElapsed = 0f;
            foreach (Image particle in particles)
            {
                ((RectTransform)particle.transform).anchoredPosition = new Vector2(0, -40);
                particle.gameObject.SetActive(true);
            }
        }
    }

    private void StopParticles()
    {
        particlesActive = false;
        foreach (Image particle in particles)
        {
            if (particle != null) particle.gameObject.SetActive(false);
        }
    }

    private void ApplySafeArea()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        lastSafeArea = Screen.safeArea;
        if (lastWidth <= 0 || lastHeight <= 0)
        {
            return;
        }
        safeRoot.anchorMin = new Vector2(Clamp01(lastSafeArea.x / lastWidth), Clamp01(lastSafeArea.y / lastHeight));
        safeRoot.anchorMax = new Vector2(Clamp01(lastSafeArea.xMax / lastWidth), Clamp01(lastSafeArea.yMax / lastHeight));
        safeRoot.offsetMin = Vector2.zero;
        safeRoot.offsetMax = Vector2.zero;
        float scale = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
        float safeWidth = lastSafeArea.width / scale;
        float safeHeight = lastSafeArea.height / scale;
        float summaryWidth = Math.Min(320f, Math.Max(160f, safeWidth - 24f));
        bool narrowSummary = summaryWidth < 280f;
        summary.sizeDelta = new Vector2(summaryWidth, narrowSummary ? 250 : 152);
        actionBar.sizeDelta = new Vector2(summaryWidth, 44);
        float actionWidth = (summaryWidth - 8f) / 3f;
        for (int i = 0; i < actionBar.childCount; i++)
        {
            RectTransform child = (RectTransform)actionBar.GetChild(i);
            child.anchoredPosition = new Vector2(i * (actionWidth + 4f), 0);
            child.sizeDelta = new Vector2(actionWidth, 44);
        }
        // Narrow displays gain extra line height rather than shrinking already-small text.
        Text[] summaryLines = { titleText, statsText, recordText, resultText, detailText, legendText };
        for (int i = 0; i < summaryLines.Length; i++)
        {
            float top = i == 0 ? 8 : narrowSummary ? 32 + (i - 1) * 36 : 32 + (i - 1) * 19;
            float height = i == 0 ? 22 : i == 5 ? narrowSummary ? 64 : 36 : narrowSummary ? 34 : 18;
            TopLine(summaryLines[i], top, height);
        }
        optionsButton.sizeDelta = new Vector2(Math.Min(204f, Math.Max(44f, safeWidth - 24f)), 44);
        toastPanel.sizeDelta = new Vector2(Math.Min(340f, Math.Max(160f, safeWidth - 24f)), 84);
        float rowWidth = Math.Min(620f, Math.Max(160f, safeWidth - 24f));
        challengeRow.sizeDelta = new Vector2(rowWidth, 44);
        float buttonWidth = (rowWidth - 16f) / 3f;
        for (int i = 0; i < challengeRow.childCount; i++)
        {
            RectTransform child = (RectTransform)challengeRow.GetChild(i);
            child.anchoredPosition = new Vector2(i * (buttonWidth + 8f), 0);
            child.sizeDelta = new Vector2(buttonWidth, 44);
        }
        drawerPanel.sizeDelta = new Vector2(Math.Min(460f, Math.Max(180f, safeWidth - 24f)),
            Math.Min(448f, Math.Max(300f, safeHeight - 24f)));
        ResizeAchievementContent();
    }

    private void ResizeAchievementContent()
    {
        if (achievementText == null || drawerPanel == null)
        {
            return;
        }
        float availableHeight = Math.Max(0f, drawerPanel.sizeDelta.y - 252f);
        achievementContent.sizeDelta = new Vector2(0, Math.Max(availableHeight, achievementText.preferredHeight));
    }

    private void OpenDrawer()
    {
        drawer.SetActive(true);
        ResizeAchievementContent();
        if (DrawerVisibilityChanged != null) DrawerVisibilityChanged(true);
    }

    private void CloseDrawer()
    {
        drawer.SetActive(false);
        if (DrawerVisibilityChanged != null) DrawerVisibilityChanged(false);
    }

    private void ToggleHints()
    {
        hintsEnabled = !hintsEnabled;
        UpdateSettingsLabels();
        if (HintsChanged != null) HintsChanged(hintsEnabled);
    }

    private void ToggleMotion()
    {
        reducedMotion = !reducedMotion;
        if (reducedMotion)
        {
            StopParticles();
            toastPanel.anchoredPosition = new Vector2(0, -12);
        }
        UpdateSettingsLabels();
        if (ReducedMotionChanged != null) ReducedMotionChanged(reducedMotion);
    }

    private void ToggleContrast()
    {
        highContrast = !highContrast;
        UpdateSettingsLabels();
        ApplyTheme();
        if (HighContrastChanged != null) HighContrastChanged(highContrast);
    }

    private void UpdateSettingsLabels()
    {
        SetText(hintsText, "Hints: " + (hintsEnabled ? "On" : "Off"));
        SetText(motionText, "Reduced motion: " + (reducedMotion ? "On" : "Off"));
        SetText(contrastText, "High contrast: " + (highContrast ? "On" : "Off"));
    }

    private void ApplyTheme()
    {
        Color panel = highContrast ? new Color(0, 0, 0, 1) : new Color(0.055f, 0.09f, 0.16f, 0.96f);
        Color button = highContrast ? new Color(0.12f, 0.12f, 0.12f, 1) : new Color(0.12f, 0.25f, 0.40f, 1);
        foreach (Image image in panelImages) image.color = panel;
        foreach (Image image in buttonImages) image.color = button;
        foreach (Outline outline in buttonOutlines) outline.enabled = highContrast;
        foreach (Text text in labels) text.color = new Color(1, 1, 1, 1);
        SetText(legendText, highContrast
            ? "Missing: yellow | Extra: magenta\nMatched: cyan (see counts above)"
            : "Missing: amber | Extra: pink\nMatched: teal (see counts above)");
    }

    private RectTransform Panel(string name, Transform parent)
    {
        RectTransform rect = Rect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        panelImages.Add(image);
        return rect;
    }

    private Button Button(string name, Transform parent, string text, Action clicked)
    {
        RectTransform rect = Rect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = true;
        buttonImages.Add(image);
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1, 1, 1, 1);
        outline.effectDistance = new Vector2(1, -1);
        outline.enabled = false;
        buttonOutlines.Add(outline);
        Button button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(() => clicked());
        Text label = Label(name + " label", rect, 14);
        label.alignment = TextAnchor.MiddleCenter;
        label.text = text;
        Stretch((RectTransform)label.transform);
        ((RectTransform)label.transform).offsetMin = new Vector2(6, 2);
        ((RectTransform)label.transform).offsetMax = new Vector2(-6, -2);
        return button;
    }

    private Text Label(string name, Transform parent, int fontSize)
    {
        RectTransform rect = Rect(name, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.supportRichText = false;
        text.raycastTarget = false;
        labels.Add(text);
        return text;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void TopLine(Text text, float top, float height)
    {
        RectTransform rect = (RectTransform)text.transform;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -top);
        rect.sizeDelta = new Vector2(-20, height);
    }

    private static void BottomSetting(RectTransform rect, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, bottom);
        rect.sizeDelta = new Vector2(-32, 44);
    }

    private static void SetText(Text text, string value)
    {
        value = value ?? "";
        if (text.text != value) text.text = value;
    }

    private static float Clamp01(float value) { return Math.Min(1f, Math.Max(0f, value)); }

    private static Font LoadBuiltinFont()
    {
        // Unity 6 replaced Arial.ttf with LegacyRuntime.ttf. Choose the native name
        // first to avoid logging a missing-resource error on a supported engine.
        bool modern = Application.unityVersion.StartsWith("6000.") || Application.unityVersion.StartsWith("7000.");
        string first = modern ? "LegacyRuntime.ttf" : "Arial.ttf";
        string second = modern ? "Arial.ttf" : "LegacyRuntime.ttf";
        Font result = TryFont(first) ?? TryFont(second);
        if (result == null) throw new InvalidOperationException("No supported built-in font is available for the experience HUD.");
        return result;
    }

    private static Font TryFont(string name)
    {
        try { return Resources.GetBuiltinResource<Font>(name); }
        catch (ArgumentException) { return null; }
    }

    private void OnDestroy()
    {
        Dispose();
    }
}
