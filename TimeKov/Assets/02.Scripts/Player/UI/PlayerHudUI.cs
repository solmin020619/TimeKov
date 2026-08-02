using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHudUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("PlayerStatComponent가 붙어있는 플레이어 오브젝트. 비워두면 씬에서 자동 탐색.")]
    [SerializeField] private PlayerStatComponent playerStat;
    [SerializeField] private PlayerSkillComponent playerSkill;

    [Header("HP / TIME")]
    [SerializeField] private Slider hpSlider;

    [Tooltip("TIME 수치 텍스트. 예: Time : 20/300")]
    [SerializeField] private TMP_Text timeValueText;

    [Tooltip("TIME 텍스트 앞에 붙일 라벨")]
    [SerializeField] private string timeTextPrefix = "Time : ";

    [Header("Time Decay HUD")]
    [Tooltip("왼쪽 하단 DECAY 상태의 시계 아이콘 Image")]
    [SerializeField] private Image timeDecayIcon;

    [Tooltip("DECAY ON / DECAY OFF 텍스트")]
    [SerializeField] private TMP_Text timeDecayStateText;

    [Tooltip("-1.0 / sec 텍스트")]
    [SerializeField] private TMP_Text timeDecayRateText;

    [Tooltip("시간 감소 중일 때 시계 아이콘과 DECAY ON 텍스트 색상")]
    [SerializeField] private Color timeDecayOnColor = new Color(0.2f, 0.75f, 1f, 1f);

    [Tooltip("시간 감소가 꺼졌을 때 시계 아이콘과 DECAY OFF 텍스트 색상")]
    [SerializeField] private Color timeDecayOffColor = Color.white;

    [Tooltip("시간 감소 중일 때 표시할 텍스트")]
    [SerializeField] private string timeDecayOnText = "DECAY ON";

    [Tooltip("시간 감소가 꺼졌을 때 표시할 텍스트")]
    [SerializeField] private string timeDecayOffText = "DECAY OFF";

    [Header("Stamina")]
    [SerializeField] private Slider staminaSlider;

    [Tooltip("플레이어 옆에 따라다니는 스태미나 UI 전체 오브젝트")]
    [SerializeField] private GameObject staminaWorldUIRoot;

    [Tooltip("스태미나가 최대치로 꽉 찬 뒤 UI를 숨기기까지 기다리는 시간")]
    [SerializeField] private float staminaHideDelay = 0.4f;

    [Tooltip("스태미나가 최대치로 판단되는 오차 범위. 0.1이면 MaxStamina - 0.1 이상부터 최대치로 판단")]
    [SerializeField] private float staminaFullThreshold = 0.1f;

    [Header("Exhausted 피드백")]
    [Tooltip("스태미나 30% 이하 진입 시 활성화할 오브젝트")]
    [SerializeField] private GameObject exhaustedIndicator;

    [Header("Skill Gauge")]
    [Tooltip("Image Type = Filled 로 설정한 Image 컴포넌트 연결")]
    [SerializeField] private Image skill1GaugeImage;
    [SerializeField] private Image skill2GaugeImage;
    [SerializeField] private Image skill3GaugeImage;

    [Header("Skill Gauge Text")]
    [Tooltip("스킬 게이지 퍼센트 텍스트 연결")]
    [SerializeField] private TMP_Text skill1GaugeText;
    [SerializeField] private TMP_Text skill2GaugeText;
    [SerializeField] private TMP_Text skill3GaugeText;

    [Tooltip("100%일 때 퍼센트 대신 READY로 표시할지 여부")]
    [SerializeField] private bool showReadyTextAtFullGauge = false;

    [Header("Skill Icon")]
    [Tooltip("스킬 슬롯 안의 실제 스킬 아이콘 Image 연결 (컬러=준비됨 레이어)")]
    [SerializeField] private Image skill1IconImage;
    [SerializeField] private Image skill2IconImage;
    [SerializeField] private Image skill3IconImage;

    [Header("New Skill Bar (명조식 5칸)")]
    [Tooltip("켜면 Q/E/R + 대쉬 + 퀵슬롯을 한 줄 명조식 바로 새로 그린다(기존 스킬 슬롯은 숨김). 끄면 구버전 HUD로 즉시 복귀.")]
    [SerializeField] private bool useNewSkillBar = true;

    [Tooltip("스킬 아이콘 크기 배율 (1=원래, 작게 하려면 0.7 등)")]
    [SerializeField, Range(0.3f, 1f)] private float skillIconScale = 0.72f;

    [Header("Skill Cooldown (쿨=회색+어두운 스윕 / 완료=밝은 색+반짝)")]
    [Tooltip("쿨타임 중 아이콘 뒤에 깔리는 '비활성' 색.")]
    [SerializeField] private Color skillGrayColor = new Color(0.13f, 0.14f, 0.17f, 1f);

    [Tooltip("쿨다운 중 스킬 아이콘 색(회색처리). 준비되면 ReadyIconColor로 확 바뀐다.")]
    [SerializeField] private Color skillCooldownIconColor = new Color(0.42f, 0.45f, 0.5f, 1f);
    [Tooltip("준비 완료 시 스킬 아이콘 색(밝게).")]
    [SerializeField] private Color skillReadyIconColor = Color.white;
    [Tooltip("쿨다운 스윕(도는 파이) 색. 남은 쿨을 덮는 어두운 부채꼴 - 어두울수록 잘 보임.")]
    [SerializeField] private Color skillCooldownSweepColor = new Color(0.03f, 0.05f, 0.08f, 0.72f);

    [Tooltip("준비 완료 시 아이콘 뒤에서 빛나는 글로우 색 (활성 강조)")]
    [SerializeField] private Color skillReadyGlowColor = new Color(0.37f, 0.77f, 1f, 1f);
    [Tooltip("준비 완료 글로우 최대 세기(알파)")]
    [SerializeField, Range(0f, 1f)] private float skillReadyGlowAlpha = 0.7f;
    [Tooltip("준비 완료 글로우 크기 배율(아이콘 대비)")]
    [SerializeField] private float skillReadyGlowScale = 1.6f;

    [Tooltip("(구버전) 쿨타임 중 fillAmount가 줄어드는 Image. 새 방식과 겹치므로 연결돼 있으면 자동으로 끔.")]
    [SerializeField] private Image skill1CooldownImage;
    [SerializeField] private Image skill2CooldownImage;
    [SerializeField] private Image skill3CooldownImage;

    // 아이콘 뒤 레이어 (스킬별): 회색 베이스 / 준비완료 글로우
    private readonly Dictionary<SkillSheetId, Image> _grayBase = new();
    private readonly Dictionary<SkillSheetId, Image> _readyGlow = new();

    // 쿨다운 -> 준비완료 전환 1회 반짝 감지용 (직전 프레임 준비 여부)
    private readonly Dictionary<SkillSheetId, bool> _wasReady = new();

    // 쿨다운 텍스트 (직렬화 필드가 비어 있으면 런타임 자동 생성해 아이콘 위에 표시 - 수동 연결 불필요)
    private readonly Dictionary<SkillSheetId, TMP_Text> _gaugeText = new();

    // 쿨다운 스윕 (아이콘 위 어두운 파이, 남은 쿨만큼 가림 -> 시계처럼 줄어듦)
    private readonly Dictionary<SkillSheetId, Image> _cdSweep = new();

    [Header("Base 상태 표시")]
    [Tooltip("기지 내부 TimeDecay 정지 시 활성화할 오브젝트")]
    [SerializeField] private GameObject baseStateIndicator;

    [Header("Hurt 피드백")]
    [Tooltip("피격 시 페이드 인/아웃할 CanvasGroup (화면 테두리 빨간색 이미지)")]
    [SerializeField] private CanvasGroup hurtVignette;
    [SerializeField] private float hurtFadeSpeed = 3f;

    [Header("플로팅 텍스트 (시간 증감)")]
    [Tooltip("시간 감소 시 뜨는 숫자 색 (예: -2)")]
    [SerializeField] private Color damageTextColor = new Color(1f, 0.36f, 0.39f, 1f);
    [Tooltip("시간 회복 시 뜨는 숫자 색 (예: +30)")]
    [SerializeField] private Color healTextColor = new Color(0.45f, 0.9f, 0.5f, 1f);
    [Tooltip("데미지/회복 숫자를 플레이어 옆에 띄움 (끄면 기존 HP바 옆)")]
    [SerializeField] private bool floatTextAtPlayer = true;
    [Tooltip("플레이어 발 기준 월드 높이 (가슴/머리쯤)")]
    [SerializeField] private float floatTextWorldHeight = 1.5f;
    [Tooltip("화면상 플레이어 우측으로 띄울 픽셀")]
    [SerializeField] private float floatTextScreenRight = 70f;
    [Tooltip("화면상 위로 보정할 픽셀")]
    [SerializeField] private float floatTextScreenUp = 20f;

    [Header("Tutorial")]
    [Tooltip("튜토리얼 스포트라이트가 강조할 시간/DECAY 패널(PlayerTime). 비우면 자동 탐색.")]
    [SerializeField] private RectTransform timeBarPanelRoot;

    private RectTransform _timeBarRoot;
    private readonly List<(string id, RectTransform rt)> _iconTargets = new();

    private float _hurtAlpha = 0f;

    // 플레이어 옆 스태미나 UI 표시/숨김 제어용
    private float _staminaHideTimer;

    void Start()
    {
        RegisterTutorialTargets();   // 튜토리얼 스포트라이트 타깃(time_bar/stat_button) 자동 등록 (playerStat 유무와 무관)

        // 자동 탐색
        if (playerStat == null || playerSkill == null)
        {
            var player = FindAnyObjectByType<Player>();
            if (player != null)
            {
                playerStat = player.GetComponent<PlayerStatComponent>();
                playerSkill = player.GetComponent<PlayerSkillComponent>();
            }
        }

        if (playerStat == null)
        {
            Debug.LogWarning("[PlayerHudUI] PlayerStatComponent를 찾을 수 없습니다.");
            return;
        }

        // 슬라이더 범위 초기화
        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = playerStat.MaxHp;
        }

        // HP 숫자 텍스트를 바보다 위 레이어로 올림 (불투명 Fill 에 가려지지 않도록)
        if (timeValueText != null)
            timeValueText.transform.SetAsLastSibling();

        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = playerStat.MaxStamina;
            staminaSlider.value = playerStat.CurrentStamina;
        }

        // 스태미나 UI 시작 상태 초기화
        // staminaHideDelay로 초기화: 게임 시작 직후에도 딜레이 0.4s 적용
        _staminaHideTimer = staminaHideDelay;

        // 시작 시에는 플레이어 옆 스태미나 UI 숨김
        if (staminaWorldUIRoot != null)
            staminaWorldUIRoot.SetActive(false);

        // 시작 시 TIME 텍스트 1회 갱신
        UpdateTimeText();

        // 시작 시 DECAY HUD 1회 갱신
        UpdateBaseState();

        // 스킬 슬롯 셋업: 바닥 게이지 바 제거 / 아이콘 축소 / 회색 베이스 + 시계방향 컬러 레이어 구성
        // 새 스킬바(명조식)를 쓰면 구버전 슬롯 셋업은 건너뛴다(아래 BuildSkillBar에서 숨김).
        if (!useNewSkillBar)
        {
            SetupSkillSlot(skill1GaugeImage, skill1GaugeText, skill1IconImage, SkillSheetId.Skill1);
            SetupSkillSlot(skill2GaugeImage, skill2GaugeText, skill2IconImage, SkillSheetId.Skill2);
            SetupSkillSlot(skill3GaugeImage, skill3GaugeText, skill3IconImage, SkillSheetId.Skill3);

            // 구버전 쿨다운 fill 이미지는 새 시계방향 방식과 겹치므로 끔
            if (skill1CooldownImage != null) skill1CooldownImage.enabled = false;
            if (skill2CooldownImage != null) skill2CooldownImage.enabled = false;
            if (skill3CooldownImage != null) skill3CooldownImage.enabled = false;
        }

        // 이벤트 구독
        playerStat.OnHurt += TriggerHurtVignette;
        playerStat.OnDead += ForceHpSliderEmpty;
        playerStat.OnDamaged += ShowDamageText;
        playerStat.OnHealed += ShowHealText;

        if (useNewSkillBar) BuildSkillBar();
        else BuildQuickSlotWidget();
    }

    // 명조식 5칸 스킬바 생성 + 구버전 스킬 슬롯 숨김. (퀵슬롯은 바에 포함되므로 구 위젯 안 만듦)
    void BuildSkillBar()
    {
        var player = playerStat != null ? playerStat.GetComponent<Player>() : null;
        if (player == null) { BuildQuickSlotWidget(); return; }

        var canvas = GetComponentInParent<Canvas>();
        RectTransform parent = canvas != null ? (RectTransform)canvas.transform : (RectTransform)transform;

        // 스킬 아이콘 스프라이트는 기존 직렬화 슬롯에서 가져온다(별도 에셋 배선 불필요).
        Sprite[] icons =
        {
            skill1IconImage != null ? skill1IconImage.sprite : null,
            skill2IconImage != null ? skill2IconImage.sprite : null,
            skill3IconImage != null ? skill3IconImage.sprite : null,
        };

        var go = new GameObject("SkillBar", typeof(RectTransform));
        var bar = go.AddComponent<SkillBarUI>();
        TMP_FontAsset hudFont = timeValueText != null ? timeValueText.font : null;
        bar.Setup(parent, player, icons, hudFont);

        // 표시/숨김(다른 UI 열림 시 숨김 + 결계 안 자동 페이드)은 SkillBarUI가 스스로 관리한다.
        // (HudAutoFader 외부 등록에 의존하지 않게 - 등록 타이밍/그리는 순서 취약성 제거)

        // 구버전 스킬 슬롯 숨김(새 바로 대체). 토글 끄면 원복.
        HideOldSkillSlot(skill1IconImage);
        HideOldSkillSlot(skill2IconImage);
        HideOldSkillSlot(skill3IconImage);
    }

    void HideOldSkillSlot(Image iconImage)
    {
        if (iconImage == null) return;
        var card = iconImage.transform.parent;   // Skill_N
        if (card != null) card.gameObject.SetActive(false);
        else iconImage.gameObject.SetActive(false);
    }

    // 소모품 퀵슬롯 HUD 위젯 런타임 생성 (구버전 — useNewSkillBar=false 일 때만).
    void BuildQuickSlotWidget()
    {
        var player = playerStat != null ? playerStat.GetComponent<Player>() : null;
        if (player == null || player.QuickSlot == null) return;

        var canvas = GetComponentInParent<Canvas>();
        RectTransform parent = canvas != null ? (RectTransform)canvas.transform : (RectTransform)transform;

        var go = new GameObject("QuickSlotWidget", typeof(RectTransform));
        var widget = go.AddComponent<PlayerQuickSlotHudUI>();
        KeyCode key = player.Input != null ? player.Input.QuickSlotKey : KeyCode.V;
        widget.Setup(parent, player.QuickSlot, key);
    }

    void OnDestroy()
    {
        if (playerStat != null)
        {
            playerStat.OnHurt -= TriggerHurtVignette;
            playerStat.OnDead -= ForceHpSliderEmpty;
            playerStat.OnDamaged -= ShowDamageText;
            playerStat.OnHealed -= ShowHealText;
        }

        if (_timeBarRoot != null)
            TutorialOverlay.UnregisterTarget("time_bar", _timeBarRoot);
        foreach (var it in _iconTargets)
            TutorialOverlay.UnregisterTarget(it.id, it.rt);
        _iconTargets.Clear();
    }

    // 튜토리얼 스포트라이트 타깃 자동 등록 (씬 수동 부착 불필요).
    void RegisterTutorialTargets()
    {
        // time_bar = 좌하단 시간/DECAY 패널(PlayerTime). hpSlider/timeValueText에서 부모로 올라가 탐색.
        _timeBarRoot = timeBarPanelRoot != null ? timeBarPanelRoot : FindTimeBarRoot();
        if (_timeBarRoot != null)
            TutorialOverlay.RegisterTarget("time_bar", _timeBarRoot);

        // stamina_bar = HUD 스태미나 막대. 인트로 투어에서 스태미나 로직(소모/회복) 강조용.
        // staminaSlider(uGUI) 우선, 없으면 셰이더 기반 StaminaBarUI 로 폴백. 둘 다 없으면 투어는 텍스트만.
        RectTransform staminaRt = staminaSlider != null ? (RectTransform)staminaSlider.transform : null;
        if (staminaRt == null)
        {
            var sb = FindAnyObjectByType<StaminaBarUI>();
            if (sb != null) staminaRt = sb.transform as RectTransform;
        }
        if (staminaRt != null)
            RegisterRectTarget(staminaRt, "stamina_bar");

        // 우측 상단 키 안내 박스 4개 → 스포트라이트 타깃.
        // 각 키는 라벨(*_Text)+아이콘(*_Icon)이 KeyGuide 바로 아래 평면 구조로 흩어져 있으므로
        // 같은 id에 둘 다 등록한다. TutorialOverlay가 같은 id의 rect들을 합집합으로 묶어
        // 라벨+아이콘을 한 박스로 감싼다 (별도 컨테이너 오브젝트 불필요, 4개 일관성 보장).
        RegisterKeyBox("stat_button", "C_Text", "C_Icon");
        RegisterKeyBox("tab_icon", "TAB_Text", "TAB_Icon");
        RegisterKeyBox("b_icon", "B_Text", "B_Icon");
        RegisterKeyBox("esc_icon", "ESC_Text", "Esc_Icon");

        // 인트로에서 메뉴 전체(K/TAB/B/C/ESC)를 한 번에 강조하기 위한 합집합 타깃(같은 id의 rect들을 한 박스로). 도감(K) 포함.
        RegisterKeyBox("menu_icons", "K_Text", "K_Icon", "TAB_Text", "TAB_Icon", "B_Text", "B_Icon", "C_Text", "C_Icon", "ESC_Text", "Esc_Icon");

        // 우하단 스킬 슬롯 Q/E/R → 스포트라이트 (인트로 투어에서 충전 조건 설명).
        // 직렬화된 아이콘 참조의 부모(슬롯)를 등록 — 게이지 링까지 포함된 슬롯 전체를 강조.
        // 새 스킬바를 쓰면 스킬 스포트라이트(skill_q/e/r)는 SkillBarUI가 등록한다(여기선 skip).
        if (!useNewSkillBar)
        {
            RegisterSkillSlot(skill1IconImage, "skill_q");
            RegisterSkillSlot(skill2IconImage, "skill_e");
            RegisterSkillSlot(skill3IconImage, "skill_r");
        }
    }

    [Tooltip("스킬 강조 박스를 아이콘 하단에서 아래로 더 내리는 양(px). 0% 바 쪽까지 살짝 덮되, 카드 밑(화면 가장자리)까진 안 가게.")]
    [SerializeField] private float skillFrameExtendDownPx = 55f;

    // 스킬 카드 강조 — 위 키라벨(Keycap_BG) + 아트(아이콘) + 아래 확장(0% 바 쪽). 폭은 아이콘(카드폭)에 맞춤.
    // 맨 아래 게이지 바(Gauge_BG)를 통째로 넣으면 카드 밑=화면 가장자리까지 내려가 잘리므로,
    // 대신 아이콘 아래에 얇은 확장 rect를 만들어 합집합 바닥을 0% 바 근처까지만 연장한다.
    void RegisterSkillSlot(Image iconImage, string spotlightId)
    {
        if (iconImage == null) return;
        var iconRt = iconImage.rectTransform;
        RegisterRectTarget(iconRt, spotlightId);

        var card = iconRt.parent;   // Skill_N (아이콘/키캡의 공통 부모)
        if (card != null)
            RegisterRectTarget(card.Find("Keycap_BG") as RectTransform, spotlightId);

        // 바닥 연장용 얇은 합성 rect (아이콘 폭, 아이콘 하단에서 skillFrameExtendDownPx만큼 아래)
        if (card != null && skillFrameExtendDownPx > 0f)
        {
            var ext = new GameObject("SkillSpotlightExt", typeof(RectTransform)).GetComponent<RectTransform>();
            ext.SetParent(card, false);
            ext.anchorMin = iconRt.anchorMin;
            ext.anchorMax = iconRt.anchorMax;
            ext.pivot = iconRt.pivot;
            ext.localScale = Vector3.one;
            ext.sizeDelta = new Vector2(iconRt.sizeDelta.x, 2f);
            float iconBottom = iconRt.anchoredPosition.y - iconRt.sizeDelta.y * 0.5f;
            ext.anchoredPosition = new Vector2(iconRt.anchoredPosition.x, iconBottom - skillFrameExtendDownPx);
            RegisterRectTarget(ext, spotlightId);
        }
    }

    void RegisterRectTarget(RectTransform rt, string spotlightId)
    {
        if (rt == null) return;
        _iconTargets.Add((spotlightId, rt));
        TutorialOverlay.RegisterTarget(spotlightId, rt);
    }

    // 한 키의 라벨+아이콘을 같은 스포트라이트 id로 등록 (합집합 박스).
    void RegisterKeyBox(string spotlightId, params string[] objectNames)
    {
        foreach (var objectName in objectNames)
            RegisterIcon(objectName, spotlightId);
    }

    void RegisterIcon(string objectName, string spotlightId)
    {
        var rt = FindDescendant(transform, objectName) as RectTransform;
        if (rt == null) return;
        _iconTargets.Add((spotlightId, rt));
        TutorialOverlay.RegisterTarget(spotlightId, rt);
    }

    RectTransform FindTimeBarRoot()
    {
        Transform t = hpSlider != null ? hpSlider.transform
                    : timeValueText != null ? timeValueText.transform
                    : null;
        for (; t != null; t = t.parent)
            if (t.name == "PlayerTime") return t as RectTransform;
        return null;
    }

    static Transform FindDescendant(Transform root, string targetName)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == targetName) return c;
            var found = FindDescendant(c, targetName);
            if (found != null) return found;
        }
        return null;
    }

    // 사망 즉시 슬라이더를 0으로 강제 설정
    // screenBlur가 슬라이더를 가리기 전에 확실하게 빈 상태로 만듦
    void ForceHpSliderEmpty()
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = playerStat != null ? playerStat.MaxHp : hpSlider.maxValue;
            hpSlider.value    = 0f;
        }
    }

    // 저시간(저HP) 경고 토스트 — 마지막으로 경고한 정수 시간 (10 이하부터 매 정수 감소마다)
    int _lastTimeWarn = int.MaxValue;

    void Update()
    {
        if (playerStat == null) return;

        UpdateHpStamina();
        UpdateExhausted();
        UpdateBaseState();
        UpdateHurtVignette();
        CheckLowTimeWarning();

        if (playerSkill == null) return;

        // 새 스킬바를 쓰면 SkillBarUI가 자체 갱신하므로 구버전 게이지 갱신은 skip.
        if (!useNewSkillBar) UpdateSkillGauge();
    }

    // 시간(체력) 표시 정수화 = CeilToInt(0 가드). 양수면 최소 1로 보이고 진짜 0일 때만 0.
    // -> "보이는 0 = 사망" 보장(사망 임계값 <=0 은 안 건드림). 바텍스트/토스트가 같은 함수를 써 어긋남 방지.
    static int TimeDisplay(float hp) => hp <= 0f ? 0 : Mathf.CeilToInt(hp);

    // 시간(HP)이 10 이하로 떨어지면 매 정수마다 경고 토스트. 피격으로 점프해도 그 지점부터 다시.
    void CheckLowTimeWarning()
    {
        if (playerStat.IsDead) return;
        int t = TimeDisplay(playerStat.CurrentHp);
        if (t > 10) { _lastTimeWarn = int.MaxValue; return; }   // 10 초과로 회복하면 리셋
        if (t >= 1 && t < _lastTimeWarn)
        {
            ToastManager.Warning(string.Format(Loc.Get("시간이 {0}초 남았습니다!"), t));
            _lastTimeWarn = t;
        }
    }

    // HP·스태미나 슬라이더 갱신
    void UpdateHpStamina()
    {
        if (hpSlider != null)
        {
            // maxValue를 매 프레임 동기화 — Start() 이후 MaxHp가 바뀌어도 추적 보장
            // Unity Slider는 value를 [minValue, maxValue]로 클램프하므로
            // maxValue가 실제 MaxHp보다 작으면 슬라이더가 중간에서 멈춰 보이는 버그 발생
            hpSlider.maxValue = playerStat.MaxHp;
            hpSlider.value    = playerStat.CurrentHp;
        }

        if (staminaSlider != null)
        {
            // HP바와 동일 - 런타임에 MaxStamina가 커져도(스태미나 앰플) 추적. 안 하면 옛 max에 클램프돼 바가 항상 꽉 차 보임.
            staminaSlider.maxValue = playerStat.MaxStamina;
            staminaSlider.value    = playerStat.CurrentStamina;
        }

        UpdateTimeText();
        UpdateStaminaWorldUI();
    }

    // 플레이어 옆 스태미나 UI 표시/숨김 갱신
    void UpdateStaminaWorldUI()
    {
        if (staminaWorldUIRoot == null || playerStat == null)
            return;

        float current = playerStat.CurrentStamina;
        float max = playerStat.MaxStamina;

        bool isStaminaFull = current >= max - staminaFullThreshold;

        // 스태미나가 최대치가 아니면 UI 표시
        if (!isStaminaFull)
        {
            if (!staminaWorldUIRoot.activeSelf)
                staminaWorldUIRoot.SetActive(true);

            _staminaHideTimer = staminaHideDelay;
            return;
        }

        // 스태미나가 최대치로 꽉 차면 일정 시간 뒤 UI 숨김
        if (_staminaHideTimer > 0f)
        {
            _staminaHideTimer -= Time.deltaTime;
        }
        else
        {
            if (staminaWorldUIRoot.activeSelf)
                staminaWorldUIRoot.SetActive(false);
        }
    }

    // TIME 텍스트 갱신
    void UpdateTimeText()
    {
        if (timeValueText == null || playerStat == null) return;

        int currentTime = TimeDisplay(playerStat.CurrentHp);
        int maxTime = Mathf.RoundToInt(playerStat.MaxHp);

        timeValueText.text = $"{timeTextPrefix}{currentTime}s/{maxTime}s";
    }

    // Exhausted 표시 갱신
    // 스태미나가 ExhaustedThreshold(30%) 이하이면 경고 표시
    // → IsExhausted(0%~30% 회복 구간)뿐 아니라 "30%로 내려가는 과정"도 포함
    void UpdateExhausted()
    {
        if (exhaustedIndicator == null) return;

        float ratio = playerStat.CurrentStamina / playerStat.MaxStamina;
        bool isLowOrExhausted = ratio <= playerStat.ExhaustedThreshold;
        exhaustedIndicator.SetActive(isLowOrExhausted);
    }

    // Base 상태 / Time Decay HUD 표시 갱신
    void UpdateBaseState()
    {
        bool isInBase = playerStat.IsInBase;
        bool isTimeDecaying = !isInBase;

        // 기존 Base 상태 표시 오브젝트가 있다면 유지
        if (baseStateIndicator != null)
            baseStateIndicator.SetActive(isInBase);

        // 시계 아이콘 색상 변경
        if (timeDecayIcon != null)
            timeDecayIcon.color = isTimeDecaying ? timeDecayOnColor : timeDecayOffColor;

        // DECAY ON / DECAY OFF 텍스트 변경
        if (timeDecayStateText != null)
        {
            timeDecayStateText.text = isTimeDecaying ? timeDecayOnText : timeDecayOffText;
            timeDecayStateText.color = isTimeDecaying ? timeDecayOnColor : timeDecayOffColor;
        }

        // -1.0 / sec 텍스트는 시간 감소 중일 때만 표시
        if (timeDecayRateText != null)
            timeDecayRateText.gameObject.SetActive(isTimeDecaying);
    }

    // 스킬 게이지 fillAmount, 퍼센트 텍스트, 아이콘 투명도 갱신
    void UpdateSkillGauge()
    {
        UpdateSingleSkillGauge(skill1IconImage, SkillSheetId.Skill1);
        UpdateSingleSkillGauge(skill2IconImage, SkillSheetId.Skill2);
        UpdateSingleSkillGauge(skill3IconImage, SkillSheetId.Skill3);
    }

    // 스킬 슬롯 초기 셋업: 바닥 게이지 바 제거 / 아이콘 축소 / 회색 베이스 + 시계방향 컬러 레이어 구성
    void SetupSkillSlot(Image gaugeImage, TMP_Text gaugeText, Image iconImage, SkillSheetId id)
    {
        // 바닥 게이지 바 제거 (Gauge_Fill + Gauge_BG 의 Image만 끔 - 자식 텍스트는 유지)
        if (gaugeImage != null)
        {
            gaugeImage.enabled = false;
            var parent = gaugeImage.transform.parent;
            if (parent != null && parent.name.Contains("Gauge"))
            {
                var bg = parent.GetComponent<Image>();
                if (bg != null) bg.enabled = false;
            }
        }

        if (iconImage == null) return;

        var iconRt = iconImage.rectTransform;

        // 아이콘 축소
        iconRt.localScale = Vector3.one * skillIconScale;

        // [회색 베이스] 같은 스프라이트를 어둡게 깔아 '비활성' 상태 표현. 아이콘 바로 뒤(아래)에 배치.
        var baseGo = new GameObject("SkillIconGray", typeof(RectTransform));
        var brt = (RectTransform)baseGo.transform;
        brt.SetParent(iconRt.parent, false);
        brt.anchorMin = iconRt.anchorMin;
        brt.anchorMax = iconRt.anchorMax;
        brt.pivot = iconRt.pivot;
        brt.sizeDelta = iconRt.sizeDelta;
        brt.anchoredPosition = iconRt.anchoredPosition;
        brt.localScale = iconRt.localScale;
        brt.SetSiblingIndex(iconRt.GetSiblingIndex());   // 아이콘보다 뒤(아래)로

        var grayImg = baseGo.AddComponent<Image>();
        grayImg.sprite = iconImage.sprite;
        grayImg.preserveAspect = iconImage.preserveAspect;
        grayImg.color = skillGrayColor;
        grayImg.raycastTarget = false;
        grayImg.type = Image.Type.Simple;
        _grayBase[id] = grayImg;

        // [준비 완료 글로우] 회색 베이스 앞 / 컬러 아이콘 뒤. 쿨 끝날수록 켜지고 준비완료 시 맥동.
        var glowGo = new GameObject("SkillReadyGlow", typeof(RectTransform));
        var grt = (RectTransform)glowGo.transform;
        grt.SetParent(iconRt.parent, false);
        grt.anchorMin = iconRt.anchorMin;
        grt.anchorMax = iconRt.anchorMax;
        grt.pivot = iconRt.pivot;
        grt.sizeDelta = iconRt.sizeDelta;
        grt.anchoredPosition = iconRt.anchoredPosition;
        grt.localScale = Vector3.one * (skillIconScale * skillReadyGlowScale);
        grt.SetSiblingIndex(iconRt.GetSiblingIndex());   // 회색 베이스 앞, 컬러 아이콘 뒤

        var glowImg = glowGo.AddComponent<Image>();
        glowImg.sprite = UISpriteFactory.Disc(128);
        glowImg.color = new Color(skillReadyGlowColor.r, skillReadyGlowColor.g, skillReadyGlowColor.b, 0f);
        glowImg.raycastTarget = false;
        _readyGlow[id] = glowImg;

        // 아이콘은 통째로 보이게(Simple). 쿨 표현은 색(회색<->밝음) + 그 위 어두운 스윕 파이로.
        iconImage.type = Image.Type.Simple;
        iconImage.color = skillReadyIconColor;   // 시작은 준비됨(밝게)

        // [쿨다운 스윕] 아이콘 위에 어두운 파이를 덮어 '남은 쿨'만큼 가린다 -> 시계처럼 줄어들며 회수(도는 게 확실히 보임).
        var sweepGo = new GameObject("SkillCooldownSweep", typeof(RectTransform));
        var srt = (RectTransform)sweepGo.transform;
        srt.SetParent(iconRt.parent, false);
        srt.anchorMin = iconRt.anchorMin;
        srt.anchorMax = iconRt.anchorMax;
        srt.pivot = iconRt.pivot;
        srt.sizeDelta = iconRt.sizeDelta;
        srt.anchoredPosition = iconRt.anchoredPosition;
        srt.localScale = iconRt.localScale;
        srt.SetAsLastSibling();   // 아이콘 위 (쿨 숫자/반짝보다는 아래 = 아래에서 텍스트를 마지막에 올림)

        var sweepImg = sweepGo.AddComponent<Image>();
        sweepImg.sprite = UISpriteFactory.Circle(96);
        sweepImg.color = skillCooldownSweepColor;
        sweepImg.raycastTarget = false;
        sweepImg.type = Image.Type.Filled;
        sweepImg.fillMethod = Image.FillMethod.Radial360;
        sweepImg.fillOrigin = (int)Image.Origin360.Top;
        sweepImg.fillClockwise = false;   // 쿨 회수는 시계방향(유저 친화적). true면 반시계로 돌아서 뒤집음.
        sweepImg.fillAmount = 0f;   // 시작은 준비됨(안 가림)
        _cdSweep[id] = sweepImg;

        _wasReady[id] = true;   // 시작은 준비됨 = 시작하자마자 반짝 안 뜨게

        // 쿨 텍스트: 인스펙터 연결돼 있으면 그걸, 비어 있으면 아이콘 위에 자동 생성(수동 연결 불필요)
        _gaugeText[id] = gaugeText != null ? gaugeText : CreateGaugeText(iconImage);
    }

    // 쿨다운 숫자 텍스트를 아이콘 위에 런타임 생성 (직렬화 필드가 비어 있을 때).
    TMP_Text CreateGaugeText(Image iconImage)
    {
        if (iconImage == null) return null;
        var iconRt = iconImage.rectTransform;
        var parent = iconRt.parent;
        if (parent == null) return null;

        var go = new GameObject("SkillCooldownText", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = iconRt.anchorMin;
        rt.anchorMax = iconRt.anchorMax;
        rt.pivot = iconRt.pivot;
        rt.anchoredPosition = iconRt.anchoredPosition;
        rt.sizeDelta = new Vector2(iconRt.sizeDelta.x * skillIconScale, iconRt.sizeDelta.y * skillIconScale);
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();   // 아이콘/글로우 위에

        var t = go.AddComponent<TextMeshProUGUI>();
        if (timeValueText != null) t.font = timeValueText.font;   // HUD 폰트 재사용
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = false;
        t.fontSize = Mathf.Max(18f, iconRt.sizeDelta.y * skillIconScale * 0.45f);
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = "";
        return t;
    }

    void UpdateSingleSkillGauge(Image iconImage, SkillSheetId id)
    {
        float gauge = playerSkill.GetGauge(id);          // 쿨다운 진행도(0~100)
        float readiness = Mathf.Clamp01(gauge / 100f);
        bool ready = readiness >= 0.999f;

        // 쿨 중 = 회색, 준비완료 = 밝은 색 (확실히 구분). 전환 순간은 아래 반짝이 덮어줌.
        if (iconImage != null)
            iconImage.color = ready ? skillReadyIconColor : skillCooldownIconColor;

        // 어두운 스윕 파이로 남은 쿨을 덮음(시계처럼 줄어듦). 준비완료면 0 = 안 가림.
        if (_cdSweep.TryGetValue(id, out var sweep) && sweep != null)
            sweep.fillAmount = 1f - readiness;

        // 쿨다운 -> 준비완료로 막 바뀌는 순간 1회 반짝(롤식). 시작 시 이미 준비됨은 제외.
        if (_wasReady.TryGetValue(id, out bool wasReady) && !wasReady && ready)
            PlaySkillReadyFx(iconImage);
        _wasReady[id] = ready;

        // 준비 완료 글로우: 쿨 끝날수록 밝아지고(charge), 준비완료면 맥동(pulse)으로 '활성' 강조.
        if (_readyGlow.TryGetValue(id, out var glow) && glow != null)
        {
            float charge = readiness * readiness;                 // 초중반 어둡게, 끝물에 글로우 키움
            float pulse = ready ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;
            var gc = glow.color;
            gc.a = skillReadyGlowAlpha * charge * pulse;
            glow.color = gc;
            float scalePulse = ready ? 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;
            glow.rectTransform.localScale = Vector3.one * (skillIconScale * skillReadyGlowScale * scalePulse);
        }

        if (_gaugeText.TryGetValue(id, out var gaugeText) && gaugeText != null)
        {
            // 쿨 중이면 남은 초(60, 59, ... 평타치면 줄어듦), 다 돌면 READY/공백
            float remaining = playerSkill.GetCooldown(id);
            if (remaining > 0.05f)
                gaugeText.text = Mathf.CeilToInt(remaining).ToString();
            else
                gaugeText.text = showReadyTextAtFullGauge ? "READY" : "";
        }
    }

    // 쿨다운이 막 끝난 순간 아이콘 위에 1회 반짝(플래시+링). 자가 소멸.
    void PlaySkillReadyFx(Image iconImage)
    {
        if (iconImage == null) return;
        var iconRt = iconImage.rectTransform;
        var parent = iconRt.parent as RectTransform;
        if (parent == null) return;

        float s = Mathf.Max(iconRt.sizeDelta.x, iconRt.sizeDelta.y) * skillIconScale;
        if (s < 1f) s = 48f;
        Color fx = new Color(skillReadyGlowColor.r, skillReadyGlowColor.g, skillReadyGlowColor.b, 1f);

        var flash = MakeSkillFxImage("SkillReadyFlash", parent, UISpriteFactory.Disc(96), iconRt, s * 0.7f, fx);
        flash.rectTransform.DOSizeDelta(new Vector2(s * 1.7f, s * 1.7f), 0.4f).SetUpdate(true).SetEase(Ease.OutCubic);
        flash.DOFade(0f, 0.4f).SetUpdate(true).OnComplete(() => { if (flash) Destroy(flash.gameObject); });

        var ring = MakeSkillFxImage("SkillReadyRing", parent, UISpriteFactory.Ring(96, 5f), iconRt, s * 0.9f, fx);
        ring.rectTransform.DOScale(1.8f, 0.45f).SetUpdate(true).SetEase(Ease.OutCubic);
        ring.DOFade(0f, 0.45f).SetUpdate(true).OnComplete(() => { if (ring) Destroy(ring.gameObject); });
    }

    // 반짝 FX 이미지 한 장 생성 (아이콘 위치/앵커에 맞춰 가운데, 최상단 레이어).
    Image MakeSkillFxImage(string objName, RectTransform parent, Sprite sprite, RectTransform anchorTo, float size, Color color)
    {
        var go = new GameObject(objName, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorTo.anchorMin;
        rt.anchorMax = anchorTo.anchorMax;
        rt.pivot = anchorTo.pivot;
        rt.anchoredPosition = anchorTo.anchoredPosition;
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // 피격 시 Vignette 트리거
    void TriggerHurtVignette()
    {
        _hurtAlpha = 1f;
    }

    // 시간(HP) 감소 → 빨간 -N 플로팅 텍스트 (시계/HP 근처)
    void ShowDamageText(float amount)
    {
        int n = Mathf.RoundToInt(amount);
        if (n <= 0) return;   // 0.x 단위는 '-0' 방지
        FloatingTextManager.Show($"-{n}", damageTextColor, GetFloatTextPos());
    }

    // 시간(HP) 회복 → 초록 +N 플로팅 텍스트
    void ShowHealText(float amount)
    {
        int n = Mathf.RoundToInt(amount);
        if (n <= 0) return;   // 0.x 단위는 '+0' 방지
        FloatingTextManager.Show($"+{n}", healTextColor, GetFloatTextPos());
    }

    // 플로팅 텍스트가 떠오를 화면 좌표 (좌하단 시계 아이콘 우선, 없으면 HP 바)
    Vector3 GetTimeAnchorScreenPos()
    {
        if (timeDecayIcon != null) return timeDecayIcon.rectTransform.position;
        if (hpSlider != null) return hpSlider.transform.position;
        return new Vector3(Screen.width * 0.13f, Screen.height * 0.18f, 0f);
    }

    // 데미지/회복 텍스트가 뜰 화면좌표 = 플레이어 우측 (toggle 꺼지면 기존 HP바 위치)
    Vector3 GetFloatTextPos()
    {
        if (floatTextAtPlayer && playerStat != null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 wp = playerStat.transform.position + Vector3.up * floatTextWorldHeight;
                Vector3 sp = cam.WorldToScreenPoint(wp);
                if (sp.z > 0f)   // 카메라 앞일 때만
                {
                    sp.x += floatTextScreenRight;
                    sp.y += floatTextScreenUp;
                    sp.z = 0f;
                    return sp;
                }
            }
        }
        return GetTimeAnchorScreenPos();   // 폴백 (카메라 뒤/없을 때)
    }

    // Hurt Vignette 페이드 아웃
    void UpdateHurtVignette()
    {
        if (hurtVignette == null) return;

        if (_hurtAlpha > 0f)
            _hurtAlpha = Mathf.Max(0f, _hurtAlpha - Time.deltaTime * hurtFadeSpeed);

        hurtVignette.alpha = _hurtAlpha;
    }
}