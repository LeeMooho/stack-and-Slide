using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

/// <summary>
/// Runs the real gameplay code against scripted Unity API substitutes.
/// These regressions check decisions and state transitions, not engine integration.
/// </summary>
internal static class Program
{
    private static int failures;
    private static int total;

    private static int Main()
    {
        // Session setup and teardown must work before any piece or physics query exists.
        Run("Reset clears inventory and creates independent empty grids", ResetStage);
        Run("Stage 101 preserves its inventory and all grid cells", Stage101);
        Run("Stage selection updates menus, colors and inventory slots", SelectStage);
        Run("Unknown stage selection leaves the menus unchanged", UnknownStage);
        Run("Menu actions cannot spawn pieces or report a puzzle result", ActionsOutsidePlay);
        Run("Restart removes whole spawned roots and restores inventory", RestartStage);
        Run("Returning to the menu clears pieces and inventory", MenuCleanup);
        Run("Grid colors return to their authored values on exit", RestoreGridColors);
        for (int i = 1; i <= 7; i++)
        {
            // Capture a separate ID so each deferred test action targets its own piece.
            int block = i;
            Run("Block " + block + " spawns matching prefabs at its original offset", () => SpawnBlock(block));
        }
        Run("Empty inventory cannot spawn or become negative", EmptyInventory);
        Run("Unknown block IDs do not spawn or consume inventory", UnknownBlock);
        Run("A moving block prevents consuming a second inventory item", ConcurrentSpawn);
        Run("The next spawn finalizes the previous block and retires its ghost root", NextBlock);
        Run("Controllers keep their owners when the old block updates first", () => ControllerOwnership(false));
        Run("Controllers keep their owners when the new block updates first", () => ControllerOwnership(true));
        Run("A ghost follows its explicitly bound block", BoundGhost);
        // Projection tests supply hit lists directly; they do not generate collisions.
        Run("Exact projection accepts active, newly fixed and fixed blocks", MatchingProjection);
        Run("A missing required block fails the projection", MissingProjection);
        Run("An unexpected block fails the projection", UnexpectedProjection);
        Run("Non-block hits cannot satisfy a required cell", NonBlockProjection);
        Run("Ghost and grid hits cannot hide a playable block", OccludedProjection);
        Run("A ray miss cannot reuse an earlier ray hit", IndependentRaycasts);
        Run("Projection casts inward from each rotated grid plane", ProjectionDirections);
        // Input tests invoke one control pass at a time to expose frame-order assumptions.
        Run("Block keyboard and UI movement preserve the half-cell steps", Movement);
        Run("Block keyboard rotations preserve all three axes", Rotation);
        Run("Inactive blocks do not move", InactiveBlock);
        Run("Landing uses the ghost distance and vertical adjustment", Landing);
        Run("A missing ghost does not cause a null-reference exception", MissingGhost);
        Run("A ghost without an active block tolerates lifecycle callbacks", MissingActiveBlock);
        Run("Ghost rotation resets offsets and follows the current target", GhostMovement);
        Run("Ghost landing selects the nearest support behind unrelated hits", NearestSupport);
        Run("Losing support invalidates the previous ghost landing point", LostSupport);
        Run("Combined lateral, rotation and down inputs refresh the current support", SameFrameSupport);
        Run("Down-step landing preserves the half-cell boundary", LandingBoundary);
        ButtonSoundTests.Register(Run);
        StageDataTests.Register(Run);
        PieceModuleTests.Register(Run);
        Run("Block registrations reject nonfinite spawn coordinates", InvalidSpawnOffset);
        ProjectionModuleTests.Register(Run);
        InventoryPresenterTests.Register(Run);
        // A nonzero exit code lets the same executable fail a local command or CI job.
        Console.WriteLine((total - failures) + "/" + total + " isolated gameplay tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        total++;
        // The substitutes keep global registries, just as Unity exposes global searches.
        // Clear every registry so a prior scenario cannot accidentally satisfy this one.
        GameObject.All.Clear();
        UObject.Spawns.Clear();
        UObject.Destroyed.Clear();
        UObject.AfterInstantiate = null;
        UObject.AfterSetActive = null;
        Physics.Results.Clear();
        Physics.AllResults.Clear();
        Physics.Directions.Clear();
        Input.Keys.Clear();
        Debug.Messages.Clear();
        try
        {
            test();
            // Unconsumed results mean the expected physics calls never happened.
            // Unexpected extra calls fail immediately when their result queue is empty.
            Equal(0, Physics.Results.Count);
            Equal(0, Physics.AllResults.Count);
            Console.WriteLine("PASS " + name);
        }
        catch (Exception error)
        {
            // Reflection wraps callback errors; report the underlying failure instead.
            failures++;
            Console.WriteLine("FAIL " + name + ": " + error.GetBaseException().Message);
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual))
        {
            throw new Exception("Expected " + expected + ", got " + actual);
        }
    }

    private static void InvalidSpawnOffset()
    {
        // No prefab cloning or inventory mutation should precede authoring validation.
        foreach (var offset in new[] { new Vector3(float.NaN, 0, 0), new Vector3(0, float.PositiveInfinity, 0),
            new Vector3(0, 0, float.NegativeInfinity) })
        {
            try { new BlockSpec(8, null, null, offset); }
            catch (ArgumentException error)
            {
                Equal("spawnOffset", error.ParamName);
                continue;
            }
            throw new Exception("Invalid offset was accepted.");
        }
        Equal(0, UObject.Spawns.Count);
    }

    private static void Invoke(object target, string method)
    {
        // Call private lifecycle methods explicitly without widening the production API.
        // The test chooses callback order; no Unity frame scheduler runs here.
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    private static int[] Counts(StageManager manager)
    {
        // Keep the selection-ID order separate from the UI's visual ordering.
        return new[] { manager.A1Number, manager.A2Number, manager.INumber, manager.ONumber,
            manager.PNumber, manager.TNumber, manager.ZNumber };
    }

    private static void ResetStage()
    {
        var manager = new StageManager();
        manager.stage101();
        var previousFloor = manager.stage_Floor;
        manager.ResetStage();
        Equal("0,0,0,0,0,0,0", string.Join(",", Counts(manager)));
        foreach (var grid in new[] { manager.stage_Floor, manager.stage_Wall, manager.stage_FrontWall })
        {
            Equal(64, grid.Length);
            Equal(0, grid.Sum());
        }
        manager.stage_Floor[0] = 1;
        // Separate arrays are important: changing one projection must not alter another.
        Equal(0, manager.stage_Wall[0]);
        Equal(0, manager.stage_FrontWall[0]);
        Equal(false, ReferenceEquals(previousFloor, manager.stage_Floor));
    }

    private static void Stage101()
    {
        var manager = new StageManager();
        manager.stage101();
        // Golden data is written independently of the implementation, including every
        // row of each projection, so an accidental level edit cannot pass by construction.
        Equal("1,1,1,0,1,1,1", string.Join(",", Counts(manager)));
        Equal("00000000/00000000/00000000/00111100/00111100/00111100/00000000/00000000", Grid(manager.stage_Floor));
        Equal("00000000/00000000/00000000/00000000/00010000/00011000/00011000/00111000", Grid(manager.stage_Wall));
        Equal("00000000/00000000/00000000/00000000/00100000/00100100/00111100/00111100", Grid(manager.stage_FrontWall));
    }

    private static string Grid(int[] cells)
    {
        // Row separators make failures readable as an 8-by-8 board instead of 64 digits.
        return string.Join("/", Enumerable.Range(0, 8).Select(row => string.Concat(cells.Skip(row * 8).Take(8))));
    }

    private static void SelectStage()
    {
        var f = new Fixture();
        f.Stage.SelectStage(101);
        Equal(true, f.Stage.playUI.activeSelf);
        Equal(false, f.Stage.mainMenu.activeSelf);
        Equal(false, f.Stage.OImage.activeSelf);
        // Stage 101 omits O pieces; the other six icons must occupy consecutive slots.
        var visible = new[] { f.Stage.A1Image, f.Stage.A2Image, f.Stage.IImage,
            f.Stage.ZImage, f.Stage.PImage, f.Stage.TImage };
        for (int i = 0; i < visible.Length; i++)
        {
            Equal(true, visible[i].activeSelf);
            Equal(f.Stage.Location[i].position, visible[i].transform.position);
        }
        foreach (var text in new[] { f.Stage.A1Text, f.Stage.A2Text, f.Stage.IText,
            f.Stage.ZText, f.Stage.PText, f.Stage.TText }) Equal("1", text.text);
        Equal(Color.black, f.Stage.floor[26].GetComponent<MeshRenderer>().material.color);
        Equal(new Color(1, 1, 1), f.Stage.floor[0].GetComponent<MeshRenderer>().material.color);
        f.Stage.openMainMenu();
        Equal(false, f.Stage.playUI.activeSelf);
        Equal(true, f.Stage.mainMenu.activeSelf);
    }

    private static void UnknownStage()
    {
        var f = new Fixture();
        f.Stage.SelectStage(-1);
        Equal(false, f.Stage.playUI.activeSelf);
        Equal(true, f.Stage.mainMenu.activeSelf);
        Equal(0, f.Stage.A1Number);
    }

    private static void SpawnBlock(int number)
    {
        var f = new Fixture();
        f.Stage.SelectStage(101);
        f.Manager.stage101();
        // Enable O only for this mapping test; the authored level gives it no inventory.
        // This lets all seven public button IDs exercise the same spawn contract.
        f.Manager.ONumber = 1;
        f.Stage.CallVari();
        var names = new[] { "A1", "A2", "I", "O", "P", "T", "Z" };
        var offsets = new[] { Vector3.zero, new Vector3(.5f, .5f, .5f), new Vector3(0, 0, .5f),
            new Vector3(.5f, 0, 0), new Vector3(0, .5f, .5f), new Vector3(0, .5f, 0), new Vector3(0, .5f, .5f) };
        string name = names[number - 1];
        f.Stage.GenerateBlock(number);
        // The first clone is the ghost root and the second is the controllable piece.
        // Check both source prefabs and world-space spawn offsets, not just clone counts.
        Equal(2, UObject.Spawns.Count);
        Equal("Ghost" + name, UObject.Spawns[0].Prefab.name);
        Equal(name, UObject.Spawns[1].Prefab.name);
        Equal(f.Stage.GhostOrigin + offsets[number - 1], UObject.Spawns[0].Clone.transform.position);
        Equal(f.Stage.Origin + offsets[number - 1], UObject.Spawns[1].Clone.transform.position);
        Equal(UObject.Spawns[0].Clone, f.Stage.pre_GhostBlock);
        Equal(false, ((GameObject)typeof(stage).GetField(name + "Image").GetValue(f.Stage)).activeSelf);
        f.Stage.GenerateBlock(number);
        Equal(2, UObject.Spawns.Count);
        Equal("1,1,1,1,1,1,1", string.Join(",", Counts(f.Manager)));
    }

    private static void EmptyInventory()
    {
        var f = new Fixture();
        // Stay in play mode so a menu guard cannot conceal a broken inventory guard.
        f.StartEmptyStage();
        for (int i = 1; i <= 7; i++) f.Stage.GenerateBlock(i);
        Equal(0, UObject.Spawns.Count);
        Equal(0, f.Stage.A1Number);
    }

    private static void UnknownBlock()
    {
        var f = new Fixture();
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(0);
        f.Stage.GenerateBlock(8);
        Equal(0, UObject.Spawns.Count);
        Equal(1, f.Stage.A1Number);
    }

    private static void ActionsOutsidePlay()
    {
        var f = new Fixture();
        // Positive inventory makes this specifically a play-state regression.
        // A zero count would also block spawning and could hide the missing state guard.
        f.Manager.stage101();
        f.Stage.CallVari();
        f.Stage.GenerateBlock(1);
        f.Stage.Slide();
        Equal(0, UObject.Spawns.Count);
        Equal(1, f.Stage.A1Number);
        Equal(0, Debug.Messages.Count);
        Equal(0, Physics.Directions.Count);
    }

    private static void RestartStage()
    {
        var f = new Fixture();
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(1);
        var oldRoots = UObject.Spawns.Select(spawn => spawn.Clone).ToArray();
        f.Stage.SelectStage(101);
        // Cleanup must target whole prefab roots; destroying only their controller
        // children would leave meshes or colliders behind in the real scene.
        foreach (var root in oldRoots)
        {
            Equal(false, root.activeSelf);
            Equal(true, UObject.Destroyed.Contains(root));
        }
        Equal(null, f.Stage.pre_GhostBlock);
        Equal(1, f.Stage.A1Number);
        f.Stage.GenerateBlock(1);
        Equal(4, UObject.Spawns.Count);
    }

    private static void MenuCleanup()
    {
        var f = new Fixture();
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(1);
        var oldRoots = UObject.Spawns.Select(spawn => spawn.Clone).ToArray();
        f.Stage.openMainMenu();
        foreach (var root in oldRoots) Equal(false, root.activeSelf);
        Equal(null, f.Stage.pre_GhostBlock);
        Equal(0, f.Stage.A1Number);
        Equal(false, f.Stage.A2Image.activeSelf);
        f.Stage.GenerateBlock(2);
        f.Stage.Slide();
        Equal(2, UObject.Spawns.Count);
        Equal(0, Debug.Messages.Count);
    }

    private static void RestoreGridColors()
    {
        // Use a non-white source material to catch hard-coded "reset to white" logic.
        var authored = new Color(.2f, .3f, .4f);
        var f = new Fixture(authored);
        f.Stage.SelectStage(101);
        Equal(Color.black, f.Stage.floor[26].GetComponent<MeshRenderer>().material.color);
        f.Stage.openMainMenu();
        Equal(authored, f.Stage.floor[26].GetComponent<MeshRenderer>().material.color);
    }

    private static void ConcurrentSpawn()
    {
        var f = new Fixture();
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(1);
        f.Stage.GenerateBlock(2);
        Equal(2, UObject.Spawns.Count);
        Equal(true, f.Stage.A2Image.activeSelf);
        Equal("1", f.Stage.A2Text.text);
    }

    private static void NextBlock()
    {
        var f = new Fixture();
        f.Stage.SelectStage(101);
        f.Stage.GenerateBlock(1);
        var previousGhost = f.Stage.pre_GhostBlock;
        var previousBlock = UObject.Spawns[1].Clone.GetComponentInChildren<Block>();
        // Simulate the placement transition directly; no collision engine is present.
        previousBlock.target.tag = "OnFixedBlock";
        f.Stage.GenerateBlock(2);
        Equal(4, UObject.Spawns.Count);
        Equal("FixedBlock", previousBlock.target.tag);
        Equal(false, previousBlock.enabled);
        Equal(false, previousGhost.activeSelf);
        Equal(true, UObject.Destroyed.Contains(previousGhost));
        var current = UObject.Spawns[3].Clone.GetComponentInChildren<Block>();
        Equal(current.gameObject, f.Stage.pre_GhostBlock.GetComponentInChildren<GhostBlock>().cBlock);
    }

    private static void ControllerOwnership(bool newFirst)
    {
        // Both orders are deliberate: Unity must not need a particular script execution
        // order for the new controller to survive or the old piece to remain stationary.
        var previous = ActiveBlock();
        previous.gameObject.tag = "OnFixedBlock";
        var current = ActiveBlock();
        current.Initialize(null);
        Input.Keys.Add(KeyCode.LeftArrow);
        Invoke(newFirst ? current : previous, "Update");
        Invoke(newFirst ? previous : current, "Update");
        Equal(Vector3.zero, previous.transform.parent.position);
        Equal(Vector3.right * .5f, current.transform.parent.position);
        Equal(previous.gameObject, previous.target);
        Equal(current.gameObject, current.target);
        Equal(true, current.enabled);
        Equal(false, UObject.Destroyed.Contains(current));
    }

    private static void BoundGhost()
    {
        // Put an unrelated active piece first in the search registry to catch a return
        // to global tag lookup instead of following the explicitly assigned controller.
        ActiveBlock();
        var owner = ActiveBlock();
        owner.transform.parent.position = new Vector3(3, 5, 1);
        var ghost = new GameObject().AddComponent<GhostBlock>();
        ghost.transform.parent = new GameObject().transform;
        owner.Initialize(ghost);
        Physics.AllResults.Enqueue(new[] { Hit("Floor") });
        Invoke(ghost, "Update");
        Equal(owner.gameObject, ghost.cBlock);
        Equal(new Vector3(3, 3, 1), ghost.transform.parent.position);
    }

    private static void EnqueueProjection(Fixture f, Func<int, int, RaycastHit?> result)
    {
        // Most scenarios need zero or one hit. Adapt them to the multi-hit API while
        // retaining a separate helper for occlusion and unrelated-hit scenarios.
        EnqueueProjectionHits(f, (grid, cell) =>
        {
            var hit = result(grid, cell);
            return hit.HasValue ? new[] { hit.Value } : new RaycastHit[0];
        });
    }

    private static void EnqueueProjectionHits(Fixture f, Func<int, int, RaycastHit[]> result)
    {
        // Match Slide's query sequence: floor, wall, front wall, then cell index.
        // Every cell receives a result, even if that result is an empty hit array.
        var grids = new[] { f.Manager.stage_Floor, f.Manager.stage_Wall, f.Manager.stage_FrontWall };
        for (int grid = 0; grid < grids.Length; grid++)
            for (int cell = 0; cell < 64; cell++) Physics.AllResults.Enqueue(result(grid, cell));
    }

    private static RaycastHit Hit(string tag)
    {
        // Tags and distances drive the gameplay decision; geometry is intentionally absent.
        var obj = new GameObject { tag = tag };
        return new RaycastHit { collider = obj.AddComponent<Collider>(), distance = 2f };
    }

    private static void MatchingProjection()
    {
        var f = new Fixture();
        f.Stage.SelectStage(101);
        var grids = new[] { f.Manager.stage_Floor, f.Manager.stage_Wall, f.Manager.stage_FrontWall };
        var tags = new[] { "Block", "OnFixedBlock", "FixedBlock" };
        // A correct puzzle remains correct across all three placement tag states.
        EnqueueProjection(f, (g, c) => grids[g][c] == 1 ? Hit(tags[g]) : (RaycastHit?)null);
        f.Stage.Slide();
        Equal("성공", Debug.Messages.Last());
    }

    private static void MissingProjection()
    {
        var f = new Fixture();
        f.Stage.SelectStage(101);
        // Required cells with no hit must fail, rather than being silently skipped.
        EnqueueProjection(f, (g, c) => null);
        f.Stage.Slide();
        Equal("실패", Debug.Messages.Last());
    }

    private static void UnexpectedProjection()
    {
        var f = new Fixture();
        f.StartEmptyStage();
        EnqueueProjection(f, (g, c) => g == 1 && c == 10 ? Hit("Block") : (RaycastHit?)null);
        f.Stage.Slide();
        Equal("실패", Debug.Messages.Last());
    }

    private static void NonBlockProjection()
    {
        var f = new Fixture();
        f.StartEmptyStage();
        f.Manager.stage_Floor[0] = 1;
        EnqueueProjection(f, (g, c) => g == 0 && c == 0 ? Hit("Floor") : (RaycastHit?)null);
        f.Stage.Slide();
        Equal("실패", Debug.Messages.Last());
    }

    private static void IndependentRaycasts()
    {
        var f = new Fixture();
        f.StartEmptyStage();
        f.Manager.stage_Floor[0] = 1;
        // Follow one valid hit with misses; no later cell may reuse the first result.
        EnqueueProjection(f, (g, c) => g == 0 && c == 0 ? Hit("Block") : (RaycastHit?)null);
        f.Stage.Slide();
        Equal("성공", Debug.Messages.Last());
    }

    private static void OccludedProjection()
    {
        var f = new Fixture();
        f.StartEmptyStage();
        f.Manager.stage_Floor[0] = 1;
        // RaycastAll does not promise hit order. A ghost and board surface appearing
        // before a placed piece must not change occupancy at this required cell.
        EnqueueProjectionHits(f, (g, c) => g == 0 && c == 0
            ? new[] { Hit("GhostBlock"), Hit("Floor"), Hit("FixedBlock") } : new RaycastHit[0]);
        f.Stage.Slide();
        Equal("성공", Debug.Messages.Last());
    }

    private static void ProjectionDirections()
    {
        var f = new Fixture();
        f.StartEmptyStage();
        // The stage object's own axis is a decoy. Each grid cell's local axis must win.
        f.Stage.transform.up = Vector3.forward;
        EnqueueProjection(f, (g, c) => null);
        f.Stage.Slide();
        Equal(192, Physics.Directions.Count);
        for (int i = 0; i < 192; i++)
        {
            Equal(i < 64 ? Vector3.up : i < 128 ? Vector3.forward : Vector3.right, Physics.Directions[i]);
        }
    }

    private static Block ActiveBlock()
    {
        // Match the production hierarchy: the controller is on a child, movement on
        // its parent root. Assign target explicitly because AddComponent runs no Awake.
        var block = new GameObject { tag = "Block" }.AddComponent<Block>();
        block.transform.parent = new GameObject("Root").transform;
        block.target = block.gameObject;
        return block;
    }

    private static void Movement()
    {
        var block = ActiveBlock();
        // Assert the existing world-axis mapping, including left input moving along +X.
        // This test intentionally leaves out a ghost so it isolates movement steps.
        var keys = new[] { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.A, KeyCode.S };
        var steps = new[] { Vector3.right, Vector3.left, Vector3.back, Vector3.forward, Vector3.down, Vector3.up };
        for (int i = 0; i < keys.Length; i++)
        {
            block.transform.parent.position = Vector3.zero;
            Input.Keys.Clear();
            Input.Keys.Add(keys[i]);
            block.ControlBlock(null);
            Equal(steps[i] * .5f, block.transform.parent.position);
            Equal(true, block.BlockRotation);
        }
        Input.Keys.Clear();
        block.transform.parent.position = Vector3.zero;
        block.ControlBlock("L");
        Equal(Vector3.right * .5f, block.transform.parent.position);
    }

    private static void Rotation()
    {
        var block = ActiveBlock();
        var keys = new[] { KeyCode.Z, KeyCode.X, KeyCode.C };
        var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
        for (int i = 0; i < keys.Length; i++)
        {
            Input.Keys.Clear();
            Input.Keys.Add(keys[i]);
            block.ControlBlock(null);
            Equal(axes[i] * 90f, block.transform.parent.LastRotation);
        }
    }

    private static void InactiveBlock()
    {
        var block = ActiveBlock();
        block.gameObject.tag = "FixedBlock";
        block.ControlBlock("L");
        Equal(Vector3.zero, block.transform.parent.position);
    }

    private static void Landing()
    {
        // Supply the pre-drop support distance and a collision adjustment separately.
        // CheckValid must account for the already-applied half-step exactly once.
        var block = ActiveBlock();
        var ghost = new GameObject { tag = "GhostBlock" }.AddComponent<GhostBlock>();
        ghost.AdjustY = new Vector3(0, .75f, 0);
        ghost.transform.parent = new GameObject().transform;
        block.Initialize(ghost);
        var floorHit = Hit("Floor");
        floorHit.distance = 1f;
        Physics.AllResults.Enqueue(new[] { floorHit });
        Invoke(ghost, "Update");
        block.CheckValid();
        Equal("OnFixedBlock", block.gameObject.tag);
        Equal(new Vector3(0, .25f, 0), block.transform.parent.position);
    }

    private static void MissingGhost()
    {
        var block = ActiveBlock();
        block.CheckValid();
        Equal("Block", block.gameObject.tag);
    }

    private static void MissingActiveBlock()
    {
        var ghost = new GameObject().AddComponent<GhostBlock>();
        Invoke(ghost, "Update");
        Equal(null, ghost.cBlock);
    }

    private static void GhostMovement()
    {
        var block = ActiveBlock();
        block.transform.parent.position = new Vector3(1, 4, 2);
        block.BlockRotation = true;
        var ghost = new GameObject().AddComponent<GhostBlock>();
        ghost.transform.parent = new GameObject().transform;
        ghost.AdjustY = Vector3.up;
        ghost.pre_AdjustY = Vector3.up;
        block.Initialize(ghost);
        Physics.AllResults.Enqueue(new[] { Hit("Floor") });
        Invoke(ghost, "Update");
        Equal(false, block.BlockRotation);
        Equal(Vector3.zero, ghost.AdjustY);
        Equal(Vector3.zero, ghost.pre_AdjustY);
        Equal(2f, ghost.distanceY);
        Equal(new Vector3(1, 2, 2), ghost.transform.parent.position);
    }

    private static void NearestSupport()
    {
        var block = ActiveBlock();
        block.transform.parent.position = new Vector3(1, 4, 2);
        var ghost = new GameObject().AddComponent<GhostBlock>();
        ghost.transform.parent = new GameObject().transform;
        block.Initialize(ghost);
        var distant = Hit("Floor");
        distant.distance = 5f;
        var nearer = Hit("FixedBlock");
        nearer.distance = 3f;
        // The first array entry is not a support and the first support is not nearest.
        // Neither array order nor an unrelated piece should determine the landing height.
        Physics.AllResults.Enqueue(new[] { Hit("GhostBlock"), distant, nearer, Hit("Block") });
        Invoke(ghost, "Update");
        Equal(true, ghost.HasLandingPoint);
        Equal(3f, ghost.distanceY);
        Equal(new Vector3(1, 1, 2), ghost.transform.parent.position);
    }

    private static void LostSupport()
    {
        var block = ActiveBlock();
        var ghost = new GameObject().AddComponent<GhostBlock>();
        ghost.transform.parent = new GameObject().transform;
        block.Initialize(ghost);
        Physics.AllResults.Enqueue(new[] { Hit("Floor") });
        Invoke(ghost, "Update");
        Equal(true, ghost.HasLandingPoint);
        // A later miss invalidates the old distance instead of fixing the block in air.
        Physics.AllResults.Enqueue(new RaycastHit[0]);
        Invoke(ghost, "Update");
        Equal(false, ghost.HasLandingPoint);
        Equal(0f, ghost.distanceY);
        block.CheckValid();
        Equal("Block", block.gameObject.tag);
    }

    private static void SameFrameSupport()
    {
        var block = ActiveBlock();
        var ghost = new GameObject().AddComponent<GhostBlock>();
        ghost.transform.parent = new GameObject().transform;
        block.Initialize(ghost);
        Physics.AllResults.Enqueue(new[] { Hit("Floor") });
        Invoke(ghost, "Update");
        Equal(true, ghost.HasLandingPoint);
        ghost.AdjustY = Vector3.up;
        // Refresh after lateral movement/rotation but before the down step. The old
        // pose had support; the new pose does not, regardless of ghost Update timing.
        Input.Keys.UnionWith(new[] { KeyCode.LeftArrow, KeyCode.Z, KeyCode.A });
        Physics.AllResults.Enqueue(new RaycastHit[0]);
        block.ControlBlock(null);
        Equal(false, ghost.HasLandingPoint);
        Equal(Vector3.zero, ghost.AdjustY);
        Equal("Block", block.gameObject.tag);
        Equal(new Vector3(.5f, -.5f, 0), block.transform.parent.position);
        Equal(Vector3.right * 90f, block.transform.parent.LastRotation);
    }

    private static void LandingBoundary()
    {
        // The placement comparison is strict: at a pre-drop distance of exactly 1.0,
        // the remaining 0.5 does not yet satisfy the "less than one step" condition.
        foreach (float distance in new[] { .5f, .99f, 1f })
        {
            var block = ActiveBlock();
            var ghost = new GameObject().AddComponent<GhostBlock>();
            block.Initialize(ghost);
            var hit = Hit("Floor");
            hit.distance = distance;
            Physics.AllResults.Enqueue(new[] { hit });
            Input.Keys.Clear();
            Input.Keys.Add(KeyCode.A);
            block.ControlBlock(null);
            Equal(distance < 1f ? "OnFixedBlock" : "Block", block.gameObject.tag);
            Equal(new Vector3(0, distance < 1f ? -distance : -.5f, 0), block.transform.parent.position);
        }
    }

    /// <summary>
    /// Supplies only the scene references used by gameplay decisions. It preserves
    /// the real board normals and child-controller layout without importing assets.
    /// </summary>
    internal sealed class Fixture
    {
        public readonly stage Stage;
        public readonly StageManager Manager;

        public Fixture(Color? authoredFloorColor = null, Action<stage, StageManager> configure = null)
        {
            Manager = new GameObject("StageManager").AddComponent<StageManager>();
            Stage = new GameObject("Stage").AddComponent<stage>();
            Stage.playUI = new GameObject("PlayUI");
            Stage.mainMenu = new GameObject("MainMenu");
            Stage.Origin = new Vector3(1, 4, 2);
            Stage.GhostOrigin = new Vector3(1, 0, 2);
            Stage.floor = GridObjects(Vector3.down);
            Stage.wall = GridObjects(Vector3.back);
            Stage.frontwall = GridObjects(Vector3.left);
            // Author a distinct material before Awake captures the original board colors.
            if (authoredFloorColor.HasValue)
                Stage.floor[26].GetComponent<MeshRenderer>().material.color = authoredFloorColor.Value;
            Stage.Location = Enumerable.Range(0, 7).Select(i =>
            {
                var location = new GameObject().AddComponent<RectTransform>();
                location.position = new Vector3(i * 10, 0, 0);
                return location;
            }).ToArray();
            foreach (string name in new[] { "A1", "A2", "I", "O", "P", "T", "Z" })
            {
                // Keep the public field names in this setup: renaming a serialized
                // reference should be visible in tests as well as in Unity scenes.
                typeof(stage).GetField(name + "Image").SetValue(Stage, new GameObject());
                typeof(stage).GetField(name + "Text").SetValue(Stage, new GameObject().AddComponent<Text>());
                var blockRoot = new GameObject(name);
                var blockChild = new GameObject { tag = "Block" };
                blockChild.transform.parent = blockRoot.transform;
                blockChild.AddComponent<Block>();
                var ghostRoot = new GameObject("Ghost" + name);
                var ghostChild = new GameObject { tag = "GhostBlock" };
                ghostChild.transform.parent = ghostRoot.transform;
                ghostChild.AddComponent<GhostBlock>();
                typeof(stage).GetField(name + "Block").SetValue(Stage, blockRoot);
                typeof(stage).GetField("Ghost" + name + "Block").SetValue(Stage, ghostRoot);
            }
            // Extension tests author the same optional references an Inspector would
            // provide, before the facade composes its runtime modules in Awake.
            if (configure != null) configure(Stage, Manager);
            Invoke(Stage, "Awake");
        }

        public void StartEmptyStage()
        {
            // Enter through the real menu flow, then clear puzzle data to isolate
            // occupancy tests from stage 101's nonempty target and inventory.
            Stage.SelectStage(101);
            Manager.ResetStage();
            Stage.CallVari();
        }

        private static GameObject[] GridObjects(Vector3 up)
        {
            return Enumerable.Range(0, 64).Select(i =>
            {
                var cell = new GameObject();
                cell.transform.up = up;
                cell.AddComponent<MeshRenderer>();
                return cell;
            }).ToArray();
        }
    }
}
