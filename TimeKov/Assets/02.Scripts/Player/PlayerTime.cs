using System;
using UnityEngine;
using UnityEngine.SceneManagement; // ✅ 추가

public class PlayerTime : MonoBehaviour
{
    [Header("Base Time Settings")]
    public int baseMaxTime = 300;
    public int timeDecay = 1;

    [Header("Raid State")]
    public bool isInRaid = false;
    public float zoneDecayMultiplier = 1f;

    public float currentTime { get; private set; }
    public float maxTime { get; private set; }

    public Action<float, float> onTimeChanged;
    public Action onTimeDepleted;

    // ✅ 베이스 씬에서는 시간 감소/피해 감소 막기
    [Header("Base Scene Rule")]
    public bool freezeTimeInBase = true;
    public string[] baseSceneNames = new string[] { "Base", "BaseScene", "Lobby" };

    private bool IsBaseScene()
    {
        if (!freezeTimeInBase) return false;
        string s = SceneManager.GetActiveScene().name;
        if (baseSceneNames == null || baseSceneNames.Length == 0) return false;

        for (int i = 0; i < baseSceneNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(baseSceneNames[i]) && baseSceneNames[i] == s)
                return true;
        }
        return false;
    }

    private void Start()
    {
        InitTime();
    }

    public void InitTime()
    {
        maxTime = baseMaxTime;
        currentTime = maxTime;

        onTimeChanged?.Invoke(currentTime, maxTime);
    }

    private void Update()
    {
        // ✅✅ 베이스 씬이면 시간 감소 자체를 하지 않음
        if (IsBaseScene()) return;

        // 레이드 중일 때만 Time이 초당 감소
        if (!isInRaid) return;

        float dt = Time.unscaledDeltaTime; // ✅ 기본은 UI로 timeScale=0이어도 계속 흐름

        // ✅ ESC Pause 상태일 때만 시간 감소 멈춤
        if (UIStateManager.Instance != null &&
            UIStateManager.Instance.GetCurrentState() == UIStateManager.UIState.Pause)
        {
            dt = 0f;
        }

        float decay = timeDecay * zoneDecayMultiplier * dt;
        ApplyTimeChange(-decay);
    }

    public void TakeDamage(float amount)
    {
        // ✅✅ 베이스 씬이면 데미지로 시간 감소도 막기
        if (IsBaseScene()) return;

        ApplyTimeChange(-amount);

        if (FindAnyObjectByType<CrosshairController>() != null)
            FindAnyObjectByType<CrosshairController>().OnHurt();
    }

    public void Recover(float amount)
    {
        ApplyTimeChange(amount);
    }

    private void ApplyTimeChange(float delta)
    {
        // ✅ (안전) 베이스 씬에서는 음수 변화(감소)만 막고 회복은 허용
        if (IsBaseScene() && delta < 0f) return;

        float old = currentTime;
        currentTime = Mathf.Clamp(currentTime + delta, 0, maxTime);

        if (Mathf.Abs(old - currentTime) > Mathf.Epsilon)
        {
            onTimeChanged?.Invoke(currentTime, maxTime);
        }

        if (currentTime <= 0)
        {
            HandleTimeDepleted();
        }
    }

    private void HandleTimeDepleted()
    {
        Debug.Log("레이드 실패");
        onTimeDepleted?.Invoke();
    }
}