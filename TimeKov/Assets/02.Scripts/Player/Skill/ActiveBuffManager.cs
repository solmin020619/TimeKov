// =====================================================================
// ActiveBuffManager.cs
// 플레이어에게 적용되는 일시 버프 관리 (Player 오브젝트에 컴포넌트로 추가)
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
            }
        }
    }

    // 일시 버프 적용
    // 같은 itemId + 같은 target 이면 기존 것 원복 후 갱신(교체), 다른 itemId + 같은 target 이면 중첩
    public void ApplyTimedBuff(EffectTarget target, float appliedDelta,
                                float duration, string sourceItemId)
    {
        // 같은 itemId + 같은 target 버프가 있으면 원복 후 제거
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

        // 스탯에 델타 적용. 클램프(Mathf.Max 등)로 실제 변화량이 요청값과 다를 수 있어, 실제 적용된 값을 저장해야 원복이 정확하다.
        float actualDelta = ApplyDelta(target, appliedDelta);

        // 버프 등록
        _activeBuffs.Add(new ActiveBuff
        {
            target = target,
            appliedDelta = actualDelta,
            remainingTime = duration,
            sourceItemId = sourceItemId
        });
    }

    // 지속 회복: 초당 healPerSecond 를 duration 동안 누적 회복
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

            // 코루틴 도중 Player 가 null 이면 이미 파괴된 것으로 보고 중단
            if (_player == null)
            {
                Debug.LogWarning("[BuffManager] SustainHeal: Player null, 코루틴 중단");
                yield break;
            }

            if (isMaxPercent)
                _player.Stat.HealPercent(healPerSecond / 100f);
            else
                _player.Stat.Heal(healPerSecond);
        }
    }

    // 버프에 따라 스탯 델타 적용 (양수=강화, 음수=원복).
    // 반환값 = 실제 적용된 델타. 클램프(Mathf.Max 등)로 요청값과 달라질 수 있어, 원복이 정확히 되돌리게 호출부가 이 값을 저장한다.
    // (옛 버그: -40 적용 시 0으로 클램프돼 실제 -30만 깎였는데 원복은 +40을 줘서 DashCost가 먹을 때마다 +10씩 영구 상승.)
    private float ApplyDelta(EffectTarget target, float delta)
    {
        if (_player == null) return 0f;

        switch (target)
        {
            case EffectTarget.ATK:
                _player.Stat.ATK += delta;
                return delta;

            case EffectTarget.DEF:
                _player.Stat.DEF += delta;
                return delta;

            case EffectTarget.MoveSpeed:
                _player.Movement.MoveSpeed += delta;
                _player.Movement.SprintSpeed += delta;
                return delta;

            case EffectTarget.TimeDecay:
            {
                // delta 음수면 드레인 감소 (필드 시간 손실 완화)
                float old = _player.Stat.HpDrainRate;
                _player.Stat.HpDrainRate = Mathf.Max(0f, old + delta);
                return _player.Stat.HpDrainRate - old;   // 클램프 후 실제 변화량
            }

            case EffectTarget.DashStamina:
            {
                float old = _player.Dash.DashCost;
                _player.Dash.DashCost = Mathf.Max(0f, old + delta);
                return _player.Dash.DashCost - old;   // 클램프 후 실제 변화량(원복 정확)
            }

            // 아직 미구현 항목
            case EffectTarget.Stamina:
                Debug.LogWarning("[BuffManager] Stamina 버프: 추후 구현 예정");
                return 0f;

            case EffectTarget.SkillGauge:
                Debug.LogWarning("[BuffManager] SkillGauge: 추후 구현 예정");
                return 0f;

            case EffectTarget.AllStats:
                Debug.LogWarning("[BuffManager] AllStats: 추후 구현 예정");
                return 0f;

            default:
                Debug.LogWarning($"[BuffManager] 처리되지 않은 EffectTarget={target}");
                return 0f;
        }
    }

    // 버프 만료 시 원복 (저장된 '실제 적용 델타'를 그대로 빼서 정확히 되돌림)
    private void RevertBuff(ActiveBuff buff)
    {
        ApplyDelta(buff.target, -buff.appliedDelta);
    }
}