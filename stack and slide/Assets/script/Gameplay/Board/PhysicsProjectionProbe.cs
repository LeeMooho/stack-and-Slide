using System;
using UnityEngine;

/// <summary>
/// Adapts Unity ray queries to the game's three accepted placement states.
/// Geometry and ghost colliders may appear before a playable block, so occupancy
/// cannot be inferred from just the first hit or from whether any collider was hit.
/// </summary>
public sealed class PhysicsProjectionProbe : IProjectionProbe
{
    public bool IsOccupied(Vector3 origin, Vector3 direction, float maxDistance)
    {
        if (maxDistance <= 0f || float.IsNaN(maxDistance) || float.IsInfinity(maxDistance))
        {
            throw new ArgumentOutOfRangeException("maxDistance", "Projection distance must be finite and positive.");
        }

        // Playable block colliders are triggers too. Ignoring triggers would remove
        // legitimate occupancy along with the ghosts we want to look through.
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null && (hit.collider.CompareTag("Block")
                || hit.collider.CompareTag("OnFixedBlock") || hit.collider.CompareTag("FixedBlock")))
            {
                return true;
            }
        }

        return false;
    }
}
