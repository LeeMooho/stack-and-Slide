using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Versioned, invariant text storage for Unity runtimes without System.Text.Json.
/// Parsing is all-or-nothing: partial/corrupt/future saves never become partial awards.
/// </summary>
public static class ProgressProfileCodec
{
    private const string Header = "STACK_AND_SLIDE_PROGRESS|1";
    private const int MaxCharacters = 1048576;

    public static string Serialize(ProgressProfile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        var text = new StringBuilder();
        text.Append(Header).Append('\n');
        text.Append("blocks|").Append(profile.LifetimeBlocksUsed.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (StageBest best in profile.GetStageBests())
        {
            text.Append("stage|").Append(best.StageId.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(best.BestScore.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(best.BestTimeSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(best.BestStars.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        foreach (string id in profile.GetUnlockedAchievements()) text.Append("achievement|").Append(id).Append('\n');
        return text.ToString();
    }

    public static bool TryDeserialize(string text, out ProgressProfile profile)
    {
        profile = new ProgressProfile();
        if (string.IsNullOrEmpty(text)) return true;
        if (text.Length > MaxCharacters) return false;
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.Length < 2 || lines.Length > ProgressProfile.MaxStages + 10 || lines[0] != Header) return false;

        int lifetime = 0;
        bool hasBlocks = false;
        var records = new Dictionary<int, StageBest>();
        var achievements = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            for (int index = 1; index < lines.Length; index++)
            {
                // Accept one writer-style final newline, but never hide missing interior rows.
                if (index == lines.Length - 1 && lines[index].Length == 0) continue;
                string[] parts = lines[index].Split('|');
                if (parts.Length == 2 && parts[0] == "blocks")
                {
                    if (hasBlocks || !TryInt(parts[1], out lifetime) || lifetime < 0 || lifetime > ProgressProfile.MaxCounter) return false;
                    hasBlocks = true;
                }
                else if (parts.Length == 5 && parts[0] == "stage")
                {
                    int stageId, score, stars;
                    double time;
                    if (!TryInt(parts[1], out stageId) || !TryInt(parts[2], out score) || !TryInt(parts[4], out stars)
                        || !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out time)
                        || records.ContainsKey(stageId) || records.Count >= ProgressProfile.MaxStages) return false;
                    records.Add(stageId, new StageBest(stageId, score, time, stars));
                }
                else if (parts.Length == 2 && parts[0] == "achievement")
                {
                    if (!ProgressProfile.IsKnownAchievement(parts[1]) || !achievements.Add(parts[1])) return false;
                }
                else return false;
            }
            if (!hasBlocks) return false;
            profile = new ProgressProfile(lifetime, records, achievements);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryInt(string value, out int parsed)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed);
    }
}
