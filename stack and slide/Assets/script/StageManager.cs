using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves reusable stage definitions and exposes the selected session through the
/// original scene-facing fields. Scene buttons and existing Inspector bindings remain valid.
/// </summary>
public class StageManager : MonoBehaviour
{
    public const int GridCellCount = 64;

    public int A1Number;
    public int A2Number;
    public int INumber;
    public int ONumber;
    public int PNumber;
    public int TNumber;
    public int ZNumber;

    // Keep these serialized names as a compatibility boundary for existing scenes.
    // The copies belong to the selected session, never to the StageDefinition asset.
    public int[] stage_Floor;
    public int[] stage_Wall;
    public int[] stage_FrontWall;

    // An asset may also use ID 101 to replace the built-in puzzle without changing
    // its existing button. IDs must be unique among the explicitly registered assets.
    public StageDefinition[] AdditionalStages = new StageDefinition[0];

    private Dictionary<int, int> selectedInventory = new Dictionary<int, int>();

    /// <summary>Clears legacy counts, future block IDs, and all current-session grid copies.</summary>
    public void ResetStage()
    {
        A1Number = 0;
        A2Number = 0;
        INumber = 0;
        ONumber = 0;
        PNumber = 0;
        TNumber = 0;
        ZNumber = 0;
        selectedInventory.Clear();

        // CallVari rebinds the scene controller after these session arrays are replaced.
        stage_Floor = new int[GridCellCount];
        stage_Wall = new int[GridCellCount];
        stage_FrontWall = new int[GridCellCount];
    }

    /// <summary>Preserves the method already wired into legacy stage-selection buttons.</summary>
    public void stage101()
    {
        TryLoadStage(101);
    }

    /// <summary>
    /// Checks the validated registry without changing the selected stage. Definitions are
    /// resolved on demand so replacing the Inspector array never leaves a stale catalog.
    /// </summary>
    public bool HasStage(int stageNumber)
    {
        StageLayout layout;
        return TryGetStageLayout(stageNumber, out layout);
    }

    /// <summary>
    /// Loads a supported puzzle into independent session copies. Missing IDs return false;
    /// malformed registrations throw before any current-session fields are changed.
    /// </summary>
    public bool TryLoadStage(int stageNumber)
    {
        StageLayout layout;
        if (!TryGetStageLayout(stageNumber, out layout))
            return false;

        LoadLayout(layout);
        return true;
    }

    /// <summary>
    /// Publishes the exact immutable layout a coordinator has already preflighted.
    /// Later edits to an authoring asset cannot replace that validated selection.
    /// </summary>
    public void LoadLayout(StageLayout layout)
    {
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        if (layout.CellCount != GridCellCount)
            throw new ArgumentException("A layout must match the existing 8-by-8 scene grids.", nameof(layout));

        // Prepare every copy before publishing the selection to scene-facing fields.
        // This keeps validation failures from partially replacing the active puzzle.
        Dictionary<int, int> inventory = layout.GetInitialInventory();
        int[] floor = layout.CopyFloor();
        int[] wall = layout.CopyWall();
        int[] frontWall = layout.CopyFrontWall();
        selectedInventory = inventory;
        A1Number = Count(inventory, 1);
        A2Number = Count(inventory, 2);
        INumber = Count(inventory, 3);
        ONumber = Count(inventory, 4);
        PNumber = Count(inventory, 5);
        TNumber = Count(inventory, 6);
        ZNumber = Count(inventory, 7);
        stage_Floor = floor;
        stage_Wall = wall;
        stage_FrontWall = frontWall;
    }

    /// <summary>
    /// Provides counts for all registered block IDs while retaining writes through the
    /// seven original public fields as a supported compatibility path.
    /// </summary>
    public Dictionary<int, int> CreateInventorySnapshot()
    {
        var snapshot = new Dictionary<int, int>(selectedInventory);
        snapshot[1] = A1Number;
        snapshot[2] = A2Number;
        snapshot[3] = INumber;
        snapshot[4] = ONumber;
        snapshot[5] = PNumber;
        snapshot[6] = TNumber;
        snapshot[7] = ZNumber;
        return snapshot;
    }

    /// <summary>
    /// Resolves a validated definition without changing the active session. Coordinators
    /// can validate their prefab/UI bindings against this snapshot before clearing a board.
    /// </summary>
    public bool TryGetStageLayout(int stageNumber, out StageLayout selected)
    {
        if (AdditionalStages == null)
            throw new InvalidOperationException("AdditionalStages must not be null.");

        var layouts = new Dictionary<int, StageLayout>();
        for (int index = 0; index < AdditionalStages.Length; index++)
        {
            StageDefinition definition = AdditionalStages[index];
            if (definition == null)
                throw new InvalidOperationException("AdditionalStages contains a missing stage definition at index " + index + ".");

            StageLayout layout;
            try
            {
                layout = definition.CreateLayout(GridCellCount);
            }
            catch (ArgumentException error)
            {
                throw new InvalidOperationException("Invalid stage definition at AdditionalStages index " + index + ".", error);
            }
            if (layouts.ContainsKey(layout.StageNumber))
                throw new InvalidOperationException("AdditionalStages contains a duplicate stage ID: " + layout.StageNumber);
            layouts.Add(layout.StageNumber, layout);
        }

        // Explicit assets take precedence over the compatibility fallback. Validate the
        // entire registry first, including unselected assets, so bad data is not hidden.
        if (layouts.TryGetValue(stageNumber, out selected))
            return true;
        if (stageNumber == 101)
        {
            selected = BuiltInStages.Create101();
            return true;
        }
        selected = null;
        return false;
    }

    private static int Count(IDictionary<int, int> inventory, int blockId)
    {
        int value;
        return inventory.TryGetValue(blockId, out value) ? value : 0;
    }
}
