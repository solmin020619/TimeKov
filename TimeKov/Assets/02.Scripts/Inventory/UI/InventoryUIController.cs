// InventoryUIController.cs
// InventoryRoot 오브젝트에 붙이는 스크립트
// TAB 열닫기 / 패널 관리 / 슬롯 이벤트 중계 / 전부 보관 버튼 처리
// IsInBase 가 true 일 때만 WarehousePanel 이 표시됨

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUIController : MonoBehaviour
{
    // 싱글톤 인스턴스 (ContextMenuUI 에서 접근)
    public static InventoryUIController Instance { get; private set; }

    [Header("루트 오브젝트")]
    [SerializeField] private GameObject inventoryRoot;

    [Header("패널")]
    [SerializeField] private GameObject warehousePanel;
    [SerializeField] private GameObject bagPanel;

    [Header("그리드 UI")]
    [SerializeField] private InventoryGridUI bagGridUI;
    [SerializeField] private InventoryGridUI warehouseGridUI;

    [Header("카테고리 필터")]
    [SerializeField] private CategoryFilterUI bagFilterUI;
    [SerializeField] private CategoryFilterUI warehouseFilterUI;

    [Header("가방 패널 UI 요소")]
    [SerializeField] private TextMeshProUGUI capacityText;
    [SerializeField] private Button moveAllBtn;
    [SerializeField] private Button bagTrashBtn;
    [SerializeField] private Button bagCloseBtn;

    [Header("창고 패널 UI 요소")]
    [SerializeField] private Button takeAllBtn;

    [Header("팝업")]
    [SerializeField] private ContextMenuUI contextMenu;
    [SerializeField] private SplitStackPopupUI splitPopup;
    [SerializeField] private ConfirmPopupUI confirmPopup;

    [Header("창고 정렬 바")]
    [SerializeField] private SortBarUI sortBarUI;

    [Header("버리기 드롭")]
    [SerializeField] private GameObject lootBoxPrefab;

    [Header("툴팁")]
    [SerializeField] private ItemTooltipUI tooltip;

    // 현재 선택된 슬롯
    private InventorySlotUI _selectedSlot;

    // 인벤토리 UI 열림 여부
    private bool _isOpen = false;

    // 기지 내부 여부 (WarehouseInteractable 에서 설정, 닫을 때 자동 초기화)
    public static bool IsInBase { get; set; } = false;

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

        // 창고 정렬 바 바인딩
        if (sortBarUI != null && InventoryManager.StorageInstance != null)
            sortBarUI.Bind(InventoryManager.StorageInstance, warehouseFilterUI);

        // 슬롯 전역 이벤트 구독
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
        // TAB 키로 인벤토리 토글
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (DataBoot.IsLoaded)
                Toggle();
            return;
        }

        // ESC 키로 팝업 우선 닫기
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (splitPopup != null && splitPopup.IsOpen) { splitPopup.Close(); return; }
            if (confirmPopup != null && confirmPopup.IsOpen) { confirmPopup.Close(); return; }
            if (_isOpen) { Close(); return; }
        }

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
        if (inventoryRoot == null) return;

        // 플레이어 입력 차단 및 커서 표시
        PlayerInputComponent.IsBlocked = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        _isOpen = true;
        inventoryRoot.SetActive(true);

        // 기지 안에서만 창고 패널 표시
        if (warehousePanel != null)
            warehousePanel.SetActive(IsInBase);

        if (bagPanel != null)
            bagPanel.SetActive(true);

        RefreshCapacityText();
    }

    // 인벤토리 닫기
    public void Close()
    {
        if (inventoryRoot == null) return;

        // 플레이어 입력 복구 및 커서 숨김
        PlayerInputComponent.IsBlocked = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // 창고 열기 상태 초기화 (다음 TAB 시 창고가 뜨지 않도록)
        IsInBase = false;

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
        inventoryRoot.SetActive(false);
    }

    // 용량 텍스트 갱신
    public void RefreshCapacityText()
    {
        if (capacityText == null || InventoryManager.Instance == null) return;
        int used = InventoryManager.Instance.GetUsedSlotCount();
        int max = InventoryManager.Instance.GetMaxSlots();
        capacityText.text = used + "/" + max;
    }

    // 슬롯 단일 클릭 핸들러
    private void OnSlotClicked(InventorySlotUI slot)
    {
        if (_selectedSlot != null && _selectedSlot != slot)
            _selectedSlot.SetSelected(false);

        _selectedSlot = slot;
        slot.SetSelected(true);
        contextMenu?.Close();
        RefreshCapacityText();
    }

    // 슬롯 더블클릭 핸들러 (반대 컨테이너로 이동)
    private void OnSlotDoubleClicked(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;

        var owner = slot.Owner;
        var player = InventoryManager.Instance;
        var storage = InventoryManager.StorageInstance;

        if (owner == player)
        {
            // 가방 -> 창고 (창고 패널이 열려있을 때만)
            bool warehouseOpen = warehousePanel != null && warehousePanel.activeSelf;
            if (IsInBase && warehouseOpen && storage != null)
                owner.MoveSlot(slot.SlotData.slotIndex, storage);
        }
        else if (owner == storage)
        {
            // 창고 -> 가방
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

    // 전부 보관 (가방 필터 기준 -> 창고)
    private void OnClickMoveAll()
    {
        var player = InventoryManager.Instance;
        var storage = InventoryManager.StorageInstance;
        if (player == null || storage == null) return;

        var filter = bagFilterUI != null ? bagFilterUI.CurrentFilter : null;
        player.MoveFilteredTo(storage, filter);
        RefreshCapacityText();
    }

    // 전부 꺼내기 (창고 필터 기준 -> 가방)
    private void OnClickTakeAll()
    {
        var player = InventoryManager.Instance;
        var storage = InventoryManager.StorageInstance;
        if (player == null || storage == null) return;

        var filter = warehouseFilterUI != null ? warehouseFilterUI.CurrentFilter : null;
        storage.MoveFilteredTo(player, filter);
        RefreshCapacityText();
    }

    // 가방 하단 버리기 버튼
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

    // 플레이어 앞에 LootBox 스폰 (버리기)
    private void SpawnDroppedItem(int itemId, int amount)
    {
        if (lootBoxPrefab == null)
        {
            Debug.LogWarning("[InventoryUIController] LootBox 프리팹이 연결되지 않았습니다.");
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
            Debug.LogWarning("[InventoryUIController] 스폰된 프리팹에 LootBox 컴포넌트가 없습니다.");
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