// =====================================================================
// MachineUI.cs
// 설비 가공 UI — 인벤토리 슬롯, 재료 드롭 슬롯, 출력 슬롯 관리
// 구버전 DataStore 참조를 새 스키마(GameDataHolder / DataBoot)로 교체
// =====================================================================

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

    [Header("인벤토리 (왼쪽) — 슬롯 부모 (ScrollView Content)")]
    public Transform inventorySlotParent;
    public GameObject inventorySlotPrefab;
    public int inventorySlotCount = 20;

    [Header("재료 슬롯 (오른쪽) — 배경 위치에 고정")]
    public RecipeDropSlot[] recipeDropSlots;

    [Header("진행 바 / 상태 텍스트")]
    public Slider progressBar;
    public TextMeshProUGUI statusText;

    [Header("출력 슬롯 — 고정 위치")]
    public MachineSlotWidget outputSlot;

    [Header("플레이어 인벤토리")]
    public InventoryManager playerInventory;

    // ── 내부 ─────────────────────────────────────────────────
    private ProcessingMachine _machine;
    private readonly List<DraggableSlot> _invSlots = new();
    private readonly List<RecipeDropSlot> _dropSlots = new();
    private readonly List<GameObject> _outputGos = new();

    // ============================================================
    // 공개 API
    // ============================================================

    public void OpenFor(ProcessingMachine machine, string title)
    {
        Debug.Log($"[MachineUI] OpenFor 호출 machine={machine?.name ?? "null"}");

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
    {
        _machine?.Receive(itemId, amount);
    }

    // ============================================================
    // 인벤토리 슬롯 (왼쪽) — 항상 고정 수만큼 존재
    // ============================================================

    private void BuildInventorySlots()
    {
        // 최초 1회만 생성
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
        // 구버전: DataStore.IsLoaded, DataStore.ItemById
        // 신버전: DataBoot.IsLoaded, GameDataHolder.I.ItemData.All
        if (playerInventory == null || !DataBoot.IsLoaded) return;

        var items = new List<(int id, int amt)>();

        // ItemData 전체를 순회해 보유 중인 아이템만 수집
        // SheetId 는 itemId 문자열 (예: "1101") → int 변환 필요
        foreach (var itemData in GameDataHolder.I.ItemData.All)
        {
            if (!int.TryParse((string)itemData.SheetId, out int itemIdInt))
                continue;

            int total = playerInventory.GetTotalItemCount(itemIdInt);
            if (total > 0)
                items.Add((itemIdInt, total));
        }

        // 슬롯에 반영 (아이템 있으면 채우고, 나머지는 빈칸)
        for (int i = 0; i < _invSlots.Count; i++)
        {
            if (i < items.Count)
                _invSlots[i].SetItem(items[i].id, items[i].amt);
            else
                _invSlots[i].Clear();
        }
    }

    // ============================================================
    // 재료 드롭 슬롯 (오른쪽)
    // ============================================================

    private void BuildRecipeSlots()
    {
        if (_machine == null || recipeDropSlots == null) return;

        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0) return;

        var inputs = recipes[0].inputs;
        Debug.Log($"[MachineUI] inputs 수={inputs?.Length ?? 0}");

        for (int i = 0; i < recipeDropSlots.Length; i++)
        {
            Debug.Log($"[MachineUI] 슬롯[{i}] = {(recipeDropSlots[i] == null ? "null" : recipeDropSlots[i].name)}");

            if (recipeDropSlots[i] == null) continue;

            if (i < inputs.Length)
            {
                recipeDropSlots[i].gameObject.SetActive(true);
                recipeDropSlots[i].Setup(
                    inputs[i].itemId, inputs[i].amount,
                    _machine, playerInventory);
                Debug.Log($"[MachineUI] RecipeSlot[{i}] Setup 완료");
            }
            else
            {
                recipeDropSlots[i].gameObject.SetActive(false);
            }
        }
    }

    // ============================================================
    // 출력 슬롯
    // ============================================================

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
        if (_machine == null || playerInventory == null) return;
        if (!_machine.TryTakeOutput(itemId, amount)) return;
        playerInventory.AddItem(itemId, amount);
        playerInventory.ForceRefreshUI();
        RefreshOutputSlots();
        RefreshInventorySlots();
    }

    // ============================================================
    // 버퍼 변경 콜백
    // ============================================================

    private void OnBufferChanged()
    {
        RefreshOutputSlots();
        RefreshInventorySlots();
        RefreshDropSlots();
    }

    // ============================================================
    // Update — 진행 바
    // ============================================================

    private void Update()
    {
        if (_machine == null || !uiPanel.activeSelf) return;

        if (progressBar != null)
            progressBar.value = _machine.Progress;

        if (statusText == null) return;

        if (_machine.Status == MachineStatus.Processing)
        {
            float remaining = 0f;
            if (_machine.ActiveRecipe != null)
                remaining = _machine.ActiveRecipe.processingTime * (1f - _machine.Progress);

            statusText.text = $"{remaining:F0}초";
        }
        else
        {
            statusText.text = "";
        }
    }

    private void RefreshDropSlots()
    {
        foreach (var slot in recipeDropSlots)
        {
            if (slot != null) slot.PublicRefresh();
        }
    }

    public void TakeAll()
    {
        if (_machine == null || playerInventory == null) return;

        // InputBuffer 전체 회수
        foreach (var kv in new Dictionary<int, int>(_machine.InputBuffer.Stock))
        {
            if (kv.Value <= 0) continue;
            _machine.InputBuffer.Consume(kv.Key, kv.Value);
            playerInventory.AddItem(kv.Key, kv.Value);
        }

        // OutputBuffer 전체 회수
        foreach (var kv in new Dictionary<int, int>(_machine.OutputBuffer.Stock))
        {
            if (kv.Value <= 0) continue;
            _machine.TryTakeOutput(kv.Key, kv.Value);
            playerInventory.AddItem(kv.Key, kv.Value);
        }

        playerInventory.ForceRefreshUI();
        _machine.PublicNotifyBufferChanged();
        RefreshInventorySlots();
        RefreshOutputSlots();
    }
}