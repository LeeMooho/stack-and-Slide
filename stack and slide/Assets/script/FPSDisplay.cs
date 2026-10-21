using UnityEngine;

/// <summary>Draws a small diagnostic overlay using smoothed, unscaled frame duration.</summary>
public class FPSDisplay : MonoBehaviour
{
    [Range(1, 100)]
    public int fFontSize;

    private float deltaTime;
    private GUIStyle style;

    /// <summary>Supplies a usable text-size divisor for older scenes that serialized zero.</summary>
    public void Start()
    {
        fFontSize = fFontSize <= 0 ? 50 : fFontSize;
    }

    private void Update()
    {
        // Exponential smoothing reduces frame-to-frame flicker. Unscaled time keeps this
        // diagnostic useful even when gameplay time is slowed or paused with timeScale.
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    private void OnGUI()
    {
        if (style == null)
        {
            // Create the style in a GUI callback and reuse it across Layout/Repaint events
            // instead of allocating a new style every time Unity redraws the overlay.
            style = new GUIStyle();
            style.alignment = TextAnchor.UpperLeft;
            style.normal.textColor = new Color(0f, 0f, 0.5f, 1f);
        }

        style.fontSize = Screen.height * 2 / fFontSize;
        Rect rect = new Rect(0, 0, Screen.width, Screen.height * 2 / 100);
        float milliseconds = deltaTime * 1000f;
        float fps = deltaTime > 0f ? 1f / deltaTime : 0f;
        string text = string.Format("{0:0.0} ms ({1:0.} fps)", milliseconds, fps);
        GUI.Label(rect, text, style);
    }
}
