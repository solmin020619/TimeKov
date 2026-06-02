using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TIMEKOV.Factory;

/// <summary>
/// StorageExtractor(창고 추출기)의 UI 패널.
/// - 추출할 아이템 선택 슬롯
/// - 창고 잔량 표시
/// - 다음 추출까지 타이머
/// - 왼쪽 인벤토리 슬롯 (드래그 소스)
/// </summary>
public class StorageExtractorUI : MonoBehaviour
{
    [Header("루트 패널")]
    public GameObject uiPanel;

    [Header("설비 이름")]
    public TextMeshProUGUI titleText;

    [Header("아이템 선택 슬롯")]
    public StorageItemSelectSlot itemSelectSlot;

    [Header("타이머 표시")]
    public TextMeshProUGUI timerText;   // "추출까지: 2초"
    public Slider          timerSlider; // 진행 바 (선택 사항)

    [Header("닫기 버튼")]
    public Button closeBtn;

    [Header("인벤토리 슬롯 (왼쪽)")]
    public Transform  inventorySlotParent;
    public GameObject inventorySlotPrefab;
    public int        inventorySlotCount = 20;

    private StorageExtractor _machine;
    private readonly List<InventorySlotUI> _invSlots = new();

    // ── 초기화 ────────────────────────────────────────────────────────

    private void Awake()
    {
        closeBtn?.onClick.AddListener(Close);
    }

    // ── 열기 / 닫기 ───────────────────────────────────────────────────

    public void OpenFor(StorageExtractor machine, string title)
    {
        _machine = machine;

        uiPanel.SetActive(true);
        if (titleText != null) titleText.text = title;

        itemSelectSlot?.Setup(machine);
        BuildInventorySlots();

        var inv = InventoryManager.StorageInstance;
        if (inv != null) inv.OnInventoryChanged += RefreshInventorySlots;
    }

    public void Close()
    {
        if (_machine == null) return;

        itemSelectSlot?.Cleanup();

        var inv = InventoryManager.StorageInstance;
        if (inv != null) inv.OnInventoryChanged -= RefreshInventorySlots;

        _machine = null;
        uiPanel.SetActive(false);

        GameUIController.Instance?.CloseFactoryUI();
    }

    // ── 매 프레임 — 타이머 갱신 ─────────────────────────────────────

    private void Update()
    {
        if (_machine == null || !uiPanel.activeSelf) return;

        float remaining = _machine.TimerRemaining;
        float interval  = _machine.ExtractInterval;

        if (timerText != null)
        {
            if (_machine.SelectedItemId <= 0)
                timerText.text = "아이템을 선택하세요";
            else
                timerText.text = $"추출까지: {remaining:F1}초";
        }

        if (timerSlider != null)
            timerSlider.value = interval > 0f ? 1f - (remaining / interval) : 0f;
    }

    // ── 인벤토리 슬롯 ────────────────────────────────────────────────

    private void BuildInventorySlots()
    {
        var inv = InventoryManager.StorageInstance;
        int slotCount = inv != null ? inv.GetMaxSlots() : inventorySlotCount;

        if (_invSlots.Count != slotCount)
        {
            foreach (var s in _invSlots)
                if (s != null) Destroy(s.gameObject);
            _invSlots.Clear();

            for (int i = 0; i < slotCount; i++)
            {
                var go   = Instantiate(inventorySlotPrefab, inventorySlotParent);
                var slot = go.GetComponent<InventorySlotUI>();
                _invSlots.Add(slot);
            }
        }

        RefreshInventorySlots();
    }

    public void RefreshInventorySlots()
    {
        var inv = InventoryManager.StorageInstance;
        if (inv == null) return;

        var slots = inv.GetSlots();
        for (int i = 0; i < _invSlots.Count; i++)
        {
            if (_invSlots[i] == null) continue;
            InventorySlot slotData = i < slots.Count ? slots[i] : null;
            _invSlots[i].Refresh(slotData, inv);
        }
    }
}
