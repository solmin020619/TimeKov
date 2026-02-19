using TMPro;
using UnityEngine;

public class InventoryTimeTextUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerTime playerTime;
    [SerializeField] private TextMeshProUGUI timeText;

    private void OnEnable()
    {
        // 인스펙터에 안 넣었으면 자동 탐색 (가능하면 인스펙터 연결 추천)
        if (playerTime == null)
            playerTime = FindAnyObjectByType<PlayerTime>();

        if (playerTime != null)
            playerTime.onTimeChanged += OnTimeChanged;

        // 인벤 켜질 때 즉시 표시(시간이 변하지 않아도 텍스트 보이게)
        RefreshNow();
    }

    private void OnDisable()
    {
        if (playerTime != null)
            playerTime.onTimeChanged -= OnTimeChanged;
    }

    private void OnTimeChanged(float current, float max)
    {
        if (timeText == null) return;
        timeText.text = $"보유 시간: {Mathf.CeilToInt(current)}s";
    }

    private void RefreshNow()
    {
        if (timeText == null || playerTime == null) return;
        timeText.text = $"보유 시간: {Mathf.CeilToInt(playerTime.currentTime)}s";
    }
}
