using System;
using System.Collections.Generic;

/// <summary>
/// Validated puzzle data without any Unity scene or asset dependencies.
/// The definition is immutable: each caller receives its own mutable session copies.
/// </summary>
public sealed class StageLayout
{
    private readonly Dictionary<int, int> initialInventory;
    private readonly int[] floor;
    private readonly int[] wall;
    private readonly int[] frontWall;

    public int StageNumber { get; private set; }
    public int CellCount { get; private set; }

    public StageLayout(int stageNumber, IDictionary<int, int> inventory,
        int[] floor, int[] wall, int[] frontWall)
    {
        if (stageNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(stageNumber), "Stage IDs must be positive.");
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));
        if (floor == null)
            throw new ArgumentNullException(nameof(floor));
        if (floor.Length == 0)
            throw new ArgumentException("A stage must contain at least one projection cell.", nameof(floor));

        // Never retain the author's mutable arrays or dictionary. A play session may
        // consume inventory or edit its grid without changing the reusable definition.
        initialInventory = new Dictionary<int, int>();
        foreach (KeyValuePair<int, int> stock in inventory)
        {
            if (stock.Key <= 0)
                throw new ArgumentOutOfRangeException(nameof(inventory), "Block IDs must be positive.");
            if (stock.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(inventory), "Block counts cannot be negative.");
            initialInventory.Add(stock.Key, stock.Value);
        }

        StageNumber = stageNumber;
        CellCount = floor.Length;
        this.floor = CopyPattern(floor, CellCount, nameof(floor));
        this.wall = CopyPattern(wall, CellCount, nameof(wall));
        this.frontWall = CopyPattern(frontWall, CellCount, nameof(frontWall));
    }

    /// <summary>Creates independent counts for one play session, including future block IDs.</summary>
    public Dictionary<int, int> GetInitialInventory()
    {
        return new Dictionary<int, int>(initialInventory);
    }

    public int[] CopyFloor() { return (int[])floor.Clone(); }
    public int[] CopyWall() { return (int[])wall.Clone(); }
    public int[] CopyFrontWall() { return (int[])frontWall.Clone(); }

    private static int[] CopyPattern(int[] pattern, int expectedLength, string argument)
    {
        if (pattern == null)
            throw new ArgumentNullException(argument);
        if (pattern.Length != expectedLength)
            throw new ArgumentException("All three projection patterns must have the same cell count.", argument);
        foreach (int cell in pattern)
        {
            if (cell != 0 && cell != 1)
                throw new ArgumentException("Projection cells must be either 0 or 1.", argument);
        }
        return (int[])pattern.Clone();
    }
}
