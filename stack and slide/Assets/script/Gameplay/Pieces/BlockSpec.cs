using System;
using UnityEngine;

/// <summary>
/// Immutable registration used by spawning code. It contains no remaining stock or UI state,
/// so one definition can be reused by several stages without sharing mutable session data.
/// </summary>
public sealed class BlockSpec
{
    public int BlockId { get; }
    public GameObject BlockPrefab { get; }
    public GameObject GhostPrefab { get; }
    public Vector3 SpawnOffset { get; }

    public BlockSpec(int blockId, GameObject blockPrefab, GameObject ghostPrefab, Vector3 spawnOffset)
    {
        if (blockId <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockId), "A block ID must be positive.");
        if (!IsFinite(spawnOffset.x) || !IsFinite(spawnOffset.y) || !IsFinite(spawnOffset.z))
            throw new ArgumentException("Spawn offsets must contain only finite coordinates.", nameof(spawnOffset));

        BlockId = blockId;
        BlockPrefab = blockPrefab;
        GhostPrefab = ghostPrefab;
        SpawnOffset = spawnOffset;
        ValidatePrefabs();
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Prefab references can be edited or destroyed after catalog creation. Validate before
    /// any clone or retirement so a bad definition cannot consume or replace an owned piece.
    /// </summary>
    internal void ValidatePrefabs()
    {
        if (BlockPrefab == null)
            throw new ArgumentException("A playable block prefab is required.", nameof(BlockPrefab));
        if (GhostPrefab == null)
            throw new ArgumentException("A ghost prefab is required.", nameof(GhostPrefab));

        Block controller = BlockPrefab.GetComponentInChildren<Block>();
        GhostBlock preview = GhostPrefab.GetComponentInChildren<GhostBlock>();
        if (controller == null || controller.gameObject == BlockPrefab || !controller.enabled)
            throw new ArgumentException("The block prefab needs an enabled Block controller on a geometry child.", nameof(BlockPrefab));
        if (preview == null || preview.gameObject == GhostPrefab || !preview.enabled)
            throw new ArgumentException("The ghost prefab needs an enabled GhostBlock controller on a geometry child.", nameof(GhostPrefab));
    }
}
