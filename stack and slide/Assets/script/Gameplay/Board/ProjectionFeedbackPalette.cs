using System;
using UnityEngine;

/// <summary>
/// Shares feedback colors between the board and any corresponding legend.
/// Brighter high-contrast colors are an optional presentation mode, not a substitute
/// for the text labels and counts that explain missing and unexpected occupancy.
/// </summary>
public static class ProjectionFeedbackPalette
{
    public static Color GetColor(ProjectionCellState state, bool highContrast = false)
    {
        switch (state)
        {
            case ProjectionCellState.MissingRequired:
                return highContrast ? new Color(1f, 1f, 0f) : new Color(1f, .65f, 0f);
            case ProjectionCellState.UnexpectedOccupied:
                return highContrast ? new Color(1f, 0f, 1f) : new Color(.85f, .1f, .6f);
            case ProjectionCellState.MatchingRequired:
                return highContrast ? new Color(0f, 1f, 1f) : new Color(0f, .55f, .5f);
            default:
                // Empty matching cells have their own authored colors; there is no
                // single palette value that can restore every material correctly.
                throw new ArgumentException("Only highlighted feedback states have a palette color.", "state");
        }
    }
}
