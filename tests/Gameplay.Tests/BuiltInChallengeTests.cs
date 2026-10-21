using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Couples the runtime challenge catalog to geometry-derived solution masks. The
/// independent Python fixture derives these cell sets from the authored Unity assets;
/// these tests ensure the factories and selection adapter publish the same puzzles.
/// </summary>
internal static class BuiltInChallengeTests
{
    public static void Register(Action<string, Action> run)
    {
        run("Challenge 102 uses the geometry-checked flat I-piece solution", Challenge102);
        run("Challenge 103 uses the geometry-checked rotated O-piece solution", Challenge103);
        run("Challenge factories return independent stock and target copies", IndependentChallenges);
        run("Both introductory challenges load through the default stage catalog", DefaultCatalog);
        run("Authored stage assets can override either introductory challenge", AuthoredOverrides);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual))
            throw new Exception("Expected " + expected + ", got " + actual);
    }

    private static string Occupied(int[] cells)
    {
        Equal(64, cells.Length);
        if (cells.Any(cell => cell != 0 && cell != 1))
            throw new Exception("Challenge projections must be binary.");
        return string.Join(",", Enumerable.Range(0, cells.Length).Where(index => cells[index] == 1));
    }

    private static void Challenge102()
    {
        StageLayout layout = BuiltInStages.Create102();
        Equal(102, layout.StageNumber);
        Equal(1, layout.GetInitialInventory().Count);
        Equal(1, layout.GetInitialInventory()[3]);
        Equal("19,27,35,43", Occupied(layout.CopyFloor()));
        Equal("58,59,60,61", Occupied(layout.CopyWall()));
        Equal("59", Occupied(layout.CopyFrontWall()));
    }

    private static void Challenge103()
    {
        StageLayout layout = BuiltInStages.Create103();
        Equal(103, layout.StageNumber);
        Equal(1, layout.GetInitialInventory().Count);
        Equal(1, layout.GetInitialInventory()[4]);
        Equal("27,28,35,36", Occupied(layout.CopyFloor()));
        Equal("59,60", Occupied(layout.CopyWall()));
        Equal("59,60", Occupied(layout.CopyFrontWall()));
    }

    private static void IndependentChallenges()
    {
        StageLayout first = BuiltInStages.Create102();
        first.GetInitialInventory()[3] = 0;
        first.CopyFloor()[19] = 0;
        first.CopyWall()[58] = 0;
        first.CopyFrontWall()[59] = 0;
        StageLayout next = BuiltInStages.Create102();
        Equal(1, next.GetInitialInventory()[3]);
        Equal(1, next.CopyFloor()[19]);
        Equal(1, next.CopyWall()[58]);
        Equal(1, next.CopyFrontWall()[59]);
        // Adjacent entries must not share session storage through the common helper.
        next.CopyFloor()[27] = 0;
        Equal(1, BuiltInStages.Create103().CopyFloor()[27]);
    }

    private static void DefaultCatalog()
    {
        var manager = new StageManager();
        manager.stage101();
        int[] previous = manager.stage_Floor;
        StageLayout preview;
        Equal(true, manager.TryGetStageLayout(102, out preview));
        Equal(102, preview.StageNumber);
        Equal(true, ReferenceEquals(previous, manager.stage_Floor));

        Equal(true, manager.TryLoadStage(102));
        Equal(1, manager.INumber);
        Equal(0, manager.ONumber);
        Equal(1, manager.CreateInventorySnapshot().Values.Sum());
        Equal("19,27,35,43", Occupied(manager.stage_Floor));
        Equal(true, manager.TryLoadStage(103));
        Equal(0, manager.INumber);
        Equal(1, manager.ONumber);
        Equal(1, manager.CreateInventorySnapshot().Values.Sum());
        Equal("27,28,35,36", Occupied(manager.stage_Floor));
        previous = manager.stage_Floor;
        Equal(false, manager.TryLoadStage(104));
        Equal(true, ReferenceEquals(previous, manager.stage_Floor));
        Equal(1, manager.ONumber);
    }

    private static void AuthoredOverrides()
    {
        foreach (int stageNumber in new[] { 102, 103 })
        {
            var definition = ScriptableObject.CreateInstance<StageDefinition>();
            definition.StageNumber = stageNumber;
            definition.Inventory = new[] { new BlockStock { BlockId = 1, Count = 2 } };
            definition.Floor[0] = 1;
            var manager = new StageManager { AdditionalStages = new[] { definition } };
            Equal(true, manager.TryLoadStage(stageNumber));
            Equal(2, manager.A1Number);
            Equal(0, manager.INumber);
            Equal(0, manager.ONumber);
            Equal("0", Occupied(manager.stage_Floor));
            Equal("", Occupied(manager.stage_Wall));
            Equal("", Occupied(manager.stage_FrontWall));
        }
    }
}
