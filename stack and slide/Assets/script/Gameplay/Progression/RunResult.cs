/// <summary>
/// Immutable run/evaluation snapshot for HUDs and effects. NewlyCompleted and NewlyUnlocked
/// describe only the evaluation that produced this result, not an award to repeat on repaint.
/// </summary>
public sealed class RunResult
{
    private readonly string[] newlyUnlocked;

    public long SessionId { get; }
    public int StageId { get; }
    public double ElapsedSeconds { get; }
    public int Checks { get; }
    public int BlocksUsed { get; }
    public int Score { get; }
    public int Stars { get; }
    public bool IsCompleted { get; }
    public bool NewlyCompleted { get; }
    public string[] NewlyUnlocked { get { return (string[])newlyUnlocked.Clone(); } }

    internal RunResult(long sessionId, int stageId, double elapsedSeconds, int checks,
        int blocksUsed, int score, int stars, bool isCompleted, bool newlyCompleted,
        string[] unlocked)
    {
        SessionId = sessionId;
        StageId = stageId;
        ElapsedSeconds = elapsedSeconds;
        Checks = checks;
        BlocksUsed = blocksUsed;
        Score = score;
        Stars = stars;
        IsCompleted = isCompleted;
        NewlyCompleted = newlyCompleted;
        newlyUnlocked = unlocked == null ? new string[0] : (string[])unlocked.Clone();
    }
}
