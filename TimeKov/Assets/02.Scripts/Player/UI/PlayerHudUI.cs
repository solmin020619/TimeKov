using UnityEngine;
using UnityEngine.UI;

public class PlayerHudUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("PlayerStatComponent가 붙어있는 플레이어 오브젝트. 비워두면 씬에서 자동 탐색.")]
    [SerializeField] private PlayerStatComponent playerStat;
    [SerializeField] private PlayerSkillComponent playerSkill;

    [Header("HP")]
    [SerializeField] private Slider hpSlider;

    [Header("Stamina")]
    [SerializeField] private Slider staminaSlider;

    [Header("Exhausted 피드백")]
    [Tooltip("스태미나 30% 이하 진입 시 활성화할 오브젝트")]
    [SerializeField] private GameObject exhaustedIndicator;

    [Header("Skill Gauge")]
    [Tooltip("Image Type = Filled 로 설정한 Image 컴포넌트 연결")]
    [SerializeField] private Image skill1GaugeImage;
    [SerializeField] private Image skill2GaugeImage;
    [SerializeField] private Image skill3GaugeImage;

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
        }

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
        if (hpSlider != null) hpSlider.value = playerStat.CurrentHp;
        if (staminaSlider != null) staminaSlider.value = playerStat.CurrentStamina;
    }

    // Exhausted 표시 갱신
    void UpdateExhausted()
    {
        if (exhaustedIndicator != null)
            exhaustedIndicator.SetActive(playerStat.IsExhausted);
    }

    // Base 상태 표시 갱신
    void UpdateBaseState()
    {
        if (baseStateIndicator != null)
            baseStateIndicator.SetActive(playerStat.IsInBase);
    }

    // 스킬 게이지 fillAmount 갱신 (0~1)
    void UpdateSkillGauge()
    {
        if (skill1GaugeImage != null)
            skill1GaugeImage.fillAmount = playerSkill.GetGauge(SkillSheetId.Skill1) / 100f;

        if (skill2GaugeImage != null)
            skill2GaugeImage.fillAmount = playerSkill.GetGauge(SkillSheetId.Skill2) / 100f;

        if (skill3GaugeImage != null)
            skill3GaugeImage.fillAmount = playerSkill.GetGauge(SkillSheetId.Skill3) / 100f;
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
}