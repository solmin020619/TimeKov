using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [Header("레일 탭 아이콘(PNG)")]
    [Tooltip("좌측 레일 가방 탭 아이콘(흰색 PNG, 런타임 틴트). 비우면 절차 도형 폴백.")]
    [SerializeField] private Sprite railBagSprite;
    [Tooltip("좌측 레일 창고 탭 아이콘(흰색 PNG, 런타임 틴트). 비우면 절차 도형 폴백.")]
    [SerializeField] private Sprite railStorageSprite;
    [Tooltip("드래그 대상 강조 프레임 스프라이트(통합 인벤과 동일 hl_region_frame). 비우면 시안 4변 폴백.")]
    [SerializeField] private Sprite regionFrameSprite;

    [Header("가방/창고 탭")]
    [Tooltip("좌측 인벤 용량 표시 (예: 용량 11 / 35).")]
    [SerializeField] private TextMeshProUGUI bagCapacityText;
    [Tooltip("가방 보기 탭 버튼.")]
    [SerializeField] private Button bagTabBtn;
    [Tooltip("창고(Storage) 보기 탭 버튼.")]
    [SerializeField] private Button storageTabBtn;
    private bool _showStorage;

    [Header("헤더 설비 아이콘")]
    [Tooltip("헤더 좌측 설비 아이콘. OpenFor 가 facilityImage 와 같은 sprite 로 세팅.")]
    [SerializeField] private Image headerIconImage;

    [Header("진행바 노브 / 빈가방 / 가동글로우")]
    [Tooltip("푸터 진행바 fill 끝 노브. value 로 x 이동, 가공 중만 표시.")]
    [SerializeField] private RectTransform progressKnob;
    [Tooltip("가방이 비었을 때 표시하는 '비어있음' 텍스트.")]
    [SerializeField] private TextMeshProUGUI bagEmptyText;
    [Tooltip("가공 중 기계 뒤 노란 글로우. 알파 펄스(unscaled).")]
    [SerializeField] private Image machineGlow;

    [Header("현재 생산 공식 스트립(하단)")]
    [Tooltip("공식 아이콘 엔트리 부모(HLG). 런타임이 재료->결과 아이콘을 채움.")]
    [SerializeField] private Transform formulaContent;
    [Tooltip("공식 스트립 좌측 상태 라벨(생산 중 / 대기 중).")]
    [SerializeField] private TextMeshProUGUI formulaStatusText;
    [Tooltip("흐름 레일(기계->출력). 입력 레일은 슬롯 자식이라 별도 ref 불필요.")]
    [SerializeField] private Image outputRail;
    [Tooltip("입력 수직 버스(합류선). 길이/위치는 런타임이 입력 칸수로 세팅.")]
    [SerializeField] private Image inputBus;
    [Tooltip("버스->기계 가로 연결.")]
    [SerializeField] private Image busToMachine;
    [Tooltip("공정 흐름 레일 컨테이너(런타임이 포트/버스/레일 생성). 0크기 = 생산영역 중심 기준 좌표.")]
    [SerializeField] private RectTransform flowRailsRoot;
    // 실제 레일(RailPiece 프리팹)을 RenderTexture 로 렌더해 UI 에 표시(도감 초상화 방식). 연결된 포트에만.
    private RailPortraitRenderer _railPortrait;
    private Texture _railTex;

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

    // 깔끔 게이지(엔필식): 얇은 선 + 양끝 점 + 좌->우 채움 + 선 위 "N초".
    // 옛 하단 슬라이더(progressBar+Knob)/트레이 노란 게이지(ProcessingGauge)를 화면에서 대체한다.
    private RectTransform _gaugeRoot;
    private Image _gaugeFill;
    private const float GA_W = 300f;   // 게이지 선 길이
    private const float GA_H = 3f;     // 게이지 선 두께

    // 설비 UI 열림 여부(HUD 자동 페이드 등 외부에서 참조).
    public static bool IsAnyOpen { get; private set; }

    // 가방/창고 이중 섹션(엔필식): 두 섹션이 세로로 늘 보이고(가방 위/창고 아래 고정),
    // 펼친 쪽 = 그리드, 접힌 쪽 = "여기로 드래그하여 보관" 박스(=드롭 타겟 + 클릭하면 펼침).
    private const float SEC_TopY    = 52f;   // 컬럼 최상단 예약 공간(엔필의 ON/OFF 토글 자리 - 확장성 대비 비워둠)
    private const float SEC_HeaderH = 34f;   // 섹션 헤더(기존 탭 버튼 재배치) 높이
    private const float SEC_BoxH    = 88f;   // 접힘 박스 높이(엔필 비례로 확대)
    private const float SEC_RailW    = 52f;  // 좌측 아이콘 레일 탭 가로(종욱: 뚱뚱 금지, 세로로 길게)
    private const float SEC_RailHOn  = 116f; // 활성(펼침) 레일 탭 세로 - 홀쭉하고 길게
    private const float SEC_RailHOff = 92f;  // 생성 시 초기값일 뿐 - 접힘 측은 ApplySectionLayout 이 섹션 밴드(헤더~박스)에 맞춤
    private const float SEC_LeftX   = 64f;   // 레일 오른쪽 = 컨텐츠 시작 x (레일 4+52+간격 8. 헤더/박스/그리드 전부)
    private const float SEC_ColW    = 620f;  // 가방 칼럼 폭(빌더 BagWidth 미러 - 그리드 셀 재계산용)
    private const float SEC_FilterH = 58f;   // 창고 카테고리 필터 행 높이(0.85 스케일 실높이 약 53)
    private RectTransform _bagBoxRt;         // 가방 접힘 박스(창고 펼침일 때 표시)
    private RectTransform _stoBoxRt;         // 창고 접힘 박스(가방 펼침일 때 표시)
    private RectTransform _bagViewportRt;    // 스크롤 뷰포트(펼친 섹션 자리로 이동)
    private RectTransform _railBagRt, _railStoRt;      // 좌측 레일 아이콘 탭(가방/창고)
    private Image _railBagBg, _railStoBg;
    private Image[] _railBagIcon, _railStoIcon;
    private CategoryFilterUI _storageFilterUI;          // 창고 카테고리 필터(통합 인벤 패널에서 복제)
    private RectTransform _filterRowRt;
    private ItemCategory? _storageFilter;               // null = 전체
    private int _pendingViewSwitch = -1;                // 드롭 직후 자동 전환 예약(-1 없음/0 가방/1 창고)
    private readonly List<InventorySlotUI> _invSlots = new();
    // 창고 필터 컴팩트 표시용 매칭 슬롯 버퍼(매 갱신 재사용, GC 회피)
    private readonly List<InventorySlot> _invFilterBuf = new();
    // A안: 드래그 중엔 인벤 재배치를 미뤘다가 놓으면 Update 가 flush(컴팩트 잔상 방지, 정확성은 B 스냅샷이 보장)
    private bool _invRefreshPending;
    /// <summary>outputSlot 외에 동적으로 생성된 추가 출력 슬롯들.</summary>
    private readonly List<MachineSlotWidget> _extraOutputSlots = new();

    // 현재 선택 레시피의 출력 버퍼에 받을 게 있는지(모두받기 dim #34 + 출력 펄스 #40 공통).
    private bool _hasOutput;
    private Coroutine _tabFadeCo;

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
        SetupCleanGauge();
        SetupDualSections();
        SetupActionButtons();
    }

    // ── 하단 액션 버튼(재료 회수 / 모두 받기) 정돈 ─────────────────────
    // 같은 높이(64) 한 줄, 우측 정렬, 바닥선 = 연료 슬롯과 동일(28). 위가 밝은 그라데이션으로 입체감.
    // 모두 받기 = 밝은 옐로 주버튼 / 재료 회수 = 밝은 간유리 보조버튼(접힘 박스와 같은 문법).
    private void SetupActionButtons()
    {
        StyleActionButton(takeInputsBtn, new Vector2(170f, 64f), new Vector2(-(26f + 280f + 12f), 28f),
            new Color(0.92f, 0.95f, 0.98f, 0.16f), new Color(0.92f, 0.95f, 0.98f, 0.92f), 18f);
        // 주버튼 = 노란색 금지(종욱) -> 우리 정체성 색(벨트 시안 계열) + 어두운 텍스트.
        StyleActionButton(takeOutputBtn, new Vector2(280f, 64f), new Vector2(-26f, 28f),
            new Color(0.30f, 0.72f, 0.95f, 1f), new Color(0.03f, 0.10f, 0.16f, 1f), 22f);
    }

    private static void StyleActionButton(Button b, Vector2 size, Vector2 pos, Color bg, Color txt, float fontSize)
    {
        if (b == null) return;
        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        if (b.image != null)
        {
            b.image.color = bg;
            if (b.image.GetComponent<UIFrostGradient>() == null)
            {
                var g = b.image.gameObject.AddComponent<UIFrostGradient>();
                g.topColor = new Color(1f, 1f, 1f, 1f);
                g.bottomColor = new Color(0.58f, 0.58f, 0.58f, 1f);   // 아래로 가라앉는 셰이딩 = 입체감
            }
        }
        var tmp = b.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) { tmp.color = txt; tmp.fontSize = fontSize; }
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

    // ── 깔끔 게이지 (엔필식 정리) ─────────────────────────────────────
    // 정리: 하단 슬라이더/노브 끔, 트레이 노란 게이지 끔, 공식 스트립 상태라벨 끔, 하단 드롭힌트 끔.
    // 생성: 도면 아래 얇은 선 게이지(양끝 점 + 좌->우 채움) + 그 위 "N초"(기존 텍스트 이사).
    private void SetupCleanGauge()
    {
        if (_gaugeRoot != null || uiPanel == null) return;

        // 게이지 위치 = 옛 트레이 게이지 자리 재사용(도면과 정렬 유지).
        Transform gaugeParent = uiPanel.transform;
        Vector2 gaugePos = new Vector2(0f, -260f);
        if (processingGauge != null)
        {
            var gRt = (RectTransform)processingGauge.transform;
            gaugeParent = gRt.parent; gaugePos = gRt.anchoredPosition;
            processingGauge.gameObject.SetActive(false);
        }
        if (progressBar != null) progressBar.gameObject.SetActive(false);
        if (progressKnob != null) progressKnob.gameObject.SetActive(false);
        if (formulaStatusText != null) formulaStatusText.gameObject.SetActive(false);
        var hint = FindDeep(uiPanel.transform, "DropHint");
        if (hint != null) hint.gameObject.SetActive(false);

        // (1) 게이지: 트랙(흐린 선) + 채움(밝은 선) + 양끝 점.
        _gaugeRoot = MakeRect("CleanGauge", gaugeParent, new Vector2(GA_W, 22f), gaugePos);
        MakeChildImage("Track", _gaugeRoot, new Vector2(GA_W, GA_H), Vector2.zero, new Color(1f, 1f, 1f, 0.28f), null);
        _gaugeFill = MakeChildImage("Fill", _gaugeRoot, new Vector2(0f, GA_H), new Vector2(-GA_W * 0.5f, 0f),
                                    new Color(1f, 1f, 1f, 0.95f), null);
        _gaugeFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        MakeChildImage("DotL", _gaugeRoot, new Vector2(8f, 8f), new Vector2(-GA_W * 0.5f, 0f), new Color(1f, 1f, 1f, 0.9f), CircleSprite());
        MakeChildImage("DotR", _gaugeRoot, new Vector2(8f, 8f), new Vector2(GA_W * 0.5f, 0f), new Color(1f, 1f, 1f, 0.9f), CircleSprite());

        // "N초" 텍스트를 게이지 바로 위 중앙으로 이사(도면 한가운데 떠 있던 것).
        if (_processTimeText != null)
        {
            var tr = _processTimeText.rectTransform;
            tr.SetParent(_gaugeRoot, false);
            tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(220f, 26f);
            tr.anchoredPosition = new Vector2(0f, 20f);
            _processTimeText.alignment = TextAlignmentOptions.Center;
        }

    }

    private static RectTransform MakeRect(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        return rt;
    }

    private static Image MakeChildImage(string name, Transform parent, Vector2 size, Vector2 pos, Color color, Sprite sprite)
    {
        var rt = MakeRect(name, parent, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color; img.raycastTarget = false;
        if (sprite != null) img.sprite = sprite;
        return img;
    }

    // ── 가방/창고 이중 섹션(엔필식) ──────────────────────────────────
    // 기존 상단 탭 2개를 "섹션 헤더"로 재배치하고, 접힌 쪽엔 드롭 박스를 만든다.
    // 실제 어느 쪽이 펼쳐지는지는 ApplySectionLayout 이 _showStorage 로 결정.
    private void SetupDualSections()
    {
        if (_stoBoxRt != null || inventorySlotParent == null) return;
        _bagViewportRt = inventorySlotParent.parent as RectTransform;   // BagContent 의 부모 = BagViewport(스크롤)
        if (_bagViewportRt == null) return;
        var col = _bagViewportRt.parent;                                 // BagColumn
        // 그리드는 상하 stretch(위 = 헤더 아래 / 아래 = 하단 고정 섹션 위) - 해상도 무관하게 바닥 라인 정렬.
        _bagViewportRt.anchorMin = new Vector2(0f, 0f);
        _bagViewportRt.anchorMax = new Vector2(1f, 1f);
        _bagViewportRt.pivot = new Vector2(0.5f, 0.5f);

        // 옛 탭 행 장식(사이 세로선/헤더 밑줄)은 새 레이아웃과 안 맞으니 끔.
        var td = FindDeep(col, "TabDivider");
        if (td != null) td.gameObject.SetActive(false);
        var bd = FindDeep(col, "BagHeaderDivider");
        if (bd != null) bd.gameObject.SetActive(false);

        // 탭 버튼 -> 풀폭 섹션 헤더. 텍스트는 좌측 정렬(엔필식 헤더 느낌).
        StyleSectionHeader(bagTabBtn);
        StyleSectionHeader(storageTabBtn);

        // 용량 = 가방 헤더 아래 왼쪽 정렬 한 줄(엔필식). 창고 뷰에선 숨김(ApplySectionLayout).
        if (bagCapacityText != null)
        {
            var crt = bagCapacityText.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.sizeDelta = new Vector2(240f, 20f);
            bagCapacityText.alignment = TextAlignmentOptions.Left;
            bagCapacityText.fontSize = 14;
        }

        // 좌측 아이콘 레일(진짜 탭). 컨텐츠는 레일 오른쪽(SEC_LeftX)부터 -> 그리드 셀 폭 재계산.
        _railBagRt = MakeRailTab(col, false, out _railBagBg, out _railBagIcon);
        _railStoRt = MakeRailTab(col, true,  out _railStoBg, out _railStoIcon);
        var grid = inventorySlotParent.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            float innerW = SEC_ColW - SEC_LeftX - 8f;   // 뷰포트 좌 SEC_LeftX / 우 8
            // 그리드 padding 까지 빼야 4열이 마스크에 안 잘린다(빌더 공식엔 padding 누락).
            float cell = Mathf.Floor((innerW - grid.padding.horizontal - grid.spacing.x * 3f) / 4f);
            grid.cellSize = new Vector2(cell, cell);
        }

        // 창고 카테고리 필터: 통합 인벤 패널의 CategoryFilterUI 를 복제(아이콘/색/펼침 애니 그대로).
        var srcFilter = FindFirstObjectByType<CategoryFilterUI>(FindObjectsInactive.Include);
        if (srcFilter != null)
        {
            var cloneGo = Instantiate(srcFilter.gameObject, col);
            cloneGo.name = "StorageCategoryFilter";
            cloneGo.SetActive(false);   // 창고 펼침일 때만 ApplySectionLayout 이 켠다
            _filterRowRt = (RectTransform)cloneGo.transform;
            _filterRowRt.anchorMin = _filterRowRt.anchorMax = new Vector2(0f, 1f);
            _filterRowRt.pivot = new Vector2(0f, 1f);
            _filterRowRt.localScale = new Vector3(0.85f, 0.85f, 1f);   // 칼럼 폭에 맞게 살짝 축소
            // 원본은 stretch 앵커라 sizeDelta.x=0 -> 앵커 교체 후 폭을 명시해야 탭들이 행 안에 온다.
            _filterRowRt.sizeDelta = new Vector2((SEC_ColW - SEC_LeftX - 8f) / 0.85f, 62f);
            _storageFilterUI = cloneGo.GetComponent<CategoryFilterUI>();
            _storageFilterUI.OnFilterChanged += f => { _storageFilter = f; UpdateStorageHeaderLabel(); RefreshInventorySlots(); };
            UpdateStorageHeaderLabel();   // 첫 오픈부터 "창고 | 전체" 형식 유지(필터 클릭 전 맨 "창고"로 뜨던 것 통일)
        }

        _bagBoxRt = MakeCollapsedBox(col, "가방", false);
        _stoBoxRt = MakeCollapsedBox(col, "창고", true);
        // 레이아웃만 하면 레일 아이콘이 첫 클릭 전까지 무착색(활성탭 = 흰 위 흰 = 안 보임).
        // 스타일까지 포함한 UpdateTabVisual 로 초기화해야 처음 열 때부터 보인다.
        UpdateTabVisual();
    }

    // 좌측 레일 아이콘 탭: 텍스트 없이 절차 도형 아이콘만(가방 = 손잡이+몸통 / 창고 = 상자+뚜껑선).
    private RectTransform MakeRailTab(Transform col, bool toStorage, out Image bg, out Image[] iconParts)
    {
        var go = new GameObject(toStorage ? "RailStorageTab" : "RailBagTab",
            typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(col, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(SEC_RailW, SEC_RailHOff);   // 높이는 ApplySectionLayout 이 활성/비활성으로 갱신

        bg = go.GetComponent<Image>();
        bg.sprite = UISpriteFactory.RoundedRect(48, 10);
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = true;

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        if (toStorage) btn.onClick.AddListener(ShowStorage);
        else           btn.onClick.AddListener(ShowBag);

        // 디자인 PNG(흰색, 틴트용)가 배선돼 있으면 그걸 쓰고, 없으면 절차 도형 폴백.
        Sprite iconSpr = toStorage ? railStorageSprite : railBagSprite;
        if (iconSpr != null)
        {
            var ic = MakeChildImage("Icon", rt, new Vector2(40f, 40f), Vector2.zero, Color.white, iconSpr);
            ic.preserveAspect = true;
            iconParts = new[] { ic };
            return rt;
        }

        var rounded = UISpriteFactory.RoundedRect(24, 6);
        if (!toStorage)
        {
            var handle = MakeChildImage("Handle", rt, new Vector2(20f, 11f), new Vector2(0f, 13f), Color.white, rounded);
            var body   = MakeChildImage("Body",   rt, new Vector2(32f, 22f), new Vector2(0f, -4f), Color.white, rounded);
            handle.type = body.type = Image.Type.Sliced;
            iconParts = new[] { handle, body };
        }
        else
        {
            var body = MakeChildImage("Body", rt, new Vector2(32f, 25f), new Vector2(0f, -4f), Color.white, rounded);
            var lid  = MakeChildImage("Lid",  rt, new Vector2(38f, 7f),  new Vector2(0f, 13f),  Color.white, rounded);
            body.type = lid.type = Image.Type.Sliced;
            iconParts = new[] { body, lid };
        }
        return rt;
    }

    private static void StyleRailTab(Image bg, Image[] icons, bool on)
    {
        // 엔필식: 활성 탭 = 밝은(흰) 탭 + 어두운 아이콘 / 비활성 = 어두운 탭 + 흐린 밝은 아이콘.
        if (bg != null) bg.color = on ? new Color(0.92f, 0.95f, 0.98f, 0.88f) : new Color(0.92f, 0.95f, 0.98f, 0.10f);
        if (icons == null) return;
        foreach (var i in icons)
            if (i != null) i.color = on ? new Color(0.16f, 0.20f, 0.26f, 0.95f) : new Color(1f, 1f, 1f, 0.40f);
    }

    private static void StyleSectionHeader(Button b)
    {
        if (b == null) return;
        var tmp = b.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.margin = new Vector4(12f, 0f, 0f, 0f);
            tmp.fontSize = 18;   // 엔필 비례로 확대
        }
        // 빌더가 붙인 Chrome 아웃라인 제거: 어두운 배경에서만 테두리 박스처럼 도드라져 두 헤더가 딴판으로 보였음.
        var ol = b.GetComponent<UnityEngine.UI.Outline>();
        if (ol != null) ol.enabled = false;
        // 상시 얇은 밑줄(엔필 헤더의 옅은 구분선). 활성 노란 밑줄(Underline)과 별개.
        var line = new GameObject("HairLine", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(b.transform, false);
        var lrt = (RectTransform)line.transform;
        lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f); lrt.pivot = new Vector2(0.5f, 0f);
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = new Vector2(0f, 2f);
        var li = line.GetComponent<Image>();
        li.color = new Color(0.85f, 0.89f, 0.94f, 0.28f);
        li.raycastTarget = false;
    }

    // 접힘 박스: 라운드 반투명 + 안내 텍스트. 드롭 = 반대 컨테이너로 이동, 클릭 = 그 섹션 펼침.
    private RectTransform MakeCollapsedBox(Transform col, string containerName, bool toStorage)
    {
        var go = new GameObject(toStorage ? "StorageDropBox" : "BagDropBox",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(MachineTransferDropBox));
        go.transform.SetParent(col, false);
        var rt = (RectTransform)go.transform;

        // 입체감(엔필 힌트박스 디테일): 밝은 림(바깥) + 살짝 인셋된 면(안쪽, 위가 밝은 그라데이션) = 눌린 판 느낌.
        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(64, 14);
        img.type = Image.Type.Sliced;
        img.color = new Color(0.85f, 0.90f, 0.95f, 0.34f);   // 림(테두리 층) - 입체감 강화
        img.raycastTarget = true;

        var faceGo = new GameObject("Face", typeof(RectTransform), typeof(Image), typeof(UIFrostGradient));
        faceGo.transform.SetParent(go.transform, false);
        var frt = (RectTransform)faceGo.transform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(1.5f, 1.5f); frt.offsetMax = new Vector2(-1.5f, -1.5f);
        var face = faceGo.GetComponent<Image>();
        face.sprite = UISpriteFactory.RoundedRect(64, 13);
        face.type = Image.Type.Sliced;
        Color idle   = new Color(1f, 1f, 1f, 0.15f);
        Color active = new Color(1f, 1f, 1f, 0.24f);
        face.color = idle;
        face.raycastTarget = false;
        var fg = faceGo.GetComponent<UIFrostGradient>();
        fg.topColor = new Color(1f, 1f, 1f, 1f);                    // 위 밝게
        fg.bottomColor = new Color(0.32f, 0.36f, 0.42f, 0.60f);     // 아래로 더 깊게 가라앉음(입체감 강화)

        var tb = go.GetComponent<MachineTransferDropBox>();
        tb.toStorage = toStorage; tb.highlight = face; tb.idleColor = idle; tb.activeColor = active;
        tb.owner = this;   // 드롭 성공 시 자동 전환 요청용
        tb.highlightFrame = DropHighlightFrame.Build(rt, regionFrameSprite);

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        if (toStorage) btn.onClick.AddListener(ShowStorage);
        else           btn.onClick.AddListener(ShowBag);

        var tmp = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        tmp.transform.SetParent(go.transform, false);
        var trt = tmp.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(10f, 4f); trt.offsetMax = new Vector2(-10f, -4f);
        if (bagCapacityText != null) tmp.font = bagCapacityText.font;   // 한글 폰트 유지
        tmp.fontSize = 22;   // 엔필 비례(박스 높이 대비 글자 크기)로 확대. 15는 박스만 커지고 글자가 못 따라갔음
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.20f, 0.23f, 0.28f, 0.85f);   // 밝은 박스 표면 = 어두운 글자(엔필과 동일 원리)
        tmp.text = "아이템을 여기로 드래그하여 " + containerName + "에 보관 가능";
        tmp.raycastTarget = false;
        return rt;
    }

    // 창고 헤더에 현재 필터명 표시(엔필식 "무릉 창고 | 광물"). 필터 UI 없으면 "창고" 유지.
    private void UpdateStorageHeaderLabel()
    {
        if (storageTabBtn == null || _storageFilterUI == null) return;
        var tmp = storageTabBtn.GetComponentInChildren<TextMeshProUGUI>(true);   // 비활성 중 호출 대비(SetTabActive 와 동일 이유)
        if (tmp == null) return;
        string f = "전체";
        if (_storageFilter != null)
        {
            switch (_storageFilter.Value)
            {
                case ItemCategory.RawMaterial:        f = "원재료"; break;
                case ItemCategory.ProcessedTier1:     f = "1차 가공품"; break;
                case ItemCategory.ProcessedTier2:     f = "2차 가공품"; break;
                case ItemCategory.TacticalConsumable: f = "전술 소모품"; break;
                case ItemCategory.CoreUpgrade:        f = "핵심 강화"; break;
                case ItemCategory.Special:            f = "특수"; break;
            }
        }
        tmp.text = "창고 | " + f;
    }

    // 섹션 배치(가방 위/창고 아래 고정, 펼친 쪽만 그리드):
    //   가방 펼침 = [가방헤더][그리드........][창고헤더][창고박스]
    //   창고 펼침 = [가방헤더][가방박스][창고헤더][그리드........]
    private void ApplySectionLayout()
    {
        if (_bagViewportRt == null || _bagBoxRt == null || _stoBoxRt == null) return;
        RectTransform bagRt = bagTabBtn != null ? (RectTransform)bagTabBtn.transform : null;
        RectTransform stoRt = storageTabBtn != null ? (RectTransform)storageTabBtn.transform : null;
        float lx = SEC_LeftX;   // 레일 오른쪽부터 컨텐츠

        // 위 = ON/OFF 예약(SEC_TopY) -> 가방 헤더부터 아래로. 아래 = 컬럼 바닥(=연료 슬롯 바닥선)에 고정.
        // 그리드가 그 사이를 stretch 로 채워 해상도가 달라도 바닥 라인이 항상 맞는다.
        float y = SEC_TopY;

        // 가방 헤더(+레일 탭) = 항상 맨 위. 활성 탭만 살짝 길다(엔필 디테일).
        if (bagRt != null)
        {
            bagRt.anchorMin = new Vector2(0f, 1f); bagRt.anchorMax = new Vector2(1f, 1f); bagRt.pivot = new Vector2(0.5f, 1f);
            bagRt.offsetMin = new Vector2(lx, -(y + SEC_HeaderH)); bagRt.offsetMax = new Vector2(-4f, -y);
        }
        if (_railBagRt != null)
        {
            // 접힘(창고 뷰) = 가방 섹션 밴드(헤더 34 + 간격 6 + 박스 88)와 세로 범위 정확히 일치.
            _railBagRt.anchorMin = _railBagRt.anchorMax = new Vector2(0f, 1f); _railBagRt.pivot = new Vector2(0f, 1f);
            _railBagRt.anchoredPosition = new Vector2(4f, -y);
            _railBagRt.sizeDelta = new Vector2(SEC_RailW, _showStorage ? SEC_HeaderH + 6f + SEC_BoxH : SEC_RailHOn);
        }
        y += SEC_HeaderH;

        if (!_showStorage)
        {
            _bagBoxRt.gameObject.SetActive(false);
            if (_filterRowRt != null) _filterRowRt.gameObject.SetActive(false);

            // 위: 용량 줄까지 흐름 / 아래: 창고 헤더 + 박스를 컬럼 바닥에 고정(박스 하단 = 연료 슬롯 라인).
            if (bagCapacityText != null)
            {
                bagCapacityText.gameObject.SetActive(true);
                bagCapacityText.rectTransform.anchoredPosition = new Vector2(lx + 2f, -(y + 4f));
            }
            y += 30f;

            float tail = SEC_BoxH + 6f + SEC_HeaderH;   // 바닥 고정 구역 높이(박스 + 간격 + 헤더)
            _bagViewportRt.offsetMin = new Vector2(lx, tail + 10f);
            _bagViewportRt.offsetMax = new Vector2(-8f, -y);

            if (stoRt != null)
            {
                stoRt.anchorMin = new Vector2(0f, 0f); stoRt.anchorMax = new Vector2(1f, 0f); stoRt.pivot = new Vector2(0.5f, 0f);
                stoRt.offsetMin = new Vector2(lx, SEC_BoxH + 6f); stoRt.offsetMax = new Vector2(-4f, SEC_BoxH + 6f + SEC_HeaderH);
            }
            if (_railStoRt != null)
            {
                // 접힘(가방 뷰) = 창고 섹션 밴드(박스 바닥 0 ~ 헤더 위끝)와 세로 범위 정확히 일치(종욱 지시).
                _railStoRt.anchorMin = _railStoRt.anchorMax = new Vector2(0f, 0f); _railStoRt.pivot = new Vector2(0f, 0f);
                _railStoRt.anchoredPosition = new Vector2(4f, 0f);
                _railStoRt.sizeDelta = new Vector2(SEC_RailW, SEC_BoxH + 6f + SEC_HeaderH);
            }
            _stoBoxRt.gameObject.SetActive(true);
            _stoBoxRt.anchorMin = new Vector2(0f, 0f); _stoBoxRt.anchorMax = new Vector2(1f, 0f); _stoBoxRt.pivot = new Vector2(0.5f, 0f);
            _stoBoxRt.offsetMin = new Vector2(lx, 0f); _stoBoxRt.offsetMax = new Vector2(-8f, SEC_BoxH);

            // 헤더 글자색은 SetTabActive 가 상태 기준으로 통일 관리(위치 기준 전환 폐기).
        }
        else
        {
            _stoBoxRt.gameObject.SetActive(false);
            if (bagCapacityText != null) bagCapacityText.gameObject.SetActive(false);   // 레퍼런스처럼 창고 뷰엔 용량 줄 없음

            // 위: 가방 접힘 박스 -> 창고 헤더 -> 카테고리 필터 / 아래: 그리드 바닥을 컬럼 바닥에 고정.
            y += 6f;
            _bagBoxRt.gameObject.SetActive(true);
            _bagBoxRt.anchorMin = new Vector2(0f, 1f); _bagBoxRt.anchorMax = new Vector2(1f, 1f); _bagBoxRt.pivot = new Vector2(0.5f, 1f);
            _bagBoxRt.offsetMin = new Vector2(lx, -(y + SEC_BoxH)); _bagBoxRt.offsetMax = new Vector2(-8f, -y);
            y += SEC_BoxH + 10f;

            if (stoRt != null)
            {
                stoRt.anchorMin = new Vector2(0f, 1f); stoRt.anchorMax = new Vector2(1f, 1f); stoRt.pivot = new Vector2(0.5f, 1f);
                stoRt.offsetMin = new Vector2(lx, -(y + SEC_HeaderH)); stoRt.offsetMax = new Vector2(-4f, -y);
            }
            if (_railStoRt != null)
            {
                _railStoRt.anchorMin = _railStoRt.anchorMax = new Vector2(0f, 1f); _railStoRt.pivot = new Vector2(0f, 1f);
                _railStoRt.anchoredPosition = new Vector2(4f, -y);
                _railStoRt.sizeDelta = new Vector2(SEC_RailW, SEC_RailHOn);
            }
            y += SEC_HeaderH;
            if (_filterRowRt != null)
            {
                _filterRowRt.gameObject.SetActive(true);
                _filterRowRt.anchoredPosition = new Vector2(lx, -(y + 4f));
                y += 4f + SEC_FilterH;
            }
            _bagViewportRt.offsetMin = new Vector2(lx, 0f);   // 그리드 바닥 = 컬럼 바닥(연료 슬롯 라인)
            _bagViewportRt.offsetMax = new Vector2(-8f, -(y + 2f));

            // 헤더 글자색은 SetTabActive 가 상태 기준으로 통일 관리(위치 기준 전환 폐기).
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
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

    // 중앙 도면 PNG 밝기(배경 가라앉히기). 너무 흐리면 올리고 슬롯/칩과 또 싸우면 내려라.
    private const float FacilityArtAlpha = 0.50f;

    // (생산 구역 어두운 무대는 오답 판정 - 종욱: 엔필의 어두워 보이는 구역은 별도 판이 아니라 그라데이션. 제거함)
    // (모델 렌더를 무채색으로 구워 배경에 까는 실험도 오답 판정 - 도면 PNG 경로로 확정됐다.)

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
        // 창고 변경도 구독: 접힘 박스 드롭(가방<->창고 이동)이 창고 뷰/용량에 바로 반영되게.
        var sto = InventoryManager.StorageInstance;
        if (sto != null)
        {
            sto.OnInventoryChanged -= RefreshInventorySlots;
            sto.OnInventoryChanged += RefreshInventorySlots;
        }

        _selectedRecipeIndex = Mathf.Max(0, machine.LockedRecipeIndex);

        if (machineTitleText != null) machineTitleText.text = title;
        StyleHeaderTitle();

        // 중앙 설비 도면 = 전용 도면 폴더(클로드디자인) 우선, 없으면 퀵슬롯 모델 폴백.
        //   도면 위치: Assets/Resources/Image/UI_Icon/FacilityBlueprint/  파일명 "{facilityId}" 또는 "{facilityId}_설비명".
        //   헤더 아이콘은 작은 모델 아이콘이라 그대로 FacilityIconDatabase 사용.
        if (facilityImage != null || headerIconImage != null)
        {
            var fIcon = FacilityIconDatabase.Instance != null
                ? FacilityIconDatabase.Instance.GetIcon(machine.FacilityId) : null;
            if (facilityImage != null)
            {
                var blueprint = LoadFacilityBlueprint(machine.FacilityId);
                var spr = blueprint != null ? blueprint : fIcon;
                facilityImage.sprite = spr;
                facilityImage.enabled = spr != null;
                // 도면은 배경 취급: 흰 라인 풀알파가 슬롯/칩과 같은 층에서 싸움 - 알파로 가라앉힘.
                facilityImage.color = new Color(1f, 1f, 1f, FacilityArtAlpha);
            }
            if (headerIconImage != null) { headerIconImage.sprite = fIcon; headerIconImage.enabled = fIcon != null; }
        }

        // 모든 슬롯을 먼저 구성한 뒤 패널을 활성화 —
        // SetActive 이전에 Setup을 완료해야 RecipeDropSlot의 Start/OnEnable
        // 기본 상태("재료 넣기" + 흰 박스)가 한 프레임 깜빡이는 현상을 방지한다.
        _showStorage = false;   // 열 때 항상 가방 뷰부터
        _pendingViewSwitch = -1;   // 직전 세션의 드롭 자동전환 예약이 새로 열 때 발화하지 않게
        BuildRecipeSlots();
        BuildInventorySlots();
        // 탭 페이드(#17)가 닫힘으로 중단돼 알파가 덜 찼을 수 있어 열 때 1로 리셋.
        var gridCg = inventorySlotParent != null ? inventorySlotParent.GetComponent<CanvasGroup>() : null;
        if (gridCg != null) gridCg.alpha = 1f;
        UpdateTabVisual();
        RefreshOutputSlots();

        // 연료 슬롯 초기화
        var inv2 = playerInventory != null ? playerInventory : InventoryManager.Instance;
        fuelDropSlot?.Setup(machine, inv2);

        uiPanel.SetActive(true);
        IsAnyOpen = true;   // HUD 자동 페이드가 하단 시간바를 숨기게(패널 밑으로 비쳐 지저분)
        GameSfx.Play(SfxId.MachineOpen);
        FacilityWorldDisplay.SuppressWorldLabels = true;   // 월드 이름표/제작아이콘이 패널 블러 위로 뚫지 않게

        ShowFirstMachineHintIfNeeded();

        // [Gauge] 게이지 초기화 — 가공 시작 전 0%로 비우고 숨김
        if (processingGauge != null) processingGauge.StopAndHide();

        // 인벤토리에서 아이템을 집어든 순간 연료/재료 슬롯을 강조 (드랍 위치 안내)
        InventorySlotUI.OnAnySlotDragBegin -= OnInventoryDragBegin;
        InventorySlotUI.OnAnySlotDragBegin += OnInventoryDragBegin;
        InventorySlotUI.OnAnySlotDragEnd   -= OnInventoryDragEnd;
        InventorySlotUI.OnAnySlotDragEnd   += OnInventoryDragEnd;
    }

    // 헤더 타이틀 확대(종욱: 아이콘/글자 너무 작음). 빌더 값(아이콘36/글자24) 위에 런타임 덮어쓰기.
    private bool _headerTitleStyled;
    private void StyleHeaderTitle()
    {
        if (_headerTitleStyled) return;
        _headerTitleStyled = true;
        if (headerIconImage != null)
            headerIconImage.rectTransform.sizeDelta = new Vector2(48f, 48f);
        if (machineTitleText != null)
        {
            machineTitleText.fontSize = 30;
            var trt = machineTitleText.rectTransform;
            trt.sizeDelta = new Vector2(460f, 44f);
            trt.anchoredPosition = new Vector2(82f, 0f);   // 아이콘 48 + 간격 12 에 맞춰 우측으로
        }
        if (closeBtn != null)
        {
            // X 버튼 = 인벤/창고와 동일한 크기(54/48) + 호버 하이라이트로 통일.
            // (빌더가 박은 흐린 호버를 런타임서 인벤 값으로 덮음 = 빌더 재실행 불필요)
            var crt = (RectTransform)closeBtn.transform;
            crt.sizeDelta = new Vector2(54f, 54f);
            var xIcon = closeBtn.transform.Find("Icon") as RectTransform;
            if (xIcon != null) xIcon.sizeDelta = new Vector2(48f, 48f);
            CloseButtonStyle.Apply(closeBtn);
        }
    }

    // 중앙 도면 로드: Resources/Image/UI_Icon/FacilityBlueprint/ 에서 "{id}" 또는 "{id}_이름" 스프라이트.
    // 없으면 null(호출측이 퀵슬롯 모델로 폴백). 폴더 없으면 LoadAll 이 빈 배열 -> 안전.
    private static Sprite LoadFacilityBlueprint(int facilityId)
    {
        var all = Resources.LoadAll<Sprite>("Image/UI_Icon/FacilityBlueprint");
        if (all == null) return null;
        string id = facilityId.ToString();
        foreach (var s in all)
        {
            if (s == null) continue;
            if (s.name == id || s.name.StartsWith(id + "_")) return s;
        }
        return null;
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
        IsAnyOpen = false;
        GameSfx.Play(SfxId.MachineClose);
        FacilityWorldDisplay.SuppressWorldLabels = false;   // 패널 닫으면 월드 표시 복구
        if (_machine != null) _machine.OnBufferChanged -= OnBufferChanged;

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        if (inv != null) inv.OnInventoryChanged -= RefreshInventorySlots;
        var stoInv = InventoryManager.StorageInstance;
        if (stoInv != null) stoInv.OnInventoryChanged -= RefreshInventorySlots;

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
        _noFuelTimer = 0f; _fuelHintOn = false;   // 연료 힌트 상태 초기화(강조 자체는 위 ClearDropHighlights가 끔)

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

    // 씬 전환 등으로 Close() 없이 꺼질 때 열림 플래그가 남지 않게.
    private void OnDisable()
    {
        IsAnyOpen = false;
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
        // A안: 드래그 중엔 재배치를 미룬다(컴팩트 잔상 방지). 놓으면 Update 가 flush. 정확성은 B 스냅샷이 보장.
        if (InventoryDragHandler.Instance != null && InventoryDragHandler.Instance.IsDragging)
        {
            _invRefreshPending = true;
            return;
        }

        var inv = ActiveInv();
        int used = inv != null ? inv.GetUsedSlotCount() : 0;
        if (bagCapacityText != null && inv != null)
            bagCapacityText.text = $"용량 {used} / {inv.GetMaxSlots()}";
        if (bagEmptyText != null) bagEmptyText.gameObject.SetActive(used == 0);
        if (inv == null)
        {
            foreach (var slot in _invSlots)
                slot?.Refresh(null, null);
            return;
        }

        var slots = inv.GetSlots();

        // 창고 뷰:
        //   - 전체(필터 없음) = 칸 위치 고정(실제 slotIndex 그대로). 모든 칸 레이캐스트 유지(실칸 드롭).
        //   - 필터 = 매칭 아이템을 slotIndex 순서로 앞 칸부터 컴팩트("1번 칸부터가 직관적"). 매칭 칸은 실슬롯을
        //     그대로 물어 드래그가 올바른 실칸을 집는다(B 스냅샷 전제라 재바인딩 안전). 남는 칸은 빈 표시 +
        //     레이캐스트 통과 -> 드롭이 뒤 패널 드롭존으로 가서 출력/회수 처리(일반 드래그는 무해 취소).
        if (_showStorage)
        {
            bool filtered = _storageFilter != null;
            if (filtered)
            {
                _invFilterBuf.Clear();
                for (int i = 0; i < slots.Count; i++)
                {
                    var s = slots[i];
                    if (s == null || s.IsEmpty) continue;
                    var d = GameDataUtility.GetItem(s.itemId);
                    if (d != null && d.itemCategory == _storageFilter.Value) _invFilterBuf.Add(s);
                }
            }

            for (int i = 0; i < _invSlots.Count; i++)
            {
                if (_invSlots[i] == null) continue;
                if (!_invSlots[i].gameObject.activeSelf) _invSlots[i].gameObject.SetActive(true);

                InventorySlot disp;
                bool showItem;
                if (filtered)
                {
                    showItem = i < _invFilterBuf.Count;
                    disp = showItem ? _invFilterBuf[i] : new InventorySlot();
                }
                else
                {
                    disp = i < slots.Count ? slots[i] : null;
                    showItem = disp != null && !disp.IsEmpty;
                }
                _invSlots[i].Refresh(disp, inv);
                // 전체 뷰는 실칸이라 모든 칸 드롭 허용, 필터 패딩만 통과.
                SetInvCellRaycast(_invSlots[i], filtered ? showItem : true);
            }
            return;
        }

        for (int i = 0; i < _invSlots.Count; i++)
        {
            if (_invSlots[i] == null) continue;
            if (!_invSlots[i].gameObject.activeSelf) _invSlots[i].gameObject.SetActive(true);   // 필터로 꺼졌던 칸 복구
            InventorySlot slotData = i < slots.Count ? slots[i] : null;
            _invSlots[i].Refresh(slotData, inv);
            SetInvCellRaycast(_invSlots[i], true);   // 가방 뷰: 창고 필터에서 꺼졌던 레이캐스트 복구(칸 공유)
        }
    }

    // 인벤 칸 레이캐스트 차단 토글(컴팩트 패딩은 통과시켜 뒤 패널 드롭존이 받게). CanvasGroup 없으면 생성.
    private void SetInvCellRaycast(InventorySlotUI ui, bool blocks)
    {
        if (ui == null) return;
        var cg = ui.GetComponent<CanvasGroup>();
        if (cg == null) cg = ui.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = blocks;
    }

    // ── 가방/창고 탭 ─────────────────────────────────────────────
    // 기본 _showStorage=false -> 가방(player) 뷰. 창고 탭 누르면 StorageInstance 뷰(칸수=씬 maxSlots, 현재 100).
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
        GameSfx.Play(SfxId.MachineTabClick);
        BuildInventorySlots();   // 칸 수(가방35/창고50) 다를 수 있어 재구성 + 갱신
        UpdateTabVisual();
        PlayTabFade();
    }

    // 탭 전환 시 그리드 짧은 페이드(#17). timeScale=0 안전(unscaledDeltaTime).
    private void PlayTabFade()
    {
        if (inventorySlotParent == null) return;
        var cg = inventorySlotParent.GetComponent<CanvasGroup>();
        if (cg == null) cg = inventorySlotParent.gameObject.AddComponent<CanvasGroup>();
        if (_tabFadeCo != null) StopCoroutine(_tabFadeCo);
        _tabFadeCo = StartCoroutine(TabFadeRoutine(cg));
    }

    private System.Collections.IEnumerator TabFadeRoutine(CanvasGroup cg)
    {
        cg.alpha = 0.25f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.12f;
            cg.alpha = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(t));
            yield return null;
        }
        cg.alpha = 1f;
        _tabFadeCo = null;
    }

    private void UpdateTabVisual()
    {
        SetTabActive(bagTabBtn, !_showStorage);
        SetTabActive(storageTabBtn, _showStorage);
        StyleRailTab(_railBagBg, _railBagIcon, !_showStorage);
        StyleRailTab(_railStoBg, _railStoIcon, _showStorage);
        ApplySectionLayout();   // 펼침/접힘 재배치(가방 위/창고 아래 고정)
    }

    /// <summary>접힘 박스 드롭 성공 직후 호출: 다음 프레임에 그 섹션으로 자동 전환(엔필식).
    /// 드롭 이벤트 도중 그리드를 재구성하면 드래그 종료 처리와 얽혀서 한 프레임 미룬다.
    /// itemId 를 주면 창고 필터가 그 아이템을 숨길 때 해당 카테고리 탭으로 자동 전환(안 보이면 "사라짐"으로 오해).</summary>
    public void RequestViewSwitch(bool storage, int itemId = -1)
    {
        _pendingViewSwitch = storage ? 1 : 0;
        _pendingSwitchItemId = itemId;
    }
    private int _pendingSwitchItemId = -1;

    // 헤더 스타일 = 활성/비활성 2상태로 통일(자기완결 표면).
    // 옛 방식(옅은 알파 밴드 + 위치 기준 글자 명암)은 뒤 그라데이션이 비쳐서
    // 같은 상태인데 위(밝음)/아래(어두움) 위치 따라 완전 딴판으로 보였음 -> 밴드가 배경을 눌러버리는 진하기로 교체.
    private static void SetTabActive(Button b, bool on)
    {
        if (b == null || b.image == null) return;

        // 밴드: 어두운 쿨 유리. 활성이 더 진하고 또렷.
        b.image.color = on ? new Color(0.055f, 0.075f, 0.11f, 0.62f)
                           : new Color(0.055f, 0.075f, 0.11f, 0.42f);

        // 글자: 어두운 밴드 위 = 항상 밝은 글자. 활성 또렷 / 비활성 흐림. 크기는 둘 다 18 고정.
        // (true) 필수: 재오픈 시 OpenFor 가 패널 SetActive 전에 호출 - 비활성 계층에서 못 찾으면 글자만 stale.
        var tmp = b.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.fontSize = 18;
            tmp.color = on ? new Color(0.93f, 0.95f, 0.98f, 0.98f)
                           : new Color(0.78f, 0.82f, 0.88f, 0.60f);
        }

        // 하단 헤어라인도 상태 따라 강약.
        var line = b.transform.Find("HairLine");
        if (line != null)
        {
            var li = line.GetComponent<Image>();
            if (li != null) { var lc = li.color; lc.a = on ? 0.34f : 0.14f; li.color = lc; }
        }

        // 노란 활성 밑줄 폐기(종욱: 노란선 금지. 엔필은 무채 헤어라인만) - 상시 HairLine 이 담당.
        var underline = b.transform.Find("Underline");
        if (underline != null && underline.gameObject.activeSelf) underline.gameObject.SetActive(false);
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
        // 슬롯 = 레시피 기준(재료칸 = 재료 수, 결과칸 = 1). 포트 구조는 BuildFlowRails 가 따로 그린다.
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

        // 재료칸/결과칸 위치·세로간격을 코드에서 통제(매니폴드 상수와 일치). 그 다음 레일 재생성.
        AdjustSlotLayout();
        // 공정 흐름 레일(포트 -> 세로 버스 -> 슬롯) 재생성.
        BuildFlowRails();

        BuildFormula();
        ShowRecipeHintIfQuestActive();
    }

    // 설비 입력/출력 포트 수 = 실제 BuildPort 개수(3x3=3, 5x5=5). 시트 slotCount 아님(다를 수 있음).
    private int InputPortCount() => CountPorts(PortType.Input);

    private int CountPorts(PortType type)
    {
        if (_machine == null) return 1;
        int n = 0;
        var ports = _machine.GetComponentsInChildren<BuildPort>();
        foreach (var p in ports) if (p != null && p.portType == type) n++;
        return Mathf.Max(n, 1);
    }

    private int OutputPortCount() => CountPorts(PortType.Output);

    // ── 공정 흐름 레일: 설비 포트(N) -> 세로 버스 -> 재료/결과칸. 입력=회색/흰, 출력=파랑. ──
    //   버스/가로레일은 얇게(한 트레이스), 포트 단자만 굵게 강조. 포트별 실제 벨트연결 반영(연결=밝게 + 아이템 통과 순간 연출).
    // ── 재료칸/결과칸 레이아웃(코드에서 통제 = AdjustSlotLayout 이 실제 슬롯에 적용). 매니폴드가 여기서 파생돼 항상 맞음. ──
    //   ★조절 노브 두 개: FR_SlotAreaX(가로), FR_SlotVGap(세로). 나머지는 자동으로 따라옴.
    private const float FR_SlotAreaX = 125f;   // 재료칸/결과칸 중심 X(중앙에서 거리). 줄이면 입력-출력이 가로로 가까워짐.
    private const float FR_SlotVGap  = 32f;     // 재료칸 세로 간격(VLG spacing). 늘리면 재료칸끼리 세로로 벌어짐.
    // 슬롯 모서리 = 칸중심 ± 칸폭절반(140/2). 그 바깥으로 버스/포트가 튜닝 간격만큼.
    private const float FR_SlotEdgeX  = FR_SlotAreaX + 70f;         // 195
    // [가방 칼럼 620 확대 보정] 생산부가 좁아져 매니폴드를 40px 안쪽으로(벨트 RT 가 패널 밖으로 안 나가게).
    private const float FR_SlotBusGap = 65f;    // 슬롯<->버스 가로선 길이(레퍼런스처럼 슬롯쪽이 김)
    private const float FR_BusPortGap = 30f;    // 버스<->포트 가로선 길이(포트쪽 짧게)
    private const float FR_BusX  = FR_SlotEdgeX + FR_SlotBusGap;    // 280
    private const float FR_PortX = FR_BusX + FR_BusPortGap;         // 330
    // FR_PortPitch = 포트 세로 간격(포트단자 키우면 출력 3개서 안 붙게 넉넉히). FR_SlotPitch = 칸높이140 + 세로간격.
    private const float FR_PortPitch = 80f;
    private const float FR_SlotPitch = 140f + FR_SlotVGap;          // 172
    // ★레퍼런스(엔필)처럼 = 버스/가로레일은 다 얇은 회로선 하나로 이어지게, 포트 연결 세로선만 굵게 강조.
    //   이음새 끊김 해결 = 가로선을 세로선 속으로 한 두께 겹치게(MakeHRail) + 같은 색 통일 + 버스 끝 늘리기.
    private const float FR_RailThin = 4f;      // 버스/가로레일 = 얇은 회로선(가늘게, 하나로 이어지게)
    private const float FR_PortTickW = 8f;      // 포트 단자 = 유일하게 강조(굵은 세로선). 얇은 레일보다 확실히 굵게.
    private const float FR_PortTickH = 64f;     // 포트 단자 길이(연결지점 세로 강조) - 레일(벨트 RT)이 여기 맞춰 들어가므로 넉넉히
    private const float FR_PortWrapGap = 28f;   // 포트가 슬롯을 감쌀 때 슬롯 최외곽에서 더 벌리는 여유(포트단자가 슬롯과 안 겹치게)
    private const float FR_SlotDotOut = 8f;     // 슬롯쪽 레일 끝(=연결점)을 슬롯 모서리에서 버스쪽으로 뺀 거리(박스에 안 가리게)
    // 실제 게임 레일(RenderTexture) = 정사각 텍스처의 가운데 가로 스트립(band) + 나머지 투명.
    //   벨트를 포트단자(FR_PortTickH)에 자동으로 맞춘다: band 높이 = 포트단자 높이가 되게 표시크기를 역산.
    //   렌더러 프레이밍(fov28/fill0.82/3타일) 상 band 높이 = 0.273*표시크기, band 좌우여백 = 0.09*표시크기.
    private const float FR_BeltSizePerTick = 3.66f; // 표시크기 = 포트단자높이 / 0.273. 벨트가 세로선보다 크거나 작으면 이 값만 조정.
    private const float FR_BeltInnerFrac   = 0.41f; // 밀어낼 거리 = 표시크기 * (0.5-0.09). band 안쪽 끝이 포트에 딱(공백 남으면 이 값만 조정).
    private const float FR_BeltSize = FR_PortTickH * FR_BeltSizePerTick;  // ★포트단자 조정하면 벨트가 자동으로 이 크기에 맞음
    private const float FR_BeltOut  = FR_BeltSize * FR_BeltInnerFrac;
    // 유입/배출 순간 연출(엔필식): 계속 흐르는 루프가 아니라, 실제 아이템이 포트를 지나는 "순간"에 1회 발동.
    //   입력 = 아이콘 휙(밖->포트) -> 다 들어간 순간 경로 한번에 점등 -> 슬롯 점 반짝.
    //   출력 = 점 반짝 -> 경로 점등 -> 아이콘 휙(포트->밖).
    //   레일 그림은 디자인일 뿐, 아이콘 휙은 실제 레일 길이와 무관하게 고정 시간.
    private const float FR_PipeLightThick = 6f;   // 경로 점등 두께(얇은 레일보다 살짝 굵게)
    private const float FR_RailDim  = 0.45f;      // 평소 회로 밝기(어둡게 깔아 점등 대비 확보). 1 = 원래 밝기.
    private const float FX_IconTime = 0.8f;       // 아이템 PNG 휙 지나가는 시간(가공 5초라 여유 있게)
    private const float FX_PathOn   = 0.06f;      // 경로 점등 램프(사실상 한번에 탁)
    private const float FX_PathHold = 0.30f;      // 경로 점등 유지
    private const float FX_PathFade = 0.35f;      // 경로 페이드아웃
    private const float FX_DotTime  = 0.45f;      // 슬롯 점 반짝 지속
    private const float FX_Total    = 1.8f;       // 연출 전체 길이(지나면 상태 리셋)

    // 평소 회로 색(어둡게). 점등 오버레이/점 반짝과의 대비용.
    private static Color DimRail(Color c) => new Color(c.r * FR_RailDim, c.g * FR_RailDim, c.b * FR_RailDim, c.a);
    private static readonly Color FR_BusGray   = new Color(0.55f, 0.58f, 0.63f, 0.9f);  // 입력 버스(회색)
    private static readonly Color FR_RailWhite = new Color(0.82f, 0.93f, 1.0f, 1f);     // 입력 가로레일(밝은 시안화이트)
    private static readonly Color FR_Blue      = new Color(0.28f, 0.80f, 1.0f, 1f);     // 출력 = 밝은 시안(실제 벨트색, 버스/레일/단자)
    private static Sprite _frCircle;
    // 포트별 순간 연출 유닛: 경로 점등 조각(항상 풀사이즈, 알파로만 점등) + 아이템 아이콘 + 대상 슬롯 점.
    //   prevOcc = 포트 앞 벨트 칸 적재물 인스턴스(직전 프레임). 인스턴스 교체가 곧 발동 신호(같은 id 연속 통과도 감지).
    private class PipeSeg { public RectTransform rt; public Image img; }
    private class PortFx
    {
        public BuildPort port; public bool isInput; public Color baseColor;
        public readonly System.Collections.Generic.List<PipeSeg> segs = new();
        public Image dot; public RectTransform dotRt;
        public Image icon; public RectTransform iconRt; public Vector2 iconA, iconB;
        public RawImage rail;          // 이 포트의 실레일 RT(잼 시 빨간 틴트용)
        public Vector2 jamPos;         // 잼 아이템 아이콘 표시 위치(포트 앞 레일 끝)
        public object prevOcc;         // 직전 프레임 적재물 인스턴스
        public int prevId = -1;        // 그 인스턴스의 itemId
        public int jamId = -1;         // 잼(레시피 불일치 정체) 표시 중인 아이템 id. 스프라이트 재조회 방지
        public float t = -1f;          // 연출 경과 시간. -1 = 대기
        public bool rejected;          // 입력인데 레시피 불일치(못 받는) 아이템 = 빨강
    }
    private readonly System.Collections.Generic.List<PortFx> _portFx = new();
    // 연출 대기 데이터(BuildRailSide 포트 루프에서 수집 -> 슬롯 점까지 그린 뒤 마지막에 생성).
    private struct PendingFx { public BuildPort port; public Vector2[] pts; public int slotIndex; public Vector2 iconA, iconB; public RawImage rail; }
    private readonly System.Collections.Generic.List<TextMeshProUGUI> _centerChevrons = new();

    // 재료칸(InputArea)/결과칸(OutputArea) 실제 위치·세로간격을 코드에서 통제 -> 매니폴드 상수와 항상 일치(빌더 재실행 불필요).
    private void AdjustSlotLayout()
    {
        if (recipeDropSlots != null && recipeDropSlots.Length > 0 && recipeDropSlots[0] != null
            && recipeDropSlots[0].transform.parent is RectTransform ia)
        {
            var p = ia.anchoredPosition; p.x = -FR_SlotAreaX; ia.anchoredPosition = p;
            var vlg = ia.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (vlg != null) vlg.spacing = FR_SlotVGap;
        }
        if (outputSlot != null && outputSlot.transform.parent is RectTransform oa)
        {
            var p = oa.anchoredPosition; p.x = FR_SlotAreaX; oa.anchoredPosition = p;
        }
    }

    private void BuildFlowRails()
    {
        if (flowRailsRoot == null || _machine == null) return;
        for (int i = flowRailsRoot.childCount - 1; i >= 0; i--)
            Destroy(flowRailsRoot.GetChild(i).gameObject);
        _portFx.Clear();
        _centerChevrons.Clear();

        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0) return;
        int ri = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
        var recipe = recipes[ri];
        int inSlots  = recipe != null && recipe.inputs  != null ? recipe.inputs.Length : 0;
        int outSlots = recipe != null && recipe.outputs != null && recipe.outputs.Length > 0 ? 1 : 0;

        // 한 색 회로 하나로: 입력=흰(시안화이트) 전부, 출력=파랑 전부. 포트단자만 굵기로 강조.
        BuildRailSide(true,  InputPortCount(),  inSlots,  FR_RailWhite);
        BuildRailSide(false, OutputPortCount(), outSlots, FR_Blue);

        // 중앙 흐름 화살표 2개(재료->결과). 회색 기본 + 가동 시 흰 펄스(UpdateFlowRails 가 좌->우로 칠함).
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject("FlowChevron", typeof(RectTransform));
            go.transform.SetParent(flowRailsRoot, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30f, 38f); rt.anchoredPosition = new Vector2(-9f + i * 17f, 0f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = ">"; tmp.fontSize = 30; tmp.fontStyle = FontStyles.Bold;
            tmp.color = FR_BusGray; tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
            _centerChevrons.Add(tmp);
        }
    }

    // 실제 레일 프리팹 -> RenderTexture (1회 렌더 후 캐시, 카메라가 매프레임 흐름 갱신).
    private Texture EnsureRailTexture()
    {
        if (_railTex != null) return _railTex;
        var rbm = FindFirstObjectByType<RailBuildManager>();
        if (rbm == null || rbm.StraightRailPrefab == null) return null;
        if (_railPortrait == null)
        {
            var go = new GameObject("RailPortrait");
            go.transform.SetParent(transform, false);
            _railPortrait = go.AddComponent<RailPortraitRenderer>();
        }
        _railTex = _railPortrait.Render(rbm.StraightRailPrefab, 256, 256);
        return _railTex;
    }

    // 연결된 포트 바깥쪽에 실제 게임 레일(RenderTexture)을 얹는다.
    //   정사각 텍스처 = 가운데 가로 레일 + 나머지 투명 -> 정사각 RawImage 로 얹으면 왜곡/크롭 없이 가로 레일만 보인다.
    private RawImage MakeRailStrip(Vector2 pos)
    {
        var tex = EnsureRailTexture();
        if (tex == null) return null;
        var go = new GameObject("PortRailReal", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(flowRailsRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(FR_BeltSize, FR_BeltSize);
        rt.anchoredPosition = pos;
        var raw = go.GetComponent<RawImage>();
        raw.texture = tex; raw.raycastTarget = false;
        return raw;   // 잼 시 빨간 틴트용 참조
    }

    // 한쪽(입력/출력) 레일 생성. isInput=true 면 왼쪽(-), false 면 오른쪽(+).
    // 버스/가로레일/포트단자 전부 같은 색(하나의 회로) + 조각을 서로 겹쳐 이음새 제거. 포트 단자만 굵게 강조.
    private void BuildRailSide(bool isInput, int nPorts, int slotCount, Color baseColor)
    {
        if (nPorts <= 0) return;
        float sign = isInput ? -1f : 1f;
        float portX = sign * FR_PortX, busX = sign * FR_BusX, slotEdge = sign * FR_SlotEdgeX;
        var ports = PortsOfType(isInput ? PortType.Input : PortType.Output);   // 틱 순서 = 이 배열 순서(실제 포트)

        float slotTop = (slotCount - 1) * 0.5f * FR_SlotPitch;
        float portNatural = (nPorts - 1) * 0.5f * FR_PortPitch;
        // 포트가 슬롯을 위아래로 감싸게(바깥) -> 슬롯은 안쪽에서 엇갈림, 버스 끝은 최외곽 포트에 딱(맨살 스텁 없음).
        //   슬롯이 있으면 포트 span 을 슬롯+gap 이상으로 벌린다. 슬롯이 좁거나 없으면(출력=슬롯1개 중앙) 포트는 자연 간격.
        float portHalf  = slotTop > 0f ? Mathf.Max(portNatural, slotTop + FR_PortWrapGap) : portNatural;
        float busHalf   = Mathf.Max(portHalf, 1f);
        float portTop   = nPorts > 1 ? portHalf : 0f;                      // 포트 1개면 중앙
        float portPitch = nPorts > 1 ? (portHalf * 2f) / (nPorts - 1) : 0f;
        // 평소 회로는 어둡게 깔고, 아이템 통과 순간 점등(PathLight)이 원색+흰빛으로 켜져 대비가 생긴다.
        Color dimBase = DimRail(baseColor);
        MakeQuad("Bus", new Vector2(busX, 0f), new Vector2(FR_RailThin, busHalf * 2f), dimBase);

        // 연결 포트별 연출 대기 데이터. 실제 생성은 슬롯 레일/점까지 다 그린 뒤(연출이 맨 위에 오도록).
        var pending = new System.Collections.Generic.List<PendingFx>();

        // 포트 단자(굵게 강조) + 포트->버스 가로레일. 연결된 포트 = 밝게 + 실제 레일 + 아이템 통과 순간 연출.
        for (int j = 0; j < nPorts; j++)
        {
            float py = portTop - j * portPitch;
            BuildPort port = j < ports.Length ? ports[j] : null;
            bool c = port != null && BeltSegment.IsPortConnected(port);
            // 연결 포트만 어두운 기본색에서 원색 쪽으로 살짝 밝게(구분), 점등만큼 밝진 않게.
            Color tickCol = c ? Color.Lerp(dimBase, baseColor, 0.55f) : dimBase;
            Color railCol = c ? Color.Lerp(dimBase, baseColor, 0.30f) : dimBase;
            // 연결된 포트 = 실제 게임 레일(RenderTexture)을 "먼저"(맨 뒤에) 깐다 -> 포트단자/가로선이 그 위를 덮어 겹침 색 얼룩(블렌딩) 방지.
            //   입력=왼쪽 바깥, 출력=오른쪽 바깥. RT 는 오른쪽 흐름(railYaw270) = 입력 유입 / 출력 배출 둘 다 맞음.
            RawImage railRaw = null;
            if (c)
                railRaw = MakeRailStrip(new Vector2(portX + sign * FR_BeltOut, py));
            MakeHRail("PortRail", portX, busX, py, railCol);                                                 // 포트<->버스 가로선(벨트 위)
            MakeQuad("Port", new Vector2(portX, py), new Vector2(FR_PortTickW, FR_PortTickH), tickCol);      // 유일한 강조(굵은 세로, 벨트 위에 덮여 얼룩 가림)
            if (c)
            {
                // 연출 경로 = 시작점->끝점 전체. 입력 = 포트 -> 버스 -> 가장 가까운 재료칸, 출력 = 결과칸 -> 버스 -> 포트.
                // 아이콘 휙 구간 = 벨트(바깥) <-> 포트. 발동은 포트 앞 칸 폴링(UpdateFlowRails)이 담당.
                Vector2 pPort = new Vector2(portX, py), pBusP = new Vector2(busX, py);
                float outerX = portX + sign * 2f * FR_BeltOut;
                Vector2[] pts; int slotIdx = -1;
                if (slotCount > 0)
                {
                    int k = 0; float best = float.MaxValue;
                    for (int q = 0; q < slotCount; q++)
                    {
                        float qy = slotTop - q * FR_SlotPitch;
                        float dd = Mathf.Abs(qy - py);
                        if (dd < best) { best = dd; k = q; }
                    }
                    float sy = slotTop - k * FR_SlotPitch;
                    Vector2 pBusS = new Vector2(busX, sy);
                    Vector2 pSlot = new Vector2(slotEdge + sign * FR_SlotDotOut, sy);
                    pts = isInput ? new[] { pPort, pBusP, pBusS, pSlot } : new[] { pSlot, pBusS, pBusP, pPort };
                    slotIdx = k;
                }
                else
                    pts = isInput ? new[] { pPort, pBusP } : new[] { pBusP, pPort };
                pending.Add(new PendingFx
                {
                    port = port, pts = pts, slotIndex = slotIdx,
                    iconA = isInput ? new Vector2(outerX, py) : new Vector2(portX, py),
                    iconB = isInput ? new Vector2(portX, py) : new Vector2(outerX, py),
                    rail = railRaw,
                });
            }
        }

        // 버스->슬롯 가로레일 + 재료칸쪽 연결점. 점은 연출(반짝)에 쓰이므로 참조를 남긴다.
        var dots = new Image[Mathf.Max(slotCount, 1)];
        for (int k = 0; k < slotCount; k++)
        {
            float sy = slotTop - k * FR_SlotPitch;
            // 레일 끝(=연결점)을 슬롯 박스 밖(버스쪽)으로 빼서 박스에 안 가리게. 점은 항상 레일 끝에.
            float slotEnd = slotEdge + sign * FR_SlotDotOut;
            MakeHRail("SlotRail", slotEnd, busX, sy, dimBase);
            dots[k] = MakeDot(new Vector2(slotEnd, sy), 9f, dimBase);
        }

        // 연출 유닛은 레일/점 위에 그려져야 하므로 마지막에 생성(형제 순서 = 그리기 순서).
        foreach (var p in pending)
            BuildPortFx(p, isInput, baseColor,
                p.slotIndex >= 0 && p.slotIndex < dots.Length ? dots[p.slotIndex] : null);
    }

    // 두 X 지점(바깥끝, 버스중심)을 정확히 잇는다. 늘리지 않음.
    //   버스 쪽 끝이 버스 중심(xB=busX)에서 딱 끝나 = 버스 절반과 겹쳐 틈 없이 붙되, 반대쪽으로 삐져나오는 스텁이 안 생긴다.
    //   바깥끝(포트단자/슬롯점)은 그 위에 덮이는 단자/동그라미가 가려줘서 별도 겹침 불필요.
    private void MakeHRail(string name, float xOuter, float xBus, float y, Color color)
    {
        float lo = Mathf.Min(xOuter, xBus);
        float hi = Mathf.Max(xOuter, xBus);
        MakeQuad(name, new Vector2((lo + hi) * 0.5f, y), new Vector2(hi - lo, FR_RailThin), color);
    }

    private void MakeQuad(string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(flowRailsRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
    }

    private Image MakeDot(Vector2 pos, float d, Color color)
    {
        var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(flowRailsRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(d, d); rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>(); img.sprite = CircleSprite(); img.color = color; img.raycastTarget = false;
        return img;
    }

    // 런타임 생성 원형 스프라이트(빌트인 Knob 리소스가 런타임에 안 잡혀서 직접 생성). 흰색 + 1px 소프트 엣지, Image.color 로 틴트.
    private static Sprite CircleSprite()
    {
        if (_frCircle != null) return _frCircle;
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        float r = S * 0.5f - 1f, c = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.Clamp01(r - dist + 0.5f) * 255f);
                px[y * S + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px); tex.Apply();
        _frCircle = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        return _frCircle;
    }

    // 연결 포트 하나의 연출 유닛 생성: 경로 점등 조각(풀사이즈, 알파 0) + 아이템 아이콘(숨김) + 슬롯 점 참조.
    private void BuildPortFx(PendingFx pf, bool isInput, Color baseColor, Image dot)
    {
        // fx.baseColor = 점 반짝 끝나고 돌아갈 평소(어두운) 색. 점등(lit)은 원색 기준 밝은 색.
        var fx = new PortFx { port = pf.port, isInput = isInput, baseColor = DimRail(baseColor), dot = dot };
        Color lit = Color.Lerp(baseColor, Color.white, 0.85f);
        for (int i = 0; i < pf.pts.Length - 1; i++)
        {
            Vector2 a = pf.pts[i], b = pf.pts[i + 1];
            float len = Vector2.Distance(a, b);
            if (len < 0.5f) continue;   // 포트와 슬롯 높이가 같으면 버스 구간 길이 0 -> 생략
            var go = new GameObject("PathLight", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(flowRailsRoot, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (a + b) * 0.5f;
            bool horiz = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.y - a.y);
            rt.sizeDelta = horiz ? new Vector2(len, FR_PipeLightThick) : new Vector2(FR_PipeLightThick, len);
            var img = go.GetComponent<Image>();
            img.color = new Color(lit.r, lit.g, lit.b, 0f);
            img.raycastTarget = false; img.enabled = false;
            fx.segs.Add(new PipeSeg { rt = rt, img = img });
        }
        // 아이템 아이콘: 발동 순간에만 벨트 위를 휙 지나간다.
        var igo = new GameObject("BeltItem", typeof(RectTransform), typeof(Image));
        igo.transform.SetParent(flowRailsRoot, false);
        var irt = igo.GetComponent<RectTransform>();
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
        float d = FR_PortTickH * 0.9f;
        irt.sizeDelta = new Vector2(d, d);
        var iimg = igo.GetComponent<Image>();
        iimg.preserveAspect = true; iimg.raycastTarget = false; iimg.enabled = false;
        fx.icon = iimg; fx.iconRt = irt; fx.iconA = pf.iconA; fx.iconB = pf.iconB;
        fx.rail = pf.rail;
        // 잼 아이템 표시 위치 = 포트 앞 레일 끝(중앙 아님 - 실제로 막혀 서 있는 자리).
        Vector2 mid = (pf.iconA + pf.iconB) * 0.5f;
        Vector2 toPort = (isInput ? pf.iconB - pf.iconA : pf.iconA - pf.iconB).normalized;
        fx.jamPos = mid + toPort * (FR_BeltSize * 0.30f);

        // 점은 연출이 위에서 반짝여야 하므로 맨 위로(여러 포트가 같은 슬롯 점을 공유해도 무해).
        if (dot != null) { fx.dotRt = dot.rectTransform; dot.transform.SetAsLastSibling(); }
        // 창을 열 때 이미 포트 앞에 놓여 있던 아이템로 오발동하지 않게 현재 상태로 초기화.
        fx.prevOcc = BeltSegment.PortFrontOccupant(pf.port, out fx.prevId);
        _portFx.Add(fx);
    }

    // 설비의 해당 타입 포트들(단자 그리는 순서 = 이 배열 순서). 연결 판정/유입아이템은 각 포트로 BeltSegment 에 질의.
    private BuildPort[] PortsOfType(PortType type)
    {
        var list = new System.Collections.Generic.List<BuildPort>();
        if (_machine != null)
            foreach (var p in _machine.GetComponentsInChildren<BuildPort>())
                if (p != null && p.portType == type) list.Add(p);
        return list.ToArray();
    }

    // ── 현재 생산 공식 스트립 ─────────────────────────────────────
    // 하단 작은 패널에 [재료 아이콘 > 결과 아이콘] 을 채운다(엔필식 요약).
    // 중앙 슬롯은 상호작용용, 이건 "지금 뭘 만드는지" 요약 표시.

    private void BuildFormula()
    {
        if (formulaContent == null || _machine == null) return;

        for (int i = formulaContent.childCount - 1; i >= 0; i--)
            Destroy(formulaContent.GetChild(i).gameObject);

        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0) return;
        int ri = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
        var recipe = recipes[ri];
        if (recipe == null) return;

        if (recipe.inputs != null)
            foreach (var inp in recipe.inputs)
                MakeFormulaEntry(inp.itemId, inp.amount, false);

        MakeFormulaArrow();

        if (recipe.outputs != null)
            foreach (var outp in recipe.outputs)
                MakeFormulaEntry(outp.itemId, outp.amount, true);
    }

    private void MakeFormulaEntry(int itemId, int amount, bool isOutput)
    {
        var go = new GameObject(isOutput ? "FxOut" : "FxIn", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(formulaContent, false);
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 40; le.preferredHeight = 40;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var irt = iconGo.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
        var img = iconGo.GetComponent<Image>(); img.preserveAspect = true; img.raycastTarget = false;
        var itemData = GameDataUtility.GetItem(itemId);
        img.sprite = itemData != null ? ItemDatabase.GetIcon(itemData.iconKey) : null;
        img.enabled = img.sprite != null;

        var amtGo = new GameObject("Amt", typeof(RectTransform));
        amtGo.transform.SetParent(go.transform, false);
        var amt = amtGo.AddComponent<TextMeshProUGUI>();
        amt.text = "x" + amount; amt.fontSize = 13; amt.color = Color.white;
        amt.alignment = TextAlignmentOptions.BottomRight; amt.fontStyle = FontStyles.Bold;
        amt.raycastTarget = false; amt.textWrappingMode = TextWrappingModes.NoWrap;
        var art = amt.rectTransform;
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = Vector2.zero; art.offsetMax = new Vector2(2, 0);
    }

    private void MakeFormulaArrow()
    {
        var go = new GameObject("Arrow", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(formulaContent, false);
        var le = go.GetComponent<LayoutElement>(); le.preferredWidth = 22; le.preferredHeight = 40;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = ">"; tmp.fontSize = 22; tmp.color = new Color(0.90f, 0.76f, 0.29f, 1f);
        tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false; tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void RefreshRecipeSelectionUI(int totalCount)
    {
        bool multiRecipe = totalCount > 1;

        // 화살표/카운터("< 1/7 >") 폐기 -> 결과물 아이콘 칩 줄에서 직접 선택(종욱 지시).
        if (recipePrevBtn != null) recipePrevBtn.gameObject.SetActive(false);
        if (recipeNextBtn != null) recipeNextBtn.gameObject.SetActive(false);
        if (recipeIndexText != null) recipeIndexText.gameObject.SetActive(false);

        if (recipeNameText != null && _machine != null && _machine.Recipes != null
            && _selectedRecipeIndex < _machine.Recipes.Count)
        {
            var recipe = _machine.Recipes[_selectedRecipeIndex];
            recipeNameText.text = !string.IsNullOrEmpty(recipe.recipeName) ? recipe.recipeName : "";
        }

        RefreshRecipeChips(multiRecipe);
    }

    // ── 레시피 칩: 이 설비가 만들 수 있는 결과물 아이콘을 한 줄로 전부 노출, 클릭 = 그 레시피 표시. ──
    //   칩은 옛 화살표와 같은 "탐색용" - 생산 레시피 고정(SetLockedRecipe)은 재료 투입 시 결정(기존 시맨틱 유지).
    private RectTransform _chipsRoot;
    private readonly System.Collections.Generic.List<Image> _chipBgs = new System.Collections.Generic.List<Image>();
    private readonly System.Collections.Generic.List<Image> _chipIcons = new System.Collections.Generic.List<Image>();
    private readonly System.Collections.Generic.List<TextMeshProUGUI> _chipNums = new System.Collections.Generic.List<TextMeshProUGUI>();   // 아이콘 없는 칩의 번호 폴백(없으면 null)
    private ProcessingMachine _chipsMachine;

    private void RefreshRecipeChips(bool show)
    {
        // 칩 부모 = 빌더의 RecipeNav(화살표 부모). 화살표 ref 가 없는 옛 프리팹이면 칩 생략.
        Transform nav = recipePrevBtn != null ? recipePrevBtn.transform.parent : null;
        if (nav == null) return;

        var recipes = _machine != null ? _machine.Recipes : null;
        int n = recipes != null ? recipes.Count : 0;
        bool rebuild = _chipsRoot == null || _chipsMachine != _machine || _chipBgs.Count != n;

        if (rebuild)
        {
            if (_chipsRoot != null) Destroy(_chipsRoot.gameObject);
            _chipBgs.Clear(); _chipIcons.Clear(); _chipNums.Clear();
            _chipsMachine = _machine;

            var rootGo = new GameObject("RecipeChips", typeof(RectTransform));
            rootGo.transform.SetParent(nav, false);
            _chipsRoot = (RectTransform)rootGo.transform;
            _chipsRoot.anchorMin = _chipsRoot.anchorMax = _chipsRoot.pivot = new Vector2(0.5f, 0.5f);
            _chipsRoot.sizeDelta = Vector2.zero;
            _chipsRoot.anchoredPosition = Vector2.zero;
            // 이름 라벨(RecipeNavCenter, 뒤 형제)이 받침판 위에 그려지게 칩 루트를 맨 앞 형제로.
            _chipsRoot.SetSiblingIndex(0);

            // 받침판/바 없이 칩 자체를 큼직하게(종욱: 검정패널 삭제 + 칩 확대). 슬롯 140 과 어울리는 체급.
            const float chipS = 80f, gap = 14f, chipY = -22f;
            float total = n * chipS + (n - 1) * gap;
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Chip" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_chipsRoot, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(chipS, chipS);
                rt.anchoredPosition = new Vector2(-total * 0.5f + chipS * 0.5f + i * (chipS + gap), chipY);

                var bg = go.GetComponent<Image>();
                bg.sprite = UISpriteFactory.RoundedRect(64, 10);
                bg.type = Image.Type.Sliced;

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var irt = (RectTransform)iconGo.transform;
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
                irt.offsetMin = new Vector2(6f, 6f); irt.offsetMax = new Vector2(-6f, -6f);   // 아이콘 85% (전 슬롯 공통 비율)
                var icon = iconGo.GetComponent<Image>();
                icon.preserveAspect = true; icon.raycastTarget = false;

                Sprite spr = null;
                var outs = recipes[i]?.outputs;
                if (outs != null && outs.Length > 0)
                {
                    var item = GameDataUtility.GetItem(outs[0].itemId);
                    if (item != null) spr = ItemDatabase.GetIcon(item.iconKey);
                }
                icon.sprite = spr;
                icon.enabled = spr != null;
                TextMeshProUGUI num = null;
                if (spr == null)
                {
                    // 아이콘 없는 레시피 폴백 = 번호 표시(색은 하이라이트 루프가 상태별 관리)
                    num = new GameObject("Num", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                    num.transform.SetParent(go.transform, false);
                    var nrt = num.rectTransform;
                    nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
                    nrt.offsetMin = Vector2.zero; nrt.offsetMax = Vector2.zero;
                    if (recipeNameText != null) num.font = recipeNameText.font;
                    num.fontSize = 28; num.fontStyle = FontStyles.Bold;
                    num.alignment = TextAlignmentOptions.Center;
                    num.text = (i + 1).ToString();
                    num.raycastTarget = false;
                }

                var btn = go.GetComponent<Button>();
                btn.targetGraphic = bg;
                var cb = btn.colors;
                cb.normalColor = Color.white; cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
                cb.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f); cb.selectedColor = Color.white;
                cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
                btn.colors = cb;
                int idx = i;
                btn.onClick.AddListener(() =>
                {
                    if (_selectedRecipeIndex == idx) return;
                    _selectedRecipeIndex = idx;
                    GameSfx.Play(SfxId.CodexTabClick);   // 레시피 전환음(도감 상단 탭과 동일한 위젯 전환음)
                    BuildRecipeSlots();
                    RefreshOutputSlots();
                });

                _chipBgs.Add(bg); _chipIcons.Add(icon); _chipNums.Add(num);
            }
        }

        _chipsRoot.gameObject.SetActive(show);

        // 이름 라벨(RecipeNavCenter)은 칩이 보일 때만 칩 줄 아래로 내림. 단일 레시피(칩 없음)면 원위치.
        if (recipeNameText != null)
        {
            var center = recipeNameText.transform.parent as RectTransform;
            if (center != null && center.parent == nav)
                center.anchoredPosition = new Vector2(0f, show ? -84f : 0f);
            // 받침판 삭제됨 - 밝은 유리 상단 위라 항상 어두운 글자(바로 밑 표면 기준 원칙).
            recipeNameText.color = new Color(0.137f, 0.165f, 0.20f, 1f);
            recipeNameText.fontSize = show ? 22 : 18;   // 칩 확대에 맞춰 이름도 업
        }

        // 활성 = 밝은 면(좌측 레일 탭과 같은 언어) / 비활성 = 어두운 유리 + 흐린 아이콘.
        for (int i = 0; i < _chipBgs.Count; i++)
        {
            bool on = i == _selectedRecipeIndex;
            if (_chipBgs[i] != null)
                _chipBgs[i].color = on ? new Color(0.92f, 0.95f, 0.98f, 0.88f)
                                       : new Color(0.055f, 0.075f, 0.11f, 0.42f);
            if (_chipIcons[i] != null)
                _chipIcons[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            // 번호 폴백: 활성 = 밝은 면 위 어두운 숫자(레일 탭 아이콘과 동일 원리) / 비활성 = 흐린 밝은 숫자.
            if (_chipNums[i] != null)
                _chipNums[i].color = on ? new Color(0.16f, 0.20f, 0.26f, 0.95f)
                                        : new Color(1f, 1f, 1f, 0.40f);
        }
    }

    // ── 출력 슬롯 ───────────────────────────────────────────────

    private void RefreshOutputSlots()
    {
        if (outputSlot == null || _machine == null) return;

        // 출력 버퍼 총량 -> 모두받기 dim(#34) + 출력 펄스(#40) 공통 판정.
        int totalOut = 0;
        var rcs = _machine.Recipes;
        if (rcs != null && rcs.Count > 0)
        {
            int ri = Mathf.Clamp(_selectedRecipeIndex, 0, rcs.Count - 1);
            var outs = rcs[ri]?.outputs;
            if (outs != null)
                foreach (var o in outs) totalOut += _machine.OutputBuffer.GetAmount(o.itemId);
        }
        _hasOutput = totalOut > 0;
        if (takeOutputBtn != null) takeOutputBtn.interactable = _hasOutput;

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
            StorageInflowNotice.SuppressBriefly();   // 자체 토스트가 있으니 공용 알림 중복 방지
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

    // 흐름 연출: 실제 아이템이 포트를 지나는 "순간"에만 1회 발동(포트 앞 칸 폴링). 중앙 화살표만 가동 시 펄스.
    private void UpdateFlowRails(bool operating)
    {
        for (int i = 0; i < _portFx.Count; i++)
        {
            var fx = _portFx[i];
            if (fx.icon == null || fx.iconRt == null) continue;

            // 발동 감지(인스턴스 교체 기준 - 배출이 밀려 칸이 비는 프레임 없이 즉시 다시 차는 경우도 놓치지 않게):
            //   입력 = 있던 인스턴스가 사라지거나 교체됨(이전 아이템이 설비로 들어간 순간)
            //   출력 = 새 인스턴스가 나타남(설비가 방금 내보낸 순간, 같은 id 연속 배출 포함)
            object curOcc = BeltSegment.PortFrontOccupant(fx.port, out int curId);
            bool fire; int fireId;
            if (fx.isInput) { fire = fx.prevOcc != null && !ReferenceEquals(curOcc, fx.prevOcc); fireId = fx.prevId; }
            else            { fire = curOcc != null && !ReferenceEquals(curOcc, fx.prevOcc);      fireId = curId; }
            fx.prevOcc = curOcc; fx.prevId = curId;
            // 아이템 실린 벨트가 "철거"돼 칸이 사라진 경우를 입고로 오인하지 않게 - 정상 입고는 항상 연결 상태.
            if (fire && !BeltSegment.IsPortConnected(fx.port)) fire = false;
            if (fire && fireId >= 0)
            {
                var itemData = GameDataUtility.GetItem(fireId);
                fx.icon.sprite = itemData != null ? ItemDatabase.GetIcon(itemData.iconKey) : null;
                fx.rejected = fx.isInput && _machine != null && !_machine.CanReceive(fireId);
                fx.jamId = -1;
                // 잼 표시 중이었다면(레시피 변경 등으로 방금 해소) 레일 원복.
                if (fx.rail != null) fx.rail.color = Color.white;
                fx.t = 0f;   // 연출 시작(진행 중이었으면 새 아이템로 재시작)
                // 재시작 시 이전 연출 잔상(점 확대/흰빛) 즉시 원복 - 새 펄스 시작 전까지 멈춘 채 남는 팝 방지.
                if (fx.dot != null && fx.dotRt != null) { fx.dotRt.localScale = Vector3.one; fx.dot.color = fx.baseColor; }
            }
            if (fx.t < 0f)
            {
                // 대기 중 잼 표시: 레시피 불일치 아이템이 입력 포트 앞에 멈춰 서 있으면 빨간 아이콘 정지 표시(깜빡).
                if (fx.isInput && curOcc != null && _machine != null && !_machine.CanReceive(curId))
                {
                    if (fx.jamId != curId)
                    {
                        fx.jamId = curId;
                        var jd = GameDataUtility.GetItem(curId);
                        fx.icon.sprite = jd != null ? ItemDatabase.GetIcon(jd.iconKey) : null;
                    }
                    if (fx.icon.sprite != null)
                    {
                        if (!fx.icon.enabled) fx.icon.enabled = true;
                        fx.iconRt.anchoredPosition = fx.jamPos;   // 포트 앞 레일 끝(막혀 서 있는 자리)
                        float blink = 0.55f + 0.35f * Mathf.PingPong(Time.unscaledTime * 1.6f, 1f);
                        fx.icon.color = new Color(1f, 0.32f, 0.28f, blink);
                    }
                    // 레일도 빨갛게(월드 벨트의 잼 표시와 동일 언어 - 종욱)
                    if (fx.rail != null) fx.rail.color = new Color(1f, 0.40f, 0.36f, 1f);
                }
                // 연결 즉시 예측(월드 레일과 동일 연동): 아이템이 없어도 이 포트 연결이 잘못됐으면(산출물 못 받음) 레일 빨강.
                else if (BeltSegment.IsPortConnectionIncompatible(fx.port))
                {
                    fx.jamId = -1;
                    if (fx.icon.enabled) fx.icon.enabled = false;
                    if (fx.rail != null) fx.rail.color = new Color(1f, 0.40f, 0.36f, 1f);
                }
                else if (fx.icon.enabled || fx.jamId != -1)
                {
                    fx.jamId = -1;
                    if (fx.icon.enabled) fx.icon.enabled = false;
                    if (fx.rail != null) fx.rail.color = Color.white;
                }
                continue;
            }
            fx.t += Time.unscaledDeltaTime;
            float t = fx.t;

            // 순서(엔필식): 입력 = 아이콘이 레일을 다 지나간 "순간" 경로 한번에 점등 -> 슬롯 점 반짝.
            //             출력 = 점 반짝 -> 경로 점등 -> 아이콘 휙(밖으로).
            float iconStart = fx.isInput ? 0f : 0.25f;
            float pathStart = fx.isInput ? FX_IconTime : 0.12f;
            float dotStart  = fx.isInput ? FX_IconTime + 0.15f : 0f;

            // 아이템 PNG 휙(레일 길이 무관 고정 시간, 양끝 페이드). 못 받는 아이템 = 빨강.
            float iu = (t - iconStart) / FX_IconTime;
            if (iu >= 0f && iu <= 1f && fx.icon.sprite != null)
            {
                if (!fx.icon.enabled) fx.icon.enabled = true;
                fx.iconRt.anchoredPosition = Vector2.Lerp(fx.iconA, fx.iconB, iu);
                Color ic = fx.rejected ? new Color(1f, 0.32f, 0.28f) : Color.white;
                ic.a = Mathf.Sin(iu * Mathf.PI);
                fx.icon.color = ic;
            }
            else if (fx.icon.enabled) fx.icon.enabled = false;

            // 경로 전체 점등: 한번에 탁 켜지고 -> 유지 -> 페이드아웃.
            float pt = t - pathStart, pa = 0f;
            if (pt >= 0f)
            {
                if (pt < FX_PathOn) pa = pt / FX_PathOn;
                else if (pt < FX_PathOn + FX_PathHold) pa = 1f;
                else pa = 1f - Mathf.Clamp01((pt - FX_PathOn - FX_PathHold) / FX_PathFade);
            }
            for (int s = 0; s < fx.segs.Count; s++)
            {
                var seg = fx.segs[s];
                if (seg.img == null) continue;
                bool on = pa > 0.01f;
                if (seg.img.enabled != on) seg.img.enabled = on;
                if (on) { var c = seg.img.color; c.a = 0.95f * pa; seg.img.color = c; }
            }

            // 슬롯 연결점 반짝(스케일 + 흰빛 펄스 후 원상복귀).
            if (fx.dot != null && fx.dotRt != null)
            {
                float du = (t - dotStart) / FX_DotTime;
                if (du >= 0f && du <= 1f)
                {
                    float pulse = Mathf.Sin(du * Mathf.PI);
                    fx.dotRt.localScale = Vector3.one * (1f + 0.9f * pulse);
                    fx.dot.color = Color.Lerp(fx.baseColor, Color.white, pulse);
                }
                else if (du > 1f)
                {
                    fx.dotRt.localScale = Vector3.one;
                    fx.dot.color = fx.baseColor;
                }
            }

            if (t > FX_Total)
            {
                fx.t = -1f;
                if (fx.icon.enabled) fx.icon.enabled = false;
            }
        }
        // 중앙 화살표 2개: 가동 시 흰 펄스가 좌->우로 지나감(위상차), 미가동 = 회색.
        for (int i = 0; i < _centerChevrons.Count; i++)
        {
            var ch = _centerChevrons[i];
            if (ch == null) continue;
            float w = operating ? Mathf.Clamp01(Mathf.Sin(Time.unscaledTime * 3.2f - i * 0.7f) * 0.5f + 0.5f) : 0f;
            ch.color = Color.Lerp(FR_BusGray, Color.white, w);
        }
    }

    private void Update()
    {
        if (_machine == null || !uiPanel.activeSelf) return;

        // A안 flush: 드래그가 끝났는데 미뤄둔 인벤 갱신이 있으면 반영.
        if (_invRefreshPending && (InventoryDragHandler.Instance == null || !InventoryDragHandler.Instance.IsDragging))
        {
            _invRefreshPending = false;
            RefreshInventorySlots();
        }

        // 접힘 박스 드롭 성공 -> 다음 프레임에 그 섹션 자동 펼침(엔필식 컬럼 전환).
        if (_pendingViewSwitch >= 0)
        {
            bool toStorage = _pendingViewSwitch == 1;
            int movedId = _pendingSwitchItemId;
            _pendingViewSwitch = -1; _pendingSwitchItemId = -1;
            SetView(toStorage);
            // 창고로 입고했는데 현재 필터가 그 아이템을 숨기면 해당 카테고리 탭으로 전환(통합 창고와 동일 동작).
            if (toStorage && movedId > 0 && _storageFilterUI != null && _storageFilter != null)
            {
                var d = GameDataUtility.GetItem(movedId);
                if (d != null && d.itemCategory != _storageFilter.Value)
                    _storageFilterUI.SelectByCategory(d.itemCategory);
            }
        }

        // 현재 화면에 표시된 레시피가 실제 생산 중인 레시피와 일치할 때만 진행 바 표시
        var recipes = _machine.Recipes;
        bool isSelectedRecipeActive =
            _machine.ActiveRecipe != null
            && recipes != null
            && _selectedRecipeIndex >= 0
            && _selectedRecipeIndex < recipes.Count
            && recipes[_selectedRecipeIndex] == _machine.ActiveRecipe;

        // 깔끔 게이지(엔필식): 얇은 선을 좌->우로 채움. 생산 중 아니면 빈 선만 남는다.
        // (옛 하단 슬라이더/노브/트레이 게이지는 SetupCleanGauge 에서 껐다.)
        if (_gaugeFill != null)
        {
            float p = isSelectedRecipeActive ? Mathf.Clamp01(_machine.Progress) : 0f;
            _gaugeFill.rectTransform.sizeDelta = new Vector2(GA_W * p, GA_H);
        }

        // 출력 펄스(#40): 받을 결과물 있으면 출력 슬롯 살짝 맥동(수령 유도). unscaled.
        if (outputSlot != null)
        {
            float s = _hasOutput ? 1f + 0.05f * Mathf.PingPong(Time.unscaledTime * 2f, 1f) : 1f;
            outputSlot.transform.localScale = new Vector3(s, s, 1f);
        }

        // 가동 글로우(가공 중 노란빛 오버레이) 제거 - 패널을 노랗게 덮어 거슬려서 항상 끔.
        if (machineGlow != null && machineGlow.color.a != 0f)
        {
            var gc = machineGlow.color; gc.a = 0f; machineGlow.color = gc;
        }

        // (옛 ProcessingGauge/공식 스트립 상태라벨은 깔끔 게이지로 대체 - SetupCleanGauge 에서 비활성.)

        // 흐름 레일: 아이템 통과 순간 연출은 항상 감지(가동 여부 무관), 중앙 화살표 펄스만 가동 시.
        UpdateFlowRails(isSelectedRecipeActive && _machine.IsProcessing);

        // 연료 없을 때 몇 초 이상 연료가 안 들어가면(팩토리오식) 연료 슬롯을 강조해 위치를 안내. 마우스 무관.
        UpdateFuelHint(_machine.Status == MachineStatus.NoFuel);

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
            // 실제 남은시간 = 레시피 craftTime x 레벨배율 x 공장속도 (폴백 processingTime 아님).
            float remaining = _machine.ResolveProcessTime(_machine.ActiveRecipe) * (1f - _machine.Progress);
            if (_processTimeText != null)
                _processTimeText.text = $"{remaining:F0}초";              // 제작 시간은 중앙(진행바 위)
        }
        else
        {
            statusText.text = "";
            // 가공 전에도 선택한 레시피의 총 제작시간을 미리 보여준다(레시피마다 다름).
            if (_processTimeText != null)
            {
                var sel = (_machine.Recipes != null && _selectedRecipeIndex >= 0 && _selectedRecipeIndex < _machine.Recipes.Count)
                    ? _machine.Recipes[_selectedRecipeIndex] : null;
                float total = _machine.ResolveProcessTime(sel);
                _processTimeText.text = total > 0f ? $"{total:F0}초" : "";
            }
        }
    }

    // ── 재료 회수 버튼 (InputBuffer → 인벤토리) ─────────────────

    /// <summary>InputBuffer 의 모든 재료를 인벤토리로 회수한다(어느 레시피 것이든).
    /// 공유버퍼라 '보는 레시피 것'만 회수하면 다른 재료가 남아 설비가 커밋 상태로 묶이므로 전량 회수한다.</summary>
    public void TakeAllInputs()
    {
        if (_machine == null) return;

        // 회수 대상 = 현재 보고 있는 인벤(가방 뷰=가방 / 창고 뷰=창고). 옛 가방 고정은 창고 뷰에서 혼란.
        var inv = ActiveInv();

        // 버퍼 키를 복사해두고 순회한다(회수하며 Stock 이 바뀌므로).
        var itemIds = new List<int>(_machine.InputBuffer.Stock.Keys);
        bool someLeft = false;
        foreach (var itemId in itemIds)
        {
            int buffered = _machine.InputBuffer.GetAmount(itemId);
            if (buffered <= 0) continue;
            // 먼저 넣어보고 들어간 만큼만 버퍼에서 차감 - 가방 가득 시 초과분이 증발하던 버그 방지(남는 건 설비 유지).
            int leftover = inv != null ? inv.AddItem(itemId, buffered) : buffered;
            int taken = buffered - leftover;
            if (taken > 0) _machine.InputBuffer.Consume(itemId, taken);
            if (leftover > 0) someLeft = true;
        }
        if (someLeft) ToastManager.Warning("가방이 가득 찼습니다");
        inv?.ForceRefreshUI();

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
        GameSfx.Play(SfxId.MachineTakeOutput);

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
                        if (leftover > 0 && storage != null)
                        {
                            StorageInflowNotice.SuppressBriefly();   // 자체 토스트가 있으니 공용 알림 중복 방지
                            storage.AddItem(output.itemId, leftover);
                            movedToStorage = true;
                        }
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

    // ── 연료 없을 때 위치 힌트(팩토리오식) ──────────────────────────
    // NoFuel 상태가 FuelHintDelay 초 이상 지속되면 연료 슬롯을 강조한다(마우스 움직임과 무관 - 그냥
    // 연료가 몇 초간 안 들어가면 뜬다). 강조는 연료를 드래그로 집어들 때 뜨는 그 강조(SetDragHighlight)를
    // 재사용해서 머신 UI 자체 요소로 확실히 보이게 한다. 연료가 채워지면(NoFuel 해제) 자동으로 꺼진다.
    // want 인 동안은 매 프레임 다시 켜서(드래그 강조가 중간에 껐다 켜도) 유지하고, 해제는 1회만 한다.
    const float FuelHintDelay = 2f;
    private float _noFuelTimer;
    private bool _fuelHintOn;

    private void UpdateFuelHint(bool noFuel)
    {
        if (fuelDropSlot == null) return;
        _noFuelTimer = noFuel ? _noFuelTimer + Time.unscaledDeltaTime : 0f;

        bool want = noFuel && _noFuelTimer >= FuelHintDelay;
        if (want)
        {
            fuelDropSlot.SetDragHighlight(true);   // 매 프레임 유지(드래그 강조가 꺼도 자기교정)
            _fuelHintOn = true;
        }
        else if (_fuelHintOn)
        {
            fuelDropSlot.SetDragHighlight(false);  // 해제는 1회 -> 이후엔 드래그 시스템이 슬롯 강조를 소유
            _fuelHintOn = false;
        }
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

// 설비 UI 접힘 섹션 박스: 펼친 그리드에서 끌어온 아이템을 반대 컨테이너로 이동(가방<->창고).
// InventoryDropZone(통합 인벤 패널용)과 같은 이동 API 를 쓰되, 그리드 없이 박스 하나만 담당한다.
// 호환 아이템을 집으면(드래그 중) 박스가 노랗게 밝아져 "여기 놓으면 된다"를 안내.
public class MachineTransferDropBox : MonoBehaviour, IDropHandler
{
    public bool toStorage;      // true = 가방 -> 창고 입고, false = 창고 -> 가방 회수
    public Image highlight;     // 박스 배경(호환 드래그 중 살짝 밝아짐)
    public Color idleColor;
    public Color activeColor;
    public GameObject highlightFrame;   // 호환 드래그 중 물결 프레임(통합 인벤과 동일 문법)
    public MachineUI owner;     // 드롭 성공 시 자동 전환(다음 프레임) 요청

    private InventoryManager Source => toStorage ? InventoryManager.Instance : InventoryManager.StorageInstance;
    private InventoryManager Target => toStorage ? InventoryManager.StorageInstance : InventoryManager.Instance;

    private void Update()
    {
        var dh = InventoryDragHandler.Instance;
        var src = Source;
        bool compat = dh != null && dh.IsDragging && dh.DraggedSlot != null && !dh.DraggedSlot.IsEmpty
                      && src != null && dh.DraggedSlot.Owner == src;
        if (highlight != null) highlight.color = compat ? activeColor : idleColor;
        if (highlightFrame != null && highlightFrame.activeSelf != compat) highlightFrame.SetActive(compat);
    }

    public void OnDrop(PointerEventData eventData)
    {
        var dh = InventoryDragHandler.Instance;
        if (dh == null || !dh.IsDragging) { dh?.EndDrag(); return; }

        var src = Source;
        var tgt = Target;
        // 라이브 시각 칸 대신 박제한 출발 슬롯 사용(컴팩트 표시에서 드래그 중 재렌더로 엉뚱한 아이템 이동 방지).
        if (src == null || tgt == null || dh.SrcManager != src || !dh.SourceStillValid())
        { dh.EndDrag(); return; }

        int fromIndex   = dh.SrcSlotIndex;
        int movedItemId = dh.SrcItemId;   // 이동하면 원본 칸이 비므로 미리 캡처
        bool moved;
        if (dh.IsSplitDrag)
        {
            int t = tgt.FindTargetSlotIndexForItem(movedItemId);
            moved = t >= 0 && src.MoveAmountToSlot(fromIndex, dh.DragAmount, tgt, t);
        }
        else
            moved = src.MoveSlot(fromIndex, tgt);   // AddItem 경유 = 자동 스택/끝에 추가

        dh.EndDrag();
        // 엔필식: 실제로 옮겨졌을 때만 넣은 쪽 섹션이 자동으로 펼쳐진다(드롭 이벤트 밖, 다음 프레임).
        if (moved && owner != null) owner.RequestViewSwitch(toStorage, movedItemId);
    }

    private void OnDisable()
    {
        if (highlight != null) highlight.color = idleColor;
        if (highlightFrame != null && highlightFrame.activeSelf) highlightFrame.SetActive(false);
    }
}

// 드래그 대상 강조 프레임 공용 팩토리(통합 인벤과 동일 문법):
//   안쪽 고정 프레임 + 바깥 얇은 프레임이 물결처럼 안으로 반복 수렴(RegionFrameRipple).
//   스프라이트(hl_region_frame) 없으면 시안 4변 정적 프레임 폴백. 만들 때 비활성으로 반환.
public static class DropHighlightFrame
{
    public static GameObject Build(RectTransform host, Sprite frameSpr)
    {
        var root = new GameObject("DropFrame", typeof(RectTransform));
        root.transform.SetParent(host, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        if (frameSpr != null)
        {
            // 물결(바깥 -> 안 수렴). [07-06] 다겹 캐스케이드: 잔잔하게 수렴 + 오버레이 렌더(마스크 밖 = 안 잘림).
            //   슬롯 크기(88~140px) 프레임이라 reach 는 기본값(44, 대형 패널용)보다 작게.
            //   튜닝 노브: reach(간격/거리) / reachBottom(하단) / cycle(속도) / layers(겹 수).
            const float rippleDepth = 1f;
            MakeLayer(rt, frameSpr, 2f, rippleDepth);                 // 안쪽 고정(굵음)
            var outer = MakeLayer(rt, frameSpr, 3f, 0f);              // 바깥(얇음) = 물결
            var ripple = outer.gameObject.AddComponent<RegionFrameRipple>();
            ripple.rippleDepth = rippleDepth; ripple.reach = 8f; ripple.cycle = 2.4f; ripple.lineGap = 5f;
        }
        else
        {
            Color edge = new Color(130f / 255f, 214f / 255f, 230f / 255f, 0.95f);
            for (int side = 0; side < 4; side++) MakeEdge(rt, edge, 3f, side);
        }

        root.SetActive(false);
        return root;
    }

    private static Image MakeLayer(RectTransform parent, Sprite spr, float ppuMult, float inset)
    {
        var go = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        var img = go.GetComponent<Image>();
        img.sprite = spr; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppuMult;
        img.color = Color.white; img.raycastTarget = false;
        return img;
    }

    private static void MakeEdge(RectTransform parent, Color c, float th, int side)
    {
        var go = new GameObject("Edge" + side, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        switch (side)
        {
            case 0: rt.anchorMin = new Vector2(0, 1); rt.anchorMax = Vector2.one;            rt.offsetMin = new Vector2(0, -th); rt.offsetMax = Vector2.zero; break;
            case 1: rt.anchorMin = Vector2.zero;      rt.anchorMax = new Vector2(1, 0);      rt.offsetMin = Vector2.zero;        rt.offsetMax = new Vector2(0, th); break;
            case 2: rt.anchorMin = Vector2.zero;      rt.anchorMax = new Vector2(0, 1);      rt.offsetMin = Vector2.zero;        rt.offsetMax = new Vector2(th, 0); break;
            default: rt.anchorMin = new Vector2(1, 0); rt.anchorMax = Vector2.one;           rt.offsetMin = new Vector2(-th, 0); rt.offsetMax = Vector2.zero; break;
        }
        var img = go.GetComponent<Image>();
        img.color = c; img.raycastTarget = false;
    }
}
