using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

/// <summary>
/// Exercises the composed experience through the real scene entry points. The probe
/// supplies independent observations; tests do not pretend that native rendering or
/// collider import occurred. Device persistence is replaced by an isolated dictionary.
/// </summary>
internal static class GameplayExperienceTests
{
    public static void Register(Action<string, Action> run)
    {
        run("Existing scenes compose the experience exactly once on Start", AutomaticComposition);
        run("Successful spawns reach achievements but rejected requests do not", SuccessfulSpawn);
        run("Canceled cloning cannot unlock a block achievement", CanceledSpawn);
        run("A solved run records stars bests and achievements once", SolveAndRepeat);
        run("A public result listener can navigate without losing the committed win", NavigateOnClear);
        run("A public spawn listener can evaluate with the authoritative block total", EvaluateOnSpawn);
        run("A restarted stage rejects old board feedback", StaleFeedback);
        run("Board feedback distinguishes missing cells and restores on retry", VisibleBoardFeedback);
        run("Disabling and re-enabling reconciles the active session without duplicate spawns", Reenable);
        run("Unavailable local storage keeps gameplay working and retries on pause", FailedPersistence);
        run("Best records survive creating another experience", ReloadProfile);
        run("Oversized incoming counters cannot clear the running attempt", CounterOverflow);
        run("A probe cannot spawn geometry during an evaluation", GenerateDuringProbe);
        run("Reloading data inside a probe discards its obsolete evaluation", ReloadDuringProbe);
        run("Readability preferences survive a fresh experience", SettingsRoundTrip);
        run("The achievements drawer suspends piece input without releasing ownership", ModalInput);
        run("A new piece resets improvement feedback for the old board", ImprovementFeedback);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual);
    }

    private static void Invoke(object target, string method, params object[] arguments)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }

    private static GameplayExperience Attach(Program.Fixture fixture)
    {
        var experience = fixture.Stage.gameObject.AddComponent<GameplayExperience>();
        experience.Initialize(fixture.Stage);
        return experience;
    }

    private static void Click(string name)
    {
        GameObject.All.Last(go => go.name == name).GetComponent<Button>().onClick.Invoke();
    }

    private sealed class Observations : IProjectionProbe
    {
        private readonly Queue<bool> occupied;
        public Action FirstQuery;
        public Observations(IEnumerable<bool> values) { occupied = new Queue<bool>(values); }
        public bool IsOccupied(Vector3 origin, Vector3 direction, float maxDistance)
        {
            Action callback = FirstQuery;
            FirstQuery = null;
            if (callback != null) callback();
            return occupied.Dequeue();
        }
    }

    private static void Evaluate(Program.Fixture fixture, bool solved)
    {
        // Produce the authored occupancy mask, or a completely empty observed board.
        fixture.Stage.ProjectionProbe = new Observations(fixture.Manager.stage_Floor
            .Concat(fixture.Manager.stage_Wall).Concat(fixture.Manager.stage_FrontWall)
            .Select(value => solved && value == 1));
        fixture.Stage.Slide();
    }

    private static void AutomaticComposition()
    {
        var f = new Program.Fixture();
        Invoke(f.Stage, "Start");
        var experience = f.Stage.GetComponent<GameplayExperience>();
        Equal(false, experience.DisplayState.IsPlaying);
        Invoke(f.Stage, "Start");
        Equal(experience, f.Stage.GetComponent<GameplayExperience>());
        f.Stage.SelectStage(102);
        f.Stage.GenerateBlock(3);
        Equal(1, experience.Profile.LifetimeBlocksUsed);
        Equal(1, experience.CurrentRun.BlocksUsed);
    }

    private static void SuccessfulSpawn()
    {
        var f = new Program.Fixture();
        var experience = Attach(f);
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(1);
        f.Stage.GenerateBlock(2);
        f.Stage.GenerateBlock(99);
        Equal(1, experience.Profile.LifetimeBlocksUsed);
        Equal(true, experience.Profile.IsUnlocked("first_block"));
        Equal(1, experience.CurrentRun.BlocksUsed);
        Equal(1, f.Stage.CurrentSession.BlocksSpawned);
        f.Stage.openMainMenu();
        Equal(null, experience.CurrentRun);
        Equal(0, f.Stage.CurrentSession.BlocksSpawned);
    }

    private static void CanceledSpawn()
    {
        var f = new Program.Fixture();
        var experience = Attach(f);
        f.Stage.SelectStage(101);
        UObject.AfterInstantiate = clone =>
        {
            UObject.AfterInstantiate = null;
            f.Stage.openMainMenu();
        };
        f.Stage.GenerateBlock(1);
        Equal(0, experience.Profile.LifetimeBlocksUsed);
        Equal(false, experience.Profile.IsUnlocked("first_block"));
        Equal(false, experience.DisplayState.IsPlaying);
    }

    private static void SolveAndRepeat()
    {
        var f = new Program.Fixture();
        var experience = Attach(f);
        f.Stage.SelectStage(102);
        f.Stage.GenerateBlock(3);
        Time.unscaledTime = 15;
        Evaluate(f, true);
        Equal(970, experience.CurrentRun.Score);
        Equal(3, experience.CurrentRun.Stars);
        Equal(1, experience.CurrentRun.Checks);
        Equal(true, experience.Profile.IsUnlocked("first_clear"));
        Equal(true, experience.Profile.IsUnlocked("first_try"));
        Equal(true, experience.Profile.IsUnlocked("swift_clear"));
        Equal(970, experience.Profile.TotalXp);
        int saves = PlayerPrefs.SaveCalls;
        Time.unscaledTime = 30;
        Evaluate(f, true);
        Equal(970, experience.Profile.TotalXp);
        Equal(1, experience.CurrentRun.Checks);
        Equal(15d, experience.CurrentRun.ElapsedSeconds);
        Equal(saves, PlayerPrefs.SaveCalls);
    }

    private static void NavigateOnClear()
    {
        var f = new Program.Fixture();
        // Register before Initialize: public subscription order must not decide whether
        // a committed clear earns a record or whether old hints repaint a new session.
        f.Stage.PuzzleEvaluated += result => f.Stage.openMainMenu();
        var experience = Attach(f);
        f.Stage.SelectStage(102);
        Evaluate(f, true);
        Equal(1, experience.Profile.CompletedStages);
        Equal(true, experience.Profile.IsUnlocked("first_clear"));
        Equal(false, experience.DisplayState.IsPlaying);
        Equal(0, f.Stage.CurrentStageNumber);
        Equal(new Color(1, 1, 1), f.Stage.floor[19].GetComponent<MeshRenderer>().material.color);
    }

    private static void EvaluateOnSpawn()
    {
        var f = new Program.Fixture();
        f.Stage.BlockSpawned += spawn => Evaluate(f, true);
        var experience = Attach(f);
        f.Stage.SelectStage(102);
        f.Stage.GenerateBlock(3);
        Equal(true, experience.CurrentRun.IsCompleted);
        Equal(1, experience.CurrentRun.BlocksUsed);
        Equal(1, experience.Profile.LifetimeBlocksUsed);
    }

    private static void StaleFeedback()
    {
        var f = new Program.Fixture();
        Attach(f);
        f.Stage.SelectStage(102);
        ProjectionResult old = null;
        f.Stage.PuzzleEvaluated += result => old = result;
        Evaluate(f, false);
        int oldSession = old.SessionId;
        f.Stage.SelectStage(102);
        Equal(true, f.Stage.CurrentSession.SessionId > oldSession);
        Equal(false, f.Stage.ShowProjectionFeedback(old, true, false));
        Equal(Color.black, f.Stage.floor[19].GetComponent<MeshRenderer>().material.color);
    }

    private static void VisibleBoardFeedback()
    {
        var f = new Program.Fixture();
        var experience = Attach(f);
        f.Stage.SelectStage(102);
        Evaluate(f, false);
        Equal(ProjectionFeedbackPalette.GetColor(ProjectionCellState.MissingRequired, false),
            f.Stage.floor[19].GetComponent<MeshRenderer>().material.color);
        Equal(true, experience.DisplayState.Detail.Contains("Floor 4 | Wall 4 | Front 1"));
        f.Stage.SelectStage(102);
        Equal(Color.black, f.Stage.floor[19].GetComponent<MeshRenderer>().material.color);
        Equal(0, experience.CurrentRun.Checks);
    }

    private static void Reenable()
    {
        var f = new Program.Fixture();
        var experience = Attach(f);
        f.Stage.SelectStage(101);
        Invoke(experience, "OnDisable");
        f.Stage.SelectStage(102);
        Time.unscaledTime = 10;
        Invoke(experience, "OnEnable");
        Invoke(experience, "OnEnable");
        Equal(102, experience.CurrentRun.StageId);
        Equal(10d, experience.CurrentRun.ElapsedSeconds);
        f.Stage.GenerateBlock(3);
        Equal(1, experience.Profile.LifetimeBlocksUsed);
    }

    private static void FailedPersistence()
    {
        var f = new Program.Fixture();
        var experience = Attach(f);
        f.Stage.SelectStage(102);
        PlayerPrefs.FailWrites = true;
        f.Stage.GenerateBlock(3);
        Equal(1, experience.Profile.LifetimeBlocksUsed);
        Equal(true, experience.DisplayState.Record.Contains("not saved"));
        PlayerPrefs.FailWrites = false;
        Invoke(experience, "OnApplicationPause", true);
        var loaded = new ProgressionTracker(new PlayerPrefsProgressStore());
        Equal(1, loaded.Profile.LifetimeBlocksUsed);
    }

    private static void ReloadProfile()
    {
        var f = new Program.Fixture();
        Attach(f);
        f.Stage.SelectStage(102);
        Time.unscaledTime = 20;
        Evaluate(f, true);
        // Retire the old scene's named manager before constructing a replacement;
        // otherwise the substitute's global Find would return that prior scene object.
        f.Manager.gameObject.SetActive(false);
        var second = new Program.Fixture();
        var reloaded = Attach(second);
        Equal(960, reloaded.Profile.GetBest(102).BestScore);
        Equal(true, reloaded.Profile.IsUnlocked("first_clear"));
        second.Stage.SelectStage(102);
        Time.unscaledTime = 60;
        Evaluate(second, true);
        Equal(960, reloaded.Profile.TotalXp);
    }

    private static void CounterOverflow()
    {
        var definition = ScriptableObject.CreateInstance<StageDefinition>();
        definition.StageNumber = 999;
        definition.Inventory = new[] { new BlockStock { BlockId = 1, Count = int.MaxValue },
            new BlockStock { BlockId = 2, Count = 1 } };
        var f = new Program.Fixture(configure: (controller, manager) => manager.AdditionalStages = new[] { definition });
        var experience = Attach(f);
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(1);
        try { f.Stage.SelectStage(999); throw new Exception("Overflow must be rejected."); }
        catch (OverflowException) { }
        Equal(101, experience.CurrentRun.StageId);
        Equal(true, UObject.Spawns[1].Clone.activeSelf);
    }

    private static void GenerateDuringProbe()
    {
        var f = new Program.Fixture();
        Attach(f);
        f.Stage.SelectStage(102);
        f.Stage.ProjectionProbe = new Observations(Enumerable.Repeat(false, 192))
            { FirstQuery = () => f.Stage.GenerateBlock(3) };
        f.Stage.Slide();
        Equal(0, UObject.Spawns.Count);
        Equal(1, f.Stage.GetRemainingBlocks(3));
    }

    private static void ReloadDuringProbe()
    {
        var f = new Program.Fixture();
        var experience = Attach(f);
        f.Stage.SelectStage(102);
        f.Stage.ProjectionProbe = new Observations(Enumerable.Repeat(false, 192)) { FirstQuery = f.Stage.CallVari };
        int events = 0;
        f.Stage.PuzzleEvaluated += result => events++;
        f.Stage.Slide();
        Equal(0, events);
        Equal(0, experience.CurrentRun.Checks);
    }

    private static void SettingsRoundTrip()
    {
        var f = new Program.Fixture();
        var experience = Attach(f);
        Click("Achievements and settings");
        Click("Hints setting");
        Click("Reduced motion setting");
        Click("High contrast setting");
        Equal(false, experience.DisplayState.HintsEnabled);
        Equal(true, experience.DisplayState.ReducedMotion);
        Equal(true, experience.DisplayState.HighContrast);
        Invoke(experience, "OnDestroy");
        f.Manager.gameObject.SetActive(false);
        var reloaded = Attach(new Program.Fixture());
        Equal(false, reloaded.DisplayState.HintsEnabled);
        Equal(true, reloaded.DisplayState.ReducedMotion);
        Equal(true, reloaded.DisplayState.HighContrast);
    }

    private static void ModalInput()
    {
        var f = new Program.Fixture();
        var experience = Attach(f);
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(1);
        var block = UObject.Spawns[1].Clone.GetComponentInChildren<Block>();
        Transform pieceRoot = block.target.transform.parent;
        Vector3 before = pieceRoot.position;
        Click("Achievements and settings");
        Input.Keys.Add(KeyCode.LeftArrow);
        Input.Keys.Add(KeyCode.Z);
        block.ControlBlock("L");
        f.Stage.GenerateBlock(2);
        f.Stage.Slide();
        Equal(before, pieceRoot.position);
        Equal(true, block.IsActive);
        Equal(true, block.InputSuspended);
        Equal(2, UObject.Spawns.Count);
        Equal(0, experience.CurrentRun.Checks);
        Click("Close drawer");
        Input.Keys.Clear();
        block.ControlBlock("L");
        Equal(false, block.InputSuspended);
        Equal(before + Vector3.right * .5f, pieceRoot.position);
        Click("Achievements and settings");
        Invoke(experience, "OnDisable");
        Equal(false, block.InputSuspended);
    }

    private static void ImprovementFeedback()
    {
        var f = new Program.Fixture();
        var experience = Attach(f);
        f.Stage.SelectStage(101);
        Evaluate(f, false);
        var improved = new bool[192];
        improved[Array.FindIndex(f.Manager.stage_Floor, value => value == 1)] = true;
        f.Stage.ProjectionProbe = new Observations(improved);
        f.Stage.Slide();
        Equal(1, experience.ImprovementCombo);
        Equal(true, experience.DisplayState.Detail.Contains("Combo x1"));
        f.Stage.GenerateBlock(1);
        Equal(0, experience.ImprovementCombo);
        Equal(false, experience.DisplayState.Detail.Contains("Combo"));
        Evaluate(f, false);
        Equal(0, experience.ImprovementCombo);
    }
}
