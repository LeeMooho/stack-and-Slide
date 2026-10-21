using UnityEngine;

/// <summary>
/// Authoring asset for a selectable piece. New IDs and prefab pairs can be added in the
/// Inspector without changing the coordinator's selection switch or the inventory model.
/// </summary>
[CreateAssetMenu(menuName = "Stack and Slide/Block Definition", fileName = "BlockDefinition")]
public sealed class BlockDefinition : ScriptableObject
{
    public int BlockId;
    public GameObject BlockPrefab;
    public GameObject GhostPrefab;
    public Vector3 SpawnOffset;

    /// <summary>
    /// Captures ID and offset values at registration time. Prefab objects remain Unity
    /// references, so their required controller hierarchy is checked again before spawning.
    /// </summary>
    public BlockSpec ToSpec()
    {
        return new BlockSpec(BlockId, BlockPrefab, GhostPrefab, SpawnOffset);
    }
}
