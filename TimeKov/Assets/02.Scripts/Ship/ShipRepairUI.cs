using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 폐우주선 수리 패널 컨트롤러 (풀스크린 대형, 공장풍 프로스티드 + 우주선 홀로그램 링게이지).
// 패널 골격은 에디터 빌더(ShipRepairUIBuilder)가 만들고 ref 로 연결한다.
// 레벨 사다리(pip)와 수리 단계 로드맵 행은 레벨 수가 가변이라 런타임에 이 컨트롤러가 생성한다.
// 부품은 단일 종류+개수제 - 데이터/조작은 ShipRepairManager 로만 (PartCount/CanRepairNext/TryRepairNext/OnChanged).
public class ShipRepairUI : MonoBehaviour
{
    public static ShipRepairUI Instance { get; private set; }

    /// <summary>F로 닫은 프레임 — 터미널이 같은 입력으로 재오픈하는 깜빡임 방지.</summary>
    public static int LastCloseFrame { get; private set; } = -10;

    /// <summary>패널 호스트를 에디터에서 안 보이게 꺼둬도(비활성) 런타임에 찾아 활성화하고 Instance 를 보장한다.
    /// 씬에 패널 자체가 없으면(빌더 미실행) null.</summary>
    public static ShipRepairUI EnsureInstance()
    {
        if (Instance != null) return Instance;
        var found = FindObjectsByType<ShipRepairUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (found.Length == 0) return null;
        var ui = found[0];
        if (!ui.gameObject.activeSelf) ui.gameObject.SetActive(true);   // SetActive(true) -> Awake 동기 실행 -> Instance 세팅
        return Instance != null ? Instance : ui;
    }

    [Header("패널 (빌더가 연결)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;

    [Header("헤더 / 사다리")]
    [SerializeField] private TextMeshProUGUI levelText;      // "수리 단계 Lv.N / M"
    [SerializeField] private RectTransform pipContainer;     // 레벨 pip 부모 (HorizontalLayoutGroup)

    [Header("홀로그램 (복원도)")]
    [SerializeField] private Image ringGauge;                // Filled Radial360, 복원도 비율
    [SerializeField] private TextMeshProUGUI restorePercentText;
    [SerializeField] private RectTransform scanLine;         // 주사선 (열려있는 동안 위아래 왕복)

    [Header("다음 수리 / 스탯")]
    [SerializeField] private TextMeshProUGUI nextHeaderText; // "다음 수리  Lv.N -> Lv.N+1"
    [Tooltip("[0]건축 범위 [1]설비 연료 [2]공장 가동속도 의 값 텍스트")]
    [SerializeField] private TextMeshProUGUI[] statValueTexts;

    [Header("부품")]
    [SerializeField] private RectTransform partsContent;     // 부품 카드 부모 (VerticalLayoutGroup)

    [Header("수리 버튼")]
    [SerializeField] private Button repairButton;
    [SerializeField] private TextMeshProUGUI repairButtonText;

    private static readonly Color Holo      = new Color(0.42f, 0.83f, 1.00f, 1f);
    private static readonly Color HoloSoft  = new Color(0.42f, 0.83f, 1.00f, 0.16f);
    private static readonly Color TextMain  = new Color(0.82f, 0.89f, 0.96f, 1f);
    private static readonly Color TextDim   = new Color(0.44f, 0.51f, 0.60f, 1f);
    private static readonly Color DoneCol   = new Color(0.36f, 0.84f, 0.56f, 1f);
    private static readonly Color PipEmpty  = new Color(0.30f, 0.36f, 0.44f, 1f);
    private static readonly Color BtnReady  = new Color(0.20f, 0.66f, 0.95f, 1f);
    private static readonly Color BtnLocked = new Color(0.16f, 0.20f, 0.28f, 0.95f);

    private readonly List<Image> _pips = new();
    private TextMeshProUGUI _partCardName;     // 아이콘 아래 부품 이름
    private TextMeshProUGUI _partCardSub;      // 다음 수리 필요 개수 안내
    private TextMeshProUGUI _partOwnedText;    // 좌측 큰 숫자 (보유)
    private TextMeshProUGUI _partReqText;      // 우측 큰 숫자 (필요)
    private Image _partCardGauge;              // 진행 게이지 (보유/필요)
    private RectTransform _partRingA;          // 아이콘 아크 링 (DriveAmbient 가 회전)
    private RectTransform _partRingB;          // 바깥 얇은 역회전 링
    private Image _partRingImgA, _partRingImgB;
    private Image _partIconGlow;               // 아이콘 뒤 후광 (DriveAmbient 가 맥동)
    private float _partGlowBaseA = 0.10f;      // 상태별 후광 기준 알파 (맥동의 중심값)
    private bool _dynamicBuilt;
    private int  _openedFrame = -1;
    private Image _holoGlowImg;      // 링 뒤 후광 (맥동 연출용)
    private Image _shipSlotImg;      // 우주선 홀로그램 아트 슬롯 (PNG 오면 자동 연결)
    private float _ringTarget;       // 링 게이지 목표치 (DriveAmbient 가 부드럽게 채움)

    // 디자인 PNG (Resources/ShipRepair/, 없으면 절차 스프라이트 폴백) - Awake 에서 1회 로드
    private Sprite _nodeOnSpr, _nodeOffSpr;   // 6/ 레벨 노드
    private Sprite _rowSpr, _rowHlSpr;        // 3/ 부품 행 패널 (일반/강조)

    // ── 라이프사이클 ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        closeButton?.onClick.AddListener(Close);
        repairButton?.onClick.AddListener(OnClickRepair);
        panelRoot?.SetActive(false);

        // 디자인 PNG 로드 (없으면 null = 절차 폴백)
        _nodeOnSpr  = Resources.Load<Sprite>("ShipRepair/6/node_on");
        _nodeOffSpr = Resources.Load<Sprite>("ShipRepair/6/node_off");
        _rowSpr     = Resources.Load<Sprite>("ShipRepair/3/row_panel");
        _rowHlSpr   = Resources.Load<Sprite>("ShipRepair/3/row_panel_hl");

        ReapplyRuntimeSprites();
        SetupButtonFeedback();
    }

    // 호버 반응 (버튼=사운드+틴트+확대 / 행 패널=확대+밝아짐). 씬 세팅 없이 코드로 부착 - 빌더 재실행 불필요.
    private void SetupButtonFeedback()
    {
        AddHoverSound(closeButton);
        AddHoverSound(repairButton);

        // 버튼 확대 (밝기는 Button ColorTint 가 담당하므로 scaleOnly)
        if (closeButton != null)  AddHoverFx(closeButton.gameObject, 1.08f, scaleOnly: true);
        if (repairButton != null) AddHoverFx(repairButton.gameObject, 1.02f, scaleOnly: true);

        // 스탯 3행: 호버 시 살짝 확대+밝아짐 (행 Image 가 포인터를 받도록 raycast 켬)
        if (statValueTexts != null)
        {
            foreach (var t in statValueTexts)
            {
                var rowTr = t != null ? t.transform.parent : null;
                if (rowTr == null) continue;
                var img = rowTr.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
                AddHoverFx(rowTr.gameObject, 1.015f, scaleOnly: false);
            }
        }

        // 수리 버튼: 유니티 기본 틴트는 호버 변화가 거의 안 보임 -> 닫기 버튼과 같은 명시 틴트.
        // 잠금(부품 부족) 어둡기는 BtnLocked 이미지색이 담당하므로 disabled 틴트는 중립(이중 어둡힘 방지).
        if (repairButton != null)
        {
            repairButton.transition = Selectable.Transition.ColorTint;
            var cb = repairButton.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.selectedColor    = Color.white;
            cb.disabledColor    = Color.white;
            cb.colorMultiplier  = 1f; cb.fadeDuration = 0.1f;
            repairButton.colors = cb;
        }
    }

    private static void AddHoverSound(Button btn)
    {
        if (btn == null) return;
        var trig = btn.gameObject.GetComponent<EventTrigger>();
        if (trig == null) trig = btn.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ =>
        {
            if (btn.interactable) UISoundManager.Instance?.PlayButtonHover();   // 잠긴 버튼은 무음
        });
        trig.triggers.Add(entry);
    }

    private static void AddHoverFx(GameObject go, float scale, bool scaleOnly)
    {
        if (go == null) return;
        var fx = go.GetComponent<ShipUIHoverFx>();
        if (fx == null) fx = go.AddComponent<ShipUIHoverFx>();
        fx.hoverScale = scale;
        fx.scaleOnly = scaleOnly;
    }

    // 빌더가 박은 UISpriteFactory 스프라이트(런타임 생성물)는 에디터 재시작 시 참조가
    // 소실될 수 있어(에셋 아님) 절차 스프라이트를 쓰는 비주얼 전부를 여기서 재적용해 보장한다.
    // (PNG 에셋 참조는 씬에 영속이라 재적용 불필요 - 소실 가능한 것만.)
    private void ReapplyRuntimeSprites()
    {
        // 링: 디자인 PNG(5/) 우선. 절차 링과 PNG 링은 굵기/반경이 달라 반드시 짝으로만 사용.
        if (ringGauge != null)
        {
            var fillSpr  = Resources.Load<Sprite>("ShipRepair/5/ring_fill");
            var trackSpr = Resources.Load<Sprite>("ShipRepair/5/ring_track");
            bool ringArt = fillSpr != null && trackSpr != null;
            ringGauge.sprite = ringArt ? fillSpr : UISpriteFactory.Ring(400, 13f);
            ringGauge.color  = Holo;
            var holoParent = ringGauge.transform.parent;
            var track = holoParent != null ? holoParent.Find("RingTrack") : null;
            if (track != null && track.TryGetComponent(out Image trackImg))
            {
                trackImg.sprite = ringArt ? trackSpr : UISpriteFactory.Ring(400, 13f);
                trackImg.color  = ringArt ? Color.white : new Color(70f / 255f, 96f / 255f, 128f / 255f, 0.35f);
            }
            var glow = holoParent != null ? holoParent.Find("HoloGlow") : null;
            if (glow != null && glow.TryGetComponent(out Image glowImg))
            {
                glowImg.sprite = UISpriteFactory.Disc(256);
                _holoGlowImg = glowImg;   // 맥동 연출 대상
            }
        }

        // 스캔라인: 위 투명 -> 아래 발광 페이드 (소실 시 흰 막대가 왕복하는 사고 방지)
        if (scanLine != null && scanLine.TryGetComponent(out Image scanImg))
        {
            scanImg.sprite = UISpriteFactory.VFade(0, 40);
            scanImg.color  = Holo;
        }

        if (panelRoot != null)
        {
            var backGlow = panelRoot.transform.Find("HoloBackGlow");
            if (backGlow != null && backGlow.TryGetComponent(out Image bgImg)) bgImg.sprite = UISpriteFactory.Disc(256);

            // "다음 수리" 헤더 배지 (항상 절차 그라데이션)
            var nh = panelRoot.transform.Find("Content/RightCol/NextHeaderBg");
            if (nh != null && nh.TryGetComponent(out Image nhImg))
                nhImg.sprite = UISpriteFactory.RoundedRectVGrad(new Color32(28, 40, 56, 235), new Color32(12, 18, 28, 235), 64, 18);

            // 배경: PNG 로 빌드된 씬은 에셋 참조라 영속. 스프라이트도 그라데이션 컴포넌트도 없는
            // 비정상 상태(PNG 삭제 등)만 단색 암막으로 메꿔 흰 화면을 막는다.
            var backdrop = panelRoot.transform.Find("Backdrop");
            if (backdrop != null && backdrop.TryGetComponent(out Image bdImg)
                && bdImg.sprite == null && backdrop.GetComponent<UIFrostGradient>() == null)
            {
                var bgSpr = Resources.Load<Sprite>("ShipRepair/2/bg_hangar_blueprint");
                if (bgSpr != null) bdImg.sprite = bgSpr;
                else bdImg.color = new Color(10f / 255f, 14f / 255f, 21f / 255f, 1f);
            }
        }

        // 절차 폴백으로 빌드됐던 스탯 행/수리 버튼: 소실(null)일 때만 재적용
        if (repairButton != null && repairButton.image != null && repairButton.image.sprite == null)
            repairButton.image.sprite = UISpriteFactory.RoundedRectVGrad(new Color32(255, 255, 255, 255), new Color32(185, 205, 222, 255), 64, 16);
        if (statValueTexts != null)
        {
            foreach (var t in statValueTexts)
            {
                if (t == null || t.transform.parent == null) continue;
                var rowImg = t.transform.parent.GetComponent<Image>();
                if (rowImg != null && rowImg.sprite == null)
                    rowImg.sprite = _rowSpr != null
                        ? _rowSpr
                        : UISpriteFactory.RoundedRectVGrad(new Color32(34, 46, 64, 215), new Color32(16, 22, 32, 235), 64, 14);
            }
        }
    }

    private void OnEnable()  => ShipRepairManager.OnChanged += Refresh;
    private void OnDisable() => ShipRepairManager.OnChanged -= Refresh;

    private void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;
        if (Time.frameCount != _openedFrame && Input.GetKeyDown(KeyCode.F))
            Close();

        DriveAmbient();
    }

    // 열려있는 동안의 앰비언트 연출: 링 부드러운 채움 / 주사선 왕복 / 후광 맥동
    private void DriveAmbient()
    {
        if (ringGauge != null)
            ringGauge.fillAmount = Mathf.MoveTowards(ringGauge.fillAmount, _ringTarget, Time.unscaledDeltaTime * 0.6f);

        if (scanLine != null && scanLine.parent is RectTransform holo)
        {
            // 왕복 범위를 링/우주선 구역으로 한정 (아래 복원도 라벨/퍼센트 텍스트는 안 쓸고 다니게)
            float half = Mathf.Max(0f, holo.rect.height * 0.5f - 150f);
            float y = Mathf.PingPong(Time.unscaledTime * 130f, half * 2f) - half;
            scanLine.anchoredPosition = new Vector2(scanLine.anchoredPosition.x, y);
        }

        if (_holoGlowImg != null)
        {
            var c = _holoGlowImg.color;
            c.a = 0.055f + 0.03f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.7f));
            _holoGlowImg.color = c;
        }

        // 부품 전시대: 아크 링 2겹 역방향 회전 + 후광 맥동 (상태별 기준 알파 중심으로, 홀로 후광과 위상 어긋나게)
        if (_partRingA != null)
            _partRingA.Rotate(0f, 0f, -26f * Time.unscaledDeltaTime);
        if (_partRingB != null)
            _partRingB.Rotate(0f, 0f, 38f * Time.unscaledDeltaTime);
        if (_partIconGlow != null)
        {
            var c = _partIconGlow.color;
            c.a = _partGlowBaseA + 0.04f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.1f + 1.3f));
            _partIconGlow.color = c;
        }
    }

    // ── 열기 / 닫기 ───────────────────────────────────────────────────

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    public void Open()
    {
        if (panelRoot == null) return;

        panelRoot.SetActive(true);
        _openedFrame = Time.frameCount;

        BuildDynamic();
        Refresh();

        UISoundManager.Instance?.PlayPanelOpen();
    }

    /// <summary>패널만 숨긴다(상태 통보 없음) — GameUIController.CloseAll 이 호출.</summary>
    public void HidePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Close()
    {
        if (panelRoot == null) return;

        LastCloseFrame = Time.frameCount;
        HidePanel();
        UISoundManager.Instance?.PlayPanelClose();
        GameUIController.Instance?.CloseShipRepairUI();
    }

    // ── 가변 요소 생성 (레벨 pip / 부품 행) ────────────────────────────

    private void BuildDynamic()
    {
        if (_dynamicBuilt) return;
        var mgr = ShipRepairManager.Instance;
        if (mgr == null) return;

        int max = mgr.MaxLevel;

        // 레벨 pip (Lv.1 ~ Lv.max)
        if (pipContainer != null)
        {
            for (int i = 0; i < max; i++)
                _pips.Add(MakePip(pipContainer));
        }

        // 부품 카드 (단일 부품 - 보유/필요 하나로 통일)
        if (partsContent != null)
            MakePartCard(partsContent);

        _dynamicBuilt = true;
    }

    private Image MakePip(RectTransform parent)
    {
        bool art = _nodeOnSpr != null && _nodeOffSpr != null;   // 디자인 육각 노드 PNG

        var go = new GameObject("Pip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = art ? 36f : 30f; le.preferredHeight = art ? 36f : 30f;
        var img = go.GetComponent<Image>();
        img.sprite = art ? _nodeOffSpr : UISpriteFactory.Circle(48);
        img.color = art ? Color.white : PipEmpty;
        img.raycastTarget = false;
        if (art) return img;   // 점등 노드에 후광이 그려져 있어 절차 후광 불필요

        // 현재 단계 표시용 후광 (Refresh 가 현재 레벨 pip 만 켬)
        var glowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        glowGo.transform.SetParent(go.transform, false);
        var grt = (RectTransform)glowGo.transform;
        grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.sizeDelta = new Vector2(54f, 54f);
        var gimg = glowGo.GetComponent<Image>();
        gimg.sprite = UISpriteFactory.Disc(64);
        gimg.color = new Color(0.42f, 0.83f, 1f, 0.35f);
        gimg.raycastTarget = false;
        glowGo.transform.SetAsFirstSibling();
        glowGo.SetActive(false);
        return img;
    }

    // 부품 전시대 (패널 없이 배경 위에 직접 띄우는 홀로그램 구성).
    //   [보유 N] --- ((대형 아이콘 + 후광 + 링 2겹 회전)) --- [필요 M]
    //                          수리 부품
    //                  다음 수리까지 N개 더 필요
    //                        [진행 게이지]
    // 아이콘 = Resources/ShipRepair/7/icon_part.png 자동 연결. 링 회전/후광 맥동은 DriveAmbient 가 구동.
    private void MakePartCard(RectTransform parent)
    {
        var go = new GameObject("PartDisplay", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 230f;
        var root = (RectTransform)go.transform;

        // 로컬 헬퍼: 중앙 기준 배치
        RectTransform Put(string name, Vector2 size, Vector2 pos, out GameObject made)
        {
            var g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(root, false);
            var rt = (RectTransform)g.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            made = g;
            return rt;
        }

        Vector2 iconC = new Vector2(0, 34);   // 아이콘 중심 (위쪽에 배치, 아래로 이름/안내/게이지)

        // 후광 (맥동)
        Put("IconGlow", new Vector2(230, 230), iconC, out var glowGo);
        _partIconGlow = glowGo.AddComponent<Image>();
        _partIconGlow.sprite = UISpriteFactory.Disc(256);
        _partIconGlow.color = new Color(Holo.r, Holo.g, Holo.b, 0.10f);
        _partIconGlow.raycastTarget = false;

        // 안쪽 3/4 아크 링 (회전) - 균일한 원은 돌아도 티가 안 나서 끊긴 arc
        _partRingA = Put("RingA", new Vector2(178, 178), iconC, out var ringAGo);
        _partRingImgA = ringAGo.AddComponent<Image>();
        _partRingImgA.sprite = UISpriteFactory.Ring(200, 4f);
        _partRingImgA.type = Image.Type.Filled;
        _partRingImgA.fillMethod = Image.FillMethod.Radial360;
        _partRingImgA.fillOrigin = (int)Image.Origin360.Top;
        _partRingImgA.fillAmount = 0.72f;
        _partRingImgA.color = new Color(Holo.r, Holo.g, Holo.b, 0.55f);
        _partRingImgA.raycastTarget = false;

        // 바깥 얇은 짧은 아크 (역회전) - 겹회전으로 관제 계기 느낌
        _partRingB = Put("RingB", new Vector2(202, 202), iconC, out var ringBGo);
        _partRingImgB = ringBGo.AddComponent<Image>();
        _partRingImgB.sprite = UISpriteFactory.Ring(200, 2.5f);
        _partRingImgB.type = Image.Type.Filled;
        _partRingImgB.fillMethod = Image.FillMethod.Radial360;
        _partRingImgB.fillOrigin = (int)Image.Origin360.Top;
        _partRingImgB.fillAmount = 0.30f;
        _partRingImgB.color = new Color(Holo.r, Holo.g, Holo.b, 0.30f);
        _partRingImgB.raycastTarget = false;

        // 아이콘 (크게)
        var iconSpr = Resources.Load<Sprite>("ShipRepair/7/icon_part");
        if (iconSpr != null)
        {
            Put("Icon", new Vector2(140, 140), iconC, out var icoGo);
            var ico = icoGo.AddComponent<Image>();
            ico.sprite = iconSpr; ico.preserveAspect = true; ico.raycastTarget = false;
        }

        // 아이콘 양옆 연결 라인 (전시대 느낌)
        Put("LineL", new Vector2(96, 2), iconC + new Vector2(-160, 0), out var lineL);
        var llImg = lineL.AddComponent<Image>();
        llImg.color = new Color(Holo.r, Holo.g, Holo.b, 0.30f); llImg.raycastTarget = false;
        Put("LineR", new Vector2(96, 2), iconC + new Vector2(160, 0), out var lineR);
        var lrImg = lineR.AddComponent<Image>();
        lrImg.color = new Color(Holo.r, Holo.g, Holo.b, 0.30f); lrImg.raycastTarget = false;

        // 좌 = 보유 / 우 = 필요 (라벨 + 큰 숫자)
        TextMeshProUGUI MakeBigNum(string label, float x, out TextMeshProUGUI numT)
        {
            Put($"{label}Label", new Vector2(140, 22), new Vector2(x, iconC.y + 34), out var lgo);
            var lt = lgo.AddComponent<TextMeshProUGUI>();
            lt.text = label; lt.fontSize = 14f; lt.characterSpacing = 6f;
            lt.alignment = TextAlignmentOptions.Center; lt.color = TextDim; lt.raycastTarget = false;

            Put($"{label}Num", new Vector2(160, 52), new Vector2(x, iconC.y - 12), out var ngo);
            numT = ngo.AddComponent<TextMeshProUGUI>();
            numT.fontSize = 42f; numT.fontStyle = FontStyles.Bold;
            numT.alignment = TextAlignmentOptions.Center; numT.raycastTarget = false;
            return numT;
        }
        MakeBigNum("보유", -252, out _partOwnedText);
        MakeBigNum("필요",  252, out _partReqText);

        // 아이콘 아래: 이름 / 안내 / 게이지
        Put("Name", new Vector2(400, 30), new Vector2(0, -74), out var nameGo);
        _partCardName = nameGo.AddComponent<TextMeshProUGUI>();
        _partCardName.fontSize = 20f; _partCardName.fontStyle = FontStyles.Bold;
        _partCardName.alignment = TextAlignmentOptions.Center; _partCardName.raycastTarget = false;
        _partCardName.color = TextMain;

        Put("Sub", new Vector2(420, 22), new Vector2(0, -98), out var subGo);
        _partCardSub = subGo.AddComponent<TextMeshProUGUI>();
        _partCardSub.fontSize = 13f;
        _partCardSub.alignment = TextAlignmentOptions.Center; _partCardSub.raycastTarget = false;
        _partCardSub.color = TextDim;

        Put("GaugeTrack", new Vector2(280, 5), new Vector2(0, -114), out var trackGo);
        var track = trackGo.AddComponent<Image>();
        track.sprite = UISpriteFactory.RoundedRect(32, 2);
        track.type = Image.Type.Sliced;
        track.color = new Color(1f, 1f, 1f, 0.10f);
        track.raycastTarget = false;

        var fillGo = new GameObject("GaugeFill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(trackGo.transform, false);
        var frt = (RectTransform)fillGo.transform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        _partCardGauge = fillGo.GetComponent<Image>();
        _partCardGauge.sprite = UISpriteFactory.RoundedRect(32, 2);
        _partCardGauge.type = Image.Type.Filled;
        _partCardGauge.fillMethod = Image.FillMethod.Horizontal;
        _partCardGauge.color = Holo;
        _partCardGauge.raycastTarget = false;
    }

    // ── 갱신 ──────────────────────────────────────────────────────────

    private void Refresh()
    {
        var mgr = ShipRepairManager.Instance;
        if (mgr == null) return;

        int cur = mgr.CurrentLevel;
        int max = mgr.MaxLevel;

        if (levelText != null)
            levelText.text = $"수리 단계  Lv.{cur} / {max}";

        // pip 점등 + 현재 단계 강조 (디자인 노드=점등 스프라이트 교체+현재만 살짝 확대 / 폴백=색+후광)
        bool pipArt = _nodeOnSpr != null && _nodeOffSpr != null;
        for (int i = 0; i < _pips.Count; i++)
        {
            int lv = i + 1;
            var img = _pips[i];
            if (img == null) continue;
            if (pipArt)
            {
                img.sprite = lv <= cur ? _nodeOnSpr : _nodeOffSpr;
                img.color  = Color.white;
                img.transform.localScale = lv == cur ? Vector3.one * 1.15f : Vector3.one;
            }
            else
            {
                img.color = lv <= cur ? Holo : PipEmpty;
            }
            var glow = img.transform.Find("Glow");
            if (glow != null) glow.gameObject.SetActive(lv == cur);
        }

        // 복원도 링 (Lv.1=0% ~ Lv.max=100%). 실제 채움은 DriveAmbient 가 부드럽게 스윕.
        float prog = max > 1 ? (cur - 1f) / (max - 1f) : 1f;
        _ringTarget = Mathf.Clamp01(prog);
        if (restorePercentText != null) restorePercentText.text = $"{Mathf.RoundToInt(prog * 100f)}%";

        // 우주선 홀로그램 아트 - Resources/ShipRepair/1/ship_holo_lv{N}.png 자동 연결 (Lv 오를수록 완성)
        if (_shipSlotImg == null && ringGauge != null && ringGauge.transform.parent != null)
        {
            var slotTr = ringGauge.transform.parent.Find("ShipHologramSlot");
            if (slotTr != null) _shipSlotImg = slotTr.GetComponent<Image>();
        }
        if (_shipSlotImg != null)
        {
            var shipSpr = Resources.Load<Sprite>($"ShipRepair/1/ship_holo_lv{cur}");
            _shipSlotImg.sprite  = shipSpr;
            _shipSlotImg.enabled = shipSpr != null;
            if (shipSpr != null) _shipSlotImg.preserveAspect = true;
        }

        bool maxed = mgr.IsFullyRepaired;

        // 다음 수리 헤더 + 스탯 미리보기
        var curDef  = mgr.GetLevel(cur);
        var nextDef = maxed ? null : mgr.GetLevel(cur + 1);

        if (nextHeaderText != null)
            nextHeaderText.text = maxed ? "수리 완료" : $"다음 수리   Lv.{cur} -> Lv.{cur + 1}";

        SetStat(0, "건축 범위",   curDef, nextDef, StatKind.Zone);
        SetStat(1, "설비 연료",   curDef, nextDef, StatKind.Fuel);
        SetStat(2, "공장 가동속도", curDef, nextDef, StatKind.Speed);

        // 부품 전시대: 좌 보유 / 우 필요 큰 숫자 + 이름/안내/게이지. 상태색 = 완료 초록 / 충분 시안 밝게 / 부족 시안 어둡게
        int count = mgr.PartCount;
        int req   = mgr.NextRequiredParts;
        bool enough = !maxed && count >= req;

        if (_partCardName != null)
            _partCardName.text = mgr.PartName;
        if (_partCardSub != null)
            _partCardSub.text = maxed
                ? "모든 수리가 끝났습니다"
                : (enough ? "다음 수리 준비 완료" : $"다음 수리까지 {req - count}개 더 필요");

        if (_partOwnedText != null)
        {
            _partOwnedText.text  = count.ToString();
            _partOwnedText.color = maxed ? DoneCol : (enough ? Holo : TextMain);
        }
        if (_partReqText != null)
        {
            _partReqText.text  = maxed ? "-" : req.ToString();
            _partReqText.color = maxed ? TextDim : TextMain;
        }

        Color state = maxed ? DoneCol : Holo;
        bool lit = maxed || enough;
        if (_partRingImgA != null) _partRingImgA.color = new Color(state.r, state.g, state.b, lit ? 0.60f : 0.35f);
        if (_partRingImgB != null) _partRingImgB.color = new Color(state.r, state.g, state.b, lit ? 0.32f : 0.18f);
        _partGlowBaseA = lit ? 0.14f : 0.07f;
        if (_partIconGlow != null)
            _partIconGlow.color = new Color(state.r, state.g, state.b, _partGlowBaseA);

        if (_partCardGauge != null)
        {
            _partCardGauge.fillAmount = maxed ? 1f : (req > 0 ? Mathf.Clamp01((float)count / req) : 1f);
            _partCardGauge.color = state;
        }

        // 수리 버튼
        bool canRepair = mgr.CanRepairNext();
        if (repairButton != null)
        {
            repairButton.interactable = canRepair;
            if (repairButton.image != null) repairButton.image.color = canRepair ? BtnReady : BtnLocked;
        }
        if (repairButtonText != null)
            repairButtonText.text = maxed
                ? "수리 완료"
                : (canRepair ? "수리 실행" : $"부품 부족  {count} / {mgr.NextRequiredParts}");
    }

    private enum StatKind { Zone, Fuel, Speed }

    private void SetStat(int idx, string _, ShipRepairManager.LevelDef cur, ShipRepairManager.LevelDef next, StatKind kind)
    {
        if (statValueTexts == null || idx < 0 || idx >= statValueTexts.Length) return;
        var t = statValueTexts[idx];
        if (t == null) return;

        string curS = FormatStat(cur, kind);
        bool changed = false;
        if (next == null)   // 최종 레벨 = 다음 없음
        {
            t.text = curS;
            t.color = TextDim;
        }
        else
        {
            string nextS = FormatStat(next, kind);
            changed = curS != nextS;
            if (changed)
            {
                t.text  = $"{curS}  ->  {nextS}";
                t.color = Holo;
            }
            else
            {
                t.text  = curS;
                t.color = TextDim;
            }
        }

        // 패널 점등 = 부품 행과 같은 문법: 이번 수리로 바뀌는 스탯만 시안 점등, 나머지는 가라앉힘
        var rowTr = t.transform.parent;
        if (rowTr == null) return;
        var rowImg = rowTr.GetComponent<Image>();
        if (rowImg != null)
        {
            if (_rowSpr != null && _rowHlSpr != null)
                rowImg.sprite = changed ? _rowHlSpr : _rowSpr;
            rowImg.color = new Color(1f, 1f, 1f, changed ? 1f : 0.8f);
        }
        var nameTr = rowTr.Find("Name");
        if (nameTr != null && nameTr.TryGetComponent(out TextMeshProUGUI nameT))
            nameT.color = changed ? new Color(0.80f, 0.87f, 0.94f, 1f) : TextDim;
    }

    private string FormatStat(ShipRepairManager.LevelDef def, StatKind kind)
    {
        if (def == null) return "-";
        switch (kind)
        {
            case StatKind.Zone:  return $"단계 {def.zoneStage}";
            case StatKind.Fuel:  return $"{Mathf.RoundToInt(def.fuelSeconds)}초";
            case StatKind.Speed: return $"제작 {Mathf.RoundToInt(def.factorySpeed * 100f)}%";
        }
        return "-";
    }

    // ── 입력 ──────────────────────────────────────────────────────────

    private void OnClickRepair()
    {
        var mgr = ShipRepairManager.Instance;
        if (mgr == null) return;

        UISoundManager.Instance?.PlayButtonClick();
        mgr.TryRepairNext();   // 성공 시 OnChanged -> Refresh 자동
        Refresh();
    }

}
