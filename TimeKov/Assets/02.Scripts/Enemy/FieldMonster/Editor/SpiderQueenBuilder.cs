using UnityEngine;

// 거미여왕(엘리트) — 맵 테마 3종. 패턴/스탯/클립은 전부 공유하고 스킨 머티리얼 + VFX만 다르다.
//   설산(Skin1, 얼음) / 사막(Skin3, 불꽃) / 자연(Skin5, 초록)
// 성격: 느리고 단단한 원거리 시전형. 큰 전조 → 발사 후 경직(반격 창) → 거리 유지 카이팅.
// ※클립 접두사 섞임: attack/idle=Anim@... , walk/straif/hit/death=Anim_...
// 실행: Window > Field Monster Builder 창의 버튼(툴 메뉴 노출 안 함).
public static class SpiderQueenBuilder
{
    const string Src     = "Assets/00.창동에셋/몬스터/거미여왕";
    const string Frost   = "Assets/00.창동에셋/VFX/VFX(눈)/Elemental VFX Mega Bundle/Frost";
    const string Charge  = "Assets/00.창동에셋/VFX/VFX(전조)/ChargeProjectiles_Chargefx/Prefabs_Chargefx";
    const string Anime   = "Assets/00.창동에셋/VFX/VFX(전조)/Anime VFX URP/Shared/Particles";
    const string NatureFX = "Assets/18.외부에셋/Eric VFX Studio/보스(자연VFX)/Prefab";  // 자연 테마 팩
    const string OutSO   = "Assets/06.ScriptableObjects/Enemy";
    const string OutPre  = "Assets/05.Prefabs/Enemy";

    // 하위호환(기존 버튼) = 설산
    public static void Build() => BuildSnow();

    // ── 테마별(스킨 + VFX만 다름) ────────────────────────────────
    public static void BuildSnow() => Make(   // 설산 = 얼음(Frost)
        skin: "1", teleScale: 0.5f, projScale: 1f,
        tele: Frost + "/Orb_Frost.prefab",
        muzzle: Frost + "/MuzzleFlash_Frost.prefab",
        proj: Frost + "/Projectile_Frost.prefab",
        impact: Frost + "/Impact_Frost.prefab",
        // ★[07-22] 접미사 없던 기본형을 _Frost 로 개명. 3테마가 전부 접미사를 갖게 해서 헷갈릴 여지를 없앴다.
        soPath: OutSO + "/FieldData_SpiderQueen_Frost.asset",
        prefabPath: OutPre + "/Enemy_SpiderQueen_Frost.prefab",
        name: "서리 거미여왕", id: "spider_queen_frost", sid: "MeleeBot_SpiderQueen_Frost");

    public static void BuildDesert() => Make( // 사막 = 불꽃(Anime 파이어볼, 주황). 2연발(팡팡).
        skin: "3", teleScale: 0.5f, projScale: 1.6f,   // 투사체 조금 키움
        tele: Anime + "/VFX_Fire.prefab",                        // 불기운 모임(전조)
        muzzle: Anime + "/VFX_Fireball_Firing.prefab",           // 발사 펀치(전조가 발사 순간 사라진 뒤라 2중전조 X)
        proj: Anime + "/VFX_Fireball_Projectile_Static.prefab",  // 주황 파이어볼(스크립트 0, URP)
        impact: Anime + "/VFX_Fireball_Impact.prefab",
        soPath: OutSO + "/FieldData_SpiderQueen_Desert.asset",
        prefabPath: OutPre + "/Enemy_SpiderQueen_Desert.prefab",
        name: "모래 거미여왕", id: "spider_queen_desert", sid: "MeleeBot_SpiderQueen_Desert",
        projCount: 2, projInterval: 0.4f);                       // ★2발 연달아(팡...팡)

    public static void BuildNature() => Make( // 자연 = 근접(거미S3 방식). 근접 공격.
        skin: "5", teleScale: 1f, projScale: 1f,
        tele: "",                                                // ★전조 임시 제외(나중에 일괄). 복구 시 원본: Charge + "/Charge/Charge_04.prefab"
        muzzle: "", proj: "", impact: "",                        // 근접이라 발사체 계열 없음
        soPath: OutSO + "/FieldData_SpiderQueen_Nature.asset",
        prefabPath: OutPre + "/Enemy_SpiderQueen_Nature.prefab",
        name: "숲 거미여왕", id: "spider_queen_nature", sid: "MeleeBot_SpiderQueen_Nature",
        hp: 240f, atk: 20f,                                      // 자연 중간보스 = 3 거미여왕 중 최약(설산 본드래곤 320보다 아래)
        ranged: false,                                           // ★근접
        attackClip: "Anim@Spider_S_Queen_attack5",               // 근접 공격 모션
        atkRange: 3.2f,
        teleGrow: false,                                         // 효과 자체가 수렴(커지는 연출 X)
        teleUp: 0.6f, teleFwd: 0.15f,                            // 몸 밖 앞쪽 + 위로(버스트 안 가리게)
        teleSync: true,                                          // 수렴이 타격 순간에 딱 완성
        // ★Circle_01/Circle_blast_2/Glow 계열만 남기고 나머지 제거(제일 깔끔)
        teleStrip: new[] { "flare", "flare_1", "Circle_blast", "Wide_ring",
                           "Collecting_particles", "Trails", "Trails_narrow",
                           "Flame_charge", "Collecting_particles (1)" });

    // ── 공통 설정(패턴/스탯/클립) — 위 값만 갈아끼움 ──────────────
    static void Make(string skin, float teleScale, float projScale,
                     string tele, string muzzle, string proj, string impact,
                     string soPath, string prefabPath,
                     string name, string id, string sid,
                     float hp = 290f, float atk = 24f,   // 서리/사막 = 엘리트(기본값). 자연은 BuildNature 에서 낮춰 부른다(중간보스).
                     bool ranged = true,
                     string attackClip = "Anim@Spider_S_Queen_attack3",
                     float atkRange = 13f, float approachRatio = 0.85f,
                     bool teleGrow = true, int projCount = 1, float projInterval = 0.15f,
                     float teleUp = 0.55f, float teleFwd = 0.85f, bool teleSync = false,
                     string[] teleStrip = null)
    {
        FieldMonsterBuilder.Build(new FieldMonsterBuildConfig
        {
            // 경로 — 스킨별 모델 프리팹/머티리얼/텍스처(각 Skin 프리팹은 자기 머티리얼 참조)
            srcRoot         = Src,
            modelPrefabName = $"거미여왕_Skin{skin}.prefab",
            skinMatName     = $"M_Spider_S_Queen_Skin{skin}.mat",
            skinTexPath     = $"{Src}/Textures/Skin{skin}/T_Spider_S_Queen_Skin{skin}_AlbedoTransparency.tga",
            ctrlName        = "거미여왕_Enemy.controller",   // 3테마 공용(클립 동일) -> 04.Animations/.../Enemy
            soPath          = soPath,
            prefabPath      = prefabPath,

            // 클립(공통, @/_ 접두사 섞임)
            clipIdle  = "Anim@Spider_S_Queen_idle1",
            clipFront = "Anim_Spider_S_Queen_walk5",
            clipBack  = "Anim_Spider_S_Queen_walk6",
            clipLeft  = "Anim_Spider_S_Queen_straif_L",
            clipRight = "Anim_Spider_S_Queen_straif_R",
            clipAttack = attackClip,
            clipRoar   = "Anim_Spider_S_Queen_get_hit3",
            clipHit    = "Anim_Spider_S_Queen_get_hit1",
            clipDeath  = "Anim_Spider_S_Queen_death2",

            // 정체(테마마다 다름 — 도감/드롭이 3종을 별개로 센다)
            enemyName = name, enemyId = id, sourceId = sid,

            // 스탯(공통)
            maxHP = hp, moveSpeed = 3.5f, attackDamage = atk,
            attackRange = atkRange, attackApproachRatio = approachRatio,
            visionRange = 22f, visionAngle = 320f,
            walkAnimRefSpeed = 2.0f,
            staggerChance = 0.3f,   // 엘리트 — 가끔만 경직(패턴 위주)

            // 원거리(공통 수치) + VFX(테마별). ranged=false 면 근접(발사체 무시).
            ranged = ranged,
            projectileVfx = proj, muzzleVfx = muzzle, impactVfx = impact, telegraphVfx = tele,
            telegraphBone = "Head",
            telegraphOffset = new Vector3(0f, teleUp, teleFwd),
            telegraphScale = teleScale,
            telegraphGrow = teleGrow, telegraphGrowFrom = 0.15f, telegraphSyncToHit = teleSync,
            telegraphStrip = teleStrip,
            projectileSpeed = 12f, projectileHitRadius = 0.7f, projectileLifeTime = 4f,
            projectileScale = projScale,
            projectileCount = projCount, projectileInterval = projInterval,
            projectileHomingDeg = 140f, projectileHomingDuration = 0.35f,
            projectileSpawnHeight = 1.3f, projectileAimHeight = 1.0f,

            // 패턴(공통)
            attackSpeedMul = 0.7f, roarSpeedMul = 0.55f,
            postAttackPause = 0.6f, attackCooldown = 0.4f,
            afterStepMin = 0.6f, afterStepMax = 1.8f, retreatDiag = 35f,
            stepSpeedMul = 0.5f, wanderRadius = 5f,

            // 사운드는 슬롯만(추후).
        });
    }
}
