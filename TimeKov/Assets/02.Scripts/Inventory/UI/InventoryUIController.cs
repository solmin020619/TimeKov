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
    [SerializeField] private GameObject inventoryRoot;      // InventoryRoot

    [Header("패널")]
    [SerializeField] private GameObject warehousePanel;     // 창고 패널
    [SerializeField] private GameObject bagPanel;           // 가방 패널

    [Header("그리드 UI")]
    [SerializeField] private InventoryGridUI bagGridUI;           // 가방 SlotGrid
    [SerializeField] private InventoryGridUI warehouseGridUI;     // 창고 SlotGrid

    [Header("카테고리 필터")]
    [SerializeField] private CategoryFilterUI bagFilterUI;         // 가방 FilterBar
    [SerializeField] private CategoryFilterUI warehouseFilterUI;   // 창고 FilterBar

    [Header("가방 패널 UI 요소")]
    [SerializeField] private TextMeshProUGUI capacityText;        // 용량 텍스트 (0/35)
    [SerializeField] private Button moveAllBtn;          // 전부 보관 버튼
    [SerializeField] private Button bagTrashBtn;         // 버리기 버튼
    [SerializeField] private Button bagCloseBtn;         // X 닫기 버튼

    [Header("창고 패널 UI 요소")]
    [SerializeField] private Button takeAllBtn;          // 전부 꺼내기 버튼

    [Header("팝업")]
    [SerializeField] private ContextMenuUI contextMenu;         // 우클릭 메뉴
    [SerializeField] private SplitStackPopupUI splitPopup;          // 분할 팝업
    [SerializeField] private ConfirmPopupUI confirmPopup;        // 확인 팝업

    [Header("창고 정렬 바")]
    [SerializeField] private SortBarUI sortBarUI;            // WarehousePanel/SortBar

    [Header("툴팁")]
    [SerializeField] private ItemTooltipUI tooltip;             // 아이템 툴팁

    // 현재 선택된 슬롯 (좌클릭으로 선택)
    private InventorySlotUI _selectedSlot;

    // 인벤토리 UI 가 열려있는지 여부
    private bool _isOpen = false;

    // 현재 기지 안에 있는지 여부 (외부에서 설정)
    // BaseManager 나 TriggerZone 에서 IsInBase = true/false 로 설정
    public static bool IsInBase { get; set; } = false;

    private void Awake()
    {
        Instance = this;

        // 시작 시 루트 비활성화
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

        // 카테고리 필터 변경 이벤트 연결
        if (bagFilterUI != null)
            bagFilterUI.OnFilterChanged += bagGridUI.SetFilter;

        if (warehouseFilterUI != null)
            warehouseFilterUI.OnFilterChanged += warehouseGridUI.SetFilter;

        // 전부 보관 버튼 이벤트
        if (moveAllBtn != null)
            moveAllBtn.onClick.AddListener(OnClickMoveAll);

        // 전부 꺼내기 버튼 이벤트
        if (takeAllBtn != null)
            takeAllBtn.onClick.AddListener(OnClickTakeAll);

        // 가방 버리기 버튼 이벤트 (선택된 슬롯 버리기)
        if (bagTrashBtn != null)
            bagTrashBtn.onClick.AddListener(OnClickBagTrash);

        // 닫기 버튼
        if (bagCloseBtn != null)
            bagCloseBtn.onClick.AddListener(Close);

        // 창고 정렬 바 바인딩
        if (sortBarUI != null && InventoryManager.StorageInstance != null)
            sortBarUI.Bind(InventoryManager.StorageInstance);

        // 슬롯 전역 이벤트 구독
        InventorySlotUI.OnAnySlotClicked += OnSlotClicked;
        InventorySlotUI.OnAnySlotDoubleClicked += OnSlotDoubleClicked;
        InventorySlotUI.OnAnySlotRightClicked += OnSlotRightClicked;
        InventorySlotUI.OnAnySlotHoverEnter += OnSlotHoverEnter;
        InventorySlotUI.OnAnySlotHoverExit += OnSlotHoverExit;
    }

    private void OnDestroy()
    {
        // 전역 이벤트 구독 해제 (메모리 누수 방지)
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

        // 플레이어 게임플레이 입력 차단
        PlayerInputComponent.IsBlocked = true;

        // 커서 표시 및 잠금 해제
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        _isOpen = true;
        inventoryRoot.SetActive(true);

        // 기지 안에서만 창고 패널 표시
        if (warehousePanel != null)
            warehousePanel.SetActive(IsInBase);

        if (bagPanel != null)
            bagPanel.SetActive(true);

        // 용량 텍스트 갱신
        RefreshCapacityText();
    }

    // 인벤토리 닫기
    public void Close()
    {
        if (inventoryRoot == null) return;

        // 플레이어 게임플레이 입력 복구
        PlayerInputComponent.IsBlocked = false;

        // 커서 숨김 및 잠금 복구
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        _isOpen = false;

        // 열려있는 팝업 모두 닫기
        contextMenu?.Close();
        splitPopup?.Close();
        confirmPopup?.Close();
        tooltip?.Hide();

        // 선택 해제
        ClearSelection();

        inventoryRoot.SetActive(false);
    }

    // 용량 텍스트 갱신 (가방 슬롯 수 업데이트)
    public void RefreshCapacityText()
    {
        if (capacityText == null || InventoryManager.Instance == null) return;

        int used = InventoryManager.Instance.GetUsedSlotCount();
        int max = InventoryManager.Instance.GetMaxSlots();
        capacityText.text = used + "/" + max;
    }

    // 슬롯 더블클릭 핸들러 (다른 인벤토리로 빠른 이동)
    private void OnSlotDoubleClicked(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;

        var owner = slot.Owner;
        var player = InventoryManager.Instance;
        var storage = InventoryManager.StorageInstance;

        if (owner == player)
        {
            // 가방 슬롯 더블클릭 -> 창고가 열려있을 때만 창고로 이동
            bool warehouseOpen = warehousePanel != null && warehousePanel.activeSelf;
            if (IsInBase && warehouseOpen && storage != null)
                owner.MoveSlot(slot.SlotData.slotIndex, storage);
        }
        else if (owner == storage)
        {
            // 창고 슬롯 더블클릭 -> 가방으로 이동
            if (player != null)
                owner.MoveSlot(slot.SlotData.slotIndex, player);
        }

        // 이동 후 선택 상태 해제
        ClearSelection();
        RefreshCapacityText();
    }

    // 슬롯 좌클릭 핸들러
    private void OnSlotClicked(InventorySlotUI slot)
    {
        // 기존 선택 해제
        if (_selectedSlot != null && _selectedSlot != slot)
            _selectedSlot.SetSelected(false);

        _selectedSlot = slot;
        slot.SetSelected(true);

        // 열려있는 ContextMenu 닫기
        contextMenu?.Close();

        // 용량 텍스트 갱신
        RefreshCapacityText();
    }

    // 슬롯 우클릭 핸들러 (ContextMenu 열기)
    private void OnSlotRightClicked(InventorySlotUI slot)
    {
        // 좌클릭 선택도 같이 처리
        OnSlotClicked(slot);

        // ContextMenu 열기
        contextMenu?.Open(slot, Input.mousePosition);
    }

    // 슬롯 호버 진입 (툴팁 표시)
    private void OnSlotHoverEnter(InventorySlotUI slot)
    {
        tooltip?.Show(slot);
    }

    // 슬롯 호버 이탈 (툴팁 숨기기)
    private void OnSlotHoverExit(InventorySlotUI slot)
    {
        tooltip?.Hide();
    }

    // 전부 보관 버튼 핸들러 (가방 -> 창고)
    private void OnClickMoveAll()
    {
        var player = InventoryManager.Instance;
        var storage = InventoryManager.StorageInstance;

        if (player == null || storage == null)
        {
            Debug.LogWarning("[InventoryUIController] 이동 실패: 인벤토리 매니저 미설정");
            return;
        }

        player.MoveAllTo(storage);
        RefreshCapacityText();
    }

    // 전부 꺼내기 버튼 핸들러 (창고 -> 가방)
    private void OnClickTakeAll()
    {
        var player = InventoryManager.Instance;
        var storage = InventoryManager.StorageInstance;

        if (player == null || storage == null)
        {
            Debug.LogWarning("[InventoryUIController] 이동 실패: 인벤토리 매니저 미설정");
            return;
        }

        storage.MoveAllTo(player);
        RefreshCapacityText();
    }

    // 가방 하단 버리기 버튼 핸들러 (선택된 슬롯 버리기)
    private void OnClickBagTrash()
    {
        if (_selectedSlot == null || _selectedSlot.IsEmpty)
        {
            Debug.Log("[InventoryUIController] 버릴 아이템이 선택되지 않았습니다.");
            return;
        }

        OpenTrashConfirm(_selectedSlot);
    }

    // 분할 팝업 열기 (ContextMenuUI 에서 호출)
    public void OpenSplitPopup(InventorySlotUI slot)
    {
        if (splitPopup == null) return;

        // 다른 팝업 닫기
        confirmPopup?.Close();

        splitPopup.Open(slot);
    }

    // 버리기 확인 팝업 열기 (ContextMenuUI 또는 TrashBtn 에서 호출)
    public void OpenTrashConfirm(InventorySlotUI slot)
    {
        if (confirmPopup == null || slot == null || slot.IsEmpty) return;

        // 다른 팝업 닫기
        splitPopup?.Close();

        var data = ItemDatabase.GetItem(slot.SlotData.itemId);
        string name = data != null ? data.itemName : "아이템";
        int amount = slot.SlotData.amount;

        string message = name + " x" + amount + "개를 버리시겠습니까?";

        confirmPopup.Open(message, () =>
        {
            // 확인 버튼 눌렀을 때 삭제 실행
            slot.Owner?.RemoveFromSlot(slot.SlotData.slotIndex, slot.SlotData.amount);
            ClearSelection();
            RefreshCapacityText();
        });
    }

    // 선택 상태 초기화
    private void ClearSelection()
    {
        if (_selectedSlot != null)
        {
            _selectedSlot.SetSelected(false);
            _selectedSlot = null;
        }
    }
}