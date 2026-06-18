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

    // 소비 전 검사: 지금 사용해도 의미가 있는지. false면 소비/효과 적용을 막아야 함(아이템 안 닳게).
    // 즉시 회복(Heal) 타입이 이미 만피(시간 최대)일 때만 막는다. 스탯/지속회복 앰플은 안 막음.
    public static bool CanApply(string itemId, Player player)
    {
        if (player == null) return true;   // 플레이어 못 찾으면 막지 않음(정상 흐름에 맡김)
        if (!GameDataHolder.I.ConsumableEffect.TryGet(itemId, out var effect)) return true;
        if (effect.consumableType == ConsumableType.Heal
            && player.Stat.CurrentHp >= player.Stat.MaxHp)
            return false;   // 시간(체력)이 이미 가득 - 회복 앰플 무의미
        return true;
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

            case ConsumableType.PermanentStat:
                return ApplyPermanentStat(effect, delta, player);

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

    // 영구 스탯 증가 (revert 없음, 누적)
    // 후반에 ATK 200 / 300 식으로 영구히 누적되는 RPG식 강화
    private static bool ApplyPermanentStat(ConsumableEffectSheetData effect, float delta, Player player)
    {
        switch (effect.effectTarget)
        {
            case EffectTarget.ATK:
                player.Stat.ATK += delta;
                Debug.Log($"[ConsumableEffect] 영구 ATK +{delta} → 현재 ATK={player.Stat.ATK}");
                return true;

            case EffectTarget.DEF:
                player.Stat.DEF += delta;
                Debug.Log($"[ConsumableEffect] 영구 DEF +{delta} → 현재 DEF={player.Stat.DEF}");
                return true;

            case EffectTarget.Stamina:
                player.Stat.MaxStamina += delta;
                Debug.Log($"[ConsumableEffect] 영구 최대 스태미나 +{delta} → 현재 MaxStamina={player.Stat.MaxStamina}");
                return true;

            // 후속 확장 — MoveSpeed 등 영구 증가 필요해지면 case 추가
            default:
                Debug.LogWarning($"[ConsumableEffect] PermanentStat 미지원 effectTarget={effect.effectTarget}");
                return false;
        }
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
            EffectTarget.DEF => player.Stat.DEF,
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