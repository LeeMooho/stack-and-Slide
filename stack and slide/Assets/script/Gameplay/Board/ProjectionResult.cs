using System;

/// <summary>
/// Immutable evaluation data for UI, scoring or progression listeners.
/// Consumers can inspect each surface without parsing the coordinator's log text.
/// </summary>
public sealed class ProjectionResult
{
    public int FloorMismatches { get; }
    public int WallMismatches { get; }
    public int FrontWallMismatches { get; }
    public int TotalMismatches { get; }
    public bool IsSolved { get { return TotalMismatches == 0; } }

    public ProjectionResult(int floorMismatches, int wallMismatches, int frontWallMismatches)
    {
        if (floorMismatches < 0)
        {
            throw new ArgumentOutOfRangeException("floorMismatches");
        }
        if (wallMismatches < 0)
        {
            throw new ArgumentOutOfRangeException("wallMismatches");
        }
        if (frontWallMismatches < 0)
        {
            throw new ArgumentOutOfRangeException("frontWallMismatches");
        }

        FloorMismatches = floorMismatches;
        WallMismatches = wallMismatches;
        FrontWallMismatches = frontWallMismatches;
        TotalMismatches = checked(floorMismatches + wallMismatches + frontWallMismatches);
    }
}
