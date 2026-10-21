using System;

/// <summary>
/// Independent bests for one stage. Score, time, and stars may come from different runs;
/// this record does not pretend that every personal best was earned in the same attempt.
/// </summary>
public sealed class StageBest
{
    public int StageId { get; }
    public int BestScore { get; }
    public double BestTimeSeconds { get; }
    public int BestStars { get; }

    public StageBest(int stageId, int bestScore, double bestTimeSeconds, int bestStars)
    {
        if (stageId <= 0) throw new ArgumentOutOfRangeException(nameof(stageId));
        if (bestScore < 100 || bestScore > 1000) throw new ArgumentOutOfRangeException(nameof(bestScore));
        if (double.IsNaN(bestTimeSeconds) || double.IsInfinity(bestTimeSeconds)
            || bestTimeSeconds < 0 || bestTimeSeconds > ProgressProfile.MaxSeconds)
            throw new ArgumentOutOfRangeException(nameof(bestTimeSeconds));
        if (bestStars < 1 || bestStars > 3) throw new ArgumentOutOfRangeException(nameof(bestStars));
        StageId = stageId;
        BestScore = bestScore;
        BestTimeSeconds = bestTimeSeconds;
        BestStars = bestStars;
    }
}
