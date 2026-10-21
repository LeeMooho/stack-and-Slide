using System;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;

/// <summary>
/// Covers the extension contracts independently of the scene coordinator. Prefab fixtures
/// contain child controllers, but cloning and lifecycle scheduling remain API substitutes.
/// </summary>
internal static class PieceModuleTests
{
    public static void Register(Action<string, Action> run)
    {
        run("Inventory accepts new IDs and copies input/output snapshots", InventorySnapshots);
        run("Invalid stock resets preserve the previous inventory", InvalidInventoryReset);
        run("Consumption affects only the requested ID and cannot go negative", InventoryConsumption);
        run("Catalog supports new IDs and explicit legacy asset overrides", CatalogExtensions);
        run("Catalog snapshots authored ID and offset values", CatalogSnapshots);
        run("Catalog rejects duplicate registrations and missing bindings", InvalidCatalog);
        run("Block specifications reject invalid controller hierarchies", InvalidSpecification);
        run("Spawner binds the matching children at configured root offsets", SpawnBinding);
        run("A moving piece blocks a second spawn without scene mutations", BlockConcurrentSpawn);
        run("Synchronous prefab callbacks cannot reenter an in-progress spawn", ReentrantSpawn);
        run("Replacing a placed piece retires only its ghost", ReplaceLandedPiece);
        run("Spawner clear is idempotent and leaves unrelated roots alone", ClearOwnedRoots);
        run("Edited prefab definitions cannot retire an existing landed pair", RevalidateBeforeReplacement);
        run("Clone failures clean returned roots without replacing the previous pair", FailedCloneRollback);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual))
            throw new Exception("Expected " + expected + ", got " + actual);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static void InventorySnapshots()
    {
        var source = new Dictionary<int, int> { { 1, 3 }, { 8, 2 } };
        var inventory = new BlockInventory();
        inventory.Reset(source);
        source[8] = 99;
        var saved = inventory.Snapshot();
        saved[1] = 0;
        saved.Add(9, 5);
        Equal(3, inventory.GetRemaining(1));
        Equal(2, inventory.GetRemaining(8));
        Equal(0, inventory.GetRemaining(9));
    }

    private static void InvalidInventoryReset()
    {
        var inventory = new BlockInventory();
        inventory.Reset(new Dictionary<int, int> { { 8, 2 } });
        // Put a valid item first to catch implementations that mutate stock as they iterate.
        Throws<ArgumentException>(() => inventory.Reset(new Dictionary<int, int> { { 1, 5 }, { 8, -1 } }));
        Throws<ArgumentException>(() => inventory.Reset(new Dictionary<int, int> { { 0, 1 } }));
        Throws<ArgumentNullException>(() => inventory.Reset(null));
        Equal(2, inventory.GetRemaining(8));
        Equal(0, inventory.GetRemaining(1));
    }

    private static void InventoryConsumption()
    {
        var inventory = new BlockInventory();
        inventory.Reset(new Dictionary<int, int> { { 1, 3 }, { 8, 1 }, { 9, 0 } });
        Equal(true, inventory.TryConsume(8));
        Equal(false, inventory.TryConsume(8));
        Equal(false, inventory.TryConsume(9));
        Equal(false, inventory.TryConsume(77));
        Equal(false, inventory.TryConsume(-1));
        Equal(0, inventory.GetRemaining(8));
        Equal(3, inventory.GetRemaining(1));
    }

    private static void CatalogExtensions()
    {
        var original = NewSpec(1);
        var replacement = NewAsset(1, new Vector3(.5f, 0, 0));
        var added = NewAsset(8, new Vector3(0, .5f, 0));
        var catalog = new BlockCatalog(new[] { original }, new[] { replacement, added });
        BlockSpec selected;
        Equal(true, catalog.TryGet(1, out selected));
        Equal(replacement.BlockPrefab, selected.BlockPrefab);
        Equal(replacement.SpawnOffset, selected.SpawnOffset);
        Equal(true, catalog.TryGet(8, out selected));
        Equal(added.GhostPrefab, selected.GhostPrefab);
        Equal(false, catalog.TryGet(9, out selected));
        Equal(null, selected);
    }

    private static void CatalogSnapshots()
    {
        var definition = NewAsset(8, new Vector3(.5f, 0, 0));
        var catalog = new BlockCatalog(new BlockSpec[0], new[] { definition });
        definition.BlockId = 9;
        definition.SpawnOffset = Vector3.up;
        BlockSpec selected;
        Equal(true, catalog.TryGet(8, out selected));
        Equal(8, selected.BlockId);
        Equal(new Vector3(.5f, 0, 0), selected.SpawnOffset);
        Equal(false, catalog.TryGet(9, out selected));
    }

    private static void InvalidCatalog()
    {
        var original = NewSpec(1);
        var definition = NewAsset(8, Vector3.zero);
        Throws<ArgumentNullException>(() => new BlockCatalog(null, null));
        Throws<ArgumentException>(() => new BlockCatalog(new[] { original, original }, null));
        Throws<ArgumentException>(() => new BlockCatalog(new BlockSpec[] { null }, null));
        Throws<ArgumentException>(() => new BlockCatalog(new BlockSpec[0], new[] { definition, definition }));
        Throws<ArgumentException>(() => new BlockCatalog(new BlockSpec[0], new BlockDefinition[] { null }));
        // An absent optional Inspector list preserves the default catalog.
        BlockSpec selected;
        Equal(true, new BlockCatalog(new[] { original }, null).TryGet(1, out selected));
        Equal(original, selected);
    }

    private static void InvalidSpecification()
    {
        var valid = NewSpec(1);
        Throws<ArgumentOutOfRangeException>(() => new BlockSpec(0, valid.BlockPrefab, valid.GhostPrefab, Vector3.zero));
        Throws<ArgumentException>(() => new BlockSpec(1, null, valid.GhostPrefab, Vector3.zero));
        Throws<ArgumentException>(() => new BlockSpec(1, valid.BlockPrefab, null, Vector3.zero));
        Throws<ArgumentException>(() => new BlockSpec(1, new GameObject(), valid.GhostPrefab, Vector3.zero));
        var rootController = new GameObject();
        rootController.AddComponent<Block>();
        Throws<ArgumentException>(() => new BlockSpec(1, rootController, valid.GhostPrefab, Vector3.zero));
        valid.BlockPrefab.GetComponentInChildren<Block>().enabled = false;
        Throws<ArgumentException>(() => new BlockSpec(1, valid.BlockPrefab, valid.GhostPrefab, Vector3.zero));
    }

    private static void SpawnBinding()
    {
        var spec = NewSpec(8, new Vector3(.5f, 0, .5f));
        var spawner = new BlockSpawner(new Vector3(1, 4, 2), new Vector3(1, 0, 2));
        Equal(true, spawner.TrySpawn(spec));
        Equal(2, UnityObject.Spawns.Count);
        Equal(spec.GhostPrefab, UnityObject.Spawns[0].Prefab);
        Equal(spec.BlockPrefab, UnityObject.Spawns[1].Prefab);
        Equal(new Vector3(1.5f, 0, 2.5f), UnityObject.Spawns[0].Clone.transform.position);
        Equal(new Vector3(1.5f, 4, 2.5f), UnityObject.Spawns[1].Clone.transform.position);
        var controller = UnityObject.Spawns[1].Clone.GetComponentInChildren<Block>();
        Equal(controller.gameObject, spawner.CurrentGhost.GetComponentInChildren<GhostBlock>().cBlock);
        Equal(true, spawner.HasActiveBlock);
    }

    private static void BlockConcurrentSpawn()
    {
        var spawner = new BlockSpawner(Vector3.zero, Vector3.zero);
        var spec = NewSpec(1);
        Equal(true, spawner.TrySpawn(spec));
        var currentGhost = spawner.CurrentGhost;
        Equal(false, spawner.TrySpawn(NewSpec(8)));
        Equal(2, UnityObject.Spawns.Count);
        Equal(0, UnityObject.Destroyed.Count);
        Equal(currentGhost, spawner.CurrentGhost);
        Equal(true, currentGhost.activeSelf);
    }

    private static void ReplaceLandedPiece()
    {
        var spawner = new BlockSpawner(Vector3.zero, Vector3.zero);
        spawner.TrySpawn(NewSpec(1));
        var oldGhost = spawner.CurrentGhost;
        var oldRoot = UnityObject.Spawns[1].Clone;
        var oldController = oldRoot.GetComponentInChildren<Block>();
        // Directly model the tag transition; the substitute does not run collision physics.
        oldController.target.tag = "OnFixedBlock";
        Equal(false, spawner.HasActiveBlock);
        Equal(true, spawner.TrySpawn(NewSpec(8)));
        Equal("FixedBlock", oldController.target.tag);
        Equal(false, oldController.enabled);
        Equal(true, oldRoot.activeSelf);
        Equal(false, oldGhost.activeSelf);
        Equal(true, UnityObject.Destroyed.Contains(oldGhost));
        Equal(false, UnityObject.Destroyed.Contains(oldRoot));
        Equal(true, spawner.HasActiveBlock);
    }

    private static void ReentrantSpawn()
    {
        var spawner = new BlockSpawner(Vector3.zero, Vector3.zero);
        var original = NewSpec(1);
        var nested = NewSpec(8);
        bool callbackRan = false;
        bool nestedResult = true;
        UnityObject.AfterInstantiate = clone =>
        {
            // Clear the hook before reentry so a broken guard produces a finite failure
            // rather than recursively cloning until the test process exhausts its stack.
            UnityObject.AfterInstantiate = null;
            callbackRan = true;
            nestedResult = spawner.TrySpawn(nested);
        };
        try
        {
            Equal(true, spawner.TrySpawn(original));
        }
        finally
        {
            UnityObject.AfterInstantiate = null;
        }
        Equal(true, callbackRan);
        Equal(false, nestedResult);
        Equal(2, UnityObject.Spawns.Count);
        Equal(original.GhostPrefab, UnityObject.Spawns[0].Prefab);
        Equal(original.BlockPrefab, UnityObject.Spawns[1].Prefab);
        var controller = UnityObject.Spawns[1].Clone.GetComponentInChildren<Block>();
        Equal(controller.gameObject, spawner.CurrentGhost.GetComponentInChildren<GhostBlock>().cBlock);
        Equal(true, spawner.HasActiveBlock);

        // Successful completion releases the guard as well as exceptional completion.
        controller.target.tag = "OnFixedBlock";
        Equal(true, spawner.TrySpawn(nested));
        Equal(4, UnityObject.Spawns.Count);
    }

    private static void ClearOwnedRoots()
    {
        var unrelated = new GameObject("Other spawner root");
        var spawner = new BlockSpawner(Vector3.zero, Vector3.zero);
        spawner.TrySpawn(NewSpec(8));
        var ghost = spawner.CurrentGhost;
        var piece = UnityObject.Spawns[1].Clone;
        spawner.Clear();
        Equal(false, ghost.activeSelf);
        Equal(false, piece.activeSelf);
        Equal(true, UnityObject.Destroyed.Contains(ghost));
        Equal(true, UnityObject.Destroyed.Contains(piece));
        Equal(false, spawner.HasActiveBlock);
        Equal(null, spawner.CurrentGhost);
        int requests = UnityObject.Destroyed.Count;
        spawner.Clear();
        Equal(requests, UnityObject.Destroyed.Count);
        Equal(true, unrelated.activeSelf);
        Equal(false, UnityObject.Destroyed.Contains(unrelated));
    }

    private static void RevalidateBeforeReplacement()
    {
        var spec = NewSpec(8);
        var spawner = new BlockSpawner(Vector3.zero, Vector3.zero);
        spawner.TrySpawn(NewSpec(1));
        var oldGhost = spawner.CurrentGhost;
        var oldController = UnityObject.Spawns[1].Clone.GetComponentInChildren<Block>();
        oldController.target.tag = "OnFixedBlock";
        // Catalog values are immutable, but the referenced prefab itself is still editable.
        spec.GhostPrefab.GetComponentInChildren<GhostBlock>().gameObject.SetActive(false);
        Throws<ArgumentException>(() => spawner.TrySpawn(spec));
        Equal(2, UnityObject.Spawns.Count);
        Equal(oldGhost, spawner.CurrentGhost);
        Equal(true, oldGhost.activeSelf);
        Equal("OnFixedBlock", oldController.target.tag);
        Equal(true, oldController.enabled);
        Equal(0, UnityObject.Destroyed.Count);
    }

    private static void FailedCloneRollback()
    {
        var spec = NewSpec(8);
        spec.BlockPrefab.AddComponent<FailOnClone>();
        var spawner = new BlockSpawner(Vector3.zero, Vector3.zero);
        spawner.TrySpawn(NewSpec(1));
        var oldGhost = spawner.CurrentGhost;
        var oldController = UnityObject.Spawns[1].Clone.GetComponentInChildren<Block>();
        oldController.target.tag = "OnFixedBlock";
        FailOnClone.Fail = true;
        try
        {
            // Simulate the second Instantiate failing after it returned the first root.
            // Unity's own handling of partially constructed, unreturned clones is not modeled.
            Throws<Exception>(() => spawner.TrySpawn(spec));
        }
        finally
        {
            FailOnClone.Fail = false;
        }
        Equal(3, UnityObject.Spawns.Count);
        var failedGhost = UnityObject.Spawns[2].Clone;
        Equal(false, failedGhost.activeSelf);
        Equal(true, UnityObject.Destroyed.Contains(failedGhost));
        Equal(oldGhost, spawner.CurrentGhost);
        Equal(true, oldGhost.activeSelf);
        Equal("OnFixedBlock", oldController.target.tag);
        Equal(true, oldController.enabled);
        // The failure path must release the reentrancy guard so a later valid piece works.
        Equal(true, spawner.TrySpawn(NewSpec(9)));
        Equal(5, UnityObject.Spawns.Count);
        Equal(false, oldGhost.activeSelf);
        Equal("FixedBlock", oldController.target.tag);
        spawner.Clear();
    }

    private static BlockSpec NewSpec(int id, Vector3? offset = null)
    {
        var block = new GameObject("Block " + id);
        var child = new GameObject("Geometry") { tag = "Block" };
        child.transform.parent = block.transform;
        child.AddComponent<Block>();
        var ghost = new GameObject("Ghost " + id);
        var preview = new GameObject("Preview") { tag = "GhostBlock" };
        preview.transform.parent = ghost.transform;
        preview.AddComponent<GhostBlock>();
        return new BlockSpec(id, block, ghost, offset ?? Vector3.zero);
    }

    private static BlockDefinition NewAsset(int id, Vector3 offset)
    {
        var spec = NewSpec(id, offset);
        var definition = ScriptableObject.CreateInstance<BlockDefinition>();
        definition.BlockId = id;
        definition.BlockPrefab = spec.BlockPrefab;
        definition.GhostPrefab = spec.GhostPrefab;
        definition.SpawnOffset = spec.SpawnOffset;
        return definition;
    }

    public sealed class FailOnClone : Component
    {
        public static bool Fail;
        public FailOnClone()
        {
            if (Fail)
                throw new InvalidOperationException("Simulated clone failure.");
        }
    }
}
