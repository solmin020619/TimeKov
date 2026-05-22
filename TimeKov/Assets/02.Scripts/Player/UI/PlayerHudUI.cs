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

    private float _hurtAlpha = 0f;

    // 플레이어 옆 스태미나 UI 표시/숨김 제어용
    private float _staminaHideTimer;

    void Start()
    {
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

        // 피격 이벤트 구독
        playerStat.OnHurt += TriggerHurtVignette;
    }

    void OnDestroy()
    {
        if (playerStat != null)
            playerStat.OnHurt -= TriggerHurtVignette;
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
            hpSlider.value = playerStat.CurrentHp;

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
            int percent = Mathf.RoundToInt(normalizedGauge * 100f);

            if (showReadyTextAtFullGauge && percent >= 100)
                gaugeText.text = "READY";
            else
                gaugeText.text = $"{percent}%";
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