using UnityEngine;

/// <summary>
/// Handles input and placement for the tagged geometry child of one playable prefab.
/// The stage owns the prefab root; this component never takes control of another piece.
/// </summary>
public class Block : MonoBehaviour
{
    private const float MoveStep = 0.5f;
    private const float RotationStep = 90f;

    public GameObject target;
    public GameObject preTarget;
    public bool BlockRotation;

    private GhostBlock ghostBlock;
    private bool canMoveUp;

    // Modal UI suspends input without changing ownership. IsActive must stay true so
    // the spawner still rejects a second moving piece while the drawer is open.
    public bool InputSuspended { get; set; }

    /// <summary>
    /// Allows input only while this enabled controller still owns the moving Block object.
    /// OnFixedBlock is the just-landed state; FixedBlock is a completed support surface.
    /// </summary>
    public bool IsActive
    {
        get { return enabled && target == gameObject && target != null && target.CompareTag("Block"); }
    }

    private void Awake()
    {
        // A global tag search could select a newer piece while this old controller still exists.
        target = gameObject;
    }

    /// <summary>
    /// Connects a freshly spawned piece to its own preview, independent of Unity's Update order.
    /// </summary>
    public void Initialize(GhostBlock preview)
    {
        target = gameObject;
        ghostBlock = preview;
        if (preview != null)
        {
            preview.Bind(this);
        }
    }

    /// <summary>
    /// Promotes a landed piece to fixed geometry before the stage creates the next piece.
    /// Its colliders remain in the puzzle, while its input controller becomes inactive.
    /// </summary>
    public void CompletePlacement()
    {
        if (target != null && target.CompareTag("OnFixedBlock"))
        {
            target.tag = "FixedBlock";
            enabled = false;
        }
    }

    private void Update()
    {
        if (!IsActive)
        {
            return;
        }

        ControlBlock(null);
    }

    /// <summary>
    /// Applies the existing keyboard controls and the optional legacy "L" command.
    /// Several keys may be pressed together, so later actions see earlier pose changes.
    /// </summary>
    public void ControlBlock(string type)
    {
        if (!IsActive || InputSuspended)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || type == "L")
        {
            MoveBlock(Vector3.right);
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            MoveBlock(Vector3.left);
        }
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveBlock(Vector3.back);
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveBlock(Vector3.forward);
        }
        if (Input.GetKeyDown(KeyCode.Z))
        {
            RotateBlock(Vector3.right);
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            RotateBlock(Vector3.up);
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            RotateBlock(Vector3.forward);
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            canMoveUp = true;
            // Earlier inputs may have changed the pose since the ghost's last Update.
            // Capture support BEFORE translating down: CheckValid subtracts that step once.
            // This explicit refresh also avoids relying on which component updates first.
            if (ghostBlock != null)
            {
                ghostBlock.RefreshLandingPoint();
            }
            MoveBlock(Vector3.down);
            CheckValid();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            MoveBlock(Vector3.up);
        }
    }

    /// <summary>Moves the whole prefab root by one half-cell in world space.</summary>
    private void MoveBlock(Vector3 direction)
    {
        // The tagged target is a child with mesh/collider offsets; moving only that child
        // would separate its geometry from the root used by the matching ghost prefab.
        BlockRotation = true;
        target.transform.parent.Translate(direction * MoveStep, Space.World);
    }

    private void RotateBlock(Vector3 axis)
    {
        // The same flag invalidates the preview's overlap correction after any pose change.
        BlockRotation = true;
        target.transform.parent.Rotate(axis * RotationStep, Space.World);
    }

    /// <summary>
    /// Resolves landing after a downward step using the preview's pre-step support snapshot.
    /// Callers must refresh that snapshot before moving, as ControlBlock does for the A key.
    /// </summary>
    public void CheckValid()
    {
        if (!IsActive || ghostBlock == null || !ghostBlock.HasLandingPoint)
        {
            return;
        }

        // distanceY was measured before the move. Subtract the half-cell already travelled
        // and the shape's overlap lift to get the remaining vertical correction.
        // A negative result means the down step overshot and must be corrected upward.
        float distanceY = ghostBlock.distanceY - MoveStep - ghostBlock.AdjustY.y;
        if (distanceY < MoveStep)
        {
            target.tag = "OnFixedBlock";
            target.transform.parent.Translate(new Vector3(0, -distanceY, 0), Space.World);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Unity may deliver trigger messages to disabled behaviours. Fixed pieces must
        // never be retagged or moved by callbacks left over from an earlier placement.
        if (!IsActive || (!other.CompareTag("Floor") && !other.CompareTag("FixedBlock")))
        {
            return;
        }

        if (target != null)
        {
            target.tag = "OnFixedBlock";
        }

        if (canMoveUp)
        {
            // Arm this recovery only on downward input, and consume it once even when
            // several colliders keep reporting the same overlap on later physics steps.
            transform.parent.Translate(Vector3.up * MoveStep, Space.World);
            canMoveUp = false;
        }
    }
}
