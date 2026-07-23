using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 화염보스(용암/화염 고정형 보스) - 전용 상태머신.
// 와이번/얼음/모래와 같은 방식(BT/EnemyBrain 미사용, BossMotor/BossLeash 글루)이지만 ★고정형(이동 없음)이다.
//
// [정체성] 와이번=근접 / 얼음=원거리캐스터 / 모래=근접위치지배 / 화염보스=★제자리 아틸러리(포격형).
//   이동을 안 하므로 원거리 투사체 + 지면 폭발 + 유성비로 사방을 때려 플레이어를 옆으로 굴린다.
//   회피는 보스가 하는 게 아니라 플레이어가 빠지는 것. -> 고정형은 "어디든 닿는 광역(유성비)"이 없으면
//   플레이어가 빙빙 돌면 무력해진다. 그래서 유성비(시그니처)가 P2+ 핵심.
//
// [애니] ★멀티레이어: L0 body / L1 face_eyes / L2 face_mouse / L3 vortex(하반신 소용돌이, 상시).
//   BossMotor.PlayState 는 L0 전용. 얼굴/보텍스 레이어는 PlayLayer(state, layer)로 직접 재생.
//   자체 파티클 39개는 전부 playOnAwake + 클립이 on/off -> 개별 Play/Stop 안 하고 애니 상태만 재생.
//
// [등장/사망] body_appear=등장(자동, 서서히 불붙음) / body_disappear(8s)="Die" 상태(EnemyFeedback.PlayDeath 자동).
// [이동 없음] NavMeshAgent 자체가 불필요(BossMotor 가 null 에이전트를 전부 방어, GroundSpot 은 NavMesh 정적).
//   -> 에이전트 미부착 = 배치 시 "not close to navmesh" 경고 없음. Chase/StopMove/TickSpeedParam 미사용.
[RequireComponent(typeof(EnemyHealth))]
public class FireBossController : MonoBehaviour, IEnemyDataSource
{
    [Header("데이터 (HP/데미지/사거리 기본 수치는 SO에서 튜닝)")]
    [SerializeField] private MeleeEnemyData data;
    public MeleeEnemyData Data => data;   // 도감/드롭/퀘스트가 킬 인식(보스는 EnemyBrain 미사용)
    [SerializeField] private string bossSubtitle = "꺼지지 않는 시간의 잿불";

    [Header("등장 (body_appear 재생 동안 공격 잠금)")]
    [SerializeField] private float appearTime = 6.3f;

    [Header("파이어볼 견제 (body_attack_002 - 원거리 주력. WyvernFireball 재사용)")]
    [SerializeField] private GameObject fireballPrefab;      // 빌더가 조립(VFX_Fireball + WyvernFireball)
    [SerializeField] private float fireballRange = 35f;      // aggro(visionRange)와 맞춰 무공격 데드밴드 방지
    [SerializeField] private float fireballCooldown = 4f;
    [SerializeField] private float fireballWindup = 0.9f;
    [SerializeField] private float fireballRecover = 0.6f;
    [SerializeField] private int fireballBurstBase = 1;
    [SerializeField] private int fireballBurstPerPhase = 1; // P1=1 / P2=2 / P3=3
    [SerializeField] private float fireballBurstGap = 0.25f;
    [Tooltip("한 발당 부채꼴로 몇 발 흩뿌릴지. 1이면 예전처럼 한 발만 나간다.")]
    [SerializeField] private int fireballSpread = 3;
    [Tooltip("부채꼴 좌우 간격(도).")]
    [SerializeField] private float fireballSpreadAngle = 15f;
    [Tooltip("파편 발사 위치(망치/손 높이). 비우면 아래 fireballOffset 사용. 머리 앵커를 쓰면 타점이 너무 높다.")]
    [SerializeField] private Transform fireballAnchor;
    [Tooltip("발사 원점(로컬). 루트 스케일이 곱해지므로 모델 원본 기준으로 넣는다.")]
    [SerializeField] private Vector3 fireballOffset = new Vector3(0f, 8f, 4f);
    [Tooltip("발사 원점 로컬 오프셋(입/손). chargeAnchor 있으면 그게 우선.")]
    [SerializeField] private Vector3 fireOffset = new Vector3(0f, 1.5f, 1f);

    [Header("지면 배러지 (body_attack_001 - 여기저기 지면 폭발. 와이번 분출 이식)")]
    [SerializeField] private GameObject eruptionVfx;         // VFX_Explosion_Floor
    [SerializeField] private float eruptionCooldown = 6f;
    [SerializeField] private float eruptionWindup = 1.0f;
    [SerializeField] private float eruptionTelegraph = 0.9f;
    [SerializeField] private float eruptionRecover = 0.6f;
    [SerializeField] private float eruptionRadius = 3f;
    [SerializeField] private float eruptionSpread = 7f;
    [SerializeField] private float eruptionDmgMul = 0.9f;
    [SerializeField] private int eruptCountBase = 3;
    [SerializeField] private int eruptCountPerPhase = 2;     // P1=3 / P2=5 / P3=7
    [SerializeField] private float eruptGap = 0.18f;

    [Header("유성비 (body_attack_003 - ★시그니처. P2+, 어디든 낙하로 카이팅 봉쇄)")]
    [SerializeField] private GameObject meteorVfx;           // 낙하선(VFX_Fireball_Projectile_Static)
    [SerializeField] private GameObject meteorImpactVfx;     // 착탄(VFX_Explosion_Floor)
    [SerializeField] private float meteorRange = 40f;
    [SerializeField] private float meteorCooldown = 10f;
    [SerializeField] private float meteorWindup = 1.2f;
    [SerializeField] private float meteorRecover = 0.8f;
    [SerializeField] private int meteorCountBase = 6;        // P2 기본
    [SerializeField] private int meteorCountPerPhase = 3;    // P3 = base+per
    [SerializeField] private float meteorSpread = 8f;
    [SerializeField] private float meteorFallHeight = 14f;
    [SerializeField] private float meteorFallTime = 0.7f;
    [SerializeField] private float meteorRadius = 2.5f;
    [SerializeField] private float meteorGap = 0.15f;
    [SerializeField] private float meteorDmgMul = 0.8f;

    [Header("근접 화염 강타 (body_attack_004 - 붙었을 때 정면 부채꼴)")]
    [SerializeField] private GameObject meleeImpactVfx;      // VFX_Explosion_Omni
    [SerializeField] private float meleeCooldown = 3f;
    [SerializeField] private float meleeWindup = 1.0f;
    [SerializeField] private float meleeRecover = 0.6f;
    [SerializeField] private float meleeHalfAngle = 60f;
    [SerializeField] private float meleeReachMul = 1.2f;
    [SerializeField] private float meleeDmgMul = 1.4f;

    [Header("전방 연쇄 분출 (body_attack_005 - 바닥 훑는 직선 광역)")]
    [SerializeField] private GameObject forwardVfx;          // Chain explosions forward
    [SerializeField] private float forwardRange = 11f;
    [SerializeField] private float forwardCooldown = 8f;
    [SerializeField] private float forwardWindup = 1.0f;
    [SerializeField] private float forwardTelegraph = 0.7f;
    [SerializeField] private float forwardRecover = 0.7f;
    [SerializeField] private float forwardLength = 12f;
    [SerializeField] private float forwardHalfWidth = 2.5f;
    [SerializeField] private float forwardDmgMul = 1.2f;

    [Header("궁극 화염 브레스 (body_attack_006 - P3+, 정면 원뿔 지속)")]
    [SerializeField] private GameObject breathVfx;           // VFX_Fire 원뿔 보강(선택)
    [SerializeField] private float breathRange = 16f;
    [SerializeField] private float breathCooldown = 14f;
    [SerializeField] private float breathWindup = 1.5f;
    [SerializeField] private float breathDuration = 2.5f;
    [SerializeField] private float breathTickInterval = 0.3f;
    [SerializeField] private float breathHalfAngle = 35f;
    [SerializeField] private float breathTurnRate = 45f;     // 브레스 중 느린 조준(회피 여지 + 서있으면 처벌)
    [SerializeField] private float breathRecover = 1.2f;
    [SerializeField] private float breathDmgMul = 0.5f;      // 틱당

    [Header("잔불 장판 (P3 - 유성/배러지 착탄이 남기는 용암. 독립 티커)")]
    [SerializeField] private GameObject lavaVfx;             // VFX_Fire
    [SerializeField] private float lavaDuration = 3f;
    [SerializeField] private float lavaTick = 0.5f;
    [SerializeField] private float lavaRadius = 2.5f;
    [SerializeField] private float lavaDmgMul = 0.3f;

    [Header("범위 텔레그래프 (지면 예고)")]
    [SerializeField] private GameObject telegraphVfx;        // Wyvern_Telegraph 복제 + 주황색
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
    [SerializeField] private float enrageCdMul = 0.82f;      // 포효마다 공격 쿨 x (누적)

    [Header("전조 앵커 (입/머리 본. 파이어볼/브레스 원점)")]
    [SerializeField] private Transform chargeAnchor;
    [Tooltip("임팩트가 뜰 플레이어 몸 높이 보정.")]
    [SerializeField] private float impactHeight = 1.0f;

    [Header("애니메이터 상태명 (L0 body)")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string eruptionState = "Attack1";
    [SerializeField] private string fireballState = "Attack2";
    [SerializeField] private string meteorState = "Attack3";
    [SerializeField] private string meleeState = "Attack4";
    [SerializeField] private string forwardState = "Attack5";
    [SerializeField] private string breathState = "Attack6";

    [Header("애니메이터 얼굴 레이어 (L1 eyes / L2 mouth)")]
    [SerializeField] private int eyesLayer = 1;
    [SerializeField] private int mouthLayer = 2;
    [SerializeField] private string faceEyesAwake = "Eyes3";   // face_eyes_003
    [SerializeField] private string faceMouthOpen = "Mouth1";  // face_mouse_001
    [SerializeField] private string faceEyesStay = "EyesStay";
    [SerializeField] private string faceMouthStay = "MouthStay";

    [Header("패턴 선택 (가중 랜덤)")]
    [SerializeField] private float wFireball = 1.4f;
    [SerializeField] private float wEruption = 1.3f;
    [SerializeField] private float wMelee = 1.6f;
    [SerializeField] private float wForward = 1.1f;
    [SerializeField] private float wMeteor = 1.3f;      // 시그니처(P2+)
    [SerializeField] private float wBreath = 1.4f;      // 궁극(P3+)
    [Range(0f, 1f)] [SerializeField] private float repeatPenalty = 0.35f;

    [Header("리쉬 (이탈 리셋 - 고정형이라 이동복귀는 no-op, HP/페이즈 리셋만)")]
    [SerializeField] private float leashDistance = 45f;
    [SerializeField] private float leashResetTime = 4f;

    private static readonly float[] RoarThresholds = { 0.66f, 0.33f };

    private enum AtkType { None, Fireball, Eruption, Melee, Forward, Meteor, Breath }
    private AtkType _lastAttack = AtkType.None;
    private readonly List<(AtkType type, float weight)> _cand = new List<(AtkType, float)>();

    private BossMotor _motor;
    private BossLeash _leash;

    private EnemyHealth _health => _motor.Health;
    private EnemyFeedback _feedback => _motor.Feedback;
    private Transform _player => _motor.Player;
    private PlayerStatComponent _playerStat => _motor.PlayerStat;

    private bool _dead;
    private bool _attacking;
    private bool _engaged;
    private bool _appearing;
    private float _fireballCd, _eruptCd, _meleeCd, _forwardCd, _meteorCd, _breathCd;
    private bool[] _roared;
    private float _enrageCd = 1f;

    private void Awake()
    {
        _motor = new BossMotor(this, speedParam);
        _leash = new BossLeash(leashDistance, leashResetTime);
        _roared = new bool[RoarThresholds.Length];
        _motor.ApplyData(data);
    }

    private void Start()
    {
        _leash.Capture(transform);
        _motor.AcquirePlayer();
        if (_health != null) _health.OnDeath += HandleDeath;
        _feedback?.PlaySpawn();
        _appearing = true;
        StartCoroutine(AppearGate());
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnDeath -= HandleDeath;
    }

    // 등장(body_appear) 재생 동안 공격 잠금. 애니는 컨트롤러 default 상태로 자동 재생됨.
    private IEnumerator AppearGate()
    {
        yield return new WaitForSeconds(appearTime);
        _appearing = false;
    }

    private void HandleDeath()
    {
        _dead = true;
        StopAllCoroutines();
        _attacking = false;
        // 사망이 브레스/유성/포효 도중이면 입/눈이 열린 채 굳는다 -> 중립 복구(사망 애니는 EnemyFeedback.PlayDeath 가 "Die").
        PlayLayer(faceMouthStay, mouthLayer);
        PlayLayer(faceEyesStay, eyesLayer);
    }

    private void Update()
    {
        if (_dead || _appearing) return;
        if (_player == null) _motor.AcquirePlayer();

        if (_engaged && _player != null && _leash.Tick(_motor.PlanarDistance(_player.position)))
            ResetBoss();

        TickCooldowns();
        CheckRoarPhase();
        if (_attacking) return;

        Transform target = ResolveTarget();
        if (target != null && !_engaged)
        {
            _engaged = true;
            BossHealthBarUI.Show(_health, data != null ? data.enemyName : "화염정령", bossSubtitle);
            _feedback?.PlayDetect();
        }
        if (target == null) return;   // 고정형 = 추격 없음. 애니는 exitTime 으로 알아서 Idle 복귀(매프레임 crossfade 금지)
        if (data == null) return;

        float dist = _motor.PlanarDistance(target.position);
        int phase = RoarsDone();   // 0=P1 / 1=P2 / 2=P3
        float meleeMax = data.attackRange * meleeReachMul;

        _cand.Clear();
        if (phase >= 2 && _breathCd <= 0f && dist <= breathRange)
            _cand.Add((AtkType.Breath, wBreath));
        if (phase >= 1 && _meteorCd <= 0f && dist <= meteorRange)
            _cand.Add((AtkType.Meteor, wMeteor));   // ★시그니처: 어디든 낙하
        if (_forwardCd <= 0f && dist <= forwardRange && dist > meleeMax)
            _cand.Add((AtkType.Forward, wForward));
        if (_meleeCd <= 0f && dist <= meleeMax)
            _cand.Add((AtkType.Melee, wMelee));
        if (_eruptCd <= 0f && dist <= fireballRange)
            _cand.Add((AtkType.Eruption, wEruption));
        // 근접 사거리 안에서는 파편을 쏘지 않는다. 코앞에서 부채꼴 전탄을 맞으면 피할 수가 없다.
        if (_fireballCd <= 0f && fireballPrefab != null && dist <= fireballRange && dist > meleeMax)
            _cand.Add((AtkType.Fireball, wFireball));

        if (_cand.Count > 0)
        {
            AtkType pick = WeightedPick();
            _lastAttack = pick;
            switch (pick)
            {
                case AtkType.Breath:   StartCoroutine(Breath()); break;
                case AtkType.Meteor:   StartCoroutine(MeteorRain()); break;
                case AtkType.Forward:  StartCoroutine(ForwardEruption()); break;
                case AtkType.Melee:    StartCoroutine(MeleeSmash()); break;
                case AtkType.Eruption: StartCoroutine(Eruption()); break;
                case AtkType.Fireball: StartCoroutine(FireballVolley()); break;
            }
            return;
        }
        // 전부 쿨: 제자리 대기(TickFacing 이 플레이어 조준 유지). 애니는 exitTime 으로 Idle 유지 -> 매프레임 crossfade 안 함.
    }

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
        if (_fireballCd > 0f) _fireballCd -= dt;
        if (_eruptCd > 0f) _eruptCd -= dt;
        if (_meleeCd > 0f) _meleeCd -= dt;
        if (_forwardCd > 0f) _forwardCd -= dt;
        if (_meteorCd > 0f) _meteorCd -= dt;
        if (_breathCd > 0f) _breathCd -= dt;
    }

    private Transform ResolveTarget()
    {
        if (_player == null) return null;
        if (_playerStat != null && (_playerStat.IsDead || _playerStat.IsInBase)) return null;
        float aggro = data != null ? Mathf.Max(data.visionRange, fireballRange) : 30f;
        if (_motor.PlanarDistance(_player.position) > aggro) return null;
        return _player;
    }

    private int RoarsDone()
    {
        int n = 0;
        for (int i = 0; i < _roared.Length; i++) if (_roared[i]) n++;
        return n;
    }

    // ==== 패턴 ====

    // 파이어볼 견제: 방향 커밋 -> 발사(페이즈별 연사). WyvernFireball 재사용.
    private IEnumerator FireballVolley()
    {
        _attacking = true;
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(fireballState);
        yield return new WaitForSeconds(fireballWindup);

        int shots = fireballBurstBase + RoarsDone() * fireballBurstPerPhase;   // P1=1 / P2=2 / P3=3
        for (int i = 0; i < shots && !_dead; i++)
        {
            if (i > 0 && _player != null) _motor.FaceInstant(_player.position);
            SpawnFireball();
            if (i < shots - 1) yield return new WaitForSeconds(fireballBurstGap);
        }

        yield return new WaitForSeconds(fireballRecover);
        _fireballCd = fireballCooldown * _enrageCd;
        _attacking = false;
    }

    // 파편 발사 원점. 브레스는 입(머리 앵커)이 맞지만 파편은 망치질이라 머리에서 나가면 타점이 붕 뜬다.
    private Vector3 FireballPoint()
        => fireballAnchor != null ? fireballAnchor.position : transform.TransformPoint(fireballOffset);

    private void SpawnFireball()
    {
        if (fireballPrefab == null || _player == null) return;

        Vector3 origin = FireballPoint();
        Vector3 aim = (_player.position + Vector3.up) - origin;
        if (aim.sqrMagnitude < 0.0001f) aim = transform.forward;

        int n = Mathf.Max(1, fireballSpread);
        float dmg = data != null ? data.attackDamage : 20f;

        for (int i = 0; i < n; i++)
        {
            // 가운데 0 기준 좌우 대칭. n=3, 15도면 -15 / 0 / +15.
            float step = i - (n - 1) * 0.5f;
            Vector3 dir = Quaternion.AngleAxis(step * fireballSpreadAngle, Vector3.up) * aim;

            var go = Instantiate(fireballPrefab, origin, Quaternion.LookRotation(dir));
            var fb = go.GetComponent<WyvernFireball>();
            if (fb == null) continue;

            fb.Launch(dir, dmg, _player);
            // 유도는 가운데 한 발만. 부채꼴 전탄이 따라오면 근접 캐릭터는 피할 방법이 없다.
            bool isCenter = (n % 2 == 1) && i == n / 2;
            if (RoarsDone() >= 1 && isCenter) fb.SetHoming(true);
        }
    }

    // 지면 배러지: 플레이어 자리 + 주변 여기저기 순차 폭발(페이즈별 발수 up). 와이번 분출 이식.
    private IEnumerator Eruption()
    {
        _attacking = true;
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(eruptionState);
        yield return new WaitForSeconds(eruptionWindup);

        int count = eruptCountBase + RoarsDone() * eruptCountPerPhase;   // P1=3 / P2=5 / P3=7
        for (int i = 0; i < count && !_dead; i++)
        {
            Vector3 spot = (i == 0 && _player != null) ? _player.position : ChooseEruptSpot();
            spot = GroundSpot(spot);
            StartCoroutine(SingleEruption(spot));
            if (i < count - 1) yield return new WaitForSeconds(eruptGap);
        }

        yield return new WaitForSeconds(eruptionTelegraph + eruptionRecover);
        _eruptCd = eruptionCooldown * _enrageCd;
        _attacking = false;
    }

    // 한 발: 텔레그래프 -> 폭발 + 반경 데미지. P3면 잔불 장판을 남긴다.
    private IEnumerator SingleEruption(Vector3 spot)
    {
        SpawnTelegraph(spot, eruptionRadius, eruptionTelegraph);
        yield return new WaitForSeconds(eruptionTelegraph);
        if (_dead) yield break;
        if (eruptionVfx != null) { var v = Instantiate(eruptionVfx, spot, Quaternion.identity); Destroy(v, 3f); }
        DealAreaDamage(spot, eruptionRadius, data.attackDamage * eruptionDmgMul);
        if (RoarsDone() >= 2) SpawnLavaPool(spot);
    }

    private Vector3 ChooseEruptSpot()
    {
        Vector3 baseP = _player != null ? _player.position : transform.position;
        Vector2 r = Random.insideUnitCircle * eruptionSpread;
        return baseP + new Vector3(r.x, 0f, r.y);
    }

    // ★유성비(시그니처): 플레이어 주변 사방에 하늘에서 불덩이가 낙하. 어디로 빠져도 낙하가 쫓아온다.
    private IEnumerator MeteorRain()
    {
        _attacking = true;
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(meteorState);
        PlayLayer(faceMouthOpen, mouthLayer);
        PlayLayer(faceEyesAwake, eyesLayer);
        yield return new WaitForSeconds(meteorWindup);

        // P2(phase1)=base / P3(phase2)=base+per
        int count = meteorCountBase + Mathf.Max(0, RoarsDone() - 1) * meteorCountPerPhase;
        for (int i = 0; i < count && !_dead; i++)
        {
            Vector3 center = _player != null ? _player.position : transform.position;
            Vector2 r = Random.insideUnitCircle * meteorSpread;
            Vector3 spot = GroundSpot(center + new Vector3(r.x, 0f, r.y));
            StartCoroutine(MeteorDrop(spot));
            yield return new WaitForSeconds(meteorGap);
        }

        PlayLayer(faceMouthStay, mouthLayer);
        PlayLayer(faceEyesStay, eyesLayer);
        yield return new WaitForSeconds(meteorRecover);
        _meteorCd = meteorCooldown * _enrageCd;
        _attacking = false;
    }

    // 유성 한 발: 지면 예고 -> 하늘에서 낙하선 lerp -> 착탄 폭발 + 반경 데미지 (+P3 잔불)
    private IEnumerator MeteorDrop(Vector3 spot)
    {
        SpawnTelegraph(spot, meteorRadius, meteorFallTime);
        Vector3 top = spot + Vector3.up * meteorFallHeight;
        GameObject m = null;
        if (meteorVfx != null)
        {
            m = Instantiate(meteorVfx, top, Quaternion.LookRotation(Vector3.down));
            Destroy(m, meteorFallTime + 1f);   // 코루틴 끊겨도 잔존 방지
        }

        float t = 0f;
        while (t < meteorFallTime && !_dead)
        {
            t += Time.deltaTime;
            if (m != null) m.transform.position = Vector3.Lerp(top, spot, t / meteorFallTime);
            yield return null;
        }
        if (m != null) Destroy(m);
        if (_dead) yield break;

        if (meteorImpactVfx != null) { var v = Instantiate(meteorImpactVfx, spot, Quaternion.identity); Destroy(v, 3f); }
        DealAreaDamage(spot, meteorRadius, data.attackDamage * meteorDmgMul);
        if (RoarsDone() >= 2) SpawnLavaPool(spot);
    }

    // 근접 화염 강타: 붙었을 때 정면 부채꼴 1타.
    private IEnumerator MeleeSmash()
    {
        _attacking = true;
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(meleeState);
        yield return new WaitForSeconds(meleeWindup);

        if (!_dead && _motor.PlayerInArc(data.attackRange * meleeReachMul, meleeHalfAngle))
        {
            DealDamage(data.attackDamage * meleeDmgMul);
            SpawnImpact(meleeImpactVfx, PlayerHitPos());
        }

        yield return new WaitForSeconds(meleeRecover);
        _meleeCd = meleeCooldown * _enrageCd;
        _attacking = false;
    }

    // 전방 연쇄 분출: 정면 직선(길이 forwardLength, 반폭 forwardHalfWidth)을 훑는 광역.
    private IEnumerator ForwardEruption()
    {
        _attacking = true;
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(forwardState);
        yield return new WaitForSeconds(forwardWindup);

        if (!_dead)
        {
            Vector3 dir = transform.forward;
            Vector3 origin = GroundSpot(transform.position + dir * 2f);
            if (forwardVfx != null)
            {
                var v = Instantiate(forwardVfx, origin, Quaternion.LookRotation(dir));
                Destroy(v, 3f);
            }
        }

        yield return new WaitForSeconds(forwardTelegraph);
        if (!_dead && PlayerInForwardLine(forwardLength, forwardHalfWidth))
            DealDamage(data.attackDamage * forwardDmgMul);

        yield return new WaitForSeconds(forwardRecover);
        _forwardCd = forwardCooldown * _enrageCd;
        _attacking = false;
    }

    // 궁극 화염 브레스(P3): 정면 원뿔로 지속 틱. 중에 느리게 조준(회피 여지 + 서있으면 처벌).
    private IEnumerator Breath()
    {
        _attacking = true;
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(breathState);
        PlayLayer(faceMouthOpen, mouthLayer);
        PlayLayer(faceEyesAwake, eyesLayer);
        yield return new WaitForSeconds(breathWindup);

        GameObject cone = null;
        if (breathVfx != null)
        {
            cone = Instantiate(breathVfx, FirePoint(), transform.rotation, transform);
            Destroy(cone, breathDuration + breathRecover + 0.5f);   // 코루틴 끊겨도(리쉬/사망) 잔존 방지
        }

        float t = 0f, tick = 0f;
        while (t < breathDuration && !_dead)
        {
            t += Time.deltaTime; tick += Time.deltaTime;
            if (_player != null)   // 느린 조준(원뿔이 서서히 플레이어를 훑음)
            {
                Vector3 to = _player.position - transform.position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), breathTurnRate * Time.deltaTime);
            }
            if (tick >= breathTickInterval)
            {
                tick = 0f;
                if (_motor.PlayerInArc(breathRange, breathHalfAngle))
                    DealDamage(data.attackDamage * breathDmgMul);
            }
            yield return null;
        }
        if (cone != null) Destroy(cone);

        PlayLayer(faceMouthStay, mouthLayer);
        PlayLayer(faceEyesStay, eyesLayer);
        yield return new WaitForSeconds(breathRecover);
        _breathCd = breathCooldown * _enrageCd;
        _attacking = false;
    }

    // 잔불 장판(P3): 착탄 지점에 용암 존. 독립 티커로 지속 동안 반경 안 틱(보스와 분리).
    private void SpawnLavaPool(Vector3 spot)
    {
        GameObject v = lavaVfx != null ? Instantiate(lavaVfx, spot, Quaternion.identity) : null;
        if (v != null) Destroy(v, lavaDuration + 0.5f);
        StartCoroutine(LavaPool(spot, v));
    }

    private IEnumerator LavaPool(Vector3 spot, GameObject v)
    {
        float t = 0f, tick = 0f;
        while (t < lavaDuration && !_dead)
        {
            t += Time.deltaTime; tick += Time.deltaTime;
            if (tick >= lavaTick)
            {
                tick = 0f;
                DealAreaDamage(spot, lavaRadius, data.attackDamage * lavaDmgMul);
            }
            yield return null;
        }
        if (v != null) Destroy(v);
    }

    // ==== 포효/페이즈 ====

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
            StartCoroutine(RoarPhase());
            return;
        }
    }

    private IEnumerator RoarPhase()
    {
        _attacking = true;
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(roarState);
        PlayLayer(faceMouthOpen, mouthLayer);
        PlayLayer(faceEyesAwake, eyesLayer);
        yield return new WaitForSeconds(roarBuildup);

        if (!_dead)
        {
            BossRoarDebuff.Trigger(debuffDuration, debuffDrainMult, debuffDarkness,
                                   debuffVignetteStrength, debuffVignetteFalloff);
            _enrageCd *= enrageCdMul;   // 공격 쿨 누적 감소(이동 없으니 이속 광폭화는 없음)
        }

        yield return new WaitForSeconds(roarRecover);
        PlayLayer(faceMouthStay, mouthLayer);
        PlayLayer(faceEyesStay, eyesLayer);
        _attacking = false;
    }

    // ==== 헬퍼 ====

    // 얼굴/보텍스 등 비-Body 레이어 직접 재생(BossMotor.PlayState 는 L0 전용).
    private void PlayLayer(string state, int layer)
    {
        var anim = _motor.Animator;
        if (anim != null && !string.IsNullOrEmpty(state) && layer > 0)
            anim.CrossFadeInFixedTime(state, 0.1f, layer);
    }

    // 발사/브레스 원점: 앵커 본 있으면 거기, 없으면 fireOffset 로컬 위치.
    private Vector3 FirePoint()
        => chargeAnchor != null ? chargeAnchor.position : transform.TransformPoint(fireOffset);

    private bool PlayerInForwardLine(float length, float halfWidth)
    {
        if (_player == null) return false;
        Vector3 to = _player.position - transform.position; to.y = 0f;
        float fwd = Vector3.Dot(to, transform.forward);
        if (fwd < 0f || fwd > length) return false;
        float side = Vector3.Dot(to, transform.right);
        return Mathf.Abs(side) <= halfWidth;
    }

    private Vector3 GroundSpot(Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out var nav, 5f, NavMesh.AllAreas))
            return nav.position;
        return pos;
    }

    private void SpawnTelegraph(Vector3 pos, float radius, float life)
    {
        if (telegraphVfx == null) return;
        var t = Instantiate(telegraphVfx, pos + Vector3.up * 0.05f, Quaternion.identity);
        t.transform.localScale = Vector3.one * radius * telegraphScaleMul;
        Destroy(t, life + 0.1f);
    }

    private void SpawnImpact(GameObject vfx, Vector3 worldPos, float scale = 1f)
    {
        if (vfx == null) return;
        var g = Instantiate(vfx, worldPos, Quaternion.identity);
        if (scale != 1f) g.transform.localScale = Vector3.one * scale;
        Destroy(g, 3f);
    }

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
        StopAllCoroutines();
        _attacking = false;

        for (int i = 0; i < _roared.Length; i++) _roared[i] = false;
        _enrageCd = 1f;
        _fireballCd = 0f; _eruptCd = 0f; _meleeCd = 0f; _forwardCd = 0f; _meteorCd = 0f; _breathCd = 0f;

        _motor.ResetToSpawn(_leash.SpawnPos, _leash.SpawnRot, data);   // 고정형이라 사실상 HP/회전 리셋
        _motor.PlayState(idleState);
        BossHealthBarUI.Hide();
    }

    private void LateUpdate()
    {
        // 이동은 없지만 제자리 회전으로 플레이어 조준(부채꼴/직선/원뿔 판정 유효). 공격/사망/등장 중엔 정지.
        _motor.TickFacing(_dead || _attacking || _appearing, ResolveTarget(), data);
    }
}
