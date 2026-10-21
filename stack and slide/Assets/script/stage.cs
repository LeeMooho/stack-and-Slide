using UnityEngine;
using UnityEngine.UI;

// Keep serialized field and public method names stable for existing scenes and buttons.
public class stage : MonoBehaviour
{
    public GameObject[] floor;
    public GameObject[] wall;
    public GameObject[] frontwall;
    public RectTransform[] Location;
    public GameObject playUI;
    public GameObject mainMenu;

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

    private const float MaxDistance = 8f;

    private void Awake()
    {
        playUI.SetActive(false);
        mainMenu.SetActive(true);
        stageManager = GameObject.Find("StageManager").GetComponent<StageManager>();
        stageManager.ResetStage();
        CallVari();
    }

    private void UpdateBlockInventory()
    {
        int locationIndex = 0;
        UpdateBlockImage(A1Image, A1Text, A1Number, ref locationIndex);
        UpdateBlockImage(A2Image, A2Text, A2Number, ref locationIndex);
        UpdateBlockImage(IImage, IText, INumber, ref locationIndex);
        UpdateBlockImage(ZImage, ZText, ZNumber, ref locationIndex);
        UpdateBlockImage(OImage, OText, ONumber, ref locationIndex);
        UpdateBlockImage(PImage, PText, PNumber, ref locationIndex);
        UpdateBlockImage(TImage, TText, TNumber, ref locationIndex);
    }

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

    public void SelectStage(int stageNumber)
    {
        if (stageNumber != 101)
        {
            return;
        }

        playUI.SetActive(true);
        mainMenu.SetActive(false);
        stageManager.stage101();
        CallVari();
        isPlaying = true;
        FloorColor();
        UpdateBlockInventory();
    }

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

    private static void ColorGrid(GameObject[] cells, int[] pattern)
    {
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == 1)
            {
                cells[i].GetComponent<MeshRenderer>().material.color = Color.black;
            }
        }
    }

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

    private void SpawnBlock(GameObject blockPrefab, GameObject ghostPrefab, Vector3 adjustment, ref int remaining)
    {
        if (remaining <= 0 || (activeBlock != null && activeBlock.IsActive))
        {
            return;
        }

        if (activeBlock != null)
        {
            activeBlock.CompletePlacement();
        }
        if (pre_GhostBlock != null)
        {
            pre_GhostBlock.SetActive(false);
            Destroy(pre_GhostBlock);
        }

        pre_GhostBlock = Instantiate(ghostPrefab, GhostOrigin + adjustment, Quaternion.identity);
        GameObject blockRoot = Instantiate(blockPrefab, Origin + adjustment, Quaternion.identity);
        activeBlock = blockRoot.GetComponentInChildren<Block>();
        activeBlock.Initialize(pre_GhostBlock.GetComponentInChildren<GhostBlock>());
        remaining--;
        UpdateBlockInventory();
    }

    public void Slide()
    {
        int mismatches = CountProjectionMismatches(floor, stage_Floor)
            + CountProjectionMismatches(wall, stage_Wall)
            + CountProjectionMismatches(frontwall, stage_FrontWall);

        Debug.Log(mismatches == 0 ? "성공" : "실패");
    }

    private static int CountProjectionMismatches(GameObject[] cells, int[] pattern)
    {
        int mismatches = 0;
        for (int i = 0; i < pattern.Length; i++)
        {
            // Each grid is rotated in the scene; its local down points into the board.
            // Playable blocks and ghosts both use triggers, so filter all hits by tag.
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

            if ((pattern[i] == 1) != hasBlock)
            {
                mismatches++;
            }
        }

        return mismatches;
    }

    public void openMainMenu()
    {
        playUI.SetActive(false);
        mainMenu.SetActive(true);
        isPlaying = false;
    }

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
