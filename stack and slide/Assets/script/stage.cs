using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Keep serialized field and public method names stable for existing scenes and buttons.
/// <summary>
/// Preserves the scene-facing API while composing independent gameplay modules.
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

    // Optional extension assets and UI entries. Existing scenes continue to use their
    // seven saved bindings; new IDs do not need another field or switch branch here.
    public BlockDefinition[] AdditionalBlocks = new BlockDefinition[0];
    public InventoryBinding[] AdditionalInventoryBindings = new InventoryBinding[0];

    public int A1Number;
    private int A2Number;
    private int INumber;
    private int ONumber;
    private int PNumber;
    private int TNumber;
    private int ZNumber;
    private bool isPlaying;
    private bool isTransitioning;
    private bool isGenerating;
    private int sessionVersion;

    private int[] stage_Floor;
    private int[] stage_Wall;
    private int[] stage_FrontWall;
    private StageManager stageManager;
    private BlockCatalog blockCatalog;
    private readonly BlockInventory inventory = new BlockInventory();
    private BlockSpawner spawner;
    private InventoryPresenter inventoryPresenter;
    private ProjectionGrid floorGrid;
    private ProjectionGrid wallGrid;
    private ProjectionGrid frontWallGrid;
    private IProjectionProbe projectionProbe = new PhysicsProjectionProbe();

    public bool IsPlaying { get { return isPlaying; } }
    public int CurrentStageNumber { get; private set; }

    /// <summary>
    /// Scoring, progress and result UI can subscribe without changing board physics or
    /// parsing log text. Evaluation does not automatically end the current session.
    /// </summary>
    public event Action<ProjectionResult> PuzzleEvaluated;

    /// <summary>Allows a different occupancy source without replacing grid or UI logic.</summary>
    public IProjectionProbe ProjectionProbe
    {
        get { return projectionProbe; }
        set
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            projectionProbe = value;
        }
    }

    private void Awake()
    {
        playUI.SetActive(false);
        mainMenu.SetActive(true);
        stageManager = GameObject.Find("StageManager").GetComponent<StageManager>();

        // Composition happens once. The module classes own their validation and state;
        // this adapter keeps the Inspector references and button methods stable.
        blockCatalog = new BlockCatalog(CreateLegacyBlockSpecs(), AdditionalBlocks);
        spawner = new BlockSpawner(Origin, GhostOrigin);
        var bindings = CreateLegacyInventoryBindings();
        if (AdditionalInventoryBindings != null) bindings.AddRange(AdditionalInventoryBindings);
        inventoryPresenter = new InventoryPresenter(bindings, Location);
        floorGrid = new ProjectionGrid(floor);
        wallGrid = new ProjectionGrid(wall);
        frontWallGrid = new ProjectionGrid(frontwall);
        stageManager.ResetStage();
        ReloadInventoryFromManager();
        UpdateBlockInventory();
    }

    /// <summary>
    /// Resolves and validates the incoming data before touching the current session.
    /// Adding a StageDefinition asset is sufficient to support another button ID.
    /// </summary>
    public void SelectStage(int stageNumber)
    {
        // SetActive can invoke user components synchronously. Finish one transition
        // before accepting another so a nested UI callback cannot overwrite half a session.
        if (isTransitioning) return;
        StageLayout layout;
        if (!stageManager.TryGetStageLayout(stageNumber, out layout)) return;
        ValidateStageBindings(layout);

        isTransitioning = true;
        try
        {
            ResetStageState();
            // Load the exact immutable snapshot already checked, rather than resolving an
            // asset again after UI/cleanup callbacks may have changed authoring data.
            stageManager.LoadLayout(layout);
            ReloadInventoryFromManager();
            CurrentStageNumber = stageNumber;
            floorGrid.ShowPattern(stage_Floor);
            wallGrid.ShowPattern(stage_Wall);
            frontWallGrid.ShowPattern(stage_FrontWall);
            UpdateBlockInventory();
            playUI.SetActive(true);
            mainMenu.SetActive(false);
            isPlaying = true;
        }
        finally
        {
            isTransitioning = false;
        }
    }

    private void ValidateStageBindings(StageLayout layout)
    {
        if (layout.CellCount != floorGrid.CellCount || layout.CellCount != wallGrid.CellCount
            || layout.CellCount != frontWallGrid.CellCount)
            throw new InvalidOperationException("Stage patterns must match all three scene grids.");

        Dictionary<int, int> stock = layout.GetInitialInventory();
        foreach (KeyValuePair<int, int> entry in stock)
        {
            if (entry.Value == 0) continue;
            BlockSpec specification;
            if (!blockCatalog.TryGet(entry.Key, out specification))
                throw new InvalidOperationException("No block definition exists for inventory ID " + entry.Key + ".");
            specification.ValidatePrefabs();
        }
        inventoryPresenter.ValidateStock(stock);
    }

    public void GenerateBlock(int blockNumber)
    {
        if (!isPlaying || isTransitioning || isGenerating) return;
        SynchronizeLegacyInventory();
        BlockSpec specification;
        if (!blockCatalog.TryGet(blockNumber, out specification) || inventory.GetRemaining(blockNumber) <= 0)
            return;

        int expectedSession = sessionVersion;
        isGenerating = true;
        try
        {
            // A blocked, failed or canceled spawn must not consume stock. A prefab may
            // return to the menu or select a new stage from its synchronous callbacks.
            if (!spawner.TrySpawn(specification) || expectedSession != sessionVersion || !isPlaying)
                return;
            // All stock writers either hold this guard or cancel the pending spawn.
            // An unsuccessful debit therefore signals a broken coordinator invariant.
            if (!inventory.TryConsume(blockNumber))
                throw new InvalidOperationException("A completed spawn must have available inventory.");
            pre_GhostBlock = spawner.CurrentGhost;
            MirrorLegacyCounts();
            UpdateBlockInventory();
        }
        finally
        {
            isGenerating = false;
        }
    }

    public int GetRemainingBlocks(int blockNumber)
    {
        return inventory.GetRemaining(blockNumber);
    }

    public void Slide()
    {
        if (!isPlaying || isTransitioning) return;
        int expectedSession = sessionVersion;
        int evaluatedStage = CurrentStageNumber;
        // Capture the selected inputs together. A replaceable probe may invoke feature
        // code; a session change during its queries makes this evaluation obsolete.
        int[] floorPattern = stage_Floor;
        int[] wallPattern = stage_Wall;
        int[] frontWallPattern = stage_FrontWall;
        IProjectionProbe probe = projectionProbe;
        var result = new ProjectionResult(
            floorGrid.CountMismatches(floorPattern, probe),
            wallGrid.CountMismatches(wallPattern, probe),
            frontWallGrid.CountMismatches(frontWallPattern, probe), evaluatedStage);
        if (expectedSession != sessionVersion) return;
        Debug.Log(result.IsSolved ? "성공" : "실패");
        Action<ProjectionResult> listeners = PuzzleEvaluated;
        if (listeners != null) listeners(result);
    }

    public void openMainMenu()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        try
        {
            ResetStageState();
            playUI.SetActive(false);
            mainMenu.SetActive(true);
        }
        finally
        {
            isTransitioning = false;
        }
    }

    private void ResetStageState()
    {
        // Revoke play before retiring roots: their OnDisable callbacks must not start
        // another generation while the previous session is still being dismantled.
        isPlaying = false;
        sessionVersion++;
        spawner.Clear();
        pre_GhostBlock = null;
        floorGrid.ClearPattern();
        wallGrid.ClearPattern();
        frontWallGrid.ClearPattern();
        stageManager.ResetStage();
        ReloadInventoryFromManager();
        CurrentStageNumber = 0;
        UpdateBlockInventory();
    }

    private void UpdateBlockInventory()
    {
        bool wasTransitioning = isTransitioning;
        isTransitioning = true;
        try
        {
            // Present one complete stock snapshot before accepting UI lifecycle actions.
            inventoryPresenter.Render(inventory);
        }
        finally
        {
            isTransitioning = wasTransitioning;
        }
    }

    private void OnDestroy()
    {
        isPlaying = false;
        isTransitioning = true;
        sessionVersion++;
        // Awake can fail on an invalid binding before the spawner has been composed.
        if (spawner != null) spawner.Clear();
    }

    /// <summary>
    /// Compatibility bridge for old callers that set StageManager's public fields.
    /// Future IDs stay in the dictionary while the seven original counts remain usable.
    /// </summary>
    public void CallVari()
    {
        // The public compatibility entry point follows the same transition policy as
        // scene buttons. A UI lifecycle callback cannot replace stock halfway through a render.
        if (isTransitioning) return;
        ReloadInventoryFromManager();
    }

    private void ReloadInventoryFromManager()
    {
        // Reloading legacy data is a stock replacement. If a prefab callback requests
        // it during Instantiate, cancel that pending pair before publishing new counts.
        if (spawner != null) spawner.CancelPendingSpawn();
        inventory.Reset(stageManager.CreateInventorySnapshot());
        MirrorLegacyCounts();
        stage_Floor = stageManager.stage_Floor;
        stage_Wall = stageManager.stage_Wall;
        stage_FrontWall = stageManager.stage_FrontWall;
    }

    private void MirrorLegacyCounts()
    {
        A1Number = inventory.GetRemaining(1);
        A2Number = inventory.GetRemaining(2);
        INumber = inventory.GetRemaining(3);
        ONumber = inventory.GetRemaining(4);
        PNumber = inventory.GetRemaining(5);
        TNumber = inventory.GetRemaining(6);
        ZNumber = inventory.GetRemaining(7);
    }

    private void SynchronizeLegacyInventory()
    {
        // Preserve direct writes through the original fields without losing new IDs.
        var stock = inventory.Snapshot();
        stock[1] = A1Number;
        stock[2] = A2Number;
        stock[3] = INumber;
        stock[4] = ONumber;
        stock[5] = PNumber;
        stock[6] = TNumber;
        stock[7] = ZNumber;
        inventory.Reset(stock);
    }

    private IEnumerable<BlockSpec> CreateLegacyBlockSpecs()
    {
        // These bindings are a migration adapter, not the extensible catalog itself.
        // AdditionalBlocks can override one ID or supply an entirely new piece type.
        yield return new BlockSpec(1, A1Block, GhostA1Block, Vector3.zero);
        yield return new BlockSpec(2, A2Block, GhostA2Block, new Vector3(.5f, .5f, .5f));
        yield return new BlockSpec(3, IBlock, GhostIBlock, new Vector3(0, 0, .5f));
        yield return new BlockSpec(4, OBlock, GhostOBlock, new Vector3(.5f, 0, 0));
        yield return new BlockSpec(5, PBlock, GhostPBlock, new Vector3(0, .5f, .5f));
        yield return new BlockSpec(6, TBlock, GhostTBlock, new Vector3(0, .5f, 0));
        yield return new BlockSpec(7, ZBlock, GhostZBlock, new Vector3(0, .5f, .5f));
    }

    private List<InventoryBinding> CreateLegacyInventoryBindings()
    {
        // Preserve the authored display order independently of numeric selection IDs.
        return new List<InventoryBinding>
        {
            new InventoryBinding(1, A1Image, A1Text),
            new InventoryBinding(2, A2Image, A2Text),
            new InventoryBinding(3, IImage, IText),
            new InventoryBinding(7, ZImage, ZText),
            new InventoryBinding(4, OImage, OText),
            new InventoryBinding(5, PImage, PText),
            new InventoryBinding(6, TImage, TText)
        };
    }
}
