using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Displays inventory without creating pieces or changing gameplay counts.
/// Binding order controls visual order; numeric IDs do not imply a screen position.
/// </summary>
public sealed class InventoryPresenter
{
    private readonly List<InventoryBinding> bindings = new List<InventoryBinding>();
    private readonly HashSet<int> boundIds = new HashSet<int>();
    private readonly RectTransform[] locations;

    public InventoryPresenter(IEnumerable<InventoryBinding> bindings, RectTransform[] locations)
    {
        if (bindings == null) throw new ArgumentNullException(nameof(bindings));
        if (locations == null) throw new ArgumentNullException(nameof(locations));
        this.locations = (RectTransform[])locations.Clone();
        foreach (RectTransform location in this.locations)
            if (location == null) throw new ArgumentException("Inventory locations must be assigned.", nameof(locations));

        foreach (InventoryBinding binding in bindings)
        {
            if (binding == null || binding.BlockId <= 0 || binding.Image == null || binding.CountText == null)
                throw new ArgumentException("Inventory bindings need a positive ID, icon and count text.", nameof(bindings));
            if (!boundIds.Add(binding.BlockId))
                throw new ArgumentException("Duplicate inventory binding ID: " + binding.BlockId, nameof(bindings));

            // Copy the mapping so later edits to its authoring object cannot desynchronize
            // the validation index and display order for an already-running session.
            this.bindings.Add(new InventoryBinding(binding.BlockId, binding.Image, binding.CountText));
        }
    }

    /// <summary>
    /// Checks every positive count before any icon is moved or hidden. The coordinator
    /// also uses this before replacing a running stage with newly authored content.
    /// </summary>
    public void ValidateStock(IDictionary<int, int> stock)
    {
        if (stock == null) throw new ArgumentNullException(nameof(stock));
        int visible = 0;
        foreach (KeyValuePair<int, int> entry in stock)
        {
            if (entry.Key <= 0 || entry.Value < 0)
                throw new ArgumentException("Inventory IDs must be positive and counts nonnegative.", nameof(stock));
            if (entry.Value == 0) continue;
            if (!boundIds.Contains(entry.Key))
                throw new InvalidOperationException("No inventory UI binding exists for block ID " + entry.Key + ".");
            visible++;
        }
        if (visible > locations.Length)
            throw new InvalidOperationException("The stage needs more inventory locations than the scene provides.");
    }

    public void Render(BlockInventory inventory)
    {
        if (inventory == null) throw new ArgumentNullException(nameof(inventory));
        ValidateStock(inventory.Snapshot());
        int locationIndex = 0;
        foreach (InventoryBinding binding in bindings)
        {
            int remaining = inventory.GetRemaining(binding.BlockId);
            binding.Image.SetActive(remaining > 0);
            if (remaining <= 0) continue;
            binding.Image.transform.position = locations[locationIndex].position;
            binding.CountText.text = remaining.ToString();
            locationIndex++;
        }
    }
}
