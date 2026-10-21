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
    // Stage numbers can repeat on replay. Session context distinguishes two evaluations
    // of the same puzzle when progression or UI listeners start another attempt.
    public int SessionId { get; }
    // Capture the stage coordinator's count instead of depending on which spawn
    // notification a scoring listener happened to receive before this evaluation.
    public int BlocksSpawned { get; }
    public ProjectionSurfaceResult FloorResults { get; }
    public ProjectionSurfaceResult WallResults { get; }
    public ProjectionSurfaceResult FrontWallResults { get; }
    public int FloorMismatches { get; }
    public int WallMismatches { get; }
    public int FrontWallMismatches { get; }
    public int TotalMismatches { get; }
    public bool IsSolved { get { return TotalMismatches == 0; } }

    public ProjectionResult(int floorMismatches, int wallMismatches, int frontWallMismatches,
        int stageNumber = 0)
        : this(floorMismatches, wallMismatches, frontWallMismatches, stageNumber, 0, 0)
    {
        // Retain the original four-parameter constructor signature as well as source
        // compatibility for consumers that do not need detailed session context.
    }

    public ProjectionResult(int floorMismatches, int wallMismatches, int frontWallMismatches,
        int stageNumber, int sessionId, int blocksSpawned = 0)
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
        if (sessionId < 0)
        {
            throw new ArgumentOutOfRangeException("sessionId");
        }
        if (blocksSpawned < 0)
        {
            throw new ArgumentOutOfRangeException("blocksSpawned");
        }

        StageNumber = stageNumber;
        SessionId = sessionId;
        BlocksSpawned = blocksSpawned;
        FloorMismatches = floorMismatches;
        WallMismatches = wallMismatches;
        FrontWallMismatches = frontWallMismatches;
        TotalMismatches = checked(floorMismatches + wallMismatches + frontWallMismatches);
    }

    /// <summary>
    /// Keeps the three surface snapshots behind the same aggregate count contract.
    /// Surface dimensions may differ on future boards; each grid validates its own size.
    /// </summary>
    public ProjectionResult(ProjectionSurfaceResult floor, ProjectionSurfaceResult wall,
        ProjectionSurfaceResult frontWall, int stageNumber = 0, int sessionId = 0, int blocksSpawned = 0)
        : this(RequireSurface(floor, "floor").MismatchCount, RequireSurface(wall, "wall").MismatchCount,
            RequireSurface(frontWall, "frontWall").MismatchCount, stageNumber, sessionId, blocksSpawned)
    {
        FloorResults = floor;
        WallResults = wall;
        FrontWallResults = frontWall;
    }

    private static ProjectionSurfaceResult RequireSurface(ProjectionSurfaceResult result, string name)
    {
        if (result == null)
        {
            throw new ArgumentNullException(name);
        }
        return result;
    }
}
