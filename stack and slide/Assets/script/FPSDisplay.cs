using UnityEngine;

public class FPSDisplay : MonoBehaviour
{
    [Range(1, 100)]
    public int fFontSize;

    private float deltaTime;
    private GUIStyle style;

    public void Start()
    {
        fFontSize = fFontSize <= 0 ? 50 : fFontSize;
    }

    private void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    private void OnGUI()
    {
        if (style == null)
        {
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
