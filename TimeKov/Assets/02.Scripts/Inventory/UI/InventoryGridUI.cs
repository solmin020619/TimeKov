// InventoryGridUI.cs
// SlotGrid 오브젝트에 붙이는 스크립트
// 슬롯 프리팹 생성 및 InventoryManager 데이터 바인딩
// 카테고리 필터 적용

using System.Collections.Generic;
using UnityEngine;

public class InventoryGridUI : MonoBehaviour
{
    [Header("슬롯 설정")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotGrid;

    private InventoryManager _manager;
    private List<InventorySlotUI> _slotUIs = new List<InventorySlotUI>();
    private ItemCategory? _currentFilter = null;

    // 인벤토리 매니저 바인딩
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

    // 카테고리 필터 변경 (null 이면 전체 표시)
    public void SetFilter(ItemCategory? category)
    {
        _currentFilter = category;
        RefreshAll();
    }

    // 슬롯 UI 생성
    private void BuildSlots()
    {
        foreach (var ui in _slotUIs)
            if (ui != null) Destroy(ui.gameObject);
        _slotUIs.Clear();

        if (_manager == null || slotPrefab == null || slotGrid == null) return;

        int slotCount = _manager.GetMaxSlots();
        for (int i = 0; i < slotCount; i++)
        {
            var obj = Instantiate(slotPrefab, slotGrid);
            obj.name = "Slot_" + i;

            var slotUI = obj.GetComponent<InventorySlotUI>();
            if (slotUI == null)
            {
                Debug.LogError("[InventoryGridUI] slotPrefab 에 InventorySlotUI 없음: " + obj.name);
                continue;
            }

            _slotUIs.Add(slotUI);
        }
    }

    // 필터 적용 후 전체 슬롯 갱신
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

            // 카테고리 필터 적용
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