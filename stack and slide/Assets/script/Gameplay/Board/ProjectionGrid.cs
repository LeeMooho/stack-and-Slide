using System;
using UnityEngine;

/// <summary>
/// Owns one projection surface's cell bindings, highlight colors and occupancy matching.
/// This is a composed runtime object, not a scene component: the existing stage can
/// delegate board behavior without changing any serialized scene references.
/// </summary>
public sealed class ProjectionGrid
{
    private readonly GameObject[] cells;
    private readonly MeshRenderer[] renderers;
    private readonly Color[] originalColors;
    private readonly bool[] highlighted;

    public int CellCount { get { return cells.Length; } }

    public ProjectionGrid(GameObject[] cells)
    {
        if (cells == null)
        {
            throw new ArgumentNullException("cells");
        }
        if (cells.Length == 0)
        {
            throw new ArgumentException("A projection grid needs at least one cell.", "cells");
        }

        // Capture the array structure so replacing an Inspector array element later
        // cannot silently desynchronize cached renderers, colors and query positions.
        this.cells = (GameObject[])cells.Clone();
        renderers = new MeshRenderer[cells.Length];
        originalColors = new Color[cells.Length];
        highlighted = new bool[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == null)
            {
                throw new ArgumentException("Projection cell " + i + " is not assigned.", "cells");
            }

            MeshRenderer renderer = cells[i].GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null)
            {
                throw new ArgumentException("Projection cell " + i + " needs a renderer and material.", "cells");
            }

            renderers[i] = renderer;
            // Reading sharedMaterial does not instantiate a material or modify its asset.
            originalColors[i] = renderer.sharedMaterial.color;
        }
    }

    /// <summary>
    /// Replaces this grid's highlighted pattern. Validate the whole input first so
    /// malformed stage data cannot paint part of the board before failing.
    /// </summary>
    public void ShowPattern(int[] pattern)
    {
        ValidatePattern(pattern);
        for (int i = 0; i < cells.Length; i++)
        {
            if (pattern[i] == 1)
            {
                // material writes affect this renderer's instance, not a shared asset.
                renderers[i].material.color = Color.black;
                highlighted[i] = true;
            }
            else if (highlighted[i])
            {
                renderers[i].material.color = originalColors[i];
                highlighted[i] = false;
            }
        }
    }

    /// <summary>
    /// Restores only colors this module changed, leaving untouched cells available
    /// for other visual features. Repeated clearing is safe during session teardown.
    /// </summary>
    public void ClearPattern()
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (!highlighted[i])
            {
                continue;
            }

            // The scene may already be removing a renderer during coordinator teardown.
            if (renderers[i] != null)
            {
                renderers[i].material.color = originalColors[i];
            }
            highlighted[i] = false;
        }
    }

    public int CountMismatches(int[] pattern, IProjectionProbe probe, float maxDistance = 8f)
    {
        ValidatePattern(pattern);
        if (probe == null)
        {
            throw new ArgumentNullException("probe");
        }
        if (maxDistance <= 0f || float.IsNaN(maxDistance) || float.IsInfinity(maxDistance))
        {
            throw new ArgumentOutOfRangeException("maxDistance", "Projection distance must be finite and positive.");
        }

        int mismatches = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            // The floor and walls have different rotations. Local down always points
            // inward; the coordinator's world up cannot describe all three surfaces.
            bool occupied = probe.IsOccupied(cells[i].transform.position, -cells[i].transform.up, maxDistance);
            if ((pattern[i] == 1) != occupied)
            {
                mismatches++;
            }
        }
        return mismatches;
    }

    private void ValidatePattern(int[] pattern)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException("pattern");
        }
        if (pattern.Length != cells.Length)
        {
            throw new ArgumentException("Pattern length must match the projection grid.", "pattern");
        }
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != 0 && pattern[i] != 1)
            {
                throw new ArgumentException("Projection patterns may contain only zero or one.", "pattern");
            }
        }
    }
}
