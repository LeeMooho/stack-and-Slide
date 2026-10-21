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
    private bool isEvaluating;
    private bool inputSuspended;
    private int sessionVersion;
    private int dataRevision;
    private int initialBlockCount;
    private int blocksSpawned;
    private double startedAtSeconds;

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

    // Late-bound features can reconcile from this snapshot instead of assuming that
    // they observed every prior event or that two attempts at one stage are the same.
    public StageSessionInfo CurrentSession
    {
        get
        {
            return new StageSessionInfo(sessionVersion, isPlaying ? CurrentStageNumber : 0,
                isPlaying ? initialBlockCount : 0, isPlaying ? blocksSpawned : 0, startedAtSeconds);
        }
    }

    public event Action<StageSessionInfo> SessionChanged;
    public event Action<BlockSpawnInfo> BlockSpawned;

    // Built-in progression records committed state before public extension callbacks
    // can navigate away. Public listeners remain free to start the next puzzle.
    internal event Action<StageSessionInfo> SessionCommitted;
    internal event Action<BlockSpawnInfo> BlockCommitted;
    internal event Action<ProjectionResult> EvaluationCommitted;

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

    private void Start()
    {
        // Runtime composition makes the feature visible in existing scenes without
        // replacing their serialized hierarchy or requiring new Inspector wiring.
        GameplayExperience experience = GetComponent<GameplayExperience>();
        if (experience == null) experience = gameObject.AddComponent<GameplayExperience>();
        experience.Initialize(this);
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
        int incomingBlockCount = ValidateStageBindings(layout);

        isTransitioning = true;
        try
        {
            ResetStageState();
            // Load the exact immutable snapshot already checked, rather than resolving an
            // asset again after UI/cleanup callbacks may have changed authoring data.
            stageManager.LoadLayout(layout);
            ReloadInventoryFromManager();
            CurrentStageNumber = stageNumber;
            initialBlockCount = incomingBlockCount;
            startedAtSeconds = Time.unscaledTime;
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
        PublishSession();
    }

    private int ValidateStageBindings(StageLayout layout)
    {
        if (layout.CellCount != floorGrid.CellCount || layout.CellCount != wallGrid.CellCount
            || layout.CellCount != frontWallGrid.CellCount)
            throw new InvalidOperationException("Stage patterns must match all three scene grids.");

        Dictionary<int, int> stock = layout.GetInitialInventory();
        int total = 0;
        foreach (KeyValuePair<int, int> entry in stock)
        {
            // Reject unrepresentable counters before clearing a running attempt.
            total = checked(total + entry.Value);
            if (entry.Value == 0) continue;
            BlockSpec specification;
            if (!blockCatalog.TryGet(entry.Key, out specification))
                throw new InvalidOperationException("No block definition exists for inventory ID " + entry.Key + ".");
            specification.ValidatePrefabs();
        }
        inventoryPresenter.ValidateStock(stock);
        return total;
    }

    public void GenerateBlock(int blockNumber)
    {
        if (!isPlaying || isTransitioning || isGenerating || isEvaluating || inputSuspended) return;
        SynchronizeLegacyInventory();
        BlockSpec specification;
        if (!blockCatalog.TryGet(blockNumber, out specification) || inventory.GetRemaining(blockNumber) <= 0)
            return;

        int expectedSession = sessionVersion;
        BlockSpawnInfo committed = null;
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
            blocksSpawned = checked(blocksSpawned + 1);
            pre_GhostBlock = spawner.CurrentGhost;
            MirrorLegacyCounts();
            UpdateBlockInventory();
            committed = new BlockSpawnInfo(sessionVersion, CurrentStageNumber, blockNumber, blocksSpawned);
        }
        finally
        {
            isGenerating = false;
        }
        if (committed != null)
        {
            Action<BlockSpawnInfo> record = BlockCommitted;
            if (record != null) record(committed);
            // Internal observers may themselves change scene state. Preserve the old
            // payload for extensions, which can compare its session with CurrentSession.
            Action<BlockSpawnInfo> listeners = BlockSpawned;
            if (listeners != null) listeners(committed);
        }
    }

    public int GetRemainingBlocks(int blockNumber)
    {
        return inventory.GetRemaining(blockNumber);
    }

    public void Slide()
    {
        if (!isPlaying || isTransitioning || isGenerating || isEvaluating || inputSuspended) return;
        int expectedSession = sessionVersion;
        int expectedData = dataRevision;
        ProjectionResult result;
        isEvaluating = true;
        try
        {
            // Capture selected inputs together. Reentrant probes cannot spawn more
            // geometry mid-query; navigation or a data reload cancels this evaluation.
            int evaluatedStage = CurrentStageNumber;
            int[] floorPattern = stage_Floor;
            int[] wallPattern = stage_Wall;
            int[] frontWallPattern = stage_FrontWall;
            IProjectionProbe probe = projectionProbe;
            result = new ProjectionResult(
                floorGrid.Evaluate(floorPattern, probe),
                wallGrid.Evaluate(wallPattern, probe),
                frontWallGrid.Evaluate(frontWallPattern, probe), evaluatedStage, expectedSession, blocksSpawned);
            if (expectedSession != sessionVersion || expectedData != dataRevision) return;
            Debug.Log(result.IsSolved ? "성공" : "실패");
            Action<ProjectionResult> record = EvaluationCommitted;
            if (record != null) record(result);
        }
        finally
        {
            isEvaluating = false;
        }
        Action<ProjectionResult> listeners = PuzzleEvaluated;
        if (listeners != null) listeners(result);
    }

    /// <summary>Applies diagnostic colors only to the attempt that produced the result.</summary>
    public bool ShowProjectionFeedback(ProjectionResult result, bool showHints, bool highContrast)
    {
        if (!isPlaying || result == null || result.SessionId != sessionVersion
            || result.StageNumber != CurrentStageNumber) return false;
        if (!showHints)
        {
            floorGrid.ShowPattern(stage_Floor);
            wallGrid.ShowPattern(stage_Wall);
            frontWallGrid.ShowPattern(stage_FrontWall);
            return true;
        }
        if (result.FloorResults == null || result.WallResults == null || result.FrontWallResults == null)
            return false;
        floorGrid.ShowFeedback(result.FloorResults, highContrast);
        wallGrid.ShowFeedback(result.WallResults, highContrast);
        frontWallGrid.ShowFeedback(result.FrontWallResults, highContrast);
        return true;
    }

    public void SetInputSuspended(bool suspended)
    {
        inputSuspended = suspended;
        if (spawner != null) spawner.SetInputSuspended(suspended);
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
        PublishSession();
    }

    private void PublishSession()
    {
        StageSessionInfo snapshot = CurrentSession;
        Action<StageSessionInfo> record = SessionCommitted;
        if (record != null) record(snapshot);
        Action<StageSessionInfo> listeners = SessionChanged;
        if (listeners != null) listeners(snapshot);
    }

    private void ResetStageState()
    {
        // Revoke play before retiring roots: their OnDisable callbacks must not start
        // another generation while the previous session is still being dismantled.
        isPlaying = false;
        sessionVersion++;
        CurrentStageNumber = 0;
        initialBlockCount = 0;
        blocksSpawned = 0;
        startedAtSeconds = Time.unscaledTime;
        spawner.Clear();
        pre_GhostBlock = null;
        floorGrid.ClearPattern();
        wallGrid.ClearPattern();
        frontWallGrid.ClearPattern();
        stageManager.ResetStage();
        ReloadInventoryFromManager();
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
        dataRevision++;
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
