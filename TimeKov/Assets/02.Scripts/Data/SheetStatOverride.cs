// =====================================================================
// SheetStatOverride.cs
// 시트 값을 SO/씬/코드 상수 위에 덮어쓰는 한 곳.
//
// [왜 한 곳에 모았나]
//   몬스터 31종 SO, 스킬 SO 6개, 플레이어 상수, 우주선 레벨표가 각자 DataBoot 를
//   구독하면 "이 값은 어디서 오나"가 네 군데로 흩어진다. 밸런싱 중에 그걸 매번
//   추적하는 게 비용이라 시트로 뺀 건데, 연결이 흩어지면 그 이득이 사라진다.
//   데이터 로드가 끝나는 시점에 여기서 한 번에 밀어넣는다.
//
// [원칙 - 시트가 이기되, 없으면 기존 값]
//   시트에 행/키가 없으면 그 항목은 건드리지 않고 원래 값을 그대로 쓴다.
//   덕분에 항목을 하나씩 옮겨도 게임이 깨지지 않고, 시트 사고가 나도 최악이 '옛 값'이다.
//
// [SO 를 직접 고치는 것에 대해]
//   에디터에서 SO 필드를 런타임에 바꾸면 플레이를 멈춰도 그 값이 남는다(유니티 동작).
//   그래서 원본을 처음 한 번 기억해두고, 적용 전에 항상 원본부터 복구한 뒤 시트를 덮는다.
//   이게 없으면 시트에서 행을 지웠을 때 '마지막에 적용된 값'이 SO 에 눌어붙는다.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public static class SheetStatOverride
{
    // SO 원본값 보관. 키 = SO 인스턴스, 값 = 시트로 덮기 전의 숫자들.
    private static readonly Dictionary<Object, float[]> _monsterOriginals = new();
    private static readonly Dictionary<Object, float[]> _skillOriginals = new();

    /// <summary>데이터 로드가 끝나면 시트 값을 밀어넣는다. 씬 진입마다 호출해도 안전(멱등).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        // 도메인 리로드를 끄면 static 이 살아남아 중복 구독된다. 매 플레이마다 초기화.
        // 중복 구독은 -= 다음 += 로 막는다(구독 여부 플래그는 필요 없다).
        _monsterOriginals.Clear();
        _skillOriginals.Clear();

        DataBoot.OnDataLoaded -= ApplyAll;
        DataBoot.OnDataLoaded += ApplyAll;

        if (DataBoot.IsLoaded) ApplyAll();
    }

    public static void ApplyAll()
    {
        if (GameDataHolder.I == null) return;
        ApplyPlayer();
        ApplyMonsters();
        ApplySkills();
        FacilityBuildLimit.RebuildDefaultsFromSheet();   // 설비 설치 상한도 시트가 원본
        // 우주선은 ShipRepairManager 가 자기 Awake 에서 직접 읽는다(레벨표가 그 컴포넌트 소유라서).
    }

    // ── 플레이어 ─────────────────────────────────────────────────────
    // 키가 시트에 없으면 기존 값 유지. PlayerStatComponent 는 씬에 있으므로
    // 인스턴스를 찾아 적용하고, 시작 스탯 3종은 static 기본값에 반영한다.
    private static void ApplyPlayer()
    {
        var t = GameDataHolder.I.PlayerStatData;
        if (t == null) return;

        PlayerBaseStats.ATK         = Get(t, "baseATK",         PlayerBaseStats.ATK);
        PlayerBaseStats.DEF         = Get(t, "baseDEF",         PlayerBaseStats.DEF);
        PlayerBaseStats.MaxStamina  = Get(t, "baseMaxStamina",  PlayerBaseStats.MaxStamina);
        PlayerStatComponent.Softness = Get(t, "defenseSoftness", PlayerStatComponent.Softness);

        var stat = Object.FindAnyObjectByType<PlayerStatComponent>(FindObjectsInactive.Include);
        if (stat == null) return;

        stat.HpDrainRate        = Get(t, "hpDrainRate",        stat.HpDrainRate);
        stat.StaminaDrain       = Get(t, "staminaDrain",       stat.StaminaDrain);
        stat.StaminaRegen       = Get(t, "staminaRegen",       stat.StaminaRegen);
        stat.StaminaRegenDelay  = Get(t, "staminaRegenDelay",  stat.StaminaRegenDelay);
        stat.ExhaustedThreshold = Get(t, "exhaustedThreshold", stat.ExhaustedThreshold);
        stat.HurtDuration       = Get(t, "hurtDuration",       stat.HurtDuration);
        stat.InvincibleDuration = Get(t, "invincibleDuration", stat.InvincibleDuration);
    }

    private static float Get(DataHolder<PlayerStatDataSheetData> t, string key, float fallback)
        => t.TryGet(key, out var row) ? row.value : fallback;

    // ── 몬스터 ───────────────────────────────────────────────────────
    // 키 = SO 에셋 파일명. enemyId 는 구 몬스터 7종이 전부 'tutorial_enemy' 라 못 쓴다
    // (킬 퀘스트 매칭용이라 바꾸면 퀘스트가 깨진다). 상세는 MonsterStatDataSchema 주석.
    private static void ApplyMonsters()
    {
        var t = GameDataHolder.I.MonsterStatData;
        if (t == null) return;

        // MeleeEnemyData 하나로 3계열이 다 잡힌다(HellMonsterData / FieldMonsterData 가 이걸 상속).
        foreach (var so in Resources.FindObjectsOfTypeAll<MeleeEnemyData>())
        {
            if (so == null) continue;

            if (!_monsterOriginals.TryGetValue(so, out var orig))
            {
                orig = new[] { so.maxHP, so.attackDamage, so.attackRange,
                               so.attackCooldown, so.moveSpeed, so.visionRange };
                _monsterOriginals[so] = orig;
            }
            // 시트에 없으면 원본으로 되돌리고 끝(눌어붙기 방지)
            so.maxHP = orig[0]; so.attackDamage = orig[1]; so.attackRange = orig[2];
            so.attackCooldown = orig[3]; so.moveSpeed = orig[4]; so.visionRange = orig[5];

            if (!t.TryGet(so.name, out var row)) continue;

            so.maxHP          = row.maxHP;
            so.attackDamage   = row.attackDamage;
            so.attackRange    = row.attackRange;
            so.attackCooldown = row.attackCooldown;
            so.moveSpeed      = row.moveSpeed;
            so.visionRange    = row.visionRange;
        }
    }

    // ── 스킬 ─────────────────────────────────────────────────────────
    // 평타(ComboAttackBase)와 스킬(SkillBase)은 타입이 달라 따로 처리한다.
    // 타격 타이밍(Hit1Time 등)은 애니 클립에 맞춘 값이라 시트에 없고 SO 그대로 둔다.
    private static void ApplySkills()
    {
        var t = GameDataHolder.I.SkillData;
        if (t == null) return;

        foreach (var so in Resources.FindObjectsOfTypeAll<ComboAttackBase>())
        {
            if (so == null) continue;
            if (!_skillOriginals.TryGetValue(so, out var orig))
            {
                orig = new[] { so.Damage, so.HitRadius };
                _skillOriginals[so] = orig;
            }
            so.Damage = orig[0]; so.HitRadius = orig[1];

            if (!t.TryGet(so.name, out var row)) continue;
            so.Damage    = row.hit1Damage;
            so.HitRadius = row.hit1Radius;
        }

        foreach (var so in Resources.FindObjectsOfTypeAll<SkillBase>())
        {
            if (so == null) continue;
            if (!t.TryGet(so.name, out var row)) continue;
            so.ApplySheetValues(row);
        }
    }
}
