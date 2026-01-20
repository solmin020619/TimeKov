using UnityEngine;

public class RaidTimeUIBinder : MonoBehaviour
{
    private PlayerTime playerTime;
    private TimeWarningUI warningUI;
    private DeathSequenceUI deathUI;

    private bool subscribed = false;

    private void Update()
    {
        if (!subscribed)
            TryBind();
    }

    private void TryBind()
    {
        if (playerTime == null) playerTime = FindAnyObjectByType<PlayerTime>();
        if (warningUI == null) warningUI = FindAnyObjectByType<TimeWarningUI>();
        if (deathUI == null) deathUI = FindAnyObjectByType<DeathSequenceUI>();

        if (playerTime != null && warningUI != null && deathUI != null)
        {
            playerTime.onTimeChanged += OnTimeChanged;
            playerTime.onTimeDepleted += OnTimeDepleted;
            subscribed = true;

            Debug.Log("[RaidTimeUIBinder] Auto bind success");
        }
    }

    private void OnDestroy()
    {
        if (playerTime != null && subscribed)
        {
            playerTime.onTimeChanged -= OnTimeChanged;
            playerTime.onTimeDepleted -= OnTimeDepleted;
        }
    }

    private void OnTimeChanged(float current, float max)
    {
        warningUI.SetTimeRemaining(current);
    }

    private void OnTimeDepleted()
    {
        deathUI.PlayDeathSequence();
    }
}
