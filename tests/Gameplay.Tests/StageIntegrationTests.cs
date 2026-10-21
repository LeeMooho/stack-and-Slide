using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

/// <summary>
/// Exercises the compatibility facade with independently registered content. These
/// tests prove extension paths are connected, not merely that helper classes exist.
/// </summary>
internal static class StageIntegrationTests
{
    public static void Register(Action<string, Action> run)
    {
        run("The scene facade selects an additional stage without an ID branch", AdditionalStage);
        run("A registered piece ID flows through selection spawning stock and UI", AdditionalPiece);
        run("A block asset can replace a legacy prefab without rewiring its button", OverridePiece);
        run("A missing extension icon cannot erase an active session", MissingExtensionUi);
        run("An unknown stocked piece cannot erase an active session", MissingExtensionPiece);
        run("An oversized inventory display cannot erase an active session", InsufficientExtensionSlots);
        run("Projection results reach independent listeners through a replaceable probe", EvaluationEvent);
        run("Null projection backends are rejected without losing the current probe", NullProbe);
        run("Legacy count writes still reach the modular inventory", LegacyCountWrite);
        run("Preflighted layout loading cannot observe later authoring edits", PreflightedSnapshot);
        run("Reentrant facade calls cannot consume stock for a rejected nested spawn", ReentrantFacade);
        run("A menu callback during cloning cancels the entire pending pair", MenuDuringSpawn);
        run("A stage selection during cloning cannot consume the replacement stock", SelectionDuringSpawn);
        run("A legacy reload during cloning cancels the pending pair", ReloadDuringSpawn);
        run("A menu callback while retiring the previous ghost cancels replacement", MenuDuringRetirement);
        run("Nested teardown callbacks cannot generate or replace the incoming stage", NestedTransition);
        run("Inventory UI callbacks cannot replace stock halfway through rendering", ReloadDuringPresentation);
        run("An earlier result listener cannot rewrite the stage in another listener's result", ResultStageContext);
        run("A probe that changes session cannot publish an obsolete evaluation", ObsoleteEvaluation);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static StageDefinition StageAsset(int number, int pieceId, int count)
    {
        var definition = ScriptableObject.CreateInstance<StageDefinition>();
        definition.StageNumber = number;
        definition.Inventory = new[] { new BlockStock { BlockId = pieceId, Count = count } };
        definition.Floor[1] = 1;
        definition.Wall[2] = 1;
        definition.FrontWall[3] = 1;
        return definition;
    }

    private static void RegisterPiece(stage controller, bool withDefinition, bool withUi)
    {
        if (withDefinition)
        {
            var definition = ScriptableObject.CreateInstance<BlockDefinition>();
            definition.BlockId = 8;
            definition.BlockPrefab = controller.A1Block;
            definition.GhostPrefab = controller.GhostA1Block;
            definition.SpawnOffset = new Vector3(1, .5f, -.5f);
            controller.AdditionalBlocks = new[] { definition };
        }
        if (withUi)
        {
            controller.AdditionalInventoryBindings = new[]
            {
                new InventoryBinding(8, new GameObject("Extra icon"), new GameObject().AddComponent<Text>())
            };
        }
    }

    private static void AdditionalStage()
    {
        var definition = StageAsset(102, 1, 2);
        var f = new Program.Fixture(configure: (controller, manager) => manager.AdditionalStages = new[] { definition });
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(1);
        var oldRoot = UObject.Spawns[1].Clone;
        f.Stage.SelectStage(102);
        Equal(false, oldRoot.activeSelf);
        Equal(102, f.Stage.CurrentStageNumber);
        Equal(true, f.Stage.IsPlaying);
        Equal(2, f.Stage.GetRemainingBlocks(1));
        Equal("2", f.Stage.A1Text.text);
        Equal(Color.black, f.Stage.floor[1].GetComponent<MeshRenderer>().material.color);
        Equal(new Color(1, 1, 1), f.Stage.floor[26].GetComponent<MeshRenderer>().material.color);
        f.Stage.GenerateBlock(1);
        f.Stage.SelectStage(102);
        Equal(2, f.Stage.GetRemainingBlocks(1));
        Equal(2, definition.Inventory[0].Count);
    }

    private static void AdditionalPiece()
    {
        var f = new Program.Fixture(configure: (controller, manager) =>
        {
            RegisterPiece(controller, true, true);
            manager.AdditionalStages = new[] { StageAsset(102, 8, 2) };
        });
        f.Stage.SelectStage(102);
        var binding = f.Stage.AdditionalInventoryBindings[0];
        Equal(true, binding.Image.activeSelf);
        Equal("2", binding.CountText.text);
        Equal(f.Stage.Location[0].position, binding.Image.transform.position);
        f.Stage.GenerateBlock(8);
        Equal(1, f.Stage.GetRemainingBlocks(8));
        Equal(0, f.Stage.A1Number);
        Equal(new Vector3(2, 4.5f, 1.5f), UObject.Spawns[1].Clone.transform.position);
        f.Stage.GenerateBlock(8);
        Equal(2, UObject.Spawns.Count);
        Equal(1, f.Stage.GetRemainingBlocks(8));
        UObject.Spawns[1].Clone.GetComponentInChildren<Block>().target.tag = "OnFixedBlock";
        f.Stage.GenerateBlock(8);
        Equal(4, UObject.Spawns.Count);
        Equal(0, f.Stage.GetRemainingBlocks(8));
        Equal(false, binding.Image.activeSelf);
    }

    private static void OverridePiece()
    {
        var f = new Program.Fixture(configure: (controller, manager) =>
        {
            var replacement = ScriptableObject.CreateInstance<BlockDefinition>();
            replacement.BlockId = 1;
            replacement.BlockPrefab = controller.A2Block;
            replacement.GhostPrefab = controller.GhostA2Block;
            controller.AdditionalBlocks = new[] { replacement };
        });
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(1);
        Equal(f.Stage.A2Block, UObject.Spawns[1].Prefab);
        Equal(f.Stage.GhostA2Block, UObject.Spawns[0].Prefab);
        Equal(false, f.Stage.A1Image.activeSelf);
        Equal(1, f.Stage.GetRemainingBlocks(2));
    }

    private static void AssertSessionSurvives(Program.Fixture f)
    {
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(1);
        var root = UObject.Spawns[1].Clone;
        Throws<InvalidOperationException>(() => f.Stage.SelectStage(102));
        Equal(true, root.activeSelf);
        Equal(101, f.Stage.CurrentStageNumber);
        Equal(true, f.Stage.IsPlaying);
        Equal(0, f.Stage.A1Number);
        Equal(1, f.Stage.GetRemainingBlocks(2));
        Equal(2, UObject.Spawns.Count);
    }

    private static void MissingExtensionUi()
    {
        AssertSessionSurvives(new Program.Fixture(configure: (controller, manager) =>
        {
            RegisterPiece(controller, true, false);
            manager.AdditionalStages = new[] { StageAsset(102, 8, 1) };
        }));
    }

    private static void MissingExtensionPiece()
    {
        AssertSessionSurvives(new Program.Fixture(configure: (controller, manager) =>
        {
            RegisterPiece(controller, false, true);
            manager.AdditionalStages = new[] { StageAsset(102, 8, 1) };
        }));
    }

    private static void InsufficientExtensionSlots()
    {
        AssertSessionSurvives(new Program.Fixture(configure: (controller, manager) =>
        {
            RegisterPiece(controller, true, true);
            var definition = StageAsset(102, 8, 1);
            definition.Inventory = Enumerable.Range(1, 8)
                .Select(id => new BlockStock { BlockId = id, Count = 1 }).ToArray();
            manager.AdditionalStages = new[] { definition };
        }));
    }

    private sealed class EmptyProbe : IProjectionProbe
    {
        public int Calls;
        public bool IsOccupied(Vector3 origin, Vector3 direction, float maxDistance)
        {
            Calls++;
            return false;
        }
    }

    private static void EvaluationEvent()
    {
        var f = new Program.Fixture();
        var probe = new EmptyProbe();
        f.Stage.ProjectionProbe = probe;
        int events = 0;
        ProjectionResult snapshot = null;
        Action<ProjectionResult> listener = result => { events++; snapshot = result; };
        f.Stage.PuzzleEvaluated += listener;
        f.Stage.Slide();
        Equal(0, events);
        f.Stage.SelectStage(101);
        f.Stage.Slide();
        Equal(1, events);
        Equal(192, probe.Calls);
        Equal(0, Physics.Directions.Count);
        Equal(f.Manager.stage_Floor.Sum(), snapshot.FloorMismatches);
        Equal(f.Manager.stage_Wall.Sum(), snapshot.WallMismatches);
        Equal(f.Manager.stage_FrontWall.Sum(), snapshot.FrontWallMismatches);
        Equal(false, snapshot.IsSolved);
        Equal(101, snapshot.StageNumber);
        f.Stage.PuzzleEvaluated -= listener;
        f.Stage.Slide();
        Equal(1, events);
        f.Stage.openMainMenu();
        f.Stage.Slide();
        Equal(384, probe.Calls);
        Equal(0, f.Stage.CurrentStageNumber);
        Equal(false, f.Stage.IsPlaying);
        Equal(31, snapshot.TotalMismatches);
    }

    private static void NullProbe()
    {
        var f = new Program.Fixture();
        var original = f.Stage.ProjectionProbe;
        Throws<ArgumentNullException>(() => f.Stage.ProjectionProbe = null);
        Equal(original, f.Stage.ProjectionProbe);
    }

    private static void LegacyCountWrite()
    {
        var f = new Program.Fixture();
        f.Stage.SelectStage(101);
        f.Stage.A1Number = 3;
        f.Stage.GenerateBlock(1);
        Equal(2, f.Stage.A1Number);
        Equal(2, f.Stage.GetRemainingBlocks(1));
        Equal("2", f.Stage.A1Text.text);
    }

    private static void PreflightedSnapshot()
    {
        var definition = StageAsset(102, 1, 2);
        var manager = new StageManager { AdditionalStages = new[] { definition } };
        StageLayout selected;
        Equal(true, manager.TryGetStageLayout(102, out selected));
        definition.Inventory[0].Count = 99;
        definition.Floor[1] = 0;
        manager.LoadLayout(selected);
        Equal(2, manager.A1Number);
        Equal(1, manager.stage_Floor[1]);
        Throws<ArgumentNullException>(() => manager.LoadLayout(null));
        Equal(2, manager.A1Number);
    }

    private static void ReentrantFacade()
    {
        var f = new Program.Fixture();
        f.Stage.SelectStage(101);
        UObject.AfterInstantiate = clone =>
        {
            UObject.AfterInstantiate = null;
            f.Stage.GenerateBlock(2);
        };
        f.Stage.GenerateBlock(1);
        Equal(2, UObject.Spawns.Count);
        Equal(0, f.Stage.GetRemainingBlocks(1));
        Equal(1, f.Stage.GetRemainingBlocks(2));
    }

    private static void AssertNoLiveSpawns()
    {
        foreach (var spawn in UObject.Spawns)
        {
            Equal(false, spawn.Clone.activeSelf);
            Equal(true, UObject.Destroyed.Contains(spawn.Clone));
        }
    }

    private static void MenuDuringSpawn()
    {
        var f = new Program.Fixture();
        f.Stage.SelectStage(101);
        UObject.AfterInstantiate = clone =>
        {
            UObject.AfterInstantiate = null;
            f.Stage.openMainMenu();
        };
        f.Stage.GenerateBlock(1);
        Equal(false, f.Stage.IsPlaying);
        Equal(0, f.Stage.CurrentStageNumber);
        Equal(0, f.Stage.GetRemainingBlocks(1));
        Equal(null, f.Stage.pre_GhostBlock);
        AssertNoLiveSpawns();
        // The canceled request must also release the generation guard for later play.
        f.Stage.SelectStage(101);
        int before = UObject.Spawns.Count;
        f.Stage.GenerateBlock(1);
        Equal(before + 2, UObject.Spawns.Count);
    }

    private static void SelectionDuringSpawn()
    {
        var f = new Program.Fixture(configure: (controller, manager) =>
            manager.AdditionalStages = new[] { StageAsset(102, 1, 3) });
        f.Stage.SelectStage(101);
        UObject.AfterInstantiate = clone =>
        {
            UObject.AfterInstantiate = null;
            f.Stage.SelectStage(102);
        };
        f.Stage.GenerateBlock(1);
        Equal(true, f.Stage.IsPlaying);
        Equal(102, f.Stage.CurrentStageNumber);
        Equal(3, f.Stage.GetRemainingBlocks(1));
        Equal("3", f.Stage.A1Text.text);
        Equal(null, f.Stage.pre_GhostBlock);
        AssertNoLiveSpawns();
        f.Stage.GenerateBlock(1);
        Equal(2, f.Stage.GetRemainingBlocks(1));
    }

    private static void ReloadDuringSpawn()
    {
        var f = new Program.Fixture();
        f.Stage.SelectStage(101);
        UObject.AfterInstantiate = clone =>
        {
            UObject.AfterInstantiate = null;
            f.Manager.A1Number = 4;
            f.Stage.CallVari();
        };
        f.Stage.GenerateBlock(1);
        Equal(4, f.Stage.GetRemainingBlocks(1));
        Equal(null, f.Stage.pre_GhostBlock);
        AssertNoLiveSpawns();
        f.Stage.GenerateBlock(1);
        Equal(3, f.Stage.GetRemainingBlocks(1));
    }

    private static void MenuDuringRetirement()
    {
        var f = new Program.Fixture();
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(1);
        var oldGhost = f.Stage.pre_GhostBlock;
        UObject.Spawns[1].Clone.GetComponentInChildren<Block>().target.tag = "OnFixedBlock";
        UObject.AfterSetActive = (root, active) =>
        {
            if (root != oldGhost || active) return;
            UObject.AfterSetActive = null;
            f.Stage.openMainMenu();
        };
        f.Stage.GenerateBlock(2);
        Equal(false, f.Stage.IsPlaying);
        Equal(0, f.Stage.GetRemainingBlocks(2));
        Equal(null, f.Stage.pre_GhostBlock);
        AssertNoLiveSpawns();
    }

    private static void NestedTransition()
    {
        var f = new Program.Fixture();
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(1);
        var oldGhost = f.Stage.pre_GhostBlock;
        UObject.AfterSetActive = (root, active) =>
        {
            if (root != oldGhost || active) return;
            UObject.AfterSetActive = null;
            f.Stage.GenerateBlock(2);
            f.Stage.openMainMenu();
        };
        int before = UObject.Spawns.Count;
        f.Stage.SelectStage(101);
        Equal(before, UObject.Spawns.Count);
        Equal(true, f.Stage.IsPlaying);
        Equal(101, f.Stage.CurrentStageNumber);
        Equal(1, f.Stage.GetRemainingBlocks(1));
        Equal(1, f.Stage.GetRemainingBlocks(2));
        AssertNoLiveSpawns();
    }

    private static void ResultStageContext()
    {
        var f = new Program.Fixture(configure: (controller, manager) =>
            manager.AdditionalStages = new[] { StageAsset(102, 1, 1) });
        f.Stage.SelectStage(101);
        f.Stage.ProjectionProbe = new EmptyProbe();
        f.Stage.PuzzleEvaluated += result => f.Stage.SelectStage(102);
        ProjectionResult observed = null;
        f.Stage.PuzzleEvaluated += result => observed = result;
        f.Stage.Slide();
        Equal(102, f.Stage.CurrentStageNumber);
        Equal(101, observed.StageNumber);
        Equal(31, observed.TotalMismatches);
    }

    private static void ReloadDuringPresentation()
    {
        var f = new Program.Fixture();
        f.Stage.SelectStage(101);
        bool attemptedReload = false;
        UObject.AfterSetActive = (root, active) =>
        {
            if (root != f.Stage.A1Image || active) return;
            UObject.AfterSetActive = null;
            attemptedReload = true;
            f.Manager.A1Number = 4;
            f.Stage.CallVari();
        };
        f.Stage.GenerateBlock(1);
        Equal(true, attemptedReload);
        Equal(0, f.Stage.A1Number);
        Equal(0, f.Stage.GetRemainingBlocks(1));
        Equal(false, f.Stage.A1Image.activeSelf);
        // The public reload remains usable after the UI update has completed.
        f.Stage.CallVari();
        Equal(4, f.Stage.GetRemainingBlocks(1));
    }

    private sealed class TransitionProbe : IProjectionProbe
    {
        public Action Transition;
        public bool IsOccupied(Vector3 origin, Vector3 direction, float maxDistance)
        {
            Action transition = Transition;
            Transition = null;
            if (transition != null) transition();
            return false;
        }
    }

    private static void ObsoleteEvaluation()
    {
        var f = new Program.Fixture();
        f.Stage.SelectStage(101);
        f.Stage.ProjectionProbe = new TransitionProbe { Transition = f.Stage.openMainMenu };
        int events = 0;
        f.Stage.PuzzleEvaluated += result => events++;
        f.Stage.Slide();
        Equal(0, events);
        Equal(0, Debug.Messages.Count);
        Equal(false, f.Stage.IsPlaying);
    }
}
