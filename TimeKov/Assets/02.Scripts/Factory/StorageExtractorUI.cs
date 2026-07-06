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
    private TextMeshProUGUI _titleText, _statusText, _stockText;
    private Slider _gauge;
    private Transform _invParent;
    private RectTransform _filterRow, _flowRailsRoot;
    private Image _dropHighlight;   // 드래그 중 추출슬롯 강조 프레임

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
        if (_titleText != null) _titleText.text = string.IsNullOrEmpty(title) ? "창고 출력 포트" : title;

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

        if (_statusText != null)
        {
            if (_machine.SelectedItemId <= 0)   _statusText.text = "아이템을 선택하세요";
            else if (!hasBelt)                  _statusText.text = "벨트 연결 필요";
            else                                _statusText.text = $"추출까지: {remaining:F1}초";
        }
        if (_gauge != null)
            _gauge.value = (hasBelt && interval > 0f) ? 1f - (remaining / interval) : 0f;

        // 선택 아이템 창고 재고수(현재 출력 슬롯 아래)
        if (_stockText != null)
        {
            var inv = InventoryManager.StorageInstance;
            int id = _machine.SelectedItemId;
            _stockText.text = (id > 0 && inv != null) ? $"창고 {inv.GetTotalItemCount(id)}개" : "";
        }

        // 드래그 중 추출 슬롯 강조 프레임 펄스(공장/인벤 드롭 강조와 동일 개념)
        if (_dropHighlight != null)
        {
            bool dragging = InventoryDragHandler.Instance != null && InventoryDragHandler.Instance.IsDragging;
            float target = dragging ? (0.32f + 0.20f * Mathf.Sin(Time.unscaledTime * 5f)) : 0f;
            var c = _dropHighlight.color;
            c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * (dragging ? 8f : 5f));
            _dropHighlight.color = c;
        }

        UpdateFlow();
    }

    // 선택 아이템이 실제로 배출될 때(출력 포트 앞 벨트에 새 인스턴스 등장) 슬롯 -> 벨트로 아이콘이 흐른다.
    // 타이머가 아니라 실제 벨트 점유자 인스턴스 변화로 발동(버퍼 밀림 시 desync 방지).
    private void UpdateFlow()
    {
        if (_flowIcon == null) return;

        if (_outPort != null)
        {
            object occ = BeltSegment.PortFrontOccupant(_outPort, out int occId);
            bool fire = occ != null && !ReferenceEquals(occ, _prevOcc);
            _prevOcc = occ; _prevOccId = occId;
            if (fire && occId > 0 && _flowT < 0f)
            {
                var d = ItemDatabase.GetItem(occId);
                var icon = d != null ? ItemDatabase.GetIcon(d.iconKey) : null;
                if (icon != null) { _flowIcon.sprite = icon; _flowT = 0f; }
            }
        }

        if (_flowT >= 0f)
        {
            _flowT += Time.unscaledDeltaTime;
            const float dur = 0.9f;
            float u = _flowT / dur;
            if (u >= 1f) { _flowT = -1f; _flowIcon.enabled = false; }
            else
            {
                _flowIcon.enabled = true;
                _flowIcon.rectTransform.anchoredPosition = Vector2.Lerp(_flowFrom, _flowTo, u);
                var c = _flowIcon.color; c.a = Mathf.Sin(u * Mathf.PI); _flowIcon.color = c;
            }
        }
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
        _titleText = NewText("Title", prt, "창고 출력 포트", 28, TxtDark, TextAlignmentOptions.Left);
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
        crt.sizeDelta = new Vector2(52, 52); crt.anchoredPosition = new Vector2(-14, -8);
        var cimg = closeGo.GetComponent<Image>(); cimg.color = new Color(0, 0, 0, 0);
        var cbtn = closeGo.GetComponent<Button>(); cbtn.onClick.AddListener(Close);
        var cx = NewText("X", closeGo.transform, "X", 26, TxtDark, TextAlignmentOptions.Center);
        cx.fontStyle = FontStyles.Bold; FillRect(cx.rectTransform);

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

        // 흐름 레인 밴드(좌->우 강조, 은은)
        var band = NewImage("FlowBand", area, new Color(72/255f, 205/255f, 255/255f, 0.05f));
        band.raycastTarget = false;
        band.rectTransform.anchoredPosition = new Vector2(0, flowY);
        band.rectTransform.sizeDelta = new Vector2(940, 210);

        // 중앙 홀로그램(도면 PNG 자리). Resources 에 있으면 자동 로드, 없으면 숨김(PNG 대기).
        _hologram = NewImage("Hologram", area, Color.white);
        _hologram.raycastTarget = false; _hologram.preserveAspect = true;
        _hologram.rectTransform.anchoredPosition = new Vector2(0, flowY);
        _hologram.rectTransform.sizeDelta = new Vector2(400, 400);
        var holo = Resources.Load<Sprite>("Image/UI_Icon/FacilityBlueprint/9_창고 출력 포트");
        if (holo != null) { _hologram.sprite = holo; _hologram.enabled = true; }
        else _hologram.enabled = false;

        // 좌측: "현재 출력" + 추출 품목 슬롯(보존)
        var cur = NewText("CurrentLabel", area, "현재 출력", 20, TxtMain, TextAlignmentOptions.Center);
        cur.rectTransform.anchoredPosition = new Vector2(-340, flowY + 120);
        cur.rectTransform.sizeDelta = new Vector2(200, 30);

        // 드래그 강조 프레임(슬롯 뒤, 드래그 중 표시) - 공장/인벤 드롭 강조와 동일 개념
        _dropHighlight = NewImage("DropHighlight", area, new Color(72/255f, 205/255f, 255/255f, 0f));
        _dropHighlight.sprite = UISpriteFactory.RoundedRect(64, 18);
        _dropHighlight.type = Image.Type.Sliced;
        _dropHighlight.raycastTarget = false;
        _dropHighlight.rectTransform.anchoredPosition = new Vector2(-340, flowY);
        _dropHighlight.rectTransform.sizeDelta = new Vector2(212, 212);

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
        }

        // 선택 아이템 창고 재고수(슬롯 아래) - 창고쪽 안 봐도 몇 개 남았는지 보이게
        _stockText = NewText("StockText", area, "", 18, new Color(72/255f, 205/255f, 255/255f, 0.95f), TextAlignmentOptions.Center);
        _stockText.fontStyle = FontStyles.Bold;
        _stockText.rectTransform.anchoredPosition = new Vector2(-340, flowY - 118);
        _stockText.rectTransform.sizeDelta = new Vector2(230, 30);

        // 방향 화살표(정적) - 슬롯->홀로그램, 홀로그램->벨트
        MakeChevron(area, new Vector2(-210, flowY));
        MakeChevron(area, new Vector2(210, flowY));

        // 우측: 레일 스트립(벨트) + "물류 출력"
        _flowRailsRoot = NewRect("FlowRailsRoot", area);
        _flowRailsRoot.anchoredPosition = new Vector2(340, flowY);
        _flowRailsRoot.sizeDelta = new Vector2(FR_BeltSize, FR_BeltSize);
        var outLbl = NewText("OutputLabel", area, "물류 출력", 18, TxtSub, TextAlignmentOptions.Center);
        outLbl.rectTransform.anchoredPosition = new Vector2(340, flowY + 120);
        outLbl.rectTransform.sizeDelta = new Vector2(200, 30);

        // 흐름 연출용 아이콘(기본 숨김). 슬롯 -> 벨트로 이동.
        _flowFrom = new Vector2(-340, flowY);
        _flowTo   = new Vector2(340, flowY);
        _flowIcon = NewImage("FlowIcon", area, Color.white);
        _flowIcon.raycastTarget = false; _flowIcon.preserveAspect = true; _flowIcon.enabled = false;
        _flowIcon.rectTransform.sizeDelta = new Vector2(72, 72);

        // 게이지 "다음 배출까지"
        var glabel = NewText("GaugeLabel", area, "다음 배출까지", 15, TxtSub, TextAlignmentOptions.Left);
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

        _statusText = NewText("StatusText", area, "아이템을 선택하세요", 17, TxtMain, TextAlignmentOptions.Left);
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
            "선택한 아이템 1종을 창고에서 자동으로 꺼내 벨트(물류 라인)로 내보내는 설비입니다.\n창고 입출력 라인에 붙여야만 배치할 수 있습니다.",
            20, TxtMain, TextAlignmentOptions.Center);
        ibody.textWrappingMode = TextWrappingModes.Normal;
        ibody.enableAutoSizing = true; ibody.fontSizeMin = 15f; ibody.fontSizeMax = 24f;   // 패널 크기에 꽉 차게
        var ibrt = ibody.rectTransform;
        ibrt.anchorMin = Vector2.zero; ibrt.anchorMax = Vector2.one; ibrt.offsetMin = new Vector2(22, 10); ibrt.offsetMax = new Vector2(-22, -10);
    }

    private void MakeChevron(Transform parent, Vector2 pos)
    {
        var t = NewText("Chevron", parent, ">", 44, new Color(72/255f, 205/255f, 255/255f, 0.5f), TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Bold;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = new Vector2(44, 54);
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

        var go = new GameObject("PortRailReal", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(_flowRailsRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(FR_BeltSize, FR_BeltSize);
        rt.anchoredPosition = Vector2.zero;
        var raw = go.GetComponent<RawImage>(); raw.texture = tex; raw.raycastTarget = false;
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
        _railTex = _railPortrait.Render(rbm.StraightRailPrefab, 256, 256);
        return _railTex;
    }
}
