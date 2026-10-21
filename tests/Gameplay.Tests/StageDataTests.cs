using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Exercises stage authoring and selection without loading Unity assets. The immutable
/// layout is tested directly; ScriptableObject instances only stand in for Inspector data.
/// </summary>
internal static class StageDataTests
{
    public static void Register(Action<string, Action> run)
    {
        run("Stage layouts isolate authored data and returned session copies", DefensiveCopies);
        run("Stage layouts reject invalid IDs and negative inventory", InvalidInventory);
        run("Stage layouts reject missing, mismatched and nonbinary projections", InvalidPatterns);
        run("Stage assets create reusable independent session layouts", ReusableAsset);
        run("Stage assets reject duplicate stock IDs and incompatible board sizes", InvalidAsset);
        run("Additional stage IDs load without adding controller branches", AdditionalStage);
        run("Stage preflight resolves immutable layouts without changing the active puzzle", PreflightLayout);
        run("Unknown stage IDs preserve the currently selected data", UnknownStage);
        run("Duplicate additional IDs are rejected before selection changes", DuplicateStageIds);
        run("Invalid unselected stage assets cannot partially replace current data", InvalidUnselectedStage);
        run("Null additional stage entries fail without resetting the selected puzzle", NullStageEntry);
        run("An authored stage can override the built-in stage 101", OverrideBuiltIn);
        run("Inventory snapshots preserve new IDs and legacy field edits independently", InventoryBridge);
        run("Reset removes additional stock and clears every legacy inventory field", ResetAdditionalInventory);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual))
            throw new Exception("Expected " + expected + ", got " + actual);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new Exception("Expected exception " + typeof(T).Name);
    }

    private static StageLayout SmallLayout(IDictionary<int, int> inventory = null,
        int[] floor = null, int[] wall = null, int[] frontWall = null, int stageNumber = 102)
    {
        // The pure layout can describe another board size; the scene-facing asset is
        // responsible for checking compatibility with the current 64-cell grid.
        return new StageLayout(stageNumber, inventory ?? new Dictionary<int, int> { { 8, 2 } },
            floor ?? new[] { 1, 0, 0, 1 }, wall ?? new[] { 0, 1, 1, 0 },
            frontWall ?? new[] { 1, 1, 0, 0 });
    }

    private static void DefensiveCopies()
    {
        var inventory = new Dictionary<int, int> { { 8, 2 } };
        var floor = new[] { 1, 0, 0, 1 };
        var wall = new[] { 0, 1, 1, 0 };
        var front = new[] { 1, 1, 0, 0 };
        var layout = new StageLayout(102, inventory, floor, wall, front);

        // Changing either the authoring inputs or a returned runtime copy must not
        // mutate a layout that another session or preview may already be using.
        inventory[8] = 99;
        inventory[9] = 1;
        floor[0] = 0;
        wall[0] = 1;
        front[0] = 0;
        layout.GetInitialInventory()[8] = 42;
        layout.CopyFloor()[0] = 0;
        layout.CopyWall()[0] = 1;
        layout.CopyFrontWall()[0] = 0;

        Equal(102, layout.StageNumber);
        Equal(4, layout.CellCount);
        Equal(2, layout.GetInitialInventory()[8]);
        Equal(false, layout.GetInitialInventory().ContainsKey(9));
        Equal("1,0,0,1", string.Join(",", layout.CopyFloor()));
        Equal("0,1,1,0", string.Join(",", layout.CopyWall()));
        Equal("1,1,0,0", string.Join(",", layout.CopyFrontWall()));
    }

    private static void InvalidInventory()
    {
        Throws<ArgumentException>(() => SmallLayout(stageNumber: 0));
        Throws<ArgumentException>(() => SmallLayout(new Dictionary<int, int> { { 0, 1 } }));
        Throws<ArgumentException>(() => SmallLayout(new Dictionary<int, int> { { 8, -1 } }));
        Throws<ArgumentNullException>(() => new StageLayout(102, null,
            new[] { 0 }, new[] { 0 }, new[] { 0 }));
        Equal(0, SmallLayout(new Dictionary<int, int> { { 8, 0 } }).GetInitialInventory()[8]);
    }

    private static void InvalidPatterns()
    {
        Throws<ArgumentNullException>(() => new StageLayout(102, new Dictionary<int, int>(),
            null, new[] { 0 }, new[] { 0 }));
        Throws<ArgumentException>(() => SmallLayout(floor: new int[0], wall: new int[0], frontWall: new int[0]));
        Throws<ArgumentException>(() => SmallLayout(wall: new[] { 0, 1 }));
        Throws<ArgumentException>(() => SmallLayout(frontWall: new[] { 0, 1, 0 }));
        Throws<ArgumentException>(() => SmallLayout(floor: new[] { 2, 0, 0, 0 }));
        Throws<ArgumentException>(() => SmallLayout(wall: new[] { 0, -1, 0, 0 }));
        Throws<ArgumentException>(() => SmallLayout(frontWall: new[] { 0, 0, 3, 0 }));
    }

    private static StageDefinition Asset(int stageNumber = 102)
    {
        var asset = ScriptableObject.CreateInstance<StageDefinition>();
        asset.StageNumber = stageNumber;
        asset.Inventory = new[]
        {
            new BlockStock { BlockId = 1, Count = 3 },
            new BlockStock { BlockId = 8, Count = 2 }
        };
        asset.Floor = new int[StageManager.GridCellCount];
        asset.Wall = new int[StageManager.GridCellCount];
        asset.FrontWall = new int[StageManager.GridCellCount];
        asset.Floor[2] = 1;
        asset.Wall[9] = 1;
        asset.FrontWall[10] = 1;
        return asset;
    }

    private static void ReusableAsset()
    {
        var asset = Asset();
        var first = asset.CreateLayout(StageManager.GridCellCount);
        first.GetInitialInventory()[8] = 0;
        first.CopyFloor()[2] = 0;

        // An authoring edit affects the next snapshot, not a layout already in play.
        asset.Inventory[1].Count = 5;
        asset.Floor[2] = 0;
        var next = asset.CreateLayout(StageManager.GridCellCount);
        Equal(2, first.GetInitialInventory()[8]);
        Equal(1, first.CopyFloor()[2]);
        Equal(5, next.GetInitialInventory()[8]);
        Equal(0, next.CopyFloor()[2]);
    }

    private static void InvalidAsset()
    {
        var asset = Asset();
        asset.Inventory = new[]
        {
            new BlockStock { BlockId = 8, Count = 1 },
            new BlockStock { BlockId = 8, Count = 2 }
        };
        Throws<ArgumentException>(() => asset.CreateLayout(StageManager.GridCellCount));
        asset = Asset();
        Throws<ArgumentException>(() => asset.CreateLayout(4));
        Throws<ArgumentException>(() => asset.CreateLayout(0));
        asset.Inventory = null;
        Throws<ArgumentException>(() => asset.CreateLayout(StageManager.GridCellCount));
    }

    private static void AdditionalStage()
    {
        var manager = new StageManager { AdditionalStages = new[] { Asset() } };
        Equal(true, manager.HasStage(101));
        Equal(true, manager.HasStage(102));
        Equal(false, manager.HasStage(999));
        Equal(true, manager.TryLoadStage(102));
        Equal(3, manager.A1Number);
        Equal(0, manager.A2Number);
        Equal(2, manager.CreateInventorySnapshot()[8]);
        Equal(1, manager.stage_Floor[2]);
        Equal(1, manager.stage_Wall[9]);
        Equal(1, manager.stage_FrontWall[10]);

        manager.stage_Floor[2] = 0;
        manager.A1Number = 0;
        Equal(true, manager.TryLoadStage(102));
        Equal(3, manager.A1Number);
        Equal(1, manager.stage_Floor[2]);
        manager.stage101();
        Equal(1, manager.A1Number);
        Equal(false, manager.CreateInventorySnapshot().ContainsKey(8));
    }

    private static void UnknownStage()
    {
        var manager = new StageManager();
        manager.stage101();
        var floor = manager.stage_Floor;
        var wall = manager.stage_Wall;
        var front = manager.stage_FrontWall;
        manager.A1Number = 7;
        Equal(false, manager.TryLoadStage(999));
        Equal(7, manager.A1Number);
        Equal(true, ReferenceEquals(floor, manager.stage_Floor));
        Equal(true, ReferenceEquals(wall, manager.stage_Wall));
        Equal(true, ReferenceEquals(front, manager.stage_FrontWall));
    }

    private static void PreflightLayout()
    {
        var asset = Asset();
        var manager = new StageManager { AdditionalStages = new[] { asset } };
        manager.stage101();
        manager.A1Number = 5;
        var previousFloor = manager.stage_Floor;
        StageLayout layout;
        Equal(true, manager.TryGetStageLayout(102, out layout));
        Equal(102, layout.StageNumber);
        Equal(1, layout.CopyFloor()[2]);
        layout.CopyFloor()[2] = 0;
        asset.Floor[2] = 0;
        Equal(1, layout.CopyFloor()[2]);
        Equal(5, manager.A1Number);
        Equal(true, ReferenceEquals(previousFloor, manager.stage_Floor));

        // The coordinator can reject unknown or invalid selections before clearing a
        // playable board; neither failed preflight may mutate the selected session.
        Equal(false, manager.TryGetStageLayout(999, out layout));
        Equal(null, layout);
        asset.Floor[0] = 2;
        Throws<InvalidOperationException>(() => manager.TryGetStageLayout(102, out layout));
        Equal(5, manager.A1Number);
        Equal(true, ReferenceEquals(previousFloor, manager.stage_Floor));
    }

    private static void DuplicateStageIds()
    {
        var manager = new StageManager();
        manager.stage101();
        var floor = manager.stage_Floor;
        manager.AdditionalStages = new[] { Asset(), Asset() };
        Throws<InvalidOperationException>(() => manager.TryLoadStage(102));
        Equal(1, manager.A1Number);
        Equal(true, ReferenceEquals(floor, manager.stage_Floor));
    }

    private static void InvalidUnselectedStage()
    {
        var manager = new StageManager();
        manager.stage101();
        var floor = manager.stage_Floor;
        var invalid = Asset(998);
        invalid.Wall = new int[1];
        manager.AdditionalStages = new[] { Asset(), invalid };
        // Validate the complete catalog before selecting even a valid or built-in ID.
        // This prevents behavior from depending on the order of Inspector entries.
        Throws<InvalidOperationException>(() => manager.TryLoadStage(101));
        Throws<InvalidOperationException>(() => manager.HasStage(102));
        Equal(1, manager.A1Number);
        Equal(true, ReferenceEquals(floor, manager.stage_Floor));
    }

    private static void NullStageEntry()
    {
        var manager = new StageManager();
        manager.stage101();
        var floor = manager.stage_Floor;
        manager.AdditionalStages = new StageDefinition[] { null };
        Throws<InvalidOperationException>(() => manager.TryLoadStage(101));
        Equal(1, manager.A1Number);
        Equal(true, ReferenceEquals(floor, manager.stage_Floor));
    }

    private static void OverrideBuiltIn()
    {
        var manager = new StageManager { AdditionalStages = new[] { Asset(101) } };
        manager.stage101();
        Equal(3, manager.A1Number);
        Equal(1, manager.stage_Floor[2]);
        Equal(2, manager.CreateInventorySnapshot()[8]);
    }

    private static void InventoryBridge()
    {
        var manager = new StageManager { AdditionalStages = new[] { Asset() } };
        manager.TryLoadStage(102);
        manager.A1Number = 7;
        manager.ZNumber = 4;
        var snapshot = manager.CreateInventorySnapshot();
        Equal(7, snapshot[1]);
        Equal(4, snapshot[7]);
        Equal(2, snapshot[8]);
        snapshot[1] = 99;
        snapshot[8] = 99;
        snapshot[9] = 1;
        var next = manager.CreateInventorySnapshot();
        Equal(7, next[1]);
        Equal(2, next[8]);
        Equal(false, next.ContainsKey(9));
    }

    private static void ResetAdditionalInventory()
    {
        var manager = new StageManager { AdditionalStages = new[] { Asset() } };
        manager.TryLoadStage(102);
        manager.ResetStage();
        var snapshot = manager.CreateInventorySnapshot();
        Equal(false, snapshot.ContainsKey(8));
        Equal(true, Enumerable.Range(1, 7).All(id => snapshot[id] == 0));
        Equal(0, manager.stage_Floor.Sum());
        Equal(0, manager.stage_Wall.Sum());
        Equal(0, manager.stage_FrontWall.Sum());
    }
}
