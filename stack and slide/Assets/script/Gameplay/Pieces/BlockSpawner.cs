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
        if (isSpawning || HasActiveBlock)
            return false;

        // Instantiate and retirement callbacks can synchronously call back into the
        // coordinator. Reject nested requests until this pair has committed or rolled back.
        isSpawning = true;
        try
        {
            return SpawnPair(specification);
        }
        finally
        {
            // A failed clone must not permanently block the next valid selection.
            isSpawning = false;
        }
    }

    private bool SpawnPair(BlockSpec specification)
    {
        GameObject ghostRoot = null;
        GameObject blockRoot = null;
        Block controller;
        try
        {
            // Awake may run synchronously while cloning. The production controllers bind
            // explicitly and no longer perform a global tag lookup from those callbacks.
            ghostRoot = UnityObject.Instantiate(specification.GhostPrefab,
                ghostOrigin + specification.SpawnOffset, Quaternion.identity);
            blockRoot = UnityObject.Instantiate(specification.BlockPrefab,
                origin + specification.SpawnOffset, Quaternion.identity);
            controller = blockRoot.GetComponentInChildren<Block>();
            GhostBlock preview = ghostRoot.GetComponentInChildren<GhostBlock>();
            if (controller == null || preview == null)
                throw new InvalidOperationException("The spawned prefab pair is missing its required controllers.");
            controller.Initialize(preview);
        }
        catch
        {
            // Clean only roots returned to this module. Do not search by tag or editor name:
            // another spawner or unrelated scene object may use exactly the same tags.
            Retire(blockRoot);
            Retire(ghostRoot);
            throw;
        }

        // Commit the replacement only after both clones exist and bind successfully.
        // The previous landed block remains valid if definition validation/cloning fails.
        if (activeBlock != null)
            activeBlock.CompletePlacement();
        Retire(CurrentGhost);
        ownedRoots.Remove(CurrentGhost);

        activeBlock = controller;
        CurrentGhost = ghostRoot;
        ownedRoots.Add(ghostRoot);
        ownedRoots.Add(blockRoot);
        return true;
    }

    /// <summary>
    /// Clears this spawner's whole prefab roots. Deactivation precedes deferred Destroy so
    /// their colliders cannot affect another session started in the same Unity frame.
    /// </summary>
    public void Clear()
    {
        foreach (GameObject root in ownedRoots)
            Retire(root);
        ownedRoots.Clear();
        activeBlock = null;
        CurrentGhost = null;
    }

    private static void Retire(GameObject root)
    {
        if (root == null)
            return;
        root.SetActive(false);
        UnityObject.Destroy(root);
    }
}
