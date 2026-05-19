using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TIMEKOV.Factory;

public class MachineUI : MonoBehaviour
{
    [Header("루트 패널")]
    public GameObject uiPanel;

    [Header("설비 이름")]
    public TextMeshProUGUI machineTitleText;

    [Header("인벤토리 슬롯 (왼쪽)")]
    public Transform inventorySlotParent;
    public GameObject inventorySlotPrefab;
    public int inventorySlotCount = 20;

    [Header("재료 슬롯 (오른쪽)")]
    public RecipeDropSlot[] recipeDropSlots;

    [Header("진행 바 / 상태 텍스트")]
    public Slider progressBar;
    public TextMeshProUGUI statusText;

    [Header("출력 슬롯")]
    public MachineSlotWidget outputSlot;

    [Header("플레이어 인벤토리")]
    public InventoryManager playerInventory;

    private ProcessingMachine _machine;
    private readonly List<DraggableSlot> _invSlots = new();

    // ── 공개 API ────────────────────────────────────────────

    public void OpenFor(ProcessingMachine machine, string title)
    {
        if (_machine != null) _machine.OnBufferChanged -= OnBufferChanged;
        _machine = machine;
        _machine.OnBufferChanged += OnBufferChanged;

        uiPanel.SetActive(true);
        if (machineTitleText != null) machineTitleText.text = title;

        BuildRecipeSlots();
        BuildInventorySlots();
        RefreshOutputSlots();
    }

    public void Close()
    {
        if (_machine != null) _machine.OnBufferChanged -= OnBufferChanged;
        _machine = null;
        uiPanel.SetActive(false);
    }

    public void AddItemFromInventory(int itemId, int amount)
        => _machine?.Receive(itemId, amount);

    // ── 인벤토리 슬롯 ───────────────────────────────────────

    private void BuildInventorySlots()
    {
        if (_invSlots.Count == 0)
        {
            for (int i = 0; i < inventorySlotCount; i++)
            {
                var go = Instantiate(inventorySlotPrefab, inventorySlotParent);
                var slot = go.GetComponent<DraggableSlot>();
                _invSlots.Add(slot);
            }
        }

        RefreshInventorySlots();
    }

    public void RefreshInventorySlots()
    {
        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        if (inv == null) { foreach (var slot in _invSlots) slot.Clear(); return; }

        // 인벤토리 슬롯 데이터 반영 — InventoryManager가 슬롯 열거를 지원하면 여기서 채워넣는다
        foreach (var slot in _invSlots) slot.Clear();
        inv.ForceRefreshUI();
    }

    // ── 재료 슬롯 ───────────────────────────────────────────

    private void BuildRecipeSlots()
    {
        if (_machine == null || recipeDropSlots == null) return;

        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0) return;

        var inputs = recipes[0].inputs;

        for (int i = 0; i < recipeDropSlots.Length; i++)
        {
            if (recipeDropSlots[i] == null) continue;

            if (i < inputs.Length)
            {
                recipeDropSlots[i].gameObject.SetActive(true);
                var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
                recipeDropSlots[i].Setup(inputs[i].itemId, inputs[i].amount, _machine, inv);
            }
            else
            {
                recipeDropSlots[i].gameObject.SetActive(false);
            }
        }
    }

    // ── 출력 슬롯 ───────────────────────────────────────────

    private void RefreshOutputSlots()
    {
        if (outputSlot == null || _machine == null) return;

        foreach (var kv in _machine.OutputBuffer.Stock)
        {
            if (kv.Value <= 0) continue;

            outputSlot.gameObject.SetActive(true);
            outputSlot.Setup(kv.Key, kv.Value);

            int id = kv.Key, amt = kv.Value;
            outputSlot.SetDoubleClickAction(() => TakeOutput(id, amt));
            return;
        }

        outputSlot.gameObject.SetActive(false);
    }

    private void TakeOutput(int itemId, int amount)
    {
        if (_machine == null) return;
        if (!_machine.TryTakeOutput(itemId, amount)) return;

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        inv?.AddItem(itemId, amount);
        inv?.ForceRefreshUI();

        RefreshOutputSlots();
        RefreshInventorySlots();
    }

    // ── 버퍼 변경 콜백 ──────────────────────────────────────

    private void OnBufferChanged()
    {
        RefreshOutputSlots();
        RefreshInventorySlots();
        foreach (var slot in recipeDropSlots)
            slot?.PublicRefresh();
    }

    // ── 진행 바 ─────────────────────────────────────────────

    private void Update()
    {
        if (_machine == null || !uiPanel.activeSelf) return;

        if (progressBar != null)
            progressBar.value = _machine.Progress;

        if (statusText == null) return;

        if (_machine.Status == MachineStatus.Processing && _machine.ActiveRecipe != null)
        {
            float remaining = _machine.ActiveRecipe.processingTime * (1f - _machine.Progress);
            statusText.text = $"{remaining:F0}초";
        }
        else
        {
            statusText.text = "";
        }
    }

    // ── 모두 받기 ───────────────────────────────────────────

    public void TakeAll()
    {
        if (_machine == null) return;

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;

        foreach (var kv in new Dictionary<int, int>(_machine.InputBuffer.Stock))
        {
            if (kv.Value > 0)
            {
                _machine.InputBuffer.Consume(kv.Key, kv.Value);
                inv?.AddItem(kv.Key, kv.Value);
            }
        }

        foreach (var kv in new Dictionary<int, int>(_machine.OutputBuffer.Stock))
        {
            if (kv.Value > 0 && _machine.TryTakeOutput(kv.Key, kv.Value))
                inv?.AddItem(kv.Key, kv.Value);
        }

        inv?.ForceRefreshUI();
        _machine.PublicNotifyBufferChanged();
        RefreshInventorySlots();
        RefreshOutputSlots();
    }
}