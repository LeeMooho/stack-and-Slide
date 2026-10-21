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
    /// Paints feedback from an immutable evaluation without issuing another physics query.
    /// Pair these colors with text labels or counts in the view, so a player need not
    /// distinguish colors alone to understand what is missing or unexpectedly occupied.
    /// </summary>
    public void ShowFeedback(ProjectionSurfaceResult result, bool highContrast = false)
    {
        if (result == null)
        {
            throw new ArgumentNullException("result");
        }
        if (result.CellCount != cells.Length)
        {
            throw new ArgumentException("Feedback size must match the projection grid.", "result");
        }

        for (int i = 0; i < cells.Length; i++)
        {
            ProjectionCellState state = result[i];
            if (state == ProjectionCellState.MatchingEmpty)
            {
                if (highlighted[i])
                {
                    renderers[i].material.color = originalColors[i];
                    highlighted[i] = false;
                }
            }
            else
            {
                renderers[i].material.color = ProjectionFeedbackPalette.GetColor(state, highContrast);
                // Unexpected occupancy may be on an originally empty target cell.
                // Track it too, so restoring the puzzle cannot leave diagnostic color behind.
                highlighted[i] = true;
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
        return Evaluate(pattern, probe, maxDistance).MismatchCount;
    }

    /// <summary>
    /// Captures target and occupancy together, querying each cell only once. The same
    /// snapshot supplies result counts, board hints and listener payloads consistently.
    /// </summary>
    public ProjectionSurfaceResult Evaluate(int[] pattern, IProjectionProbe probe, float maxDistance = 8f)
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

        ProjectionCellState[] states = new ProjectionCellState[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            // The floor and walls have different rotations. Local down always points
            // inward; the coordinator's world up cannot describe all three surfaces.
            bool occupied = probe.IsOccupied(cells[i].transform.position, -cells[i].transform.up, maxDistance);
            if (pattern[i] == 1)
            {
                states[i] = occupied ? ProjectionCellState.MatchingRequired : ProjectionCellState.MissingRequired;
            }
            else
            {
                states[i] = occupied ? ProjectionCellState.UnexpectedOccupied : ProjectionCellState.MatchingEmpty;
            }
        }
        return new ProjectionSurfaceResult(states);
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
