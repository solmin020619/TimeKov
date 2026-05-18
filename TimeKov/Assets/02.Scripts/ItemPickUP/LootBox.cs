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

    // F를 누르면 근처(showRange) 박스를 전부 한 번에 수집한다
    public void Interact(Player player)
    {
        LootBoxScanner scanner = FindAnyObjectByType<LootBoxScanner>();
        if (scanner != null)
            scanner.CollectAllInRange(player);
        else
            Collect(player);
    }

    // 이 박스를 수집한다 — 인벤토리에 넣고, 수집 VFX를 띄우고, 박스를 제거
    public void Collect(Player player)
    {
        InventoryManager inv = FindPlayerInventory();
        if (inv != null)
            for (int i = 0; i < _contents.Count; i++)
                inv.AddItem(_contents[i].itemId, _contents[i].count);

        LootBoxVFX vfx = GetComponentInParent<LootBoxVFX>();
        if (vfx != null && player != null)
            vfx.PlayCollectEffect(transform.position, player.transform);

        // LootBox 는 자식(item)에 있으므로 루트째로 파괴한다
        Destroy(transform.root.gameObject);
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
