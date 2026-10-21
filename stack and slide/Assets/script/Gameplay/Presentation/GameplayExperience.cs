using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Connects committed gameplay to local progression and presentation. Scoring stays in
/// ProgressionTracker, board colors stay in ProjectionGrid, and the HUD owns its UI.
/// The component is composed by stage.Start so old scenes receive the feature automatically.
/// </summary>
public sealed class GameplayExperience : MonoBehaviour
{
    private const string SettingsPrefix = "StackAndSlide.Experience.v1.";
    private stage puzzle;
    private ExperienceHud hud;
    private ProgressionTracker tracker;
    private Func<double> clock;
    private bool bound;
    private bool initialized;
    private bool hintsEnabled = true;
    private bool reducedMotion;
    private bool highContrast;
    private double nextRefresh;
    private long observedSession;
    private int observedStage;
    private ProjectionResult lastEvaluation;

    public ProgressProfile Profile { get { return tracker == null ? null : tracker.Profile; } }
    public RunResult CurrentRun { get { return tracker == null ? null : tracker.GetRun(clock()); } }
    public int ImprovementCombo { get; private set; }
    public ExperienceHudState DisplayState { get; private set; }

    public void Initialize(stage source, IProgressStore store = null, Func<double> now = null)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (initialized)
        {
            if (puzzle != source) throw new InvalidOperationException("An experience belongs to one puzzle.");
            return;
        }

        puzzle = source;
        clock = now ?? (() => Time.unscaledTime);
        tracker = new ProgressionTracker(store ?? new PlayerPrefsProgressStore());
        hintsEnabled = ReadPreference("Hints", true);
        reducedMotion = ReadPreference("ReducedMotion", false);
        highContrast = ReadPreference("HighContrast", false);
        hud = GetComponent<ExperienceHud>();
        if (hud == null) hud = gameObject.AddComponent<ExperienceHud>();
        hud.Initialize();
        hud.CheckRequested += Check;
        hud.RetryRequested += Retry;
        hud.MenuRequested += Menu;
        hud.StageRequested += SelectStage;
        hud.HintsChanged += ChangeHints;
        hud.ReducedMotionChanged += ChangeMotion;
        hud.HighContrastChanged += ChangeContrast;
        hud.DrawerVisibilityChanged += SuspendInput;
        initialized = true;
        Bind();
        SynchronizeSession(puzzle.CurrentSession);
    }

    private void OnEnable()
    {
        // AddComponent invokes this before Initialize can inject its dependencies.
        if (!initialized) return;
        Bind();
        SynchronizeSession(puzzle.CurrentSession);
    }

    private void OnDisable()
    {
        Unbind();
        if (hud != null) hud.SetVisible(false);
    }

    private void Bind()
    {
        if (bound) return;
        puzzle.SessionCommitted += SynchronizeSession;
        puzzle.BlockCommitted += OnBlockCommitted;
        puzzle.EvaluationCommitted += OnEvaluationCommitted;
        bound = true;
        hud.SetVisible(true);
    }

    private void Unbind()
    {
        if (!bound || puzzle == null) return;
        puzzle.SessionCommitted -= SynchronizeSession;
        puzzle.BlockCommitted -= OnBlockCommitted;
        puzzle.EvaluationCommitted -= OnEvaluationCommitted;
        bound = false;
    }

    private void SynchronizeSession(StageSessionInfo session)
    {
        if (session.SessionId != puzzle.CurrentSession.SessionId) return;
        if (!session.IsPlaying)
        {
            if (observedStage > 0) tracker.EndRun(observedSession, observedStage);
            observedSession = session.SessionId;
            observedStage = 0;
            lastEvaluation = null;
            ImprovementCombo = 0;
            hud.ClearTransientFeedback();
            Refresh();
            return;
        }

        if (observedSession != session.SessionId || observedStage != session.StageNumber)
        {
            tracker.StartRun(session.SessionId, session.StageNumber, session.StartedAtSeconds);
            observedSession = session.SessionId;
            observedStage = session.StageNumber;
            lastEvaluation = null;
            ImprovementCombo = 0;
            hud.ClearTransientFeedback();
        }
        string[] unlocked;
        tracker.SyncSpawns(session.SessionId, session.StageNumber, session.BlocksSpawned, out unlocked);
        AnnounceAchievements(unlocked);
        Refresh();
    }

    private bool IsCurrent(int sessionId, int stageNumber)
    {
        StageSessionInfo current = puzzle.CurrentSession;
        return current.IsPlaying && current.SessionId == sessionId && current.StageNumber == stageNumber;
    }

    private void OnBlockCommitted(BlockSpawnInfo spawn)
    {
        if (!IsCurrent(spawn.SessionId, spawn.StageNumber)) return;
        string[] unlocked;
        tracker.SyncSpawns(spawn.SessionId, spawn.StageNumber, spawn.BlocksSpawned, out unlocked);
        AnnounceAchievements(unlocked);
        if (lastEvaluation != null)
        {
            // A new piece makes old cell diagnostics obsolete. Movement can also change
            // them; the HUD explicitly labels any remaining diagnostics as the last check.
            puzzle.ShowProjectionFeedback(lastEvaluation, false, highContrast);
            lastEvaluation = null;
        }
        ImprovementCombo = 0;
        Refresh();
    }

    private void OnEvaluationCommitted(ProjectionResult evaluation)
    {
        if (!IsCurrent(evaluation.SessionId, evaluation.StageNumber)) return;
        // Authoritative totals survive delayed/out-of-order observer notifications.
        string[] unlocked;
        tracker.SyncSpawns(evaluation.SessionId, evaluation.StageNumber, evaluation.BlocksSpawned, out unlocked);
        AnnounceAchievements(unlocked);
        RunResult run;
        if (!tracker.TryEvaluate(evaluation.SessionId, evaluation.StageNumber, evaluation.IsSolved, clock(), out run))
            return;

        if (lastEvaluation != null && !run.IsCompleted)
        {
            ImprovementCombo = evaluation.TotalMismatches < lastEvaluation.TotalMismatches
                ? ImprovementCombo + 1 : 0;
            if (ImprovementCombo > 0)
                hud.ShowToast("Getting closer!", "Improvement combo x" + ImprovementCombo, false);
        }
        lastEvaluation = evaluation;
        puzzle.ShowProjectionFeedback(evaluation, hintsEnabled, highContrast);
        Refresh();
        if (run.NewlyCompleted)
        {
            hud.ShowToast("Puzzle cleared!", run.Stars + "/3 stars  |  " + run.Score + " points", true);
            AnnounceAchievements(run.NewlyUnlocked);
        }
    }

    private void Update()
    {
        if (!initialized || !bound) return;
        double now = clock();
        if (now < nextRefresh) return;
        nextRefresh = now + .2d;
        Refresh();
    }

    private void Refresh()
    {
        RunResult run = tracker.GetRun(clock());
        ProgressProfile profile = tracker.Profile;
        StageBest best = run == null ? null : profile.GetBest(run.StageId);
        bool playing = puzzle.IsPlaying && run != null;
        DisplayState = new ExperienceHudState
        {
            IsPlaying = playing,
            HintsEnabled = hintsEnabled,
            ReducedMotion = reducedMotion,
            HighContrast = highContrast,
            Title = playing ? "STAGE " + run.StageId : "STACK & SLIDE",
            Stats = playing ? FormatTime(run.ElapsedSeconds) + "  |  Checks " + run.Checks + "  |  Blocks " + run.BlocksUsed
                : profile.CompletedStages + " stages cleared  |  " + profile.TotalXp + " XP",
            Record = best == null ? "Best: not cleared yet" : "Best " + best.BestScore + " pts  |  "
                + best.BestStars + "/3 stars  |  " + FormatTime(best.BestTimeSeconds),
            Result = run != null && run.IsCompleted ? "CLEARED  " + run.Score + " pts  " + run.Stars + "/3 stars"
                : lastEvaluation == null ? "Match all three shadows" : "Last check: " + lastEvaluation.TotalMismatches + " cells to fix",
            Detail = lastEvaluation == null ? "Check to reveal missing and extra cells."
                : "Floor " + lastEvaluation.FloorMismatches + " | Wall " + lastEvaluation.WallMismatches
                    + " | Front " + lastEvaluation.FrontWallMismatches
                    + (ImprovementCombo > 0 ? " | Combo x" + ImprovementCombo : "")
        };
        if (!tracker.LastSaveSucceeded) DisplayState.Record += "  (not saved)";
        hud.Render(DisplayState);
        hud.SetAchievementText(BuildAchievementText(profile));
    }

    private static string FormatTime(double seconds)
    {
        long total = (long)Math.Floor(seconds);
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    private void AnnounceAchievements(string[] ids)
    {
        foreach (string id in ids) hud.ShowToast("Achievement unlocked", AchievementTitle(id), false);
    }

    private static string AchievementTitle(string id)
    {
        switch (id)
        {
            case "first_block": return "First step";
            case "first_clear": return "Shadow solver";
            case "first_try": return "First-check finish";
            case "swift_clear": return "Quick thinker";
            case "three_stages": return "Three-dimensional thinker";
            case "builder": return "Block enthusiast";
            default: return id;
        }
    }

    private static string BuildAchievementText(ProgressProfile profile)
    {
        string[] ids = { "first_block", "first_clear", "first_try", "swift_clear", "three_stages", "builder" };
        string[] descriptions = { "Use your first block.", "Clear any stage.", "Clear on the first check.",
            "Clear within 60 seconds.", "Clear three different stages (" + profile.CompletedStages + "/3).",
            "Use 10 blocks (" + Math.Min(profile.LifetimeBlocksUsed, 10) + "/10)." };
        var text = new StringBuilder();
        text.AppendLine("LOCAL ACHIEVEMENTS  |  " + profile.GetUnlockedAchievements().Length + "/6");
        text.AppendLine(profile.TotalXp + " best-score XP  |  " + profile.CompletedStages + " stages cleared");
        for (int i = 0; i < ids.Length; i++)
        {
            text.AppendLine((profile.IsUnlocked(ids[i]) ? "[UNLOCKED] " : "[LOCKED] ") + AchievementTitle(ids[i]));
            text.AppendLine(descriptions[i]);
        }
        text.AppendLine();
        text.AppendLine("3 stars: first check within 60s.\n2 stars: up to 3 checks within 180s.\nXP adds only improvements to each stage's best score.");
        return text.ToString();
    }

    private void Check() { puzzle.Slide(); }
    private void Retry() { if (puzzle.IsPlaying) puzzle.SelectStage(puzzle.CurrentStageNumber); }
    private void Menu() { puzzle.openMainMenu(); }
    private void SelectStage(int stageNumber) { puzzle.SelectStage(stageNumber); }
    private void SuspendInput(bool suspended) { if (puzzle != null) puzzle.SetInputSuspended(suspended); }

    private static bool ReadPreference(string key, bool fallback)
    {
        try { return PlayerPrefs.GetInt(SettingsPrefix + key, fallback ? 1 : 0) != 0; }
        catch (Exception) { return fallback; }
    }

    private void SavePreference(string key, bool value)
    {
        try
        {
            PlayerPrefs.SetInt(SettingsPrefix + key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
        catch (Exception)
        {
            hud.ShowToast("Settings active", "Device storage is unavailable; this session keeps your choice.", false);
        }
    }

    private void ChangeHints(bool value)
    {
        hintsEnabled = value;
        SavePreference("Hints", value);
        if (lastEvaluation != null) puzzle.ShowProjectionFeedback(lastEvaluation, value, highContrast);
        Refresh();
    }

    private void ChangeMotion(bool value)
    {
        reducedMotion = value;
        SavePreference("ReducedMotion", value);
        Refresh();
    }

    private void ChangeContrast(bool value)
    {
        highContrast = value;
        SavePreference("HighContrast", value);
        if (lastEvaluation != null) puzzle.ShowProjectionFeedback(lastEvaluation, hintsEnabled, value);
        Refresh();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && tracker != null) tracker.Flush();
    }

    private void OnDestroy()
    {
        Unbind();
        if (hud == null) return;
        hud.CheckRequested -= Check;
        hud.RetryRequested -= Retry;
        hud.MenuRequested -= Menu;
        hud.StageRequested -= SelectStage;
        hud.HintsChanged -= ChangeHints;
        hud.ReducedMotionChanged -= ChangeMotion;
        hud.HighContrastChanged -= ChangeContrast;
        hud.DrawerVisibilityChanged -= SuspendInput;
        if (puzzle != null) puzzle.SetInputSuspended(false);
        hud.Dispose();
    }
}
