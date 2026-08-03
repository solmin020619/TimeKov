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
    [SerializeField] private GameObject bagBlurCanvas;    // 가방 뒤 블러 캔버스 (Screen Space-Camera, 열릴 때만 활성)
    [SerializeField] private GameObject chestPanel;       // 상자 파밍 패널

    [Header("그리드 UI")]
    [SerializeField] private InventoryGridUI bagGridUI;
    [SerializeField] private InventoryGridUI warehouseGridUI;
    [SerializeField] private InventoryGridUI chestGridUI; // 상자 파밍 그리드
    [SerializeField] private InventoryGridUI warehouseBagGridUI;   // 통합패널 안의 가방 구역 그리드(결계 안에서 단독 가방 대신 표시)

    [Header("통합 패널(결계 안) 전용")]
    [SerializeField] private TextMeshProUGUI warehouseBagCapacityText;   // 통합패널 가방 구역 용량
    [SerializeField] private UnityEngine.UI.Button warehouseDualCloseBtn; // 통합패널 닫기

    [Header("카테고리 필터")]
    [SerializeField] private CategoryFilterUI bagFilterUI;
    [SerializeField] private CategoryFilterUI warehouseFilterUI;

    [Header("가방 패널 UI 버튼")]
    [SerializeField] private TextMeshProUGUI capacityText;
    [SerializeField] private Image bagCapacityGaugeFill;   // 용량 게이지 fill (비율 + 상태색 갱신)
    [SerializeField] private Button moveAllBtn;
    [SerializeField] private Button bagTrashBtn;
    [SerializeField] private Button bagCloseBtn;

    [Header("창고 패널 UI 버튼")]
    [SerializeField] private Button takeAllBtn;

    [Header("상자 패널 UI 버튼")]
    [SerializeField] private Button takeAllFromChestBtn;  // 상자 아이템 전부 가방으로
    [SerializeField] private Button chestCloseBtn;        // 상자 패널 닫기 X
    [SerializeField] private TextMeshProUGUI chestCapacityText;  // 상자 카운터 "아이템 N/M"

    [Header("팝업")]
    [SerializeField] private ContextMenuUI contextMenu;
    [SerializeField] private SplitStackPopupUI splitPopup;
    [SerializeField] private ConfirmPopupUI confirmPopup;

    [Header("창고 정렬 바")]
    [SerializeField] private SortBarUI sortBarUI;

    [Header("가방 정렬 바")]
    [SerializeField] private SortBarUI bagSortBarUI;
    [SerializeField] private SortBarUI warehouseBagSortBarUI;   // 통합패널 가방 구역 정렬

    [Header("드랍 아이템 프리팹")]
    [SerializeField] private GameObject lootBoxPrefab;

    [Header("툴팁")]
    [SerializeField] private ItemTooltipUI tooltip;

    // 현재 선택된 슬롯
    private InventorySlotUI _selectedSlot;

    // 인벤 열기/닫기 가로 슬라이드 효과 (inventoryRoot에 자동 부착)
    private UISlideEffect _slide;

    // 인벤토리 UI 오픈 상태
    private bool _isOpen = false;
    public bool IsOpen => _isOpen;

    // 기지(결계존) 내부 여부. Open()에서 플레이어 결계상태(IsPlayerInBase)로 매번 세팅(상자 파밍 중엔 false). 닫을 때 false.
    public static bool IsInBase { get; set; } = false;

    // 상자 열기 여부 (ChestInteractable에서 설정, 닫을 때 자동 초기화)
    public static bool IsChestOpen { get; set; } = false;

    // 상자 패널이 열린 프레임 — 같은 프레임에 F(여는 입력)로 바로 닫히는 것 방지용.
    private int _chestOpenFrame = -1;

    // 플레이어가 결계존(BaseZone) 안인지 조회 — TAB로 인벤 열 때 창고 자동 연동 판정용
    private static Player _player;
    private static bool IsPlayerInBase()
    {
        if (_player == null) _player = FindAnyObjectByType<Player>();
        return _player != null && _player.Stat != null && _player.Stat.IsInBase;
    }

    private void Awake()
    {
        Instance = this;
        if (inventoryRoot != null)
        {
            inventoryRoot.SetActive(false);

            // 가방을 화면 오른쪽으로 (빌더 재실행 안 해도 먹게 런타임 강제).
            // ★이 값이 실제 화면 위치를 결정한다(런타임이 빌더 BagRightX를 덮어씀). 위치 바꾸려면 여기. 빌더 BagRightX와 같게 유지.
            const float bagRightX = 500f;
            if (bagPanel != null)
            {
                var prt0 = bagPanel.GetComponent<RectTransform>();
                if (prt0 != null) prt0.anchoredPosition = new Vector2(bagRightX, prt0.anchoredPosition.y);
            }

            // 파밍 상자 = 화면 왼쪽(가방 거울). 이 값이 실제 위치 결정(빌더값 덮음). 위치 바꾸려면 여기.
            const float chestLeftX = -480f;
            if (chestPanel != null)
            {
                var cprt = chestPanel.GetComponent<RectTransform>();
                if (cprt != null) cprt.anchoredPosition = new Vector2(chestLeftX, cprt.anchoredPosition.y);
            }
            // 블러 영역 위치/크기는 LateUpdate의 SyncBlurToPanel이 매 프레임 패널 스크린 사각형에 맞춘다
            // (Camera 블러캔버스 vs Overlay 패널 좌표 어긋남 -> 블러가 일부만 덮이던 문제 해결).

            // 인벤 열기/닫기 = 가로 슬라이드. 공용 UIUnfoldEffect(Y스케일)는 꺼서 충돌 방지.
            var fold = inventoryRoot.GetComponent<UIUnfoldEffect>();
            if (fold != null) fold.enabled = false;
            _slide = inventoryRoot.GetComponent<UISlideEffect>();
            if (_slide == null) _slide = inventoryRoot.AddComponent<UISlideEffect>();
            if (bagBlurCanvas != null)
            {
                var bcg = bagBlurCanvas.GetComponent<CanvasGroup>();
                if (bcg == null) bcg = bagBlurCanvas.AddComponent<CanvasGroup>();
                _slide.SetExtras(bcg, bagBlurCanvas, null);   // 블러는 알파 페이드/끄기만. 위치는 SyncBlurToPanel이.
            }
        }
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

        // 통합패널 가방 구역 = 단독 가방과 같은 데이터(Instance)
        if (warehouseBagGridUI != null && InventoryManager.Instance != null)
            warehouseBagGridUI.Bind(InventoryManager.Instance);

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
        if (warehouseDualCloseBtn != null) warehouseDualCloseBtn.onClick.AddListener(Close);
        if (takeAllFromChestBtn != null) takeAllFromChestBtn.onClick.AddListener(OnClickTakeAllFromChest);
        if (chestCloseBtn != null) chestCloseBtn.onClick.AddListener(Close);

        // 창고 정렬 바 바인딩
        if (sortBarUI != null && InventoryManager.StorageInstance != null)
            sortBarUI.Bind(InventoryManager.StorageInstance, warehouseFilterUI);

        // 가방 정렬 바 바인딩 (가방=Instance)
        if (bagSortBarUI != null && InventoryManager.Instance != null)
            bagSortBarUI.Bind(InventoryManager.Instance, bagFilterUI);

        // 통합패널 가방 정렬 (필터 없음)
        if (warehouseBagSortBarUI != null && InventoryManager.Instance != null)
            warehouseBagSortBarUI.Bind(InventoryManager.Instance, null);

        // 인벤(가방/상자) 데이터가 바뀌면 용량/카운터 즉시 갱신.
        // (드래그 입출고는 RefreshCapacityText를 직접 안 부르므로 이벤트로 실시간 반영 — 창 열린 채로 0/35 안 멈추게)
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= RefreshCapacityText;
            InventoryManager.Instance.OnInventoryChanged += RefreshCapacityText;
        }
        if (InventoryManager.ChestInstance != null)
        {
            InventoryManager.ChestInstance.OnInventoryChanged -= RefreshCapacityText;
            InventoryManager.ChestInstance.OnInventoryChanged += RefreshCapacityText;
        }

        // 슬롯 클릭 이벤트 연결
        InventorySlotUI.OnAnySlotClicked += OnSlotClicked;
        InventorySlotUI.OnAnySlotDoubleClicked += OnSlotDoubleClicked;
        InventorySlotUI.OnAnySlotRightClicked += OnSlotRightClicked;
        InventorySlotUI.OnAnySlotHoverEnter += OnSlotHoverEnter;
        InventorySlotUI.OnAnySlotHoverExit += OnSlotHoverExit;
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshCapacityText;
        if (InventoryManager.ChestInstance != null)
            InventoryManager.ChestInstance.OnInventoryChanged -= RefreshCapacityText;

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

        // 인벤토리 토글 (DataBoot 완료 여부와 무관하게 항상 열 수 있음)
        if (Input.GetKeyDown(KeyBindings.Inventory))
        {
            Toggle();
            return;
        }

        // 상자 파밍 패널은 F(인터랙트)로도 닫힌다 — 연 프레임엔 무시(여는 F 와 겹침 방지).
        if (_isOpen && IsChestOpen && Time.frameCount > _chestOpenFrame
            && Input.GetKeyDown(KeyBindings.Interact))
        {
            Close();
            return;
        }
        // ESC는 GameUIController.HandleEscape()가 TryCloseTopPopup() → Close() 순으로 처리

        // ContextMenu 외부 클릭 감지
        if (contextMenu != null)
            contextMenu.TryCloseOnOutsideClick();
    }

    private void LateUpdate()
    {
        // 열려있는 동안 + 닫기 슬라이드 중에도(블러캔버스 켜진 동안) 블러를 패널에 붙여둔다.
        // (_isOpen만 보면 닫기 시작 즉시 false라 블러가 멈춰서, 패널만 슬라이드돼 나가고 왼쪽에 블러 잔상이 남음)
        if (bagBlurCanvas != null && bagBlurCanvas.activeInHierarchy) SyncBlurToPanel();
    }

    // 블러 영역(별도 Screen Space-Camera 캔버스)을 가방 패널의 실제 화면 사각형에 맞춘다.
    // Camera 캔버스와 Overlay 패널은 같은 anchoredPosition이라도 화면 위치/스케일이 달라 어긋남 -> 화면좌표 변환으로 동기.
    private void SyncBlurToPanel()
    {
        if (bagBlurCanvas == null || bagPanel == null) return;
        if (bagBlurCanvas.transform.childCount == 0) return;

        var region       = bagBlurCanvas.transform.GetChild(0) as RectTransform;
        var blurCanvasRt = bagBlurCanvas.transform as RectTransform;
        var blurCanvas   = bagBlurCanvas.GetComponent<Canvas>();
        var panelRt      = bagPanel.transform as RectTransform;
        if (region == null || blurCanvasRt == null || blurCanvas == null || panelRt == null) return;

        Camera blurCam = blurCanvas.worldCamera != null ? blurCanvas.worldCamera : Camera.main;
        var invCanvas = bagPanel.GetComponentInParent<Canvas>();
        Camera invCam = (invCanvas != null && invCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? invCanvas.worldCamera : null;

        Vector3[] corners = new Vector3[4];
        panelRt.GetWorldCorners(corners);
        Vector2 sMin = RectTransformUtility.WorldToScreenPoint(invCam, corners[0]);   // 좌하
        Vector2 sMax = RectTransformUtility.WorldToScreenPoint(invCam, corners[2]);   // 우상
        float wPx = Mathf.Abs(sMax.x - sMin.x), hPx = Mathf.Abs(sMax.y - sMin.y);
        if (wPx < 100f || hPx < 100f) return;   // 비정상(찌부러짐) 방어 - 잘못된 값이면 적용 안 함

        Vector2 sCenter = (sMin + sMax) * 0.5f;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(blurCanvasRt, sCenter, blurCam, out var local))
            region.anchoredPosition = local;
        float sf = blurCanvas.scaleFactor <= 0f ? 1f : blurCanvas.scaleFactor;
        // 패널 코너가 둥근데 블러는 사각이라 모서리가 삐짐. 코너 반경만큼만 안쪽으로(5). 링은 인셋폭에 비례하니 코너만 가릴 최소로.
        const float blurInset = 5f;
        region.sizeDelta = new Vector2(Mathf.Max(0f, wPx / sf - blurInset * 2f), Mathf.Max(0f, hPx / sf - blurInset * 2f));
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
            // 인벤토리가 이미 열려있는 상태에서 상자 열기 요청이면 패널만 재구성
            if (_isOpen && IsChestOpen)
            {
                _chestOpenFrame = Time.frameCount;   // 여는 F 와 닫는 F 가 같은 프레임에 겹치지 않게
                IsInBase = false;
                if (warehousePanel != null) warehousePanel.SetActive(false);
                if (chestPanel != null) chestPanel.SetActive(true);
                if (chestGridUI != null && InventoryManager.ChestInstance != null)
                    chestGridUI.Bind(InventoryManager.ChestInstance);
                if (bagPanel != null) bagPanel.SetActive(true);
            }
            return;
        }

        // 커서·입력 차단은 GameUIController에 위임
        gui?.SetState(GameUIController.UIState.Inventory);

        _isOpen = true;
        if (IsChestOpen) _chestOpenFrame = Time.frameCount;   // 여는 F 와 닫는 F 가 같은 프레임에 겹치지 않게
        inventoryRoot.SetActive(true);
        if (!TimeKov.UI.WindowManager.SuppressPanelSfx) GameSfx.Play(SfxId.PanelWarehouseOpen);   // 창고(인벤토리) 열기음(코드 직결 — 씬 WindowManager 미사용)

        // 창고 표시 판정: 상자 파밍 중이면 창고 X(가방+상자만), 그 외엔 결계존 안일 때만 창고 자동 연동.
        // (구 WarehouseInteractable F 상호작용 폐기 -> 결계존 기반으로 일원화)
        IsInBase = !IsChestOpen && IsPlayerInBase();

        // 결계 안 = 통합패널(창고+가방) 표시. (아래에서 단독 가방은 숨김)
        if (warehousePanel != null)
            warehousePanel.SetActive(IsInBase);

        // 상자 열었을 때 상자 패널 표시
        if (chestPanel != null)
            chestPanel.SetActive(IsChestOpen);

        // 상자 패널 열릴 때 ChestInstance에 그리드 바인딩 (타이밍 이슈 방지)
        if (IsChestOpen && chestGridUI != null && InventoryManager.ChestInstance != null)
            chestGridUI.Bind(InventoryManager.ChestInstance);

        // 단독 가방 = 결계 밖(또는 상자)에서만. 결계 안이면 통합패널의 가방 구역이 대신 표시.
        if (bagPanel != null)
            bagPanel.SetActive(!IsInBase);

        // 블러 캔버스(별도 루트)는 inventoryRoot 밖이라 직접 토글
        if (bagBlurCanvas != null)
            bagBlurCanvas.SetActive(true);

        // 슬라이드 열기 명시 호출 (닫기 도중 재오픈해도 복구되게 - SetActive(true) no-op 대비)
        _slide?.Open();

        RefreshCapacityText();

        // 엔필식 열기: 가방 먼저, 창고 콘텐츠(탭+그리드)는 살짝 뒤 페이드 인.
        // (아이템 첫칸부터 촤라락 순차 등장은 카테고리 바꿀 때만 = InventoryGridUI.SetFilter)
        if (IsInBase)
            PlayWarehouseSectionOpen();
    }

    // 엔필식 열기 애니: 가방 먼저 보이고, 창고 콘텐츠(탭+그리드)는 살짝 뒤 페이드 인.
    private Coroutine _whRevealCo;

    private void PlayWarehouseSectionOpen()
    {
        var cgGrid = EnsureCanvasGroup(warehouseGridUI != null ? warehouseGridUI.gameObject : null);
        var cgTabs = EnsureCanvasGroup(warehouseFilterUI != null ? warehouseFilterUI.gameObject : null);
        if (cgGrid == null && cgTabs == null) return;
        if (_whRevealCo != null) { StopCoroutine(_whRevealCo); _whRevealCo = null; }
        _whRevealCo = StartCoroutine(WarehouseSectionOpenRoutine(cgGrid, cgTabs));
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        if (go == null) return null;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    private System.Collections.IEnumerator WarehouseSectionOpenRoutine(CanvasGroup a, CanvasGroup b)
    {
        if (a != null) a.alpha = 0f;
        if (b != null) b.alpha = 0f;

        // 가방 먼저: 창고 콘텐츠는 잠깐 뒤 등장
        float delay = 0.16f, td = 0f;
        while (td < delay) { td += Time.unscaledDeltaTime; yield return null; }

        float dur = 0.18f, e = 0f;
        while (e < dur)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / dur); k = 1f - (1f - k) * (1f - k);   // ease-out
            if (a != null) a.alpha = k;
            if (b != null) b.alpha = k;
            yield return null;
        }
        if (a != null) a.alpha = 1f;
        if (b != null) b.alpha = 1f;
        _whRevealCo = null;
    }

    // 인벤토리 닫기 (커서·입력 복구까지)
    public void Close()
    {
        CloseInternal();
        // 커서·입력 복구는 GameUIController에 위임
        GameUIController.Instance?.CloseAll();
    }

    // 패널/상태만 닫는다 (커서·입력 복구 위임 없음).
    // GameUIController.CloseAll()이 사망 등에서 인벤을 닫을 때 이걸 호출한다 -
    // Close()를 부르면 다시 CloseAll()로 되돌아와 무한 재귀가 되므로 분리.
    public void CloseInternal()
    {
        if (inventoryRoot == null) return;
        if (_isOpen && !TimeKov.UI.WindowManager.SuppressPanelSfx) GameSfx.Play(SfxId.PanelWarehouseClose);   // 창고(인벤토리) 닫기음(코드 직결). 실제 열려있던 경우만.

        // 드래그 중이었으면 강제 종료 (ESC로 닫을 때 Ghost 화면 잔재 방지)
        InventoryDragHandler.Instance?.EndDrag();

        // 인벤 닫으면 NEW 뱃지 일괄 소멸 (한 번 열어 봤으면 인식 -> 다시 열면 그 자리에 소모품 마커가 뜸)
        InventoryManager.Instance?.ClearAllNewFlags();
        InventoryManager.StorageInstance?.ClearAllNewFlags();
        InventoryManager.ChestInstance?.ClearAllNewFlags();

        // 창고 재진입 플래그 초기화 (다음 TAB 시 창고가 열리지 않도록)
        IsInBase = false;

        // 상자 닫기 — 전리품은 비우지 않는다(껐다 켜도 그대로 즉시 재오픈). 비우기는 ChestInteractable 재가동 때.
        if (IsChestOpen)
            IsChestOpen = false;

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

        // 가로 슬라이드 아웃 후 비활성화 (블러 캔버스도 슬라이드가 같이 끔). 슬라이드 없으면 즉시.
        if (_slide != null && inventoryRoot.activeInHierarchy)
            _slide.Close();
        else
        {
            inventoryRoot.SetActive(false);
            if (bagBlurCanvas != null) bagBlurCanvas.SetActive(false);
        }
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

    /// <summary>
    /// 우클릭 컨텍스트 메뉴가 열려 있으면 닫는다. 열려 있었으면 true.
    /// GameUIController가 ESC/상태전환 시 호출 - 공장/인벤이 닫힐 때 이 메뉴가 화면에 남지 않게.
    /// </summary>
    public bool TryCloseContextMenu()
    {
        if (contextMenu != null && contextMenu.IsOpen) { contextMenu.Close(); return true; }
        return false;
    }

    // 용량 텍스트 + 게이지 갱신
    public void RefreshCapacityText()
    {
        if (InventoryManager.Instance == null) return;
        int used = InventoryManager.Instance.GetUsedSlotCount();
        int max = InventoryManager.Instance.GetMaxSlots();

        if (capacityText != null)
        {
            capacityText.text = Loc.Get("용량") + " " + used + "/" + max;   // clggdesign #4: 띄어쓰기 없이
            // 최종 디자인 = 게이지 바 없음, 글자색만 상태색 (평소 어두운회색 / 90% 노랑 / 가득 빨강)
            float r = max > 0 ? Mathf.Clamp01((float)used / max) : 0f;
            capacityText.color =
                r >= 1f   ? new Color(0.878f, 0.349f, 0.290f, 1f) :   // 가득 빨강 (경고)
                r >= 0.9f ? new Color(0.878f, 0.627f, 0.125f, 1f) :   // 거의참 노랑 (경고)
                            new Color(0.141f, 0.165f, 0.192f, 1f);    // 평소 어두운 슬레이트 #242a31 (잘 보이게)
        }

        // 파밍 상자 카운터 "아이템 N/M" (상자 인벤 기준)
        if (chestCapacityText != null && InventoryManager.ChestInstance != null)
        {
            int cu = InventoryManager.ChestInstance.GetUsedSlotCount();
            int cm = InventoryManager.ChestInstance.GetMaxSlots();
            chestCapacityText.text = Loc.Get("아이템") + " " + cu + "/" + cm;
        }

        // 통합패널(결계 안) 가방 구역 용량도 동일하게
        if (warehouseBagCapacityText != null)
        {
            warehouseBagCapacityText.text = Loc.Get("용량") + " " + used + "/" + max;
            float rw = max > 0 ? Mathf.Clamp01((float)used / max) : 0f;
            warehouseBagCapacityText.color =
                rw >= 1f   ? new Color(0.878f, 0.349f, 0.290f, 1f) :
                rw >= 0.9f ? new Color(0.878f, 0.627f, 0.125f, 1f) :
                             new Color(0.141f, 0.165f, 0.192f, 1f);
        }

        if (bagCapacityGaugeFill != null && max > 0)
        {
            float ratio = Mathf.Clamp01((float)used / max);
            bagCapacityGaugeFill.fillAmount = ratio;
            // HANDOFF 3-5: 90% 이상 노랑, 가득 빨강, 그 외 시안
            bagCapacityGaugeFill.color =
                ratio >= 1f   ? new Color(1f, 0.42f, 0.42f, 1f) :
                ratio >= 0.9f ? new Color(1f, 0.69f, 0.13f, 1f) :
                                new Color(0.37f, 0.77f, 1f, 1f);
        }
    }

    // 가방 -> 창고 입고 후 호출. 현재 창고 탭이 그 아이템을 숨기는 상태면 해당 카테고리 탭으로 자동 전환.
    // (전체 탭이거나 이미 같은 카테고리면 그대로 둠 - 탭이 매번 튀는 것 방지. InventoryDropZone/더블클릭에서 호출)
    public void OnDepositedToWarehouse(int itemId)
    {
        if (!IsInBase || warehouseFilterUI == null) return;

        var data = ItemDatabase.GetItem(itemId);
        if (data == null) return;

        var cur = warehouseFilterUI.CurrentFilter;
        if (cur.HasValue && cur.Value != data.itemCategory)
            warehouseFilterUI.SelectByCategory(data.itemCategory);
    }

    // 단일 슬롯 클릭 핸들러 (시각 강조 없음 - 우클릭 메뉴/버리기 버튼용으로 슬롯만 기억)
    private void OnSlotClicked(InventorySlotUI slot)
    {
        _selectedSlot = slot;
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
                {
                    int movedItemId = slot.SlotData.itemId;   // MoveSlot 후 칸이 비므로 미리 캡처
                    StorageInflowNotice.SuppressBriefly();    // 더블클릭 수동 입고 - 공용 자동입고 알림 제외
                    owner.MoveSlot(slot.SlotData.slotIndex, storage);
                    OnDepositedToWarehouse(movedItemId);
                }
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
        if (slot == null || slot.IsEmpty) return;

        // 파밍 상자 슬롯 = 우클릭으로 바로 가방에 회수 (분할/버리기 메뉴 X)
        if (slot.Owner != null && slot.Owner == InventoryManager.ChestInstance)
        {
            var player = InventoryManager.Instance;
            if (player != null)
                slot.Owner.MoveSlot(slot.SlotData.slotIndex, player);
            ClearSelection();
            RefreshCapacityText();
            return;
        }

        // 일반 슬롯 = 컨텍스트 메뉴(분할/버리기)
        OnSlotClicked(slot);
        contextMenu?.Open(slot, Input.mousePosition);
    }

    // 툴팁 표시 (강조는 슬롯이 호버 시 스스로 처리)
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
        StorageInflowNotice.SuppressBriefly();   // 전체 보내기 버튼 = 수동 입고 - 공용 자동입고 알림 제외
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

    // 화면 좌표가 현재 열린 패널(가방/창고/상자) 안에 있는지 검사 (드래그 버리기 판정용)
    public bool IsPointInsideOpenPanel(Vector2 screenPos)
    {
        return RectContains(bagPanel, screenPos)
            || RectContains(warehousePanel, screenPos)
            || RectContains(chestPanel, screenPos);
    }

    private static bool RectContains(GameObject go, Vector2 screenPos)
    {
        if (go == null || !go.activeInHierarchy) return false;
        var rt = go.transform as RectTransform;
        if (rt == null) return false;
        var canvas = go.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam);
    }

    // 슬롯을 패널 밖으로 드래그해 놓으면 호출 = 바닥에 버리기(루트박스로 회수 가능). 결계 안/밖 공통.
    public void DiscardSlotByDrag(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;

        // 버리기는 인벤토리 화면(가방/창고/상자)에서만 허용. 설비(공장) UI 등 다른 상태에선 막는다.
        // (설비 UI에서 슬롯 옆 빈 곳에 잘못 드롭하면 아이템이 버려지던 문제 차단 - 그 경우 그냥 취소)
        var gui = GameUIController.Instance;
        if (gui != null && gui.GetCurrentState() != GameUIController.UIState.Inventory) return;

        // 라이브 시각 칸 대신 드래그 시작 때 박제한 출발 슬롯 사용(컴팩트 재렌더 시 엉뚱한 아이템이 버려지는 것 방지).
        var dh = InventoryDragHandler.Instance;
        if (dh == null || !dh.SourceStillValid()) return;

        var owner   = dh.SrcManager;
        int slotIdx = dh.SrcSlotIndex;
        int itemId  = dh.SrcItemId;
        var real    = owner.GetSlot(slotIdx);
        if (real == null) return;
        int amount  = dh.IsSplitDrag ? Mathf.Min(dh.DragAmount, real.amount) : real.amount;
        if (amount <= 0) return;

        owner.RemoveFromSlot(slotIdx, amount);
        SpawnDroppedItem(itemId, amount);
        ClearSelection();
        RefreshCapacityText();
    }

    // 분할 팝업 열기
    public void OpenSplitPopup(InventorySlotUI slot)
    {
        if (splitPopup == null) return;
        confirmPopup?.Close();
        splitPopup.Open(slot);
    }

    // 버리기 확인 팝업 열기
    public void OpenTrashConfirm(InventorySlotUI slot, bool permanent = false)
    {
        if (confirmPopup == null || slot == null || slot.IsEmpty) return;

        splitPopup?.Close();

        var data = ItemDatabase.GetItem(slot.SlotData.itemId);
        string name = data != null ? data.GetLocalizedName() : Loc.Get("아이템");
        int amount = slot.SlotData.amount;
        int itemId = slot.SlotData.itemId;
        int slotIdx = slot.SlotData.slotIndex;
        var owner = slot.Owner;

        string message = name + " x" + amount + (permanent ? Loc.Get("개를 영구 삭제하시겠습니까?") : Loc.Get("개를 버리시겠습니까?"));

        confirmPopup.Open(message, () =>
        {
            owner?.RemoveFromSlot(slotIdx, amount);
            if (!permanent) SpawnDroppedItem(itemId, amount);   // 영구삭제는 바닥에 드롭하지 않음
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
        _selectedSlot = null;
    }
}