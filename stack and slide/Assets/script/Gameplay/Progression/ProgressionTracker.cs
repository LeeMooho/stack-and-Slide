using System;

/// <summary>
/// Owns attempt scoring, duplicate suppression, persistent personal bests, and achievements.
/// The caller supplies monotonic time and authoritative successful-spawn totals; this domain
/// never reads Unity clocks or mistakes a spawned piece for a physically placed piece.
/// </summary>
public sealed class ProgressionTracker
{
    private readonly IProgressStore store;
    private ProgressProfile profile;
    private long greatestSessionId;
    private long sessionId;
    private int stageId;
    private bool active;
    private bool completed;
    private double startedAt;
    private double lastObservedAt;
    private double completedElapsed;
    private int checks;
    private int blocksUsed;
    private int score;
    private int stars;

    public bool LastLoadSucceeded { get; private set; }
    public bool LastSaveSucceeded { get; private set; }
    public ProgressProfile Profile { get { return profile.Copy(); } }

    public ProgressionTracker(IProgressStore store = null)
    {
        this.store = store;
        profile = new ProgressProfile();
        LastLoadSucceeded = true;
        LastSaveSucceeded = true;
        if (store == null) return;
        try
        {
            ProgressProfile loaded;
            LastLoadSucceeded = ProgressProfileCodec.TryDeserialize(store.Load(), out loaded);
            if (LastLoadSucceeded) profile = loaded;
        }
        catch (Exception)
        {
            // Storage is an optional boundary. A broken disk/PlayerPrefs adapter must not
            // prevent a playable session; the UI can surface the status without credentials.
            LastLoadSucceeded = false;
        }
    }

    public RunResult StartRun(long newSessionId, int newStageId, double nowSeconds)
    {
        if (newSessionId <= greatestSessionId) throw new ArgumentOutOfRangeException(nameof(newSessionId), "Session IDs must increase.");
        if (newStageId <= 0) throw new ArgumentOutOfRangeException(nameof(newStageId));
        ValidateClock(nowSeconds);
        greatestSessionId = newSessionId;
        sessionId = newSessionId;
        stageId = newStageId;
        startedAt = lastObservedAt = nowSeconds;
        completedElapsed = 0d;
        checks = blocksUsed = score = stars = 0;
        active = true;
        completed = false;
        return Snapshot(nowSeconds, false, null);
    }

    public bool RecordSpawn(long expectedSessionId, int expectedStageId, out string[] newlyUnlocked)
    {
        newlyUnlocked = new string[0];
        if (!Matches(expectedSessionId, expectedStageId) || completed || blocksUsed >= ProgressProfile.MaxCounter) return false;
        return SyncSpawns(expectedSessionId, expectedStageId, blocksUsed + 1, out newlyUnlocked);
    }

    /// <summary>
    /// Applies only the positive difference from a run's authoritative total. Repeated or
    /// out-of-order notifications cannot count one successful spawn more than once.
    /// </summary>
    public bool SyncSpawns(long expectedSessionId, int expectedStageId, int totalBlocksUsed, out string[] newlyUnlocked)
    {
        newlyUnlocked = new string[0];
        if (!Matches(expectedSessionId, expectedStageId)) return false;
        if (totalBlocksUsed < 0 || totalBlocksUsed > ProgressProfile.MaxCounter)
            throw new ArgumentOutOfRangeException(nameof(totalBlocksUsed));
        if (totalBlocksUsed <= blocksUsed) return true;
        if (completed) return false;
        int delta = totalBlocksUsed - blocksUsed;
        blocksUsed = totalBlocksUsed;
        newlyUnlocked = profile.AddSuccessfulSpawns(delta);
        Persist();
        return true;
    }

    /// <summary>
    /// Accepts matching attempts only. The first solved evaluation freezes run metrics and
    /// updates bests; repeats return a non-awarding snapshot without incrementing checks.
    /// </summary>
    public bool TryEvaluate(long expectedSessionId, int expectedStageId, bool solved, double nowSeconds, out RunResult result)
    {
        result = null;
        if (!Matches(expectedSessionId, expectedStageId)) return false;
        ValidateClock(nowSeconds);
        lastObservedAt = nowSeconds;
        if (completed)
        {
            result = Snapshot(nowSeconds, false, null);
            return true;
        }

        if (checks < ProgressProfile.MaxCounter) checks++;
        if (!solved)
        {
            result = Snapshot(nowSeconds, false, null);
            return true;
        }

        double elapsed = nowSeconds - startedAt;
        int runScore = (int)Math.Max(100d, 1000d - Math.Ceiling(elapsed) * 2d - 100d * (checks - 1d));
        int runStars = checks == 1 && elapsed <= 60d ? 3 : checks <= 3 && elapsed <= 180d ? 2 : 1;
        string[] newly = profile.RecordClear(stageId, runScore, elapsed, runStars, checks);
        score = runScore;
        stars = runStars;
        completedElapsed = elapsed;
        completed = true;
        Persist();
        result = Snapshot(nowSeconds, true, newly);
        return true;
    }

    public RunResult GetRun(double nowSeconds)
    {
        if (!active) return null;
        ValidateClock(nowSeconds);
        lastObservedAt = nowSeconds;
        return Snapshot(nowSeconds, false, null);
    }

    public bool EndRun(long expectedSessionId, int expectedStageId)
    {
        if (!Matches(expectedSessionId, expectedStageId)) return false;
        active = false;
        return true;
    }

    /// <summary>
    /// Retries the complete in-memory snapshot after a failed save, for example when the
    /// application pauses. Accepted milestones already save immediately, so a clean profile
    /// needs no duplicate storage write merely because the HUD or platform asks to flush.
    /// </summary>
    public bool Flush()
    {
        if (!LastSaveSucceeded) Persist();
        return LastSaveSucceeded;
    }

    private bool Matches(long expectedSessionId, int expectedStageId)
    {
        return active && sessionId == expectedSessionId && stageId == expectedStageId;
    }

    private void ValidateClock(double nowSeconds)
    {
        if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds) || nowSeconds < lastObservedAt
            || nowSeconds < 0d || nowSeconds > ProgressProfile.MaxSeconds)
            throw new ArgumentOutOfRangeException(nameof(nowSeconds), "Clock values must be finite and monotonic.");
    }

    private RunResult Snapshot(double nowSeconds, bool newlyCompleted, string[] newlyUnlocked)
    {
        return new RunResult(sessionId, stageId, completed ? completedElapsed : nowSeconds - startedAt,
            checks, blocksUsed, score, stars, completed, newlyCompleted, newlyUnlocked);
    }

    private void Persist()
    {
        if (store == null) return;
        try
        {
            store.Save(ProgressProfileCodec.Serialize(profile));
            LastSaveSucceeded = true;
        }
        catch (Exception)
        {
            // Retain accepted progress in memory if the platform cannot currently save it.
            // A later successful event will retry persistence of the whole latest snapshot.
            LastSaveSucceeded = false;
        }
    }
}
