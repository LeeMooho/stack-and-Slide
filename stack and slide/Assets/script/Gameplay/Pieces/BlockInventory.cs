using System;
using System.Collections.Generic;

/// <summary>
/// Session-owned piece counts, independent of Unity objects and the seven legacy IDs.
/// A new block type is simply another positive key in a stage's stock snapshot.
/// </summary>
public sealed class BlockInventory
{
    private Dictionary<int, int> remaining = new Dictionary<int, int>();

    /// <summary>
    /// Replaces stock only after the entire input is valid. A malformed new stage must not
    /// leave a previously valid session with partially changed counts.
    /// </summary>
    public void Reset(IDictionary<int, int> stock)
    {
        if (stock == null)
            throw new ArgumentNullException(nameof(stock));

        var replacement = new Dictionary<int, int>();
        foreach (KeyValuePair<int, int> entry in stock)
        {
            if (entry.Key <= 0)
                throw new ArgumentException("Inventory block IDs must be positive.", nameof(stock));
            if (entry.Value < 0)
                throw new ArgumentException("Inventory counts cannot be negative.", nameof(stock));
            replacement.Add(entry.Key, entry.Value);
        }
        remaining = replacement;
    }

    public int GetRemaining(int blockId)
    {
        int count;
        return remaining.TryGetValue(blockId, out count) ? count : 0;
    }

    /// <summary>Consumes one known, available item without allowing negative counts.</summary>
    public bool TryConsume(int blockId)
    {
        int count;
        if (!remaining.TryGetValue(blockId, out count) || count <= 0)
            return false;

        remaining[blockId] = count - 1;
        return true;
    }

    /// <summary>Returns independent data for UI, compatibility mirrors, or save systems.</summary>
    public Dictionary<int, int> Snapshot()
    {
        return new Dictionary<int, int>(remaining);
    }
}
