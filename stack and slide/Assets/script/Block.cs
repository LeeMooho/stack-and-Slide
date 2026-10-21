using UnityEngine;

public class Block : MonoBehaviour
{
    private const float MoveStep = 0.5f;
    private const float RotationStep = 90f;

    public GameObject target;
    public GameObject preTarget;
    public bool BlockRotation;

    private GhostBlock ghostBlock;
    private bool canMoveUp;

    public bool IsActive
    {
        get { return enabled && target == gameObject && target != null && target.CompareTag("Block"); }
    }

    private void Awake()
    {
        target = gameObject;
    }

    public void Initialize(GhostBlock preview)
    {
        target = gameObject;
        ghostBlock = preview;
        if (preview != null)
        {
            preview.Bind(this);
        }
    }

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

    public void ControlBlock(string type)
    {
        if (!IsActive)
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
            MoveBlock(Vector3.down);
            CheckValid();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            MoveBlock(Vector3.up);
        }
    }

    private void MoveBlock(Vector3 direction)
    {
        BlockRotation = true;
        target.transform.parent.Translate(direction * MoveStep, Space.World);
    }

    private void RotateBlock(Vector3 axis)
    {
        BlockRotation = true;
        target.transform.parent.Rotate(axis * RotationStep, Space.World);
    }

    public void CheckValid()
    {
        if (!IsActive || ghostBlock == null || !ghostBlock.HasLandingPoint)
        {
            return;
        }

        float distanceY = ghostBlock.distanceY - MoveStep - ghostBlock.AdjustY.y;
        if (distanceY < MoveStep)
        {
            target.tag = "OnFixedBlock";
            target.transform.parent.Translate(new Vector3(0, -distanceY, 0), Space.World);
        }
    }

    private void OnTriggerStay(Collider other)
    {
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
            transform.parent.Translate(Vector3.up * MoveStep, Space.World);
            canMoveUp = false;
        }
    }
}
