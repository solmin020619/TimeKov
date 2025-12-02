using System;
using UnityEngine;

public class PlayerTime : MonoBehaviour
{
    [Header("Base Time Settings")]
    public int baseMaxTime = 300;               // 플레이어 시작 체력(Time)
    public int timeDecay = 1;                   // 초당 Time 감소(레이드)

    [Header("Raid State")]
    public bool isInRaid = false;               // 레이드 들어가면 true
    public float zoneDecayMultiplier = 1f;      // 레이드 지역 보정 값

    public float currentTime {get; private set;}    // 현재 체력(Time)
    public float maxTime {get; private set;}        // 최대 체력(Time)

    public Action<float, float> onTimeChanged;      // UI 업데이트 용
    public Action onTimeDepleted;                   // Time이 0이 되었을떄

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
        if (!isInRaid) return;

        float decay = timeDecay * zoneDecayMultiplier * Time.deltaTime;
        ApplyTimeChange(-decay);
    }

    public void TakeDamage(float amount)
    {
        ApplyTimeChange(-amount);
    }

    public void Recover(float amount)
    {
        ApplyTimeChange(amount);
    }

    private void ApplyTimeChange(float delta)
    {
        float old = currentTime;                                            // 변경하기전 이전 Time값 저장
        currentTime = Mathf.Clamp(currentTime + delta,0,maxTime);           // delta -> Time의 증가/감소량 + 회복 - 데미지 Time이 0 아래로 내려가지않도록 clamp(값,최소,최대)

        if(Mathf.Abs(old - currentTime) > Mathf.Epsilon)                    // Epsilon -> float 비교에서 거의 0에 가까운 값
        {
            onTimeChanged?.Invoke(currentTime, maxTime);                    // Time값이 실제로 바뀌었는지 확인 후 UI 업데이트
        }

        if(currentTime <= 0)                                                // 사망 체크(Time이 0이하인지)
        {
            HandleTimeDepleted();                                           
        }
    }

    private void HandleTimeDepleted()
    {
        Debug.Log("레이드 실패");
        onTimeDepleted?.Invoke();                                           // 다른 스크립트에게 죽었다고 알리는 용도
    }
}
