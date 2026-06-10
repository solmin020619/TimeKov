using System.Collections.Generic;
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

    [Header("Skill Icon Alpha")]
    [Tooltip("스킬 슬롯 안의 실제 스킬 아이콘 Image 연결")]
    [SerializeField] private Image skill1IconImage;
    [SerializeField] private Image skill2IconImage;
    [SerializeField] private Image skill3IconImage;

    [Tooltip("스킬 게이지가 100% 미만일 때 아이콘 투명도")]
    [SerializeField, Range(0f, 1f)] private float skillIconLockedAlpha = 0.35f;

    [Tooltip("스킬 게이지가 100% 이상일 때 아이콘 투명도")]
    [SerializeField, Range(0f, 1f)] private float skillIconReadyAlpha = 1f;

    [Tooltip("아이콘 투명도 전환 속도. 높을수록 빠르게 바뀜")]
    [SerializeField] private float skillIconAlphaLerpSpeed = 10f;

    [Header("Skill Cooldown")]
    [Tooltip("쿨타임 중 fillAmount가 줄어드는 Image 컴포넌트 연결")]
    [SerializeField] private Image skill1CooldownImage;
    [SerializeField] private Image skill2CooldownImage;
    [SerializeField] private Image skill3CooldownImage;

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

        // 시작 시 스킬 아이콘 투명도 초기화
        SetImageAlpha(skill1IconImage, skillIconLockedAlpha);
        SetImageAlpha(skill2IconImage, skillIconLockedAlpha);
        SetImageAlpha(skill3IconImage, skillIconLockedAlpha);

        // 이벤트 구독
        playerStat.OnHurt += TriggerHurtVignette;
        playerStat.OnDead += ForceHpSliderEmpty;
        playerStat.OnDamaged += ShowDamageText;
        playerStat.OnHealed += ShowHealText;
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

        // 우측 상단 키 안내 박스 4개 → 스포트라이트 타깃.
        // 각 키는 라벨(*_Text)+아이콘(*_Icon)이 KeyGuide 바로 아래 평면 구조로 흩어져 있으므로
        // 같은 id에 둘 다 등록한다. TutorialOverlay가 같은 id의 rect들을 합집합으로 묶어
        // 라벨+아이콘을 한 박스로 감싼다 (별도 컨테이너 오브젝트 불필요, 4개 일관성 보장).
        RegisterKeyBox("stat_button", "C_Text", "C_Icon");
        RegisterKeyBox("tab_icon", "TAB_Text", "TAB_Icon");
        RegisterKeyBox("b_icon", "B_Text", "B_Icon");
        RegisterKeyBox("esc_icon", "ESC_Text", "Esc_Icon");

        // 우하단 스킬 슬롯 Q/E/R → 스포트라이트 (인트로 투어에서 충전 조건 설명).
        // 직렬화된 아이콘 참조의 부모(슬롯)를 등록 — 게이지 링까지 포함된 슬롯 전체를 강조.
        RegisterSkillSlot(skill1IconImage, "skill_q");
        RegisterSkillSlot(skill2IconImage, "skill_e");
        RegisterSkillSlot(skill3IconImage, "skill_r");
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

    void Update()
    {
        if (playerStat == null) return;

        UpdateHpStamina();
        UpdateExhausted();
        UpdateBaseState();
        UpdateHurtVignette();

        if (playerSkill == null) return;

        UpdateSkillGauge();
        UpdateCooldown();
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
            staminaSlider.value = playerStat.CurrentStamina;

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

        int currentTime = Mathf.RoundToInt(playerStat.CurrentHp);
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
        UpdateSingleSkillGauge(
            skill1GaugeImage,
            skill1GaugeText,
            skill1IconImage,
            SkillSheetId.Skill1
        );

        UpdateSingleSkillGauge(
            skill2GaugeImage,
            skill2GaugeText,
            skill2IconImage,
            SkillSheetId.Skill2
        );

        UpdateSingleSkillGauge(
            skill3GaugeImage,
            skill3GaugeText,
            skill3IconImage,
            SkillSheetId.Skill3
        );
    }

    void UpdateSingleSkillGauge(Image gaugeImage, TMP_Text gaugeText, Image iconImage, SkillSheetId id)
    {
        float gauge = playerSkill.GetGauge(id);
        float normalizedGauge = Mathf.Clamp01(gauge / 100f);

        if (gaugeImage != null)
            gaugeImage.fillAmount = normalizedGauge;

        if (gaugeText != null)
        {
            // LoL식 쿨다운 표기: 쿨 중이면 남은 초(60→59→…→평타치면 줄어듦), 다 돌면 READY/공백
            float remaining = playerSkill.GetCooldown(id);
            if (remaining > 0.05f)
                gaugeText.text = Mathf.CeilToInt(remaining).ToString();
            else
                gaugeText.text = showReadyTextAtFullGauge ? "READY" : "";
        }

        if (iconImage != null)
        {
            float targetAlpha = gauge >= 100f ? skillIconReadyAlpha : skillIconLockedAlpha;

            Color color = iconImage.color;
            color.a = Mathf.Lerp(color.a, targetAlpha, Time.deltaTime * skillIconAlphaLerpSpeed);
            iconImage.color = color;
        }
    }

    // 쿨타임 fillAmount 갱신 (쿨타임 남을수록 채워짐)
    void UpdateCooldown()
    {
        UpdateCooldownImage(skill1CooldownImage, SkillSheetId.Skill1);
        UpdateCooldownImage(skill2CooldownImage, SkillSheetId.Skill2);
        UpdateCooldownImage(skill3CooldownImage, SkillSheetId.Skill3);
    }

    void UpdateCooldownImage(Image img, SkillSheetId id)
    {
        if (img == null) return;

        float max = playerSkill.GetMaxCooldown(id);
        img.fillAmount = max > 0 ? playerSkill.GetCooldown(id) / max : 0f;
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
        FloatingTextManager.Show($"-{n}", damageTextColor, GetTimeAnchorScreenPos());
    }

    // 시간(HP) 회복 → 초록 +N 플로팅 텍스트
    void ShowHealText(float amount)
    {
        int n = Mathf.RoundToInt(amount);
        if (n <= 0) return;   // 0.x 단위는 '+0' 방지
        FloatingTextManager.Show($"+{n}", healTextColor, GetTimeAnchorScreenPos());
    }

    // 플로팅 텍스트가 떠오를 화면 좌표 (좌하단 시계 아이콘 우선, 없으면 HP 바)
    Vector3 GetTimeAnchorScreenPos()
    {
        if (timeDecayIcon != null) return timeDecayIcon.rectTransform.position;
        if (hpSlider != null) return hpSlider.transform.position;
        return new Vector3(Screen.width * 0.13f, Screen.height * 0.18f, 0f);
    }

    // Hurt Vignette 페이드 아웃
    void UpdateHurtVignette()
    {
        if (hurtVignette == null) return;

        if (_hurtAlpha > 0f)
            _hurtAlpha = Mathf.Max(0f, _hurtAlpha - Time.deltaTime * hurtFadeSpeed);

        hurtVignette.alpha = _hurtAlpha;
    }

    void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;

        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }
}