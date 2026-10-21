using System;

/// <summary>
/// Immutable evaluation data for UI, scoring or progression listeners.
/// Consumers can inspect each surface without parsing the coordinator's log text.
/// </summary>
public sealed class ProjectionResult
{
    // Keep the evaluated stage in the payload: another result listener may already
    // have selected a new stage before a later scoring or progression listener runs.
    // Zero identifies standalone evaluations with no associated stage.
    public int StageNumber { get; }
    public int FloorMismatches { get; }
    public int WallMismatches { get; }
    public int FrontWallMismatches { get; }
    public int TotalMismatches { get; }
    public bool IsSolved { get { return TotalMismatches == 0; } }

    public ProjectionResult(int floorMismatches, int wallMismatches, int frontWallMismatches, int stageNumber = 0)
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
        if (stageNumber < 0)
        {
            throw new ArgumentOutOfRangeException("stageNumber");
        }

        StageNumber = stageNumber;
        FloorMismatches = floorMismatches;
        WallMismatches = wallMismatches;
        FrontWallMismatches = frontWallMismatches;
        TotalMismatches = checked(floorMismatches + wallMismatches + frontWallMismatches);
    }
}
