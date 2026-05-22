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
    [Tooltip("체력 - '현재 / 최대' 형식")]
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

        if (atkText != null) atkText.text = stat.ATK.ToString(fmt);
        if (defText != null) defText.text = stat.DEF.ToString(fmt);
        if (hpText != null) hpText.text = $"{stat.CurrentHp:F0} / {stat.MaxHp:F0}";
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
}
