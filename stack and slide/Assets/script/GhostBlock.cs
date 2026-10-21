using UnityEngine;

/// <summary>
/// Positions one landing preview beneath its explicitly bound playable piece.
/// A downward support ray estimates height; trigger callbacks refine the shape's clearance.
/// </summary>
public class GhostBlock : MonoBehaviour
{
    public GameObject cBlock;
    public float distanceY;
    public bool OnInsideY;
    public Vector3 AdjustY;
    public Vector3 pre_AdjustY;
    /// <summary>
    /// Whether the latest query found Floor or FixedBlock support within the ray range.
    /// A zero distance without this flag is an invalid result, not a valid landing at zero.
    /// </summary>
    public bool HasLandingPoint { get; private set; }

    private Vector3 maxY;
    private Block block;

    private void Awake()
    {
        OnInsideY = false;
    }

    /// <summary>
    /// Associates this preview with one controller instead of searching for an arbitrary tag.
    /// Binding invalidates the old support result until the new owner has been queried.
    /// </summary>
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
            // Retire the visible child after placement. The stage still owns the outer
            // preview root and removes that root on the next spawn or a stage reset.
            Destroy(gameObject);
        }

        if (cBlock != null && HasLandingPoint && (other.CompareTag("FixedBlock") || other.CompareTag("Floor")))
        {
            // These small increments are the existing clearance heuristic for irregular
            // pieces; the support ray alone does not represent the full collider shape.
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
            // Retain the last exit correction so the preview does not immediately fall
            // back into the same surface. A later pose change invalidates this correction.
            pre_AdjustY = AdjustY;
        }
    }

    /// <summary>
    /// Refreshes support data without moving the preview root.
    /// Input code can call this immediately before a down step, outside the visual Update.
    /// </summary>
    /// <returns>True when a supported surface was found for the owner's current pose.</returns>
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
            // Both translations and rotations change which parts of a piece overlap.
            // Corrections calculated for the previous pose must not be applied to this one.
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
        // RaycastAll has no distance-order guarantee. Ignore ghosts and the moving piece,
        // then choose the nearest accepted support rather than trusting the first hit.
        // Fixed pieces use trigger colliders, so trigger hits must remain enabled here.
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
            // Clear the old distance too: otherwise moving away from an edge could keep
            // showing or accepting a landing based on a surface that is no longer below us.
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

        // Both components live on geometry children. Match their prefab roots so their
        // authored child offsets continue to line up after rotation and vertical movement.
        transform.parent.rotation = cBlock.transform.parent.rotation;
        if (!hasSupport)
        {
            // An unsupported preview follows the piece instead of using a stale height.
            transform.parent.position = cBlock.transform.parent.position;
            return;
        }

        if (!OnInsideY)
        {
            // During overlap, trigger callbacks temporarily own the vertical correction.
            transform.parent.position = cBlock.transform.parent.position + pre_AdjustY - maxY;
        }
    }
}
