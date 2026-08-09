using System;
using UnityEngine;

[CreateAssetMenu(menuName = "TIMEKOV/퀘스트/목표/키 누르기")]
public class PressKeyObjective : ObjectiveSO
{
    public KeyCode key;
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIActivated;

    public override void Activate() { InputBus.OnKeyDown += OnKey; }
    public override void Deactivate() => InputBus.OnKeyDown -= OnKey;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{Loc.Get(label)} ({_count}/{requiredCount})" : label;

    void OnKey(KeyCode k)
    {
        if (IsInGracePeriod || k != key) return;
        _count++;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        requiredCount = Mathf.Max(1, requiredCount);
        if (key == KeyCode.None)
            Debug.LogError($"[PressKey] '{name}' key=None. 어떤 키도 안 받음", this);
        // Tab(인벤 토글)은 InputBus로 키 이벤트를 받아 튜토 감지엔 문제없음 -> 경고 제외(오탐).
        // Escape(메뉴/설정 토글)만 PressKey 키로 쓰면 충돌 소지 있어 경고 유지.
        if (key == KeyCode.Escape)
            Debug.LogWarning($"[PressKey] '{name}' Escape는 메뉴/설정 토글과 충돌 가능", this);
    }
#endif
}
