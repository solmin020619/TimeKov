using System;
using UnityEngine;

[CreateAssetMenu(menuName = "TIMEKOV/퀘스트/목표/적 처치")]
public class EnemyKillObjective : ObjectiveSO
{
    [Tooltip("빈 값이면 모든 적 카운트 ('아무 적이나 N마리')")]
    public string enemyId;
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()
    {
        GameEvents.OnEnemyKilled += OnKill;
        if (!string.IsNullOrEmpty(enemyId))
            _count += GameEvents.RecentCount(GameEvents.KeyEnemy(enemyId));   // 갭에서 미리 잡은 분 인정
    }
    public override void Deactivate() => GameEvents.OnEnemyKilled -= OnKill;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));
    protected override bool IsAlreadySatisfied() => _count >= requiredCount;

    public override string GetDisplayLabel()
    {
        var displayLabel = string.IsNullOrEmpty(label) ? Loc.Get("처치") : Loc.Get(label);
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
