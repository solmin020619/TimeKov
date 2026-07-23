using UnityEngine;

// 머쉬룸 몬스터 — 돌진 공격형(공격은 돌진 하나만). 맵 테마 4종, 스킨(모델 프리팹)만 다르다.
//   자연(기본=Red) / 설산(3=Cold) / 사막(6=Orange) / 용암(1=Black)
//   준비 웅크림 → 앞으로 돌진(접촉 피해, 히트하면 그 자리서 종료) → 마무리. 돌진 애니 = MushAttack04 start/loop/end.
//   클립은 단일 DEMO FBX 서브클립. 각 스킨 프리팹이 자체 머티리얼 참조 → 오버라이드 X.
// 실행: Window > Field Monster Builder 창의 버튼.
public static class MushroomBuilder
{
    const string Src   = "Assets/00.창동에셋/몬스터/머쉬룸몬스터";
    const string Demo  = "Meshes/Mushroom_v2 DEMO";   // 모든 클립이 든 단일 FBX(srcRoot 기준 상대)
    const string OutSO  = "Assets/06.ScriptableObjects/Enemy";
    const string OutPre = "Assets/05.Prefabs/Enemy";

    // 하위호환(기존 버튼) = 자연
    public static void Build() => BuildNature();

    // ── 테마별(스킨=모델 프리팹만 다름) ──────────────────────────
    // ★[07-22] 테마마다 정체(이름/id/드롭키)를 따로 준다. 도감엔 4칸으로 뜨고 맵마다 다른 걸 떨굴 수 있다.
    //   예전엔 4종이 sourceId 를 공유해서 도감/드롭이 한 몬스터로 뭉쳐 있었다.
    public static void BuildNature() => Make("머쉬룸몬스터기본",                        // 자연(Red). 기존 이름 유지(씬 인스턴스 보존)
        OutSO + "/FieldData_Mushroom.asset",        OutPre + "/Enemy_Mushroom.prefab",
        "머쉬룸",      "mushroom",        "MeleeBot_Mushroom",
        hp: 60f, atk: 12f);   // 자연맵 입문 = 서리/용암/모래 머쉬룸(95/16)보다 약하게

    public static void BuildSnow()   => Make("머쉬룸몬스터3",                           // 설산(Cold)
        OutSO + "/FieldData_Mushroom_Snow.asset",   OutPre + "/Enemy_Mushroom_Snow.prefab",
        "서리 머쉬룸", "mushroom_snow",   "MeleeBot_Mushroom_Snow");

    public static void BuildDesert() => Make("머쉬룸몬스터6",                           // 사막(Orange)
        OutSO + "/FieldData_Mushroom_Desert.asset", OutPre + "/Enemy_Mushroom_Desert.prefab",
        "모래 머쉬룸", "mushroom_desert", "MeleeBot_Mushroom_Desert");

    public static void BuildLava()   => Make("머쉬룸몬스터1",                           // 용암(Black)
        OutSO + "/FieldData_Mushroom_Lava.asset",   OutPre + "/Enemy_Mushroom_Lava.prefab",
        "용암 머쉬룸", "mushroom_lava",   "MeleeBot_Mushroom_Lava");

    // ── 공통 설정(패턴/스탯/클립) — 모델 프리팹 + 출력 경로 + 정체만 갈아끼움 ──
    static void Make(string modelName, string soPath, string prefabPath,
                     string name, string id, string sid,
                     float hp = 95f, float atk = 16f)   // 자연은 BuildNature 에서 낮춰 부른다(입문맵). 서리/용암/모래는 기본값(95/16).
    {
        FieldMonsterBuilder.Build(new FieldMonsterBuildConfig
        {
            // 경로 — 스킨은 각 프리팹이 자체 머티리얼 참조 → 오버라이드 X
            srcRoot         = Src,
            modelPrefabName = modelName + ".prefab",
            ctrlName        = "머쉬룸몬스터_Enemy.controller",   // 4테마 공용(클립 동일)
            soPath          = soPath,
            prefabPath      = prefabPath,

            singleAnimFbx = Demo,

            // 클립(서브클립) — 옆걸음 전용 없어 좌/우는 전진 재사용
            clipIdle  = "MushIdle",
            clipFront = "MushWalk",
            clipBack  = "MushWalkBack",
            clipLeft  = "MushWalk",
            clipRight = "MushWalk",
            clipAttack     = "MushAttack04start",   // 돌진 시작(준비 웅크림)
            chargeLoopClip = "MushAttack04loop",    // 돌진 루프(전진, 반복)
            chargeEndClip  = "MushAttack04end",     // 돌진 마무리
            clipRoar  = "MushSquash",               // 발견 반응(짧은 스쿼시)
            clipHit   = "MushHit",
            clipDeath = "MushDeath",

            // 정체(테마마다 다름 — 도감/드롭이 4종을 별개로 센다)
            enemyName = name, enemyId = id, sourceId = sid,

            // 스탯 — 먼 거리에서 달려드는 돌진형(중간 체력)
            maxHP = hp, moveSpeed = 3.5f, attackDamage = atk,
            attackRange = 10f, attackApproachRatio = 0.9f,   // ~9m 안 들어오면 돌진 개시(멀리서 시작)
            visionRange = 20f, visionAngle = 300f,
            staggerChance = 0.4f,   // 피격 경직 40%

            // ★돌진 공격(공격은 이것 하나만) — 멀리서 길게 돌진(12×1.0 ≈ 12m), 히트 시 그 자리서 종료
            chargeAttack = true,
            chargeSpeed = 12f, chargeDuration = 1.0f, chargeHitRadius = 1.4f,

            // 패턴 — 돌진 후 경직, 뒤로 빠졌다 재돌진
            attackSpeedMul = 1.0f, roarSpeedMul = 1.0f,
            postAttackPause = 0.7f, attackCooldown = 0.8f,
            afterStep = CombatStepKind.Retreat, afterStepMin = 0.6f, afterStepMax = 1.6f, retreatDiag = 0f,
            stepSpeedMul = 0.7f, walkAnimRefSpeed = 2.5f, wanderRadius = 6f,

            // 전조/접촉 VFX 없음(추후) → 비움
            telegraphVfx = "",
            meleeImpactVfx = "",

            // 사운드(몬스터 자체 wav)
            sndAttack = Src + "/Sounds/Attack_1/Attack_1.wav",
            sndDie    = Src + "/Sounds/Death/Death.wav",
        });
    }
}
