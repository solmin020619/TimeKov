using UnityEngine;

// 전투 중 한 번의 "스텝"(공격 사이에 위치를 바꾸는 짧은 이동). 몬스터마다 다른 리듬을 만드는 핵심.
public enum CombatStepKind
{
    None,          // 안 움직임 (그 자리에서 대기)
    StrafeLeft,    // 타깃을 본 채 왼쪽으로
    StrafeRight,   // 타깃을 본 채 오른쪽으로
    StrafeRandom,  // 좌/우 랜덤
    BackStep,      // 뒤로 물러남
    BackOrStrafe,  // 뒤로 or 좌/우 랜덤
    Retreat,       // 플레이어를 '보면서' 멀어짐(정후방~대각선 뒤). 매 프레임 플레이어 반대쪽으로.
                   //   뒷걸음 애니가 실제 이동과 매칭돼 옆으로 안 보고 걷는 어색함이 없다.
}

/// <summary>
/// 신규 필드 몬스터용 데이터. 기존 MeleeEnemyData 를 상속해서
///   - 기존 스탯/피드백 필드를 그대로 쓰고 (수정 안 함)
///   - 전조(telegraph) + 행동 패턴 필드만 추가한다.
///
/// 상속하는 이유: EnemyHealth 가 EnemyBrain.Data(=MeleeEnemyData)에서
/// enemyId(퀘스트 킬 집계) / enemyName(HP바) / deathAnimDuration 을 읽는다.
/// 상속해두면 그 연동이 전부 그대로 살아있다.
/// </summary>
[CreateAssetMenu(fileName = "FieldMonsterData", menuName = "Enemy/Field Monster Data")]
public class FieldMonsterData : MeleeEnemyData
{
    // ── 전조(Telegraph) ────────────────────────────────────────────
    // 규칙: 모든 공격은 전조가 있어야 한다. 공격 모션 시작과 동시에 눈/턱에서 번쩍이고,
    // 실제 타격은 hitDelay 뒤에 들어간다 = hitDelay 가 곧 플레이어가 읽고 피할 수 있는 시간.
    [Header("── 전조 (모든 공격 필수) ──")]
    [Tooltip("공격 시작 시 눈/턱에서 번쩍이는 전조 VFX. 위치는 FieldMonsterAI.telegraphAnchor.")]
    public GameObject telegraphVFX;

    [Tooltip("전조 소리 (클립은 나중에 채움. 비어도 동작)")]
    public AudioClip telegraphSound;

    [Tooltip("전조 VFX 자동 삭제까지 시간(초). 0이면 파티클 자체 수명")]
    public float telegraphLifeTime = 0.8f;

    [Tooltip("공격 애니 재생 속도 배율. 1보다 작으면 모션이 느려져 전조가 잘 읽힌다.")]
    [Range(0.3f, 1.5f)] public float attackSpeedMul = 0.85f;

    // ── 행동 패턴 ──────────────────────────────────────────────────
    // 발견 → 즉시 공격이 아니라, 발견 → (오프닝) → 접근 → 공격 → 옆/뒤 스텝 → 재공격 리듬.
    // 몬스터마다 다르게 잡아서 성격을 만든다.
    [Header("── 행동 패턴 (몬스터마다 다르게) ──")]
    [Tooltip("플레이어를 처음 발견한 직후 행동. None이면 바로 접근.")]
    public CombatStepKind openingStep = CombatStepKind.None;

    [Tooltip("발견 직후 스텝 지속 시간(초)")]
    public float openingStepDuration = 0f;

    [Tooltip("공격 후 후퇴 '전에' 잠깐 멈추는 시간(초). 공격 경직 = 플레이어가 반격할 틈. 0이면 바로 후퇴.")]
    public float postAttackPause = 0.5f;

    [Tooltip("공격 직후 행동. 치고 빠지는 리듬의 핵심. Retreat = 플레이어 보며 뒤/대각선 뒤로 멀어짐.")]
    public CombatStepKind afterAttackStep = CombatStepKind.Retreat;

    [Tooltip("공격 후 스텝 지속 시간 랜덤 범위(초) 최소/최대. ★매번 랜덤 = 실질 공격 간격이 들쭉날쭉해짐.")]
    public Vector2 afterAttackStepDurationRange = new Vector2(0.8f, 1.6f);

    [Tooltip("Retreat 시 뒤로 빠지는 좌우 최대 각도(도). 0=정후방만, 45=대각선 뒤까지 섞임.")]
    [Range(0f, 80f)] public float retreatDiagonalMaxAngle = 45f;

    [Tooltip("뒤/기타 스텝 이동 속도 = moveSpeed × 이 값")]
    [Range(0.2f, 2f)] public float stepSpeedMul = 0.85f;

    [Tooltip("옆걸음(straif) 전용 속도 = moveSpeed × 이 값. 옆으로만 느리게/빠르게 하고 싶을 때.")]
    [Range(0.05f, 2f)] public float strafeSpeedMul = 0.85f;

    [Tooltip("옆걸음 애니 재생 배속(★이동 속도와 분리). 옆걸음을 느리게 이동시켜도 애니는 이 배속으로 " +
             "자연스럽게 밟는다. 1=클립 원속도. 이동속도에 묶으면 느린 옆걸음이 슬로모션처럼 보여서 분리함.")]
    [Range(0.2f, 2f)] public float strafeAnimSpeedMul = 1f;

    // ── 이동 애니 속도 동기화 ──────────────────────────────────────
    // 클립이 제자리(in-place) 애니라 루트모션이 없어서 Unity가 자동으로 발을 맞춰주지 못한다.
    // -> "이 클립이 자연스러워 보이는 속도"를 적어두고, 실제속도/기준속도 만큼 재생 배속을 건다.
    [Header("── 이동 애니 동기화 (발 미끄러짐 방지) ──")]
    [Tooltip("걷기 애니가 원래 속도(1배속)로 자연스러워 보이는 이동 속도(m/s).\n" +
             "발이 헛돌면(애니가 빠름) 값을 올리고, 발이 질질 끌리면(애니가 느림) 내린다.")]
    public float walkAnimRefSpeed = 2.5f;

    [Tooltip("애니 재생 배속 허용 범위. 너무 벌어지면 부자연스러워서 잘라낸다.")]
    public Vector2 walkAnimSpeedClamp = new Vector2(0.4f, 2.5f);

    [Tooltip("걷기 방향 전환(옆↔앞↔뒤) 블렌드 damp 시간(초). 스텝이 끝날 때 포즈가 툭 튀는(들썩) 것을 " +
             "부드럽게 이어 없앤다. 0=즉시(튐). 0.1~0.2 권장.")]
    public float locoDirDamp = 0.12f;

    [Header("── 순찰 (타깃 없을 때) ──")]
    [Tooltip("가만히 서 있지 않고 주변을 어슬렁거림")]
    public bool wander = true;

    [Tooltip("어슬렁 반경(m). 스폰 지점 기준")]
    public float wanderRadius = 6f;

    [Tooltip("어슬렁 목적지 사이 대기(초) 최소/최대")]
    public Vector2 wanderPauseRange = new Vector2(1.5f, 4f);

    [Tooltip("어슬렁 이동 속도 = moveSpeed × 이 값")]
    [Range(0.1f, 1f)] public float wanderSpeedMul = 0.4f;
}
