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

    [Tooltip("전조/발사 위치 미세 보정(몸 기준: x=오른쪽, y=위, z=앞). 본이 얼굴 안쪽/옆이라 " +
             "이펙트가 어긋날 때 앞(z)·위(y)로 밀어 얼굴 앞에 오게 한다.")]
    public Vector3 telegraphLocalOffset = Vector3.zero;

    [Tooltip("전조 VFX 크기 배율. 이펙트가 너무 크면 줄인다(1=원본).")]
    public float telegraphScale = 1f;

    [Tooltip("전조 VFX에서 제거할 자식 오브젝트 이름들(예: 중복 원 'Circle_blast'). " +
             "원본 프리팹은 그대로, 스폰된 인스턴스에서만 제거한다.")]
    public string[] telegraphStripObjects;

    [Tooltip("전조 VFX 재생속도를 hitDelay에 맞춤 → 공격(타격) 순간에 딱 완성/수렴되게. " +
             "수렴형 전조(Charge 등)가 중간에 끊기지 않고 정확히 다 모였을 때 타격.")]
    public bool telegraphSyncToHit = false;

    [Tooltip("전조가 작게 시작해 발사 순간까지 커지는 '차징' 연출. 원거리 시전이 잘 읽힌다.")]
    public bool telegraphGrow = false;

    [Tooltip("차징 시작 크기 비율(최종 대비). 0.15면 15%에서 시작해 100%까지 차오름.")]
    [Range(0.02f, 1f)] public float telegraphGrowFrom = 0.15f;

    [Tooltip("공격 애니 재생 속도 배율. 1보다 작으면 모션이 느려져 전조가 잘 읽힌다.")]
    [Range(0.3f, 1.5f)] public float attackSpeedMul = 0.85f;

    [Tooltip("피격 시 경직(Hit 애니)이 나올 확률(0~1). 1=매 피격마다 경직(기존), 0=경직 없음(하이퍼아머). " +
             "낮출수록 피격에 공격/패턴이 덜 끊긴다. 무겁고 단단한 몹일수록 낮게. " +
             "※AnyState→Hit 전이가 Hit && Stagger 를 요구하고, 피격 순간 AI가 이 확률로 Stagger 를 켠다.")]
    [Range(0f, 1f)] public float staggerChance = 1f;

    // ── 휴면/기상 (선택: 바위 골렘처럼 웅크렸다 깨어남) ──────────────
    [Header("── 휴면/기상 (선택) ──")]
    [Tooltip("켜면 시작이 휴면: 발견 전 제자리 대기, '첫 발견'에만 기상(Detect) 연출. " +
             "재조우(이미 각성) 시엔 기상 연출 생략하고 바로 전투. 빌더가 휴면 클립 지정 시 자동 ON.")]
    public bool startDormant = false;

    [Tooltip("각성 상태에서 타깃을 이 시간(초) 이상 못 보면 붕괴(Sleep)해 휴면으로 복귀. 0=복귀 안 함. " +
             "그 전까지는 배회하며 재조우를 기다린다.")]
    public float sleepAfterIdle = 0f;

    [Tooltip("붕괴(휴면 복귀) 애니 길이(초). 빌더가 crumble 클립에서 자동 계산.")]
    public float sleepAnimDuration = 1f;

    // ── 근접 타격 VFX ──────────────────────────────────────────────
    // 근접 공격 접점(hitDelay)에 스폰. 명중 여부와 무관하게 항상 재생(헛쳐도 슬램 이펙트는 난다) = 묵직함.
    [Header("── 근접 타격 VFX ──")]
    [Tooltip("근접 타격 순간 스폰하는 VFX(슬램/클랩 먼지 등). 월드에 남고 자동 소멸. 비면 없음.")]
    public GameObject meleeImpactVFX;

    [Tooltip("근접 타격 VFX 스폰 위치(몸 기준: x=오른쪽, y=위, z=앞). 손이 맞부딪는/내려찍는 지점에 맞춘다.")]
    public Vector3 meleeImpactOffset = new Vector3(0f, 0f, 1.5f);

    [Tooltip("근접 타격 VFX 크기 배율(1=원본). 묵직하게 키우려면 올린다.")]
    public float meleeImpactScale = 1f;

    [Tooltip("근접 타격 VFX 자동 삭제까지 시간(초).")]
    public float meleeImpactLifeTime = 3f;

    // ── 돌진 공격 (앞으로 달려들어 접촉 피해) ──────────────────────
    // chargeAttack=true 면 제자리 타격 대신 '준비 → 돌진(전진 이동) → 마무리' 3단으로 공격한다.
    //   방향은 돌진 시작 순간의 플레이어 쪽으로 커밋(직진 = 옆으로 피하면 회피 가능).
    [Header("── 돌진 공격 (선택) ──")]
    [Tooltip("켜면 근접 타격 대신 돌진: 앞으로 달려들며 접촉하면 피해. 애니는 ChargeStart→Loop→End 3단.")]
    public bool chargeAttack = false;

    [Tooltip("돌진 전 준비(웅크림) 시간(초). 이 동안 방향이 커밋되고 ChargeStart 가 재생된다. 빌더가 시작 클립 길이로 자동 세팅.")]
    public float chargeWindup = 0.5f;

    [Tooltip("돌진 이동 속도(m/s). 빠를수록 피하기 어렵다.")]
    public float chargeSpeed = 9f;

    [Tooltip("돌진 지속 시간(초). 이 시간 동안 전진한다(속도×시간 ≈ 돌진 거리).")]
    public float chargeDuration = 0.7f;

    [Tooltip("돌진 중 접촉 판정 반경(m). 이 안에 플레이어가 들어오면 1회 명중.")]
    public float chargeHitRadius = 1.3f;

    [Tooltip("돌진 종료(ChargeEnd) 모션 시간(초). 빌더가 종료 클립 길이로 자동 세팅.")]
    public float chargeEndDuration = 0.5f;

    // ── 원거리 공격 (얼음/마법 발사체) ─────────────────────────────
    // ranged=true 면 hitDelay 시점에 근접 타격 대신 발사체(FieldMonsterProjectile)를 쏜다.
    // 발사체는 발사 순간 플레이어 위치를 향해 날아가고(회피 가능), 스치면 피격 판정을 준다.
    [Header("── 원거리 공격 (발사체) ──")]
    [Tooltip("켜면 근접 타격 대신 발사체를 쏜다(원거리). 끄면 기존 근접.")]
    public bool ranged = false;

    [Tooltip("날아가는 발사체 VFX(예: Frost/Projectile_Frost). 이동/충돌은 코드가 구동.")]
    public GameObject projectileVFX;

    [Tooltip("발사 순간 총구(눈/턱)에서 번쩍이는 머즐 VFX. 선택.")]
    public GameObject muzzleVFX;

    [Tooltip("발사체가 맞았을 때 터지는 임팩트 VFX. 선택.")]
    public GameObject impactVFX;

    [Tooltip("발사체 속도(m/s). 느릴수록 피하기 쉬움.")]
    public float projectileSpeed = 14f;

    [Tooltip("발사체 피격 반경(m). 이 안으로 플레이어가 들어오면 명중.")]
    public float projectileHitRadius = 0.6f;

    [Tooltip("발사체 VFX 크기 배율(1=원본). 이펙트가 너무 작으면 키운다.")]
    public float projectileScale = 1f;

    [Tooltip("한 번의 공격에서 연속 발사할 발사체 수(1=단발, 2=팡팡). 각 발사는 그 순간 플레이어를 재조준.")]
    public int projectileCount = 1;

    [Tooltip("연속 발사 사이 간격(초). projectileCount>1 일 때만 사용.")]
    public float projectileInterval = 0.15f;

    [Tooltip("발사체 최대 수명(초). 이 시간이 지나면 사라짐(빗나감).")]
    public float projectileLifeTime = 4f;

    [Tooltip("유도 회전(도/초). 0=직선(회피 가능), 클수록 플레이어를 따라 휘어짐.")]
    public float projectileHomingDeg = 0f;

    [Tooltip("유도가 작동하는 초기 시간(초). 이 시간 동안만 플레이어를 따라가고 이후엔 직진. " +
             "0이면 수명 내내 유도. '처음에만 살짝 따라감'은 0.3 같은 짧은 값.")]
    public float projectileHomingDuration = 0f;

    [Tooltip("발사체를 막는 장애물 레이어(벽/나무/바위/건물). 이 레이어에 막히면 그 자리서 터지고 " +
             "관통하지 않는다(피격 없음).")]
    public LayerMask projectileBlockMask;

    [Tooltip("총구 높이(m). telegraphAnchor 가 없을 때 몸통 기준 발사 높이.")]
    public float projectileSpawnHeight = 1.2f;

    [Tooltip("조준 높이(m). 플레이어 발밑이 아니라 이 높이(가슴)를 겨냥.")]
    public float projectileAimHeight = 1.0f;

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
