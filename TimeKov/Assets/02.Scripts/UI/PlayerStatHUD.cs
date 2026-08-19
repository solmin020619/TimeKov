using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 스탯창 HUD. C키 등으로 열리는 UI 패널에 부착.
/// Player.Stat 의 값을 매 프레임 읽어 UI에 표시 (읽기 전용 — Player 폴더 안 건드림).
///
/// 사용법:
///   1. UI 패널 GameObject에 이 컴포넌트 부착
///   2. 인스펙터에서 각 텍스트/슬라이더 슬롯에 자식 GameObject 드래그
///   3. 사용 안 하는 슬롯은 비워두면 그 부분만 갱신 안 함
/// </summary>
public class PlayerStatHUD : MonoBehaviour
{
    [Header("Player 참조 (비우면 Tag로 자동 검색)")]
    [SerializeField] private Player player;

    [Header("Text 슬롯 (사용 안 하는 건 비워둬도 OK)")]
    [Tooltip("공격력")]
    [SerializeField] private TMP_Text atkText;
    [Tooltip("방어력")]
    [SerializeField] private TMP_Text defText;
    [Tooltip("체력 - '현재(-초당감소) / 최대' 형식")]
    [SerializeField] private TMP_Text hpText;
    [Tooltip("스태미나 - '현재 / 최대' 형식")]
    [SerializeField] private TMP_Text staminaText;

    [Header("Slider 슬롯 (HP/Stamina 게이지 바)")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Slider staminaSlider;

    [Header("표시 포맷")]
    [Tooltip("ATK/DEF 소수점 자릿수. 0이면 정수만.")]
    [SerializeField] private int statDecimals = 0;

    private PlayerStatComponent stat;

    private void Awake()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.GetComponent<Player>();
        }
        if (player != null) stat = player.GetComponent<PlayerStatComponent>();
    }

    private void OnEnable()
    {
        // 초당 감소량을 작게·흐리게 붙이려면 리치텍스트가 켜져 있어야 한다.
        // 꺼져 있으면 태그가 글자 그대로 보인다.
        if (hpText != null) hpText.richText = true;

        // 패널 켜질 때 즉시 한 번 갱신 (Update 첫 frame 전 깜빡임 방지)
        RefreshAll();
    }

    private void Update()
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (stat == null) return;

        string fmt = "F" + Mathf.Max(0, statDecimals);

        if (atkText != null) atkText.text = WithCap(stat.ATK, EffectTarget.ATK, fmt);
        if (defText != null) defText.text = WithCap(stat.DEF, EffectTarget.DEF, fmt);
        if (hpText != null) hpText.text = $"{stat.CurrentHp:F0}{DrainSuffix()} / {stat.MaxHp:F0}";
        if (staminaText != null) staminaText.text = $"{stat.CurrentStamina:F0} / {stat.MaxStamina:F0}";

        // Slider 인스펙터 설정과 무관하게 작동하도록 min/max도 매 프레임 동기화
        if (hpSlider != null && stat.MaxHp > 0f)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = stat.MaxHp;
            hpSlider.value = stat.CurrentHp;
        }
        if (staminaSlider != null && stat.MaxStamina > 0f)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = stat.MaxStamina;
            staminaSlider.value = stat.CurrentStamina;
        }
    }

    // ── 초당 시간 감소 표시 ────────────────────────────────────────────────
    // 현재 체력 바로 뒤에 괄호로 붙인다.  예)  80(-1/s) / 300
    //
    //   ★'지속'으로 닳는 것만 나온다. 피격처럼 한 번에 크게 깎이는 건 세지 않는다
    //     (PlayerStatComponent.HpDrainPerSecond 참고).
    //   안 닳는 동안(결계 안·안전지대)에는 괄호째 사라진다 — '(-0/s)' 가 붙어 있어 봐야
    //     자리만 차지하고 알려주는 게 없다.
    //   소수 한 자리까지만: 1 / 3 처럼 딱 떨어지면 그냥 '-1/s' 로 나온다.
    //
    //   작게·흐리게 보이는 건 리치텍스트로 처리한다. 별도 글자 오브젝트를 두면 체력 숫자가
    //   자릿수에 따라 움직일 때 따라붙지 못해 사이가 벌어진다.
    //   색은 PlayerStatPanelStyle 의 DrainCol 과 같은 값이다 — 바꿀 땐 둘 다 바꿀 것.
    private const string DrainOpen = "<size=65%><color=#6E90AE>";
    private const string DrainClose = "</color></size>";

    private string DrainSuffix()
    {
        float perSec = stat.HpDrainPerSecond;
        return perSec > 0.05f ? $"{DrainOpen}(-{perSec:0.#}/s){DrainClose}" : "";
    }

    // 스탯 값 뒤에 '지금 구간의 끝'을 붙인다. 예) 공격력 12.5 / 16
    //
    // 앰플에는 티어별 천장이 있다(초급 16 / 중급 28 / 고급 무한). 숫자만 보여주면
    // 왜 초급 앰플이 어느 순간부터 안 먹는지 알 수가 없어서, 지금 부딪힐 천장을 같이 보여준다.
    // 모든 천장을 넘어선 뒤에는(고급 구간) 한계가 없으므로 숫자만 남긴다.
    private string WithCap(float value, EffectTarget target, string fmt)
    {
        float cap = ConsumableEffectApplier.GetNextCap(target, value);
        return cap > 0f ? $"{value.ToString(fmt)} / {cap:0.#}" : value.ToString(fmt);
    }
}
