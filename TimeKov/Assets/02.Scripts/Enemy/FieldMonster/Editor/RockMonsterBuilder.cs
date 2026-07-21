using UnityEngine;

// 록몬스터(근접 골렘) — 맵 테마 4종. 패턴/스탯/클립/컨트롤러는 전부 공유하고 스킨(모델 프리팹)만 다르다.
//   자연(기본=Default) / 설산(5=Snow) / 사막(7=Rock2) / 용암(6=Rock1)
// 성격: 느리고 단단한 강타 골렘. 바위 더미로 휴면하다 발견 시 조립 기동, 오래 미탐지면 다시 붕괴.
//   각 스킨 프리팹이 자기 머티리얼을 참조하므로 머티리얼 오버라이드는 하지 않는다(프리팹만 교체).
//   ※클립은 단일 DEMO FBX 의 서브클립(singleAnimFbx). 전조 VFX 는 추후 통합 작업에서 일괄.
// 실행: Window > Field Monster Builder 창의 버튼.
public static class RockMonsterBuilder
{
    const string Src   = "Assets/00.창동에셋/몬스터/록몬스터";
    const string Demo  = "Animations/Rock Monster v2 DEMO";   // 모든 클립이 든 단일 FBX(srcRoot 기준 상대)
    const string OutSO  = "Assets/06.ScriptableObjects/Enemy";
    const string OutPre = "Assets/05.Prefabs/Enemy";

    // 하위호환(기존 버튼) = 자연
    public static void Build() => BuildNature();

    // ── 테마별(스킨=모델 프리팹만 다름) ──────────────────────────
    public static void BuildNature() => Make("록몬스터기본",                         // 자연(Default). 기존 이름 유지(씬 인스턴스 보존)
        OutSO + "/FieldData_RockMonster.asset",       OutPre + "/Enemy_RockMonster.prefab");

    public static void BuildSnow()   => Make("록몬스터5",                            // 설산(Snow)
        OutSO + "/FieldData_RockMonster_Snow.asset",  OutPre + "/Enemy_RockMonster_Snow.prefab");

    public static void BuildDesert() => Make("록몬스터7",                            // 사막(Rock2)
        OutSO + "/FieldData_RockMonster_Desert.asset", OutPre + "/Enemy_RockMonster_Desert.prefab");

    public static void BuildLava()   => Make("록몬스터6",                            // 용암(Rock1)
        OutSO + "/FieldData_RockMonster_Lava.asset",  OutPre + "/Enemy_RockMonster_Lava.prefab");

    // ── 공통 설정(패턴/스탯/클립) — 모델 프리팹 + 출력 경로만 갈아끼움 ──
    static void Make(string modelName, string soPath, string prefabPath)
    {
        FieldMonsterBuilder.Build(new FieldMonsterBuildConfig
        {
            // 경로 — 스킨은 각 프리팹이 자체 머티리얼 참조(자연=Default/설산=Snow/사막=Rock2/용암=Rock1) → 오버라이드 X
            srcRoot         = Src,
            modelPrefabName = modelName + ".prefab",
            ctrlName        = "록몬스터_Enemy.controller",   // 4테마 공용(클립 동일)
            soPath          = soPath,
            prefabPath      = prefabPath,

            singleAnimFbx = Demo,   // 단일 FBX 서브클립 방식

            // 클립(서브클립 이름) — 옆걸음 전용 클립 없어 좌/우는 전진 재사용(골렘은 옆걸음 거의 안 함)
            clipIdle  = "Idle",
            clipFront = "Walk",
            clipBack  = "WalkBackward",
            clipLeft  = "Walk",
            clipRight = "Walk",
            clipAttack    = "Attack01a",   // 강타(A=한쪽 손)
            clipAttackAlt = "Attack01b",   // 강타(B=반대 손) — 매 공격 A/B 랜덤, 손 쪽으로 VFX 미러링
            dormantClip = "RubblePose",    // 휴면: 바위 더미로 누워 대기(제자리)
            clipRoar    = "RubbleToIdle",  // 첫 발견 시: 몸이 조립되며 일어남(기동)
            crumbleClip = "IdleToRubble",  // 오래 미탐지 시: 무너져 다시 바위 더미로(휴면 복귀)
            clipHit  = "GotHit",
            clipDeath = "Death",

            // 정체(공통 — 같은 몬스터, 스킨만 다름)
            enemyName = "록몬스터", enemyId = "rock_monster", sourceId = "MeleeBot_RockMonster",

            // 스탯 — 느리고 단단한 강타형
            maxHP = 180f, moveSpeed = 2.2f, attackDamage = 30f, attackRange = 2.8f,
            visionRange = 16f, visionAngle = 260f,
            angularSpeed = 130f,     // 무거운 골렘 — 천천히 회전
            staggerChance = 0.15f,   // 웬만해선 경직 안 함(패턴 유지)

            // 패턴 — 묵직: 강타 후 길게 경직(반격 창), 플레이어 보며 뒤로(정후방) 물러남
            attackSpeedMul = 0.9f, roarSpeedMul = 1.0f,
            postAttackPause = 0.9f, attackCooldown = 0.6f,
            afterStep = CombatStepKind.Retreat, afterStepMin = 0.5f, afterStepMax = 1.4f, retreatDiag = 0f,
            stepSpeedMul = 0.6f, walkAnimRefSpeed = 2.2f,
            // 휴면 중엔 제자리(!awake 분기가 배회 무시), 각성 후 타깃 상실 시엔 걸어다님.
            wander = true, wanderRadius = 7f,
            sleepAfterIdle = 12f,    // 각성 후 12초간 타깃 없으면 붕괴 → 다시 바위 더미로

            // 전조는 추후 일괄 → 비움
            telegraphVfx = "", telegraphBone = "RMHead",

            // 근접 타격 VFX — 지면 흙먼지 폭발(골렘 슬램). 때린 손 쪽 바닥에서 크게 팡(변형 B는 좌우 미러).
            meleeImpactVfx    = Src + "/VFX/Rock Monster Dust Puff.prefab",
            meleeImpactOffset = new Vector3(0.7f, 0.2f, 1.4f),
            meleeImpactScale  = 6.0f,

            // 사운드(몬스터 자체 wav — 슬롯 채움)
            sndAttack = Src + "/Sounds/Attack_1/Attack_1.wav",
            sndDie    = Src + "/Sounds/Death/Death.wav",
        });
    }
}
