using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 헬 몬스터 지상 4종 설정 + 생성 메뉴.
/// 튜닝은 전부 여기서 한다. ★프리팹/SO 인스펙터에서 만지면 재빌드 때 날아간다.
/// 메뉴: Tools > Enemy > Hell
/// </summary>
public static class HellMonsterConfigs
{
    // Charge / Muzzle / Effect(투사체) / Hit 이 번호별로 짝이 맞는 팩이다.
    // 몹마다 번호 하나를 잡으면 모으기~날아감~착탄 톤이 자동으로 통일된다.
    const string VfxRoot = "Assets/00.창동에셋/VFX/VFX(전조)/ChargeProjectiles_Chargefx/Prefabs_Chargefx";
    static string Charge(int n) => $"{VfxRoot}/Charge/Charge_{n:00}.prefab";
    static string Muzzle(int n) => $"{VfxRoot}/Muzzle/Muzzle_{n:00}.prefab";
    static string Shot(int n) => $"{VfxRoot}/Projectile/Effect_{n:00}.prefab";
    static string Hit(int n) => $"{VfxRoot}/Hit/Hit_{n:00}.prefab";

    [MenuItem("Tools/Enemy/Hell/Build 헬하운드")]
    public static void BuildHound() => HellMonsterBuilder.Build(Hound());

    [MenuItem("Tools/Enemy/Hell/Build 헬뱃")]
    public static void BuildBat() => HellMonsterBuilder.Build(Bat());

    [MenuItem("Tools/Enemy/Hell/Build 헬버그")]
    public static void BuildBug() => HellMonsterBuilder.Build(Bug());

    [MenuItem("Tools/Enemy/Hell/Build 헬사이클롭")]
    public static void BuildCyclop() => HellMonsterBuilder.Build(Cyclop());

    [MenuItem("Tools/Enemy/Hell/Build 지상 4종 전부")]
    public static void BuildAll()
    {
        if (!EditorUtility.DisplayDialog("헬 지상 4종 생성",
            "헬하운드 / 헬뱃 / 헬버그 / 헬사이클롭 을 전부 다시 굽는다.\n" +
            "프리팹과 SO 는 통째로 덮어써진다. 계속?", "생성", "취소")) return;
        BuildHound(); BuildBat(); BuildBug(); BuildCyclop();
    }

    // ── 헬하운드: 러셔. 물고 할퀴며 붙고, 멀면 도약으로 좁힌다.
    static HellConfig Hound() => new HellConfig
    {
        enemyName = "헬하운드", enemyId = "hell_hound", sourceId = "hell_hound",
        folderName = "헬하운드",
        // ★크기 1.5배. 콜라이더는 로컬값이라 scale 이 알아서 키우지만,
        //   월드 좌표로 도는 값들(사거리/시야높이/전조높이/입오프셋)은 여기서 직접 1.5배 해줬다.
        maxHP = 90, moveSpeed = 5.4f, attackDamage = 14f, attackRange = 3.9f, attackCooldown = 1.4f,
        visionRange = 20f, visionAngle = 300f,
        bodyHeight = 1.6f, bodyRadius = 0.6f, scale = 1.5f, eyeHeight = 1.8f,
        clipIdle = "Idle1", clipWalk = "Walk", clipRun = "Run",
        clipRoar = "BattleRoar1", clipDeath = "Death",
        roarTime = 1.3f,
        // jaw 본을 앵커로 잡으므로 거의 보정이 필요 없다. 살짝만 앞으로.
        // (모델 기준 값이라 scale 을 바꿔도 알아서 따라간다)
        muzzleBoneHint = "jaw",
        muzzleOffset = new Vector3(0f, 0f, 0.15f),
        telegraphVfxPath = Charge(7), muzzleVfxPath = Muzzle(7),
        projectileVfxPath = Shot(7), projectileHitVfxPath = Hit(7),
        telegraphTime = 0.85f, telegraphHeight = 1.65f, telegraphScale = 1.4f,
        // ★파티클 색은 0~1 로 넣는다. HDR(8, 1.1, 0.1) 처럼 넣으면 잘려서 노랗게 나온다.
        //   녹은 쇳물 느낌 = 빨강 베이스에 주황이 살짝.
        telegraphColor = new Color(1f, 0.28f, 0.04f, 1f),
        // ★공격은 딱 2종. 밋밋한 근접 평타(물기/할퀴기/강타)는 전부 뺐다.
        //   멀면 불을 뿜고, 붙을 땐 덮친다. 두 개 다 전조가 확실해서 읽고 피할 수 있다.
        attacks = new List<HellAttackConfig>
        {
            // minRange 0 = 코앞에서도 뿜는다. 근접 평타를 뺐기 때문에 이게 없으면
            // 붙어 있을 때 도약 쿨 도는 동안 아무것도 안 하고 서 있는 구멍이 생긴다.
            new HellAttackConfig { label = "화염토해내기", state = "Spit", clipName = "BattleRoar2",
                kind = HellAttackKind.Ranged, telegraph = HellTelegraphKind.Charge,
                weight = 1f, minRange = 0f, maxRange = 18f,
                hitTime = 0.5f, totalTime = 1.8f, cooldown = 4f, damageMul = 0.8f,
                telegraphTime = 1f, telegraphScaleMul = 0.6f,
                shots = 3, shotGap = 0.14f, spreadAngle = 12f },
        },
        // 도약이 곧 근접 공격이다. 붙어 있을 때도 쓸 수 있게 최소거리를 없앴다.
        useLeap = true, leapWeight = 1.2f, leapMinRange = 2.5f, leapMaxRange = 15f,
        // ★leapRadius = 보이는 원 반경(m) = 실제 피해 범위. 일반몹이라 3.2m 면 충분하다.
        //   메시 bounds 로 실측해 맞추므로 이 숫자 그대로 화면에 나온다.
        leapCooldown = 5f, leapDamageMul = 1.3f, leapRadius = 3.2f, leapImpactVfxPath = Hit(7),
        leapArcHeight = 3.2f,
        // 착지 자리에 원이 뜨고, 다 차오르는 순간 덮친다. 그 사이에 걸어 나오면 헛착지.
        leapTelegraphTime = 1.1f, leapTelegraphScale = 1f,
    };

    // ── 헬뱃: 날렵한 견제형. 짧게 치고 빠지는 두 방. 도약으로 거리 재조정.
    static HellConfig Bat() => new HellConfig
    {
        enemyName = "헬뱃", enemyId = "hell_bat", sourceId = "hell_bat",
        folderName = "헬뱃",
        maxHP = 65, moveSpeed = 5.2f, attackDamage = 11f, attackRange = 2.3f, attackCooldown = 1.2f,
        visionRange = 18f, visionAngle = 280f,
        bodyHeight = 1.5f, bodyRadius = 0.55f, scale = 1f, eyeHeight = 1.2f,
        clipIdle = "Idle1", clipWalk = "Walk", clipRun = "Run",
        clipRoar = "BattleRoar1", clipDeath = "Death",
        roarTime = 1.1f,
        telegraphVfxPath = Charge(4), muzzleVfxPath = Muzzle(4),
        projectileVfxPath = Shot(4), projectileHitVfxPath = Hit(4),
        telegraphTime = 0.75f, telegraphHeight = 1.2f,
        telegraphColor = new Color(0.85f, 0.06f, 0.05f, 1f),
        projectileSpeed = 17f,
        attacks = new List<HellAttackConfig>
        {
            new HellAttackConfig { label = "할퀴기", state = "Attack1", clipName = "Attack1",
                weight = 1.3f, maxRange = 2.5f, hitTime = 0.35f, totalTime = 1f, cooldown = 1.8f, damageMul = 1f },
            new HellAttackConfig { label = "연타", state = "Attack2", clipName = "Attack2",
                weight = 1f, maxRange = 2.7f, hitTime = 0.45f, totalTime = 1.4f, cooldown = 3.2f,
                damageMul = 1.25f, impactVfxPath = Hit(4) },
            // ★원거리: 빠른 단발 음파탄. 얘는 견제형이라 자주 쏘되 아프지 않게.
            new HellAttackConfig { label = "음파탄", state = "Screech", clipName = "BattleRoar2",
                kind = HellAttackKind.Ranged, telegraph = HellTelegraphKind.Charge,
                weight = 1.2f, minRange = 4f, maxRange = 16f,
                hitTime = 0.4f, totalTime = 1.4f, cooldown = 4.5f, damageMul = 0.7f,
                telegraphTime = 0.8f, telegraphScaleMul = 0.55f,
                shots = 2, shotGap = 0.18f, spreadAngle = 8f },
        },
        useLeap = true, leapWeight = 1.1f, leapMinRange = 5f, leapMaxRange = 12f,
        leapCooldown = 6.5f, leapFlyTime = 0.6f, leapDamageMul = 1.1f, leapRadius = 2.3f,
        leapImpactVfxPath = Hit(4),
    };

    // ── 헬버그: 잠복형. 땅에 숨었다 플레이어 옆에서 튀어나온다. 시그니처.
    static HellConfig Bug() => new HellConfig
    {
        enemyName = "헬버그", enemyId = "hell_bug", sourceId = "hell_bug",
        folderName = "헬버그",
        maxHP = 110, moveSpeed = 3.6f, attackDamage = 16f, attackRange = 2.8f, attackCooldown = 1.8f,
        visionRange = 17f, visionAngle = 360f,
        bodyHeight = 1.4f, bodyRadius = 0.75f, scale = 1f, eyeHeight = 1.0f,
        clipIdle = "Idle", clipWalk = "Walk", clipRun = "Run",   // ★이 몹만 Idle1 이 아니라 Idle
        clipRoar = "BattleRoar", clipDeath = "Death",            // ★BattleRoar1 이 아니라 BattleRoar
        roarTime = 1.2f,
        telegraphVfxPath = Charge(6), muzzleVfxPath = Muzzle(6),
        projectileVfxPath = Shot(6), projectileHitVfxPath = Hit(6),
        telegraphTime = 1f, telegraphHeight = 0.9f,
        telegraphColor = new Color(1f, 0.45f, 0.03f, 1f),
        projectileSpeed = 11f,
        attacks = new List<HellAttackConfig>
        {
            new HellAttackConfig { label = "물어뜯기", state = "Attack1", clipName = "Attack1",
                weight = 1.2f, maxRange = 3f, hitTime = 0.5f, totalTime = 1.4f, cooldown = 2.6f, damageMul = 1f },
            new HellAttackConfig { label = "휩쓸기", state = "Attack2", clipName = "Attack2",
                weight = 0.9f, maxRange = 3.4f, hitTime = 0.6f, totalTime = 1.7f, cooldown = 4f,
                damageMul = 1.3f, radius = 2.8f, impactVfxPath = Hit(6),
                telegraph = HellTelegraphKind.Ground, telegraphTime = 0.9f },
            // ★원거리: 산성 침을 넓게 뿌린다. 느린 탄이라 옆으로 걸으면 피해진다.
            new HellAttackConfig { label = "산성침", state = "Spit", clipName = "BattleRoar",
                kind = HellAttackKind.Ranged, telegraph = HellTelegraphKind.Charge,
                weight = 1f, minRange = 4.5f, maxRange = 15f,
                hitTime = 0.55f, totalTime = 1.9f, cooldown = 6.5f, damageMul = 0.75f,
                telegraphTime = 1.05f, telegraphScaleMul = 0.6f,
                shots = 3, shotGap = 0.1f, spreadAngle = 18f },
        },
        useBurrow = true, burrowWeight = 1.2f, burrowCooldown = 13f,
        burrowUnderTime = 1.5f, burrowEmergeDistance = 2.6f,
        burrowDamageMul = 1.5f, burrowRadius = 3f,
        clipSubmerge = "Submerge", clipEmerge = "Emerge", burrowImpactVfxPath = Hit(6),
    };

    // ── 헬사이클롭: 준보스. 느리고 아프다. 전조가 제일 길어서 확실히 보고 피할 수 있다.
    static HellConfig Cyclop() => new HellConfig
    {
        enemyName = "헬사이클롭", enemyId = "hell_cyclop", sourceId = "hell_cyclop",
        folderName = "헬사이클롭",
        maxHP = 220, moveSpeed = 3.2f, attackDamage = 24f, attackRange = 3.4f, attackCooldown = 2.4f,
        visionRange = 22f, visionAngle = 270f,
        bodyHeight = 2.6f, bodyRadius = 0.95f, scale = 1f, eyeHeight = 2.0f,
        clipIdle = "Idle1", clipWalk = "Walk", clipRun = "Run",
        clipRoar = "BattleRoar", clipDeath = "Death",
        roarTime = 1.6f,
        telegraphVfxPath = Charge(1), muzzleVfxPath = Muzzle(1),
        projectileVfxPath = Shot(1), projectileHitVfxPath = Hit(1),
        telegraphTime = 1.25f, telegraphHeight = 1.9f,
        telegraphScale = 1.3f,
        telegraphColor = new Color(1f, 0.38f, 0.08f, 1f),
        projectileSpeed = 13f, projectileExplodeRadius = 2.6f,
        attacks = new List<HellAttackConfig>
        {
            new HellAttackConfig { label = "내려찍기", state = "Attack1", clipName = "Attack1",
                weight = 1.2f, maxRange = 3.6f, hitTime = 0.65f, totalTime = 1.9f, cooldown = 3.5f,
                damageMul = 1f, radius = 2.6f, impactVfxPath = Hit(1),
                telegraph = HellTelegraphKind.Ground, telegraphTime = 0.9f },
            new HellAttackConfig { label = "휘두르기", state = "Attack2", clipName = "Attack2",
                weight = 1f, maxRange = 4f, hitTime = 0.7f, totalTime = 2f, cooldown = 4.5f,
                damageMul = 1.2f, halfAngle = 100f, reach = 4f },
            new HellAttackConfig { label = "양손내려치기", state = "Attack4", clipName = "Attack4",
                weight = 0.7f, maxRange = 3.8f, hitTime = 0.85f, totalTime = 2.4f, cooldown = 7f,
                damageMul = 1.8f, radius = 3.4f, impactVfxPath = Hit(1),
                telegraph = HellTelegraphKind.Ground, telegraphTime = 1.2f },
            // ★원거리: 전용 시전 모션(CastToTarget)이 있어서 제일 자연스럽다. 준보스답게 유도 한 발.
            new HellAttackConfig { label = "화염구", state = "Cast", clipName = "CastToTarget",
                kind = HellAttackKind.Ranged, telegraph = HellTelegraphKind.Charge,
                weight = 1.1f, minRange = 5f, maxRange = 20f,
                hitTime = 0.7f, totalTime = 2.2f, cooldown = 6f, damageMul = 0.9f,
                telegraphTime = 1.25f, telegraphScaleMul = 0.7f,
                shots = 1, homing = true },
        },
        useLeap = true, leapWeight = 0.6f, leapMinRange = 7f, leapMaxRange = 15f,
        leapCooldown = 12f, leapFlyTime = 0.85f, leapDamageMul = 1.6f, leapRadius = 3.4f,
        clipJumpStart = "JumpStart", clipJumpFly = "JumpFly", clipJumpEnd = "JumpEnd",
        leapImpactVfxPath = Hit(1),
    };
}

/// 몹 1종의 전체 설정. 빌더가 이것만 보고 굽는다.
public class HellConfig
{
    public string enemyName = "", enemyId = "", sourceId = "", folderName = "";

    public float maxHP = 100, moveSpeed = 4f, attackDamage = 15f, attackRange = 2.5f, attackCooldown = 1.5f;
    public float visionRange = 18f, visionAngle = 300f;
    public float bodyHeight = 1.8f, bodyRadius = 0.6f, scale = 1f;
    public float deathAnimDuration = 2f;
    public float eyeHeight = 1.6f;   // 시야 레이 시작 높이. 큰 몹은 올린다.

    public string clipIdle = "Idle1", clipWalk = "Walk", clipRun = "Run";
    public string clipRoar = "BattleRoar1", clipDeath = "Death";
    public float walkSpeedRef = 2f, runSpeedRef = 5f;
    public float roarTime = 1.2f;

    public string telegraphVfxPath = "";
    // 지면 전조. 예전엔 내가 만든 단순 링(Wyvern_Telegraph)이라 싸구려로 보였다.
    // Anime VFX 팩의 저작된 시전 마법진(원+불꽃+상승화살)으로 교체.
    public string groundTelegraphVfxPath =
        "Assets/00.창동에셋/VFX/VFX(전조)/Anime VFX URP/Shared/Particles/VFX_Debuff_Cast.prefab";
    // 저작된 VFX 라 스케일 1 일 때의 실제 반경. 이걸로 나눠서 원하는 반경에 맞춘다.
    public float groundTelegraphUnitRadius = 1.2f;

    // 차오르는 원(도약 착지 예고). 저작된 AOE 마법진을 빨갛게 물들여 쓴다.
    public float fillCircleUnitRadius = 5f, fillCircleFromScale = 0.05f, fillCircleLinger = 1.2f;
    public Color fillCircleColor = new Color(1f, 0.82f, 0.55f, 1f);
    public float fillOutlineDim = 0.45f;
    public float actionGapMin = 1.2f, actionGapMax = 2f;
    public string fillCircleVfxPath =
        "Assets/00.창동에셋/VFX/VFX(전조)/Anime VFX URP/Shared/Meshes/SM_VFX_Ring01.prefab";
    public string fillCircleMaterialPath =
        "Assets/00.창동에셋/VFX/VFX(전조)/Anime VFX URP/Shared/Materials/M_VFX_Ring_01_Add.mat";

    // 피격 반응
    public bool useHitReaction = true;
    public string[] hitStates = { "GetHit1", "GetHit2" };
    public string[] hitClips = { "GetHit1", "GetHit2" };
    public float hitReactionTime = 0.35f, hitReactionCooldown = 0.7f;
    public float knockbackDistance = 0.35f, knockbackTime = 0.15f;
    public float telegraphTime = 0.9f, telegraphScale = 1f, telegraphHeight = 1.2f;
    public Color telegraphColor = new Color(4f, 0.7f, 0.15f, 1f);

    // 입 앵커. 빌더가 이름으로 찾는다. 위치가 어긋나면 정확한 본 이름을 여기 적는다.
    public string muzzleBoneHint = "";
    public Vector3 muzzleOffset = new Vector3(0f, 0f, 0.5f);

    // 원거리 (패턴에 kind=Ranged 가 하나라도 있으면 빌더가 투사체를 굽는다)
    public string projectileVfxPath = "", projectileHitVfxPath = "", muzzleVfxPath = "";
    public float projectileSpeed = 14f, projectileHitRadius = 0.9f, projectileExplodeRadius = 2f;
    public bool HasRanged => attacks != null && attacks.Exists(a => a.kind == HellAttackKind.Ranged);

    public List<HellAttackConfig> attacks = new List<HellAttackConfig>();

    public bool useLeap = false;
    public float leapWeight = 1f, leapMinRange = 6f, leapMaxRange = 14f, leapCooldown = 9f;
    public float leapFlyTime = 0.7f, leapDamageMul = 1.2f, leapRadius = 2.6f;
    public float leapArcHeight = 2.2f;
    public float leapTelegraphTime = 0.9f, leapTelegraphScale = 1f;
    public string clipJumpStart = "JumpStart", clipJumpFly = "JumpFly", clipJumpEnd = "JumpEnd";
    public string leapImpactVfxPath = "";

    public bool useBurrow = false;
    public float burrowWeight = 1f, burrowCooldown = 14f, burrowUnderTime = 1.4f;
    public float burrowEmergeDistance = 2.5f, burrowDamageMul = 1.3f, burrowRadius = 3f;
    public string clipSubmerge = "Submerge", clipEmerge = "Emerge";
    public string burrowImpactVfxPath = "";

    // 경로 규약
    public string AnimFolder => $"Assets/00.창동에셋/몬스터/{folderName}/Animations";
    public string modelPath => $"Assets/00.창동에셋/몬스터/{folderName}/Prefabs/{folderName}.prefab";
    public string WorkFolder => $"Assets/05.Prefabs/Enemy/{folderName}";
    public string CtrlPath => $"{WorkFolder}/{folderName}_Enemy.controller";
    public string SoFolder => "Assets/05.Prefabs/Enemy/SO";
    public string SoPath => $"{SoFolder}/HellData_{enemyId}.asset";
    public string PrefabName => $"Enemy_{enemyId}";
    public string PrefabPath => $"Assets/05.Prefabs/Enemy/{PrefabName}.prefab";
}

/// 공격 패턴 1개의 설정(빌더 입력용). 런타임 HellAttack 으로 변환된다.
public class HellAttackConfig
{
    public string label = "Attack", state = "Attack1", clipName = "Attack1";
    public float weight = 1f;
    public float minRange = 0f, maxRange = 3f;
    public float hitTime = 0.45f, totalTime = 1.4f, cooldown = 3f;
    public float damageMul = 1f, radius = 0f, halfAngle = 70f, reach = 0f;
    public string impactVfxPath = "";

    public HellAttackKind kind = HellAttackKind.Melee;
    public HellTelegraphKind telegraph = HellTelegraphKind.None;
    public float telegraphTime = 0f, telegraphScaleMul = 1f;
    public int shots = 1;
    public float shotGap = 0.12f, spreadAngle = 0f;
    public bool homing = false;
    public bool lockFacing = false;
}
