using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

internal static class InventoryPresenterTests
{
    public static void Register(Action<string, Action> run)
    {
        run("Inventory presenter handles a new piece ID in binding order", NewPieceDisplay);
        run("Inventory presenter reuses the first slot after an item is exhausted", CompactDisplay);
        run("Missing extension UI is rejected before existing icons change", MissingBinding);
        run("Insufficient inventory locations leave the previous display unchanged", InsufficientSlots);
        run("Duplicate UI binding IDs are rejected during composition", DuplicateBindings);
    }

    private static InventoryBinding Binding(int id)
    {
        return new InventoryBinding(id, new GameObject(), new GameObject().AddComponent<Text>());
    }

    private static RectTransform[] Locations(int count)
    {
        var result = new RectTransform[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = new GameObject().AddComponent<RectTransform>();
            result[i].position = new Vector3(10 + i * 20, 0, 0);
        }
        return result;
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static void NewPieceDisplay()
    {
        var first = Binding(8);
        var second = Binding(1);
        var hidden = Binding(2);
        var locations = Locations(2);
        var presenter = new InventoryPresenter(new[] { first, second, hidden }, locations);
        var inventory = new BlockInventory();
        inventory.Reset(new Dictionary<int, int> { { 1, 2 }, { 8, 3 } });
        presenter.Render(inventory);
        Equal(locations[0].position, first.Image.transform.position);
        Equal(locations[1].position, second.Image.transform.position);
        Equal("3", first.CountText.text);
        Equal("2", second.CountText.text);
        Equal(false, hidden.Image.activeSelf);
    }

    private static void CompactDisplay()
    {
        var first = Binding(1);
        var next = Binding(8);
        var locations = Locations(2);
        var presenter = new InventoryPresenter(new[] { first, next }, locations);
        var inventory = new BlockInventory();
        inventory.Reset(new Dictionary<int, int> { { 1, 1 }, { 8, 1 } });
        presenter.Render(inventory);
        inventory.TryConsume(1);
        presenter.Render(inventory);
        Equal(false, first.Image.activeSelf);
        Equal(locations[0].position, next.Image.transform.position);
    }

    private static void MissingBinding()
    {
        var binding = Binding(1);
        var presenter = new InventoryPresenter(new[] { binding }, Locations(2));
        var inventory = new BlockInventory();
        inventory.Reset(new Dictionary<int, int> { { 1, 1 } });
        presenter.Render(inventory);
        var before = binding.Image.transform.position;
        inventory.Reset(new Dictionary<int, int> { { 8, 1 } });
        Throws<InvalidOperationException>(() => presenter.Render(inventory));
        Equal(true, binding.Image.activeSelf);
        Equal(before, binding.Image.transform.position);
        Equal("1", binding.CountText.text);
    }

    private static void InsufficientSlots()
    {
        var first = Binding(1);
        var second = Binding(8);
        var presenter = new InventoryPresenter(new[] { first, second }, Locations(1));
        var inventory = new BlockInventory();
        inventory.Reset(new Dictionary<int, int> { { 1, 1 } });
        presenter.Render(inventory);
        inventory.Reset(new Dictionary<int, int> { { 1, 1 }, { 8, 1 } });
        Throws<InvalidOperationException>(() => presenter.Render(inventory));
        Equal(false, second.Image.activeSelf);
        Equal(true, first.Image.activeSelf);
    }

    private static void DuplicateBindings()
    {
        Throws<ArgumentException>(() => new InventoryPresenter(new[] { Binding(8), Binding(8) }, Locations(2)));
    }
}
