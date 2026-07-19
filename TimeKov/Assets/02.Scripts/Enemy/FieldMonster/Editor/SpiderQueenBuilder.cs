// 거미여왕(엘리트). 공용 FieldMonsterBuilder 에 설정만 넘긴다.
// 성격(거미S3와 대비): 느리고 단단한 여왕. 공격/전조가 느려 큰 예고 → 공격 후 경직이 길어
//   반격 창이 크고, 대신 짧게만 물러났다 금방 다시 달려든다(압박형).
// ※클립 접두사가 섞여 있음: attack/idle=Anim@... , walk/straif/hit/death=Anim_...
// 실행: Window > Field Monster Builder 창의 버튼(툴 메뉴에는 노출 안 함).
public static class SpiderQueenBuilder
{
    const string Src = "Assets/00.창동에셋/몬스터/거미여왕";
    const string Frost = "Assets/00.창동에셋/VFX/VFX(눈)/Elemental VFX Mega Bundle/Frost";

    public static void Build()
    {
        FieldMonsterBuilder.Build(new FieldMonsterBuildConfig
        {
            // 경로
            srcRoot         = Src,
            modelPrefabName = "거미여왕_Skin1.prefab",
            skinMatName     = "M_Spider_S_Queen_Skin1.mat",
            skinTexPath     = Src + "/Textures/Skin1/T_Spider_S_Queen_Skin1_AlbedoTransparency.tga",
            ctrlName        = "거미여왕_Enemy.controller",   // -> 04.Animations/AnimationController/Enemy (기본)
            soPath          = "Assets/06.ScriptableObjects/Enemy/FieldData_SpiderQueen.asset",
            prefabPath      = "Assets/05.Prefabs/Enemy/Enemy_SpiderQueen.prefab",

            // 클립 (@/_ 접두사 섞임 주의)
            clipIdle  = "Anim@Spider_S_Queen_idle1",
            clipFront = "Anim_Spider_S_Queen_walk5",
            clipBack  = "Anim_Spider_S_Queen_walk6",
            clipLeft  = "Anim_Spider_S_Queen_straif_L",
            clipRight = "Anim_Spider_S_Queen_straif_R",
            clipAttack = "Anim@Spider_S_Queen_attack3",     // 원거리 시전 모션
            clipRoar   = "Anim_Spider_S_Queen_get_hit3",    // 포효 대용
            clipHit    = "Anim_Spider_S_Queen_get_hit1",
            clipDeath  = "Anim_Spider_S_Queen_death2",

            // 정체
            enemyName = "거미여왕", enemyId = "spider_queen", sourceId = "MeleeBot_SpiderQueen",

            // 스탯 — 느리고 단단, 원거리 얼음 시전 (설산)
            maxHP = 200f, moveSpeed = 3.5f, attackDamage = 25f,
            attackRange = 13f, attackApproachRatio = 0.85f,   // ~11m 거리 유지하며 시전
            visionRange = 22f, visionAngle = 320f,
            walkAnimRefSpeed = 2.0f,   // 느린 대형 걸음 기준(발 미끄러지면 조정)

            // ── 원거리(얼음/마법 발사체) ──
            ranged = true,
            projectileVfx = Frost + "/Projectile_Frost.prefab",
            muzzleVfx     = Frost + "/MuzzleFlash_Frost.prefab",
            impactVfx     = Frost + "/Impact_Frost.prefab",
            telegraphVfx  = Frost + "/Orb_Frost.prefab",       // 제자리 구체(모여드는 모션 없음)
            telegraphBone = "Head",                            // 옆쪽 Jaw 말고 중앙 머리 본
            telegraphOffset = new UnityEngine.Vector3(0f, 0.55f, 0.85f), // 얼굴 앞·위로(앞0.85·위0.55)
            telegraphScale = 0.5f,                             // 크기 조정(발사 시점 최대치 기준)
            telegraphGrow = true,                              // 작게 시작→발사 순간 최대(차징 읽힘)
            telegraphGrowFrom = 0.15f,
            projectileSpeed = 12f,       // 피할 수 있는 속도
            projectileHitRadius = 0.7f,
            projectileLifeTime = 4f,
            projectileHomingDeg = 140f,  // 초반 강하게 보정
            projectileHomingDuration = 0.35f,  // 처음 0.35초만 따라감 -> 그냥 서있으면 맞고, 늦게 피하면 회피
            projectileSpawnHeight = 1.3f,
            projectileAimHeight = 1.0f,

            // 패턴 — 큰 예고 + 큰 경직(반격 창) + 거리 유지 카이팅
            attackSpeedMul = 0.7f,     // 공격 더 느리게(전조 잘 읽힘)
            roarSpeedMul   = 0.55f,    // 위압적인 느린 포효
            postAttackPause = 0.6f,    // 시전 후 경직(접근 반격 기회)
            attackCooldown  = 0.4f,
            afterStepMin = 0.6f, afterStepMax = 1.8f,   // 쏘고 뒤로 빠져 거리 유지
            retreatDiag  = 35f,
            stepSpeedMul = 0.5f,
            wanderRadius = 5f,

            // 사운드는 슬롯만(추후).
        });
    }
}
