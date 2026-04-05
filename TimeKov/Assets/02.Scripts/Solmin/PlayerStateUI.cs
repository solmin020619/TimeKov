using UnityEngine;
using UnityEngine.UI;
using TMPro; // ★ 텍스트 처리를 위해 추가

public class PlayerStatusUI : MonoBehaviour
{
    [Header("References")]
    public PlayerTime playerTime;  
    public PlayerController playerController;

    [Header("UI")]
    public Slider timeSlider;
    public Slider staminaSlider;

    public Image timeSliderFill;    
    public TextMeshProUGUI timeText;  

    [Header("Color Settings")]
    public Gradient timeColorGradient;

    private void Start()
    {
        if (playerTime != null)
        {
            timeSlider.maxValue = playerTime.maxTime;
            timeSlider.value = playerTime.currentTime;

            ApplyTimeUI(playerTime.currentTime, playerTime.maxTime);

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

        ApplyTimeUI(current, max);
    }

    private void ApplyTimeUI(float current, float max)
    {
        if (max <= 0) return;

        float ratio = Mathf.Clamp01(current / max);

        if (timeSliderFill != null)
        {
            timeSliderFill.color = timeColorGradient.Evaluate(ratio);
        }

        if (timeText != null)
        {
            int seconds = Mathf.CeilToInt(current);
            timeText.text = seconds.ToString();
        }
    }
}