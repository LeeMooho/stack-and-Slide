using System;

/// <summary>
/// Describes a completed, debited spawn rather than an Instantiate request. The
/// cumulative count lets listeners reconcile events delivered after another callback.
/// </summary>
public sealed class BlockSpawnInfo
{
    public int SessionId { get; }
    public int StageNumber { get; }
    public int BlockId { get; }
    public int BlocksSpawned { get; }

    public BlockSpawnInfo(int sessionId, int stageNumber, int blockId, int blocksSpawned)
    {
        if (sessionId <= 0 || stageNumber <= 0 || blockId <= 0 || blocksSpawned <= 0)
            throw new ArgumentOutOfRangeException(nameof(sessionId), "A committed spawn needs positive identifiers and count.");
        SessionId = sessionId;
        StageNumber = stageNumber;
        BlockId = blockId;
        BlocksSpawned = blocksSpawned;
    }
}
