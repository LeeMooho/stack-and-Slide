using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Keep serialized field and public method names stable for existing scenes and buttons.
/// <summary>
/// Coordinates menu state, puzzle inventory, spawned prefab roots, and projection checks.
/// The scene supplies its cell arrays, UI objects, and prefab bindings through the Inspector.
/// </summary>
public class stage : MonoBehaviour
{
    public GameObject[] floor;
    public GameObject[] wall;
    public GameObject[] frontwall;
    public RectTransform[] Location;
    public GameObject playUI;
    public GameObject mainMenu;

    // The separate public bindings are intentional: renaming or replacing them with a new
    // serialized structure would require migrating the existing scene's saved references.
    public GameObject A1Image;
    public GameObject A2Image;
    public GameObject IImage;
    public GameObject OImage;
    public GameObject PImage;
    public GameObject TImage;
    public GameObject ZImage;

    public GameObject A1Block;
    public GameObject A2Block;
    public GameObject IBlock;
    public GameObject OBlock;
    public GameObject PBlock;
    public GameObject TBlock;
    public GameObject ZBlock;
    public Vector3 Origin;
    public Vector3 GhostOrigin;

    public GameObject GhostA1Block;
    public GameObject GhostA2Block;
    public GameObject GhostIBlock;
    public GameObject GhostOBlock;
    public GameObject GhostPBlock;
    public GameObject GhostTBlock;
    public GameObject GhostZBlock;
    public GameObject pre_GhostBlock;

    public Text A1Text;
    public Text A2Text;
    public Text IText;
    public Text OText;
    public Text PText;
    public Text TText;
    public Text ZText;

    public int A1Number;
    private int A2Number;
    private int INumber;
    private int ONumber;
    private int PNumber;
    private int TNumber;
    private int ZNumber;
    private bool isPlaying;

    private int[] stage_Floor;
    private int[] stage_Wall;
    private int[] stage_FrontWall;
    private StageManager stageManager;
    private Block activeBlock;
    // Track outer prefab roots, not just their controller children. Cleanup must also remove
    // child renderers/colliders and any empty roots left after a preview child is destroyed.
    private readonly List<GameObject> spawnedRoots = new List<GameObject>();
    private Color[] floorColors;
    private Color[] wallColors;
    private Color[] frontWallColors;

    private const float MaxDistance = 8f;

    private void Awake()
    {
        playUI.SetActive(false);
        mainMenu.SetActive(true);
        stageManager = GameObject.Find("StageManager").GetComponent<StageManager>();
        // Cache authored colors before any puzzle pattern is painted over the grid.
        floorColors = CaptureColors(floor);
        wallColors = CaptureColors(wall);
        frontWallColors = CaptureColors(frontwall);
        stageManager.ResetStage();
        CallVari();
        UpdateBlockInventory();
    }

    /// <summary>Compacts remaining piece icons into the scene's available display slots.</summary>
    private void UpdateBlockInventory()
    {
        int locationIndex = 0;
        // Preserve the authored display order, which intentionally puts Z before O/P/T.
        // Only visible icons consume slots; stage 101 contains six available piece types.
        UpdateBlockImage(A1Image, A1Text, A1Number, ref locationIndex);
        UpdateBlockImage(A2Image, A2Text, A2Number, ref locationIndex);
        UpdateBlockImage(IImage, IText, INumber, ref locationIndex);
        UpdateBlockImage(ZImage, ZText, ZNumber, ref locationIndex);
        UpdateBlockImage(OImage, OText, ONumber, ref locationIndex);
        UpdateBlockImage(PImage, PText, PNumber, ref locationIndex);
        UpdateBlockImage(TImage, TText, TNumber, ref locationIndex);
    }

    /// <summary>Hides an exhausted type or places its count in the next visible UI slot.</summary>
    private void UpdateBlockImage(GameObject image, Text text, int remaining, ref int locationIndex)
    {
        image.SetActive(remaining > 0);
        if (remaining <= 0)
        {
            return;
        }

        image.transform.position = Location[locationIndex].position;
        text.text = remaining.ToString();
        locationIndex++;
    }

    /// <summary>
    /// Starts a supported puzzle from a clean session, including when replaying the same stage.
    /// Unknown button identifiers leave the current session untouched.
    /// </summary>
    public void SelectStage(int stageNumber)
    {
        if (stageNumber != 101)
        {
            return;
        }

        // Clear physical pieces and restore the outgoing pattern before loading new data.
        // Resetting only the counts would let an old solution remain on a fresh board.
        ResetStageState();
        playUI.SetActive(true);
        mainMenu.SetActive(false);
        stageManager.stage101();
        CallVari();
        isPlaying = true;
        FloorColor();
        UpdateBlockInventory();
    }

    /// <summary>Paints the occupied silhouette cells on all three projection surfaces.</summary>
    private void FloorColor()
    {
        if (!isPlaying)
        {
            return;
        }

        ColorGrid(floor, stage_Floor);
        ColorGrid(wall, stage_Wall);
        ColorGrid(frontwall, stage_FrontWall);
    }

    /// <summary>Highlights required cells without modifying shared material assets.</summary>
    private static void ColorGrid(GameObject[] cells, int[] pattern)
    {
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == 1)
            {
                // material gives this renderer its own instance. Changing sharedMaterial
                // here would also recolor other cells that use the same authored asset.
                cells[i].GetComponent<MeshRenderer>().material.color = Color.black;
            }
        }
    }

    /// <summary>Reads the original cell colors without creating renderer material copies.</summary>
    private static Color[] CaptureColors(GameObject[] cells)
    {
        Color[] colors = new Color[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            colors[i] = cells[i].GetComponent<MeshRenderer>().sharedMaterial.color;
        }
        return colors;
    }

    /// <summary>Restores only the cells painted by the outgoing puzzle pattern.</summary>
    private static void RestoreColors(GameObject[] cells, int[] pattern, Color[] colors)
    {
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == 1)
            {
                cells[i].GetComponent<MeshRenderer>().material.color = colors[i];
            }
        }
    }

    /// <summary>Retires every piece and preview created during this stage session.</summary>
    private void ClearSpawnedObjects()
    {
        foreach (GameObject root in spawnedRoots)
        {
            if (root != null)
            {
                // Destroy is deferred; hide old colliders before another piece can spawn.
                // This also stops old pieces from affecting ray queries in the same frame.
                root.SetActive(false);
                Destroy(root);
            }
        }
        spawnedRoots.Clear();
        activeBlock = null;
        pre_GhostBlock = null;
    }

    /// <summary>
    /// Returns gameplay data and visuals to their menu baseline, without changing menu panels.
    /// Shared by stage selection and menu return so those paths cannot leave different state.
    /// </summary>
    private void ResetStageState()
    {
        ClearSpawnedObjects();
        // The old arrays identify which colors need restoring. Do this before ResetStage
        // replaces them and CallVari rebinds the controller to fresh, empty patterns.
        RestoreColors(floor, stage_Floor, floorColors);
        RestoreColors(wall, stage_Wall, wallColors);
        RestoreColors(frontwall, stage_FrontWall, frontWallColors);
        stageManager.ResetStage();
        CallVari();
        isPlaying = false;
        UpdateBlockInventory();
    }

    private void OnDestroy()
    {
        // Spawned roots are not children of the stage object, so explicitly retire them
        // if the coordinator itself is removed rather than relying on its hierarchy.
        ClearSpawnedObjects();
    }

    /// <summary>
    /// Handles the stable numeric piece IDs stored in the scene's button events.
    /// The adjustments preserve each mesh's authored pivot relative to the shared spawn point.
    /// </summary>
    public void GenerateBlock(int blockNumber)
    {
        switch (blockNumber)
        {
            case 1:
                SpawnBlock(A1Block, GhostA1Block, Vector3.zero, ref A1Number);
                break;
            case 2:
                SpawnBlock(A2Block, GhostA2Block, new Vector3(0.5f, 0.5f, 0.5f), ref A2Number);
                break;
            case 3:
                SpawnBlock(IBlock, GhostIBlock, new Vector3(0, 0, 0.5f), ref INumber);
                break;
            case 4:
                SpawnBlock(OBlock, GhostOBlock, new Vector3(0.5f, 0, 0), ref ONumber);
                break;
            case 5:
                SpawnBlock(PBlock, GhostPBlock, new Vector3(0, 0.5f, 0.5f), ref PNumber);
                break;
            case 6:
                SpawnBlock(TBlock, GhostTBlock, new Vector3(0, 0.5f, 0), ref TNumber);
                break;
            case 7:
                SpawnBlock(ZBlock, GhostZBlock, new Vector3(0, 0.5f, 0.5f), ref ZNumber);
                break;
        }
    }

    /// <summary>
    /// Creates one matching piece/preview pair and consumes inventory only when spawning is allowed.
    /// At most one piece may be under active input control at a time.
    /// </summary>
    private void SpawnBlock(GameObject blockPrefab, GameObject ghostPrefab, Vector3 adjustment, ref int remaining)
    {
        if (!isPlaying || remaining <= 0 || (activeBlock != null && activeBlock.IsActive))
        {
            return;
        }

        if (activeBlock != null)
        {
            // The previous piece has landed. Make its tag a support surface before querying
            // the next preview, and disable its controller rather than transferring ownership.
            activeBlock.CompletePlacement();
        }
        if (pre_GhostBlock != null)
        {
            // The old preview is no longer useful once a new piece is selected. Deactivate
            // the whole root immediately, even if its visible child already destroyed itself.
            pre_GhostBlock.SetActive(false);
            Destroy(pre_GhostBlock);
        }

        pre_GhostBlock = Instantiate(ghostPrefab, GhostOrigin + adjustment, Quaternion.identity);
        spawnedRoots.Add(pre_GhostBlock);
        GameObject blockRoot = Instantiate(blockPrefab, Origin + adjustment, Quaternion.identity);
        spawnedRoots.Add(blockRoot);
        // Controllers live on geometry children, while the instantiated objects are roots.
        // Explicit binding avoids tag-search races and does not depend on Start/Update order.
        activeBlock = blockRoot.GetComponentInChildren<Block>();
        activeBlock.Initialize(pre_GhostBlock.GetComponentInChildren<GhostBlock>());
        remaining--;
        UpdateBlockInventory();
    }

    /// <summary>
    /// Reports whether current block occupancy matches every cell of all three silhouettes.
    /// This checks the current scene, including a moving Block, rather than requiring all pieces to be fixed.
    /// </summary>
    public void Slide()
    {
        if (!isPlaying)
        {
            return;
        }

        int mismatches = CountProjectionMismatches(floor, stage_Floor)
            + CountProjectionMismatches(wall, stage_Wall)
            + CountProjectionMismatches(frontwall, stage_FrontWall);

        Debug.Log(mismatches == 0 ? "성공" : "실패");
    }

    /// <summary>Counts missing and unexpected occupancy along one grid's inward rays.</summary>
    private static int CountProjectionMismatches(GameObject[] cells, int[] pattern)
    {
        int mismatches = 0;
        for (int i = 0; i < pattern.Length; i++)
        {
            // Each grid is rotated in the scene; its local down points into the board.
            // Playable blocks and ghosts both use triggers, so filter all hits by tag.
            // Using the stage's world up vector would make both wall checks vertical.
            // Looking through all hits prevents a ghost or grid collider from hiding a piece.
            RaycastHit[] hits = Physics.RaycastAll(cells[i].transform.position,
                -cells[i].transform.up, MaxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            bool hasBlock = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.CompareTag("Block") || hit.collider.CompareTag("OnFixedBlock")
                    || hit.collider.CompareTag("FixedBlock"))
                {
                    hasBlock = true;
                    break;
                }
            }

            // Both directions matter: required cells need a hit, and empty cells must not
            // contain a piece. A ray miss is therefore a failure when the pattern expects 1.
            if ((pattern[i] == 1) != hasBlock)
            {
                mismatches++;
            }
        }

        return mismatches;
    }

    /// <summary>Leaves gameplay and restores a clean board before showing the main menu.</summary>
    public void openMainMenu()
    {
        ResetStageState();
        playUI.SetActive(false);
        mainMenu.SetActive(true);
    }

    /// <summary>
    /// Copies available piece counts and binds the current pattern arrays from StageManager.
    /// Counts are consumed locally during play; patterns remain shared references for this session.
    /// </summary>
    public void CallVari()
    {
        A1Number = stageManager.A1Number;
        A2Number = stageManager.A2Number;
        INumber = stageManager.INumber;
        ONumber = stageManager.ONumber;
        PNumber = stageManager.PNumber;
        TNumber = stageManager.TNumber;
        ZNumber = stageManager.ZNumber;

        stage_Floor = stageManager.stage_Floor;
        stage_Wall = stageManager.stage_Wall;
        stage_FrontWall = stageManager.stage_FrontWall;
    }
}
