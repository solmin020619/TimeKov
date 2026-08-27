using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TIMEKOV.Factory;

/// <summary>
/// 설비 UI의 연료 슬롯.
/// - 인벤토리 → 연료 슬롯 드래그&드랍 : 연료 투입
/// - 연료 슬롯 → 인벤토리 드래그&드랍 : 연료 회수
/// - 더블클릭 : 연료 전량 회수
/// </summary>
[RequireComponent(typeof(Image))]
public class FuelDropSlot : MonoBehaviour,
    IDropHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler
{
    [SerializeField] private Image           iconImage;
    [SerializeField] private Image           borderImage;
    [SerializeField] private TextMeshProUGUI amountText; // 스택 수 (예: "x3")
    [SerializeField] private TextMeshProUGUI timeText;   // 남은 가동 시간 (예: "80초")
    [SerializeField] private TextMeshProUGUI labelText;  // hover 시 "연료 넣기" 표시
    [SerializeField] private Image           fuelGauge;  // 현재 연료 1개분 남은 연소 비율 바(#41)
    [SerializeField] private Image           gradeAurora; // 연료색 오로라(하단 그라데이션, 인벤 슬롯과 동일. GradeVisual.FuelColor)

    [Header("Hover 색상")]
    [SerializeField] private Color normalBorderColor = new Color(1f, 1f, 1f, 0f);
    // 옛 노란 통짜(1, 0.8, 0.2, 0.8)는 화면이 노랗게 떡져서 폐기 -> 은은한 시안 워시 + 물결 프레임이 강조 담당.
    [SerializeField] private Color hoverBorderColor  = new Color(0.37f, 0.77f, 1f, 0.14f);

    [Header("드래그 강조 프레임")]
    [Tooltip("통합 인벤과 동일한 hl_region_frame 스프라이트(빌더 배선). 비우면 시안 4변 폴백.")]
    [SerializeField] private Sprite highlightFrameSprite;
    private GameObject _highlightFrame;
    [SerializeField] private Color normalTextColor   = new Color(1f, 1f, 1f, 1f);

    [Header("빈 슬롯 미리보기")]
    [Tooltip("연료가 없을 때 연료 아이템을 검은색 실루엣으로 미리 표시하는 색상.")]
    [SerializeField] private Color emptySilhouetteColor = new Color(0f, 0f, 0f, 1f);

    // ── 드래그 아웃 static 상태 (InventoryPanelDropZone이 읽어감) ──────
    public static bool             IsFuelDragging { get; private set; }
    public static MachineBase      DragMachine   { get; private set; }
    public static InventoryManager DragInventory { get; private set; }
    public static int              DragFuelCount { get; private set; }
    public static int              DragFuelItemId{ get; private set; }

    private static GameObject _dragVisual;

    // ── 더블클릭 감지 ──────────────────────────────────────────────────
    private float _lastClickTime = -1f;
    private const float DoubleClickThreshold = 0.3f;

    private MachineBase       _machine;
    private InventoryManager  _inventory;
    private Canvas            _canvas;
    private bool              _dragHighlighted; // 인벤 드래그 시작으로 강조 중인지

    // ── 화로(버너) 패널: 슬롯 오른쪽 빈 공간에 연소 상태를 크게 표시 ──
    //   도화선(fuse) 게이지 = 게이지 자체가 "타들어가는 연료". fill 끝점에 잉걸불 글로우가 일렁이고
    //   끝단 구간은 밝게(뜨거운 부분). 불꽃 아이콘 없음(물방울로 읽혀 폐기). + "나뭇가지 xN - M초" / 빈 상태 안내.
    //   런타임 생성(빌더 재실행 불필요). 큰 박스 없이 요소만 얹는다(단일 면 원칙). 슬롯의 수량/시간 텍스트는 여기로 이사.
    private RectTransform _burnerRt;
    private Image _burnFill, _emberGlow, _emberCore;
    private Image _burnItemIcon;    // 지금 타는 1개의 아이콘(도화선 = 항상 나뭇가지 1개 분)
    private TextMeshProUGUI _burnInfo;
    private const float BurnerGaugeX = 16f;    // 게이지 시작 x(패널 안)
    private const float BurnerGaugeW = 330f;   // 도화선 게이지 길이

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        GetComponent<Image>().raycastTarget = true;

        // 초기 상태 명시적 초기화
        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";

        // 드래그 대상 강조 프레임(물결) - 통합 인벤과 동일 문법.
        _highlightFrame = DropHighlightFrame.Build((RectTransform)transform, highlightFrameSprite);
    }

    private void SetFrame(bool on)
    {
        if (_highlightFrame != null && _highlightFrame.activeSelf != on) _highlightFrame.SetActive(on);
    }

    private void OnDisable()
    {
        IsFuelDragging = false;
        if (_dragVisual != null) { Destroy(_dragVisual); _dragVisual = null; }
    }

    // ── 초기화 / 정리 ───────────────────────────────────────────────────

    public void Setup(MachineBase machine, InventoryManager inventory)
    {
        if (_machine != null) _machine.OnFuelChanged -= OnFuelChanged;

        _machine   = machine;
        _inventory = inventory != null ? inventory : InventoryManager.Instance;

        _machine.OnFuelChanged += OnFuelChanged;

        // UI 초기 상태 리셋
        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";

        // fuelGauge = 재료 슬롯의 GradeBar 와 '동일한' 플레인 솔리드 선으로 교정(빌더 RoundedSprite+Filled 라 좌우 공백 -> sprite 제거 + Simple).
        //   색/표시(주황 굵은선 + 위로 은은한 주황 오로라)는 RefreshTime 이 담당.
        if (fuelGauge != null)
        {
            fuelGauge.sprite = null;
            fuelGauge.type   = Image.Type.Simple;
        }

        EnsureBurnerPanel();
        RefreshIcon();
        RefreshTime();
    }

    // ── 화로 패널 생성/갱신 ─────────────────────────────────────────────

    private void EnsureBurnerPanel()
    {
        if (_burnerRt != null) return;
        var slotRt = transform as RectTransform;
        if (slotRt == null || slotRt.parent == null) return;

        var go = new GameObject("FuelBurnerPanel", typeof(RectTransform));
        go.transform.SetParent(slotRt.parent, false);
        _burnerRt = (RectTransform)go.transform;
        _burnerRt.anchorMin = slotRt.anchorMin; _burnerRt.anchorMax = slotRt.anchorMax;
        _burnerRt.pivot = new Vector2(0f, 0f);
        _burnerRt.sizeDelta = new Vector2(380f, slotRt.sizeDelta.y);
        _burnerRt.anchoredPosition = slotRt.anchoredPosition + new Vector2(slotRt.sizeDelta.x + 16f, 0f);

        // 슬롯과 화로 사이 얇은 세로 구분선(박스 대신 선 - 단일 면 원칙)
        MakeBurnerImage("Divider", new Vector2(-10f, 0f), new Vector2(1.5f, 96f), new Color(0.55f, 0.62f, 0.70f, 0.25f), null);

        // 상태 라인: [타는 중인 나뭇가지 1개 아이콘] + "연소 중 - 31초" / 빈 상태 안내.
        _burnItemIcon = MakeBurnerImage("BurnItem", new Vector2(BurnerGaugeX, 16f), new Vector2(26f, 26f), Color.white, null);
        _burnItemIcon.preserveAspect = true;
        _burnItemIcon.enabled = false;

        var info = new GameObject("Info", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        info.transform.SetParent(_burnerRt, false);
        var irt = info.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f); irt.pivot = new Vector2(0f, 0.5f);
        irt.sizeDelta = new Vector2(330f, 26f); irt.anchoredPosition = new Vector2(BurnerGaugeX, 16f);
        if (timeText != null) info.font = timeText.font;   // 한글 폰트 유지
        info.fontSize = 15; info.alignment = TextAlignmentOptions.Left; info.raycastTarget = false;
        _burnInfo = info;

        // 도화선 게이지: 트랙 + 단색 fill + 잉걸불 글로우 2겹(끝점, 가동 중 일렁임). 색 구간 분리 없음(두 색으로 보여서 폐기).
        MakeBurnerImage("FuseTrack", new Vector2(BurnerGaugeX, -14f), new Vector2(BurnerGaugeW, 6f), new Color(1f, 1f, 1f, 0.14f), null);
        _burnFill = MakeBurnerImage("FuseFill", new Vector2(BurnerGaugeX, -14f), new Vector2(0f, 6f), Color.white, null);
        _emberGlow = MakeBurnerImage("EmberGlow", new Vector2(BurnerGaugeX, -14f), new Vector2(26f, 26f), new Color(1f, 0.55f, 0.18f, 0.55f), UISpriteFactory.Disc(64));
        _emberCore = MakeBurnerImage("EmberCore", new Vector2(BurnerGaugeX, -14f), new Vector2(10f, 10f), new Color(1f, 0.93f, 0.72f, 0.95f), UISpriteFactory.Disc(64));
        _emberGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _emberCore.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _emberGlow.enabled = _emberCore.enabled = false;
    }

    private Image MakeBurnerImage(string name, Vector2 pos, Vector2 size, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_burnerRt, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = color; img.raycastTarget = false;
        if (sprite != null) img.sprite = sprite;
        return img;
    }

    // 곧 꺼진다는 경고. 대기 연료가 없고 지금 타는 1개도 이 비율 밑으로 남으면 도화선이 서서히 붉어진다.
    //   ★깜빡이지 않는다. 움직이는 경고는 시야에 계속 걸려서 거슬리고, 색만 바뀌어도 상태는 그대로 전달된다.
    //   대기 연료가 있으면 경고하지 않는다 - 어차피 이어서 탄다.
    private const float FuelWarnBelow = 0.30f;
    private static readonly Color FuelWarnColor  = new Color(0.95f, 0.27f, 0.20f, 1f);
    private static readonly Color EmberGlowNormal = new Color(1f, 0.55f, 0.18f, 0.55f);
    private static readonly Color EmberGlowWarn   = new Color(1f, 0.28f, 0.18f, 0.75f);

    private void UpdateBurnerPanel(float t, float secs, float currentTime, int queued)
    {
        if (_burnerRt == null) return;
        bool lit = t > 0f;
        Color fc = GradeVisual.FuelColor;
        float left = secs > 0.01f ? Mathf.Clamp01(currentTime / secs) : 0f;
        float fillW = lit ? BurnerGaugeW * left : 0f;

        // 0 = 평소, 1 = 다 타감. 마지막 30% 구간에서 0 -> 1 로 올라간다.
        float warnK = (lit && queued <= 0 && left < FuelWarnBelow)
            ? 1f - (left / FuelWarnBelow)
            : 0f;
        Color burnColor = Color.Lerp(fc, FuelWarnColor, warnK);

        // 도화선: fill 이 오른쪽에서 왼쪽으로 타들어간다. 끝단은 밝게(뜨거움), 끝점엔 잉걸불.
        if (_burnFill != null)
        {
            _burnFill.color = Color.Lerp(burnColor, Color.white, 0.15f);
            _burnFill.rectTransform.sizeDelta = new Vector2(fillW, 6f);
        }
        if (_emberGlow != null && _emberCore != null)
        {
            Vector2 tip = new Vector2(BurnerGaugeX + fillW, -14f);
            _emberGlow.rectTransform.anchoredPosition = tip;
            _emberCore.rectTransform.anchoredPosition = tip;
            _emberGlow.color = Color.Lerp(EmberGlowNormal, EmberGlowWarn, warnK);
            if (_emberGlow.enabled != lit) { _emberGlow.enabled = lit; _emberCore.enabled = lit; }
        }

        // 상태 라인: 도화선은 항상 "지금 타는 1개"만 대표한다(수량은 투입구 슬롯 담당).
        if (_burnItemIcon != null)
        {
            if (lit && _burnItemIcon.sprite == null)
            {
                var cfg0 = FuelConfig.Instance;
                var d0 = cfg0 != null ? GameDataUtility.GetItem(cfg0.fuelItemId) : null;
                _burnItemIcon.sprite = d0 != null ? ItemDatabase.GetIcon(d0.iconKey) : null;
            }
            _burnItemIcon.enabled = lit && _burnItemIcon.sprite != null;
        }
        if (_burnInfo != null)
        {
            string fuelName = Loc.Get("연료");
            var cfg = FuelConfig.Instance;
            if (cfg != null)
            {
                var d = GameDataUtility.GetItem(cfg.fuelItemId);
                if (d != null) fuelName = d.GetLocalizedName();
            }
            bool iconShown = _burnItemIcon != null && _burnItemIcon.enabled;
            _burnInfo.rectTransform.anchoredPosition = new Vector2(BurnerGaugeX + (iconShown ? 34f : 0f), 16f);
            if (lit)
            {
                _burnInfo.text = Loc.Get("연소 중 -") + "  " + $"{currentTime:F0}" + Loc.Get("초");
                // 남은 시간 글자도 같이 붉어진다(도화선과 같은 신호를 두 번 준다).
                _burnInfo.color = Color.Lerp(new Color(0.92f, 0.94f, 0.97f, 0.95f), FuelWarnColor, warnK);
            }
            else
            {
                _burnInfo.text = Loc.Get("연료 없음 -") + "  " + fuelName + " " + Loc.Get("필요");
                _burnInfo.color = new Color(0.80f, 0.58f, 0.52f, 0.85f);
            }
        }
    }

    public void Cleanup()
    {
        if (_machine != null) _machine.OnFuelChanged -= OnFuelChanged;
        _machine = null;
    }

    private void OnDestroy() => Cleanup();

    // ── 매 프레임 — 남은 시간 갱신 ─────────────────────────────────────

    private void Update()
    {
        if (_machine == null) return;
        bool processing = _machine.Status == MachineStatus.Processing;
        if (processing)
            RefreshTime();

        // 잉걸불 일렁임(가동 중에만). 멈추면 원래 크기/밝기로.
        if (_emberGlow != null && _emberCore != null && _emberGlow.enabled)
        {
            if (processing && _machine.FuelTimeRemaining > 0f)
            {
                float n1 = Mathf.PerlinNoise(Time.unscaledTime * 6f, 0.37f) - 0.5f;
                float n2 = Mathf.PerlinNoise(Time.unscaledTime * 9f, 1.73f) - 0.5f;
                _emberGlow.rectTransform.localScale = Vector3.one * (1f + 0.30f * n1);
                _emberCore.rectTransform.localScale = Vector3.one * (1f + 0.40f * n2);
                var gc = _emberGlow.color; gc.a = 0.45f + 0.20f * (n1 + 0.5f); _emberGlow.color = gc;
            }
            else if (_emberGlow.rectTransform.localScale != Vector3.one)
            {
                _emberGlow.rectTransform.localScale = Vector3.one;
                _emberCore.rectTransform.localScale = Vector3.one;
                var gc = _emberGlow.color; gc.a = 0.55f; _emberGlow.color = gc;
            }
        }
    }

    // ── 이벤트 콜백 ────────────────────────────────────────────────────

    private void OnFuelChanged() => RefreshTime();

    // ── UI 갱신 ─────────────────────────────────────────────────────────

    private void RefreshTime()
    {
        if (_machine == null) return;

        float t    = _machine.FuelTimeRemaining;
        // secs 는 static(FuelConfig.SecondsPerFuel) - 우주선 수리 연료효율 오버라이드까지 반영해
        //   AddFuel/소모와 같은 기준으로 개수를 역산한다. instance(cfg.secondsPerFuel)는 오버라이드를
        //   무시해서, 효율 보상 활성 시 표시 개수가 실제보다 뻥튀기됐다(37 투입 -> 46 표시 버그).
        float secs = FuelConfig.SecondsPerFuel;

        // 현재 연소 중인 1개를 제외한 대기 중 아이템 수
        // CeilToInt(t/secs) = 전체 아이템 수 → -1 = 대기 중인 것만
        int queued = t > 0f ? Mathf.Max(0, Mathf.CeilToInt(t / secs) - 1) : 0;

        // 현재 아이템의 남은 연소 시간 (0 ~ secondsPerFuel 범위)
        // t가 정확히 secs의 배수일 때 % 결과가 0이 되므로 secs로 보정
        float currentTime = t % secs;
        if (currentTime < 0.01f && t > 0f) currentTime = secs;

        // 투입구 모델: 슬롯 = 대기 중인 연료(xN). 타는 중인 1개는 옆 도화선이 담당.
        if (amountText != null)
            amountText.text = queued > 0 ? $"x{queued}" : "";

        if (timeText != null)
        {
            if (_burnerRt == null && t > 0f)
            {
                // 현재 아이템 1개분의 남은 시간만 표시
                timeText.text  = $"{currentTime:F0}" + Loc.Get("초");
                timeText.color = normalTextColor;
                timeText.enabled = true;
            }
            else
            {
                // 연료 없음은 MachineUI.statusText가 "연료 부족"으로 표시 -> 여기선 숨김(겹침 방지)
                timeText.text  = "";
                timeText.enabled = false;
            }
        }

        // 재료 슬롯과 동일 2겹: (1) 바닥 솔리드 '굵은 선'(fuelGauge - Setup 서 플레인 솔리드로 교정=좌우 공백 없음) + (2) 위로 은은한 오로라.
        if (fuelGauge != null)
        {
            fuelGauge.color   = new Color(1f, 0.5f, 0.12f, 1f);   // 주황 굵은선 (재료 GradeBar 와 동일 위치=바닥 h6)
            fuelGauge.enabled = queued > 0;
        }
        // 오로라(선 위로 은은하게) = 재료와 동일 alpha 0.6, 연료는 주황.
        if (gradeAurora != null)
            gradeAurora.color = queued > 0 ? new Color(1f, 0.5f, 0.12f, 0.6f) : new Color(0f, 0f, 0f, 0f);

        // 화로 패널(불꽃/와이드 게이지/상태 라인) 동기.
        UpdateBurnerPanel(t, secs, currentTime, queued);

        // 연료 상태 변경 시 아이콘도 갱신
        RefreshIcon();
    }

    private void RefreshIcon()
    {
        if (iconImage == null) return;

        var cfg = FuelConfig.Instance;

        // 연료 아이템 아이콘 미리 로드 (빈 슬롯 실루엣 표시용으로도 사용)
        Sprite sprite = null;
        if (cfg != null)
        {
            var itemData = GameDataUtility.GetItem(cfg.fuelItemId);
            sprite = itemData != null ? ItemDatabase.GetIcon(itemData.iconKey) : null;
        }

        // 연료가 없으면 연료 아이템을 검은색 실루엣으로 미리 표시 (연료가 들어가면 원래 아이콘)
        if (cfg == null || _machine == null || _machine.FuelTimeRemaining <= 0f)
        {
            if (sprite != null)
            {
                iconImage.sprite  = sprite;
                iconImage.color   = emptySilhouetteColor;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
            return;
        }

        // 현재 연소 중인 1개를 제외한 대기 아이템 수
        float t      = _machine.FuelTimeRemaining;
        float secs   = FuelConfig.SecondsPerFuel;   // static (오버라이드 반영) - instance 는 표시 개수 어긋남
        int   queued = Mathf.Max(0, Mathf.CeilToInt(t / secs) - 1);

        if (queued <= 0)
        {
            // 투입구 모델: 대기 연료가 없으면 빈 칸(실루엣). 타는 중인 1개는 옆 도화선이 보여주므로 모순 없음.
            if (sprite != null)
            {
                iconImage.sprite  = sprite;
                iconImage.color   = emptySilhouetteColor;
                iconImage.enabled = true;
            }
            else iconImage.enabled = false;
            return;
        }

        iconImage.sprite  = sprite;
        iconImage.color   = Color.white;
        iconImage.enabled = sprite != null;
    }

    // ── 드래그 시작 강조 (MachineUI가 인벤 드래그 시작 시 호출) ──────────

    /// <summary>이 슬롯이 받는 아이템 ID (연료).</summary>
    public int AcceptedItemId => FuelConfig.Instance != null ? FuelConfig.Instance.fuelItemId : -1;

    /// <summary>"연료 넣기" 프롬프트(호버/드래그 강조)가 떠 있는지 = labelText 비어있지 않음.
    /// MachineUI가 같은 자리의 "연료 부족" 경고와 겹치지 않게 조회한다.</summary>
    public bool IsInsertPromptVisible => labelText != null && !string.IsNullOrEmpty(labelText.text);

    /// <summary>인벤토리에서 연료를 집어든 순간 드랍 대상임을 미리 강조한다.</summary>
    public void SetDragHighlight(bool on)
    {
        _dragHighlighted = on;
        if (borderImage != null) borderImage.color = on ? hoverBorderColor : normalBorderColor;
        if (labelText   != null) labelText.text    = on ? Loc.Get("연료 넣기") : "";
        SetFrame(on);
    }

    // ── Hover (인벤토리 → 연료슬롯 방향) ────────────────────────────────

    public void OnPointerEnter(PointerEventData e)
    {
        // 아이템 정보 툴팁 (인벤토리/창고와 동일) — 연료가 들어있을 때만
        var fcfg = FuelConfig.Instance;
        if (fcfg != null && _machine != null && _machine.FuelItemCount > 0)
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            ItemTooltipUI.Instance?.Show(fcfg.fuelItemId, _canvas);
        }

        bool isDragging = InventoryDragHandler.Instance != null
                       && InventoryDragHandler.Instance.IsDragging;
        if (!isDragging) return;

        var cfg = FuelConfig.Instance;
        if (cfg == null) return;

        // 라이브 시각 칸 대신 스냅샷으로 판정(뷰 전환 재바인딩 후 시각 칸은 딴 아이템일 수 있다).
        var dh = InventoryDragHandler.Instance;
        if (!dh.SourceStillValid()) return;
        if (dh.SrcItemId != cfg.fuelItemId) return;

        if (borderImage != null) borderImage.color = hoverBorderColor;
        if (labelText   != null) labelText.text    = Loc.Get("연료 넣기");
        SetFrame(true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        ItemTooltipUI.Instance?.Hide();

        // 드래그 강조 중이면 커서가 벗어나도 강조 유지
        if (_dragHighlighted)
        {
            if (borderImage != null) borderImage.color = hoverBorderColor;
            if (labelText   != null) labelText.text    = Loc.Get("연료 넣기");
            return;
        }
        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";
        SetFrame(false);
    }

    // ── Drop (인벤토리 → 연료슬롯) ───────────────────────────────────────

    public void OnDrop(PointerEventData e)
    {
        _dragHighlighted = false;
        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";
        SetFrame(false);

        if (_machine == null) return;

        var handler = InventoryDragHandler.Instance;
        if (handler == null || !handler.IsDragging) return;
        // (라이브 DraggedSlot 게이트는 안 둔다 - 뷰 전환 재바인딩 후 시각 칸이 비어 보여도
        //  실제 출발 칸은 살아 있을 수 있다. 진실은 아래 SourceStillValid 스냅샷 검사가 가린다)

        var cfg = FuelConfig.Instance;
        if (cfg == null) { Debug.LogWarning("[FuelDropSlot] FuelConfig 없음."); return; }

        // 라이브 시각 칸 대신 박제한 출발 슬롯 사용(컴팩트 표시에서 드래그 중 재렌더로 엉뚱한 아이템 차감 방지).
        if (!handler.SourceStillValid()) return;
        int itemId = handler.SrcItemId;
        if (itemId != cfg.fuelItemId) { ToastManager.Warning(Loc.Get("연료만 넣을 수 있습니다")); return; }

        var inv      = handler.SrcManager;
        int srcIndex = handler.SrcSlotIndex;
        int have     = inv.GetSlot(srcIndex).amount;
        int amount   = handler.IsSplitDrag ? Mathf.Min(handler.DragAmount, have) : have;   // ALT 분할 드래그 = 든 수량만
        if (amount <= 0) return;

        // 같은 아이템이 여러 칸에 나뉘어 있어도 실제로 드래그한 칸에서만 차감
        if (!inv.RemoveFromSlot(srcIndex, amount)) return;
        inv.ForceRefreshUI();
        _machine.AddFuel(amount);
        RefreshTime();
    }

    // ── 더블클릭 — 전량 회수 ────────────────────────────────────────────

    public void OnPointerClick(PointerEventData e)
    {
        if (_machine == null || _machine.FuelItemCount <= 0) return;

        float now = Time.unscaledTime;
        if (now - _lastClickTime < DoubleClickThreshold)
        {
            _lastClickTime = -1f;
            ReturnFuelToInventory();
        }
        else
        {
            _lastClickTime = now;
        }
    }

    // ── 드래그 아웃 (연료슬롯 → 인벤토리) ──────────────────────────────

    public void OnBeginDrag(PointerEventData e)
    {
        if (_machine == null || _machine.FuelItemCount <= 0)
        {
            e.pointerDrag = null;
            return;
        }

        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();

        var cfg = FuelConfig.Instance;
        if (cfg == null) { e.pointerDrag = null; return; }

        IsFuelDragging = true;
        DragMachine    = _machine;
        DragInventory  = _inventory;
        DragFuelCount  = _machine.FuelItemCount;
        DragFuelItemId = cfg.fuelItemId;

        // 드래그 고스트 이미지
        _dragVisual = new GameObject("FuelDragVisual");
        _dragVisual.transform.SetParent(_canvas.transform, false);
        _dragVisual.transform.SetAsLastSibling();

        var rt  = _dragVisual.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(64f, 64f);

        var img = _dragVisual.AddComponent<Image>();
        img.sprite       = iconImage != null ? iconImage.sprite : null;
        img.color        = Color.white;
        img.raycastTarget = false;

        var cg = _dragVisual.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.alpha          = 0.85f;
    }

    public void OnDrag(PointerEventData e)
    {
        // 우클릭으로 드래그 취소 시 고스트 강제 정리
        if (Input.GetMouseButton(1)) { OnEndDrag(e); return; }

        if (_dragVisual == null || _canvas == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            e.position,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
            out Vector2 localPos))
        {
            _dragVisual.transform.localPosition = localPos;
        }
    }

    public void OnEndDrag(PointerEventData e)
    {
        IsFuelDragging = false;
        if (_dragVisual != null) { Destroy(_dragVisual); _dragVisual = null; }
    }

    // ── 내부 회수 메서드 ─────────────────────────────────────────────────

    private void ReturnFuelToInventory()
    {
        if (_machine == null) return;

        var cfg = FuelConfig.Instance;
        if (cfg == null) return;

        int count = _machine.TakeFuel();
        if (count <= 0) return;

        var inv = _inventory != null ? _inventory : InventoryManager.Instance;
        // 못 들어간 만큼은 설비 연료로 되돌림 - 가방 가득 시 증발 방지.
        int leftover = inv != null ? inv.AddItem(cfg.fuelItemId, count) : count;
        if (leftover > 0)
        {
            _machine.AddFuel(leftover);
            ToastManager.Warning(Loc.Get("가방이 가득 찼습니다"));
        }
        inv?.ForceRefreshUI();

        // 회수로 슬롯이 비었으므로 호버 툴팁 즉시 숨김 (마우스가 그대로면 Exit가 안 뜸)
        ItemTooltipUI.Instance?.Hide();

        RefreshTime();
    }
}
