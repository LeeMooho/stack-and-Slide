using UnityEngine;

/// <summary>
/// Stores the selected puzzle's available pieces and three 8-by-8 projection patterns.
/// Public names remain compatible with the existing scene and its Inspector bindings.
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

    // Each pattern follows the cell order of its corresponding Inspector array in stage.
    // A value of 1 requires projected block occupancy; 0 requires an empty projection.
    public int[] stage_Floor;
    public int[] stage_Wall;
    public int[] stage_FrontWall;

    /// <summary>Clears inventory and creates fresh empty pattern arrays for a new session.</summary>
    public void ResetStage()
    {
        A1Number = 0;
        A2Number = 0;
        INumber = 0;
        ONumber = 0;
        PNumber = 0;
        TNumber = 0;
        ZNumber = 0;

        // Replace the arrays rather than clearing shared instances. The stage controller
        // must call CallVari afterwards to bind the patterns belonging to this new session.
        stage_Floor = new int[GridCellCount];
        stage_Wall = new int[GridCellCount];
        stage_FrontWall = new int[GridCellCount];
    }

    /// <summary>
    /// Loads puzzle 101, whose numeric identifier is already wired to scene buttons.
    /// Rows are written explicitly so the authored silhouettes remain easy to inspect.
    /// </summary>
    public void stage101()
    {
        A1Number = 1;
        A2Number = 1;
        INumber = 1;
        ONumber = 0;
        PNumber = 1;
        TNumber = 1;
        ZNumber = 1;

        stage_Floor = new int[GridCellCount]
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 1, 1, 1, 1, 0, 0,
            0, 0, 1, 1, 1, 1, 0, 0,
            0, 0, 1, 1, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
        };

        stage_Wall = new int[GridCellCount]
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 1, 0, 0, 0, 0,
            0, 0, 0, 1, 1, 0, 0, 0,
            0, 0, 0, 1, 1, 0, 0, 0,
            0, 0, 1, 1, 1, 0, 0, 0,
        };

        stage_FrontWall = new int[GridCellCount]
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 1, 0, 0, 0, 0, 0,
            0, 0, 1, 0, 0, 1, 0, 0,
            0, 0, 1, 1, 1, 1, 0, 0,
            0, 0, 1, 1, 1, 1, 0, 0,
        };
    }
}
