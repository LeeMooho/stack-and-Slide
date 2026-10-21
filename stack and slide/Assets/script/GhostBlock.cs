using UnityEngine;

public class GhostBlock : MonoBehaviour
{
    public GameObject cBlock;
    public float distanceY;
    public bool OnInsideY;
    public Vector3 AdjustY;
    public Vector3 pre_AdjustY;
    public bool HasLandingPoint { get; private set; }

    private Vector3 maxY;
    private Block block;

    private void Awake()
    {
        OnInsideY = false;
    }

    public void Bind(Block controller)
    {
        block = controller;
        cBlock = controller != null ? controller.gameObject : null;
        HasLandingPoint = false;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("OnFixedBlock"))
        {
            Destroy(gameObject);
        }

        if (cBlock != null && HasLandingPoint && (other.CompareTag("FixedBlock") || other.CompareTag("Floor")))
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

    public bool RefreshLandingPoint()
    {
        if (block == null || !block.IsActive)
        {
            HasLandingPoint = false;
            cBlock = null;
            return false;
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
            HasLandingPoint = false;
            return false;
        }

        HasLandingPoint = false;
        float nearestDistance = float.PositiveInfinity;
        RaycastHit[] hits = Physics.RaycastAll(cBlock.transform.position, Vector3.down, 20f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            if ((hit.collider.CompareTag("Floor") || hit.collider.CompareTag("FixedBlock"))
                && hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                HasLandingPoint = true;
            }
        }

        if (!HasLandingPoint)
        {
            distanceY = 0f;
            maxY = Vector3.zero;
            return false;
        }

        distanceY = nearestDistance;
        maxY = Vector3.up * distanceY;
        return true;
    }

    private void Update()
    {
        bool hasSupport = RefreshLandingPoint();
        if (cBlock == null)
        {
            return;
        }

        transform.parent.rotation = cBlock.transform.parent.rotation;
        if (!hasSupport)
        {
            transform.parent.position = cBlock.transform.parent.position;
            return;
        }

        if (!OnInsideY)
        {
            transform.parent.position = cBlock.transform.parent.position + pre_AdjustY - maxY;
        }
    }
}
