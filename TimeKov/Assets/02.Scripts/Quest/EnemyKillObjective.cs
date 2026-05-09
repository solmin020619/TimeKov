using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Quest/Objective/EnemyKill")]
public class EnemyKillObjective : ObjectiveSO
{
    [Tooltip("빈 값이면 모든 적 카운트 ('아무 적이나 N마리')")]
    public string enemyId;
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate() { GameEvents.OnEnemyKilled += OnKill; }
    public override void Deactivate() => GameEvents.OnEnemyKilled -= OnKill;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));

    public override string GetDisplayLabel()
    {
        var displayLabel = string.IsNullOrEmpty(label) ? "처치" : label;
        return requiredCount > 1 ? $"{displayLabel} ({_count}/{requiredCount})" : displayLabel;
    }

    void OnKill(string id)
    {
        if (IsInGracePeriod) return;
        if (!string.IsNullOrEmpty(enemyId) && id != enemyId) return;
        _count++;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        requiredCount = Mathf.Max(1, requiredCount);
        if (string.IsNullOrEmpty(label))
            Debug.LogError($"[EnemyKill] '{name}' label 필수", this);
        if (requiredCount > 1)
            Debug.LogWarning($"[EnemyKill] '{name}' requiredCount={requiredCount}. " +
                             "한정 자원이면 영구 미완료 위험. 디자이너 가이드 참조.", this);
    }
#endif
}
