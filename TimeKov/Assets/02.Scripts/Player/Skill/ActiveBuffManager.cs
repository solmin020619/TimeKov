// =====================================================================
// ActiveBuffManager.cs
// 플레이어에 적용 중인 시한부 버프 목록 관리
// Player 오브젝트에 컴포넌트로 추가
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActiveBuffManager : MonoBehaviour
{
    private Player _player;

    // 활성 버프 목록
    private readonly List<ActiveBuff> _activeBuffs = new List<ActiveBuff>();

    // 외부 읽기 전용 (UI 버프 아이콘 표시용)
    public IReadOnlyList<ActiveBuff> ActiveBuffs => _activeBuffs;

    private void Awake()
    {
        _player = GetComponent<Player>();
    }

    private void Update()
    {
        TickBuffs();
    }

    // 매 프레임 타이머 감소 및 만료 처리
    private void TickBuffs()
    {
        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            var buff = _activeBuffs[i];
            buff.remainingTime -= Time.deltaTime;

            if (buff.IsExpired)
            {
                RevertBuff(buff);
                _activeBuffs.RemoveAt(i);
                Debug.Log($"[BuffManager] 버프 만료: {buff.target} delta={buff.appliedDelta}");
            }
        }
    }

    // 시한부 버프 적용
    // 동일 itemId + target 이면 기존 원복 후 새로 적용 (교체)
    // 다른 itemId + 동일 target 이면 중첩
    public void ApplyTimedBuff(EffectTarget target, float appliedDelta,
                                float duration, string sourceItemId)
    {
        // 동일 itemId + 동일 target 버프가 있으면 원복 후 제거
        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            var existing = _activeBuffs[i];
            if (existing.target == target && existing.sourceItemId == sourceItemId)
            {
                RevertBuff(existing);
                _activeBuffs.RemoveAt(i);
                break;
            }
        }

        // 스탯에 델타 적용
        ApplyDelta(target, appliedDelta);

        // 버프 등록
        _activeBuffs.Add(new ActiveBuff
        {
            target = target,
            appliedDelta = appliedDelta,
            remainingTime = duration,
            sourceItemId = sourceItemId
        });

        Debug.Log($"[BuffManager] 버프 적용: {target} delta={appliedDelta} / {duration}초");
    }

    // 지속 회복 : 초당 healPerSecond 씩 duration 초 동안 회복
    public void ApplySustainHeal(float healPerSecond, float duration, bool isMaxPercent)
    {
        StartCoroutine(SustainHealRoutine(healPerSecond, duration, isMaxPercent));
    }

    private IEnumerator SustainHealRoutine(float healPerSecond, float duration, bool isMaxPercent)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return new WaitForSeconds(1f);
            elapsed += 1f;

            // 코루틴 진행 중 Player 가 null 이면 이미 진행된 효과는 복구 없이 종료
            if (_player == null)
            {
                Debug.LogWarning("[BuffManager] SustainHeal: Player null : 코루틴 종료");
                yield break;
            }

            if (isMaxPercent)
                _player.Stat.HealPercent(healPerSecond / 100f);
            else
                _player.Stat.Heal(healPerSecond);
        }
    }

    // 스탯에 델타 적용 (양수=증가, 음수=감소)
    private void ApplyDelta(EffectTarget target, float delta)
    {
        if (_player == null) return;

        switch (target)
        {
            case EffectTarget.ATK:
                _player.Stat.ATK += delta;
                break;

            case EffectTarget.MoveSpeed:
                _player.Movement.MoveSpeed += delta;
                _player.Movement.SprintSpeed += delta;
                break;

            case EffectTarget.TimeDecay:
                // delta 가 음수면 감소 (필드 시간 손실 억제)
                _player.Stat.HpDrainRate =
                    Mathf.Max(0f, _player.Stat.HpDrainRate + delta);
                break;

            case EffectTarget.DashStamina:
                _player.Dash.DashCost =
                    Mathf.Max(0f, _player.Dash.DashCost + delta);
                break;

            // 이번 범위 제외 항목
            case EffectTarget.Stamina:
                Debug.LogWarning("[BuffManager] Stamina 버프 : 후속 구현 대상");
                break;

            case EffectTarget.SkillGauge:
                Debug.LogWarning("[BuffManager] SkillGauge : 후속 구현 대상");
                break;

            case EffectTarget.AllStats:
                Debug.LogWarning("[BuffManager] AllStats : 후속 구현 대상");
                break;

            default:
                Debug.LogWarning($"[BuffManager] 처리되지 않은 EffectTarget={target}");
                break;
        }
    }

    // 버프 만료 시 원복
    private void RevertBuff(ActiveBuff buff)
    {
        ApplyDelta(buff.target, -buff.appliedDelta);
    }
}