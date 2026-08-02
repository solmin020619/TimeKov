using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JeffGrawAssets.FlexibleUI;
using TIMEKOV.Factory;

/// <summary>
/// StorageExtractor(창고 출력 포트)의 UI. 공장 UI(MachineUI)와 동일 사이즈/톤으로 런타임 전면 구성.
///  - 좌측: 창고 그리드(선택 전용, owner=null) + 카테고리 필터(1번 칸부터 컴팩트)
///  - 우측: 추출 품목 슬롯(창고 아이템 드래그&드롭 or 더블클릭으로 지정) + 실 레일 RT -> 물류 출력
/// ★씬 패널(StorageMachinePanel)이 Canvas 프리팹 인스턴스라 에디터 빌더는 자식 삭제가 막힘 ->
///   런타임 구성으로 전환(프리팹 제약 없음, 메뉴 실행 불필요, Play 시 자동 반영).
/// </summary>
public class StorageExtractorUI : MonoBehaviour
{
    [Header("루트 패널 (StorageMachinePanel)")]
    public GameObject uiPanel;

    [Header("추출 품목 슬롯 (보존·재배치)")]
    public StorageItemSelectSlot itemSelectSlot;

    [Header("창고 슬롯 프리팹")]
    public GameObject inventorySlotPrefab;
    public int inventorySlotCount = 20;

    // ── 색 팔레트(공장 UI 동일) ──
    static readonly Color TxtMain    = new Color(0.914f, 0.933f, 0.960f, 1f);   // e9eef5
    static readonly Color TxtSub     = new Color(0.604f, 0.671f, 0.749f, 1f);   // 9aabbf
    static readonly Color TxtDark    = new Color(0.137f, 0.165f, 0.200f, 1f);   // 232a33
    static readonly Color HeaderHair = new Color(120/255f, 140/255f, 170/255f, 0.45f);

    const float PanelW = 1700f, PanelH = 830f, HeaderH = 64f, SidePad = 26f, Gap = 20f, LeftW = 620f;
    const float FR_BeltSize = 234.24f;   // 포트단자 64 * 3.66 (레일 스트립 정사각 변)

    private StorageExtractor _machine;
    private bool _built;

    // 런타임 생성 참조
    private TextMeshProUGUI _titleText, _statusText, _stockText, _stockShadow, _installText;
    private Slider _gauge;
    private Transform _invParent;
    private RectTransform _filterRow, _flowRailsRoot;
    private GameObject _dropFrame;   // 드래그 중 추출슬롯 흰색 물결 강조(RegionFrameRipple)
    private Image _portTick;   // 벨트 시작 포트 단자(배출 순간 반짝)
    private Color _tickBase;


    private readonly List<InventorySlotUI> _invSlots = new();

    // 필터
    private CategoryFilterUI _filterUI;
    private ItemCategory? _storageFilter;
    private readonly List<InventorySlot> _filterBuf = new();
    private bool _refreshPending;

    // 레일 RT
    private RailPortraitRenderer _railPortrait;
    private Texture _railTex;

    // 흐름 연출(선택 아이템이 창고 -> 포트 -> 벨트로 좌->우 이동) + 중앙 홀로그램
    private Image _hologram;
    private Image _flowIcon;
    private BuildPort _outPort;
    private object _prevOcc;
    private int _prevOccId = -1;
    private float _flowT = -1f;
    private Vector2 _flowFrom, _flowTo;

    // ── 열기 / 닫기 ───────────────────────────────────────────────────

    public void OpenFor(StorageExtractor machine, string title)
    {
        _machine = machine;

        BuildUI();

        uiPanel.SetActive(true);
        uiPanel.GetComponent<UISlideEffect>()?.Open();
        if (_titleText != null) _titleText.text = string.IsNullOrEmpty(title) ? Loc.Get("창고 출력 포트") : title;

        // 설치 개수 제한 표시 (패널 열 때 현재 설치수/상한). UI 열려 있는 동안엔 안 바뀜.
        if (_installText != null)
        {
            int fid = FacilityBuildLimit.WarehousePortId;
            if (FacilityBuildLimit.HasLimit(fid))
            {
                var bm = FindFirstObjectByType<BuildManager>();
                int placed = bm != null ? bm.CountPlacedFacilities(fid) : 0;
                _installText.text = Loc.Get("설치") + " " + $"{placed}/{FacilityBuildLimit.GetMax(fid)}";
            }
            else _installText.text = "";
        }

        itemSelectSlot?.Setup(machine);
        EnsureFilterUI();
        BuildInventorySlots();
        BuildRail();

        var inv = InventoryManager.StorageInstance;
        if (inv != null) inv.OnInventoryChanged += RefreshInventorySlots;

        InventorySlotUI.OnAnySlotDoubleClicked += OnGridSlotDoubleClicked;
    }

    public void Close()
    {
        if (_machine == null) return;

        itemSelectSlot?.Cleanup();

        var inv = InventoryManager.StorageInstance;
        if (inv != null) inv.OnInventoryChanged -= RefreshInventorySlots;

        InventorySlotUI.OnAnySlotDoubleClicked -= OnGridSlotDoubleClicked;

        _machine = null;

        var slide = uiPanel.GetComponent<UISlideEffect>();
        if (slide != null && uiPanel.activeInHierarchy) slide.Close();
        else uiPanel.SetActive(false);

        GameUIController.Instance?.CloseFactoryUI();
    }

    // ── 매 프레임 — 타이머 + 동결 flush ─────────────────────────────

    private void Update()
    {
        if (_machine == null || !uiPanel.activeSelf) return;

        if (_refreshPending && (InventoryDragHandler.Instance == null || !InventoryDragHandler.Instance.IsDragging))
        {
            _refreshPending = false;
            RefreshInventorySlots();
        }

        float remaining = _machine.TimerRemaining;
        float interval  = _machine.ExtractInterval;
        bool hasBelt = _machine.HasOutputBelt;

        var inv = InventoryManager.StorageInstance;
        int selId = _machine.SelectedItemId;
        int stock = (selId > 0 && inv != null) ? inv.GetTotalItemCount(selId) : 0;

        if (_statusText != null)
        {
            if (selId <= 0)      _statusText.text = Loc.Get("아이템을 선택하세요");
            else if (!hasBelt)   _statusText.text = Loc.Get("벨트 연결 필요");
            else if (stock <= 0) _statusText.text = Loc.Get("창고에 재고 없음");
            else                 _statusText.text = string.Format(Loc.Get("추출까지: {0}초"), $"{remaining:F1}");
        }
        // 재고 없으면 게이지도 빈 채로(설비 타이머가 이미 멈춰 remaining=interval 이지만 UI 도 방어)
        if (_gauge != null)
            _gauge.value = (hasBelt && stock > 0 && interval > 0f) ? 1f - (remaining / interval) : 0f;

        // 선택 아이템 창고 재고수 = 슬롯 가운데 큰 숫자로("창고 N개" 텍스트 폐기)
        if (_stockText != null)
        {
            string s = selId > 0 ? stock.ToString() : "";
            _stockText.text = s;
            if (_stockShadow != null) _stockShadow.text = s;
        }

        // 드래그 중에만 추출 슬롯 흰색 물결 강조 on (인벤/공장과 동일 RegionFrameRipple)
        if (_dropFrame != null)
        {
            bool dragging = InventoryDragHandler.Instance != null && InventoryDragHandler.Instance.IsDragging;
            if (_dropFrame.activeSelf != dragging) _dropFrame.SetActive(dragging);
        }

        UpdateFlow();
    }

    // 배출 순간 연출(공장 출력 언어): 출력 파이프가 "반짝" 빛난 뒤 아이템이 레일 위에 나타나 벨트로 타고 나간다.
    // 발동은 타이머가 아니라 출력 포트 앞 벨트에 새 인스턴스가 등장하는 순간(버퍼 밀림 시 desync 방지).
    private void UpdateFlow()
    {
        if (_outPort != null)
        {
            object occ = BeltSegment.PortFrontOccupant(_outPort, out int occId);
            bool fire = occ != null && !ReferenceEquals(occ, _prevOcc);
            _prevOcc = occ; _prevOccId = occId;
            if (fire && occId > 0 && _flowT < 0f)
            {
                var d = ItemDatabase.GetItem(occId);
                var icon = d != null ? ItemDatabase.GetIcon(d.iconKey) : null;
                if (_flowIcon != null && icon != null) _flowIcon.sprite = icon;
                _flowT = 0f;
            }
        }

        if (_flowT < 0f) { RestorePipe(); return; }

        _flowT += Time.unscaledDeltaTime;
        float t = _flowT;
        const float total = 1.25f, iconDur = 1.1f, flashStart = 0.06f, flashDur = 0.28f;

        // 1) 아이템 아이콘이 긴 벨트를 타고 슬롯 -> 물류 출력까지 흐른다(양끝 페이드).
        if (_flowIcon != null)
        {
            float iu = t / iconDur;
            if (iu >= 0f && iu <= 1f && _flowIcon.sprite != null)
            {
                _flowIcon.enabled = true;
                _flowIcon.rectTransform.anchoredPosition = Vector2.Lerp(_flowFrom, _flowTo, iu);
                var c = _flowIcon.color; c.a = Mathf.Sin(iu * Mathf.PI); _flowIcon.color = c;
            }
            else if (_flowIcon.enabled) _flowIcon.enabled = false;
        }

        // 2) 아이템이 벨트로 나오는 순간 포트 단자가 반짝(배경/레일과 같은 시안 언어).
        float fa = (t >= flashStart && t < flashStart + flashDur) ? Mathf.Sin((t - flashStart) / flashDur * Mathf.PI) : 0f;
        ApplyPipeFlash(fa);

        if (t > total)
        {
            _flowT = -1f;
            if (_flowIcon != null) _flowIcon.enabled = false;
            RestorePipe();
        }
    }

    // 포트 단자 반짝(a=0 원복 ~ 1 최대). 밝은 시안화이트로 튄다.
    private void ApplyPipeFlash(float a)
    {
        Color bright = new Color(0.80f, 0.96f, 1f, 1f);
        if (_portTick != null) _portTick.color = Color.Lerp(_tickBase, bright, a);
    }

    private void RestorePipe()
    {
        if (_portTick != null && _portTick.color != _tickBase) _portTick.color = _tickBase;
    }

    // ══════════════════════════════════════════════════════════════════
    //  런타임 UI 구성 (프리팹 제약 회피)
    // ══════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        if (_built) return;
        _built = true;

        var prt = uiPanel.GetComponent<RectTransform>();
        if (prt == null) prt = uiPanel.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.localScale = Vector3.one;
        prt.sizeDelta = new Vector2(PanelW, PanelH);
        prt.anchoredPosition = Vector2.zero;

        // 보존할 추출슬롯을 먼저 패널 직속으로 빼둔다(자식 정리 때 안 지워지게).
        Transform keep = itemSelectSlot != null ? itemSelectSlot.transform : null;
        if (keep != null) keep.SetParent(prt, false);

        // 옛 자식 정리(추출슬롯 제외). StorageExtractorUI 는 패널 루트에 있으므로 자기 자신은 안전.
        for (int i = prt.childCount - 1; i >= 0; i--)
        {
            var ch = prt.GetChild(i);
            if (ch == keep) continue;
            ch.gameObject.SetActive(false);
            Destroy(ch.gameObject);
        }

        // 패널 표면(옅게) + 코너 마스크
        var pimg = uiPanel.GetComponent<Image>();
        if (pimg == null) pimg = uiPanel.AddComponent<Image>();
        pimg.sprite = null; pimg.type = Image.Type.Simple; pimg.color = new Color(1f, 1f, 1f, 0.12f);
        var mask = uiPanel.GetComponent<UnityEngine.UI.Mask>();
        if (mask == null) mask = uiPanel.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = true;

        BuildFrost(prt);
        BuildHeader(prt);
        BuildLeftColumn(prt);
        BuildRightArea(prt, keep);
    }

    private void BuildHeader(RectTransform prt)
    {
        _titleText = NewText("Title", prt, Loc.Get("창고 출력 포트"), 28, TxtDark, TextAlignmentOptions.Left);
        _titleText.fontStyle = FontStyles.Bold;
        var tr = _titleText.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0, 1); tr.pivot = new Vector2(0, 1);
        tr.sizeDelta = new Vector2(460, 44); tr.anchoredPosition = new Vector2(28, -12);
        AddWhiteHalo(_titleText.gameObject);

        // 닫기 버튼 (우상단, TMP X)
        var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(prt, false);
        var crt = closeGo.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(1, 1);
        crt.sizeDelta = new Vector2(54, 54); crt.anchoredPosition = new Vector2(-14, -8);   // 인벤과 동일 크기
        var cbtn = closeGo.GetComponent<Button>(); cbtn.onClick.AddListener(Close);
        CloseButtonStyle.Apply(cbtn);   // 인벤과 동일한 호버 하이라이트 + 둥근 배경
        // 기존 공용 X 아이콘(ic_close, 공장 UI 와 동일)으로 통일. 없으면 TMP 폴백.
        var closeSpr = Resources.Load<Sprite>("Image/UI_Icon/ic_close");
        if (closeSpr != null)
        {
            var ci = NewImage("Icon", closeGo.transform, TxtDark);
            ci.sprite = closeSpr; ci.preserveAspect = true; ci.raycastTarget = false;
            ci.rectTransform.sizeDelta = new Vector2(48, 48);   // 인벤과 동일 아이콘 크기
        }
        else
        {
            var cx = NewText("X", closeGo.transform, "X", 26, TxtDark, TextAlignmentOptions.Center);
            cx.fontStyle = FontStyles.Bold; FillRect(cx.rectTransform);
        }

        // 헤더 구분선
        var hair = NewImage("HeaderDivider", prt, new Color(70/255f, 84/255f, 104/255f, 0.5f));
        hair.raycastTarget = false;
        var hr = hair.rectTransform;
        hr.anchorMin = new Vector2(0, 1); hr.anchorMax = new Vector2(1, 1); hr.pivot = new Vector2(0.5f, 1);
        hr.offsetMin = new Vector2(3, -HeaderH - 2); hr.offsetMax = new Vector2(-3, -HeaderH);
    }

    private void BuildLeftColumn(RectTransform prt)
    {
        var col = NewRect("WarehouseColumn", prt);
        col.anchorMin = new Vector2(0, 0); col.anchorMax = new Vector2(0, 1); col.pivot = new Vector2(0.5f, 0.5f);
        col.offsetMin = new Vector2(SidePad, 28); col.offsetMax = new Vector2(SidePad + LeftW, -(HeaderH + Gap));

        // 필터행(런타임 CategoryFilterUI 클론 자리) - 좌측 최상단("창고|전체" 라벨 제거)
        _filterRow = NewRect("FilterRow", col);
        _filterRow.anchorMin = new Vector2(0, 1); _filterRow.anchorMax = new Vector2(1, 1); _filterRow.pivot = new Vector2(0.5f, 1);
        _filterRow.offsetMin = new Vector2(0, -54); _filterRow.offsetMax = new Vector2(0, -2);

        // 필터/그리드 구분선
        var div = NewImage("ColHair", col, HeaderHair); div.raycastTarget = false;
        var dr = div.rectTransform;
        dr.anchorMin = new Vector2(0, 1); dr.anchorMax = new Vector2(1, 1); dr.pivot = new Vector2(0.5f, 1);
        dr.offsetMin = new Vector2(2, -64); dr.offsetMax = new Vector2(-2, -63);

        // 스크롤 뷰포트
        var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        vpGo.transform.SetParent(col, false);
        var vpRt = vpGo.GetComponent<RectTransform>();
        vpRt.anchorMin = new Vector2(0, 0); vpRt.anchorMax = new Vector2(1, 1);
        vpRt.offsetMin = new Vector2(8, 8); vpRt.offsetMax = new Vector2(-8, -72);
        var vpImg = vpGo.GetComponent<Image>(); vpImg.color = new Color(1, 1, 1, 0f); vpImg.raycastTarget = true;

        int cols = 4;
        Vector2 spacing = new Vector2(8, 8);
        float innerW = LeftW - 16f;
        float cell = Mathf.Floor((innerW - spacing.x * (cols - 1)) / cols);

        var content = NewRect("Content", vpRt);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cell, cell); grid.spacing = spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = cols;
        grid.childAlignment = TextAnchor.UpperCenter; grid.padding = new RectOffset(2, 2, 2, 2);
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var scroll = vpGo.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f; scroll.viewport = vpRt; scroll.content = content;

        _invParent = content;
    }

    private void BuildRightArea(RectTransform prt, Transform keep)
    {
        var area = NewRect("OutputArea", prt);
        area.anchorMin = new Vector2(0, 0); area.anchorMax = new Vector2(1, 1); area.pivot = new Vector2(0.5f, 0.5f);
        area.offsetMin = new Vector2(SidePad + LeftW + Gap, 28); area.offsetMax = new Vector2(-SidePad, -(HeaderH + Gap));

        const float flowY = 60f;   // 흐름 레인 세로 위치(영역 중앙 기준 약간 위)

        // 중앙 홀로그램(도면 PNG). Resources 자동 로드. 원본이 가로형(2063x1375)이라 칸도 가로로.
        // 라인아트라 알파 올려도 투과됨 = 선만 또렷해짐(종욱: 너무 흐릴 필요 없다 -> 0.82).
        _hologram = NewImage("Hologram", area, new Color(1f, 1f, 1f, 0.82f));
        _hologram.raycastTarget = false; _hologram.preserveAspect = true;
        _hologram.rectTransform.anchoredPosition = new Vector2(0, flowY);
        _hologram.rectTransform.sizeDelta = new Vector2(720, 479);
        var holo = Resources.Load<Sprite>("Image/UI_Icon/FacilityBlueprint/9_창고 출력 포트");
        if (holo != null) { _hologram.sprite = holo; _hologram.enabled = true; }
        else _hologram.enabled = false;

        // ── 설치 개수 (우측 상단 여백). 큰 값 + 개수 늘리는 방법 힌트. OpenFor 에서 값 세팅. ──
        _installText = NewText("InstallLimit", area, "", 26, TxtDark, TextAlignmentOptions.Center);
        _installText.fontStyle = FontStyles.Bold;
        _installText.rectTransform.anchoredPosition = new Vector2(20, 326);
        _installText.rectTransform.sizeDelta = new Vector2(420, 40);
        var instHint = NewText("InstallHint", area, Loc.Get("우주선을 수리하면 설치 개수를 늘릴 수 있습니다"),
            15, new Color(0.20f, 0.24f, 0.30f, 1f), TextAlignmentOptions.Center);
        instHint.rectTransform.anchoredPosition = new Vector2(20, 296);
        instHint.rectTransform.sizeDelta = new Vector2(500, 24);

        // 좌측: "현재 출력" + 추출 품목 슬롯(보존)
        var cur = NewText("CurrentLabel", area, Loc.Get("현재 출력"), 20, TxtMain, TextAlignmentOptions.Center);
        cur.rectTransform.anchoredPosition = new Vector2(-340, flowY + 120);
        cur.rectTransform.sizeDelta = new Vector2(200, 30);

        if (keep != null)
        {
            keep.SetParent(area, false);
            var ssRt = keep.GetComponent<RectTransform>();
            if (ssRt != null)
            {
                ssRt.anchorMin = ssRt.anchorMax = ssRt.pivot = new Vector2(0.5f, 0.5f);
                ssRt.sizeDelta = new Vector2(180, 180);
                ssRt.anchoredPosition = new Vector2(-340, flowY);
            }
            // 드래그 대상 강조 = 흰색 물결(RegionFrameRipple). 인벤/공장과 동일 hl_region_frame.
            // 슬롯을 host 로 프레임 생성(비활성 반환) -> Update 서 드래그 중에만 켠다.
            var frameSpr = Resources.Load<Sprite>("Image/UI_Icon/hl_region_frame");
            _dropFrame = DropHighlightFrame.Build((RectTransform)keep, frameSpr);
        }

        // 선택 아이템 창고 재고수 = 현재 출력 슬롯 가운데(하단)에 큰 숫자로. ("창고 N개" 폐기)
        // TMP 는 UI.Outline 이 안 먹으니 그림자용 텍스트를 뒤에 깔아 아이콘 위에서도 잘 읽히게.
        Vector2 stockPos = new Vector2(-340, flowY - 56);
        _stockShadow = NewText("StockCountShadow", area, "", 48, new Color(0.02f, 0.03f, 0.06f, 0.9f), TextAlignmentOptions.Center);
        _stockShadow.fontStyle = FontStyles.Bold;
        _stockShadow.rectTransform.anchoredPosition = stockPos + new Vector2(2.5f, -2.5f);
        _stockShadow.rectTransform.sizeDelta = new Vector2(220, 66);

        _stockText = NewText("StockCount", area, "", 48, new Color(0.90f, 0.97f, 1f, 1f), TextAlignmentOptions.Center);
        _stockText.fontStyle = FontStyles.Bold;
        _stockText.rectTransform.anchoredPosition = stockPos;
        _stockText.rectTransform.sizeDelta = new Vector2(220, 66);

        // 긴 벨트(실 레일): 슬롯 오른쪽에서 물류 출력까지 하나로 이어진다(가운데를 벨트가 관통).
        // ★레일 텍스처를 가로로 타일(UV 반복)해 길게 늘림 - 종욱 "레일을 처음부터 이어서 길게".
        const float beltStartX = -230f, beltEndX = 430f;
        float beltCenterX = (beltStartX + beltEndX) * 0.5f;
        float beltW = beltEndX - beltStartX;
        _flowRailsRoot = NewRect("FlowRailsRoot", area);
        _flowRailsRoot.anchoredPosition = new Vector2(beltCenterX, flowY);
        _flowRailsRoot.sizeDelta = new Vector2(beltW, FR_BeltSize);

        // 벨트 시작점(포트 단자) - 슬롯에서 벨트로 나오는 지점. 배출 순간 반짝.
        var portTick = NewImage("PortTick", area, new Color(0.28f, 0.80f, 1.0f, 0.95f));
        portTick.raycastTarget = false;
        portTick.rectTransform.anchoredPosition = new Vector2(beltStartX, flowY);
        portTick.rectTransform.sizeDelta = new Vector2(8f, 64f);
        _portTick = portTick; _tickBase = portTick.color;   // 배출 순간 반짝용

        var outLbl = NewText("OutputLabel", area, Loc.Get("물류 출력"), 18, TxtSub, TextAlignmentOptions.Center);
        outLbl.rectTransform.anchoredPosition = new Vector2(beltEndX - 40f, flowY + 120);
        outLbl.rectTransform.sizeDelta = new Vector2(200, 30);

        // 흐름 연출용 아이콘(기본 숨김). 슬롯 오른쪽에서 긴 벨트를 타고 물류 출력까지.
        _flowFrom = new Vector2(-250f, flowY);
        _flowTo   = new Vector2(beltEndX - 40f, flowY);
        _flowIcon = NewImage("FlowIcon", area, Color.white);
        _flowIcon.raycastTarget = false; _flowIcon.preserveAspect = true; _flowIcon.enabled = false;
        _flowIcon.rectTransform.sizeDelta = new Vector2(72, 72);

        // 게이지 "다음 배출까지"
        var glabel = NewText("GaugeLabel", area, Loc.Get("다음 배출까지"), 15, TxtSub, TextAlignmentOptions.Left);
        glabel.rectTransform.pivot = new Vector2(0, 0.5f);
        glabel.rectTransform.anchoredPosition = new Vector2(-380, -150);
        glabel.rectTransform.sizeDelta = new Vector2(300, 26);

        var track = NewImage("GaugeTrack", area, new Color(150/255f, 178/255f, 205/255f, 0.28f));
        track.raycastTarget = false;
        var tkr = track.rectTransform;
        tkr.pivot = new Vector2(0, 0.5f);
        tkr.sizeDelta = new Vector2(760, 4); tkr.anchoredPosition = new Vector2(-380, -178);

        var fillImg = NewImage("GaugeFill", track.transform, new Color(72/255f, 205/255f, 255/255f, 0.95f));
        fillImg.raycastTarget = false;
        var flr = fillImg.rectTransform;
        flr.anchorMin = new Vector2(0, 0); flr.anchorMax = new Vector2(0, 1); flr.pivot = new Vector2(0, 0.5f);
        flr.offsetMin = Vector2.zero; flr.offsetMax = Vector2.zero;

        _gauge = track.gameObject.AddComponent<Slider>();
        _gauge.transition = Selectable.Transition.None;
        _gauge.direction = Slider.Direction.LeftToRight;
        _gauge.minValue = 0f; _gauge.maxValue = 1f; _gauge.value = 0f;
        _gauge.fillRect = flr;
        _gauge.targetGraphic = fillImg;

        _statusText = NewText("StatusText", area, Loc.Get("아이템을 선택하세요"), 17, TxtMain, TextAlignmentOptions.Left);
        _statusText.rectTransform.pivot = new Vector2(0, 0.5f);
        _statusText.rectTransform.anchoredPosition = new Vector2(-380, -206);
        _statusText.rectTransform.sizeDelta = new Vector2(600, 28);

        // 하단 설명 정보 패널 - 입체(둥근 + 세로 그라데이션 엠보스 + 밝은 림), 텍스트 꽉차게 (공장 박스 방식)
        var info = NewImage("InfoPanel", area, Color.white);
        info.sprite = UISpriteFactory.RoundedRectVGrad(new Color32(52, 60, 74, 224), new Color32(18, 23, 32, 206), 64, 16);
        info.type = Image.Type.Sliced;
        info.raycastTarget = false;
        info.rectTransform.anchoredPosition = new Vector2(0, -286);
        info.rectTransform.sizeDelta = new Vector2(860, 100);
        var infoRim = info.gameObject.AddComponent<UnityEngine.UI.Outline>();
        infoRim.effectColor = new Color(0.55f, 0.66f, 0.80f, 0.34f); infoRim.effectDistance = new Vector2(0f, -1.5f);
        var ibody = NewText("InfoBody", info.transform,
            Loc.Get("고른 아이템을 창고에서 꺼내 벨트로 내보냅니다.\n창고 테두리에 설치하세요."),
            20, TxtMain, TextAlignmentOptions.Center);
        ibody.textWrappingMode = TextWrappingModes.Normal;
        ibody.enableAutoSizing = true; ibody.fontSizeMin = 15f; ibody.fontSizeMax = 24f;   // 패널 크기에 꽉 차게
        var ibrt = ibody.rectTransform;
        ibrt.anchorMin = Vector2.zero; ibrt.anchorMax = Vector2.one; ibrt.offsetMin = new Vector2(22, 10); ibrt.offsetMax = new Vector2(-22, -10);
    }

    private void BuildFrost(RectTransform prt)
    {
        var blurGo = new GameObject("PanelBlur", typeof(RectTransform), typeof(CanvasRenderer), typeof(UIBlur));
        blurGo.transform.SetParent(prt, false);
        var blRt = blurGo.GetComponent<RectTransform>();
        blRt.anchorMin = Vector2.zero; blRt.anchorMax = Vector2.one;
        blRt.offsetMin = new Vector2(6, 6); blRt.offsetMax = new Vector2(-6, -6);
        var blur = blurGo.GetComponent<UIBlur>();
        blur.Common.blurReferencesFrom = UIBlurCommon.BlurReferencesFrom.Self;
        blur.Common.cameraReference = null;
        blur.Common.featureNumber = 0;
        blur.Common.unrankedLayer = 0;
        blur.Common.blurStrength = 1f;
        var bs = blur.Common.blurInstanceSettings;
        if (bs != null)
        {
            if (bs.downscaleSections != null) foreach (var sec in bs.downscaleSections) { sec.SetAlgorithm(BlurAlgorithm.Tap5Star); sec.iterations = 2; sec.sampleDistance = 1.5f; }
            if (bs.blurSections != null) foreach (var sec in bs.blurSections) { sec.SetAlgorithm(BlurAlgorithm.Gaussian); sec.horizontalSamplesPerSide = 1; sec.verticalSamplesPerSide = 1; sec.iterations = 4; sec.sampleDistance = 1.5f; }
            bs.blurAdditionalDistancePerIteration = 1f;
            bs.referenceResolution = 1080;
            bs.hqResample = false;
            bs.ditherStrength = 0.25f;
            bs.vibrancy = 1f; bs.brightness = 0f; bs.contrast = 0f;
        }
        blur.Common.ValidateBlur();

        var bg = NewImage("BgDark", prt, Color.white);
        bg.raycastTarget = false;
        var bgrt = bg.rectTransform;
        bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
        var grad = bg.gameObject.AddComponent<UIFrostGradient>();
        grad.topColor = new Color(1f, 1f, 1f, 0.9f);
        grad.bottomColor = new Color(18/255f, 20/255f, 26/255f, 0.62f);
        grad.topBias = 3f;
    }

    // ── 런타임 헬퍼 ──────────────────────────────────────────────────

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private static Image NewImage(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var img = go.GetComponent<Image>(); img.color = c;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color c, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = c; tmp.alignment = align;
        tmp.raycastTarget = false; tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private static void FillRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void AddWhiteHalo(GameObject go)
    {
        var ol = go.AddComponent<UnityEngine.UI.Outline>();
        ol.effectColor = new Color(1f, 1f, 1f, 0.4f); ol.effectDistance = new Vector2(1f, -1f);
    }

    // ── 카테고리 필터 (씬 CategoryFilterUI 클론) ─────────────────────

    private void EnsureFilterUI()
    {
        if (_filterUI != null || _filterRow == null) return;
        var src = FindFirstObjectByType<CategoryFilterUI>(FindObjectsInactive.Include);
        if (src == null) return;

        var cloneGo = Instantiate(src.gameObject, _filterRow);
        cloneGo.SetActive(true);
        var rt = (RectTransform)cloneGo.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(LeftW, 52f);
        rt.anchoredPosition = Vector2.zero;

        _filterUI = cloneGo.GetComponent<CategoryFilterUI>();
        if (_filterUI != null) _filterUI.OnFilterChanged += OnFilterChanged;
    }

    private void OnFilterChanged(ItemCategory? cat)
    {
        _storageFilter = cat;
        RefreshInventorySlots();
    }

    // ── 창고 그리드 (선택 전용) ───────────────────────────────────────

    private void BuildInventorySlots()
    {
        if (_invParent == null || inventorySlotPrefab == null) return;
        var inv = InventoryManager.StorageInstance;
        int slotCount = inv != null ? inv.GetMaxSlots() : inventorySlotCount;

        if (_invSlots.Count != slotCount)
        {
            foreach (var s in _invSlots)
                if (s != null) Destroy(s.gameObject);
            _invSlots.Clear();

            for (int i = 0; i < slotCount; i++)
            {
                var go   = Instantiate(inventorySlotPrefab, _invParent);
                var slot = go.GetComponent<InventorySlotUI>();
                _invSlots.Add(slot);
            }
        }

        RefreshInventorySlots();
    }

    public void RefreshInventorySlots()
    {
        var inv = InventoryManager.StorageInstance;
        if (inv == null) return;

        // 드래그 중이면 재배치 미룸(컴팩트 잔상 방지). Update 가 놓는 순간 flush.
        if (InventoryDragHandler.Instance != null && InventoryDragHandler.Instance.IsDragging)
        {
            _refreshPending = true;
            return;
        }

        var slots = inv.GetSlots();

        bool filtered = _storageFilter != null;
        if (filtered)
        {
            _filterBuf.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null || s.IsEmpty) continue;
                var d = GameDataUtility.GetItem(s.itemId);
                if (d != null && d.itemCategory == _storageFilter.Value) _filterBuf.Add(s);
            }
        }

        for (int i = 0; i < _invSlots.Count; i++)
        {
            if (_invSlots[i] == null) continue;
            InventorySlot disp;
            if (filtered) disp = i < _filterBuf.Count ? _filterBuf[i] : new InventorySlot();
            else          disp = i < slots.Count ? slots[i] : null;
            _invSlots[i].Refresh(disp, null);   // owner=null = 선택 전용(이동 차단)
        }
    }

    private void OnGridSlotDoubleClicked(InventorySlotUI slot)
    {
        if (_machine == null || slot == null || slot.IsEmpty) return;
        if (!_invSlots.Contains(slot)) return;

        _machine.SetTargetItem(slot.SlotData.itemId);
        UISoundManager.Instance?.PlayItemDrop();
    }

    // ── 출력 레일 (실 레일 RT) ───────────────────────────────────────

    private void BuildRail()
    {
        if (_flowRailsRoot == null || _machine == null) return;

        for (int i = _flowRailsRoot.childCount - 1; i >= 0; i--)
            Destroy(_flowRailsRoot.GetChild(i).gameObject);

        BuildPort outPort = null;
        foreach (var p in _machine.GetComponentsInChildren<BuildPort>())
            if (p.portType == PortType.Output) { outPort = p; break; }
        _outPort = outPort;
        _prevOcc = null; _prevOccId = -1;
        if (outPort == null || !BeltSegment.IsPortConnected(outPort)) return;

        // 이미 포트 앞에 아이템이 있으면 오픈 즉시 오발동 안 하게 초기 점유자 기록.
        _prevOcc = BeltSegment.PortFrontOccupant(outPort, out _prevOccId);

        var tex = EnsureRailTexture();
        if (tex == null) return;

        // 벨트 루트(가로로 긴) 를 꽉 채우는 RawImage 1장. 정사각 레일 텍스처를 가로로 타일(UV 반복)해
        // 슬롯 -> 물류 출력까지 하나로 이어진 벨트로. 세로는 그대로(레일 밴드가 가운데 = 두께 유지).
        var go = new GameObject("PortRailReal", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(_flowRailsRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var raw = go.GetComponent<RawImage>();
        raw.texture = tex; raw.raycastTarget = false;
        float bw = _flowRailsRoot.sizeDelta.x, bh = _flowRailsRoot.sizeDelta.y;
        raw.uvRect = new Rect(0f, 0f, bh > 0f ? bw / bh : 1f, 1f);   // 가로로 bw/bh 번 반복 = 정사각 비율 유지
    }

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
        // 가로 타일용: 레일이 텍스처 좌우 끝까지 차게(fill 1) 렌더 -> 반복 이음새 최소화.
        _railPortrait.fill = 1.0f;
        _railTex = _railPortrait.Render(rbm.StraightRailPrefab, 256, 256);
        if (_railTex != null) _railTex.wrapMode = TextureWrapMode.Repeat;   // UV 반복(가로 타일) 가능하게
        return _railTex;
    }
}
