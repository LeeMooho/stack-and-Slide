using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One Inspector-authored inventory entry, keyed by a stable block selection ID.</summary>
[Serializable]
public sealed class BlockStock
{
    public int BlockId;
    public int Count;
}

/// <summary>
/// Authorable puzzle data. Register assets on StageManager instead of adding another
/// switch case or copying the gameplay controller for each new stage.
/// </summary>
[CreateAssetMenu(menuName = "Stack and Slide/Stage Definition", fileName = "StageDefinition")]
public sealed class StageDefinition : ScriptableObject
{
    public int StageNumber;
    public BlockStock[] Inventory = new BlockStock[0];

    // New assets start with the current 8-by-8 board size. The validation boundary
    // accepts another size only when the scene explicitly requests that cell count.
    public int[] Floor = new int[64];
    public int[] Wall = new int[64];
    public int[] FrontWall = new int[64];

    /// <summary>
    /// Validates Inspector data and returns an immutable snapshot. Later edits to this
    /// asset do not silently mutate the layout already used by another play session.
    /// </summary>
    public StageLayout CreateLayout(int expectedCellCount)
    {
        if (expectedCellCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedCellCount));
        if (Inventory == null)
            throw new ArgumentNullException(nameof(Inventory));

        var inventory = new Dictionary<int, int>();
        foreach (BlockStock stock in Inventory)
        {
            if (stock == null)
                throw new ArgumentException("Inventory contains a missing stock entry.", nameof(Inventory));
            if (inventory.ContainsKey(stock.BlockId))
                throw new ArgumentException("Inventory contains a duplicate block ID: " + stock.BlockId, nameof(Inventory));
            inventory.Add(stock.BlockId, stock.Count);
        }

        var layout = new StageLayout(StageNumber, inventory, Floor, Wall, FrontWall);
        if (layout.CellCount != expectedCellCount)
            throw new ArgumentException("The stage's projection cell count does not match this board.");
        return layout;
    }
}
