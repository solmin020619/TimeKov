// =====================================================================
// ConsumableEffectApplier.cs
// ConsumableEffectTable 데이터를 읽어 플레이어에 효과 적용
// InventoryManager.UseItem() 에서만 호출
// 수량 차감은 이 클래스에서 하지 않는다
//   차감 → InventoryManager.UseItem() 에서 TryConsumeItem()
//   복구 → Apply 실패 시 UseItem() 에서 AddItem() 으로 즉시 복구
// =====================================================================

using UnityEngine;

public static class ConsumableEffectApplier
{
    // 반환값: true=성공 / false=실패
    // 실패해도 이 클래스 안에서 수량을 건드리지 않는다
    public static bool Apply(string itemId, Player player)
    {
        if (player == null)
        {
            Debug.LogWarning("[ConsumableEffect] Player null");
            return false;
        }

        if (!GameDataHolder.I.ConsumableEffect.TryGet(itemId, out var effect))
        {
            Debug.LogWarning($"[ConsumableEffect] 데이터 없음: itemId={itemId}");
            return false;
        }

        float delta = CalculateDelta(effect, player);

        switch (effect.consumableType)
        {
            case ConsumableType.Heal:
                return ApplyHeal(effect, delta, player);

            case ConsumableType.SustainHeal:
                return ApplySustainHeal(effect, player);

            case ConsumableType.Buff:
                return ApplyBuff(itemId, effect, delta, player);

            // 이번 범위 제외
            case ConsumableType.Stamina:
                Debug.LogWarning("[ConsumableEffect] Stamina 타입 : 후속 구현 대상");
                return false;

            default:
                Debug.LogWarning($"[ConsumableEffect] 처리되지 않은 consumableType={effect.consumableType}");
                return false;
        }
    }

    // ── 효과별 처리 ──────────────────────────────────────────

    // 즉시 HP(Time) 회복
    private static bool ApplyHeal(ConsumableEffectSheetData effect, float delta, Player player)
    {
        if (effect.effectValueType == EffectValueType.MaxPercent)
            player.Stat.HealPercent(effect.effectValue / 100f);
        else
            player.Stat.Heal(delta);

        return true;
    }

    // 지속 회복 : ActiveBuffManager 코루틴으로 위임
    private static bool ApplySustainHeal(ConsumableEffectSheetData effect, Player player)
    {
        var buffManager = player.GetComponent<ActiveBuffManager>();
        if (buffManager == null)
        {
            Debug.LogWarning("[ConsumableEffect] ActiveBuffManager 없음 : SustainHeal 실패");
            return false;
        }

        bool isMaxPercent = effect.effectValueType == EffectValueType.MaxPercent;
        buffManager.ApplySustainHeal(effect.effectValue, effect.duration, isMaxPercent);
        return true;
    }

    // 시한부 스탯 버프
    private static bool ApplyBuff(string itemId, ConsumableEffectSheetData effect,
                                   float delta, Player player)
    {
        // 이번 범위 제외 항목
        if (effect.effectTarget == EffectTarget.AllStats ||
            effect.effectTarget == EffectTarget.Stamina ||
            effect.effectTarget == EffectTarget.SkillGauge)
        {
            Debug.LogWarning($"[ConsumableEffect] {effect.effectTarget} : 후속 구현 대상");
            return false;
        }

        var buffManager = player.GetComponent<ActiveBuffManager>();
        if (buffManager == null)
        {
            Debug.LogWarning("[ConsumableEffect] ActiveBuffManager 없음 : Buff 실패");
            return false;
        }

        buffManager.ApplyTimedBuff(effect.effectTarget, delta, effect.duration, itemId);
        return true;
    }

    // ── 수치 계산 ─────────────────────────────────────────────

    private static float CalculateDelta(ConsumableEffectSheetData effect, Player player)
    {
        return effect.effectValueType switch
        {
            EffectValueType.Flat => effect.effectValue,
            EffectValueType.Percent => GetBaseStat(effect.effectTarget, player) * (effect.effectValue / 100f),
            EffectValueType.MaxPercent => GetMaxStat(effect.effectTarget, player) * (effect.effectValue / 100f),
            _ => effect.effectValue
        };
    }

    // Percent 기준 현재 스탯
    private static float GetBaseStat(EffectTarget target, Player player)
    {
        return target switch
        {
            EffectTarget.ATK => player.Stat.ATK,
            EffectTarget.MoveSpeed => player.Movement.MoveSpeed,
            _ => 0f
        };
    }

    // MaxPercent 기준 최대 스탯
    private static float GetMaxStat(EffectTarget target, Player player)
    {
        return target switch
        {
            EffectTarget.Time => player.Stat.MaxHp,
            EffectTarget.Stamina => player.Stat.MaxStamina,
            _ => 0f
        };
    }
}