using System;
using System.Globalization;

internal static class ProgressionTests
{
    public static void Register(Action<string, Action> run)
    {
        run("Progression scores elapsed seconds and failed checks", ScoreAndCheckPenalty);
        run("Progression awards stars at the documented time/check boundaries", StarBoundaries);
        run("Progression scores have an attainable maximum and a positive floor", ScoreBounds);
        run("A solved attempt freezes metrics and cannot award twice", CompletionIsIdempotent);
        run("Replaying a stage earns only improvements to aggregate best XP", PersonalBests);
        run("Three-stage achievement requires three distinct stages", DistinctStages);
        run("Successful spawn totals unlock first block and builder once", SpawnAchievements);
        run("Out-of-order authoritative spawn totals cannot double count", AuthoritativeSpawnTotals);
        run("Evaluation keeps the authoritative spawn total before delayed callbacks", EvaluationUsesSyncedTotal);
        run("A new attempt accepts reused stage IDs while rejecting stale callbacks", SessionIsolation);
        run("Ending an attempt rejects late events without affecting its successor", EndingRuns);
        run("Invalid clocks and identities leave the current run usable", InvalidInputsAreTransactional);
        run("Progress snapshots and achievement arrays do not expose mutable state", SnapshotIsolation);
        run("Versioned progress round trips deterministically across locales", CodecRoundTrip);
        run("Progress codec rejects malformed saves without partial awards", CodecRejectsCorruption);
        run("Progress codec accepts empty storage and Windows line endings", CodecEmptyAndWindowsLines);
        run("Malformed and unavailable storage start a playable empty profile", LoadFailures);
        run("Save failures preserve progress and later saves retry the latest snapshot", SaveFailures);
        run("Application pause can flush a failed save without duplicating successful writes", FlushRetriesFailures);
        run("Loaded progress preserves bests and suppresses existing achievement rewards", LoadedProgress);
        run("Lifetime spawn totals saturate safely at their persistence bound", LifetimeCounterBound);
    }

    private sealed class MemoryStore : IProgressStore
    {
        public string Data;
        public int SaveCalls;
        public bool FailLoad;
        public bool FailSave;

        public string Load()
        {
            if (FailLoad) throw new InvalidOperationException("Storage unavailable.");
            return Data;
        }

        public void Save(string value)
        {
            SaveCalls++;
            if (FailSave) throw new InvalidOperationException("Storage unavailable.");
            Data = value;
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static void Contains(string expected, string[] values)
    {
        if (Array.IndexOf(values, expected) < 0) throw new Exception("Missing achievement " + expected);
    }

    private static RunResult Evaluate(ProgressionTracker tracker, long sessionId, int stageId, bool solved, double now)
    {
        RunResult result;
        Equal(true, tracker.TryEvaluate(sessionId, stageId, solved, now, out result));
        return result;
    }

    private static RunResult Finish(int checks, double elapsed)
    {
        var tracker = new ProgressionTracker();
        tracker.StartRun(1, 1, 0d);
        for (int i = 1; i < checks; i++) Evaluate(tracker, 1, 1, false, 0d);
        return Evaluate(tracker, 1, 1, true, elapsed);
    }

    private static void ScoreAndCheckPenalty()
    {
        var tracker = new ProgressionTracker();
        tracker.StartRun(1, 7, 100d);
        var failed = Evaluate(tracker, 1, 7, false, 101d);
        Equal(1, failed.Checks);
        Equal(0, failed.Score);
        Equal(false, failed.IsCompleted);
        Equal(0, tracker.Profile.TotalXp);
        var cleared = Evaluate(tracker, 1, 7, true, 110.1d);
        Equal(878, cleared.Score); // Eleven rounded-up seconds and one unsuccessful check.
        Equal(2, cleared.Checks);
        Equal(2, cleared.Stars);
        Equal(true, cleared.NewlyCompleted);
        Equal(false, tracker.Profile.IsUnlocked("first_try"));
        Equal(true, tracker.Profile.IsUnlocked("swift_clear"));
    }

    private static void StarBoundaries()
    {
        Equal(3, Finish(1, 60d).Stars);
        Equal(2, Finish(1, 60.001d).Stars);
        Equal(2, Finish(3, 180d).Stars);
        Equal(1, Finish(3, 180.001d).Stars);
        Equal(1, Finish(4, 1d).Stars);
    }

    private static void ScoreBounds()
    {
        Equal(1000, Finish(1, 0d).Score);
        Equal(998, Finish(1, 0.001d).Score);
        Equal(100, Finish(1, 1000000d).Score);
        Equal(100, Finish(20, 0d).Score);
    }

    private static void CompletionIsIdempotent()
    {
        var store = new MemoryStore();
        var tracker = new ProgressionTracker(store);
        tracker.StartRun(1, 1, 0d);
        var first = Evaluate(tracker, 1, 1, true, 25d);
        Equal(3, first.NewlyUnlocked.Length);
        Contains("first_clear", first.NewlyUnlocked);
        Contains("first_try", first.NewlyUnlocked);
        Contains("swift_clear", first.NewlyUnlocked);
        foreach (bool solved in new[] { true, false, true })
        {
            var repeat = Evaluate(tracker, 1, 1, solved, 100d);
            Equal(true, repeat.IsCompleted);
            Equal(false, repeat.NewlyCompleted);
            Equal(0, repeat.NewlyUnlocked.Length);
            Equal(1, repeat.Checks);
            Equal(25d, repeat.ElapsedSeconds);
            Equal(first.Score, repeat.Score);
        }
        Equal(25d, tracker.GetRun(200d).ElapsedSeconds);
        Equal(1, store.SaveCalls);
        Equal(first.Score, tracker.Profile.TotalXp);
    }

    private static void PersonalBests()
    {
        var tracker = new ProgressionTracker();
        tracker.StartRun(1, 4, 0d);
        Evaluate(tracker, 1, 4, true, 90d); // Score 820, time 90, two stars.
        tracker.StartRun(2, 4, 100d);
        Evaluate(tracker, 2, 4, false, 100d);
        Evaluate(tracker, 2, 4, false, 100d);
        Evaluate(tracker, 2, 4, true, 130d); // Faster, but the score is lower.
        Equal(820, tracker.Profile.TotalXp);
        Equal(30d, tracker.Profile.GetBest(4).BestTimeSeconds);
        tracker.StartRun(3, 4, 200d);
        Evaluate(tracker, 3, 4, true, 250d);
        Equal(900, tracker.Profile.TotalXp);
        Equal(3, tracker.Profile.GetBest(4).BestStars);
        Equal(30d, tracker.Profile.GetBest(4).BestTimeSeconds);
        tracker.StartRun(4, 4, 300d);
        Evaluate(tracker, 4, 4, true, 395d);
        Equal(900, tracker.Profile.TotalXp);
        Equal(1, tracker.Profile.CompletedStages);
        tracker.StartRun(5, 8, 400d);
        Evaluate(tracker, 5, 8, true, 400d);
        Equal(1900, tracker.Profile.TotalXp);
    }

    private static void DistinctStages()
    {
        var tracker = new ProgressionTracker();
        for (int session = 1; session <= 3; session++)
        {
            tracker.StartRun(session, 1, session);
            Evaluate(tracker, session, 1, true, session);
        }
        Equal(false, tracker.Profile.IsUnlocked("three_stages"));
        tracker.StartRun(4, 2, 4d);
        Evaluate(tracker, 4, 2, true, 4d);
        Equal(false, tracker.Profile.IsUnlocked("three_stages"));
        tracker.StartRun(5, 8, 5d);
        var third = Evaluate(tracker, 5, 8, true, 5d);
        Contains("three_stages", third.NewlyUnlocked);
        Equal(3, tracker.Profile.CompletedStages);
    }

    private static void SpawnAchievements()
    {
        var tracker = new ProgressionTracker();
        tracker.StartRun(1, 1, 0d);
        string[] newly;
        Equal(true, tracker.RecordSpawn(1, 1, out newly));
        Equal(1, newly.Length);
        Contains("first_block", newly);
        Equal(0, tracker.Profile.CompletedStages);
        Equal(0, tracker.Profile.TotalXp);
        tracker.SyncSpawns(1, 1, 9, out newly);
        Equal(0, newly.Length);
        tracker.RecordSpawn(1, 1, out newly);
        Equal(1, newly.Length);
        Contains("builder", newly);
        tracker.RecordSpawn(1, 1, out newly);
        Equal(0, newly.Length);
        Equal(11, tracker.GetRun(0d).BlocksUsed);
        Equal(11, tracker.Profile.LifetimeBlocksUsed);
        Evaluate(tracker, 1, 1, true, 0d);
        Equal(false, tracker.RecordSpawn(1, 1, out newly));
        Equal(11, tracker.Profile.LifetimeBlocksUsed);
    }

    private static void AuthoritativeSpawnTotals()
    {
        var store = new MemoryStore();
        var tracker = new ProgressionTracker(store);
        tracker.StartRun(1, 1, 0d);
        string[] newly;
        foreach (int total in new[] { 2, 1, 2, 5, 4, 5 })
            Equal(true, tracker.SyncSpawns(1, 1, total, out newly));
        Equal(5, tracker.Profile.LifetimeBlocksUsed);
        Equal(5, tracker.GetRun(0d).BlocksUsed);
        Equal(2, store.SaveCalls);
        tracker.StartRun(2, 2, 0d);
        tracker.SyncSpawns(2, 2, 5, out newly);
        Contains("builder", newly);
        Equal(10, tracker.Profile.LifetimeBlocksUsed);
    }

    private static void EvaluationUsesSyncedTotal()
    {
        var tracker = new ProgressionTracker();
        tracker.StartRun(1, 1, 0d);
        string[] newly;
        tracker.SyncSpawns(1, 1, 6, out newly);
        var cleared = Evaluate(tracker, 1, 1, true, 10d);
        Equal(6, cleared.BlocksUsed);
        Equal(true, tracker.SyncSpawns(1, 1, 3, out newly));
        Equal(0, newly.Length);
        Equal(true, tracker.SyncSpawns(1, 1, 6, out newly));
        Equal(false, tracker.SyncSpawns(1, 1, 7, out newly));
        Equal(6, tracker.Profile.LifetimeBlocksUsed);
        Equal(6, tracker.GetRun(10d).BlocksUsed);
    }

    private static void SessionIsolation()
    {
        var tracker = new ProgressionTracker();
        tracker.StartRun(1, 4, 0d);
        tracker.StartRun(2, 4, 10d);
        string[] newly;
        RunResult result;
        // Stale messages are ignored before inspecting their payload or timestamp.
        Equal(false, tracker.SyncSpawns(1, 4, -1, out newly));
        Equal(false, tracker.TryEvaluate(1, 4, true, double.NaN, out result));
        Equal(null, result);
        Equal(false, tracker.RecordSpawn(2, 5, out newly));
        Equal(false, tracker.TryEvaluate(2, 5, true, 10d, out result));
        Equal(0, tracker.GetRun(10d).Checks);
        Equal(0, tracker.Profile.LifetimeBlocksUsed);
        Throws<ArgumentOutOfRangeException>(() => tracker.StartRun(2, 8, 10d));
        Equal(2L, Evaluate(tracker, 2, 4, true, 10d).SessionId);
    }

    private static void EndingRuns()
    {
        var tracker = new ProgressionTracker();
        tracker.StartRun(1, 1, 0d);
        Equal(false, tracker.EndRun(1, 2));
        Equal(true, tracker.EndRun(1, 1));
        Equal(false, tracker.EndRun(1, 1));
        Equal(null, tracker.GetRun(double.NaN));
        string[] newly;
        Equal(false, tracker.RecordSpawn(1, 1, out newly));
        tracker.StartRun(2, 1, 0d);
        Equal(false, tracker.EndRun(1, 1));
        Equal(true, tracker.RecordSpawn(2, 1, out newly));
    }

    private static void InvalidInputsAreTransactional()
    {
        var tracker = new ProgressionTracker();
        Throws<ArgumentOutOfRangeException>(() => tracker.StartRun(0, 1, 0d));
        Throws<ArgumentOutOfRangeException>(() => tracker.StartRun(1, 0, 0d));
        tracker.StartRun(1, 1, 10d);
        foreach (double invalid in new[] { 9d, -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1000000000001d })
        {
            RunResult ignored;
            Throws<ArgumentOutOfRangeException>(() => tracker.TryEvaluate(1, 1, true, invalid, out ignored));
            Throws<ArgumentOutOfRangeException>(() => tracker.GetRun(invalid));
            Throws<ArgumentOutOfRangeException>(() => tracker.StartRun(2, 1, invalid));
        }
        string[] newly;
        Throws<ArgumentOutOfRangeException>(() => tracker.SyncSpawns(1, 1, -1, out newly));
        Throws<ArgumentOutOfRangeException>(() => tracker.SyncSpawns(1, 1, 1000000001, out newly));
        Equal(0, tracker.GetRun(10d).Checks);
        Equal(0, tracker.Profile.TotalXp);
        tracker.StartRun(2, 8, 10d); // Invalid starts did not reserve the session token.
        Equal(1, Evaluate(tracker, 2, 8, true, 10d).Checks);
    }

    private static void SnapshotIsolation()
    {
        var tracker = new ProgressionTracker();
        tracker.StartRun(1, 1, 0d);
        var before = tracker.Profile;
        var cleared = Evaluate(tracker, 1, 1, true, 0d);
        var profile = tracker.Profile;
        var bests = profile.GetStageBests();
        var achievements = profile.GetUnlockedAchievements();
        var resultAchievements = cleared.NewlyUnlocked;
        bests[0] = null;
        achievements[0] = "fake";
        resultAchievements[0] = "fake";
        Equal(0, before.CompletedStages);
        Equal(1, profile.CompletedStages);
        Equal(1000, profile.GetStageBests()[0].BestScore);
        Equal(true, profile.IsUnlocked("first_clear"));
        Equal(false, profile.IsUnlocked("fake"));
        Contains("first_clear", cleared.NewlyUnlocked);
        tracker.StartRun(2, 2, 0d);
        Evaluate(tracker, 2, 2, true, 0d);
        Equal(1, profile.CompletedStages);
        Equal(2, tracker.Profile.CompletedStages);
    }

    private static void CodecRoundTrip()
    {
        var tracker = new ProgressionTracker();
        tracker.StartRun(1, 8, 0d);
        string[] newly;
        tracker.SyncSpawns(1, 8, 12, out newly);
        Evaluate(tracker, 1, 8, true, 1.25d);
        tracker.StartRun(2, 2, 2d);
        Evaluate(tracker, 2, 2, true, 2d);
        var originalCulture = CultureInfo.CurrentCulture;
        string text;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            text = ProgressProfileCodec.Serialize(tracker.Profile);
            Equal(true, text.Contains("stage|8|996|1.25|3"));
            ProgressProfile restored;
            Equal(true, ProgressProfileCodec.TryDeserialize(text, out restored));
            Equal(1996, restored.TotalXp);
            Equal(12, restored.LifetimeBlocksUsed);
            Equal(1.25d, restored.GetBest(8).BestTimeSeconds);
            Equal(text, ProgressProfileCodec.Serialize(restored));
            Equal(true, text.IndexOf("stage|2|", StringComparison.Ordinal) < text.IndexOf("stage|8|", StringComparison.Ordinal));
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    private static void CodecRejectsCorruption()
    {
        const string header = "STACK_AND_SLIDE_PROGRESS|1\n";
        string[] badSaves =
        {
            " ", "STACK_AND_SLIDE_PROGRESS|2\nblocks|0\n", header,
            header + "blocks|0\nblocks|1\n",
            header + "blocks|-1\n", header + "blocks|1000000001\n",
            header + "blocks|0\n\nstage|1|1000|0|3\n",
            header + "blocks|0\nstage|0|1000|0|3\n",
            header + "blocks|0\nstage|1|99|0|3\n",
            header + "blocks|0\nstage|1|1001|0|3\n",
            header + "blocks|0\nstage|1|1000|NaN|3\n",
            header + "blocks|0\nstage|1|1000|Infinity|3\n",
            header + "blocks|0\nstage|1|1000|-1|3\n",
            header + "blocks|0\nstage|1|1000|1000000000001|3\n",
            header + "blocks|0\nstage|1|1000|0|0\n",
            header + "blocks|0\nstage|1|1000|0|4\n",
            header + "blocks|0\nstage|1|1000|0|3\nstage|1|900|10|2\n",
            header + "blocks|0\nachievement|unknown\n",
            header + "blocks|0\nachievement|first_clear\nachievement|first_clear\n",
            header + "blocks|0\nunknown|value\n",
            header + "blocks|0\nstage|1|1000|1,25|3\n",
            new string('x', 1048577)
        };
        foreach (string save in badSaves)
        {
            ProgressProfile rejected;
            Equal(false, ProgressProfileCodec.TryDeserialize(save, out rejected));
            Equal(0, rejected.TotalXp);
            Equal(0, rejected.LifetimeBlocksUsed);
            Equal(0, rejected.GetUnlockedAchievements().Length);
        }
        Throws<ArgumentNullException>(() => ProgressProfileCodec.Serialize(null));
    }

    private static void CodecEmptyAndWindowsLines()
    {
        ProgressProfile profile;
        Equal(true, ProgressProfileCodec.TryDeserialize(null, out profile));
        Equal(0, profile.TotalXp);
        Equal(true, ProgressProfileCodec.TryDeserialize("", out profile));
        const string windowsSave = "STACK_AND_SLIDE_PROGRESS|1\r\nblocks|1\r\nstage|1|1000|0|3\r\nachievement|first_block\r\n";
        Equal(true, ProgressProfileCodec.TryDeserialize(windowsSave, out profile));
        Equal(1, profile.LifetimeBlocksUsed);
        Equal(1000, profile.TotalXp);
    }

    private static void LoadFailures()
    {
        foreach (var store in new[] { new MemoryStore { FailLoad = true }, new MemoryStore { Data = "corrupt" } })
        {
            var tracker = new ProgressionTracker(store);
            Equal(false, tracker.LastLoadSucceeded);
            Equal(0, tracker.Profile.TotalXp);
            tracker.StartRun(1, 1, 0d);
            Equal(true, Evaluate(tracker, 1, 1, true, 0d).IsCompleted);
            Equal(true, tracker.LastSaveSucceeded);
        }
    }

    private static void SaveFailures()
    {
        var store = new MemoryStore { FailSave = true };
        var tracker = new ProgressionTracker(store);
        tracker.StartRun(1, 1, 0d);
        string[] newly;
        tracker.RecordSpawn(1, 1, out newly);
        Equal(false, tracker.LastSaveSucceeded);
        Equal(1, tracker.Profile.LifetimeBlocksUsed);
        store.FailSave = false;
        Evaluate(tracker, 1, 1, true, 0d);
        Equal(true, tracker.LastSaveSucceeded);
        Equal(2, store.SaveCalls);
        ProgressProfile saved;
        Equal(true, ProgressProfileCodec.TryDeserialize(store.Data, out saved));
        Equal(1, saved.LifetimeBlocksUsed);
        Equal(1000, saved.TotalXp);
        Equal(true, saved.IsUnlocked("first_block"));
    }

    private static void LoadedProgress()
    {
        var store = new MemoryStore();
        var original = new ProgressionTracker(store);
        original.StartRun(1, 1, 0d);
        string[] newly;
        original.SyncSpawns(1, 1, 10, out newly);
        Evaluate(original, 1, 1, true, 10d);
        var restored = new ProgressionTracker(store);
        Equal(true, restored.LastLoadSucceeded);
        Equal(980, restored.Profile.TotalXp);
        Equal(true, restored.Profile.IsUnlocked("builder"));
        restored.StartRun(1, 1, 0d); // Session tokens are local to the tracker lifetime.
        restored.RecordSpawn(1, 1, out newly);
        Equal(0, newly.Length);
        var replay = Evaluate(restored, 1, 1, true, 20d);
        Equal(0, replay.NewlyUnlocked.Length);
        Equal(980, restored.Profile.TotalXp);
        Equal(11, restored.Profile.LifetimeBlocksUsed);
    }

    private static void FlushRetriesFailures()
    {
        var store = new MemoryStore { FailSave = true };
        var tracker = new ProgressionTracker(store);
        Equal(true, tracker.Flush());
        Equal(0, store.SaveCalls);
        tracker.StartRun(1, 1, 0d);
        string[] newly;
        tracker.RecordSpawn(1, 1, out newly);
        Equal(false, tracker.Flush());
        Equal(2, store.SaveCalls);
        store.FailSave = false;
        Equal(true, tracker.Flush());
        Equal(3, store.SaveCalls);
        Equal(true, tracker.Flush());
        Equal(3, store.SaveCalls);
        ProgressProfile profile;
        Equal(true, ProgressProfileCodec.TryDeserialize(store.Data, out profile));
        Equal(1, profile.LifetimeBlocksUsed);
        Equal(true, new ProgressionTracker().Flush());
    }

    private static void LifetimeCounterBound()
    {
        var store = new MemoryStore { Data = "STACK_AND_SLIDE_PROGRESS|1\nblocks|999999999\n" };
        var tracker = new ProgressionTracker(store);
        tracker.StartRun(1, 1, 0d);
        string[] newly;
        tracker.SyncSpawns(1, 1, 20, out newly);
        Equal(1000000000, tracker.Profile.LifetimeBlocksUsed);
        Equal(20, tracker.GetRun(0d).BlocksUsed);
        ProgressProfile profile;
        Equal(true, ProgressProfileCodec.TryDeserialize(store.Data, out profile));
        Equal(1000000000, profile.LifetimeBlocksUsed);
    }
}
