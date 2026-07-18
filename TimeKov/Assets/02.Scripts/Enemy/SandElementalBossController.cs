using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 모래정령(사막 보스) - 전용 상태머신.
// 와이번/얼음정령과 같은 방식(BT/EnemyBrain 미사용)이며 글루는 BossMotor/BossLeash 가 대신한다.
//
// [정체성] 와이번 = 근접형 / 얼음정령 = 원거리 캐스터형.
//   모래정령 = "가둔다 -> 받아친다 -> 짓이긴다" = 근접 위치지배/카운터형.
//   거리를 좁혀 감금(모래관)/늪으로 그 자리에 묶고, 가드 카운터로 응징하며, 다이브 강타로 마무리한다.
//   - 감금(Coffin) = 시그니처. 짧게 고정(1.2s) 후 붕괴 폭발. 고정 풀린 뒤 도망칠 여지가 있다.
//   - 늪(Quicksand) = 지면 장판(지속 피해). "그 자리에 있지 마라"를 강요한다.
//   - 가드/패리(Guard) = 붙으면 무적 후 반격. "함부로 붙지 마라".
//   - 다이브 강타(Dive) = Sand_Smash 내장 예고 후 수직 내려찍기(와이번 다이브 이식).
//
// [페이즈] 포효(66%/33%) 기준 3단. RoarsDone() 방식.
//   P1: 근접 + 파도 + 가시 + 감금 / P2: + 늪 + 가드 + 다이브 / P3: + 궁극(피라미드), 광폭화
//
// [애니] SandElementalAnimatorBuilder 로 굽는 전용 컨트롤러. 상태 이름으로 CrossFade.
//   모래정령은 다리뼈가 없는 부유(호버)형이라 Walk/Run 은 발디딤이 아닌 몸통 스웨이 = 제자리 재생.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class SandElementalBossController : MonoBehaviour, IEnemyDataSource
{
    [Header("데이터 (HP/속도/근접공격 기본 수치는 SO에서 튜닝)")]
    [SerializeField] private MeleeEnemyData data;
    public MeleeEnemyData Data => data;   // 도감 등 외부서 보스 스탯 조회용(보스는 EnemyBrain 미사용)
    [SerializeField] private string bossSubtitle = "시간을 삼키는 사막의 지배자";

    [Header("모래 파도 (SandWave - 전방 부채꼴 스윕. 중거리 견제)")]
    [SerializeField] private GameObject waveVfx;
    [SerializeField] private float waveRange = 9f;
    [SerializeField] private float waveCooldown = 4.5f;
    [SerializeField] private float waveWindup = 0.7f;       // Cast1 시전
    [SerializeField] private float waveRecover = 0.6f;
    [SerializeField] private float waveHalfAngle = 40f;     // 정면 부채꼴 반각
    [SerializeField] private float waveDmgMul = 1.1f;

    [Header("모래 회오리 (Sand tornado - 플레이어 주변에 세워지는 지속 회오리, 닿으면 거슬리는 틱뎀)")]
    [Tooltip("세로 회오리 기둥(Sand tornado loop). 사방에 여러 개 세워 지형을 막는다.")]
    [SerializeField] private GameObject tornadoVfx;
    [SerializeField] private float tornadoRange = 18f;
    [SerializeField] private float tornadoCooldown = 11f;
    [SerializeField] private float tornadoWindup = 0.9f;    // 시전
    [SerializeField] private float tornadoRecover = 0.6f;
    [SerializeField] private float tornadoDmgMul = 0.25f;   // 틱당(저위협 거슬림용)
    [Tooltip("P1 회오리 수. 페이즈마다 아래 값만큼 증가")]
    [SerializeField] private int tornadoBase = 3;
    [SerializeField] private int tornadoPerPhase = 1;       // P1=3 / P2=4 / P3=5
    [Tooltip("플레이어 주변 흩뿌리는 반경(사방).")]
    [SerializeField] private float tornadoSpread = 6f;
    [Tooltip("각 회오리 지속 시간(초). 세워진 채 유지되며 지형을 막는다.")]
    [SerializeField] private float tornadoDuration = 10f;
    [Tooltip("닿음 판정 반경.")]
    [SerializeField] private float tornadoRadius = 3f;
    [Tooltip("데미지 틱 간격(초).")]
    [SerializeField] private float tornadoTick = 0.5f;

    [Header("모래 늪 (QuickSand - 지면 장판 지속 피해)")]
    [SerializeField] private GameObject quicksandVfx;
    [SerializeField] private float quicksandRange = 13f;
    [SerializeField] private float quicksandCooldown = 9f;
    [SerializeField] private float quicksandWindup = 0.8f;  // Cast2(지면 지목)
    [SerializeField] private float quicksandRecover = 0.7f;
    [SerializeField] private float quicksandRadius = 5f;
    [SerializeField] private float quicksandDuration = 4f;  // 늪 지속 시간
    [SerializeField] private float quicksandTick = 0.5f;    // 데미지 틱 간격
    [SerializeField] private float quicksandDmgMul = 0.4f;  // 틱당(장판이라 낮게)

    [Header("모래 감옥 (Sand_Coffin - 시그니처 감금)")]
    [SerializeField] private GameObject coffinVfx;
    [SerializeField] private float coffinRange = 10f;
    [SerializeField] private float coffinCooldown = 13f;
    [SerializeField] private float coffinWindup = 0.6f;     // Cast2(지목)
    [Tooltip("완전 고정 시간(짧게). 이 뒤엔 풀려서 폭발 전에 도망칠 수 있다.")]
    [SerializeField] private float coffinLockTime = 1.2f;
    [Tooltip("모래관 붕괴(폭발) 타이밍. Sand_Coffin VFX 가 2.6s에 터진다.")]
    [SerializeField] private float coffinBurstTime = 2.6f;
    [SerializeField] private float coffinRadius = 2.5f;
    [SerializeField] private float coffinDmgMul = 1.8f;     // 갇힌 채 맞으면 아프게(도망치면 회피)
    [SerializeField] private float coffinRecover = 0.6f;

    [Header("다이브 강타 (Sand_Smash - 수직 내려찍기. 와이번 이식)")]
    [Tooltip("착지 임팩트 = Sand_Smash. 내장 예고(약 1.6s) 타이밍에 맞춰 강하한다.")]
    [SerializeField] private GameObject slamVfx;
    [SerializeField] private float diveRange = 16f;
    [SerializeField] private float diveCooldown = 9f;
    [SerializeField] private float divePrepTime = 0.4f;     // 이륙 전 웅크림(예고)
    [SerializeField] private float diveRiseTime = 0.35f;    // 상승
    [SerializeField] private float diveHoverTime = 1.0f;    // 체공(조준). Sand_Smash 예고와 정렬
    [SerializeField] private float diveDropTime = 0.6f;     // 급강하 -> hover+drop=1.6s = 예고 길이
    [SerializeField] private float diveHeight = 11f;
    [SerializeField] private float diveRecover = 0.7f;
    [SerializeField] private float diveRadius = 8f;         // Sand_Smash 데칼 반경 약 8m
    [SerializeField] private float diveDmgMul = 1.5f;

    [Header("궁극 (Cast3 + Pyramid_Explosion - 페이즈3 전용)")]
    [SerializeField] private GameObject ultVfx;
    [SerializeField] private float ultRange = 16f;
    [SerializeField] private float ultCooldown = 13f;
    [SerializeField] private float ultWindup = 1.4f;        // Cast3 = 긴 채널(예고)
    [SerializeField] private float ultTelegraph = 1.0f;
    [SerializeField] private float ultRecover = 1.2f;
    [SerializeField] private float ultRadius = 9f;
    [SerializeField] private float ultDmgMul = 2.6f;

    [Header("가드 반격 (Parry + Sand_shield - 근접 압박 시 무적 후 광역 반격)")]
    [SerializeField] private GameObject guardVfx;           // Sand_shield(가드 동안 몸에 두른다)
    [SerializeField] private float guardRange = 4f;
    [SerializeField] private float guardCooldown = 7f;
    [SerializeField] private float guardDuration = 1.0f;    // 무적 시간(Parry 클립)
    [SerializeField] private string guardCounterState = "Attack2";   // 가드 후 반격(광역 스윙)
    [SerializeField] private float guardCounterWindup = 0.5f;
    [SerializeField] private float guardCounterDmgMul = 1.5f;

    [Header("범위 텔레그래프 (늪/궁극 발동 전 지면 표시)")]
    [SerializeField] private GameObject telegraphVfx;       // Wyvern_Telegraph 복제 + 황토색
    [Tooltip("텔레그래프 원의 보이는 크기 배율. 파티클 링이라 transform 스케일 대비 작게 보여서 보정용.")]
    [SerializeField] private float telegraphScaleMul = 2.5f;

    [Header("포효 페이즈 (HP 66%/33%)")]
    [SerializeField] private string roarState = "Roar";
    [SerializeField] private float roarBuildup = 0.6f;
    [SerializeField] private float roarRecover = 0.9f;
    [SerializeField] private float debuffDuration = 6f;
    [SerializeField] private float debuffDrainMult = 2.5f;
    [Range(0f, 0.8f)] [SerializeField] private float debuffDarkness = 0.45f;
    [Range(0f, 1f)] [SerializeField] private float debuffVignetteStrength = 0.9f;
    [Range(0.05f, 0.8f)] [SerializeField] private float debuffVignetteFalloff = 0.55f;
    [SerializeField] private float enrageCdMul = 0.82f;     // 포효마다 공격 쿨 x (누적)
    [SerializeField] private float enrageSpeedMul = 1.15f;  // 포효마다 이속 x (누적)

    [Header("전조/임팩트 VFX (공격 3박자: 응축 -> 발동 -> 타격)")]
    [Tooltip("손 응축(근접/파도/감금 전조). 비우면 안 나옴.")]
    [SerializeField] private GameObject chargeVfxHand;
    [Tooltip("몸 응축(가시/늪/다이브/궁극 전조). 큰 시전 예고.")]
    [SerializeField] private GameObject chargeVfxBody;
    [Tooltip("근접 타격 임팩트(모래 튐). 맞는 순간 1회.")]
    [SerializeField] private GameObject impactVfxMelee;
    [Tooltip("대형 착탄 임팩트(감금 붕괴/궁극). 1회성이라 무거워도 됨.")]
    [SerializeField] private GameObject impactVfxHeavy;
    [Tooltip("전조가 붙을 손 본. 비우면 spawnOffset 위치(로컬)에 붙는다.")]
    [SerializeField] private Transform chargeAnchor;
    [Tooltip("손 본이 없을 때 전조가 붙을 로컬 위치.")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 2f, 1f);
    [Tooltip("임팩트가 뜰 플레이어 몸 높이 보정(발밑 pivot 이면 위로).")]
    [SerializeField] private float impactHeight = 1.0f;
    [Tooltip("근접 임팩트 크기 배율.")]
    [SerializeField] private float meleeImpactScale = 0.8f;

    [Header("애니메이터 (전용 컨트롤러 상태명)")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string waveState = "CastWave";
    [SerializeField] private string tornadoState = "Spike";   // 애니 상태는 Cast5(SpikeStart/Spike) 재사용
    [SerializeField] private string coffinState = "CastCoffin";
    [SerializeField] private string quicksandState = "CastCoffin";   // 지면 지목 = 감금과 같은 시전 모션 공유
    [SerializeField] private string ultState = "CastBig";
    [SerializeField] private string diveState = "Fly";               // 부유형이라 이륙/체공/낙하 전부 Fly
    [SerializeField] private string guardState = "Guard";            // Parry 클립

    [Header("패턴 선택 (가중 랜덤 - 조건 맞는 것 중 확률로 뽑음)")]
    [Tooltip("각 공격의 선택 가중치. 클수록 자주 뽑힌다. 개별 쿨과 별개.")]
    [SerializeField] private float wMelee = 1.6f;
    [SerializeField] private float wWave = 1.2f;
    [SerializeField] private float wTornado = 1.1f;
    [SerializeField] private float wCoffin = 1.4f;      // 시그니처(쿨 길게로 리듬 제어)
    [SerializeField] private float wQuicksand = 1.0f;
    [SerializeField] private float wGuard = 1.2f;       // 카운터 정체성 -> 자주 보이게
    [SerializeField] private float wDive = 1.1f;
    [SerializeField] private float wUlt = 1.2f;
    [Tooltip("직전에 쓴 패턴의 가중치 배율(연속 방지). 0=절대 연속 안 함, 1=페널티 없음.")]
    [Range(0f, 1f)] [SerializeField] private float repeatPenalty = 0.35f;

    [Header("리쉬 (이탈 리셋)")]
    [SerializeField] private float leashDistance = 45f;
    [SerializeField] private float leashResetTime = 4f;

    // 근접 공격 정의
    private struct AtkDef
    {
        public string state; public float reachMul, halfAngle, windup, recover, dmgMul, cd, weight;
    }
    private static readonly AtkDef[] MeleeAttacks =
    {
        // Attack1 = 근접 A(1.33s) / Attack2 = 근접 B(1.1s, 빠른 후속/광역)
        new AtkDef { state="Attack1", reachMul=1.0f, halfAngle=45f, windup=0.40f, recover=0.40f, dmgMul=1.0f, cd=1.5f, weight=3f },
        new AtkDef { state="Attack2", reachMul=1.2f, halfAngle=90f, windup=0.45f, recover=0.45f, dmgMul=1.2f, cd=2.5f, weight=2f },
    };
    private const float MeleeMaxReachMul = 1.2f;
    private const float MeleeGap = 0.3f;

    private static readonly float[] RoarThresholds = { 0.66f, 0.33f };

    // 패턴 종류(가중 랜덤 선택용). 조건 맞는 후보를 모아 확률로 뽑는다(우선순위 사다리 대신).
    private enum AtkType { None, Melee, Wave, Tornado, Coffin, Quicksand, Guard, Dive, Ult }
    private AtkType _lastAttack = AtkType.None;
    private readonly List<(AtkType type, float weight)> _cand = new List<(AtkType, float)>();

    // 공용 모터/리쉬 (보스 3종 공유)
    private BossMotor _motor;
    private BossLeash _leash;

    // 모터 위임(본문 가독성용 얇은 프로퍼티)
    private NavMeshAgent _agent => _motor.Agent;
    private EnemyHealth _health => _motor.Health;
    private EnemyFeedback _feedback => _motor.Feedback;
    private Transform _player => _motor.Player;
    private PlayerStatComponent _playerStat => _motor.PlayerStat;

    // 상태
    private bool _dead;
    private bool _attacking;
    private bool _engaged;
    private float _waveCd, _tornadoCd, _quicksandCd, _coffinCd, _diveCd, _ultCd, _guardCd, _meleeGapCd;
    private float[] _atkCd;
    private bool[] _roared;
    private float _enrageCd = 1f;
    private float _enrageSpeed = 1f;

    // 감금 락(플레이어 이동 잠금) - StopAllCoroutines 로 코루틴이 끊겨도 반드시 풀어야 한다.
    private bool _coffinLockActive;
    private PlayerMovementComponent _lockedMove;

    private void Awake()
    {
        _motor = new BossMotor(this, speedParam);
        _leash = new BossLeash(leashDistance, leashResetTime);
        _atkCd = new float[MeleeAttacks.Length];
        _roared = new bool[RoarThresholds.Length];
        _motor.ApplyData(data);
    }

    private void Start()
    {
        _leash.Capture(transform);
        _motor.AcquirePlayer();
        if (_health != null) _health.OnDeath += HandleDeath;
        _feedback?.PlaySpawn();
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnDeath -= HandleDeath;
        ReleaseCoffinLock();   // 파괴 시 플레이어 락 잔존 방지
    }

    private void OnDisable()
    {
        ReleaseCoffinLock();   // 비활성화(풀링/컬링 등)로 코루틴이 죽어도 플레이어 락은 반드시 푼다
    }

    private void HandleDeath()
    {
        _dead = true;
        if (_health != null) _health.Invulnerable = false;   // 가드 중 사망 시 무적 잔존 방지
        StopAllCoroutines();
        ReleaseCoffinLock();   // ★사망이 코루틴을 끊어도 감금 락은 반드시 푼다
        EndDiveCleanup();      // 다이브 중 사망 시 공중에 멈추지 않게 지면 복구
        _motor.StopMove();
    }

    private void Update()
    {
        if (_dead) return;
        if (_player == null) _motor.AcquirePlayer();

        if (_engaged && _player != null && _leash.Tick(_motor.PlanarDistance(_player.position)))
            ResetBoss();

        _motor.TickSpeedParam();
        TickCooldowns();
        CheckRoarPhase();
        if (_attacking) return;

        Transform target = ResolveTarget();
        if (target != null && !_engaged)
        {
            _engaged = true;
            BossHealthBarUI.Show(_health, data != null ? data.enemyName : "모래정령", bossSubtitle);
            _feedback?.PlayDetect();
        }
        if (target == null) { _motor.StopMove(); return; }
        if (data == null) return;

        float dist = _motor.PlanarDistance(target.position);
        int phase = RoarsDone();   // 0=P1 / 1=P2 / 2=P3
        float meleeMax = data.attackRange * MeleeMaxReachMul;

        // 조건(페이즈/쿨/거리) 맞는 공격을 전부 후보로 모은다.
        _cand.Clear();
        if (phase >= 2 && _ultCd <= 0f && ultVfx != null && dist <= ultRange)
            _cand.Add((AtkType.Ult, wUlt));
        if (phase >= 1 && _diveCd <= 0f && slamVfx != null && dist <= diveRange)
            _cand.Add((AtkType.Dive, wDive));
        if (phase >= 1 && _quicksandCd <= 0f && dist <= quicksandRange)
            _cand.Add((AtkType.Quicksand, wQuicksand));
        if (phase >= 1 && _guardCd <= 0f && dist <= guardRange)
            _cand.Add((AtkType.Guard, wGuard));
        if (_coffinCd <= 0f && dist <= coffinRange && dist > meleeMax * 0.5f)
            _cand.Add((AtkType.Coffin, wCoffin));   // 감금은 전 페이즈(시그니처 조기 등장)
        if (_tornadoCd <= 0f && dist <= tornadoRange)
            _cand.Add((AtkType.Tornado, wTornado));
        if (_waveCd <= 0f && dist <= waveRange && dist > meleeMax)
            _cand.Add((AtkType.Wave, wWave));   // 파도는 근접 사거리 밖에서만(붙으면 근접이 낫다)

        int meleeIdx = -1;
        bool canMelee = dist <= meleeMax && _meleeGapCd <= 0f && TrySelectMelee(dist, out meleeIdx);
        if (canMelee)
            _cand.Add((AtkType.Melee, wMelee));

        // 후보가 있으면 가중 랜덤(직전 패턴은 확률 낮춤)으로 하나 뽑아 발동.
        if (_cand.Count > 0)
        {
            AtkType pick = WeightedPick();
            _lastAttack = pick;
            switch (pick)
            {
                case AtkType.Ult:       StartCoroutine(Ultimate()); break;
                case AtkType.Dive:      StartCoroutine(DiveSlam()); break;
                case AtkType.Quicksand: StartCoroutine(QuicksandAttack()); break;
                case AtkType.Guard:     StartCoroutine(GuardCounter()); break;
                case AtkType.Coffin:    StartCoroutine(CoffinAttack()); break;
                case AtkType.Tornado:   StartCoroutine(TornadoAttack()); break;
                case AtkType.Wave:      StartCoroutine(WaveAttack()); break;
                case AtkType.Melee:     StartCoroutine(MeleeAttack(meleeIdx)); break;
            }
            return;
        }

        // 후보 없음(전부 쿨/사거리 밖): 근접 사거리면 제자리 대기, 아니면 추격.
        if (dist <= meleeMax) _motor.StopMove();
        else _motor.Chase(target.position);
    }

    // 후보 중 가중 랜덤 선택. 직전에 쓴 패턴은 repeatPenalty 배로 낮춰 연속을 막는다.
    private AtkType WeightedPick()
    {
        float total = 0f;
        foreach (var c in _cand)
            total += (c.type == _lastAttack) ? c.weight * repeatPenalty : c.weight;

        float r = Random.value * total;
        foreach (var c in _cand)
        {
            float w = (c.type == _lastAttack) ? c.weight * repeatPenalty : c.weight;
            r -= w;
            if (r <= 0f) return c.type;
        }
        return _cand[_cand.Count - 1].type;
    }

    private void TickCooldowns()
    {
        float dt = Time.deltaTime;
        if (_waveCd > 0f) _waveCd -= dt;
        if (_tornadoCd > 0f) _tornadoCd -= dt;
        if (_quicksandCd > 0f) _quicksandCd -= dt;
        if (_coffinCd > 0f) _coffinCd -= dt;
        if (_diveCd > 0f) _diveCd -= dt;
        if (_ultCd > 0f) _ultCd -= dt;
        if (_guardCd > 0f) _guardCd -= dt;
        if (_meleeGapCd > 0f) _meleeGapCd -= dt;
        for (int i = 0; i < _atkCd.Length; i++)
            if (_atkCd[i] > 0f) _atkCd[i] -= dt;
    }

    private Transform ResolveTarget()
    {
        if (_player == null) return null;
        if (_playerStat != null && (_playerStat.IsDead || _playerStat.IsInBase)) return null;
        float aggro = data != null ? Mathf.Max(data.visionRange, tornadoRange) : 25f;
        if (_motor.PlanarDistance(_player.position) > aggro) return null;
        return _player;
    }

    private int RoarsDone()
    {
        int n = 0;
        for (int i = 0; i < _roared.Length; i++) if (_roared[i]) n++;
        return n;
    }

    // 사거리/각도/쿨 충족하는 근접 공격을 가중 랜덤으로 선택
    private bool TrySelectMelee(float dist, out int chosen)
    {
        chosen = -1;
        float total = 0f;
        for (int i = 0; i < MeleeAttacks.Length; i++)
        {
            if (_atkCd[i] > 0f) continue;
            if (dist > data.attackRange * MeleeAttacks[i].reachMul) continue;
            total += MeleeAttacks[i].weight;
        }
        if (total <= 0f) return false;

        float r = Random.value * total;
        for (int i = 0; i < MeleeAttacks.Length; i++)
        {
            if (_atkCd[i] > 0f) continue;
            if (dist > data.attackRange * MeleeAttacks[i].reachMul) continue;
            r -= MeleeAttacks[i].weight;
            if (r <= 0f) { chosen = i; return true; }
        }
        return false;
    }

    // 근접: 방향 커밋(정지+스냅) -> 상태 재생 -> windup 후 호 판정 -> recover
    private IEnumerator MeleeAttack(int idx)
    {
        var a = MeleeAttacks[idx];
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(a.state);

        SpawnCharge(chargeVfxHand, ChargePos(), a.windup);
        yield return new WaitForSeconds(a.windup);
        if (!_dead && _motor.PlayerInArc(data.attackRange * a.reachMul, a.halfAngle))
        {
            DealDamage(data.attackDamage * a.dmgMul);
            SpawnImpact(impactVfxMelee, PlayerHitPos(), meleeImpactScale);
        }

        yield return new WaitForSeconds(a.recover);
        _atkCd[idx] = a.cd * _enrageCd;
        _meleeGapCd = MeleeGap;
        _attacking = false;
    }

    // 모래 파도: 전방 부채꼴 스윕. 중거리에서 라인 회피를 강요한다.
    private IEnumerator WaveAttack()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(waveState);

        SpawnCharge(chargeVfxHand, ChargePos(), waveWindup);
        yield return new WaitForSeconds(waveWindup);

        if (!_dead)
        {
            // 파도 VFX 를 보스 정면 지면에 스폰(부채꼴 콘이 앞을 향하도록 보스 회전 사용).
            if (waveVfx != null)
            {
                Vector3 front = GroundSpot(transform.position + transform.forward * (waveRange * 0.4f));
                var wv = Instantiate(waveVfx, front, transform.rotation);
                Destroy(wv, 2.5f);   // 루프 서브이미터라 자동 소멸 안 됨 -> 예약 삭제
            }
            if (_motor.PlayerInArc(waveRange, waveHalfAngle))
            {
                DealDamage(data.attackDamage * waveDmgMul);
                SpawnImpact(impactVfxMelee, PlayerHitPos(), meleeImpactScale);
            }
        }

        yield return new WaitForSeconds(waveRecover);
        _waveCd = waveCooldown * _enrageCd;
        _attacking = false;
    }

    // 모래 회오리: 예고 -> 플레이어 주변 사방에 세로 회오리 기둥을 세운다.
    // 각 기둥은 독립 티커(TornadoColumn)로 ~10s 유지되며 닿으면 틱 데미지 = "이 자리 비켜라"(지형 방해).
    private IEnumerator TornadoAttack()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(tornadoState);
        SpawnCharge(chargeVfxBody, ChargePos(), tornadoWindup);
        yield return new WaitForSeconds(tornadoWindup);

        if (!_dead)
        {
            int phase = RoarsDone();
            int count = tornadoBase + phase * tornadoPerPhase;   // P1=3 / P2=4 / P3=5
            Vector3 center = _player != null ? _player.position : transform.position;
            for (int i = 0; i < count; i++)
            {
                // 플레이어 주변 반경 안에 사방으로 흩뿌린다(정확히 발밑은 아님 = 이동으로 회피 가능).
                Vector2 r = Random.insideUnitCircle * tornadoSpread;
                Vector3 spot = GroundSpot(center + new Vector3(r.x, 0f, r.y));
                StartCoroutine(TornadoColumn(spot));   // 보스와 독립(보스는 곧 풀려 다른 행동)
            }
        }

        ReturnToLocomotion();   // Spike 는 자동복귀 없는 시퀀스 상태 -> 명시 복귀
        yield return new WaitForSeconds(tornadoRecover);
        _tornadoCd = tornadoCooldown * _enrageCd;
        _attacking = false;
    }

    // 모래 회오리 기둥 하나: 세워진 채 tornadoDuration(~10s) 유지, 반경 안 플레이어에게 틱 데미지.
    // 보스 코루틴과 분리(보스는 깔고 이동). VFX 는 자체 예약으로도 정리돼 코루틴이 끊겨도 안 남는다.
    private IEnumerator TornadoColumn(Vector3 spot)
    {
        GameObject vfx = tornadoVfx != null ? Instantiate(tornadoVfx, spot, Quaternion.identity) : null;
        if (vfx != null) Destroy(vfx, tornadoDuration + 0.5f);
        float t = 0f, tick = 0f;
        while (t < tornadoDuration && !_dead)
        {
            t += Time.deltaTime; tick += Time.deltaTime;
            if (tick >= tornadoTick)
            {
                tick = 0f;
                DealAreaDamage(spot, tornadoRadius, data.attackDamage * tornadoDmgMul);
            }
            yield return null;
        }
        if (vfx != null) Destroy(vfx);
    }

    // 모래 늪: 예고 -> 플레이어 발밑에 텔레그래프 + 장판 스폰 -> 지속 틱 데미지.
    private IEnumerator QuicksandAttack()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(quicksandState);
        SpawnCharge(chargeVfxBody, ChargePos(), quicksandWindup);
        yield return new WaitForSeconds(quicksandWindup);

        // 늪을 깔고 보스는 곧 풀려난다(장판 데미지는 독립 티커가 유지). = "깔고 이동한다".
        // 보스가 채널에 묶여 서 있지 않으므로 반경 밖 회피가 '보스 무료 딜'로 이어지지 않는다.
        if (!_dead && _player != null)
        {
            Vector3 spot = GroundSpot(_player.position);
            SpawnTelegraph(spot, quicksandRadius, quicksandDuration);
            GameObject field = quicksandVfx != null ? Instantiate(quicksandVfx, spot, Quaternion.identity) : null;
            if (field != null) Destroy(field, quicksandDuration + 1f);   // 자체 예약(티커가 끊겨도 정리됨)
            StartCoroutine(QuicksandField(spot, field));
        }

        yield return new WaitForSeconds(quicksandRecover);
        _quicksandCd = quicksandCooldown * _enrageCd;
        _attacking = false;
    }

    // 늪 장판 독립 데미지 티커. 보스 코루틴과 분리돼(보스는 다른 행동 가능) 그 자리에서
    // 지속 동안 반경 안 플레이어에게 틱 데미지 = "그 자리에 있지 마라".
    private IEnumerator QuicksandField(Vector3 spot, GameObject field)
    {
        float t = 0f, tick = 0f;
        while (t < quicksandDuration && !_dead)
        {
            t += Time.deltaTime; tick += Time.deltaTime;
            if (tick >= quicksandTick)
            {
                tick = 0f;
                DealAreaDamage(spot, quicksandRadius, data.attackDamage * quicksandDmgMul);
            }
            yield return null;
        }
        if (field != null) Destroy(field);
    }

    // 감금(시그니처): 예고 -> 플레이어를 모래관에 가둠(짧게 완전고정) -> 풀림 -> 붕괴 폭발.
    // 고정이 짧아(coffinLockTime) 풀린 뒤 폭발 전에 도망칠 여지가 있다.
    private IEnumerator CoffinAttack()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(coffinState);
        SpawnCharge(chargeVfxHand, ChargePos(), coffinWindup);
        yield return new WaitForSeconds(coffinWindup);

        if (_dead || _player == null) { _attacking = false; yield break; }

        // 관은 플레이어의 현재 위치에 형성된다(스냅샷). 폭발 판정도 이 지점.
        Vector3 coffinSpot = GroundSpot(_player.position);
        GameObject vfx = coffinVfx != null ? Instantiate(coffinVfx, coffinSpot, Quaternion.identity) : null;
        if (vfx != null) Destroy(vfx, coffinBurstTime + 0.6f);   // 포효 중단에도 정리(무한 잔존 방지)

        // 짧은 완전 고정. ★락은 아래에서 반드시 푼다(SetCoffinLock/ReleaseCoffinLock).
        SetCoffinLock(true);
        float lockT = 0f;
        while (lockT < coffinLockTime && !_dead) { lockT += Time.deltaTime; yield return null; }
        SetCoffinLock(false);

        // 풀린 뒤 붕괴까지 남은 시간(이 동안 반경 밖으로 도망치면 회피).
        float rest = Mathf.Max(0f, coffinBurstTime - coffinLockTime);
        float t = 0f;
        while (t < rest && !_dead) { t += Time.deltaTime; yield return null; }

        if (!_dead)
        {
            DealAreaDamage(coffinSpot, coffinRadius, data.attackDamage * coffinDmgMul);
            SpawnImpact(impactVfxHeavy, coffinSpot + Vector3.up * 0.3f);
        }

        yield return new WaitForSeconds(coffinRecover);
        _coffinCd = coffinCooldown * _enrageCd;
        _attacking = false;
    }

    // 가드 반격: Sand_shield 두르고 무적으로 버틴 뒤 광역 반격. "함부로 붙지 마라".
    private IEnumerator GuardCounter()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(guardState);
        GameObject shield = SpawnCharge(guardVfx, transform.position + Vector3.up * 1f, guardDuration + 0.2f);
        if (_health != null) _health.Invulnerable = true;

        yield return new WaitForSeconds(guardDuration);
        if (_health != null) _health.Invulnerable = false;
        if (shield != null) Destroy(shield);

        if (!_dead)
        {
            _motor.PlayState(guardCounterState);
            if (_player != null) _motor.FaceInstant(_player.position);
            SpawnCharge(chargeVfxHand, ChargePos(), guardCounterWindup);
            yield return new WaitForSeconds(guardCounterWindup);
            if (!_dead && _motor.PlayerInArc(data.attackRange * 1.2f, 90f))
            {
                DealDamage(data.attackDamage * guardCounterDmgMul);
                SpawnImpact(impactVfxMelee, PlayerHitPos(), meleeImpactScale);
            }
            yield return new WaitForSeconds(0.5f);
        }

        _guardCd = guardCooldown * _enrageCd;
        _attacking = false;
    }

    // 다이브 강타: 수직 상승 -> 착지점 상공에서 조준 -> 급강하 내려찍기(와이번 이식).
    // Sand_Smash 내장 예고(약 1.6s)를 체공+낙하 시간과 정렬해 예고와 타격이 맞물린다.
    private IEnumerator DiveSlam()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(diveState);

        Vector3 start = transform.position;
        bool agentWasOn = _agent != null && _agent.enabled;
        if (agentWasOn) { _agent.isStopped = true; _agent.ResetPath(); _agent.enabled = false; }

        SpawnCharge(chargeVfxBody, transform.position + Vector3.up * 1f, divePrepTime);
        yield return new WaitForSeconds(divePrepTime);   // 이륙 전 웅크림(예고)

        // 1) 빠르게 상승
        Vector3 apexUp = start + Vector3.up * diveHeight;
        yield return MoveBetween(start, apexUp, diveRiseTime);

        // 2) 착지 지점 스냅샷 + Sand_Smash 스폰(내장 예고 시작) + 그 상공으로 이동(조준)
        Vector3 land = GroundSpot(_player != null ? _player.position : start);
        if (slamVfx != null) { var sl = Instantiate(slamVfx, land, Quaternion.identity); Destroy(sl, 3.5f); }   // 루프 서브이미터 -> 예약 삭제
        Vector3 over = new Vector3(land.x, land.y + diveHeight, land.z);
        _motor.FaceInstant(land);
        yield return MoveBetween(apexUp, over, diveHoverTime);

        // 3) 수직 급강하 -> hover+drop = 약 1.6s = Sand_Smash 예고 길이에 맞물림
        yield return MoveBetween(over, land, diveDropTime);
        transform.position = land;

        // 4) 착지 판정
        if (!_dead) DealAreaDamage(land, diveRadius, data.attackDamage * diveDmgMul);
        SpawnImpact(impactVfxMelee, land + Vector3.up * 0.2f);

        // 5) 에이전트 재활성 + Warp 재동기화(안 하면 공중/벽 밖에 남는다)
        if (agentWasOn && _agent != null)
        {
            _agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, 8f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
        }

        ReturnToLocomotion();
        yield return new WaitForSeconds(diveRecover);
        _diveCd = diveCooldown * _enrageCd;
        _attacking = false;
    }

    // from -> to 로 transform 부드럽게 이동(다이브 상승/급강하).
    private IEnumerator MoveBetween(Vector3 from, Vector3 to, float time)
    {
        if (time <= 0f) { transform.position = to; yield break; }
        float t = 0f;
        while (t < time && !_dead)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / time));
            transform.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        if (!_dead) transform.position = to;
    }

    // 다이브 중단(포효/사망) 시 공중 보스를 지면으로 내려놓고 에이전트 재활성.
    private void EndDiveCleanup()
    {
        if (_agent == null || _agent.enabled) return;
        Vector3 p = transform.position;
        if (NavMesh.SamplePosition(p, out var hit, 10f, NavMesh.AllAreas)) p = hit.position;
        transform.position = p;
        _agent.enabled = true;
        _agent.Warp(p);
    }

    // sequence 상태(Spike/Fly)는 자동 복귀 전이가 없어서 코루틴이 끝나며 명시적으로 Idle 로 되돌린다.
    private void ReturnToLocomotion()
    {
        if (!_dead) _motor.PlayState("Idle");
    }

    // 궁극(P3): 긴 예고 후 지면 융기 대폭발(Pyramid_Explosion)
    private IEnumerator Ultimate()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(ultState);
        SpawnCharge(chargeVfxBody, transform.position + Vector3.up * 1.5f, ultWindup);
        yield return new WaitForSeconds(ultWindup);

        if (_dead || _player == null) { _attacking = false; yield break; }

        Vector3 spot = GroundSpot(_player.position);
        SpawnTelegraph(spot, ultRadius, ultTelegraph + 0.5f);
        if (ultVfx != null) { var uv = Instantiate(ultVfx, spot, Quaternion.identity); Destroy(uv, 5.5f); }   // 루프 서브이미터 -> 예약 삭제

        yield return new WaitForSeconds(ultTelegraph);
        if (!_dead)
        {
            DealAreaDamage(spot, ultRadius, data.attackDamage * ultDmgMul);
            SpawnImpact(impactVfxHeavy, spot + Vector3.up * 0.3f);
        }

        yield return new WaitForSeconds(ultRecover);
        _ultCd = ultCooldown * _enrageCd;
        _attacking = false;
    }

    // HP 임계값(66%/33%) 통과 시 포효 1회
    private void CheckRoarPhase()
    {
        if (_health == null || _health.maxHP <= 0f) return;
        float ratio = _health.currentHP / _health.maxHP;
        for (int i = 0; i < RoarThresholds.Length; i++)
        {
            if (_roared[i] || ratio > RoarThresholds[i]) continue;
            _roared[i] = true;
            StopAllCoroutines();
            ReleaseCoffinLock();   // ★포효가 감금 코루틴을 끊어도 플레이어 락은 반드시 푼다
            if (_health != null) _health.Invulnerable = false;   // 가드 무적 중 포효가 끼어도 무적 잔존 방지
            EndDiveCleanup();      // 다이브 중 포효 시 공중 보스 지면 복구
            _attacking = false;
            StartCoroutine(RoarPhase());
            return;
        }
    }

    private IEnumerator RoarPhase()
    {
        _attacking = true;
        // 포효는 StopAllCoroutines 뒤에 시작된다. 다이브 중이었다면 에이전트가 꺼진 채
        // 남을 수 있으니(EndDiveCleanup 이 복구) 여기서도 한번 더 보장한다.
        if (_agent != null && !_agent.enabled)
        {
            _agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, 8f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
        }
        _motor.StopMove();
        _motor.PlayState(roarState);
        yield return new WaitForSeconds(roarBuildup);

        if (!_dead)
        {
            BossRoarDebuff.Trigger(debuffDuration, debuffDrainMult, debuffDarkness,
                                   debuffVignetteStrength, debuffVignetteFalloff);
            Enrage();
        }

        yield return new WaitForSeconds(roarRecover);
        _attacking = false;
    }

    // 포효마다 누적: 공격 쿨 감소 + 이속 증가
    private void Enrage()
    {
        _enrageCd *= enrageCdMul;
        _enrageSpeed *= enrageSpeedMul;
        if (_agent != null && data != null) _agent.speed = data.moveSpeed * _enrageSpeed;
    }

    // 지면으로 스냅. 플레이어 pivot 이 몸통 중앙이라 pos.y 가 지면보다 높다
    // -> NavMesh 표면 높이를 지면으로 써서 텔레그래프/장판 VFX 가 공중에 뜨지 않게 한다.
    private Vector3 GroundSpot(Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out var nav, 5f, NavMesh.AllAreas))
            return nav.position;
        return pos;
    }

    // 지면 범위 텔레그래프 링: pos 에 radius 크기로 띄우고 life 후 제거.
    private void SpawnTelegraph(Vector3 pos, float radius, float life = 3f)
    {
        if (telegraphVfx == null) return;
        var t = Instantiate(telegraphVfx, pos + Vector3.up * 0.05f, Quaternion.identity);
        t.transform.localScale = Vector3.one * radius * telegraphScaleMul;
        Destroy(t, life);
    }

    // 전조가 붙을 위치(손 본이 지정됐으면 거기, 아니면 spawnOffset 로컬 위치)
    private Vector3 ChargePos()
        => chargeAnchor != null ? chargeAnchor.position : transform.TransformPoint(spawnOffset);

    // 전조(응축) VFX: windup 동안 손/몸에 재생하다 공격 발동과 함께 사라진다.
    private GameObject SpawnCharge(GameObject vfx, Vector3 pos, float life)
    {
        if (vfx == null) return null;
        var g = Instantiate(vfx, pos, transform.rotation, transform);
        if (life > 0f) Destroy(g, life);
        return g;
    }

    // 임팩트(타격) VFX: 맞는 순간 1회. 안전하게 3초 후 파괴.
    private void SpawnImpact(GameObject vfx, Vector3 worldPos, float scale = 1f)
    {
        if (vfx == null) return;
        var g = Instantiate(vfx, worldPos, Quaternion.identity);
        if (scale != 1f) g.transform.localScale = Vector3.one * scale;
        Destroy(g, 3f);
    }

    // 플레이어 몸통 높이의 타격점(발밑 pivot 보정)
    private Vector3 PlayerHitPos()
        => _player != null ? _player.position + Vector3.up * impactHeight : transform.position;

    private void DealDamage(float amount)
    {
        if (_playerStat != null) _playerStat.TakeDamage(amount);
    }

    private void DealAreaDamage(Vector3 center, float radius, float amount)
    {
        if (_player == null) return;
        Vector3 a = center; a.y = 0f;
        Vector3 b = _player.position; b.y = 0f;
        if (Vector3.Distance(a, b) <= radius) DealDamage(amount);
    }

    // 감금 락 제어. 켤 때 잠근 컴포넌트를 기억해 두고, 끌 때(또는 강제 해제 시) 그걸 풀어 준다.
    private void SetCoffinLock(bool on)
    {
        if (on)
        {
            _lockedMove = _player != null ? _player.GetComponent<PlayerMovementComponent>() : null;
            if (_lockedMove != null) { _lockedMove.LockMovement(true); _coffinLockActive = true; }
        }
        else
        {
            if (_coffinLockActive && _lockedMove != null) _lockedMove.LockMovement(false);
            _coffinLockActive = false;
            _lockedMove = null;
        }
    }

    // 코루틴이 강제로 끊겨도(사망/포효/리셋) 플레이어 락이 남지 않게 하는 안전 해제.
    private void ReleaseCoffinLock()
    {
        if (_coffinLockActive) SetCoffinLock(false);
    }

    private void ResetBoss()
    {
        _leash.Clear();
        _engaged = false;

        StopAllCoroutines();
        ReleaseCoffinLock();   // ★리쉬 리셋이 코루틴을 끊어도 플레이어 락은 반드시 푼다
        EndDiveCleanup();
        _attacking = false;

        for (int i = 0; i < _roared.Length; i++) _roared[i] = false;
        _enrageCd = 1f;
        _enrageSpeed = 1f;
        for (int i = 0; i < _atkCd.Length; i++) _atkCd[i] = 0f;
        _waveCd = 0f; _tornadoCd = 0f; _quicksandCd = 0f; _coffinCd = 0f;
        _diveCd = 0f; _ultCd = 0f; _guardCd = 0f; _meleeGapCd = 0f;

        if (_agent != null && !_agent.enabled) _agent.enabled = true;

        _motor.ResetToSpawn(_leash.SpawnPos, _leash.SpawnRot, data);
        BossHealthBarUI.Hide();
    }

    private void LateUpdate()
    {
        _motor.TickFacing(_dead || _attacking, ResolveTarget(), data);
    }
}
