using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusUI : MonoBehaviour
{
    [Header("References")]
    public PlayerTime playerTime;             // Player의 PlayerTime 컴포넌트
    public PlayerController playerController; // Player의 PlayerController 컴포넌트

    [Header("UI")]
    public Slider timeSlider;
    public Slider staminaSlider;

    private void Start()
    {
        if (playerTime != null)
        {
            // 처음 값 초기화
            timeSlider.maxValue = playerTime.maxTime;
            timeSlider.value = playerTime.currentTime;

            // Time 바뀔 때마다 업데이트
            playerTime.onTimeChanged += UpdateTimeBar;
        }

        if (playerController != null)
        {
            staminaSlider.maxValue = playerController.staminaMax;
            staminaSlider.value = playerController.currentStamina;
        }
    }

    private void Update()
    {
        // 스태미나는 이벤트 안 쓰고 매 프레임 가져와도 가벼움
        if (playerController != null && staminaSlider != null)
        {
            staminaSlider.value = playerController.currentStamina;
        }
    }

    void UpdateTimeBar(float current, float max)
    {
        if (timeSlider == null) return;

        timeSlider.maxValue = max;
        timeSlider.value = current;
    }
}
