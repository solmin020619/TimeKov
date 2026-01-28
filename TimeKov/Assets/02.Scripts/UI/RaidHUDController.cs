using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RaidHUDController : MonoBehaviour
{
    [Header("Bind Targets (optional)")]
    [SerializeField] private PlayerTime playerTime;
    [SerializeField] private PlayerController playerController;

    [Header("UI")]
    [SerializeField] private Slider timeSlider;
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private TextMeshProUGUI timeText; // 남은 시간 표시(mm:ss)

    private bool subscribed;

    private void Start()
    {
        AutoFindIfNull();
        BindTimeEvents();
        ForceRefreshAll();
    }

    private void Update()
    {
        // 스테미나는 이벤트 없어도 가벼우니까 매프레임 갱신
        if (playerController != null && staminaSlider != null)
        {
            staminaSlider.maxValue = playerController.staminaMax;
            staminaSlider.value = playerController.currentStamina;
        }
    }

    private void OnDestroy()
    {
        if (playerTime != null && subscribed)
        {
            playerTime.onTimeChanged -= OnTimeChanged;
        }
    }

    private void AutoFindIfNull()
    {
        if (playerTime == null) playerTime = FindAnyObjectByType<PlayerTime>();
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
    }

    private void BindTimeEvents()
    {
        if (playerTime == null) return;

        if (!subscribed)
        {
            playerTime.onTimeChanged += OnTimeChanged;
            subscribed = true;
        }
    }

    private void ForceRefreshAll()
    {
        if (playerTime != null)
            OnTimeChanged(playerTime.currentTime, playerTime.maxTime);

        if (playerController != null && staminaSlider != null)
        {
            staminaSlider.maxValue = playerController.staminaMax;
            staminaSlider.value = playerController.currentStamina;
        }
    }

    private void OnTimeChanged(float current, float max)
    {
        if (timeSlider != null)
        {
            timeSlider.maxValue = max;
            timeSlider.value = current;
        }

        if (timeText != null)
        {
            int sec = Mathf.CeilToInt(current);
            int m = sec / 60;
            int s = sec % 60;
            timeText.text = $"{m:00}:{s:00}";
        }
    }
}
