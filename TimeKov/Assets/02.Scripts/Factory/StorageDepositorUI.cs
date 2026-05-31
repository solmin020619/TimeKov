using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TIMEKOV.Factory;

/// <summary>
/// StorageDepositor(창고 적재기)의 UI 패널.
/// - 버퍼 내용 및 잔량 표시
/// - 다음 전송까지 타이머
/// - 연료 슬롯 (FuelDropSlot)
/// - 왼쪽 인벤토리 슬롯
/// </summary>
public class StorageDepositorUI : MonoBehaviour
{
    [Header("루트 패널")]
    public GameObject uiPanel;

    [Header("설비 이름")]
    public TextMeshProUGUI titleText;

    [Header("버퍼 상태")]
    public TextMeshProUGUI bufferText;   // "보관 중: 23개 / 100개"

    [Header("타이머")]
    public TextMeshProUGUI timerText;    // "3초 후 창고 전송" / "⚠ 연료 부족"
    public Slider          timerSlider;  // 진행 바 (선택 사항)

    [Header("연료 슬롯")]
    public FuelDropSlot fuelDropSlot;

    [Header("닫기 버튼")]
    public Button closeBtn;

    [Header("인벤토리 슬롯 (왼쪽)")]
    public Transform  inventorySlotParent;
    public GameObject inventorySlotPrefab;
    public int        inventorySlotCount = 20;

    private StorageDepositor _machine;
    private readonly List<InventorySlotUI> _invSlots = new();

    // ── 초기화 ────────────────────────────────────────────────────────

    private void Awake()
    {
        closeBtn?.onClick.AddListener(Close);
    }

    // ── 열기 / 닫기 ───────────────────────────────────────────────────

    public void OpenFor(StorageDepositor machine, string title)
    {
        _machine = machine;
        _machine.OnBufferChanged += OnBufferChanged;

        uiPanel.SetActive(true);
        if (titleText != null) titleText.text = title;

        var inv = InventoryManager.Instance;
        fuelDropSlot?.Setup(machine, inv);
        if (inv != null) inv.OnInventoryChanged += RefreshInventorySlots;

        BuildInventorySlots();
        RefreshBuffer();
    }

    public void Close()
    {
        if (_machine == null) return;

        _machine.OnBufferChanged -= OnBufferChanged;
        fuelDropSlot?.Cleanup();

        var inv = InventoryManager.Instance;
        if (inv != null) inv.OnInventoryChanged -= RefreshInventorySlots;

        _machine = null;
        uiPanel.SetActive(false);

        GameUIController.Instance?.CloseFactoryUI();
    }

    // ── 콜백 ─────────────────────────────────────────────────────────

    private void OnBufferChanged() => RefreshBuffer();

    private void RefreshBuffer()
    {
        if (_machine == null || bufferText == null) return;

        int total = 0;
        foreach (var kv in _machine.InputBuffer.Stock)
            total += kv.Value;

        bufferText.text = $"보관 중: {total}개 / {_machine.maxBuffer}개";
    }

    // ── 매 프레임 — 타이머 갱신 ─────────────────────────────────────

    private void Update()
    {
        if (_machine == null || !uiPanel.activeSelf) return;

        if (timerText != null)
        {
            if (_machine.Status == MachineStatus.NoFuel)
                timerText.text = "⚠ 연료 부족";
            else
            {
                float t = _machine.NextDepositTime;
                timerText.text = $"{t:F0}초 후 창고 전송";
            }
        }

        if (timerSlider != null)
        {
            float interval = _machine.DepositInterval;
            float t        = _machine.NextDepositTime;
            timerSlider.value = interval > 0f ? 1f - (t / interval) : 0f;
        }
    }

    // ── 인벤토리 슬롯 ────────────────────────────────────────────────

    private void BuildInventorySlots()
    {
        var inv = InventoryManager.Instance;
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
        var inv = InventoryManager.Instance;
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
