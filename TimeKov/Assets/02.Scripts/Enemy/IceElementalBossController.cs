using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PilotoStudio;   // BeamEmitter (FrostRay 빔 조준)

// 얼음정령(설산 보스) - 전용 상태머신.
// 와이번과 같은 방식(BT/EnemyBrain 미사용)이지만 글루는 BossMotor/BossLeash 가 대신한다.
//
// [정체성] 와이번 = 근접형(다가와서 물기). 얼음정령 = 캐스터형(거리 두고 압박).
//   같은 리듬이면 보스 둘이 겹치므로 축을 다르게 잡았다:
//   - 원거리 주력(빔/낙하)으로 압박하고, 붙으면 노바로 밀어낸다.
//   - 시그니처 = 활강 돌진(Fly 클립이 몸 65도 눕힌 돌진 포즈라 그대로 살림).
//   - 가드(Block) = 근접 압박 시 무적 후 광역 반격. "함부로 붙지 마라"를 가르치는 장치. 와이번엔 없다.
//
// [페이즈] 포효(66%/33%) 기준 3단. 와이번과 동일한 RoarsDone() 방식.
//   P1: 근접 + 빔 / P2: + 낙하 + 노바 + 가드 / P3: + 활강돌진 + 궁극, 광폭화
//
// [애니] IceElementalAnimatorBuilder 로 굽는 전용 컨트롤러. 상태 이름으로 CrossFade.
//   벤더가 텔레그래프(BeamStart/RainStart)와 발사(Beam/Rain)를 나눠놔서 예고->발동 리듬이 공짜.
// [사망] Death2 = 스스로 지면 아래로 침강 = "녹아 스며듦"이 코드 0줄.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class IceElementalBossController : MonoBehaviour, IEnemyDataSource
{
    [Header("데이터 (HP/속도/근접공격 기본 수치는 SO에서 튜닝)")]
    [SerializeField] private MeleeEnemyData data;
    public MeleeEnemyData Data => data;   // 도감 등 외부서 보스 스탯 조회용(보스는 EnemyBrain 미사용)
    [SerializeField] private string bossSubtitle = "얼어붙은 시간의 파수꾼";

    [Header("사운드 (패턴별 전용 SFX. data 의 attackSound 는 슬롯이 하나뿐이라 여기서 패턴별로 관리)")]
    [Tooltip("근접(MeleeAttack 양쪽 다/GuardCounter 반격 스윙) 공용 공격음. 근접 테이크가 하나뿐이라 공유.")]
    [SerializeField] private AudioClip meleeSound;
    [Tooltip("근접/돌진/반격이 맞는 순간의 얼음 파편 임팩트음. meleeSound 와 같이 겹쳐 재생.")]
    [SerializeField] private AudioClip meleeImpactSound;
    [Tooltip("표적 빔(BeamAttack) 전용. 이 패턴만 신규 음원이 없어 기존 것을 그대로 씀.")]
    [SerializeField] private AudioClip castAttackSound;
    [SerializeField] private AudioClip rainSound;
    [SerializeField] private AudioClip novaSound;
    [SerializeField] private AudioClip dashSound;
    [Tooltip("궁극기 시전(차징) 시작 시점.")]
    [SerializeField] private AudioClip ultimateChargeSound;
    [Tooltip("궁극기 폭발 시점.")]
    [SerializeField] private AudioClip ultimateBurstSound;
    [Tooltip("가드(무적) 돌입 시점.")]
    [SerializeField] private AudioClip guardSound;
    [Tooltip("페이즈 전환 포효 1회차(66%). data.detectSound(최초 발견)와는 별도.")]
    [SerializeField] private AudioClip roarSound;
    [Tooltip("페이즈 전환 포효 2회차(33%).")]
    [SerializeField] private AudioClip roar2Sound;

    [Header("표적 빔 (손에서 지속 레이저 - 플레이어 조준 추적)")]
    [SerializeField] private GameObject beamVfx;
    [SerializeField] private float beamRange = 20f;
    [SerializeField] private float beamCooldown = 3.5f;
    [SerializeField] private float beamWindup = 0.73f;      // CastToTargetStart 예고
    [SerializeField] private float beamDuration = 1.5f;     // 빔 지속(이 동안 틱 데미지)
    [SerializeField] private float beamTickInterval = 0.2f;
    [SerializeField] private float beamRecover = 0.6f;
    [SerializeField] private float beamDmgMul = 0.22f;      // x attackDamage (틱당). 지속 추적빔이라 낮게
    [SerializeField] private Vector3 beamOffset = new Vector3(0f, 2f, 1f);   // 발사 위치(로컬: 손 근처)

    [Header("낙하 (사방에서 고드름 다발 - 페이즈별 강화)")]
    [Tooltip("고드름 한 발(SkySingle_Frost). 자체 착탄 원 포함이라 별도 텔레그래프 불필요.")]
    [SerializeField] private GameObject rainVfx;
    [SerializeField] private float rainRange = 18f;
    [SerializeField] private float rainCooldown = 5.5f;
    [SerializeField] private float rainWindup = 0.77f;      // CastUpStart 길이
    [SerializeField] private float rainRecover = 0.8f;
    [SerializeField] private float rainDmgMul = 1.0f;       // 발당(여러 발이라 개별은 약하게)
    [Tooltip("P1 고드름 발 수. 페이즈마다 아래 값만큼 증가")]
    [SerializeField] private int rainShardsBase = 5;
    [SerializeField] private int rainShardsPerPhase = 4;    // P1=5 / P2=9 / P3=13
    [Tooltip("발 사이 간격(초). 페이즈마다 30% 빨라짐")]
    [SerializeField] private float rainShardGap = 0.22f;
    [Tooltip("플레이어 주변 흩뿌리는 반경. 작을수록 발밑에 몰림(회피 어려움).")]
    [SerializeField] private float rainSpreadRadius = 5f;
    [Tooltip("고드름 낙하 시간(스폰~착탄 판정 지연). 페이즈마다 빨라짐")]
    [SerializeField] private float rainShardFall = 0.5f;
    [Tooltip("발당 데미지 반경")]
    [SerializeField] private float rainShardRadius = 2.5f;

    [Header("자기중심 노바 (AOE_Explosion_Frost - 바닥 원 퍼지며 밀어내기)")]
    [SerializeField] private GameObject novaVfx;
    [SerializeField] private float novaRange = 5f;          // 이 안에 플레이어가 있으면 사용
    [SerializeField] private float novaCooldown = 6f;
    [SerializeField] private float novaWindup = 0.9f;       // Cast2 시전 중 떠오름
    [SerializeField] private float novaRecover = 0.9f;
    [SerializeField] private float novaRadius = 7f;
    [SerializeField] private float novaDmgMul = 1.6f;

    [Header("활강 돌진 (Fly - 시그니처. 거리 좁히며 관통)")]
    [SerializeField] private GameObject dashVfx;            // Wing_Frost 등(선택)
    [SerializeField] private float dashMinRange = 8f;       // 이 거리 이상일 때만(붙어있으면 의미 없음)
    [SerializeField] private float dashMaxRange = 22f;
    [SerializeField] private float dashCooldown = 7f;
    [SerializeField] private float dashWindup = 0.5f;
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.75f;
    [SerializeField] private float dashRecover = 0.7f;
    [SerializeField] private float dashRadius = 2.5f;       // 관통 판정 반경
    [SerializeField] private float dashDmgMul = 1.4f;

    [Header("궁극 (Cast3 + NuclearBomb_Frost - 페이즈3 전용)")]
    [SerializeField] private GameObject ultVfx;
    [SerializeField] private float ultRange = 16f;
    [SerializeField] private float ultCooldown = 11f;
    [SerializeField] private float ultWindup = 1.4f;        // Cast3 = 양팔 대시전(긴 예고)
    [SerializeField] private float ultTelegraph = 1.0f;
    [SerializeField] private float ultRecover = 1.2f;
    [SerializeField] private float ultRadius = 9f;
    [SerializeField] private float ultDmgMul = 2.6f;

    [Header("가드 반격 (Block - 근접 압박 시 무적 후 광역 반격)")]
    [SerializeField] private float guardRange = 4f;         // 이 안에 플레이어가 있고
    [SerializeField] private float guardCooldown = 6f;      // 쿨이 차면 가드
    [SerializeField] private float guardDuration = 1.0f;    // 무적 시간(Block 클립 길이)
    [SerializeField] private string guardCounterState = "Attack2";   // 가드 후 반격(광역 스윙)
    [SerializeField] private float guardCounterWindup = 0.5f;
    [SerializeField] private float guardCounterDmgMul = 1.5f;

    [Header("범위 텔레그래프 (낙하/궁극 발동 전 지면 표시)")]
    [SerializeField] private GameObject telegraphVfx;       // Wyvern_Telegraph 복제 + 하늘색
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
    [Tooltip("손 응축(근접/빔 전조). windup 동안 손에 서리가 모임. 비우면 안 나옴.")]
    [SerializeField] private GameObject chargeVfxHand;
    [Tooltip("몸 응축(낙하/노바/궁극/돌진 전조). 큰 시전 예고.")]
    [SerializeField] private GameObject chargeVfxBody;
    [Tooltip("근접 타격 임팩트(얼음 파편). 맞는 순간 1회.")]
    [SerializeField] private GameObject impactVfxMelee;
    [Tooltip("빔 틱 임팩트(초당 여러 번). 반드시 경량 프리팹.")]
    [SerializeField] private GameObject impactVfxRanged;
    [Tooltip("대형 착탄 임팩트(낙하/궁극). 1회성이라 무거워도 됨.")]
    [SerializeField] private GameObject impactVfxHeavy;
    [Tooltip("전조가 붙을 손 본. 비우면 beamOffset 위치(로컬)에 붙는다.")]
    [SerializeField] private Transform chargeAnchor;
    [Tooltip("임팩트가 뜰 플레이어 몸 높이 보정(발밑 pivot 이면 위로).")]
    [SerializeField] private float impactHeight = 1.0f;
    [Tooltip("근접 임팩트 크기 배율(IceCubesExplosion 은 원래 대형 AoE라 0.5 로 줄임).")]
    [SerializeField] private float meleeImpactScale = 0.5f;

    [Header("애니메이터 (전용 컨트롤러 상태명)")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string beamStartState = "BeamStart";
    [SerializeField] private string beamState = "Beam";
    [SerializeField] private string rainStartState = "RainStart";
    [SerializeField] private string rainState = "Rain";
    [SerializeField] private string novaState = "CastNova";
    [SerializeField] private string ultState = "CastBig";
    [SerializeField] private string dashState = "Fly";
    [SerializeField] private string blockState = "Block";

    [Header("패턴 선택 (가중 랜덤 - 조건 맞는 것 중 확률로 뽑음)")]
    [Tooltip("각 공격의 선택 가중치. 클수록 자주 뽑힌다. 개별 쿨과 별개.")]
    [SerializeField] private float wMelee = 1.5f;
    [SerializeField] private float wBeam = 1.5f;
    [SerializeField] private float wRain = 1.2f;
    [SerializeField] private float wNova = 1.0f;
    [SerializeField] private float wGuard = 0.8f;
    [SerializeField] private float wDash = 1.0f;
    [SerializeField] private float wUlt = 1.2f;
    [Tooltip("직전에 쓴 패턴의 가중치 배율(연속 방지). 0=절대 연속 안 함, 1=페널티 없음.")]
    [Range(0f, 1f)] [SerializeField] private float repeatPenalty = 0.35f;

    [Header("리쉬 (이탈 리셋)")]
    [SerializeField] private float leashDistance = 45f;
    [SerializeField] private float leashResetTime = 4f;

    // 근접 공격 정의 (와이번과 동일 패턴)
    private struct AtkDef
    {
        public string state; public float reachMul, halfAngle, windup, recover, dmgMul, cd, weight;
    }
    private static readonly AtkDef[] MeleeAttacks =
    {
        // Attack1 = 찌르기 런지(hips 68cm 전진) / Attack2 = 광역 스윙(hips X41 Z64)
        new AtkDef { state="Attack1", reachMul=1.0f, halfAngle=45f, windup=0.40f, recover=0.40f, dmgMul=1.0f, cd=1.5f, weight=3f },
        new AtkDef { state="Attack2", reachMul=1.2f, halfAngle=90f, windup=0.50f, recover=0.50f, dmgMul=1.2f, cd=2.5f, weight=2f },
    };
    private const float MeleeMaxReachMul = 1.2f;
    private const float MeleeGap = 0.3f;

    private static readonly float[] RoarThresholds = { 0.66f, 0.33f };

    // 패턴 종류(가중 랜덤 선택용). 우선순위 사다리 대신 조건 맞는 후보를 모아 확률로 뽑는다.
    private enum AtkType { None, Melee, Beam, Rain, Nova, Guard, Dash, Ult }
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
    private float _beamCd, _rainCd, _novaCd, _dashCd, _ultCd, _guardCd, _meleeGapCd;
    private float[] _atkCd;
    private bool[] _roared;
    private float _enrageCd = 1f;
    private float _enrageSpeed = 1f;

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
    }

    private void HandleDeath()
    {
        _dead = true;
        BattleBgm.End();   // 처치 → 전투 브금 종료, 기존 BGM 재개
        if (_health != null) _health.Invulnerable = false;   // 가드 중 사망 시 무적 잔존 방지
        StopAllCoroutines();
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
            BossHealthBarUI.Show(_health, data != null ? data.enemyName : "얼음정령", bossSubtitle);
            _feedback?.PlayDetect();
            BattleBgm.Begin(SfxId.WyvernBattleBgm);   // 교전 시작 → 전투 브금(기존 BGM 일시정지). 보스 공통 브금 재사용.
        }
        if (target == null) { _motor.StopMove(); return; }
        if (data == null) return;

        float dist = _motor.PlanarDistance(target.position);
        int phase = RoarsDone();   // 0=P1 / 1=P2 / 2=P3
        float meleeMax = data.attackRange * MeleeMaxReachMul;

        // 조건(페이즈/쿨/거리) 맞는 공격을 전부 후보로 모은다. 거리 조건이 근접/원거리를
        // 자연스럽게 갈라주므로, 현재 위치에서 쓸 수 있는 것들 중에서만 뽑힌다.
        _cand.Clear();
        if (phase >= 2 && _ultCd <= 0f && ultVfx != null && dist <= ultRange)
            _cand.Add((AtkType.Ult, wUlt));
        if (phase >= 1 && _novaCd <= 0f && dist <= novaRange)
            _cand.Add((AtkType.Nova, wNova));
        if (phase >= 1 && _guardCd <= 0f && dist <= guardRange)
            _cand.Add((AtkType.Guard, wGuard));
        if (phase >= 1 && _dashCd <= 0f && dist >= dashMinRange && dist <= dashMaxRange)
            _cand.Add((AtkType.Dash, wDash));
        if (_rainCd <= 0f && rainVfx != null && dist <= rainRange)
            _cand.Add((AtkType.Rain, wRain));
        if (_beamCd <= 0f && beamVfx != null && dist <= beamRange && dist > meleeMax)
            _cand.Add((AtkType.Beam, wBeam));

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
                case AtkType.Ult:   StartCoroutine(Ultimate()); break;
                case AtkType.Nova:  StartCoroutine(Nova()); break;
                case AtkType.Guard: StartCoroutine(GuardCounter()); break;
                case AtkType.Dash:  StartCoroutine(DashCharge()); break;
                case AtkType.Rain:  StartCoroutine(RainAttack()); break;
                case AtkType.Beam:  StartCoroutine(BeamAttack()); break;
                case AtkType.Melee: StartCoroutine(MeleeAttack(meleeIdx)); break;
            }
            return;
        }

        // 후보 없음(전부 쿨/사거리 밖): 근접 사거리면 제자리 대기(견제), 아니면 추격.
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
        if (_beamCd > 0f) _beamCd -= dt;
        if (_rainCd > 0f) _rainCd -= dt;
        if (_novaCd > 0f) _novaCd -= dt;
        if (_dashCd > 0f) _dashCd -= dt;
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
        float aggro = data != null ? Mathf.Max(data.visionRange, beamRange) : 25f;
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

        SpawnCharge(chargeVfxHand, ChargePos(), a.windup);   // 손에 서리 응축(전조)
        yield return new WaitForSeconds(a.windup);
        _feedback?.PlaySound(meleeSound);
        if (!_dead && _motor.PlayerInArc(data.attackRange * a.reachMul, a.halfAngle))
        {
            DealDamage(data.attackDamage * a.dmgMul);
            _feedback?.PlaySound(meleeImpactSound);
            SpawnImpact(impactVfxMelee, PlayerHitPos(), meleeImpactScale);   // 얼음 파편(임팩트)
        }

        yield return new WaitForSeconds(a.recover);
        _atkCd[idx] = a.cd * _enrageCd;
        _meleeGapCd = MeleeGap;
        _attacking = false;
    }

    // 표적 빔: 예고(BeamStart) -> 발사(Beam). 빔 VFX 를 스폰하고 지속 동안 틱 데미지.
    private IEnumerator BeamAttack()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(beamStartState);
        SpawnCharge(chargeVfxHand, ChargePos(), beamWindup);   // 손에 서리 응축(전조)
        yield return new WaitForSeconds(beamWindup);

        if (_dead) { _attacking = false; yield break; }
        _motor.PlayState(beamState);
        _feedback?.PlaySound(castAttackSound);

        GameObject vfx = null;
        if (beamVfx != null)
        {
            Vector3 origin = transform.TransformPoint(beamOffset);
            vfx = Instantiate(beamVfx, origin, transform.rotation, transform);
            // ★무한 빔 방지: 포효(CheckRoarPhase)가 StopAllCoroutines 로 이 코루틴을 끊으면
            // 아래 Destroy(vfx) 가 실행 안 돼서 빔이 영원히 남는다 -> 스폰 즉시 예약 삭제.
            Destroy(vfx, beamDuration + 0.3f);
            // 빔이 플레이어를 실제로 조준하게(추적빔). 안 하면 시각과 판정이 따로 논다.
            var emitter = vfx.GetComponentInChildren<BeamEmitter>();
            if (emitter != null && _player != null) emitter.SetBeamTarget(_player);
        }

        // 지속 동안 플레이어를 계속 향하며(추적빔) 틱 데미지.
        float t = 0f, tick = 0f;
        while (t < beamDuration && !_dead)
        {
            t += Time.deltaTime; tick += Time.deltaTime;
            if (_player != null)
            {
                _motor.FaceInstant(_player.position);
                if (tick >= beamTickInterval)
                {
                    tick = 0f;
                    if (_motor.PlanarDistance(_player.position) <= beamRange)
                    {
                        DealDamage(data.attackDamage * beamDmgMul);
                        SpawnImpact(impactVfxRanged, PlayerHitPos());   // 빔 닿는 곳에 서리 튐(경량)
                    }
                }
            }
            yield return null;
        }
        if (vfx != null) Destroy(vfx);

        ReturnToLocomotion();   // Beam(손 내밀기) 상태에서 복귀
        yield return new WaitForSeconds(beamRecover);
        _beamCd = beamCooldown * _enrageCd;
        _attacking = false;
    }

    // 낙하: 예고(RainStart) -> 플레이어 발밑에 텔레그래프 -> VFX 스폰 -> 판정
    private IEnumerator RainAttack()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(rainStartState);
        SpawnCharge(chargeVfxBody, ChargePos(), rainWindup);   // 손 위 냉기 응축(전조)
        yield return new WaitForSeconds(rainWindup);

        if (_dead || _player == null) { _attacking = false; yield break; }
        _motor.PlayState(rainState);
        _feedback?.PlaySound(rainSound);

        // 페이즈별 강화: 발 수↑ / 간격↓(빠르게) / 낙하 시간↓
        int phase = RoarsDone();
        int shards = rainShardsBase + phase * rainShardsPerPhase;   // P1=5 / P2=9 / P3=13
        float gap = rainShardGap * Mathf.Pow(0.7f, phase);         // 페이즈마다 30% 빠르게
        float fall = rainShardFall * Mathf.Pow(0.75f, phase);      // 낙하도 빨라짐

        for (int i = 0; i < shards && !_dead; i++)
        {
            // 매 발 현재 플레이어 위치 주변으로(움직이면 따라감). 반경 안 랜덤이라
            // 정확히 발밑은 아님 = 지그재그로 움직이면 일부는 회피된다.
            Vector3 center = _player != null ? _player.position : transform.position;
            Vector2 r = Random.insideUnitCircle * rainSpreadRadius;
            Vector3 spot = GroundSpot(center + new Vector3(r.x, 0f, r.y));
            if (rainVfx != null) Instantiate(rainVfx, spot, Quaternion.identity);
            StartCoroutine(RainShardDamage(spot, fall));   // 고드름 떨어진 뒤 그 지점 판정
            yield return new WaitForSeconds(gap);
        }

        ReturnToLocomotion();   // Rain 상태에서 복귀
        yield return new WaitForSeconds(rainRecover);
        _rainCd = rainCooldown * _enrageCd;
        _attacking = false;
    }

    // 고드름 한 발: 낙하 시간 뒤 착탄점에 데미지. 착탄 연출은 SkySingle 자체에 포함(중복 스폰 안 함).
    private IEnumerator RainShardDamage(Vector3 spot, float fall)
    {
        yield return new WaitForSeconds(fall);
        if (_dead) yield break;
        DealAreaDamage(spot, rainShardRadius, data.attackDamage * rainDmgMul);
    }

    // 자기중심 노바: 붙은 플레이어를 밀어내는 용도
    private IEnumerator Nova()
    {
        _attacking = true;
        _motor.StopMove();
        _motor.PlayState(novaState);
        // 페이즈가 오르면 반경이 커진다(P1 1.0 / P2 1.25 / P3 1.5배)
        float radius = novaRadius * (1f + RoarsDone() * 0.25f);
        Vector3 center = GroundSpot(transform.position);   // 보스 발밑 지면
        // 예고는 몸 냉기 응축(chargeVfxBody)에 맡긴다. 밋밋한 텔레그래프 원은 안 쓴다.
        // 발동 시 AOE_Explosion_Frost 가 바닥 원으로 확 퍼지며 밀어낸다.
        SpawnCharge(chargeVfxBody, transform.position + Vector3.up * 1f, novaWindup);
        yield return new WaitForSeconds(novaWindup);
        _feedback?.PlaySound(novaSound);

        if (!_dead)
        {
            if (novaVfx != null) Instantiate(novaVfx, center, Quaternion.identity);
            DealAreaDamage(center, radius, data.attackDamage * novaDmgMul);
        }

        yield return new WaitForSeconds(novaRecover);
        _novaCd = novaCooldown * _enrageCd;
        _attacking = false;
    }

    // 가드 반격: 무적으로 버틴 뒤 광역 반격. "함부로 붙지 마라"를 가르치는 장치.
    private IEnumerator GuardCounter()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(blockState);
        _feedback?.PlaySound(guardSound);
        if (_health != null) _health.Invulnerable = true;

        yield return new WaitForSeconds(guardDuration);
        if (_health != null) _health.Invulnerable = false;

        if (!_dead)
        {
            _motor.PlayState(guardCounterState);
            if (_player != null) _motor.FaceInstant(_player.position);
            SpawnCharge(chargeVfxHand, ChargePos(), guardCounterWindup);   // 반격 전 응축
            yield return new WaitForSeconds(guardCounterWindup);
            _feedback?.PlaySound(meleeSound);
            if (!_dead && _motor.PlayerInArc(data.attackRange * 1.2f, 90f))
            {
                DealDamage(data.attackDamage * guardCounterDmgMul);
                _feedback?.PlaySound(meleeImpactSound);
                SpawnImpact(impactVfxMelee, PlayerHitPos(), meleeImpactScale);
            }
            yield return new WaitForSeconds(0.5f);
        }

        _guardCd = guardCooldown * _enrageCd;
        _attacking = false;
    }

    // 활강 돌진: 몸 65도 눕힌 Fly 포즈로 플레이어 방향으로 관통. 시그니처.
    private IEnumerator DashCharge()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player == null) { _attacking = false; yield break; }
        _motor.FaceInstant(_player.position);
        _motor.PlayState(dashState);

        SpawnCharge(chargeVfxBody, transform.position + Vector3.up * 1f, dashWindup);   // 몸에 냉기 두르기(전조)
        yield return new WaitForSeconds(dashWindup);
        if (_dead) { _attacking = false; yield break; }
        _feedback?.PlaySound(dashSound);

        GameObject vfx = dashVfx != null ? Instantiate(dashVfx, transform.position, transform.rotation, transform) : null;
        if (vfx != null) Destroy(vfx, dashDuration + dashRecover + 0.5f);   // 포효 중단에도 정리

        // 에이전트를 끄고 직접 이동(관통). 끝나면 반드시 되살린다.
        Vector3 dir = transform.forward;
        bool agentWasOn = _agent != null && _agent.enabled;
        if (agentWasOn) _agent.enabled = false;

        float t = 0f;
        bool hitOnce = false;
        while (t < dashDuration && !_dead)
        {
            t += Time.deltaTime;
            transform.position += dir * dashSpeed * Time.deltaTime;
            if (!hitOnce && _player != null
                && Vector3.Distance(transform.position, _player.position) <= dashRadius)
            {
                hitOnce = true;   // 관통 1회만
                DealDamage(data.attackDamage * dashDmgMul);
                _feedback?.PlaySound(meleeImpactSound);
                SpawnImpact(impactVfxMelee, PlayerHitPos(), meleeImpactScale);   // 스치는 얼음 파편

            }
            yield return null;
        }

        // 에이전트 복구 + 네비메시 스냅(안 하면 공중/벽 밖에 남는다)
        if (agentWasOn && _agent != null)
        {
            _agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, 8f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
        }
        if (vfx != null) Destroy(vfx);

        ReturnToLocomotion();   // Fly 상태에서 복귀
        yield return new WaitForSeconds(dashRecover);
        _dashCd = dashCooldown * _enrageCd;
        _attacking = false;
    }

    // sequence 상태(Beam/Rain/Fly)는 자동 복귀 전이가 없어서 코루틴이 끝나며 명시적으로 Idle 로 되돌린다.
    // Idle <-> Locomotion 은 Speed 로 이어지므로 다음 프레임에 이동하면 자연스럽게 Loco 로 전이된다.
    private void ReturnToLocomotion()
    {
        if (!_dead) _motor.PlayState("Idle");
    }

    // 궁극(P3): 긴 예고 후 대형 폭발
    private IEnumerator Ultimate()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(ultState);
        SpawnCharge(chargeVfxBody, transform.position + Vector3.up * 1.5f, ultWindup);   // 대형 응축(긴 예고)
        yield return new WaitForSeconds(ultWindup);
        _feedback?.PlaySound(ultimateChargeSound);

        if (_dead || _player == null) { _attacking = false; yield break; }

        Vector3 spot = GroundSpot(_player.position);   // 지면으로 스냅
        SpawnTelegraph(spot, ultRadius);
        if (ultVfx != null) Instantiate(ultVfx, spot, Quaternion.identity);

        yield return new WaitForSeconds(ultTelegraph);
        if (!_dead)
        {
            _feedback?.PlaySound(ultimateBurstSound);
            DealAreaDamage(spot, ultRadius, data.attackDamage * ultDmgMul);
            SpawnImpact(impactVfxHeavy, spot + Vector3.up * 0.3f);   // 대형 착탄
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
            _attacking = false;
            StartCoroutine(RoarPhase(i));
            return;
        }
    }

    private IEnumerator RoarPhase(int idx)
    {
        _attacking = true;
        // 포효는 StopAllCoroutines 뒤에 시작된다. 활강 돌진 중이었다면 에이전트가 꺼진 채
        // 남아 보스가 영영 못 움직인다 -> 여기서 복구를 보장한다.
        if (_agent != null && !_agent.enabled)
        {
            _agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, 8f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
        }
        _motor.StopMove();
        _motor.PlayState(roarState);
        _feedback?.PlaySound(idx == 0 ? roarSound : roar2Sound);
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
    // -> 그대로 쓰면 텔레그래프 원/낙하 VFX 가 공중에 뜬다. NavMesh 는 지형 위에 베이크되므로
    //    그 표면 높이를 지면으로 쓴다(레이어 마스크/플레이어 콜라이더 걱정 없음).
    private Vector3 GroundSpot(Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out var nav, 5f, NavMesh.AllAreas))
            return nav.position;
        return pos;
    }

    private void SpawnTelegraph(Vector3 pos, float radius)
    {
        if (telegraphVfx == null) return;
        var t = Instantiate(telegraphVfx, pos + Vector3.up * 0.05f, Quaternion.identity);
        t.transform.localScale = Vector3.one * radius * telegraphScaleMul;
        Destroy(t, 3f);
    }

    // 전조가 붙을 위치(손 본이 지정됐으면 거기, 아니면 beamOffset 로컬 위치)
    private Vector3 ChargePos()
        => chargeAnchor != null ? chargeAnchor.position : transform.TransformPoint(beamOffset);

    // 전조(응축) VFX: windup 동안 손/몸에 재생하다 공격 발동과 함께 사라진다.
    // 보스 자식으로 붙여서 본을 따라 움직이게 하고, life 후 파괴.
    private GameObject SpawnCharge(GameObject vfx, Vector3 pos, float life)
    {
        if (vfx == null) return null;
        var g = Instantiate(vfx, pos, transform.rotation, transform);
        if (life > 0f) Destroy(g, life);
        return g;
    }

    // 임팩트(타격) VFX: 맞는 순간 1회. 파티클이 알아서 끝나되 안전하게 3초 후 파괴.
    // scale: 원본이 큰 프리팹(IceCubesExplosion 등)을 근접 피격 크기로 줄일 때.
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

    private void ResetBoss()
    {
        _leash.Clear();
        _engaged = false;
        BattleBgm.End();   // 이탈(리셋) → 전투 브금 종료, 기존 BGM 재개

        StopAllCoroutines();
        _attacking = false;

        for (int i = 0; i < _roared.Length; i++) _roared[i] = false;
        _enrageCd = 1f;
        _enrageSpeed = 1f;
        for (int i = 0; i < _atkCd.Length; i++) _atkCd[i] = 0f;
        _beamCd = 0f; _rainCd = 0f; _novaCd = 0f; _dashCd = 0f; _ultCd = 0f; _guardCd = 0f; _meleeGapCd = 0f;

        // 돌진 중 리셋되면 에이전트가 꺼진 채 남는다 -> 되살린다
        if (_agent != null && !_agent.enabled) _agent.enabled = true;

        _motor.ResetToSpawn(_leash.SpawnPos, _leash.SpawnRot, data);
        BossHealthBarUI.Hide();
    }

    private void LateUpdate()
    {
        _motor.TickFacing(_dead || _attacking, ResolveTarget(), data);
    }
}
