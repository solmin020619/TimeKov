using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 폐우주선 수리 패널 컨트롤러 (풀스크린 대형, 공장풍 프로스티드 + 우주선 홀로그램 링게이지).
// 패널 계층은 씬에 있는 실물이 원본이고, 이 클래스는 ref 로 연결된 것을 쓰기만 한다.
// (생성용 에디터 빌더는 08-03 에 팀 합의로 제거. 다시 구워야 하면 git 이력에서 꺼낸다)
//
// [08-02] 런타임 생성 요소를 씬 실물로 전환.
//   이전에는 부품 전시대/특수부품 칩/호버 툴팁을 이 스크립트가 코드로 만들었다. 그래서
//   (1) "보유"/"필요"/부품 이름 같은 문구에 로컬라이징 컴포넌트를 못 붙이고
//   (2) 위치/색을 인스펙터에서 못 고쳤다.
//   지금은 전부 씬에 있고, 개수가 데이터에 따라 변하는 것(레벨 pip / 특수부품 칩)만
//   빌더가 만들어둔 '템플릿'을 런타임에 복제한다. 이 파일에는 계층을 만드는 코드가 없다.
//
//   Resources.Load 와 transform.Find 경로 문자열도 전부 직렬화 참조로 교체했다
//   (경로 오타/이름 변경으로 조용히 죽던 자리들).
//
// 부품은 단일 종류+개수제 - 데이터/조작은 ShipRepairManager 로만 (PartCount/CanRepairNext/TryRepairNext/OnChanged).
[SingleInstance]
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

    [Header("헤더 / 레벨 사다리")]
    [SerializeField] private TextMeshProUGUI levelText;      // "수리 단계 Lv.N / M"
    [Tooltip("레벨 pip 부모 (HorizontalLayoutGroup).")]
    [SerializeField] private RectTransform pipContainer;
    [Tooltip("pip 원본. 레벨 수만큼 복제한다. 씬에서는 꺼둔 상태로 둔다.")]
    [SerializeField] private Image pipTemplate;

    [Header("홀로그램 (복원도)")]
    [SerializeField] private Image ringGauge;                // Filled Radial360, 복원도 비율
    [SerializeField] private TextMeshProUGUI restorePercentText;
    [Tooltip("주사선. 열려있는 동안 위아래로 왕복한다.")]
    [SerializeField] private RectTransform scanLine;
    [Tooltip("링 뒤 후광. 열려있는 동안 알파가 맥동한다.")]
    [SerializeField] private Image holoGlow;
    [Tooltip("우주선 홀로그램 아트. 레벨이 오를수록 완성된 그림으로 바뀐다.")]
    [SerializeField] private Image shipHologramSlot;

    [Header("다음 수리 / 스탯")]
    [SerializeField] private TextMeshProUGUI nextHeaderText; // "다음 수리  Lv.N -> Lv.N+1"
    [Tooltip("[0]건축 범위 [1]설비 연료 [2]공장 가동속도 의 값 텍스트")]
    [SerializeField] private TextMeshProUGUI[] statValueTexts;
    [Tooltip("위와 같은 순서의 항목 이름 텍스트. 이번 수리로 바뀌는 행만 밝아진다.")]
    [SerializeField] private TextMeshProUGUI[] statNameTexts;
    [Tooltip("위와 같은 순서의 행 배경. 바뀌는 행만 강조 스프라이트로 교체한다.")]
    [SerializeField] private Image[] statRowImages;

    [Header("부품 전시대")]
    [Tooltip("보유 개수(좌측 큰 숫자).")]
    [SerializeField] private TextMeshProUGUI partOwnedText;
    [Tooltip("필요 개수(우측 큰 숫자).")]
    [SerializeField] private TextMeshProUGUI partRequiredText;
    [Tooltip("아이콘 아래 부품 이름. 내용은 데이터에서 온다.")]
    [SerializeField] private TextMeshProUGUI partNameText;
    [Tooltip("아이콘 둘레 링 게이지 (보유/필요 비율로 차오름).")]
    [SerializeField] private Image partGauge;
    [Tooltip("링 트랙. 열려있는 동안 천천히 회전한다(스캐너 느낌).")]
    [SerializeField] private RectTransform partRingTrack;
    [Tooltip("아이콘 뒤 후광. 열려있는 동안 맥동한다.")]
    [SerializeField] private Image partIconGlow;

    [Header("특수 부품 칩")]
    [Tooltip("칩 부모 (HorizontalLayoutGroup). 특수 부품이 정의된 레벨 수만큼 칩이 생긴다.")]
    [SerializeField] private RectTransform extraChipContainer;
    [Tooltip("칩 원본. 씬에서는 꺼둔 상태로 둔다.")]
    [SerializeField] private ShipExtraPartChip extraChipTemplate;
    [Tooltip("특수 부품이 하나도 정의되지 않았을 때 통째로 숨길 섹션.")]
    [SerializeField] private GameObject extraChipSection;

    [Header("특수 부품 호버 툴팁")]
    [SerializeField] private GameObject chipTooltip;
    [SerializeField] private TextMeshProUGUI chipTooltipTitle;
    [SerializeField] private TextMeshProUGUI chipTooltipBody;
    [SerializeField] private TextMeshProUGUI chipTooltipState;
    // ★풀네임 필수: 18.외부에셋 의 3D Outline(전역 네임스페이스)이 UnityEngine.UI.Outline 을 가린다.
    [Tooltip("툴팁 테두리. 상태색을 따라간다.")]
    [SerializeField] private UnityEngine.UI.Outline chipTooltipOutline;

    [Header("수리 버튼")]
    [SerializeField] private Button repairButton;
    [SerializeField] private TextMeshProUGUI repairButtonText;

    [Header("디자인 스프라이트 (빌더가 연결)")]
    [Tooltip("점등된 레벨 노드.")]
    [SerializeField] private Sprite pipOnSprite;
    [Tooltip("꺼진 레벨 노드.")]
    [SerializeField] private Sprite pipOffSprite;
    [Tooltip("스탯 행 배경(기본).")]
    [SerializeField] private Sprite statRowSprite;
    [Tooltip("스탯 행 배경(이번 수리로 바뀌는 행).")]
    [SerializeField] private Sprite statRowHighlightSprite;
    [Tooltip("레벨별 우주선 홀로그램 아트. [0] = Lv.1.")]
    [SerializeField] private Sprite[] shipHologramByLevel;

    [Header("상태 색")]
    [Tooltip("기본 홀로그램 시안. 진행/강조에 쓴다.")]
    [SerializeField] private Color holoColor = new Color(0.42f, 0.83f, 1.00f, 1f);
    [SerializeField] private Color textMainColor = new Color(0.82f, 0.89f, 0.96f, 1f);
    [SerializeField] private Color textDimColor = new Color(0.44f, 0.51f, 0.60f, 1f);
    [Tooltip("완료 상태(초록).")]
    [SerializeField] private Color doneColor = new Color(0.36f, 0.84f, 0.56f, 1f);
    [Tooltip("아직 도달 못한 레벨 pip 색(디자인 노드가 없을 때만 쓰임).")]
    [SerializeField] private Color pipEmptyColor = new Color(0.30f, 0.36f, 0.44f, 1f);
    [SerializeField] private Color repairReadyColor = new Color(0.20f, 0.66f, 0.95f, 1f);
    [SerializeField] private Color repairLockedColor = new Color(0.16f, 0.20f, 0.28f, 0.95f);
    [Tooltip("특수 부품 칩 배경 - 이미 사용(수리 완료).")]
    [SerializeField] private Color chipUsedTint = new Color(0.26f, 0.45f, 0.62f, 1f);
    [Tooltip("특수 부품 칩 배경 - 보유 중.")]
    [SerializeField] private Color chipOwnedTint = new Color(0.30f, 0.62f, 0.44f, 1f);
    [Tooltip("특수 부품 칩 배경 - 미획득.")]
    [SerializeField] private Color chipMissingTint = Color.white;

    [Header("연출 값")]
    [Tooltip("복원도 링이 목표치까지 차는 속도(초당 비율).")]
    [SerializeField] private float ringFillSpeed = 0.6f;
    [Tooltip("주사선 왕복 속도(px/초).")]
    [SerializeField] private float scanSpeed = 130f;
    [Tooltip("주사선이 왕복할 위/아래 여백(px). 아래 복원도 라벨은 안 쓸고 다니게.")]
    [SerializeField] private float scanMargin = 150f;
    [Tooltip("부품 링 트랙 회전 속도(도/초). 음수 = 시계 방향.")]
    [SerializeField] private float partRingSpin = -9f;

    private const float PipGapPx = 16f;        // 제목 글자 끝과 pip 줄 사이 최소 간격
    private float _pipLeftAuthored = float.NaN; // 씬에 저장된 pip 줄 시작점(짧은 언어의 기준선)

    private readonly List<Image> _pips = new();
    private readonly List<ShipExtraPartChip> _extraChips = new();
    private float _partGlowBaseA = 0.10f;      // 상태별 후광 기준 알파 (맥동의 중심값)
    private bool _dynamicBuilt;
    private int _openedFrame = -1;
    private float _ringTarget;                 // 링 게이지 목표치 (DriveAmbient 가 부드럽게 채움)

    // ── 라이프사이클 ──────────────────────────────────────────────────

    private void Awake()
    {
        if (UIDuplicateGuard.Report(Instance, this)) { Destroy(gameObject); return; }
        Instance = this;

        closeButton?.onClick.AddListener(() => UISoundManager.Instance?.PlayButtonClick());   // 닫기 버튼 클릭음(수리 버튼은 OnClickRepair 에서 재생)
        closeButton?.onClick.AddListener(Close);
        repairButton?.onClick.AddListener(OnClickRepair);
        panelRoot?.SetActive(false);

        if (pipTemplate != null) pipTemplate.gameObject.SetActive(false);
        if (extraChipTemplate != null) extraChipTemplate.gameObject.SetActive(false);
        if (chipTooltip != null) chipTooltip.SetActive(false);

        SetupHoverSound();
    }

    // 호버 사운드는 람다라 직렬화가 안 된다 -> 런타임에 건다.
    // (확대/밝아짐 연출은 ShipUIHoverFx 컴포넌트로 씬에 붙어 있다.)
    private void SetupHoverSound()
    {
        AddHoverSound(closeButton);
        AddHoverSound(repairButton);
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

    private void OnEnable()  { ShipRepairManager.OnChanged += Refresh; Loc.OnLanguageChanged += Refresh; }
    private void OnDisable() { ShipRepairManager.OnChanged -= Refresh; Loc.OnLanguageChanged -= Refresh; }

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
            ringGauge.fillAmount = Mathf.MoveTowards(ringGauge.fillAmount, _ringTarget, Time.unscaledDeltaTime * ringFillSpeed);

        if (scanLine != null && scanLine.parent is RectTransform holo)
        {
            // 왕복 범위를 링/우주선 구역으로 한정 (아래 복원도 라벨/퍼센트 텍스트는 안 쓸고 다니게)
            float half = Mathf.Max(0f, holo.rect.height * 0.5f - scanMargin);
            float y = Mathf.PingPong(Time.unscaledTime * scanSpeed, half * 2f) - half;
            scanLine.anchoredPosition = new Vector2(scanLine.anchoredPosition.x, y);
        }

        if (holoGlow != null)
        {
            var c = holoGlow.color;
            c.a = 0.055f + 0.03f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.7f));
            holoGlow.color = c;
        }

        // 부품 전시대: 눈금 트랙 링 천천히 회전(스캐너 느낌) + 후광 맥동 (상태별 기준 알파 중심)
        if (partRingTrack != null)
            partRingTrack.Rotate(0f, 0f, partRingSpin * Time.unscaledDeltaTime);
        if (partIconGlow != null)
        {
            var c = partIconGlow.color;
            c.a = _partGlowBaseA + 0.04f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.1f + 1.3f));
            partIconGlow.color = c;
        }
    }

    // ── 열기 / 닫기 ───────────────────────────────────────────────────

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    public void Open()
    {
        // 조용히 실패하지 않게 한다. panelRoot 는 빌더가 연결하는 참조라,
        //   빌더를 안 돌렸거나 패널을 손으로 지웠으면 null 이 된다.
        //   예전엔 여기서 그냥 return 해서 "F 는 먹는데 아무것도 안 뜨는" 상태가 됐다.
        if (panelRoot == null)
        {
            Debug.LogError("[ShipRepairUI] panelRoot 가 비어 있어 패널을 못 연다. " +
                           "메뉴 Tools > TIMEKOV > 우주선 수리 UI 생성 을 실행해라.");
            return;
        }

        panelRoot.SetActive(true);
        _openedFrame = Time.frameCount;

        BuildDynamic();
        HideChipTooltip();   // 닫았다 열 때 호버 툴팁 잔상 제거
        Refresh();

        GameSfx.Play(SfxId.PanelShipRepairToggle);   // 우주선 수리 전용 열기음
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
        GameSfx.Play(SfxId.PanelShipRepairToggle);   // 우주선 수리 전용 닫기음(열/닫 공용 클립)
        GameUIController.Instance?.CloseShipRepairUI();
    }

    // ── 개수가 데이터에 따라 변하는 것만 복제 (레벨 pip / 특수부품 칩) ──
    // 계층은 만들지 않는다. 빌더가 씬에 넣어둔 템플릿을 그대로 찍어낸다.

    private void BuildDynamic()
    {
        if (_dynamicBuilt) return;
        var mgr = ShipRepairManager.Instance;
        if (mgr == null) return;

        // 레벨 pip (Lv.1 ~ Lv.max)
        if (pipContainer != null && pipTemplate != null)
        {
            for (int lv = 1; lv <= mgr.MaxLevel; lv++)
            {
                var pip = Instantiate(pipTemplate, pipContainer);
                pip.name = $"Pip_Lv{lv}";
                pip.gameObject.SetActive(true);
                _pips.Add(pip);
            }
        }

        // 특수 부품(전송 보상) 칩 - 이름이 정의된 레벨만.
        //   데이터 = ShipRepairManager.ExtraPartNameFor (레벨 순회 = 데이터 정의 따라감, 하드코딩 X)
        if (extraChipContainer != null && extraChipTemplate != null)
        {
            for (int lv = 1; lv <= mgr.MaxLevel; lv++)
            {
                string partName = mgr.ExtraPartNameFor(lv);
                if (string.IsNullOrEmpty(partName)) continue;

                var chip = Instantiate(extraChipTemplate, extraChipContainer);
                chip.name = $"Chip_Lv{lv}";
                chip.gameObject.SetActive(true);
                chip.Setup(this, lv, partName);
                _extraChips.Add(chip);
            }
        }
        // 특수 부품 정의가 하나도 없으면 섹션 자체를 접는다(빈 칸이 남지 않게).
        if (extraChipSection != null) extraChipSection.SetActive(_extraChips.Count > 0);

        _dynamicBuilt = true;
    }

    // ── 특수 부품 툴팁 ────────────────────────────────────────────────
    // 칩에 마우스를 올리면 "이게 뭐지?" 하다가도 "몇 %에서 얻는 거구나" 하고 이해되게 한다.

    public void ShowChipTooltip(int level, RectTransform anchor)
    {
        if (chipTooltip == null || anchor == null) return;
        var mgr = ShipRepairManager.Instance;
        bool used = mgr != null && mgr.CurrentLevel >= level;   // 그 레벨까지 수리됨 = 사용 완료
        bool have = !used && mgr != null && mgr.HasExtraPart(level);
        int pct = ShipPartMilestone(level);

        string stateText; Color col;
        if (used)      { stateText = Loc.Get("사용됨 (수리 완료)"); col = holoColor; }
        else if (have) { stateText = Loc.Get("보유 중");            col = doneColor; }
        else           { stateText = Loc.Get("미획득");             col = textDimColor; }

        if (chipTooltipTitle != null)
        {
            chipTooltipTitle.text = mgr != null ? Loc.Get(mgr.ExtraPartNameFor(level)) : "";
            chipTooltipTitle.color = (used || have) ? col : textMainColor;   // 미획득 제목은 읽히게 흰색
        }
        if (chipTooltipBody != null)
            chipTooltipBody.text = pct > 0
                ? string.Format(Loc.Get("시간에너지 전송률 {0}% 에서 획득"), pct)
                : Loc.Get("시간에너지 전송으로 획득");
        if (chipTooltipState != null)
        {
            chipTooltipState.text = stateText;
            chipTooltipState.color = col;
        }
        if (chipTooltipOutline != null)
            chipTooltipOutline.effectColor = new Color(col.r, col.g, col.b, 0.5f);

        chipTooltip.SetActive(true);
        chipTooltip.transform.SetAsLastSibling();
        // 칩 상단 중앙 위에 배치(월드좌표 직접 세팅이라 부모 pivot 무관) + 살짝 위로.
        chipTooltip.transform.position = anchor.TransformPoint(new Vector3(0f, anchor.rect.height * 0.5f, 0f));
        ((RectTransform)chipTooltip.transform).anchoredPosition += new Vector2(0f, 12f);
    }

    public void HideChipTooltip()
    {
        if (chipTooltip != null) chipTooltip.SetActive(false);
    }

    // 각 특수부품(레벨)이 나오는 시간에너지 전송률(%). 단일 소스 = TransmissionManager.ShipPartMilestones.
    //   (하드코딩 중복이 % 재조정 때 어긋나서, 매니저 표를 직접 참조하도록 통일 - 이제 여기 숫자 없음)
    private static int ShipPartMilestone(int level) => TransmissionManager.ShipPartMilestonePct(level);

    // ── 갱신 ──────────────────────────────────────────────────────────

    private void Refresh()
    {
        var mgr = ShipRepairManager.Instance;
        if (mgr == null) return;

        int cur = mgr.CurrentLevel;
        int max = mgr.MaxLevel;

        if (levelText != null)
            levelText.text = $"{Loc.Get("수리 단계")}  Lv.{cur} / {max}";

        PushPipsBehindLabel();
        RefreshPips(cur);

        // 복원도 링 (Lv.1=0% ~ Lv.max=100%). 실제 채움은 DriveAmbient 가 부드럽게 스윕.
        float prog = max > 1 ? (cur - 1f) / (max - 1f) : 1f;
        _ringTarget = Mathf.Clamp01(prog);
        if (restorePercentText != null) restorePercentText.text = $"{Mathf.RoundToInt(prog * 100f)}%";

        // 우주선 홀로그램 아트 - 레벨 오를수록 완성된 그림
        if (shipHologramSlot != null)
        {
            var shipSpr = SpriteForLevel(cur);
            shipHologramSlot.sprite = shipSpr;
            shipHologramSlot.enabled = shipSpr != null;
            if (shipSpr != null) shipHologramSlot.preserveAspect = true;
        }

        bool maxed = mgr.IsFullyRepaired;

        // 다음 수리 헤더 + 스탯 미리보기
        var curDef  = mgr.GetLevel(cur);
        var nextDef = maxed ? null : mgr.GetLevel(cur + 1);

        if (nextHeaderText != null)
            nextHeaderText.text = maxed ? Loc.Get("수리 완료") : $"{Loc.Get("다음 수리")}   Lv.{cur} -> Lv.{cur + 1}";

        SetStat(0, curDef, nextDef, StatKind.Zone);
        SetStat(1, curDef, nextDef, StatKind.Fuel);
        SetStat(2, curDef, nextDef, StatKind.Speed);

        RefreshPartDisplay(mgr, maxed);
        RefreshExtraChips(mgr);
        RefreshRepairButton(mgr, maxed);
    }

    // pip 점등 + 현재 단계 강조 (디자인 노드=점등 스프라이트 교체+현재만 살짝 확대 / 없으면 색만)
    // "수리 단계 Lv.1 / 10" 제목과 그 오른쪽 pip 줄은 좌표가 각각 고정이라,
    //   제목이 길어지면 pip 가 글자 위로 올라온다("Niveau de reparation" 에서 실제로 겹쳤다).
    //   글자 실제 폭을 재서 pip 줄 시작점을 그 뒤로 민다. 둘은 같은 부모라 좌표계가 같다.
    //   한국어처럼 짧으면 원래 위치보다 왼쪽이 나오므로 그때는 손대지 않는다.
    private void PushPipsBehindLabel()
    {
        if (levelText == null || pipContainer == null) return;
        if (float.IsNaN(_pipLeftAuthored)) _pipLeftAuthored = pipContainer.offsetMin.x;

        var lrt = levelText.rectTransform;
        float left = lrt.anchoredPosition.x - lrt.rect.width * lrt.pivot.x;
        float wanted = left + levelText.preferredWidth + PipGapPx;
        if (wanted <= _pipLeftAuthored) return;   // 짧은 언어 = 원래 배치 유지

        pipContainer.offsetMin = new Vector2(wanted, pipContainer.offsetMin.y);
    }

    private void RefreshPips(int cur)
    {
        bool art = pipOnSprite != null && pipOffSprite != null;
        for (int i = 0; i < _pips.Count; i++)
        {
            int lv = i + 1;
            var img = _pips[i];
            if (img == null) continue;
            if (art)
            {
                img.sprite = lv <= cur ? pipOnSprite : pipOffSprite;
                img.color  = Color.white;
                img.transform.localScale = lv == cur ? Vector3.one * 1.15f : Vector3.one;
            }
            else
            {
                img.color = lv <= cur ? holoColor : pipEmptyColor;
            }
        }
    }

    private Sprite SpriteForLevel(int level)
    {
        int idx = level - 1;   // [0] = Lv.1
        if (shipHologramByLevel == null || idx < 0 || idx >= shipHologramByLevel.Length) return null;
        return shipHologramByLevel[idx];
    }

    // 부품 전시대: 좌 보유 / 우 필요 큰 숫자 + 이름 + 링 게이지.
    // 상태색 = 완료 초록 / 충분 시안 밝게 / 부족 시안 어둡게
    private void RefreshPartDisplay(ShipRepairManager mgr, bool maxed)
    {
        int count = mgr.PartCount;
        int req   = mgr.NextRequiredParts;
        bool enough = !maxed && count >= req;

        if (partNameText != null) partNameText.text = Loc.Get(mgr.PartName);

        if (partOwnedText != null)
        {
            partOwnedText.text  = count.ToString();
            partOwnedText.color = maxed ? doneColor : (enough ? holoColor : textMainColor);
        }
        if (partRequiredText != null)
        {
            partRequiredText.text  = maxed ? "-" : req.ToString();
            partRequiredText.color = maxed ? textDimColor : textMainColor;
        }

        Color state = maxed ? doneColor : holoColor;
        _partGlowBaseA = (maxed || enough) ? 0.14f : 0.07f;
        if (partIconGlow != null)
            partIconGlow.color = new Color(state.r, state.g, state.b, _partGlowBaseA);

        if (partGauge != null)
        {
            partGauge.fillAmount = maxed ? 1f : (req > 0 ? Mathf.Clamp01((float)count / req) : 1f);
            partGauge.color = state;
        }
    }

    // 특수 부품 칩: 미획득=회색 / 보유(먹었고 아직 안 씀)=초록 / 이미 사용(그 레벨까지 수리 완료)=파랑.
    //   ★수리하면 extraMask 가 소모돼 HasExtraPart 가 false 로 돌아간다 -> 그것만 보면 "미획득"이랑 구분 안 됨.
    //     그래서 CurrentLevel >= 부품레벨 이면 "사용 완료"로 따로 처리한다(한 번만 먹는 부품이라 이게 자연스럽다).
    private void RefreshExtraChips(ShipRepairManager mgr)
    {
        foreach (var chip in _extraChips)
        {
            if (chip == null) continue;
            int lv = chip.Level;
            bool used = mgr.CurrentLevel >= lv;          // 그 레벨까지 수리됨 = 이 부품 사용 완료
            bool have = !used && mgr.HasExtraPart(lv);   // 획득했고 아직 안 씀
            chip.SetState(
                used ? chipUsedTint : have ? chipOwnedTint : chipMissingTint,
                used ? holoColor    : have ? doneColor     : textMainColor);
        }
    }

    // 수리 버튼 (완전 수리 + 시간에너지 100% = "탈출하기" 로 전환 = 게임 클리어 트리거)
    private void RefreshRepairButton(ShipRepairManager mgr, bool maxed)
    {
        bool canRepair = mgr.CanRepairNext();
        bool canEscape = ShipEscape.CanEscape();   // 우주선 Lv.최대 AND 전송률 100%
        bool canAct = canEscape || canRepair;
        if (repairButton != null)
        {
            repairButton.interactable = canAct;
            if (repairButton.image != null) repairButton.image.color = canAct ? repairReadyColor : repairLockedColor;
        }
        if (repairButtonText != null)
            repairButtonText.text = maxed
                ? (canEscape ? Loc.Get("탈출하기") : EscapeBlockedReason())
                : (canRepair ? Loc.Get("수리 실행") : BlockedReason(mgr, mgr.PartCount));
    }

    // 왜 수리를 못 하는지 버튼에 그대로 적는다.
    // ★에너지와 별개로 레벨 전용 추가 재료가 필요할 수 있다.
    //   그걸 구분 안 하면 에너지는 충분한데 "부품 부족 3 / 3" 이 떠서 이유를 알 수 없다.
    private static string BlockedReason(ShipRepairManager mgr, int count)
    {
        int need = mgr.NextRequiredParts;
        if (count < need) return $"{Loc.Get(mgr.PartName)}  {count} / {need}";

        string extra = mgr.NextExtraPartName;
        if (!string.IsNullOrEmpty(extra)) return string.Format(Loc.Get("{0} 필요"), Loc.Get(extra));

        return Loc.Get("수리 불가");
    }

    // 수리는 끝났지만(Lv.최대) 아직 탈출 못 하는 이유 = 시간에너지 전송률 미완(100% 필요).
    private static string EscapeBlockedReason()
    {
        var tm = TransmissionManager.Instance;
        int rate = tm != null ? tm.TransmissionRate : 0;
        return string.Format(Loc.Get("시간에너지 {0}% / 100%"), rate);
    }

    private enum StatKind { Zone, Fuel, Speed }

    private void SetStat(int idx, ShipRepairManager.LevelDef cur, ShipRepairManager.LevelDef next, StatKind kind)
    {
        if (statValueTexts == null || idx < 0 || idx >= statValueTexts.Length) return;
        var t = statValueTexts[idx];
        if (t == null) return;

        string curS = FormatStat(cur, kind);
        bool changed = false;
        if (next == null)   // 최종 레벨 = 다음 없음
        {
            t.text = curS;
            t.color = textDimColor;
        }
        else
        {
            string nextS = FormatStat(next, kind);
            changed = curS != nextS;
            if (changed)
            {
                t.text  = $"{curS}  ->  {nextS}";
                t.color = holoColor;
            }
            else
            {
                t.text  = curS;
                t.color = textDimColor;
            }
        }

        // 패널 점등 = 부품 행과 같은 문법: 이번 수리로 바뀌는 스탯만 시안 점등, 나머지는 가라앉힘
        if (statRowImages != null && idx < statRowImages.Length && statRowImages[idx] != null)
        {
            var rowImg = statRowImages[idx];
            if (statRowSprite != null && statRowHighlightSprite != null)
                rowImg.sprite = changed ? statRowHighlightSprite : statRowSprite;
            rowImg.color = new Color(1f, 1f, 1f, changed ? 1f : 0.8f);
        }
        if (statNameTexts != null && idx < statNameTexts.Length && statNameTexts[idx] != null)
        {
            statNameTexts[idx].color = changed ? new Color(0.80f, 0.87f, 0.94f, 1f) : textDimColor;
            switch (kind)
            {
                case StatKind.Zone:  statNameTexts[idx].text = Loc.Get("건축 범위"); break;
                case StatKind.Fuel:  statNameTexts[idx].text = Loc.Get("설비 연료"); break;
                case StatKind.Speed: statNameTexts[idx].text = Loc.Get("공장 가동속도"); break;
            }
        }
    }

    private string FormatStat(ShipRepairManager.LevelDef def, StatKind kind)
    {
        if (def == null) return "-";
        switch (kind)
        {
            // 단계 번호는 플레이어에게 아무 의미가 없다. 실제 칸 수를 보여준다.
            case StatKind.Zone:  return def.zoneCells > 0 ? string.Format(Loc.Get("{0}x{1}칸"), def.zoneCells, def.zoneCells) : "-";
            case StatKind.Fuel:  return string.Format(Loc.Get("{0}초"), Mathf.RoundToInt(def.fuelSeconds));
            case StatKind.Speed: return string.Format(Loc.Get("제작 {0}%"), Mathf.RoundToInt(def.factorySpeed * 100f));
        }
        return "-";
    }

    // ── 입력 ──────────────────────────────────────────────────────────

    private void OnClickRepair()
    {
        var mgr = ShipRepairManager.Instance;
        if (mgr == null) return;

        UISoundManager.Instance?.PlayButtonClick();

        // 완전 수리 + 시간에너지 100% 면 이 버튼은 "탈출하기" = 게임 클리어(페이드 후 메인 메뉴).
        if (ShipEscape.CanEscape())
        {
            ShipEscape.Run();
            return;
        }

        mgr.TryRepairNext();   // 성공 시 OnChanged -> Refresh 자동
        Refresh();
    }
}
