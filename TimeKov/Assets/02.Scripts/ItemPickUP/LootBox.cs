using System.Collections.Generic;
using UnityEngine;

public class LootBox : MonoBehaviour, IInteractable
{
    public static readonly List<LootBox> All = new List<LootBox>();

    private readonly List<(int itemId, int count)> _contents = new List<(int itemId, int count)>();
    public IReadOnlyList<(int itemId, int count)> Contents => _contents;

    public void Initialize(List<(int itemId, int count)> contents)
    {
        _contents.Clear();
        if (contents != null) _contents.AddRange(contents);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public bool CanInteract => true;

    public void Interact(Player player)
    {
        InventoryManager inv = FindPlayerInventory();
        if (inv == null) return;

        for (int i = 0; i < _contents.Count; i++)
            inv.AddItem(_contents[i].itemId, _contents[i].count);

        Destroy(gameObject);
    }

    private InventoryManager FindPlayerInventory()
    {
        InventoryManager[] all = FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].ownerType == InventoryManager.InventoryOwnerType.Player)
                return all[i];
        return null;
    }
}
