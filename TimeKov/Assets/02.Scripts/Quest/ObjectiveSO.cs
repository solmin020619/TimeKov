using System;
using UnityEngine;

public enum ActivationTiming
{
    OnUIActivated,   // 슬라이드 인 끝난 시점 (입력형, 고인물 방어)
    OnUIPresented,   // 슬라이드 인 시작 시점 (게임 이벤트형, 백그라운드 카운트)
}

public abstract class ObjectiveSO : ScriptableObject
{
    public event Action<ObjectiveSO> OnCompleted;
    public event Action<float> OnProgressChanged;

    [Tooltip("표시 텍스트. TMP rich text 태그 자유 사용 가능.\n" +
             "  색 강조: 통제탑으로 <color=#FFCC00>이동</color>하여 대화\n" +
             "  굵게: <b>중요</b> 단어\n" +
             "  크기: <size=120%>크게</size>")]
    [TextArea(2, 4)]
    public string label;
    public float startGracePeriod = 0.1f;

    /// <summary>
    /// 활성화 시점. 클래스가 강제. 인스펙터 노출 X.
    /// 입력형(PressKey/MoveDistance) = OnUIActivated (고인물 방어)
    /// 게임 이벤트형(EnemyKill/ReachTrigger) = OnUIPresented (백그라운드 카운트)
    /// </summary>
    public abstract ActivationTiming Timing { get; }

    [NonSerialized] protected float _activatedTime;
    [NonSerialized] bool _isCompleted;

    public bool IsCompleted => _isCompleted;

    public abstract void Activate();
    public abstract void Deactivate();
    public virtual float Progress => 0f;

    /// <summary>
    /// 활성화 시점에 이미 충족 상태인지.
    /// 카운트 누적형(PressKey/MoveDistance/EnemyKill)은 항상 _count=0부터라 false.
    /// 환경 상태형(ReachTrigger처럼 "이미 트리거 안에 있는지")만 의미 있음.
    /// </summary>
    protected virtual bool IsAlreadySatisfied() => false;

    public virtual string GetDisplayLabel() => label;

    // 순수 설명/코치마크(클릭하여 계속) objective인지. true면 완료 시 트래커 완료배너/토스트를 생략한다.
    public virtual bool IsExplanation => false;

    public void ActivateInternal()
    {
        _isCompleted = false;
        _activatedTime = Time.unscaledTime;
        Activate();
        if (IsAlreadySatisfied()) Complete();
    }

    protected bool IsInGracePeriod => Time.unscaledTime - _activatedTime < startGracePeriod;

    protected void Complete()
    {
        if (_isCompleted) return;
        _isCompleted = true;
        ReportProgress(1f);
        OnCompleted?.Invoke(this);
    }

    protected void ReportProgress(float p) => OnProgressChanged?.Invoke(Mathf.Clamp01(p));
}
