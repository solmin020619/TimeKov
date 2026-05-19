// =====================================================================
// PlayerHudUI.cs
// 플레이어 HP / 스태미나 슬라이더 연동
// =====================================================================

using UnityEngine;
using UnityEngine.UI;

public class PlayerHudUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("PlayerStatComponent가 붙어있는 플레이어 오브젝트. 비워두면 씬에서 자동 탐색.")]
    [SerializeField] private PlayerStatComponent playerStat;

    [Header("HP")]
    [SerializeField] private Slider hpSlider;

    [Header("Stamina")]
    [SerializeField] private Slider staminaSlider;

    private void Start()
    {
        if (playerStat == null)
        {
            var player = FindAnyObjectByType<Player>();
            if (player != null)
                playerStat = player.GetComponent<PlayerStatComponent>();
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
    }

    private void Update()
    {
        if (playerStat == null) return;

        if (hpSlider != null)
            hpSlider.value = playerStat.CurrentHp;

        if (staminaSlider != null)
            staminaSlider.value = playerStat.CurrentStamina;
    }
}
