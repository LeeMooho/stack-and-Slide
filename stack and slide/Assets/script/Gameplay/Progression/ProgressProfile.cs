using System;
using System.Collections.Generic;

/// <summary>
/// Persistent bests and unlocked achievement IDs. Public reads return immutable records
/// or independent arrays; only the tracker can apply accepted session events.
/// </summary>
public sealed class ProgressProfile
{
    // Bounds keep malformed saves from allocating unlimited records or overflowing counters.
    // The generous clock bound also covers long-running editor sessions without infinities.
    internal const int MaxStages = 4096;
    internal const int MaxCounter = 1000000000;
    internal const double MaxSeconds = 1000000000000d;
    private static readonly HashSet<string> KnownAchievements = new HashSet<string>(StringComparer.Ordinal)
    {
        "first_block", "first_clear", "first_try", "swift_clear", "three_stages", "builder"
    };

    private readonly Dictionary<int, StageBest> bests;
    private readonly HashSet<string> unlocked;
    public int LifetimeBlocksUsed { get; private set; }
    public int CompletedStages { get { return bests.Count; } }

    /// <summary>
    /// XP is the sum of per-stage best scores, not every clear reward. Replaying an equal
    /// or worse result therefore cannot farm XP; improving a best adds only its difference.
    /// </summary>
    public int TotalXp
    {
        get
        {
            int total = 0;
            foreach (StageBest best in bests.Values) total += best.BestScore;
            return total;
        }
    }

    public ProgressProfile()
        : this(0, new Dictionary<int, StageBest>(), new string[0]) { }

    internal ProgressProfile(int lifetimeBlocksUsed, IDictionary<int, StageBest> records, IEnumerable<string> achievements)
    {
        if (lifetimeBlocksUsed < 0 || lifetimeBlocksUsed > MaxCounter)
            throw new ArgumentOutOfRangeException(nameof(lifetimeBlocksUsed));
        if (records == null) throw new ArgumentNullException(nameof(records));
        if (records.Count > MaxStages) throw new ArgumentException("Too many stage records.", nameof(records));
        bests = new Dictionary<int, StageBest>();
        foreach (KeyValuePair<int, StageBest> entry in records)
        {
            if (entry.Value == null || entry.Key != entry.Value.StageId)
                throw new ArgumentException("Invalid stage record.", nameof(records));
            bests.Add(entry.Key, entry.Value);
        }
        unlocked = new HashSet<string>(StringComparer.Ordinal);
        if (achievements == null) throw new ArgumentNullException(nameof(achievements));
        foreach (string achievement in achievements)
        {
            if (!IsKnownAchievement(achievement) || !unlocked.Add(achievement))
                throw new ArgumentException("Invalid or duplicate achievement.", nameof(achievements));
        }
        LifetimeBlocksUsed = lifetimeBlocksUsed;
    }

    public StageBest GetBest(int stageId)
    {
        StageBest best;
        return bests.TryGetValue(stageId, out best) ? best : null;
    }

    public bool IsUnlocked(string achievementId)
    {
        return achievementId != null && unlocked.Contains(achievementId);
    }

    public StageBest[] GetStageBests()
    {
        var result = new List<StageBest>(bests.Values);
        result.Sort((a, b) => a.StageId.CompareTo(b.StageId));
        return result.ToArray();
    }

    public string[] GetUnlockedAchievements()
    {
        var result = new List<string>(unlocked);
        result.Sort(StringComparer.Ordinal);
        return result.ToArray();
    }

    internal ProgressProfile Copy()
    {
        return new ProgressProfile(LifetimeBlocksUsed, bests, GetUnlockedAchievements());
    }

    internal static bool IsKnownAchievement(string id)
    {
        return id != null && KnownAchievements.Contains(id);
    }

    internal string[] AddSuccessfulSpawns(int delta)
    {
        var newly = new List<string>();
        LifetimeBlocksUsed = (int)Math.Min(MaxCounter, (long)LifetimeBlocksUsed + delta);
        if (LifetimeBlocksUsed > 0) Unlock("first_block", newly);
        if (LifetimeBlocksUsed >= 10) Unlock("builder", newly);
        return newly.ToArray();
    }

    internal string[] RecordClear(int stageId, int score, double elapsed, int stars, int checks)
    {
        StageBest previous = GetBest(stageId);
        if (previous == null && bests.Count >= MaxStages)
            throw new InvalidOperationException("The progress profile reached its stage-record limit.");
        bests[stageId] = new StageBest(stageId,
            previous == null ? score : Math.Max(previous.BestScore, score),
            previous == null ? elapsed : Math.Min(previous.BestTimeSeconds, elapsed),
            previous == null ? stars : Math.Max(previous.BestStars, stars));

        var newly = new List<string>();
        Unlock("first_clear", newly);
        if (checks == 1) Unlock("first_try", newly);
        if (elapsed <= 60d) Unlock("swift_clear", newly);
        if (bests.Count >= 3) Unlock("three_stages", newly);
        return newly.ToArray();
    }

    private void Unlock(string id, List<string> newly)
    {
        if (unlocked.Add(id)) newly.Add(id);
    }
}
