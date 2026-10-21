using System;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;

/// <summary>
/// Owns spawned roots and the current piece/preview pair. Inventory, menus and puzzle rules
/// remain outside this module so the same spawning lifecycle can serve another game flow.
/// </summary>
public sealed class BlockSpawner
{
    private readonly Vector3 origin;
    private readonly Vector3 ghostOrigin;
    private readonly List<GameObject> ownedRoots = new List<GameObject>();
    private Block activeBlock;
    private bool isSpawning;
    private bool isClearing;
    private int generation;

    public bool HasActiveBlock { get { return activeBlock != null && activeBlock.IsActive; } }
    public GameObject CurrentGhost { get; private set; }

    public BlockSpawner(Vector3 origin, Vector3 ghostOrigin)
    {
        this.origin = origin;
        this.ghostOrigin = ghostOrigin;
    }

    /// <summary>
    /// Creates one pair, or returns false while another piece is still moving. Invalid
    /// specifications throw before replacing a previous pair; inventory is never changed here.
    /// </summary>
    public bool TrySpawn(BlockSpec specification)
    {
        if (specification == null)
            throw new ArgumentNullException(nameof(specification));
        specification.ValidatePrefabs();
        if (isSpawning || isClearing || HasActiveBlock)
            return false;

        // Instantiate and retirement callbacks can synchronously call back into the
        // coordinator. Reject nested requests until this pair has committed or rolled back.
        isSpawning = true;
        try
        {
            return SpawnPair(specification, generation);
        }
        finally
        {
            // A failed clone must not permanently block the next valid selection.
            isSpawning = false;
        }
    }

    private bool SpawnPair(BlockSpec specification, int expectedGeneration)
    {
        GameObject ghostRoot = null;
        GameObject blockRoot = null;
        bool committed = false;
        try
        {
            // Awake may run synchronously while cloning. The production controllers bind
            // explicitly and no longer perform a global tag lookup from those callbacks.
            ghostRoot = UnityObject.Instantiate(specification.GhostPrefab,
                ghostOrigin + specification.SpawnOffset, Quaternion.identity);
            if (generation != expectedGeneration)
                return false;

            blockRoot = UnityObject.Instantiate(specification.BlockPrefab,
                origin + specification.SpawnOffset, Quaternion.identity);
            if (generation != expectedGeneration)
                return false;

            Block controller = blockRoot.GetComponentInChildren<Block>();
            GhostBlock preview = ghostRoot.GetComponentInChildren<GhostBlock>();
            if (controller == null || preview == null)
                throw new InvalidOperationException("The spawned prefab pair is missing its required controllers.");
            controller.Initialize(preview);
            if (generation != expectedGeneration)
                return false;

            // Detach the outgoing pair before any OnDisable callback can clear/reload the
            // session. Clear owns its remaining root snapshot; this method owns oldGhost.
            Block previousController = activeBlock;
            GameObject oldGhost = CurrentGhost;
            activeBlock = null;
            CurrentGhost = null;
            ownedRoots.Remove(oldGhost);
            if (previousController != null)
                previousController.CompletePlacement();
            Retire(oldGhost);
            if (generation != expectedGeneration)
                return false;

            // Publishing is the last step. A reset anywhere above cancels both staged
            // roots instead of leaking them into the inventory or ownership of a new session.
            activeBlock = controller;
            CurrentGhost = ghostRoot;
            ownedRoots.Add(ghostRoot);
            ownedRoots.Add(blockRoot);
            committed = true;
            return true;
        }
        finally
        {
            if (!committed)
            {
                // This covers exceptions AND cancellation after synchronous callbacks.
                // Never look up roots by tag: a new session may already use the same tags.
                Retire(blockRoot);
                Retire(ghostRoot);
            }
        }
    }

    /// <summary>
    /// Invalidates work started before a data reload without deleting committed pieces.
    /// The coordinator can call this when inventory is rebound during a prefab callback.
    /// </summary>
    public void CancelPendingSpawn()
    {
        unchecked { generation++; }
    }

    /// <summary>
    /// Clears this spawner's whole prefab roots. Deactivation precedes deferred Destroy so
    /// their colliders cannot affect another session started in the same Unity frame.
    /// </summary>
    public void Clear()
    {
        CancelPendingSpawn();
        if (isClearing)
            return;

        // SetActive(false) can synchronously enter Clear again. Detach every owned field
        // and copy the list before callbacks so they cannot mutate an active enumeration.
        GameObject[] retiringRoots = ownedRoots.ToArray();
        ownedRoots.Clear();
        activeBlock = null;
        CurrentGhost = null;
        isClearing = true;
        try
        {
            foreach (GameObject root in retiringRoots)
                Retire(root);
        }
        finally
        {
            isClearing = false;
        }
    }

    private static void Retire(GameObject root)
    {
        if (root == null)
            return;
        root.SetActive(false);
        UnityObject.Destroy(root);
    }
}
