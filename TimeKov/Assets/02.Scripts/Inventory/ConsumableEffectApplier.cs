// =====================================================================
// ConsumableEffectApplier.cs
// ConsumableEffectTable �����͸� �о� �÷��̾ ȿ�� ����
// InventoryManager.UseItem() ������ ȣ��
// ���� ������ �� Ŭ�������� ���� �ʴ´�
//   ���� �� InventoryManager.UseItem() ���� TryConsumeItem()
//   ���� �� Apply ���� �� UseItem() ���� AddItem() ���� ��� ����
// =====================================================================

using UnityEngine;

public static class ConsumableEffectApplier
{
    // ��ȯ��: true=���� / false=����
    // �����ص� �� Ŭ���� �ȿ��� ������ �ǵ帮�� �ʴ´�
    public static bool Apply(string itemId, Player player)
    {
        bool result = ApplyInternal(itemId, player);
        // 퀘스트 시스템 통지 (효과 적용 성공 시점 = 진짜 사용)
        if (result && int.TryParse(itemId, out int parsedId))
            GameEvents.RaiseItemUsed(parsedId);
        return result;
    }

    private static bool ApplyInternal(string itemId, Player player)
    {
        if (player == null)
        {
            Debug.LogWarning("[ConsumableEffect] Player null");
            return false;
        }

        if (!GameDataHolder.I.ConsumableEffect.TryGet(itemId, out var effect))
        {
            Debug.LogWarning($"[ConsumableEffect] ������ ����: itemId={itemId}");
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

            // �̹� ���� ����
            case ConsumableType.Stamina:
                Debug.LogWarning("[ConsumableEffect] Stamina Ÿ�� : �ļ� ���� ���");
                return false;

            default:
                Debug.LogWarning($"[ConsumableEffect] ó������ ���� consumableType={effect.consumableType}");
                return false;
        }
    }

    // ���� ȿ���� ó�� ������������������������������������������������������������������������������������

    // ��� HP(Time) ȸ��
    private static bool ApplyHeal(ConsumableEffectSheetData effect, float delta, Player player)
    {
        if (effect.effectValueType == EffectValueType.MaxPercent)
            player.Stat.HealPercent(effect.effectValue / 100f);
        else
            player.Stat.Heal(delta);

        return true;
    }

    // ���� ȸ�� : ActiveBuffManager �ڷ�ƾ���� ����
    private static bool ApplySustainHeal(ConsumableEffectSheetData effect, Player player)
    {
        var buffManager = player.GetComponent<ActiveBuffManager>();
        if (buffManager == null)
        {
            Debug.LogWarning("[ConsumableEffect] ActiveBuffManager ���� : SustainHeal ����");
            return false;
        }

        bool isMaxPercent = effect.effectValueType == EffectValueType.MaxPercent;
        buffManager.ApplySustainHeal(effect.effectValue, effect.duration, isMaxPercent);
        return true;
    }

    // ���Ѻ� ���� ����
    private static bool ApplyBuff(string itemId, ConsumableEffectSheetData effect,
                                   float delta, Player player)
    {
        // �̹� ���� ���� �׸�
        if (effect.effectTarget == EffectTarget.AllStats ||
            effect.effectTarget == EffectTarget.Stamina ||
            effect.effectTarget == EffectTarget.SkillGauge)
        {
            Debug.LogWarning($"[ConsumableEffect] {effect.effectTarget} : �ļ� ���� ���");
            return false;
        }

        var buffManager = player.GetComponent<ActiveBuffManager>();
        if (buffManager == null)
        {
            Debug.LogWarning("[ConsumableEffect] ActiveBuffManager ���� : Buff ����");
            return false;
        }

        buffManager.ApplyTimedBuff(effect.effectTarget, delta, effect.duration, itemId);
        return true;
    }

    // ���� ��ġ ��� ������������������������������������������������������������������������������������������

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

    // Percent ���� ���� ����
    private static float GetBaseStat(EffectTarget target, Player player)
    {
        return target switch
        {
            EffectTarget.ATK => player.Stat.ATK,
            EffectTarget.MoveSpeed => player.Movement.MoveSpeed,
            _ => 0f
        };
    }

    // MaxPercent ���� �ִ� ����
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