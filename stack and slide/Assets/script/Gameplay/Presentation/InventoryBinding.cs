using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maps a stable piece ID to its scene-owned inventory icon. New piece types can add
/// entries in the Inspector without adding fields or branching in the presenter.
/// </summary>
[Serializable]
public sealed class InventoryBinding
{
    public int BlockId;
    public GameObject Image;
    public Text CountText;

    public InventoryBinding() { }

    public InventoryBinding(int blockId, GameObject image, Text countText)
    {
        BlockId = blockId;
        Image = image;
        CountText = countText;
    }
}
