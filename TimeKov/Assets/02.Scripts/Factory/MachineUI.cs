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

    [Header("닫기 버튼")]
    [Tooltip("설비 UI 닫기 버튼 (없으면 무시됨)")]
    public Button closeBtn;

    [Header("레시피 선택 UI (선택 사항)")]
    [Tooltip("레시피 이전 버튼 (없으면 무시됨)")]
    public Button recipePrevBtn;
    [Tooltip("레시피 다음 버튼 (없으면 무시됨)")]
    public Button recipeNextBtn;
    [Tooltip("현재 레시피 인덱스 표시 텍스트 (예: 1/5)")]
    public TextMeshProUGUI recipeIndexText;
    [Tooltip("현재 레시피 이름 표시 텍스트 (선택 사항)")]
    public TextMeshProUGUI recipeNameText;

    [Header("진행 바 / 상태 텍스트")]
    public Slider progressBar;
    public TextMeshProUGUI statusText;

    [Header("출력 슬롯")]
    public MachineSlotWidget outputSlot;

    [Header("플레이어 인벤토리")]
    public InventoryManager playerInventory;

    private ProcessingMachine _machine;
    private int _selectedRecipeIndex = 0;
    // DraggableSlot → InventorySlotUI 로 전환 (등급 테두리·아이콘 표시 지원)
    private readonly List<InventorySlotUI> _invSlots = new();

    // ── 공개 API ────────────────────────────────────────────

    private void Awake()
    {
        if (closeBtn != null)      closeBtn.onClick.AddListener(Close);
        if (recipePrevBtn != null) recipePrevBtn.onClick.AddListener(PrevRecipe);
        if (recipeNextBtn != null) recipeNextBtn.onClick.AddListener(NextRecipe);
    }

    public void OpenFor(ProcessingMachine machine, string title)
    {
        if (_machine != null) _machine.OnBufferChanged -= OnBufferChanged;
        _machine = machine;
        _machine.OnBufferChanged += OnBufferChanged;

        // 인벤토리 변경 이벤트 구독
        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        if (inv != null) inv.OnInventoryChanged += RefreshInventorySlots;

        // 설비를 열 때마다 첫 번째 레시피로 초기화
        _selectedRecipeIndex = 0;

        uiPanel.SetActive(true);
        if (machineTitleText != null) machineTitleText.text = title;

        BuildRecipeSlots();
        BuildInventorySlots();
        RefreshOutputSlots();
    }

    // ── 레시피 선택 ─────────────────────────────────────────

    public void PrevRecipe()
    {
        if (_machine == null || _machine.Recipes == null || _machine.Recipes.Count == 0) return;
        _selectedRecipeIndex = (_selectedRecipeIndex - 1 + _machine.Recipes.Count) % _machine.Recipes.Count;
        BuildRecipeSlots();
    }

    public void NextRecipe()
    {
        if (_machine == null || _machine.Recipes == null || _machine.Recipes.Count == 0) return;
        _selectedRecipeIndex = (_selectedRecipeIndex + 1) % _machine.Recipes.Count;
        BuildRecipeSlots();
    }

    public void SelectRecipe(int index)
    {
        if (_machine == null || _machine.Recipes == null) return;
        _selectedRecipeIndex = Mathf.Clamp(index, 0, _machine.Recipes.Count - 1);
        BuildRecipeSlots();
    }

    public void Close()
    {
        if (_machine != null) _machine.OnBufferChanged -= OnBufferChanged;

        // 인벤토리 변경 이벤트 구독 해제
        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        if (inv != null) inv.OnInventoryChanged -= RefreshInventorySlots;

        _machine = null;

        // UIUnfoldEffect가 있으면 닫기 애니메이션 후 SetActive(false), 없으면 즉시 비활성화
        var unfold = uiPanel.GetComponent<UIUnfoldEffect>();
        if (unfold != null && uiPanel.activeInHierarchy)
            unfold.Close();
        else
            uiPanel.SetActive(false);

        // 커서·입력 복구 및 HUD 복원은 GameUIController에 위임
        GameUIController.Instance?.CloseFactoryUI();
    }

    public void AddItemFromInventory(int itemId, int amount)
        => _machine?.Receive(itemId, amount);

    // ── 인벤토리 슬롯 ───────────────────────────────────────

    private void BuildInventorySlots()
    {
        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        int slotCount = inv != null ? inv.GetMaxSlots() : inventorySlotCount;

        // 슬롯 수가 다르면 기존 슬롯 제거 후 재생성
        if (_invSlots.Count != slotCount)
        {
            foreach (var s in _invSlots)
                if (s != null) Destroy(s.gameObject);
            _invSlots.Clear();

            for (int i = 0; i < slotCount; i++)
            {
                var go = Instantiate(inventorySlotPrefab, inventorySlotParent);
                var slot = go.GetComponent<InventorySlotUI>();
                if (slot == null)
                    Debug.LogError("[MachineUI] inventorySlotPrefab에 InventorySlotUI 컴포넌트가 없습니다!");
                _invSlots.Add(slot);
            }
        }

        RefreshInventorySlots();
    }

    public void RefreshInventorySlots()
    {
        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        if (inv == null)
        {
            // 인벤토리 없으면 전부 빈 슬롯으로
            foreach (var slot in _invSlots)
                slot?.Refresh(null, null);
            return;
        }

        var slots = inv.GetSlots();
        for (int i = 0; i < _invSlots.Count; i++)
        {
            if (_invSlots[i] == null) continue;
            // InventorySlotUI.Refresh()가 아이콘·등급 테두리·수량 모두 처리
            InventorySlot slotData = i < slots.Count ? slots[i] : null;
            _invSlots[i].Refresh(slotData, inv);
        }
    }

    // ── 재료 슬롯 ───────────────────────────────────────────

    private void BuildRecipeSlots()
    {
        if (_machine == null || recipeDropSlots == null) return;

        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0) return;

        // 선택된 레시피 인덱스 범위 보정
        _selectedRecipeIndex = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
        var selectedRecipe = recipes[_selectedRecipeIndex];
        var inputs = selectedRecipe.inputs;

        // 레시피 선택 UI 갱신
        RefreshRecipeSelectionUI(recipes.Count);

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        for (int i = 0; i < recipeDropSlots.Length; i++)
        {
            if (recipeDropSlots[i] == null) continue;

            if (i < inputs.Length)
            {
                recipeDropSlots[i].gameObject.SetActive(true);
                recipeDropSlots[i].Setup(inputs[i].itemId, inputs[i].amount, _machine, inv);
            }
            else
            {
                recipeDropSlots[i].gameObject.SetActive(false);
            }
        }
    }

    private void RefreshRecipeSelectionUI(int totalCount)
    {
        // 이전/다음 버튼 표시 여부 (레시피가 1개면 숨김)
        bool multiRecipe = totalCount > 1;
        if (recipePrevBtn != null) recipePrevBtn.gameObject.SetActive(multiRecipe);
        if (recipeNextBtn != null) recipeNextBtn.gameObject.SetActive(multiRecipe);

        // 인덱스 텍스트 갱신 (예: "2 / 5")
        if (recipeIndexText != null)
            recipeIndexText.text = multiRecipe ? $"{_selectedRecipeIndex + 1} / {totalCount}" : "";

        // 레시피 이름 텍스트 갱신
        if (recipeNameText != null && _machine != null && _machine.Recipes != null
            && _selectedRecipeIndex < _machine.Recipes.Count)
        {
            var recipe = _machine.Recipes[_selectedRecipeIndex];
            recipeNameText.text = !string.IsNullOrEmpty(recipe.recipeName) ? recipe.recipeName : "";
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

        GameEvents.RaiseItemAcquired(itemId, amount);

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
            {
                inv?.AddItem(kv.Key, kv.Value);
                GameEvents.RaiseItemAcquired(kv.Key, kv.Value);
            }
        }

        _machine.PublicNotifyBufferChanged();
        RefreshInventorySlots();
        RefreshOutputSlots();
    }
}