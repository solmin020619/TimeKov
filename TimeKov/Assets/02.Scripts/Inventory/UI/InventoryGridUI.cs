// InventoryGridUI.cs
// SlotGrid ������Ʈ�� ���̴� ��ũ��Ʈ
// ���� ������ ���� �� InventoryManager ������ ���ε�
// ī�װ��� ���� ����

using System.Collections.Generic;
using UnityEngine;

public class InventoryGridUI : MonoBehaviour
{
    [Header("���� ����")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotGrid;

    private InventoryManager _manager;
    private List<InventorySlotUI> _slotUIs = new List<InventorySlotUI>();
    private ItemCategory? _currentFilter = null;

    // �κ��丮 �Ŵ��� ���ε�
    public void Bind(InventoryManager manager)
    {
        if (_manager != null)
            _manager.OnInventoryChanged -= OnDataChanged;

        _manager = manager;

        if (_manager != null)
            _manager.OnInventoryChanged += OnDataChanged;

        BuildSlots();
        RefreshAll();
    }

    // ī�װ��� ���� ���� (null �̸� ��ü ǥ��)
    public void SetFilter(ItemCategory? category)
    {
        _currentFilter = category;
        RefreshAll();
    }

    // ���� UI ����
    private void BuildSlots()
    {
        foreach (var ui in _slotUIs)
            if (ui != null) Destroy(ui.gameObject);
        _slotUIs.Clear();

        if (_manager == null || slotPrefab == null || slotGrid == null)
        {
            Debug.LogWarning($"[InventoryGridUI:{name}] BuildSlots 중단 — " +
                             $"manager={(_manager == null ? "NULL" : _manager.ownerType.ToString())} " +
                             $"slotPrefab={slotPrefab != null} " +
                             $"slotGrid={slotGrid != null}");
            return;
        }

        int slotCount = _manager.GetMaxSlots();
        for (int i = 0; i < slotCount; i++)
        {
            var obj = Instantiate(slotPrefab, slotGrid);
            obj.name = "Slot_" + i;

            var slotUI = obj.GetComponent<InventorySlotUI>();
            if (slotUI == null)
            {
                Debug.LogError("[InventoryGridUI] slotPrefab �� InventorySlotUI ����: " + obj.name);
                continue;
            }

            _slotUIs.Add(slotUI);
        }
    }

    // ���� ���� �� ��ü ���� ����
    public void RefreshAll()
    {
        if (_manager == null) return;

        var slots = _manager.GetSlots();

        for (int i = 0; i < _slotUIs.Count; i++)
        {
            if (_slotUIs[i] == null) continue;

            if (i >= slots.Count)
            {
                _slotUIs[i].Refresh(null, _manager);
                _slotUIs[i].gameObject.SetActive(false);
                continue;
            }

            var slot = slots[i];
            _slotUIs[i].gameObject.SetActive(true);

            // ī�װ��� ���� ����
            if (!slot.IsEmpty && _currentFilter != null)
            {
                var data = ItemDatabase.GetItem(slot.itemId);
                bool match = data != null && data.itemCategory == _currentFilter.Value;

                if (!match)
                {
                    _slotUIs[i].Refresh(new InventorySlot(), _manager);
                    continue;
                }
            }

            _slotUIs[i].Refresh(slot, _manager);
        }
    }

    private void OnDataChanged() => RefreshAll();

    private void OnDestroy()
    {
        if (_manager != null)
            _manager.OnInventoryChanged -= OnDataChanged;
    }
}