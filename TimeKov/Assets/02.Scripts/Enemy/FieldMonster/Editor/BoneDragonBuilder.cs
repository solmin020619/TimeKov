using UnityEngine;

// 본드래곤 — 설산 전용 엘리트. 이중 공격: 멀면 '하늘 낙하'(얼음 돔+가시 폭발), 가까우면 물기(근접).
//   스킨=본드래곤6(Ice). 모델 프리팹이 자체 머티리얼 2개(Limbs+Spine) 참조 → 오버라이드 X.
//   클립은 단일 FBX(Meshes/Bone Dragon v2) 서브클립. 큰 몸집이라 nav 반경↓·회피 None·루트드리프트 제거로 이동 안정화.
//   전조 VFX 는 추후. (Collapse/Reassemble 로 휴면도 가능하나 지금은 공격 2종만.)
// 실행: Window > Field Monster Builder 창의 버튼.
public static class BoneDragonBuilder
{
    const string Src   = "Assets/00.창동에셋/몬스터/본드래곤";
    const string Demo  = "Meshes/Bone Dragon v2";   // 모든 클립이 든 단일 FBX(srcRoot 기준 상대)
    const string Frost = "Assets/00.창동에셋/VFX/VFX(눈)/Elemental VFX Mega Bundle/Frost";   // 얼음 발사체/머즐/임팩트

    public static void Build()
    {
        FieldMonsterBuilder.Build(new FieldMonsterBuildConfig
        {
            // 경로 — 스킨(Ice)은 본드래곤6 이 자체 머티리얼 2개 참조 → 오버라이드 X
            srcRoot         = Src,
            modelPrefabName = "본드래곤6.prefab",
            ctrlName        = "본드래곤_Enemy.controller",
            soPath          = "Assets/06.ScriptableObjects/Enemy/FieldData_BoneDragon.asset",
            prefabPath      = "Assets/05.Prefabs/Enemy/Enemy_BoneDragon.prefab",

            singleAnimFbx = Demo,

            // 클립(서브클립, 이름에 공백 포함) — 옆걸음 전용 없어 좌/우는 전진 재사용
            clipIdle  = "Idle",
            clipFront = "Walk",
            clipBack  = "Walk Backward",
            clipLeft  = "Walk",
            clipRight = "Walk",
            clipAttack = "Fire Head 1",    // ★원거리 = 머리로 쏘는 짧은 모션(투사체에 맞음. 지속 브레스 X)
            meleeClip  = "Attack1",        // ★근접(물기)
            clipRoar   = "Attack2",             // 발견 반응(앞 향한 위협 스냅). 길면 roarSpeedMul 로 조절.
            clipHit    = "Got Hit 1",
            clipDeath  = "Death",

            // 정체
            enemyName = "본드래곤", enemyId = "bone_dragon", sourceId = "MeleeBot_BoneDragon",

            // 스탯 — 단단한 엘리트
            maxHP = 150f, moveSpeed = 3f, attackDamage = 25f,
            attackRange = 12f, attackApproachRatio = 0.85f,   // ~10m 에서 브레스 사거리
            visionRange = 24f, visionAngle = 300f,
            navRadius = 1.2f,   // 큰 몸집이라 nav 반경 작게(경계 끼임 방지). 콜라이더는 크게 유지.
            navAvoidanceType = 0,   // 장애물 회피 None.
            cancelRootDrift = true, // ★걷기 클립이 앞으로 밀렸다 되돌아오는(텔레포트) 것 제거 — 진짜 원인.
            staggerChance = 0.25f,

            // ★공격은 원거리(하늘 낙하) 하나만 — 근접(물기) 제거. ranged=true 라야 발사 분기가 걸림.
            dualAttack = false, ranged = true,

            // ★원거리 = 하늘 낙하. 플레이어 주변에 얼음(창)이 위에서 떨어져 착지 시 가시 폭발 + 반경 피해.
            // 하늘 낙하 = 얼음 돔/장판(PS_SkyShot) + 착지 얼음 가시 폭발(SpikesStrike). 레이저/균열만 제거.
            skyfallAttack = true,
            skyfallVfx = Frost + "/PS_SkyShot_Frost.prefab",         // 얼음 돔/장판(하늘 낙하 연출)
            flattenParticles = new[] { "SnowflakeAtlas" },           // 눈꽃 문양을 바닥에 눕힘(수평 빌보드)

            impactVfx  = Frost + "/IceMagic_SpikesStrike.prefab",    // 착지 얼음 가시 폭발
            // ★제거: 루트 레이저(PS_SkyShot_Frost)·MA_Laser_01 + 후속 레이저형 파티클(콘빔/트레일/광선/스파크/얇은바람)
            //   + 가운데 눈꽃 문양 데칼(MA_UB_DecalSnowflake) + 갈색 균열/분화구 + 흙먼지.
            //   유지: 돔/외곽원/떨어지는 눈꽃(SnowflakeAtlas)/얼음 가시 폭발.
            impactStrip = new[] { "PS_SkyShot_Frost", "MA_Laser_01",
                                  "MFX_Glow_ConeAttack02", "GhostTrail 1", "CS_Impact_Circle_Wind_Soft_Thin",
                                  "ConalATK_PanningNoise_Misty", "Spark2_Additive", "Trai2",
                                  "ImpactLightrays_Blurry 1", "Flecks_Clipped_Alpha",
                                  "WaterNoise_MaskedAlpha_02", "Smoke_HarsherAlbedo_Alpha_Mist_Soft", "Smoke_HarshBlack_Alpha",
                                  "MA_UB_DecalSnowflake",
                                  "Crack_Hole", "MA_Crater_Erosion", "Smoke_Generic_AlphaSoft" },
            projectileCount = 6, projectileInterval = 0.45f,         // 6번 낙하, 애니 길이에 맞춰 흩뿌림
            projectileScale = 0.5f,                                  // ★원 전체 크기 축소(50%) — 보이는 링을 판정(3.3m)에 맞춤
            skyfallHeight = 10f, skyfallFallTime = 0.7f,
            skyfallImpactRadius = 3.3f, skyfallSpread = 3.5f,        // ★판정 반경 = 축소된 보이는 원에 맞춤(끝쪽도 맞도록)
            skyfallMaxTilt = 45f,                                   // ★경사면에선 지면 법선에 맞춰 최대 45도까지 눕혀 밀착(묻힘 방지)
            skyfallGroundClearance = 0.4f,                          // ★법선 방향으로 살짝 띄워 곡면 터레인 묻힘 완화

            // 패턴 — 원거리로 견제, 붙으면 물기, 공격 후 뒤로 빠져 거리 유지
            //   ★Fire Head 는 긴 클립(발사는 앞부분)이라 발사 시점을 20%로 앞당김. 물기는 50% 유지(아래 분리 비율).
            attackSpeedMul = 0.9f, roarSpeedMul = 1.0f, hitDelayRatio = 0.2f, meleeHitDelayRatio = 0.5f,
            postAttackPause = 0.7f, attackCooldown = 0.8f,
            afterStep = CombatStepKind.Retreat, afterStepMin = 0.6f, afterStepMax = 1.8f, retreatDiag = 0f,
            stepSpeedMul = 0.6f, walkAnimRefSpeed = 2.5f, wanderRadius = 5f,

            // 전조 없음(추후)
            telegraphVfx = "",

            // 사운드(몬스터 자체 wav) — 브레스 사운드는 투사체와 안 맞아 촙(스냅)으로. 근접/원거리 공용.
            sndAttack = Src + "/Sounds/BoneDragon_Chomp_1.wav",
            sndDie    = Src + "/Sounds/BoneDragon_Bonecreak_1.wav",
        });
    }
}
