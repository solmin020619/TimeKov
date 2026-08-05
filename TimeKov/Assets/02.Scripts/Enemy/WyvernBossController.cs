using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 와이번 지상 보스 - 전용 상태머신.
// 기존 BT(BehaviorGraph)/EnemyBrain은 건드리지 않고(취약), 그 글루(Data 동기화/Speed 파라미터/타게팅/회전)만 직접 복제.
// 재사용 컴포넌트: EnemyHealth(피해/사망), EnemyFeedback(피격/사망 연출), NavMeshAgent, Animator(WyvernBoss 전용 컨트롤러).
// 공격은 상태 이름으로 CrossFade 재생(전용 컨트롤러에 공격별 상태 존재).
// [Stage 1] 추적+물기+사망 [Stage 2] 원거리 파이어볼 [Stage 3] 근접(물기/꼬리침) + 라이트필러 분출 + 공중 다이브 강타(올라갔다 내려찍기). 분출/다이브는 지면 범위 텔레그래프 후 발동.
// 공격 시작 시 방향 커밋(추적 정지) -> dash로 옆/뒤 회피 가능. 강타는 긴 윈드업=읽히는 텔레그래프.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class WyvernBossController : MonoBehaviour, IEnemyDataSource
{
    [Header("데이터 (HP/속도/근접공격 기본 수치는 SO에서 튜닝)")]
    [SerializeField] private MeleeEnemyData data;
    public MeleeEnemyData Data => data;   // 도감 등 외부서 보스 스탯 조회용(보스는 EnemyBrain 미사용)
    [SerializeField] private string bossSubtitle = "시간을 빨아먹는 포식자";   // 상단 보스바 부제

    [Header("사운드 (신규 커스텀 SFX. 패턴별로 다른 소리 - data.attackSound(기존 wyvern attack (1))는 물기(Bite)에 그대로 남겨둠)")]
    [Tooltip("근접 물기(Bite) 전용. 비우면 기존 data.attackSound 로 폴백.")]
    [SerializeField] private AudioClip biteSound;
    [Tooltip("근접 꼬리치기(Stinger) 전용.")]
    [SerializeField] private AudioClip stingerSound;
    [Tooltip("원거리 파이어볼(SpitFireball) 발사음.")]
    [SerializeField] private AudioClip fireballSound;
    [Tooltip("분출(EruptionBarrage) 시전 시작 시 1회(불타는 장판 앰비언스). 개별 발마다가 아니라 바라지 시작에만.")]
    [SerializeField] private AudioClip eruptionChargeSound;
    [Tooltip("분출 개별 발(SingleEruption)이 터지는 순간마다.")]
    [SerializeField] private AudioClip eruptionBurstSound;
    [Tooltip("공중 다이브 강타(DiveSlam) 이륙 시 날갯짓.")]
    [SerializeField] private AudioClip diveWindupSound;
    [Tooltip("공중 다이브 강타 착지 충격음.")]
    [SerializeField] private AudioClip diveImpactSound;
    [Tooltip("페이즈 전환 포효(RoarPhase, HP 66% 도달 시 1회).")]
    [SerializeField] private AudioClip phaseRoarSound;
    [Tooltip("페이즈3 진입 회복 비행(HealPhase) 시 포효.")]
    [SerializeField] private AudioClip healRoarSound;

    [Header("원거리 파이어볼")]
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private Vector3 fireOffset = new Vector3(0f, 2.5f, 2.5f); // 발사 위치(로컬: 위+앞=입 근처)
    [SerializeField] private float rangedRange = 22f;
    [SerializeField] private float rangedCooldown = 3.5f;
    [SerializeField] private float fireballWindup = 0.55f;
    [SerializeField] private float fireballRecover = 0.7f;
    [SerializeField] private int fireballBurstCount = 3;       // 페이즈3 연속 발사 수(팡팡팡)
    [SerializeField] private float fireballBurstGap = 0.18f;   // 연사 간격

    [Header("분출 공격 (라이트필러, 플레이어 위치 조준 - 브레스 대체)")]
    [SerializeField] private GameObject eruptionVfx;            // FX_LightPillar 래퍼
    [SerializeField] private float eruptionRange = 14f;         // 이 사거리 안이면 사용
    [SerializeField] private float eruptionCooldown = 7f;
    [SerializeField] private float eruptionWindup = 0.6f;       // 시전 -> 스폰
    [SerializeField] private float eruptionTelegraph = 0.6f;    // 스폰 -> 데미지(이 사이 피하면 회피)
    [SerializeField] private float eruptionRecover = 0.7f;
    [SerializeField] private float eruptionRadius = 2.5f;       // 솟구침 명중 반경(발당)
    [SerializeField] private float eruptionDmgMul = 1.2f;       // x attackDamage
    [SerializeField] private string eruptionState = "SpreadFire";   // 기존 브레스 애니 재사용
    [SerializeField] private int eruptionBarrageCount = 9;      // 페2 분출 발수(페3 = 이 값의 2배). 여기저기 팡팡팡.
    [SerializeField] private float eruptionBarrageGap = 0.25f;  // 팡 사이 간격(순차 발생)
    [SerializeField] private float eruptionSpread = 6f;         // 주변 폭발이 흩어지는 반경(플레이어 기준)

    [Header("공중 다이브 강타 (FinishBite 대체 - 올라갔다 내려찍기)")]
    [SerializeField] private GameObject slamVfx;               // FX_Weapon Effect 래퍼(착지 임팩트)
    [SerializeField] private float diveRange = 16f;            // 이 사거리 안이면 사용
    [SerializeField] private float diveCooldown = 11f;
    [SerializeField] private float divePrepTime = 0.35f;       // 제자리 준비 모션(날기 전 웅크림)
    [SerializeField] private float diveRiseTime = 0.3f;        // 빠르게 상승(촥)
    [SerializeField] private float diveHoverTime = 0.55f;      // 정점 체공(범위 텔레그래프+상공 이동)
    [SerializeField] private float diveDropTime = 0.26f;       // 급강하(내려찍기)
    [SerializeField] private float diveHeight = 12f;           // 상승 높이(완전 쫙 위로)
    [SerializeField] private float diveRecover = 0.8f;
    [SerializeField] private float diveRadius = 16f;           // 착지 충격 반경(와이번 비례 크게. 30까지 올리면 거의 회피불가-텔레그래프/대쉬 전제)
    [SerializeField] private float diveDmgMul = 2.4f;          // x attackDamage
    [SerializeField] private string diveTakeoffState = "TakeOff";   // 이륙(상승)
    [SerializeField] private string diveHoverState   = "FlyHover";  // 제자리 비행(체공/조준)
    [SerializeField] private string diveFallState    = "DiveFall";  // 급강하
    [SerializeField] private string diveLandState    = "Landing";   // 착지

    [Header("범위 텔레그래프 (분출/다이브 발동 전 지면 표시)")]
    [SerializeField] private GameObject telegraphVfx;          // 지면 링 인디케이터(자체 제작)
    [Tooltip("텔레그래프 원의 보이는 크기 배율. 파티클 링이라 transform 스케일 대비 작게 보여서 보정용. Play 중 이 값 올리며 원이 실제 딜 반경과 겹칠 때까지 맞춰라. 분출/다이브 공통.")]
    [SerializeField] private float telegraphScaleMul = 2.5f;

    [Header("포효 페이즈 (HP 66%/33%서 포효 -> 디버프 + 광폭화)")]
    [SerializeField] private string roarState = "Roar";
    [SerializeField] private float roarBuildup = 0.6f;     // 포효 시작~절규(디버프 발동) 시점
    [SerializeField] private float roarRecover = 1.0f;
    [SerializeField] private float roarLiftHeight = 14f;   // 포효 동안 띄움(로어가 비행 포즈라 지상서 꼬리가 땅에 박힘 -> 확 띄워서 회피. 7은 부족, 14면 회복비행 12보다 위라 확실)
    [SerializeField] private float roarLiftTime = 0.35f;   // 띄움/내림 시간(빠르게)
    [SerializeField] private float debuffDuration = 6f;    // 화면 어둠 + 시간 가속 지속(초)
    [SerializeField] private float debuffDrainMult = 2.5f; // 시간 드레인 배수
    [Range(0f, 0.8f)]    [SerializeField] private float debuffDarkness = 0.5f;          // 화면 전체 어둠 세기(0=없음 ~ 0.8=매우 어둡게). "암흑효과" 강도.
    [Range(0f, 1f)]      [SerializeField] private float debuffVignetteStrength = 0.95f; // 가장자리 비네트 진하기
    [Range(0.05f, 0.8f)] [SerializeField] private float debuffVignetteFalloff = 0.55f;  // 비네트 퍼짐(클수록 넓고 부드럽게)
    [SerializeField] private float enrageCdMul = 0.8f;     // 포효마다 공격 쿨 x (누적, 빨라짐)
    [SerializeField] private float enrageSpeedMul = 1.15f; // 포효마다 이속 x (누적)

    [Header("페이즈3 진입 체력 회복 (1회, 무적+공중부양)")]
    [SerializeField] private float healDuration = 2.5f;    // 회복 지속(파바바박 차오름)
    [SerializeField] private float healPercent = 0.25f;    // maxHP의 비율만큼 회복
    [SerializeField] private int healTicks = 12;           // 회복 분할 틱(파바바박 = 한 칸씩 점프)
    [SerializeField] private float healRiseHeight = 12f;   // 회복 중 떠오르는 높이(확실히 공중에. 꼬리 안 박히게 크게, 인스펙터서 줄여라)
    [SerializeField] private float healRiseTime = 0.8f;    // 떠오름/내려옴 시간(이륙 애니에 맞춰 여유)
    [SerializeField] private GameObject healVfx;           // 충전 연출(선택)

    [Header("애니메이터 (전용 컨트롤러 상태명)")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string fireballState = "Fireball";

    // [07-29] 리쉬 폐지 - 원거리 처리는 EnemySpawnPoint 원거리슬립(activateDistance)이 전담(보스 4종 통일).

    // 근접 공격 정의 (state=컨트롤러 상태명 / reachMul=사거리배수 x attackRange / halfAngle=정면 호 반각
    //  / windup=발사프레임 / recover=후딜 / dmgMul=데미지배수 x attackDamage / cd=개별쿨 / weight=선택가중)
    private struct AtkDef
    {
        public string state; public float reachMul, halfAngle, windup, recover, dmgMul, cd, weight;
    }
    private static readonly AtkDef[] MeleeAttacks =
    {
        new AtkDef { state="Bite",    reachMul=1.0f, halfAngle=50f, windup=0.45f, recover=0.5f, dmgMul=1.0f, cd=2.0f, weight=3f },
        new AtkDef { state="Stinger", reachMul=1.4f, halfAngle=70f, windup=0.50f, recover=0.6f, dmgMul=0.9f, cd=3.5f, weight=2f },
    };
    private const float MeleeMaxReachMul = 1.4f;   // 근접 고려 최대 사거리(가장 긴 reachMul)
    private const float MeleeGap = 0.45f;          // 근접 공격 간 최소 간격

    // 공용 모터(컴포넌트 캐싱 / SO동기화 / 이동 / 회전 / Speed 파라미터). 보스 3종 공유.
    private BossMotor _motor;

    // 아래는 전부 모터 위임(기존 본문 코드를 그대로 쓰기 위한 얇은 프로퍼티)
    private NavMeshAgent _agent => _motor.Agent;
    private EnemyHealth _health => _motor.Health;
    private EnemyFeedback _feedback => _motor.Feedback;
    private Animator _animator => _motor.Animator;
    private Transform _player => _motor.Player;
    private PlayerStatComponent _playerStat => _motor.PlayerStat;

    // 상태
    private bool _dead;
    private bool _attacking;
    private float _rangedCd;
    private float _meleeGapCd;
    private float _eruptCd = 3f;        // 분출 쿨(초기 약간 지연 = 교전 직후 바로 안 나옴)
    private float _diveCd = 6f;         // 공중 다이브 쿨(초기 지연)
    private bool _diving;               // 다이브 중(에이전트 끈 상태) - 포효/사망 중단 시 복구 필요
    private bool _healed;               // 페이즈3 회복 1회 사용 여부
    private bool _lockScale;            // 회복 비행 중 스케일 고정 활성(비행 클립 루트 스케일 커브의 둥둥 차단)
    private Vector3 _healLockScale = Vector3.one;   // 고정할 기준 스케일(회복 진입 시점 캡처)
    private float[] _atkCd;
    private bool[] _roared;
    private bool _engaged;              // 교전 시작(보스바 1회 표시)
    private float _enrageCd = 1f;       // 누적 공격쿨 배수(<1 = 빨라짐)
    private float _enrageSpeed = 1f;    // 누적 이속 배수
    private static readonly float[] RoarThresholds = { 0.66f, 0.33f };

    private void Awake()
    {
        // 컴포넌트 캐싱 / applyRootMotion=false / updateRotation=false / Speed 해시 = 전부 모터가 처리
        _motor = new BossMotor(this, speedParam);

        _atkCd = new float[MeleeAttacks.Length];
        _roared = new bool[RoarThresholds.Length];

        _motor.ApplyData(data);   // SO -> 컴포넌트 동기화 (EnemyBrain 역할 대체)
    }

    private void Start()
    {
        _healLockScale = transform.localScale;   // 평상(rest) 스케일 캡처 = 회복 비행 중 고정 기준(애니 첫 평가 전이라 순수 값)
        AcquirePlayer();
        if (_health != null) _health.OnDeath += HandleDeath;
        _feedback?.PlaySpawn();
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnDeath -= HandleDeath;
        if (_engaged) BattleBgm.End();   // [07-29] 원거리슬립 despawn 시 전투 브금 원복(리쉬 폐지)
    }

    private void HandleDeath()
    {
        _dead = true;
        BattleBgm.End();   // 처치 → 전투 브금 종료, 기존 BGM 재개
        if (_health != null) _health.Invulnerable = false;   // 회복 중 사망 시 무적 잔존 방지
        _lockScale = false;                                  // 회복 중 사망 시 스케일 고정 해제(사망 애니 정상 재생)
        EndDiveCleanup();   // 다이브/포효띄움 중 사망 시 공중에 멈추지 않게 지면 복구(에이전트 꺼져 있으면 재활성+스냅, 아니면 no-op)
        StopAllCoroutines();
        StopMove();
    }

    private void AcquirePlayer() => _motor.AcquirePlayer();

    private void Update()
    {
        if (_dead) return;
        if (_player == null) AcquirePlayer();

        _motor.TickSpeedParam();   // 다이브 중 에이전트 비활성이면 조용히 스킵

        TickCooldowns();
        CheckRoarPhase();
        if (_attacking) return;

        Transform target = ResolveTarget();
        if (target != null && !_engaged)
        {
            _engaged = true;
            BossHealthBarUI.Show(_health, data != null ? Loc.Get(data.enemyName) : Loc.Get("보스"), Loc.Get(bossSubtitle));
            _feedback?.PlayDetect();   // SO detectSound 1회 재생(다른 적과 동일 훅)
            BattleBgm.Begin(SfxId.WyvernBattleBgm);   // 교전 시작 → 전투 브금(기존 BGM 일시정지)
        }
        if (target == null) { StopMove(); return; }
        if (data == null) return;

        float dist = PlanarDistance(target.position);
        float meleeMax = data.attackRange * MeleeMaxReachMul;

        // 분출(라이트필러) - 쿨 차고 사거리 안이면 우선. 페이즈1=1발 / 페이즈2+=주변 여기저기 팡팡팡(피하기 어렵게)
        if (_eruptCd <= 0f && eruptionVfx != null && dist <= eruptionRange)
        {
            int roars = RoarsDone();
            int count = roars == 0 ? 1 : eruptionBarrageCount * roars;       // 페2=base 발수, 페3=2배 (base 9 -> 페2 9 / 페3 18)
            float gap = roars >= 2 ? eruptionBarrageGap * 0.6f : eruptionBarrageGap;   // 페3 더 빠르게(파파방)
            StartCoroutine(EruptionBarrage(count, gap));
            return;
        }

        // 공중 다이브 강타 - 쿨 차고 사거리 안이면
        if (_diveCd <= 0f && slamVfx != null && dist <= diveRange)
        {
            StartCoroutine(DiveSlam());
            return;
        }

        if (dist <= meleeMax)
        {
            if (_meleeGapCd <= 0f && TrySelectMelee(dist, out int idx))
                StartCoroutine(MeleeAttack(idx));
            else
                StopMove();   // 사거리 안인데 쿨 -> 제자리 대기(견제 호흡)
        }
        else if (fireballPrefab != null && dist <= rangedRange)
        {
            if (_rangedCd <= 0f) StartCoroutine(SpitFireball());
            else Chase(target.position);
        }
        else
        {
            Chase(target.position);
        }
    }

    private void TickCooldowns()
    {
        float dt = Time.deltaTime;
        if (_rangedCd > 0f) _rangedCd -= dt;
        if (_meleeGapCd > 0f) _meleeGapCd -= dt;
        if (_eruptCd > 0f) _eruptCd -= dt;
        if (_diveCd > 0f) _diveCd -= dt;
        for (int i = 0; i < _atkCd.Length; i++)
            if (_atkCd[i] > 0f) _atkCd[i] -= dt;
    }

    // 사거리/각도/쿨 충족하는 근접 공격을 가중 랜덤으로 선택
    private bool TrySelectMelee(float dist, out int chosen)
    {
        chosen = -1;
        float total = 0f;
        for (int i = 0; i < MeleeAttacks.Length; i++)
        {
            var a = MeleeAttacks[i];
            if (_atkCd[i] > 0f) continue;
            if (dist > data.attackRange * a.reachMul) continue;
            if (!PlayerInArc(data.attackRange * a.reachMul + 0.5f, a.halfAngle)) continue;
            total += a.weight;
        }
        if (total <= 0f) return false;

        float r = Random.value * total;
        for (int i = 0; i < MeleeAttacks.Length; i++)
        {
            var a = MeleeAttacks[i];
            if (_atkCd[i] > 0f) continue;
            if (dist > data.attackRange * a.reachMul) continue;
            if (!PlayerInArc(data.attackRange * a.reachMul + 0.5f, a.halfAngle)) continue;
            r -= a.weight;
            if (r <= 0f) { chosen = i; return true; }
        }
        return false;
    }

    private Transform ResolveTarget()
    {
        if (_player == null) return null;
        if (_playerStat != null && (_playerStat.IsDead || _playerStat.IsInBase)) return null;
        float aggro = data != null ? Mathf.Max(data.visionRange, rangedRange) : 25f;
        if (PlanarDistance(_player.position) > aggro) return null;
        return _player;
    }

    // 이동/회전/재생 = 전부 모터 위임 (보스 3종 공통)
    private void Chase(Vector3 dest) => _motor.Chase(dest);
    private void StopMove() => _motor.StopMove();
    private bool AgentReady() => _motor.AgentReady();
    private void PlayState(string stateName) => _motor.PlayState(stateName);

    // 근접 공격: 방향 커밋(정지+스냅) -> 상태 재생 -> windup 후 호(arc) 판정 데미지 -> recover -> 쿨 등록
    private IEnumerator MeleeAttack(int idx)
    {
        var a = MeleeAttacks[idx];
        _attacking = true;
        StopMove();
        if (_player != null) FaceInstant(_player.position);   // 시작 시 방향 커밋(이후 추적 정지 = 회피 가능)
        PlayState(a.state);
        if (idx == 1) _feedback?.PlaySound(stingerSound);
        else if (biteSound != null) _feedback?.PlaySound(biteSound);
        else _feedback?.PlayAttack();   // 폴백: 기존 data.attackSound

        yield return new WaitForSeconds(a.windup);

        if (!_dead && _playerStat != null &&
            PlayerInArc(data.attackRange * a.reachMul + 1f, a.halfAngle))
            _playerStat.TakeDamage(data.attackDamage * a.dmgMul, transform.position);

        yield return new WaitForSeconds(a.recover);

        _attacking = false;
        _atkCd[idx] = a.cd * _enrageCd;
        _meleeGapCd = MeleeGap * _enrageCd;
        if (AgentReady()) _agent.isStopped = false;
    }

    // 원거리 파이어볼: 방향 커밋 -> Fireball -> windup 후 발사체 -> recover -> 쿨
    private IEnumerator SpitFireball()
    {
        _attacking = true;
        StopMove();
        if (_player != null) FaceInstant(_player.position);
        PlayState(fireballState);
        _feedback?.PlaySound(fireballSound);

        yield return new WaitForSeconds(fireballWindup);

        // 페이즈3 = 연속 팡팡팡, 그 전엔 1발. 유도는 SpawnFireball서 페이즈2+면 켬.
        int shots = RoarsDone() >= 2 ? Mathf.Max(1, fireballBurstCount) : 1;
        for (int i = 0; i < shots; i++)
        {
            if (_dead) break;
            if (i > 0 && _player != null) FaceInstant(_player.position);   // 연사 중 재조준
            SpawnFireball();
            if (i < shots - 1) yield return new WaitForSeconds(fireballBurstGap);
        }

        yield return new WaitForSeconds(fireballRecover);

        _attacking = false;
        _rangedCd = rangedCooldown * _enrageCd;
        if (AgentReady()) _agent.isStopped = false;
    }

    // 분출: 페이즈1 = 플레이어 자리 1발. 페이즈2+ = 플레이어 자리 + 주변 여기저기 순차 팡팡팡(피하기 어렵게).
    // 각 발은 SingleEruption(텔레그래프->팡)을 독립 코루틴으로 스태거 발생.
    private IEnumerator EruptionBarrage(int count, float gap)
    {
        _attacking = true;
        StopMove();
        if (_player != null) FaceInstant(_player.position);
        PlayState(eruptionState);
        _feedback?.PlaySound(eruptionChargeSound);

        yield return new WaitForSeconds(eruptionWindup);

        for (int i = 0; i < count; i++)
        {
            Vector3 spot = (i == 0 && _player != null) ? _player.position : ChooseEruptSpot();
            spot.y = transform.position.y;   // 지면 높이(보스 발 기준)
            StartCoroutine(SingleEruption(spot));
            if (i < count - 1) yield return new WaitForSeconds(gap);
        }

        // 마지막 발이 터지고 끝날 때까지 대기
        yield return new WaitForSeconds(eruptionTelegraph + eruptionRecover);
        _attacking = false;
        _eruptCd = eruptionCooldown * _enrageCd;
        if (AgentReady()) _agent.isStopped = false;
    }

    // 한 발: 범위 텔레그래프 -> 텔레그래프 시간 후 라이트필러 "팡" + 그 자리 반경 데미지.
    private IEnumerator SingleEruption(Vector3 spot)
    {
        SpawnTelegraph(spot, eruptionRadius, eruptionTelegraph);
        yield return new WaitForSeconds(eruptionTelegraph);
        if (_dead) yield break;
        _feedback?.PlaySound(eruptionBurstSound);
        if (eruptionVfx != null) Instantiate(eruptionVfx, spot, Quaternion.identity);
        if (_playerStat != null && !_playerStat.IsDead && _player != null)
        {
            Vector3 to = _player.position - spot; to.y = 0f;
            if (to.sqrMagnitude <= eruptionRadius * eruptionRadius)
                _playerStat.TakeDamage(data != null ? data.attackDamage * eruptionDmgMul : 20f, spot);
        }
    }

    // 주변 폭발 위치 = 플레이어 현재 위치 기준 랜덤(흩뿌려서 맞추기 어렵게). 호출 시점 위치라 약간 추적됨.
    private Vector3 ChooseEruptSpot()
    {
        Vector3 baseP = _player != null ? _player.position : transform.position;
        Vector2 r = Random.insideUnitCircle * eruptionSpread;
        return baseP + new Vector3(r.x, 0f, r.y);
    }

    // 완료한 포효 수 = 페이즈 판단(0=페1, 1=페2, 2=페3). _roared 기반.
    private int RoarsDone()
    {
        int n = 0;
        for (int i = 0; i < _roared.Length; i++) if (_roared[i]) n++;
        return n;
    }

    // 공중 다이브 강타: 상승 -> 정점 체공(착지 지점 범위 텔레그래프) -> 급강하 -> 착지 임팩트 + 범위 데미지.
    // 수직 이동 위해 NavMeshAgent 잠깐 끔(베이크/BT 무관, 런타임 제어만) -> 착지 시 Warp 로 네비메시 재동기화.
    private IEnumerator DiveSlam()
    {
        _attacking = true;
        _diving = true;
        StopMove();
        if (_player != null) FaceInstant(_player.position);
        PlayState(diveTakeoffState);   // 이륙
        _feedback?.PlaySound(diveWindupSound);

        Vector3 start = transform.position;
        if (_agent != null && _agent.enabled) { _agent.isStopped = true; _agent.ResetPath(); _agent.enabled = false; }

        // 0) 제자리 준비 모션(이륙 애니 앞부분 = 웅크림) - 날기 전 텔레그래프
        yield return new WaitForSeconds(divePrepTime);

        // 1) 빠르게 상승(촥 - 높이↑ 시간↓ = 완전 쫙 위로)
        Vector3 apexUp = start + Vector3.up * diveHeight;
        yield return MoveBetween(start, apexUp, diveRiseTime);

        // 2) 착지 지점(플레이어) 스냅샷 + 지면 범위 텔레그래프 + 그 '상공'으로 이동(체공=조준)
        PlayState(diveHoverState);   // 제자리 비행(날갯짓)
        Vector3 land = _player != null ? _player.position : start;
        if (NavMesh.SamplePosition(land, out var navHit, 5f, NavMesh.AllAreas)) land = navHit.position;
        else land = new Vector3(land.x, start.y, land.z);
        Vector3 over = new Vector3(land.x, land.y + diveHeight, land.z);   // 착지점 바로 위
        SpawnTelegraph(land, diveRadius, diveHoverTime + diveDropTime);
        FaceInstant(land);
        yield return MoveBetween(apexUp, over, diveHoverTime);

        // 3) 수직 급강하 (착지점 상공 -> 착지점) = 표시된 범위에 내려찍기
        PlayState(diveFallState);   // 낙하
        yield return MoveBetween(over, land, diveDropTime);
        transform.position = land;

        // 4) 착지 임팩트 + 범위 데미지
        PlayState(diveLandState);   // 착지
        _feedback?.PlaySound(diveImpactSound);
        if (slamVfx != null) Instantiate(slamVfx, land, Quaternion.identity);
        if (!_dead && _playerStat != null && !_playerStat.IsDead && _player != null)
        {
            Vector3 to = _player.position - land; to.y = 0f;
            if (to.sqrMagnitude <= diveRadius * diveRadius)
            {
                float dmg = data != null ? data.attackDamage * diveDmgMul : 30f;
                _playerStat.TakeDamage(dmg, land);
            }
        }

        // 5) 에이전트 재활성 + Warp 재동기화
        if (_agent != null && !_agent.enabled) { _agent.enabled = true; _agent.Warp(land); }
        _diving = false;

        yield return new WaitForSeconds(diveRecover);
        _attacking = false;
        _diveCd = diveCooldown * _enrageCd;
        if (AgentReady()) _agent.isStopped = false;
    }

    // from -> to 로 transform 부드럽게 이동(다이브 상승/급강하).
    private IEnumerator MoveBetween(Vector3 from, Vector3 to, float time)
    {
        if (time <= 0f) { transform.position = to; yield break; }
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / time));
            transform.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        transform.position = to;
    }

    // 지면 범위 텔레그래프 링: spot 에 radius 크기로 띄우고 life 후 제거.
    private void SpawnTelegraph(Vector3 spot, float radius, float life)
    {
        if (telegraphVfx == null) return;
        var tg = Instantiate(telegraphVfx, spot + Vector3.up * 0.1f, Quaternion.identity);   // 살짝 띄움(z-fighting 방지, 셰이더가 항상 위에 그림)
        float d = Mathf.Max(0.1f, radius * 2f * telegraphScaleMul);   // 파티클 링 보정(telegraphScaleMul)으로 보이는 원을 딜 반경에 맞춤
        tg.transform.localScale = new Vector3(d, d, d);
        Destroy(tg, life + 0.1f);
    }

    // 다이브 중단(포효/사망) 시 공중 보스를 지면으로 내려놓고 에이전트 재활성(안 하면 공중에 멈춤).
    private void EndDiveCleanup()
    {
        _diving = false;
        if (_agent == null || _agent.enabled) return;
        Vector3 p = transform.position;
        if (NavMesh.SamplePosition(p, out var hit, 10f, NavMesh.AllAreas)) p = hit.position;
        transform.position = p;
        _agent.enabled = true;
        _agent.Warp(p);
    }

    // HP 임계값(66%/33%) 도달 시 1회씩 포효 -> 디버프(화면 어둠+시간 가속) + 광폭화
    private void CheckRoarPhase()
    {
        if (_health == null || _health.maxHP <= 0f) return;
        float frac = _health.currentHP / _health.maxHP;
        for (int i = 0; i < RoarThresholds.Length; i++)
        {
            if (_roared[i] || frac > RoarThresholds[i]) continue;
            _roared[i] = true;
            if (_diving) EndDiveCleanup();   // 다이브 중이면 공중 보스 지면 복구(에이전트 재활성) 후 끊음
            StopAllCoroutines();   // 진행 중 공격 끊고 포효 우선
            _attacking = false;
            StartCoroutine(RoarPhase(i));
            return;
        }
    }

    private IEnumerator RoarPhase(int idx)
    {
        _attacking = true;
        StopMove();
        if (_player != null) FaceInstant(_player.position);

        // 마지막 포효(페이즈3 진입) = 포효 애니/디버프 생략하고 바로 회복 비행으로(날기전 대기 TakeOff -> 날기 FlyHover). 광폭화는 유지.
        if (idx >= RoarThresholds.Length - 1 && !_healed)
        {
            _healed = true;
            Enrage();
            yield return HealPhase();
            yield break;   // HealPhase가 _attacking/agent 정리
        }

        // 포효 클립이 비행 포즈라 지상서 꼬리가 땅에 박힘 -> 포효 동안만 살짝 띄움(에이전트 끄고 transform 이동, 다이브/회복과 동일).
        Vector3 ground = transform.position;
        _lockScale = true;   // 띄운 비행 포즈 동안 몸 크기 둥둥(루트 스케일 커브) 차단
        if (_agent != null && _agent.enabled) { _agent.isStopped = true; _agent.ResetPath(); _agent.enabled = false; }

        PlayState(roarState);
        _feedback?.PlaySound(phaseRoarSound);
        yield return MoveBetween(ground, ground + Vector3.up * roarLiftHeight, roarLiftTime);   // 살짝 떠오름

        yield return new WaitForSeconds(roarBuildup);
        BossRoarDebuff.Trigger(debuffDuration, debuffDrainMult, debuffDarkness, debuffVignetteStrength, debuffVignetteFalloff);   // 화면 어둠 + 시간 가속
        Enrage();
        yield return new WaitForSeconds(roarRecover);

        yield return MoveBetween(transform.position, ground, roarLiftTime);   // 내려옴
        transform.position = ground;
        if (_agent != null && !_agent.enabled) { _agent.enabled = true; _agent.Warp(ground); }
        _lockScale = false;

        _attacking = false;
        if (AgentReady()) _agent.isStopped = false;
    }

    // 페이즈3 진입 시 1회 체력 회복: 이륙(TakeOff)으로 떠올라 -> 체공(FlyHover) 날갯짓하며 회복 -> 착지.
    // 무적 + HP 파바바박 틱(한 칸씩 점프). 띄우기는 에이전트 끄고 transform 직접 이동(다이브와 동일 = 떨림 없음).
    // 비행 클립 루트 스케일 커브로 몸이 둥둥거리던 것 -> _lockScale 로 LateUpdate에서 스케일 고정해 차단(날갯짓은 살림).
    private IEnumerator HealPhase()
    {
        _attacking = true;
        StopMove();
        if (_player != null) FaceInstant(_player.position);
        _feedback?.PlaySound(healRoarSound);
        if (_health != null) _health.Invulnerable = true;

        Vector3 ground = transform.position;
        _lockScale = true;                        // LateUpdate에서 몸 크기 둥둥(루트 스케일 커브) 차단. 기준은 Start서 캡처한 평상 스케일.
        if (_agent != null && _agent.enabled) { _agent.isStopped = true; _agent.ResetPath(); _agent.enabled = false; }
        GameObject vfx = (healVfx != null) ? Instantiate(healVfx, transform.position, transform.rotation) : null;

        // 1) 이륙(상승) - 에이전트 끄고 transform 직접 이동(baseOffset 안 씀 = 에이전트랑 Y 안 싸움 = 안 떨림)
        Vector3 up = ground + Vector3.up * healRiseHeight;
        PlayState(diveTakeoffState);
        yield return MoveBetween(ground, up, healRiseTime);

        // 2) 체공 + 회복 - 호버(날갯짓) 유지. 몸 크기는 LateUpdate 고정이라 안 둥둥. HP는 틱마다 한 칸씩 점프 = 파바바박.
        PlayState(diveHoverState);
        float start = _health != null ? _health.currentHP : 0f;
        float target = _health != null ? Mathf.Min(_health.maxHP, start + _health.maxHP * healPercent) : 0f;
        int ticks = Mathf.Max(1, healTicks);
        float perTick = (target - start) / ticks;
        float tickGap = healDuration / ticks;
        for (int i = 0; i < ticks && !_dead; i++)
        {
            if (_health != null) _health.currentHP = Mathf.Min(target, _health.currentHP + perTick);   // 한 칸 차오름(파박)
            float e = 0f;
            while (e < tickGap) { e += Time.deltaTime; yield return null; }
        }
        if (_health != null) _health.currentHP = target;

        // 3) 하강 - transform 직접 이동
        yield return MoveBetween(transform.position, ground, healRiseTime);
        transform.position = ground;

        // 4) 착지 + 에이전트 복구(Warp 재동기화) -> 평상(Landing 상태가 Idle 자동 복귀)
        PlayState(diveLandState);
        if (_agent != null && !_agent.enabled) { _agent.enabled = true; _agent.Warp(ground); }
        _lockScale = false;   // 착지 = 스케일 고정 해제(평상 애니 복귀, 평상 스케일 = 고정값과 동일)
        if (_health != null) _health.Invulnerable = false;
        if (vfx != null) Destroy(vfx);

        _attacking = false;
        if (AgentReady()) _agent.isStopped = false;
    }

    // 포효마다 공격/이동이 빨라짐(누적)
    private void Enrage()
    {
        _enrageCd *= enrageCdMul;
        _enrageSpeed *= enrageSpeedMul;
        if (_agent != null && data != null) _agent.speed = data.moveSpeed * _enrageSpeed;
    }

    private void SpawnFireball()
    {
        if (fireballPrefab == null || _player == null) return;
        Vector3 origin = transform.TransformPoint(fireOffset);
        Vector3 aim = (_player.position + Vector3.up) - origin;
        var go = Instantiate(fireballPrefab, origin, Quaternion.LookRotation(aim.sqrMagnitude > 0.0001f ? aim : transform.forward));
        var fb = go.GetComponent<WyvernFireball>();
        float dmg = data != null ? data.attackDamage : 20f;
        if (fb != null)
        {
            fb.Launch(aim, dmg, _player);
            if (RoarsDone() >= 1) fb.SetHoming(true);   // 페이즈2+ = 유도탄
        }
    }

    private bool PlayerInArc(float range, float halfAngleDeg) => _motor.PlayerInArc(range, halfAngleDeg);
    private void FaceInstant(Vector3 pos) => _motor.FaceInstant(pos);
    private float PlanarDistance(Vector3 worldPos) => _motor.PlanarDistance(worldPos);

    private void LateUpdate()
    {
        // [와이번 전용] 회복 비행 중 몸 크기 둥둥(비행 클립 루트 스케일 커브) 차단.
        // 애니메이터가 transform 쓴 뒤(LateUpdate)라 무조건 이김.
        // applyRootMotion=false는 위치/회전 커브만 제거하고 스케일은 안 막아서 클론 localScale이 펄스하던 것.
        if (_lockScale && !_dead) transform.localScale = _healLockScale;

        // 평소엔 이동방향/타깃을 향함. 공격 중엔 회전 정지(방향 커밋 = 플레이어가 옆/뒤로 dash 회피 가능).
        _motor.TickFacing(_dead || _attacking, ResolveTarget(), data);
    }
}
