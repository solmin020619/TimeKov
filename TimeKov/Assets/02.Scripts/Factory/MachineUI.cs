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

    [Header("회수 버튼")]
    [Tooltip("재료 회수 버튼 — RecipeSlot(InputBuffer)의 아이템만 인벤토리로 반환")]
    public Button takeInputsBtn;
    [Tooltip("모두 받기 버튼 — OutputSlot(현재 레시피 결과물)만 인벤토리로 가져옴")]
    public Button takeOutputBtn;

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

    // [Gauge] 가공 진행 게이지 — 기존 가운데 ">>" 화살표 대체용
    // 비워두면 게이지 동작 안 함 (안전 가드, 기존 동작 유지)
    [Header("진행 게이지")]
    [Tooltip("가운데 화살표 자리의 ProcessingGauge. 비워두면 게이지 동작 안 함.")]
    [SerializeField] private ProcessingGauge processingGauge;

    [Header("설비 도면 이미지")]
    [Tooltip("중앙 설비 모델 렌더. OpenFor에서 facilityId로 자동 세팅(FacilityIconDatabase).")]
    [SerializeField] private Image facilityImage;

    [Header("가방/창고 탭")]
    [Tooltip("좌측 인벤 용량 표시 (예: 용량 11 / 35).")]
    [SerializeField] private TextMeshProUGUI bagCapacityText;
    [Tooltip("가방 보기 탭 버튼.")]
    [SerializeField] private Button bagTabBtn;
    [Tooltip("창고(Storage) 보기 탭 버튼.")]
    [SerializeField] private Button storageTabBtn;
    private bool _showStorage;

    [Header("플레이어 인벤토리")]
    public InventoryManager playerInventory;

    [Header("연료 슬롯")]
    [Tooltip("FuelDropSlot 컴포넌트가 붙은 연료 슬롯 오브젝트.")]
    public FuelDropSlot fuelDropSlot;

    [Header("드래그&드랍 설정")]
    [Tooltip("인벤토리 패널에 붙어 있는 InventoryPanelDropZone 오브젝트.\n" +
             "비워두면 inventorySlotParent 부모에서 자동으로 찾거나 추가합니다.")]
    public InventoryPanelDropZone inventoryDropZone;

    private ProcessingMachine _machine;
    private int _selectedRecipeIndex = 0;

    // statusText(연료 부족 — 연료 칸 위)와 분리된 제작 시간 표시용.
    // statusText를 복제해 원래 중앙(진행바 위) 위치에 배치한다.
    private TextMeshProUGUI _processTimeText;
    private readonly List<InventorySlotUI> _invSlots = new();
    /// <summary>outputSlot 외에 동적으로 생성된 추가 출력 슬롯들.</summary>
    private readonly List<MachineSlotWidget> _extraOutputSlots = new();

    // ── 초기화 ────────────────────────────────────────────────

    private void Awake()
    {
        if (closeBtn != null)       closeBtn.onClick.AddListener(Close);
        if (recipePrevBtn != null)  recipePrevBtn.onClick.AddListener(PrevRecipe);
        if (recipeNextBtn != null)  recipeNextBtn.onClick.AddListener(NextRecipe);
        if (takeInputsBtn != null)  takeInputsBtn.onClick.AddListener(TakeAllInputs);
        if (takeOutputBtn != null)  takeOutputBtn.onClick.AddListener(TakeAll);
        if (bagTabBtn != null)      bagTabBtn.onClick.AddListener(ShowBag);
        if (storageTabBtn != null)  storageTabBtn.onClick.AddListener(ShowStorage);

        SetupDropZone();
        SetupProcessTimeText();
    }

    // ── 제작 시간 텍스트 분리 ───────────────────────────────────────────
    // statusText는 연료 칸 위로 옮겨져 "연료 부족" 전용이 됐으므로,
    // 제작 시간("N초")은 statusText를 복제해 원래 위치(진행바 위, 중앙)에 따로 띄운다.
    private void SetupProcessTimeText()
    {
        if (_processTimeText != null) return;       // 이미 생성됨
        if (statusText == null) return;

        var clone = Instantiate(statusText.gameObject, statusText.transform.parent);
        clone.name = "ProcessTimeText";
        _processTimeText = clone.GetComponent<TextMeshProUGUI>();

        // statusText가 원래 있던 중앙(진행바 위) 위치로 복귀.
        _processTimeText.rectTransform.anchoredPosition = new Vector2(-14.001f, 98f);
        _processTimeText.text = "";
    }

    // ── 드롭존 자동 설정 ────────────────────────────────────────

    private void SetupDropZone()
    {
        if (inventoryDropZone != null)
        {
            inventoryDropZone.SetDropCallback(TakeOutput);
            return;
        }

        if (inventorySlotParent == null) return;

        inventoryDropZone = inventorySlotParent.GetComponent<InventoryPanelDropZone>();

        if (inventoryDropZone == null && inventorySlotParent.parent != null)
            inventoryDropZone = inventorySlotParent.parent.GetComponent<InventoryPanelDropZone>();

        if (inventoryDropZone == null)
        {
            inventoryDropZone = inventorySlotParent.gameObject.AddComponent<InventoryPanelDropZone>();
        }

        inventoryDropZone.SetDropCallback(TakeOutput);
    }

    // ── 설비 UI 열기 ────────────────────────────────────────────

    public void OpenFor(ProcessingMachine machine, string title)
    {
        if (_machine != null) _machine.OnBufferChanged -= OnBufferChanged;
        _machine = machine;
        _machine.OnBufferChanged += OnBufferChanged;

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        if (inv != null)
        {
            inv.OnInventoryChanged -= RefreshInventorySlots;
            inv.OnInventoryChanged += RefreshInventorySlots;
        }

        _selectedRecipeIndex = Mathf.Max(0, machine.LockedRecipeIndex);

        if (machineTitleText != null) machineTitleText.text = title;

        // 중앙 설비 도면 = 퀵슬롯 설비 모델 렌더 재사용 (facilityId 매핑)
        if (facilityImage != null)
        {
            var fIcon = FacilityIconDatabase.Instance != null
                ? FacilityIconDatabase.Instance.GetIcon(machine.FacilityId) : null;
            facilityImage.sprite = fIcon;
            facilityImage.enabled = fIcon != null;
        }

        // 모든 슬롯을 먼저 구성한 뒤 패널을 활성화 —
        // SetActive 이전에 Setup을 완료해야 RecipeDropSlot의 Start/OnEnable
        // 기본 상태("재료 넣기" + 흰 박스)가 한 프레임 깜빡이는 현상을 방지한다.
        _showStorage = false;   // 열 때 항상 가방 뷰부터
        BuildRecipeSlots();
        BuildInventorySlots();
        UpdateTabVisual();
        RefreshOutputSlots();

        // 연료 슬롯 초기화
        var inv2 = playerInventory != null ? playerInventory : InventoryManager.Instance;
        fuelDropSlot?.Setup(machine, inv2);

        uiPanel.SetActive(true);

        ShowFirstMachineHintIfNeeded();

        // [Gauge] 게이지 초기화 — 가공 시작 전 0%로 비우고 숨김
        if (processingGauge != null) processingGauge.StopAndHide();

        // 인벤토리에서 아이템을 집어든 순간 연료/재료 슬롯을 강조 (드랍 위치 안내)
        InventorySlotUI.OnAnySlotDragBegin -= OnInventoryDragBegin;
        InventorySlotUI.OnAnySlotDragBegin += OnInventoryDragBegin;
        InventorySlotUI.OnAnySlotDragEnd   -= OnInventoryDragEnd;
        InventorySlotUI.OnAnySlotDragEnd   += OnInventoryDragEnd;
    }

    // ── 레시피 선택 ─────────────────────────────────────────────

    public void PrevRecipe()
    {
        if (_machine == null || _machine.Recipes == null || _machine.Recipes.Count == 0) return;
        _selectedRecipeIndex = (_selectedRecipeIndex - 1 + _machine.Recipes.Count) % _machine.Recipes.Count;
        // SetLockedRecipe 호출 없음 — 화살표는 탐색용, 생산 레시피는 재료 투입 시 결정
        BuildRecipeSlots();
        RefreshOutputSlots();
    }

    public void NextRecipe()
    {
        if (_machine == null || _machine.Recipes == null || _machine.Recipes.Count == 0) return;
        _selectedRecipeIndex = (_selectedRecipeIndex + 1) % _machine.Recipes.Count;
        // SetLockedRecipe 호출 없음 — 화살표는 탐색용, 생산 레시피는 재료 투입 시 결정
        BuildRecipeSlots();
        RefreshOutputSlots();
    }

    public void SelectRecipe(int index)
    {
        if (_machine == null || _machine.Recipes == null) return;
        _selectedRecipeIndex = Mathf.Clamp(index, 0, _machine.Recipes.Count - 1);
        _machine.SetLockedRecipe(_selectedRecipeIndex);
        BuildRecipeSlots();
        RefreshOutputSlots();
    }

    // ── 닫기 ────────────────────────────────────────────────────

    public void Close()
    {
        if (_machine != null) _machine.OnBufferChanged -= OnBufferChanged;

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        if (inv != null) inv.OnInventoryChanged -= RefreshInventorySlots;

        InventorySlotUI.OnAnySlotDragBegin -= OnInventoryDragBegin;
        InventorySlotUI.OnAnySlotDragEnd   -= OnInventoryDragEnd;
        ClearDropHighlights();

        var hintMgr = TimeKov.UI.HintArrowManager.I;
        if (hintMgr != null)
        {
            hintMgr.Hide("machine_output");
            hintMgr.Hide("recipe_slot_hint");
            hintMgr.Hide("first_machine_hint");
        }

        // [Gauge] 게이지 정리 — 패널 닫을 때 0%로 비우고 숨김
        if (processingGauge != null) processingGauge.StopAndHide();

        // 연료 슬롯 정리
        fuelDropSlot?.Cleanup();

        _machine = null;

        var unfold = uiPanel.GetComponent<UIUnfoldEffect>();
        if (unfold != null && uiPanel.activeInHierarchy)
            unfold.Close();
        else
            uiPanel.SetActive(false);

        GameUIController.Instance?.CloseFactoryUI();
    }

    public void AddItemFromInventory(int itemId, int amount)
    {
        if (_machine == null) return;
        if (!_machine.CanReceive(itemId)) return;
        _machine.Receive(itemId, amount);
    }

    // ── 인벤토리 슬롯 ───────────────────────────────────────────

    private void BuildInventorySlots()
    {
        var inv = ActiveInv();
        int slotCount = inv != null ? inv.GetMaxSlots() : inventorySlotCount;

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
        var inv = ActiveInv();
        if (bagCapacityText != null && inv != null)
            bagCapacityText.text = $"용량 {inv.GetUsedSlotCount()} / {inv.GetMaxSlots()}";
        if (inv == null)
        {
            foreach (var slot in _invSlots)
                slot?.Refresh(null, null);
            return;
        }

        var slots = inv.GetSlots();
        for (int i = 0; i < _invSlots.Count; i++)
        {
            if (_invSlots[i] == null) continue;
            InventorySlot slotData = i < slots.Count ? slots[i] : null;
            _invSlots[i].Refresh(slotData, inv);
        }
    }

    // ── 가방/창고 탭 ─────────────────────────────────────────────
    // 기본 _showStorage=false -> 가방(player) 뷰. 창고 탭 누르면 StorageInstance(50칸) 뷰.
    // 드래그 출처는 InventorySlotUI.Refresh(slotData, inv) 가 Owner=inv 로 잡아주므로
    // 재료/연료 슬롯으로 드래그하면 해당 인벤(가방 or 창고)에서 차감된다.

    private InventoryManager ActiveInv()
        => _showStorage ? InventoryManager.StorageInstance
                        : (playerInventory != null ? playerInventory : InventoryManager.Instance);

    public void ShowBag()     => SetView(false);
    public void ShowStorage() => SetView(true);

    private void SetView(bool storage)
    {
        if (_showStorage == storage) return;
        _showStorage = storage;
        BuildInventorySlots();   // 칸 수(가방35/창고50) 다를 수 있어 재구성 + 갱신
        UpdateTabVisual();
    }

    private void UpdateTabVisual()
    {
        SetTabActive(bagTabBtn, !_showStorage);
        SetTabActive(storageTabBtn, _showStorage);
    }

    // 선택 탭은 또렷(알파 0.9), 비선택은 흐리게(0.2). 색 RGB는 빌더값 유지, 알파만 토글.
    private static void SetTabActive(Button b, bool on)
    {
        if (b == null || b.image == null) return;
        var c = b.image.color; c.a = on ? 0.9f : 0.2f; b.image.color = c;
    }

    // ── 재료 슬롯 ───────────────────────────────────────────────

    private void BuildRecipeSlots()
    {
        if (_machine == null || recipeDropSlots == null) return;

        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0) return;

        _selectedRecipeIndex = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
        var selectedRecipe = recipes[_selectedRecipeIndex];
        var inputs = selectedRecipe.inputs;

        RefreshRecipeSelectionUI(recipes.Count);

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        for (int i = 0; i < recipeDropSlots.Length; i++)
        {
            if (recipeDropSlots[i] == null) continue;

            if (i < inputs.Length)
            {
                recipeDropSlots[i].gameObject.SetActive(true);
                // _selectedRecipeIndex 전달 — 재료 드랍 시 해당 레시피로 생산 고정
                recipeDropSlots[i].Setup(inputs[i].itemId, inputs[i].amount, _machine, inv, _selectedRecipeIndex);
            }
            else
            {
                recipeDropSlots[i].gameObject.SetActive(false);
            }
        }

        ShowRecipeHintIfQuestActive();
    }

    private void RefreshRecipeSelectionUI(int totalCount)
    {
        bool multiRecipe = totalCount > 1;
        if (recipePrevBtn != null) recipePrevBtn.gameObject.SetActive(multiRecipe);
        if (recipeNextBtn != null) recipeNextBtn.gameObject.SetActive(multiRecipe);

        if (recipeIndexText != null)
            recipeIndexText.text = multiRecipe ? $"{_selectedRecipeIndex + 1} / {totalCount}" : "";

        if (recipeNameText != null && _machine != null && _machine.Recipes != null
            && _selectedRecipeIndex < _machine.Recipes.Count)
        {
            var recipe = _machine.Recipes[_selectedRecipeIndex];
            recipeNameText.text = !string.IsNullOrEmpty(recipe.recipeName) ? recipe.recipeName : "";
        }
    }

    // ── 출력 슬롯 ───────────────────────────────────────────────

    private void RefreshOutputSlots()
    {
        if (outputSlot == null || _machine == null) return;

        // 이전에 동적으로 생성된 추가 슬롯 제거
        foreach (var s in _extraOutputSlots)
            if (s != null) Destroy(s.gameObject);
        _extraOutputSlots.Clear();

        // 현재 선택된 레시피의 outputs 가져오기
        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0)
        {
            outputSlot.gameObject.SetActive(false);
            return;
        }

        int recipeIdx = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
        var recipe    = recipes[recipeIdx];
        var outputs   = recipe?.outputs;

        if (outputs == null || outputs.Length == 0)
        {
            outputSlot.gameObject.SetActive(false);
            inventoryDropZone?.SetDropCallback(null);
            TimeKov.UI.HintArrowManager.I?.Hide("machine_output");
            return;
        }

        Transform slotParent = outputSlot.transform.parent;
        int slotIndex = 0; // 실제로 표시된 슬롯 수

        foreach (var output in outputs)
        {
            int buffered = _machine.OutputBuffer.GetAmount(output.itemId);
            if (buffered <= 0) continue; // 버퍼에 없으면 슬롯 표시 안 함

            MachineSlotWidget slot;
            if (slotIndex == 0)
            {
                slot = outputSlot;
                slot.gameObject.SetActive(true);
            }
            else
            {
                var go = Instantiate(outputSlot.gameObject, slotParent);
                slot = go.GetComponent<MachineSlotWidget>();
                _extraOutputSlots.Add(slot);
            }

            int id = output.itemId, amt = buffered;
            slot.Setup(id, amt);
            slot.SetDoubleClickAction(() => TakeOutput(id, amt));

            // 첫 슬롯에만 힌트 화살표
            if (slotIndex == 0)
            {
                inventoryDropZone?.SetDropCallback(TakeOutput);

                var hintCanvas = slot.GetComponentInParent<Canvas>();
                var hintRect   = slot.GetComponent<RectTransform>();
                if (hintCanvas != null && hintRect != null)
                    TimeKov.UI.HintArrowManager.I?.ShowOnUI("machine_output", hintRect, hintCanvas, 0f);
            }

            slotIndex++;
        }

        if (slotIndex == 0) // 현재 레시피 결과물이 버퍼에 없음 → 결과물을 흐리게 미리보기
        {
            outputSlot.gameObject.SetActive(true);
            outputSlot.SetupPreview(outputs[0].itemId);
            outputSlot.SetDoubleClickAction(null);
            inventoryDropZone?.SetDropCallback(null);
            TimeKov.UI.HintArrowManager.I?.Hide("machine_output");
        }
    }

    private void TakeOutput(int itemId, int amount)
    {
        if (_machine == null) return;
        if (!_machine.TryTakeOutput(itemId, amount)) return;

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        int leftover = inv != null ? inv.AddItem(itemId, amount) : amount;
        inv?.ForceRefreshUI();

        // 가방에 못 들어간 분량은 창고로 (창고는 거의 무한)
        if (leftover > 0)
        {
            var storage = InventoryManager.StorageInstance;
            if (storage != null) storage.AddItem(itemId, leftover);
            ToastManager.Info("인벤토리가 가득 차 창고로 이동했습니다");
        }

        GameEvents.RaiseItemAcquired(itemId, amount);

        RefreshOutputSlots();
        RefreshInventorySlots();
    }

    // ── 버퍼 변경 콜백 ──────────────────────────────────────────

    private void OnBufferChanged()
    {
        RefreshOutputSlots();
        RefreshInventorySlots();
        foreach (var slot in recipeDropSlots)
            slot?.PublicRefresh();

        ShowRecipeHintIfQuestActive();
    }

    // ── 인벤 드래그 시작/종료에 맞춘 드랍 대상 강조 ──────────────

    /// <summary>인벤토리에서 아이템을 집어들면 그 아이템을 받는 슬롯만 강조한다.</summary>
    private void OnInventoryDragBegin(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;
        int id = slot.SlotData.itemId;

        if (fuelDropSlot != null && id == fuelDropSlot.AcceptedItemId)
            fuelDropSlot.SetDragHighlight(true);

        if (recipeDropSlots != null)
        {
            foreach (var r in recipeDropSlots)
                if (r != null && r.gameObject.activeSelf && r.RequiredItemId == id)
                    r.SetDragHighlight(true);
        }
    }

    private void OnInventoryDragEnd(InventorySlotUI slot)
    {
        ClearDropHighlights();
    }

    private void ClearDropHighlights()
    {
        if (fuelDropSlot != null) fuelDropSlot.SetDragHighlight(false);
        if (recipeDropSlots != null)
        {
            foreach (var r in recipeDropSlots)
                if (r != null) r.SetDragHighlight(false);
        }
    }

    // ── 진행 바 ─────────────────────────────────────────────────

    private void Update()
    {
        if (_machine == null || !uiPanel.activeSelf) return;

        // 현재 화면에 표시된 레시피가 실제 생산 중인 레시피와 일치할 때만 진행 바 표시
        var recipes = _machine.Recipes;
        bool isSelectedRecipeActive =
            _machine.ActiveRecipe != null
            && recipes != null
            && _selectedRecipeIndex >= 0
            && _selectedRecipeIndex < recipes.Count
            && recipes[_selectedRecipeIndex] == _machine.ActiveRecipe;

        if (progressBar != null)
            progressBar.value = isSelectedRecipeActive ? _machine.Progress : 0f;

        // [Gauge] 현재 표시된 레시피가 실제 생산 중일 때만 게이지 진행도 동기, 아니면 숨김
        // progressBar 와 같은 조건 — UI 일관성 유지
        // [Sync Fix] StartProcessing() 호출 금지 — 자체 타이머(3.5초 사이클)가 켜져서
        // _machine.Progress(실제 가공 시간) 와 충돌하면 게이지가 "지맘대로" 갔다가 다시 가는 현상 발생.
        // GO만 활성화하고 SetProgress 로만 외부 동기 모드 운영.
        if (processingGauge != null)
        {
            if (isSelectedRecipeActive && _machine.IsProcessing)
            {
                if (!processingGauge.gameObject.activeSelf)
                    processingGauge.gameObject.SetActive(true);
                processingGauge.SetProgress(_machine.Progress);
            }
            else
            {
                if (processingGauge.gameObject.activeSelf)
                    processingGauge.StopAndHide();
            }
        }

        if (statusText == null) return;

        if (_machine.Status == MachineStatus.NoFuel)
        {
            // 연료 슬롯에 "연료 넣기" 프롬프트가 떠 있으면(연료 드래그/호버) 같은 자리에 겹치므로 그땐 경고를 숨긴다.
            // (옛 경고기호는 Static 한글 폰트에 없어 깨진 네모로 떠서 텍스트만 남김 - 강조는 statusText 색으로.)
            bool inserting = fuelDropSlot != null && fuelDropSlot.IsInsertPromptVisible;
            statusText.text = inserting ? "" : "연료 부족";              // 연료 칸 위
            if (_processTimeText != null) _processTimeText.text = "";
        }
        else if (isSelectedRecipeActive)
        {
            statusText.text = "";                                         // 연료 부족 칸은 비움
            float remaining = _machine.processingTime * (1f - _machine.Progress);
            if (_processTimeText != null)
                _processTimeText.text = $"{remaining:F0}초";              // 제작 시간은 중앙(진행바 위)
        }
        else
        {
            statusText.text = "";
            if (_processTimeText != null) _processTimeText.text = "";
        }
    }

    // ── 재료 회수 버튼 (InputBuffer → 인벤토리) ─────────────────

    /// <summary>현재 선택된 레시피의 재료(InputBuffer)만 인벤토리로 반환한다.</summary>
    public void TakeAllInputs()
    {
        if (_machine == null) return;

        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0) return;

        int recipeIdx = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
        var inputs    = recipes[recipeIdx]?.inputs;
        if (inputs == null) return;

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;

        foreach (var input in inputs)
        {
            int buffered = _machine.InputBuffer.GetAmount(input.itemId);
            if (buffered <= 0) continue;
            _machine.InputBuffer.Consume(input.itemId, buffered);
            inv?.AddItem(input.itemId, buffered);
        }

        _machine.PublicNotifyBufferChanged();
        RefreshInventorySlots();
        foreach (var slot in recipeDropSlots)
            slot?.PublicRefresh();
    }

    // ── 모두 받기 버튼 (OutputBuffer → 인벤토리) ────────────────

    /// <summary>현재 선택된 레시피의 OutputBuffer 결과물을 전부 인벤토리로 가져온다.</summary>
    public void TakeAll()
    {
        if (_machine == null) return;

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;

        var recipes = _machine.Recipes;
        if (recipes != null && recipes.Count > 0)
        {
            int recipeIdx = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
            var outputs   = recipes[recipeIdx]?.outputs;
            if (outputs != null)
            {
                bool movedToStorage = false;
                var storage = InventoryManager.StorageInstance;
                foreach (var output in outputs)
                {
                    int buffered = _machine.OutputBuffer.GetAmount(output.itemId);
                    if (buffered > 0 && _machine.TryTakeOutput(output.itemId, buffered))
                    {
                        int leftover = inv != null ? inv.AddItem(output.itemId, buffered) : buffered;
                        if (leftover > 0 && storage != null) { storage.AddItem(output.itemId, leftover); movedToStorage = true; }
                        GameEvents.RaiseItemAcquired(output.itemId, buffered);
                    }
                }
                if (movedToStorage) ToastManager.Info("인벤토리가 가득 차 창고로 이동했습니다");
            }
        }

        _machine.PublicNotifyBufferChanged();
        _machine.ResetStatusIfIdle();
        RefreshInventorySlots();
        RefreshOutputSlots();
    }

    // ── HintArrow 가이드 ─────────────────────────────────────────

    const string FirstMachineHintKey = "HintArrow_FirstMachineOpen";

    void ShowFirstMachineHintIfNeeded()
    {
        if (PlayerPrefs.GetInt(FirstMachineHintKey, 0) == 1) return;
        if (_invSlots == null || _invSlots.Count == 0) return;

        var firstSlot = _invSlots[0];
        if (firstSlot == null) return;

        var canvas = firstSlot.GetComponentInParent<Canvas>();
        var rect = firstSlot.GetComponent<RectTransform>();
        var mgr = TimeKov.UI.HintArrowManager.I;
        if (mgr == null || canvas == null || rect == null) return;

        mgr.ShowOnUI("first_machine_hint", rect, canvas, 5f);
        PlayerPrefs.SetInt(FirstMachineHintKey, 1);
        PlayerPrefs.Save();
    }

    void ShowRecipeHintIfQuestActive()
    {
        var mgr = TimeKov.UI.HintArrowManager.I;
        if (mgr == null) return;

        if (QuestManager.Instance == null || _machine == null || recipeDropSlots == null)
        {
            mgr.Hide("recipe_slot_hint");
            return;
        }

        int targetItemId = 0;
        foreach (var rt in QuestManager.Instance.Runtimes)
        {
            if (rt?.activeObjectives == null) continue;
            foreach (var obj in rt.activeObjectives)
            {
                if (obj is FacilityInputObjective inp && !inp.IsCompleted)
                {
                    if (inp.facilityId == 0 || inp.facilityId == _machine.FacilityId)
                    {
                        int have = _machine.InputBuffer.GetAmount(inp.inputItemId);
                        if (have >= inp.requiredCount) continue;
                        targetItemId = inp.inputItemId;
                        break;
                    }
                }
            }
            if (targetItemId != 0) break;
        }

        if (targetItemId == 0)
        {
            mgr.Hide("recipe_slot_hint");
            return;
        }

        foreach (var slot in recipeDropSlots)
        {
            if (slot == null || !slot.gameObject.activeSelf) continue;
            if (slot.RequiredItemId != targetItemId) continue;

            var canvas = slot.GetComponentInParent<Canvas>();
            var rect = slot.GetComponent<RectTransform>();
            if (canvas != null && rect != null)
            {
                mgr.ShowOnUI("recipe_slot_hint", rect, canvas, 0f);
                return;
            }
        }

        mgr.Hide("recipe_slot_hint");
    }
}
