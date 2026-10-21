using System.Collections.Generic;

/// <summary>
/// Supplies the original puzzle and two geometry-checked introductory challenges.
/// Further content can use StageDefinition assets without changing this fallback catalog.
/// </summary>
public static class BuiltInStages
{
    public static StageLayout Create101()
    {
        return new StageLayout(101, new Dictionary<int, int>
        {
            { 1, 1 }, { 2, 1 }, { 3, 1 }, { 4, 0 }, { 5, 1 }, { 6, 1 }, { 7, 1 }
        },
        new int[]
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 1, 1, 1, 1, 0, 0,
            0, 0, 1, 1, 1, 1, 0, 0,
            0, 0, 1, 1, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
        },
        new int[]
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 1, 0, 0, 0, 0,
            0, 0, 0, 1, 1, 0, 0, 0,
            0, 0, 0, 1, 1, 0, 0, 0,
            0, 0, 1, 1, 1, 0, 0, 0,
        },
        new int[]
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 1, 0, 0, 0, 0, 0,
            0, 0, 1, 0, 0, 1, 0, 0,
            0, 0, 1, 1, 1, 1, 0, 0,
            0, 0, 1, 1, 1, 1, 0, 0,
        });
    }

    /// <summary>
    /// Introduces the I piece with a single flat placement. The masks come from the
    /// authored prefab colliders and scene grids, verified in test_challenge_layouts.py.
    /// </summary>
    public static StageLayout Create102()
    {
        // Known solution: I root at (3.5, 1, 3), with its original orientation.
        return CreateChallenge(102, 3,
            new[] { 19, 27, 35, 43 }, new[] { 58, 59, 60, 61 }, new[] { 59 });
    }

    /// <summary>
    /// Introduces a quarter-turn: the O piece lies flat after rotating around X.
    /// See docs/challenge-layouts.md for the exact pose and half-cell controls.
    /// </summary>
    public static StageLayout Create103()
    {
        // Known solution: O root at (3.5, 1, 3.5), rotated +90 degrees around X.
        return CreateChallenge(103, 4,
            new[] { 27, 28, 35, 36 }, new[] { 59, 60 }, new[] { 59, 60 });
    }

    private static StageLayout CreateChallenge(int stageNumber, int blockId,
        int[] floorCells, int[] wallCells, int[] frontWallCells)
    {
        // Only the required piece has stock. StageManager mirrors missing legacy IDs
        // as zero, so challenge content remains independent of inventory UI bindings.
        return new StageLayout(stageNumber, new Dictionary<int, int> { { blockId, 1 } },
            Pattern(floorCells), Pattern(wallCells), Pattern(frontWallCells));
    }

    private static int[] Pattern(int[] occupiedCells)
    {
        var cells = new int[64];
        foreach (int index in occupiedCells)
            cells[index] = 1;
        return cells;
    }
}
