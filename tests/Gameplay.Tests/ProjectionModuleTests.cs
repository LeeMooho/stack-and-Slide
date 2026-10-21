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
    }

    private static void InvalidResultCounts()
    {
        Throws<ArgumentOutOfRangeException>(() => new ProjectionResult(-1, 0, 0));
        Throws<ArgumentOutOfRangeException>(() => new ProjectionResult(0, -1, 0));
        Throws<ArgumentOutOfRangeException>(() => new ProjectionResult(0, 0, -1));
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
