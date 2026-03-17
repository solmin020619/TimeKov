using TMPro;
using UnityEngine;

public class InventoryTimeTextUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerTime playerTime;
    [SerializeField] private TextMeshProUGUI timeText;

    private bool _subscribed = false;
    private int _lastShownTime = int.MinValue;

    private void Awake()
    {
        ResolvePlayerTime();
    }

    private void OnEnable()
    {
        ResolvePlayerTime();
        Subscribe();
        RefreshNow(force: true);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolvePlayerTime()
    {
        if (playerTime == null)
            playerTime = FindAnyObjectByType<PlayerTime>();
    }

    private void Subscribe()
    {
        if (_subscribed || playerTime == null)
            return;

        playerTime.onTimeChanged += OnTimeChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || playerTime == null)
            return;

        playerTime.onTimeChanged -= OnTimeChanged;
        _subscribed = false;
    }

    private void OnTimeChanged(float current, float max)
    {
        ApplyText(Mathf.CeilToInt(current));
    }

    private void RefreshNow(bool force = false)
    {
        if (timeText == null || playerTime == null)
            return;

        int current = Mathf.CeilToInt(playerTime.currentTime);
        if (!force && current == _lastShownTime)
            return;

        ApplyText(current);
    }

    private void ApplyText(int currentSeconds)
    {
        if (timeText == null)
            return;

        if (_lastShownTime == currentSeconds && !string.IsNullOrEmpty(timeText.text))
            return;

        timeText.text = $"보유 시간: {currentSeconds}s";
        _lastShownTime = currentSeconds;
    }
}