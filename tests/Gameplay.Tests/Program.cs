using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

internal static class Program
{
    private static int failures;
    private static int total;

    private static int Main()
    {
        Run("Reset clears inventory and creates independent empty grids", ResetStage);
        Run("Stage 101 preserves its inventory and all grid cells", Stage101);
        Run("Stage selection updates menus, colors and inventory slots", SelectStage);
        Run("Unknown stage selection leaves the menus unchanged", UnknownStage);
        for (int i = 1; i <= 7; i++)
        {
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
        Run("Exact projection accepts active, newly fixed and fixed blocks", MatchingProjection);
        Run("A missing required block fails the projection", MissingProjection);
        Run("An unexpected block fails the projection", UnexpectedProjection);
        Run("Non-block hits cannot satisfy a required cell", NonBlockProjection);
        Run("Ghost and grid hits cannot hide a playable block", OccludedProjection);
        Run("A ray miss cannot reuse an earlier ray hit", IndependentRaycasts);
        Run("Projection casts inward from each rotated grid plane", ProjectionDirections);
        Run("Block keyboard and UI movement preserve the half-cell steps", Movement);
        Run("Block keyboard rotations preserve all three axes", Rotation);
        Run("Inactive blocks do not move", InactiveBlock);
        Run("Landing uses the ghost distance and vertical adjustment", Landing);
        Run("A missing ghost does not cause a null-reference exception", MissingGhost);
        Run("A ghost without an active block tolerates lifecycle callbacks", MissingActiveBlock);
        Run("Ghost rotation resets offsets and follows the current target", GhostMovement);
        Console.WriteLine((total - failures) + "/" + total + " isolated gameplay tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        total++;
        GameObject.All.Clear();
        UObject.Spawns.Clear();
        UObject.Destroyed.Clear();
        Physics.Results.Clear();
        Physics.AllResults.Clear();
        Physics.Directions.Clear();
        Input.Keys.Clear();
        Debug.Messages.Clear();
        try
        {
            test();
            Equal(0, Physics.Results.Count);
            Equal(0, Physics.AllResults.Count);
            Console.WriteLine("PASS " + name);
        }
        catch (Exception error)
        {
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

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    private static int[] Counts(StageManager manager)
    {
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
        Equal(0, manager.stage_Wall[0]);
        Equal(0, manager.stage_FrontWall[0]);
        Equal(false, ReferenceEquals(previousFloor, manager.stage_Floor));
    }

    private static void Stage101()
    {
        var manager = new StageManager();
        manager.stage101();
        Equal("1,1,1,0,1,1,1", string.Join(",", Counts(manager)));
        Equal("00000000/00000000/00000000/00111100/00111100/00111100/00000000/00000000", Grid(manager.stage_Floor));
        Equal("00000000/00000000/00000000/00000000/00010000/00011000/00011000/00111000", Grid(manager.stage_Wall));
        Equal("00000000/00000000/00000000/00000000/00100000/00100100/00111100/00111100", Grid(manager.stage_FrontWall));
    }

    private static string Grid(int[] cells)
    {
        return string.Join("/", Enumerable.Range(0, 8).Select(row => string.Concat(cells.Skip(row * 8).Take(8))));
    }

    private static void SelectStage()
    {
        var f = new Fixture();
        f.Stage.SelectStage(101);
        Equal(true, f.Stage.playUI.activeSelf);
        Equal(false, f.Stage.mainMenu.activeSelf);
        Equal(false, f.Stage.OImage.activeSelf);
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
        f.Manager.stage101();
        f.Manager.ONumber = 1;
        f.Stage.CallVari();
        var names = new[] { "A1", "A2", "I", "O", "P", "T", "Z" };
        var offsets = new[] { Vector3.zero, new Vector3(.5f, .5f, .5f), new Vector3(0, 0, .5f),
            new Vector3(.5f, 0, 0), new Vector3(0, .5f, .5f), new Vector3(0, .5f, 0), new Vector3(0, .5f, .5f) };
        string name = names[number - 1];
        f.Stage.GenerateBlock(number);
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
        ActiveBlock();
        var owner = ActiveBlock();
        owner.transform.parent.position = new Vector3(3, 5, 1);
        var ghost = new GameObject().AddComponent<GhostBlock>();
        ghost.transform.parent = new GameObject().transform;
        owner.Initialize(ghost);
        Physics.Results.Enqueue(Hit("Floor"));
        Invoke(ghost, "Update");
        Equal(owner.gameObject, ghost.cBlock);
        Equal(new Vector3(3, 3, 1), ghost.transform.parent.position);
    }

    private static void EnqueueProjection(Fixture f, Func<int, int, RaycastHit?> result)
    {
        EnqueueProjectionHits(f, (grid, cell) =>
        {
            var hit = result(grid, cell);
            return hit.HasValue ? new[] { hit.Value } : new RaycastHit[0];
        });
    }

    private static void EnqueueProjectionHits(Fixture f, Func<int, int, RaycastHit[]> result)
    {
        var grids = new[] { f.Manager.stage_Floor, f.Manager.stage_Wall, f.Manager.stage_FrontWall };
        for (int grid = 0; grid < grids.Length; grid++)
            for (int cell = 0; cell < 64; cell++) Physics.AllResults.Enqueue(result(grid, cell));
    }

    private static RaycastHit Hit(string tag)
    {
        var obj = new GameObject { tag = tag };
        return new RaycastHit { collider = obj.AddComponent<Collider>(), distance = 2f };
    }

    private static void MatchingProjection()
    {
        var f = new Fixture();
        f.Stage.SelectStage(101);
        var grids = new[] { f.Manager.stage_Floor, f.Manager.stage_Wall, f.Manager.stage_FrontWall };
        var tags = new[] { "Block", "OnFixedBlock", "FixedBlock" };
        EnqueueProjection(f, (g, c) => grids[g][c] == 1 ? Hit(tags[g]) : (RaycastHit?)null);
        f.Stage.Slide();
        Equal("성공", Debug.Messages.Last());
    }

    private static void MissingProjection()
    {
        var f = new Fixture();
        f.Stage.SelectStage(101);
        EnqueueProjection(f, (g, c) => null);
        f.Stage.Slide();
        Equal("실패", Debug.Messages.Last());
    }

    private static void UnexpectedProjection()
    {
        var f = new Fixture();
        EnqueueProjection(f, (g, c) => g == 1 && c == 10 ? Hit("Block") : (RaycastHit?)null);
        f.Stage.Slide();
        Equal("실패", Debug.Messages.Last());
    }

    private static void NonBlockProjection()
    {
        var f = new Fixture();
        f.Manager.stage_Floor[0] = 1;
        EnqueueProjection(f, (g, c) => g == 0 && c == 0 ? Hit("Floor") : (RaycastHit?)null);
        f.Stage.Slide();
        Equal("실패", Debug.Messages.Last());
    }

    private static void IndependentRaycasts()
    {
        var f = new Fixture();
        f.Manager.stage_Floor[0] = 1;
        EnqueueProjection(f, (g, c) => g == 0 && c == 0 ? Hit("Block") : (RaycastHit?)null);
        f.Stage.Slide();
        Equal("성공", Debug.Messages.Last());
    }

    private static void OccludedProjection()
    {
        var f = new Fixture();
        f.Manager.stage_Floor[0] = 1;
        EnqueueProjectionHits(f, (g, c) => g == 0 && c == 0
            ? new[] { Hit("GhostBlock"), Hit("Floor"), Hit("FixedBlock") } : new RaycastHit[0]);
        f.Stage.Slide();
        Equal("성공", Debug.Messages.Last());
    }

    private static void ProjectionDirections()
    {
        var f = new Fixture();
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
        var block = new GameObject { tag = "Block" }.AddComponent<Block>();
        block.transform.parent = new GameObject("Root").transform;
        block.target = block.gameObject;
        return block;
    }

    private static void Movement()
    {
        var block = ActiveBlock();
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
        var block = ActiveBlock();
        var ghost = new GameObject { tag = "GhostBlock" }.AddComponent<GhostBlock>();
        ghost.distanceY = 1f;
        ghost.AdjustY = new Vector3(0, .75f, 0);
        block.Initialize(ghost);
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
        Physics.Results.Enqueue(Hit("Floor"));
        Invoke(ghost, "Update");
        Equal(false, block.BlockRotation);
        Equal(Vector3.zero, ghost.AdjustY);
        Equal(Vector3.zero, ghost.pre_AdjustY);
        Equal(2f, ghost.distanceY);
        Equal(new Vector3(1, 2, 2), ghost.transform.parent.position);
    }

    private sealed class Fixture
    {
        public readonly stage Stage;
        public readonly StageManager Manager;

        public Fixture()
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
            Stage.Location = Enumerable.Range(0, 7).Select(i =>
            {
                var location = new GameObject().AddComponent<RectTransform>();
                location.position = new Vector3(i * 10, 0, 0);
                return location;
            }).ToArray();
            foreach (string name in new[] { "A1", "A2", "I", "O", "P", "T", "Z" })
            {
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
            Invoke(Stage, "Awake");
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
