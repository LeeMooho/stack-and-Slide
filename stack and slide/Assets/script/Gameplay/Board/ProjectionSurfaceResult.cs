using System;

/// <summary>Explains both the target silhouette and the observed occupancy of one cell.</summary>
public enum ProjectionCellState
{
    MatchingEmpty,
    MatchingRequired,
    MissingRequired,
    UnexpectedOccupied
}

/// <summary>
/// Immutable cell-level feedback for one projection surface. A result can outlive the
/// current stage session, so neither the source array nor a returned copy can edit it.
/// </summary>
public sealed class ProjectionSurfaceResult
{
    private readonly ProjectionCellState[] states;

    public int CellCount { get { return states.Length; } }
    public int MatchingEmptyCount { get; }
    public int MatchingRequiredCount { get; }
    public int MissingRequiredCount { get; }
    public int UnexpectedOccupiedCount { get; }
    public int MismatchCount { get { return MissingRequiredCount + UnexpectedOccupiedCount; } }
    public bool IsSolved { get { return MismatchCount == 0; } }
    public ProjectionCellState this[int index] { get { return GetCellState(index); } }

    public ProjectionSurfaceResult(ProjectionCellState[] states)
    {
        if (states == null)
        {
            throw new ArgumentNullException("states");
        }

        this.states = (ProjectionCellState[])states.Clone();
        int matchingEmpty = 0;
        int matchingRequired = 0;
        int missingRequired = 0;
        int unexpectedOccupied = 0;
        foreach (ProjectionCellState state in this.states)
        {
            switch (state)
            {
                case ProjectionCellState.MatchingEmpty:
                    matchingEmpty++;
                    break;
                case ProjectionCellState.MatchingRequired:
                    matchingRequired++;
                    break;
                case ProjectionCellState.MissingRequired:
                    missingRequired++;
                    break;
                case ProjectionCellState.UnexpectedOccupied:
                    unexpectedOccupied++;
                    break;
                default:
                    throw new ArgumentException("Unknown projection cell state.", "states");
            }
        }

        MatchingEmptyCount = matchingEmpty;
        MatchingRequiredCount = matchingRequired;
        MissingRequiredCount = missingRequired;
        UnexpectedOccupiedCount = unexpectedOccupied;
    }

    public ProjectionCellState GetCellState(int index)
    {
        if (index < 0 || index >= states.Length)
        {
            throw new ArgumentOutOfRangeException("index");
        }
        return states[index];
    }

    /// <summary>Returns editable data for consumers without exposing this snapshot's storage.</summary>
    public ProjectionCellState[] CopyCellStates()
    {
        return (ProjectionCellState[])states.Clone();
    }
}
