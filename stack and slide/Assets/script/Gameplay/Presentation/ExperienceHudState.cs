/// <summary>
/// Presentation data prepared by the experience controller. The view does not infer
/// progress or scoring from gameplay objects and never changes the supplied model.
/// </summary>
public sealed class ExperienceHudState
{
    public string Title = "";
    public string Stats = "";
    public string Record = "";
    public string Result = "";
    public string Detail = "";
    public bool IsPlaying;
    public bool HintsEnabled = true;
    public bool ReducedMotion;
    public bool HighContrast;
}
