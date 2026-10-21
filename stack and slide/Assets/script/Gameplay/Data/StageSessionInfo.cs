using System;

/// <summary>
/// Immutable context for a committed session. A new attempt has a new ID even when
/// the player retries the same puzzle, so delayed effects cannot target that retry.
/// Stage number zero represents the menu rather than a playable puzzle.
/// </summary>
public sealed class StageSessionInfo
{
    public int SessionId { get; }
    public int StageNumber { get; }
    public int InitialBlockCount { get; }
    public int BlocksSpawned { get; }
    public double StartedAtSeconds { get; }
    public bool IsPlaying { get { return StageNumber > 0; } }

    public StageSessionInfo(int sessionId, int stageNumber, int initialBlockCount,
        int blocksSpawned, double startedAtSeconds)
    {
        if (sessionId < 0 || stageNumber < 0 || initialBlockCount < 0 || blocksSpawned < 0)
            throw new ArgumentOutOfRangeException(nameof(sessionId), "Session values cannot be negative.");
        if (double.IsNaN(startedAtSeconds) || double.IsInfinity(startedAtSeconds) || startedAtSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(startedAtSeconds));
        SessionId = sessionId;
        StageNumber = stageNumber;
        InitialBlockCount = initialBlockCount;
        BlocksSpawned = blocksSpawned;
        StartedAtSeconds = startedAtSeconds;
    }
}
