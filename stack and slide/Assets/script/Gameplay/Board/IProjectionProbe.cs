using UnityEngine;

/// <summary>
/// Supplies occupancy observations without deciding whether a puzzle cell should be occupied.
/// A different probe can replay recorded observations or support another physics backend
/// while the grid's matching rules and result reporting stay unchanged.
/// </summary>
public interface IProjectionProbe
{
    bool IsOccupied(Vector3 origin, Vector3 direction, float maxDistance);
}
