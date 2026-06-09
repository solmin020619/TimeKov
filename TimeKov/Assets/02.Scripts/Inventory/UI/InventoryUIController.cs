// InventoryUIController.cs
// InventoryRoot 게임오브젝트에 붙이는 스크립트
// TAB 키 닫기 / 패널 관리 / 슬롯 이벤트 추가 / 버리기 버튼 처리
// IsInBase 가 true 일 경우에만 WarehousePanel 이 표시됨

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUIController : MonoBehaviour
{
    // 싱글톤 인스턴스 (ContextMenuUI 등에서 접근)
    public static InventoryUIController Instance { get; private set; }

    [Header("루트 게임오브젝트")]
    [SerializeField] private GameObject inventoryRoot;

    [Header("패널")]
    [SerializeField] private GameObject warehousePanel;
    [SerializeField] private GameObject bagPanel;
    [SerializeField] private GameObject chestPanel;       // 상자 파밍 패널

    [Header("그리드 UI")]
    [SerializeField] private InventoryGridUI bagGridUI;
    [SerializeField] private InventoryGridUI warehouseGridUI;
    [SerializeField] private InventoryGridUI chestGridUI; // 상자 파밍 그리드

    [Header("카테고리 필터")]
    [SerializeField] private CategoryFilterUI bagFilterUI;
    [SerializeField] private CategoryFilterUI warehouseFilterUI;

    [Header("가방 패널 UI 버튼")]
    [SerializeField] private TextMeshProUGUI capacityText;
    [SerializeField] private Button moveAllBtn;
    [SerializeField] private Button bagTrashBtn;
    [SerializeField] private Button bagCloseBtn;

    [Header("창고 패널 UI 버튼")]
    [SerializeField] private Button takeAllBtn;

    [Header("상자 패널 UI 버튼")]
    [SerializeField] private Button takeAllFromChestBtn;  // 상자 아이템 전부 가방으로

    [Header("팝업")]
    [SerializeField] private ContextMenuUI contextMenu;
    [SerializeField] private SplitStackPopupUI splitPopup;
    [SerializeField] private ConfirmPopupUI confirmPopup;

    [Header("창고 정렬 바")]
    [SerializeField] private SortBarUI sortBarUI;

    [Header("드랍 아이템 프리팹")]
    [SerializeField] private GameObject lootBoxPrefab;

    [Header("툴팁")]
    [SerializeField] private ItemTooltipUI tooltip;

    // 현재 선택된 슬롯
    private InventorySlotUI _selectedSlot;

    // 인벤토리 UI 오픈 상태
    private bool _isOpen = false;
    public bool IsOpen => _isOpen;

    // 기지 내부 여부 (WarehouseInteractable 등에서 설정, 닫을 때 자동 초기화)
    public static bool IsInBase { get; set; } = false;

    // 상자 열기 여부 (ChestInteractable에서 설정, 닫을 때 자동 초기화)
    public static bool IsChestOpen { get; set; } = false;

    private void Awake()
    {
        Instance = this;
        if (inventoryRoot != null)
            inventoryRoot.SetActive(false);
    }

    private void Start()
    {
        // 인벤토리 매니저 바인딩
        if (bagGridUI != null && InventoryManager.Instance != null)
            bagGridUI.Bind(InventoryManager.Instance);

        if (warehouseGridUI != null && InventoryManager.StorageInstance != null)
            warehouseGridUI.Bind(InventoryManager.StorageInstance);

        if (chestGridUI != null && InventoryManager.ChestInstance != null)
            chestGridUI.Bind(InventoryManager.ChestInstance);

        // 카테고리 필터 이벤트 연결
        if (bagFilterUI != null)
            bagFilterUI.OnFilterChanged += bagGridUI.SetFilter;

        if (warehouseFilterUI != null)
            warehouseFilterUI.OnFilterChanged += warehouseGridUI.SetFilter;

        // 버튼 이벤트 등록
        if (moveAllBtn != null) moveAllBtn.onClick.AddListener(OnClickMoveAll);
        if (takeAllBtn != null) takeAllBtn.onClick.AddListener(OnClickTakeAll);
        if (bagTrashBtn != null) bagTrashBtn.onClick.AddListener(OnClickBagTrash);
        if (bagCloseBtn != null) bagCloseBtn.onClick.AddListener(Close);
        if (takeAllFromChestBtn != null) takeAllFromChestBtn.onClick.AddListener(OnClickTakeAllFromChest);

        // 창고 정렬 바 바인딩
        if (sortBarUI != null && InventoryManager.StorageInstance != null)
            sortBarUI.Bind(InventoryManager.StorageInstance, warehouseFilterUI);

        // 슬롯 클릭 이벤트 연결
        InventorySlotUI.OnAnySlotClicked += OnSlotClicked;
        InventorySlotUI.OnAnySlotDoubleClicked += OnSlotDoubleClicked;
        InventorySlotUI.OnAnySlotRightClicked += OnSlotRightClicked;
        InventorySlotUI.OnAnySlotHoverEnter += OnSlotHoverEnter;
        InventorySlotUI.OnAnySlotHoverExit += OnSlotHoverExit;
    }

    private void OnDestroy()
    {
        InventorySlotUI.OnAnySlotClicked -= OnSlotClicked;
        InventorySlotUI.OnAnySlotDoubleClicked -= OnSlotDoubleClicked;
        InventorySlotUI.OnAnySlotRightClicked -= OnSlotRightClicked;
        InventorySlotUI.OnAnySlotHoverEnter -= OnSlotHoverEnter;
        InventorySlotUI.OnAnySlotHoverExit -= OnSlotHoverExit;
    }

    private void Update()
    {
        // 튜토리얼 코치마크(오버레이) 중에는 키보드 차단 — TAB 인벤 토글 무시.
        if (GameUIController.Instance != null && GameUIController.Instance.IsTutorialCoachActive)
            return;

        // 사망 오버레이 중에는 인벤 토글 차단 (부활 버튼 외 입력 금지)
        if (DeathOverlayUI.IsOpen)
            return;

        // TAB 키로 인벤토리 토글 (DataBoot 완료 여부와 무관하게 항상 열 수 있음)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Toggle();
            return;
        }
        // ESC는 GameUIController.HandleEscape()가 TryCloseTopPopup() → Close() 순으로 처리

        // ContextMenu 외부 클릭 감지
        if (contextMenu != null)
            contextMenu.TryCloseOnOutsideClick();
    }

    // 인벤토리 토글
    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    // 인벤토리 열기
    public void Open()
    {
        if (inventoryRoot == null)
        {
            Debug.LogError("[InventoryUI] inventoryRoot가 인스펙터에 연결되지 않았습니다!");
            return;
        }

        // 다른 UI가 이미 열려있으면 차단 (설비·설정·퀘스트·건설 모드 등)
        var gui = GameUIController.Instance;
        if (gui != null && gui.IsUIBlocking())
        {
            Debug.Log($"[InventoryUI] Open 차단 — 현재 UI 상태: {gui.GetCurrentState()}");
            return;
        }

        // 커서·입력 차단은 GameUIController에 위임
        gui?.SetState(GameUIController.UIState.Inventory);

        _isOpen = true;
        inventoryRoot.SetActive(true);

        // 창고 안에서만 창고 패널 표시
        if (warehousePanel != null)
            warehousePanel.SetActive(IsInBase && !IsChestOpen);

        // 상자 열었을 때 상자 패널 표시
        if (chestPanel != null)
            chestPanel.SetActive(IsChestOpen);

        // 상자 패널 열릴 때 ChestInstance에 그리드 바인딩 (타이밍 이슈 방지)
        if (IsChestOpen && chestGridUI != null && InventoryManager.ChestInstance != null)
            chestGridUI.Bind(InventoryManager.ChestInstance);

        if (bagPanel != null)
            bagPanel.SetActive(true);

        RefreshCapacityText();
        Debug.Log("[InventoryUI] 인벤토리 열림");

        // ── 진단: 부모 캔버스 / CanvasGroup 상태 확인 ─────────────────
        var parentCanvas = inventoryRoot.GetComponentInParent<Canvas>();
        if (parentCanvas == null)
            Debug.LogError("[InventoryUI] InventoryRoot 위에 Canvas가 없습니다!");
        else
            Debug.Log($"[InventoryUI] 부모 Canvas='{parentCanvas.name}'  activeInHierarchy={parentCanvas.gameObject.activeInHierarchy}  sortOrder={parentCanvas.sortingOrder}  renderMode={parentCanvas.renderMode}");

        var cg = inventoryRoot.GetComponent<CanvasGroup>();
        if (cg != null)
            Debug.Log($"[InventoryUI] InventoryRoot CanvasGroup  alpha={cg.alpha}  interactable={cg.interactable}  blocksRaycasts={cg.blocksRaycasts}");

        if (bagPanel != null)
            Debug.Log($"[InventoryUI] bagPanel active={bagPanel.activeSelf}  activeInHierarchy={bagPanel.activeInHierarchy}");
        else
            Debug.LogWarning("[InventoryUI] bagPanel이 인스펙터에 연결되지 않았습니다!");
        // ─────────────────────────────────────────────────────────────
    }

    // 인벤토리 닫기
    public void Close()
    {
        if (inventoryRoot == null) return;

        // 드래그 중이었으면 강제 종료 (ESC로 닫을 때 Ghost 화면 잔재 방지)
        InventoryDragHandler.Instance?.EndDrag();

        // 창고 재진입 플래그 초기화 (다음 TAB 시 창고가 열리지 않도록)
        IsInBase = false;

        // 상자 닫기 — 남은 아이템 비우기
        if (IsChestOpen)
        {
            IsChestOpen = false;
            InventoryManager.ChestInstance?.ClearAllItems();
        }

        _isOpen = false;

        // 필터 초기화
        bagFilterUI?.ResetToAll();
        warehouseFilterUI?.ResetToAll();

        // 팝업 닫기
        contextMenu?.Close();
        splitPopup?.Close();
        confirmPopup?.Close();
        tooltip?.Hide();

        ClearSelection();

        // UIUnfoldEffect가 있으면 닫기 애니메이션 후 SetActive(false), 없으면 즉시 비활성화
        var unfold = inventoryRoot.GetComponent<UIUnfoldEffect>();
        if (unfold != null && inventoryRoot.activeInHierarchy)
            unfold.Close();
        else
            inventoryRoot.SetActive(false);

        // 커서·입력 복구는 GameUIController에 위임
        GameUIController.Instance?.CloseAll();
    }

    /// <summary>
    /// 열려있는 팝업 중 최상위 팝업 하나를 닫습니다.
    /// GameUIController.HandleEscape()가 ESC 우선순위 처리에 사용합니다.
    /// </summary>
    public bool TryCloseTopPopup()
    {
        if (splitPopup != null && splitPopup.IsOpen) { splitPopup.Close(); return true; }
        if (confirmPopup != null && confirmPopup.IsOpen) { confirmPopup.Close(); return true; }
        return false;
    }

    // 용량 텍스트 갱신
    public void RefreshCapacityText()
    {
        if (capacityText == null || InventoryManager.Instance == null) return;
        int used = InventoryManager.Instance.GetUsedSlotCount();
        int max = InventoryManager.Instance.GetMaxSlots();
        capacityText.text = used + "/" + max;
    }

    // 단일 슬롯 클릭 핸들러
    private void OnSlotClicked(InventorySlotUI slot)
    {
        if (_selectedSlot != null && _selectedSlot != slot)
            _selectedSlot.SetSelected(false);

        _selectedSlot = slot;
        slot.SetSelected(true);
        contextMenu?.Close();
        RefreshCapacityText();
    }

    // 슬롯 더블클릭 핸들러 (반대 인벤토리로 이동)
    private void OnSlotDoubleClicked(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;

        var owner = slot.Owner;
        var player = InventoryManager.Instance;
        var storage = InventoryManager.StorageInstance;

        var chest = InventoryManager.ChestInstance;

        if (owner == player)
        {
            // 가방 → 상자가 열려있으면 상자로, 아니면 창고로
            if (IsChestOpen && chest != null)
                owner.MoveSlot(slot.SlotData.slotIndex, chest);
            else
            {
                bool warehouseOpen = warehousePanel != null && warehousePanel.activeSelf;
                if (IsInBase && warehouseOpen && storage != null)
                    owner.MoveSlot(slot.SlotData.slotIndex, storage);
            }
        }
        else if (owner == storage)
        {
            // 창고 → 가방
            if (player != null)
                owner.MoveSlot(slot.SlotData.slotIndex, player);
        }
        else if (owner == chest)
        {
            // 상자 → 가방
            if (player != null)
                owner.MoveSlot(slot.SlotData.slotIndex, player);
        }

        ClearSelection();
        RefreshCapacityText();
    }

    // 슬롯 우클릭 핸들러
    private void OnSlotRightClicked(InventorySlotUI slot)
    {
        OnSlotClicked(slot);
        contextMenu?.Open(slot, Input.mousePosition);
    }

    // 툴팁 표시
    private void OnSlotHoverEnter(InventorySlotUI slot)
    {
        tooltip?.Show(slot);
    }

    // 툴팁 숨김
    private void OnSlotHoverExit(InventorySlotUI slot)
    {
        tooltip?.Hide();
    }

    // 전체 이동 (가방 필터 아이템 -> 창고)
    private void OnClickMoveAll()
    {
        if (!IsInBase) return;  // BaseZone 밖에서는 창고 이동 불가

        var player = InventoryManager.Instance;
        var storage = InventoryManager.StorageInstance;
        if (player == null || storage == null) return;

        var filter = bagFilterUI != null ? bagFilterUI.CurrentFilter : null;
        player.MoveFilteredTo(storage, filter);
        RefreshCapacityText();
    }

    // 전체 가져오기 (창고 필터 아이템 -> 가방)
    private void OnClickTakeAll()
    {
        if (!IsInBase) return;  // BaseZone 밖에서는 창고 접근 불가

        var player = InventoryManager.Instance;
        var storage = InventoryManager.StorageInstance;
        if (player == null || storage == null) return;

        var filter = warehouseFilterUI != null ? warehouseFilterUI.CurrentFilter : null;
        storage.MoveFilteredTo(player, filter);
        RefreshCapacityText();
    }

    // 상자 아이템 전부 가방으로
    private void OnClickTakeAllFromChest()
    {
        var player = InventoryManager.Instance;
        var chest  = InventoryManager.ChestInstance;
        if (player == null || chest == null) return;
        chest.MoveFilteredTo(player, null);
        RefreshCapacityText();
    }

    // 가방 하단 휴지통 버튼
    private void OnClickBagTrash()
    {
        if (_selectedSlot == null || _selectedSlot.IsEmpty) return;
        OpenTrashConfirm(_selectedSlot);
    }

    // 분할 팝업 열기
    public void OpenSplitPopup(InventorySlotUI slot)
    {
        if (splitPopup == null) return;
        confirmPopup?.Close();
        splitPopup.Open(slot);
    }

    // 버리기 확인 팝업 열기
    public void OpenTrashConfirm(InventorySlotUI slot)
    {
        if (confirmPopup == null || slot == null || slot.IsEmpty) return;

        splitPopup?.Close();

        var data = ItemDatabase.GetItem(slot.SlotData.itemId);
        string name = data != null ? data.itemName : "아이템";
        int amount = slot.SlotData.amount;
        int itemId = slot.SlotData.itemId;
        int slotIdx = slot.SlotData.slotIndex;
        var owner = slot.Owner;

        string message = name + " x" + amount + "개를 버리시겠습니까?";

        confirmPopup.Open(message, () =>
        {
            owner?.RemoveFromSlot(slotIdx, amount);
            SpawnDroppedItem(itemId, amount);
            ClearSelection();
            RefreshCapacityText();
        });
    }

    // 플레이어 앞에 LootBox 소환 (아이템 드랍)
    private void SpawnDroppedItem(int itemId, int amount)
    {
        if (lootBoxPrefab == null)
        {
            Debug.LogWarning("[InventoryUIController] LootBox 프리팹이 설정되지 않았습니다.");
            return;
        }

        var player = FindAnyObjectByType<Player>();
        Vector3 spawnPos = player != null
            ? player.transform.position + player.transform.forward * 1.2f
            : Vector3.zero;

        var go = Instantiate(lootBoxPrefab, spawnPos, Quaternion.identity);
        var lootBox = go.GetComponentInChildren<LootBox>(true);
        if (lootBox != null)
            lootBox.Initialize(
                new System.Collections.Generic.List<(int, int)> { (itemId, amount) });
        else
            Debug.LogWarning("[InventoryUIController] 생성된 프리팹에 LootBox 컴포넌트가 없습니다.");
    }

    // 선택 해제
    private void ClearSelection()
    {
        if (_selectedSlot != null)
        {
            _selectedSlot.SetSelected(false);
            _selectedSlot = null;
        }
    }
}