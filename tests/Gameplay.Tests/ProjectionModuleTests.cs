using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Exercises board presentation and matching through their public composition seams.
/// Scripted probes check matching rules; the physics adapter tests use queued hits,
/// so neither set of cases claims Unity collision or rendering integration coverage.
/// </summary>
internal static class ProjectionModuleTests
{
    public static void Register(Action<string, Action> run)
    {
        run("Projection grid rejects incomplete cell bindings", InvalidBindings);
        run("Projection grid keeps its bound cell array independent", IndependentBindings);
        run("Replacing a projection pattern restores its previous highlights", ReplacePattern);
        run("Invalid projection patterns cannot partially repaint a grid", InvalidPatternDoesNotPaint);
        run("Clearing a projection does not overwrite untouched visual cells", ClearOnlyOwnedHighlights);
        run("Projection module matches every cell with its inward axis", QueryNormalsAndOccupancy);
        run("Invalid projection queries make no probe calls", InvalidQueries);
        run("Physics projection probe accepts every playable placement tag", AcceptedTags);
        run("Physics projection probe ignores ghost and grid hits", UnrelatedHits);
        run("Projection results preserve surface counts and solved status", ResultCounts);
        run("Projection result rejects negative mismatch counts", InvalidResultCounts);
        run("Projection results preserve optional evaluated-stage context", ResultStageContext);
        run("Detailed projection captures every cell state with one query", DetailedEvaluation);
        run("Surface result cells remain independent of input and returned arrays", IndependentSurfaceSnapshot);
        run("Surface results reject invalid cell state snapshots", InvalidSurfaceSnapshots);
        run("Board feedback distinguishes missing extra and matched cells", FeedbackColors);
        run("Puzzle reset clears unexpected feedback on originally empty cells", ClearFeedbackHighlights);
        run("High-contrast feedback changes the highlighted palette", HighContrastFeedback);
        run("Invalid feedback cannot partially repaint a grid", InvalidFeedbackDoesNotPaint);
        run("Detailed evaluation validates input before querying", InvalidDetailedEvaluation);
        run("Projection results preserve session and spawn context", ResultSessionContext);
        run("Detailed results aggregate immutable surface snapshots", DetailedResultSnapshots);
        run("Detailed results reject missing surfaces and invalid context", InvalidDetailedResults);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual))
        {
            throw new Exception("Expected " + expected + ", got " + actual);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static GameObject Cell(Color color, Vector3? up = null)
    {
        var cell = new GameObject("Projection cell");
        cell.transform.up = up ?? Vector3.down;
        cell.AddComponent<MeshRenderer>().material.color = color;
        return cell;
    }

    private static Color ColorOf(GameObject cell)
    {
        return cell.GetComponent<MeshRenderer>().material.color;
    }

    private static void InvalidBindings()
    {
        Throws<ArgumentNullException>(() => new ProjectionGrid(null));
        Throws<ArgumentException>(() => new ProjectionGrid(new GameObject[0]));
        var authored = new Color(.2f, .3f, .4f);
        var valid = Cell(authored);
        Throws<ArgumentException>(() => new ProjectionGrid(new[] { valid, null }));
        Throws<ArgumentException>(() => new ProjectionGrid(new[] { valid, new GameObject("No renderer") }));
        var noMaterial = Cell(authored);
        noMaterial.GetComponent<MeshRenderer>().material = null;
        Throws<ArgumentException>(() => new ProjectionGrid(new[] { valid, noMaterial }));
        Equal(authored, ColorOf(valid));
    }

    private static void IndependentBindings()
    {
        var bound = Cell(new Color(1, 1, 1), Vector3.back);
        bound.transform.position = new Vector3(1, 2, 3);
        var input = new[] { bound };
        var grid = new ProjectionGrid(input);
        input[0] = Cell(new Color(1, 0, 0), Vector3.up);
        var probe = new ScriptedProbe(false);
        Equal(1, grid.CellCount);
        Equal(0, grid.CountMismatches(new[] { 0 }, probe));
        Equal(bound.transform.position, probe.Origins[0]);
        Equal(Vector3.forward, probe.Directions[0]);
    }

    private static void ReplacePattern()
    {
        var first = new Color(.2f, .3f, .4f);
        var second = new Color(.6f, .7f, .8f);
        var cells = new[] { Cell(first), Cell(second) };
        var grid = new ProjectionGrid(cells);
        grid.ShowPattern(new[] { 1, 0 });
        Equal(Color.black, ColorOf(cells[0]));
        Equal(second, ColorOf(cells[1]));
        grid.ShowPattern(new[] { 0, 1 });
        Equal(first, ColorOf(cells[0]));
        Equal(Color.black, ColorOf(cells[1]));
        grid.ClearPattern();
        grid.ClearPattern();
        Equal(first, ColorOf(cells[0]));
        Equal(second, ColorOf(cells[1]));
    }

    private static void InvalidPatternDoesNotPaint()
    {
        var cells = new[] { Cell(new Color(.2f, .3f, .4f)), Cell(new Color(.6f, .7f, .8f)) };
        var grid = new ProjectionGrid(cells);
        grid.ShowPattern(new[] { 1, 0 });
        var second = ColorOf(cells[1]);
        // If validation were interleaved with drawing, the leading zero would clear
        // the first highlight before the invalid second cell caused an exception.
        Throws<ArgumentException>(() => grid.ShowPattern(new[] { 0, 2 }));
        Throws<ArgumentException>(() => grid.ShowPattern(new[] { 0 }));
        Throws<ArgumentNullException>(() => grid.ShowPattern(null));
        Equal(Color.black, ColorOf(cells[0]));
        Equal(second, ColorOf(cells[1]));
    }

    private static void ClearOnlyOwnedHighlights()
    {
        var authored = new Color(.2f, .3f, .4f);
        var otherFeatureColor = new Color(.8f, .1f, .5f);
        var cells = new[] { Cell(authored), Cell(authored) };
        var grid = new ProjectionGrid(cells);
        grid.ShowPattern(new[] { 1, 0 });
        cells[1].GetComponent<MeshRenderer>().material.color = otherFeatureColor;
        grid.ClearPattern();
        Equal(authored, ColorOf(cells[0]));
        Equal(otherFeatureColor, ColorOf(cells[1]));
    }

    private static void QueryNormalsAndOccupancy()
    {
        var cells = new[]
        {
            Cell(new Color(1, 1, 1), Vector3.down),
            Cell(new Color(1, 1, 1), Vector3.back),
            Cell(new Color(1, 1, 1), Vector3.left)
        };
        for (int i = 0; i < cells.Length; i++) cells[i].transform.position = new Vector3(i, 2, 3);
        var probe = new ScriptedProbe(true, true, false);
        var grid = new ProjectionGrid(cells);
        Equal(2, grid.CountMismatches(new[] { 1, 0, 1 }, probe, 4.5f));
        var expected = new[] { Vector3.up, Vector3.forward, Vector3.right };
        Equal(3, probe.Origins.Count);
        Equal(0, probe.Remaining);
        for (int i = 0; i < cells.Length; i++)
        {
            Equal(cells[i].transform.position, probe.Origins[i]);
            Equal(expected[i], probe.Directions[i]);
            Equal(4.5f, probe.Distances[i]);
        }
    }

    private static void InvalidQueries()
    {
        var grid = new ProjectionGrid(new[] { Cell(new Color(1, 1, 1)) });
        var probe = new ScriptedProbe();
        Throws<ArgumentException>(() => grid.CountMismatches(new[] { 2 }, probe));
        Throws<ArgumentException>(() => grid.CountMismatches(new int[0], probe));
        Throws<ArgumentNullException>(() => grid.CountMismatches(null, probe));
        Throws<ArgumentNullException>(() => grid.CountMismatches(new[] { 0 }, null));
        foreach (float distance in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            Throws<ArgumentOutOfRangeException>(() => grid.CountMismatches(new[] { 0 }, probe, distance));
        Equal(0, probe.Origins.Count);
    }

    private static RaycastHit Hit(string tag)
    {
        return new RaycastHit { collider = new GameObject { tag = tag }.AddComponent<Collider>() };
    }

    private static void AcceptedTags()
    {
        var probe = new PhysicsProjectionProbe();
        foreach (string tag in new[] { "Block", "OnFixedBlock", "FixedBlock" })
        {
            Physics.AllResults.Enqueue(new[] { Hit("GhostBlock"), Hit("Floor"), Hit(tag) });
            Equal(true, probe.IsOccupied(Vector3.zero, Vector3.up, 8f));
        }
    }

    private static void UnrelatedHits()
    {
        var probe = new PhysicsProjectionProbe();
        Physics.AllResults.Enqueue(new[] { new RaycastHit(), Hit("GhostBlock"), Hit("Floor") });
        Equal(false, probe.IsOccupied(Vector3.zero, Vector3.up, 8f));
        Physics.AllResults.Enqueue(new RaycastHit[0]);
        Equal(false, probe.IsOccupied(Vector3.zero, Vector3.up, 8f));
    }

    private static void ResultCounts()
    {
        var result = new ProjectionResult(1, 2, 3);
        Equal(1, result.FloorMismatches);
        Equal(2, result.WallMismatches);
        Equal(3, result.FrontWallMismatches);
        Equal(6, result.TotalMismatches);
        Equal(false, result.IsSolved);
        Equal(true, new ProjectionResult(0, 0, 0).IsSolved);
        Equal(null, result.FloorResults);
        Equal(null, result.WallResults);
        Equal(null, result.FrontWallResults);
    }

    private static void InvalidResultCounts()
    {
        Throws<ArgumentOutOfRangeException>(() => new ProjectionResult(-1, 0, 0));
        Throws<ArgumentOutOfRangeException>(() => new ProjectionResult(0, -1, 0));
        Throws<ArgumentOutOfRangeException>(() => new ProjectionResult(0, 0, -1));
    }

    private static void ResultStageContext()
    {
        // Standalone callers keep the original three-argument constructor contract.
        // Stage-aware callers snapshot the evaluated ID instead of reading live state
        // that an earlier event listener may have advanced to the next puzzle.
        Equal(0, new ProjectionResult(0, 0, 0).StageNumber);
        Equal(102, new ProjectionResult(0, 0, 0, 102).StageNumber);
        Throws<ArgumentOutOfRangeException>(() => new ProjectionResult(0, 0, 0, -1));
    }

    private static ProjectionCellState[] EveryCellState()
    {
        return new[] { ProjectionCellState.MatchingEmpty, ProjectionCellState.MatchingRequired,
            ProjectionCellState.MissingRequired, ProjectionCellState.UnexpectedOccupied };
    }

    private static void DetailedEvaluation()
    {
        var cells = new[] { Cell(new Color(1, 1, 1)), Cell(new Color(1, 1, 1)),
            Cell(new Color(1, 1, 1)), Cell(new Color(1, 1, 1)) };
        var grid = new ProjectionGrid(cells);
        var probe = new ScriptedProbe(false, true, false, true);
        var pattern = new[] { 0, 1, 1, 0 };
        var result = grid.Evaluate(pattern, probe);
        Equal(4, result.CellCount);
        Equal(4, probe.Origins.Count);
        Equal(0, probe.Remaining);
        Equal(2, result.MismatchCount);
        Equal(1, result.MatchingEmptyCount);
        Equal(1, result.MatchingRequiredCount);
        Equal(1, result.MissingRequiredCount);
        Equal(1, result.UnexpectedOccupiedCount);
        Equal(false, result.IsSolved);
        var expected = EveryCellState();
        for (int i = 0; i < expected.Length; i++) Equal(expected[i], result.GetCellState(i));
        pattern[0] = 1;
        Equal(ProjectionCellState.MatchingEmpty, result[0]);
    }

    private static void IndependentSurfaceSnapshot()
    {
        var states = EveryCellState();
        var result = new ProjectionSurfaceResult(states);
        states[0] = ProjectionCellState.UnexpectedOccupied;
        var copy = result.CopyCellStates();
        copy[1] = ProjectionCellState.MissingRequired;
        Equal(ProjectionCellState.MatchingEmpty, result[0]);
        Equal(ProjectionCellState.MatchingRequired, result[1]);
        Equal(2, result.MismatchCount);
        Throws<ArgumentOutOfRangeException>(() => result.GetCellState(-1));
        Throws<ArgumentOutOfRangeException>(() => result.GetCellState(result.CellCount));
    }

    private static void InvalidSurfaceSnapshots()
    {
        Throws<ArgumentNullException>(() => new ProjectionSurfaceResult(null));
        Throws<ArgumentException>(() => new ProjectionSurfaceResult(new[] { (ProjectionCellState)99 }));
        Equal(true, new ProjectionSurfaceResult(new[] { ProjectionCellState.MatchingRequired }).IsSolved);
    }

    private static void FeedbackColors()
    {
        var authored = new Color(.2f, .3f, .4f);
        var cells = new[] { Cell(authored), Cell(authored), Cell(authored), Cell(authored) };
        var grid = new ProjectionGrid(cells);
        grid.ShowFeedback(new ProjectionSurfaceResult(EveryCellState()));
        Equal(authored, ColorOf(cells[0]));
        Equal(ProjectionFeedbackPalette.GetColor(ProjectionCellState.MatchingRequired), ColorOf(cells[1]));
        Equal(ProjectionFeedbackPalette.GetColor(ProjectionCellState.MissingRequired), ColorOf(cells[2]));
        Equal(ProjectionFeedbackPalette.GetColor(ProjectionCellState.UnexpectedOccupied), ColorOf(cells[3]));
        Equal(false, ColorOf(cells[1]).Equals(ColorOf(cells[2])));
        Equal(false, ColorOf(cells[1]).Equals(ColorOf(cells[3])));
        Equal(false, ColorOf(cells[2]).Equals(ColorOf(cells[3])));
        grid.ShowFeedback(new ProjectionSurfaceResult(new[] { ProjectionCellState.MatchingEmpty,
            ProjectionCellState.MatchingEmpty, ProjectionCellState.MatchingEmpty, ProjectionCellState.MatchingEmpty }));
        foreach (var cell in cells) Equal(authored, ColorOf(cell));
    }

    private static void ClearFeedbackHighlights()
    {
        var authored = new Color(.2f, .3f, .4f);
        var cells = new[] { Cell(authored), Cell(authored), Cell(authored), Cell(authored) };
        var grid = new ProjectionGrid(cells);
        var feedback = new ProjectionSurfaceResult(EveryCellState());
        grid.ShowFeedback(feedback);
        // Cell 3 was not part of the target silhouette, but feedback painted it.
        // Returning to the normal target must clear that extra-cell diagnostic too.
        grid.ShowPattern(new[] { 0, 1, 1, 0 });
        Equal(authored, ColorOf(cells[0]));
        Equal(Color.black, ColorOf(cells[1]));
        Equal(Color.black, ColorOf(cells[2]));
        Equal(authored, ColorOf(cells[3]));
        grid.ShowFeedback(feedback);
        grid.ClearPattern();
        foreach (var cell in cells) Equal(authored, ColorOf(cell));
    }

    private static void HighContrastFeedback()
    {
        var cells = new[] { Cell(new Color(1, 1, 1)), Cell(new Color(1, 1, 1)), Cell(new Color(1, 1, 1)) };
        var grid = new ProjectionGrid(cells);
        var states = new[] { ProjectionCellState.MatchingRequired, ProjectionCellState.MissingRequired,
            ProjectionCellState.UnexpectedOccupied };
        var feedback = new ProjectionSurfaceResult(states);
        grid.ShowFeedback(feedback);
        var ordinary = new[] { ColorOf(cells[0]), ColorOf(cells[1]), ColorOf(cells[2]) };
        grid.ShowFeedback(feedback, true);
        for (int i = 0; i < cells.Length; i++)
        {
            Equal(ProjectionFeedbackPalette.GetColor(states[i], true), ColorOf(cells[i]));
            Equal(false, ordinary[i].Equals(ColorOf(cells[i])));
        }
        // Empty matching cells use authored material colors, not a global palette entry.
        Throws<ArgumentException>(() => ProjectionFeedbackPalette.GetColor(ProjectionCellState.MatchingEmpty));
    }

    private static void InvalidFeedbackDoesNotPaint()
    {
        var authored = new Color(.2f, .3f, .4f);
        var cells = new[] { Cell(authored), Cell(authored) };
        var grid = new ProjectionGrid(cells);
        grid.ShowPattern(new[] { 1, 0 });
        Throws<ArgumentNullException>(() => grid.ShowFeedback(null));
        Throws<ArgumentException>(() => grid.ShowFeedback(new ProjectionSurfaceResult(new[] { ProjectionCellState.MatchingEmpty })));
        Equal(Color.black, ColorOf(cells[0]));
        Equal(authored, ColorOf(cells[1]));
    }

    private static void InvalidDetailedEvaluation()
    {
        var grid = new ProjectionGrid(new[] { Cell(new Color(1, 1, 1)) });
        var probe = new ScriptedProbe();
        Throws<ArgumentNullException>(() => grid.Evaluate(null, probe));
        Throws<ArgumentException>(() => grid.Evaluate(new[] { 2 }, probe));
        Throws<ArgumentException>(() => grid.Evaluate(new int[0], probe));
        Throws<ArgumentNullException>(() => grid.Evaluate(new[] { 0 }, null));
        foreach (float distance in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            Throws<ArgumentOutOfRangeException>(() => grid.Evaluate(new[] { 0 }, probe, distance));
        Equal(0, probe.Origins.Count);
    }

    private static void ResultSessionContext()
    {
        Equal(0, new ProjectionResult(0, 0, 0).SessionId);
        Equal(0, new ProjectionResult(0, 0, 0).BlocksSpawned);
        var result = new ProjectionResult(0, 0, 0, 102, 7, 3);
        Equal(102, result.StageNumber);
        Equal(7, result.SessionId);
        Equal(3, result.BlocksSpawned);
        Throws<ArgumentOutOfRangeException>(() => new ProjectionResult(0, 0, 0, 102, -1));
        Throws<ArgumentOutOfRangeException>(() => new ProjectionResult(0, 0, 0, 102, 7, -1));
    }

    private static void DetailedResultSnapshots()
    {
        var floor = new ProjectionSurfaceResult(EveryCellState());
        var wall = new ProjectionSurfaceResult(new[] { ProjectionCellState.MissingRequired });
        var frontWall = new ProjectionSurfaceResult(new[] { ProjectionCellState.MatchingRequired,
            ProjectionCellState.MatchingEmpty });
        var result = new ProjectionResult(floor, wall, frontWall, 102, 7, 3);
        Equal(floor, result.FloorResults);
        Equal(wall, result.WallResults);
        Equal(frontWall, result.FrontWallResults);
        Equal(2, result.FloorMismatches);
        Equal(1, result.WallMismatches);
        Equal(0, result.FrontWallMismatches);
        Equal(3, result.TotalMismatches);
        Equal(false, result.IsSolved);
        Equal(102, result.StageNumber);
        Equal(7, result.SessionId);
        Equal(3, result.BlocksSpawned);
        var copied = result.FloorResults.CopyCellStates();
        copied[2] = ProjectionCellState.MatchingRequired;
        Equal(3, result.TotalMismatches);
        Equal(ProjectionCellState.MissingRequired, result.FloorResults[2]);
    }

    private static void InvalidDetailedResults()
    {
        var surface = new ProjectionSurfaceResult(new[] { ProjectionCellState.MatchingEmpty });
        Throws<ArgumentNullException>(() => new ProjectionResult(null, surface, surface));
        Throws<ArgumentNullException>(() => new ProjectionResult(surface, null, surface));
        Throws<ArgumentNullException>(() => new ProjectionResult(surface, surface, null));
        Throws<ArgumentOutOfRangeException>(() => new ProjectionResult(surface, surface, surface, -1));
        Throws<ArgumentOutOfRangeException>(() => new ProjectionResult(surface, surface, surface, 102, -1));
        Throws<ArgumentOutOfRangeException>(() => new ProjectionResult(surface, surface, surface, 102, 7, -1));
    }

    private sealed class ScriptedProbe : IProjectionProbe
    {
        private readonly Queue<bool> results;
        public readonly List<Vector3> Origins = new List<Vector3>();
        public readonly List<Vector3> Directions = new List<Vector3>();
        public readonly List<float> Distances = new List<float>();
        public int Remaining { get { return results.Count; } }

        public ScriptedProbe(params bool[] results)
        {
            this.results = new Queue<bool>(results);
        }

        public bool IsOccupied(Vector3 origin, Vector3 direction, float maxDistance)
        {
            Origins.Add(origin);
            Directions.Add(direction);
            Distances.Add(maxDistance);
            return results.Dequeue();
        }
    }
}
