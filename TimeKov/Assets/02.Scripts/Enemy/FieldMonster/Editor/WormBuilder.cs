using UnityEngine;

// 자이언트웜(사막 고정형 매복 몬스터) — 스킨=자이언트웜2(Default=모래색).
//   평소엔 모래 속에 잠복(Hidden) + 발밑에 'Sand Ground' VFX 상시.
//   플레이어가 사거리 안에 들어오면 Sand Ground 사라지고 'Sand FX 3'(등장 폭발)와 함께 솟아오름(Appear).
//   공격: 온몸으로 주변을 휩쓰는 스윕(Attack01) — 360도 거리 판정, 착지 순간 모래 폭발 VFX.
//   타깃 상실 후 오래되면 다시 잠복(Disappear)으로 복귀.
//   ※고정형: 이동 안 함(wander X, afterStep None, visionRange=attackRange 로 Chase 무동작).
//   ※클립은 단일 DEMO FBX 서브클립. 전조 VFX 는 추후 통합.
// 실행: Window > Field Monster Builder 창의 버튼.
public static class WormBuilder
{
    const string Src  = "Assets/00.창동에셋/몬스터/자이언트웜";
    const string Demo = "Animations/Giant Worm v2 DEMO";   // 모든 클립이 든 단일 FBX(srcRoot 기준 상대)
    const string Vfx  = "Assets/00.창동에셋/VFX/VFX(사막 환경효과)/Sand Effects pack/Prefabs";

    public static void Build()
    {
        FieldMonsterBuilder.Build(new FieldMonsterBuildConfig
        {
            // 경로 — 스킨은 자이언트웜2(Default=모래색)가 자체 머티리얼 참조 → 오버라이드 X
            srcRoot         = Src,
            modelPrefabName = "자이언트웜2.prefab",
            ctrlName        = "자이언트웜_Enemy.controller",
            soPath          = "Assets/06.ScriptableObjects/Enemy/FieldData_GiantWorm.asset",
            prefabPath      = "Assets/05.Prefabs/Enemy/Enemy_GiantWorm.prefab",

            singleAnimFbx = Demo,

            // 클립(서브클립) — 고정형이라 로코 클립은 전부 Idle(이동 없음)
            clipIdle  = "Idle",
            clipFront = "Idle", clipBack = "Idle", clipLeft = "Idle", clipRight = "Idle",
            clipAttack  = "Attack02",     // ★온몸 스윕(주변 휩쓸기) — Attack01=물기, Attack02=스윕
            dormantClip = "Hidden",       // 잠복: 모래 속에 숨어 대기(제자리) → startDormant 자동 ON
            clipRoar    = "Appear",       // 발견 시: 모래 뚫고 솟아오름
            crumbleClip = "Disappear",    // 타깃 상실 오래되면: 다시 모래 속으로(잠복 복귀)
            clipHit  = "GotHitBody",
            clipDeath = "Death",

            // 정체
            enemyName = "자이언트웜", enemyId = "giant_worm", sourceId = "MeleeBot_GiantWorm",

            // 스탯 — 고정 매복 엘리트. 사거리=스윕 반경, 시야=사거리(그 안에 들어와야 감지·타격) → 이동 불필요
            maxHP = 200f, moveSpeed = 2f, attackDamage = 35f,
            attackRange = 6f, attackApproachRatio = 1.0f,     // 접근 안 함(그 자리서 스윕). reach(6)≥시야(5.5) → Chase 무이동
            visionRange = 5.5f, visionAngle = 360f,           // 사방 감지(사거리 안일 때만)

            // ★스윕 판정 — 단발 X. 스윕 활성 구간(20~80%) 동안 '머리 본(Head Control)'이 훑고 지나며
            //   플레이어에 반경(2.5) 안으로 접근하는 순간 히트 → 타이밍이 플레이어 위치에 따라 달라짐.
            sweepAttack = true, sweepStartRatio = 0.2f, sweepEndRatio = 0.6f,   // 0.6 이후는 회복 모션 → 판정 종료
            sweepHitBone = "Head Control", sweepHitRadius = 2.5f,
            angularSpeed = 120f,
            staggerChance = 0.1f,       // 큰 몸집 — 웬만해선 경직 안 함

            // 패턴 — 고정형: 스텝 이동 전부 없음(제자리), 각성 후 오래 미탐지면 잠복 복귀
            attackSpeedMul = 1.0f, roarSpeedMul = 1.0f, hitDelayRatio = 0.5f,
            postAttackPause = 0.7f, attackCooldown = 0.8f,
            openingStep = CombatStepKind.None, afterStep = CombatStepKind.None,
            wander = false,
            sleepAfterIdle = 8f,        // 각성 후 8초간 타깃 없으면 다시 모래 속으로

            // 전조는 추후 → 비움
            telegraphVfx = "",

            // ★VFX — 사용자가 지정한 사막 환경효과
            dormantGroundVfx  = Vfx + "/Sand Ground.prefab",   // 잠복 중 발밑 상시(각성 시 제거)
            dormantGroundScale = 1f,
            // 등장 버스트 = 1회만(Sand FX 3 이 루프 프리팹이라 loop 꺼서 스폰). detectVfx 는 비워 공용 피드백의 루프 스폰 방지.
            appearBurstVfx    = Vfx + "/Sand FX 3.prefab",     // 발견=등장 폭발(솟아오를 때 1회)
            appearBurstScale  = 1f, appearBurstLifeTime = 3f,

            // 공격 VFX 없음(모션만) — 스윕 자체로 표현. 사운드도 VFX 오디오 제거로 조용히.

            // 사운드(슬롯) — 추후
        });
    }
}
