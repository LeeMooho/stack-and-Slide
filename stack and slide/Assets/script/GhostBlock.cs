using UnityEngine;

public class GhostBlock : MonoBehaviour
{
    public GameObject cBlock;
    public float distanceY;
    public bool OnInsideY;
    public Vector3 AdjustY;
    public Vector3 pre_AdjustY;

    private Vector3 maxY;
    private Block block;

    private void Awake()
    {
        OnInsideY = false;
    }

    private void Start()
    {
        GameObject activeBlock = GameObject.FindWithTag("Block");
        if (activeBlock != null)
        {
            block = activeBlock.GetComponent<Block>();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("OnFixedBlock"))
        {
            Destroy(gameObject);
        }

        if (cBlock != null && (other.CompareTag("FixedBlock") || other.CompareTag("Floor")))
        {
            OnInsideY = true;
            AdjustY += new Vector3(0, 0.1f, 0);
            transform.parent.position = cBlock.transform.parent.position - maxY + AdjustY;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("FixedBlock") || other.CompareTag("Floor"))
        {
            OnInsideY = false;
            pre_AdjustY = AdjustY;
        }
    }

    private void Update()
    {
        if (block == null)
        {
            return;
        }

        cBlock = block.target;
        if (block.BlockRotation)
        {
            AdjustY = Vector3.zero;
            pre_AdjustY = Vector3.zero;
            block.BlockRotation = false;
        }

        if (cBlock == null)
        {
            return;
        }

        RaycastHit hit;
        if (Physics.Raycast(cBlock.transform.position, Vector3.down, out hit, 20f)
            && (hit.collider.CompareTag("Floor") || hit.collider.CompareTag("FixedBlock")))
        {
            distanceY = hit.distance;
            maxY = new Vector3(0, distanceY, 0);
        }

        transform.parent.rotation = cBlock.transform.parent.rotation;
        if (!OnInsideY)
        {
            transform.parent.position = cBlock.transform.parent.position + pre_AdjustY - maxY;
        }
    }
}
