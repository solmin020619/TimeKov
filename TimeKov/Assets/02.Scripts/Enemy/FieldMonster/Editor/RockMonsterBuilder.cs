using UnityEngine;

// 록몬스터(근접 골렘). 느리고 단단하며 한 방이 묵직한 강타형.
//   성격: 천천히 접근 → 강타 → 길게 경직(반격 창) → 뒤로 물러남 → 재접근.
//   ※클립이 '단일 DEMO FBX'의 서브클립이라 singleAnimFbx 로 지정한다(거미와 다른 점).
//   전조 VFX 는 추후 전조 통합 작업에서 일괄 적용 예정 → 지금은 비운다(telegraphVfx="").
// 실행: Window > Field Monster Builder 창의 버튼.
public static class RockMonsterBuilder
{
    const string Src  = "Assets/00.창동에셋/몬스터/록몬스터";
    const string Demo = "Rock Monster v2 DEMO";   // 모든 클립이 든 단일 FBX

    public static void Build()
    {
        FieldMonsterBuilder.Build(new FieldMonsterBuildConfig
        {
            // 경로
            srcRoot             = Src,
            modelPrefabName     = "록몬스터기본.prefab",
            skinMatPathOverride = Src + "/Skins/RockMonster Default.mat",              // 완성형 스킨(Materials/ 아님)
            skinTexPath         = Src + "/Skins/01_Default/RockMonster_albedoOpacity.png",
            ctrlName            = "록몬스터_Enemy.controller",   // -> 04.Animations/AnimationController/Enemy
            soPath              = "Assets/06.ScriptableObjects/Enemy/FieldData_RockMonster.asset",
            prefabPath          = "Assets/05.Prefabs/Enemy/Enemy_RockMonster.prefab",

            singleAnimFbx = Demo,   // ★단일 FBX 서브클립 방식

            // 클립(서브클립 이름) — 옆걸음 전용 클립이 없어 좌/우는 전진 재사용(골렘은 옆걸음 거의 안 함)
            clipIdle  = "Idle",
            clipFront = "Walk",
            clipBack  = "WalkBackward",
            clipLeft  = "Walk",
            clipRight = "Walk",
            clipAttack    = "Attack01a",   // 강타(A=한쪽 손)
            clipAttackAlt = "Attack01b",   // 강타(B=반대 손) — 매 공격 A/B 랜덤, 손 쪽으로 VFX 미러링
            dormantClip = "RubblePose",    // 휴면: 바위 더미로 누워 대기(제자리)
            clipRoar   = "RubbleToIdle",   // 첫 발견 시: 몸이 조립되며 일어남(기동)
            crumbleClip = "IdleToRubble",  // 오래 미탐지 시: 무너져 다시 바위 더미로(휴면 복귀)
            clipHit    = "GotHit",
            clipDeath  = "Death",

            // 정체
            enemyName = "록몬스터", enemyId = "rock_monster", sourceId = "MeleeBot_RockMonster",

            // 스탯 — 느리고 단단한 강타형
            maxHP = 180f, moveSpeed = 2.2f, attackDamage = 30f, attackRange = 2.8f,
            visionRange = 16f, visionAngle = 260f,
            angularSpeed = 130f,     // 무거운 골렘 — 천천히 회전(기상 중·전투 중 홱 도는 버그감 제거)
            staggerChance = 0.15f,   // 무거운 골렘 — 웬만해선 경직 안 함(패턴 유지)

            // 패턴 — 묵직: 강타 후 길게 경직(반격 창), 플레이어를 '보며' 뒤로 물러남(거미와 동일 Retreat).
            //   옆걸음 전용 클립이 없어 대각선은 0(정후방만) → WalkBackward 로 깔끔하게 뒷걸음.
            attackSpeedMul = 0.9f, roarSpeedMul = 1.0f,   // 포효가 2클립(무너짐+재조립)이라 배속 1.0으로 경직 과다 방지
            postAttackPause = 0.9f, attackCooldown = 0.6f,
            afterStep = CombatStepKind.Retreat, afterStepMin = 0.5f, afterStepMax = 1.4f, retreatDiag = 0f,
            stepSpeedMul = 0.6f, walkAnimRefSpeed = 2.2f,
            // 휴면 중엔 제자리(!awake 분기가 배회 무시), 각성 후 타깃 상실 시엔 걸어다님.
            wander = true, wanderRadius = 7f,
            sleepAfterIdle = 12f,   // 각성 후 12초간 타깃 없으면 붕괴 → 다시 바위 더미로

            // 전조는 추후 일괄 작업 → 지금은 비움(기본값 Charge_01 이 붙지 않게 명시적으로 "")
            telegraphVfx = "", telegraphBone = "RMHead",

            // 근접 타격 VFX — 지면 흙먼지 폭발(골렘 슬램). 크게 키워 묵직함 강조.
            //   2종 공격이라 x 오프셋을 변형(B)에서 좌우 미러링 → 때린 손 쪽 바닥에서 팡.
            meleeImpactVfx    = Src + "/VFX/Rock Monster Dust Puff.prefab",
            meleeImpactOffset = new Vector3(0.7f, 0.2f, 1.4f),
            meleeImpactScale  = 6.0f,

            // 사운드(몬스터 자체 wav — 슬롯 채움)
            sndAttack = Src + "/Sounds/Attack_1/Attack_1.wav",
            sndDie    = Src + "/Sounds/Death/Death.wav",
        });
    }
}
