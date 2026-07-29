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
    [SerializeField] private float beamDmgMul = 0.30f;      // x attackDamage (틱당). 지속 추적빔이라 낮게. [07-29] 압박 주력다운 무게로 0.22 -> 0.30
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
    [Tooltip("[07-29] 고드름 크기 배율(비주얼 + 판정 반경 동시). 1.5 = 50% 큼 -> 자연히 위협적.")]
    [SerializeField] private float rainShardScale = 1.5f;

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
    [Tooltip("[07-29] 돌진 관통 반경 배율(다단 돌진 재조준 빗맞음 보정).")]
    [SerializeField] private float dashRadiusMul = 1.3f;
    [Tooltip("[07-29] 다단 돌진 사이 재조준 텀(초).")]
    [SerializeField] private float dashRepassGap = 0.3f;
    [Tooltip("[07-29] 돌진 횟수(확확확). 매 회 재조준하며 관통. 4~5면 대쉬로 여러 번 피하는 맛.")]
    [SerializeField] private int dashPasses = 4;

    [Header("궁극 (Cast3 + NuclearBomb_Frost - 페이즈3 전용)")]
    [SerializeField] private GameObject ultVfx;
    [SerializeField] private float ultRange = 16f;
    [SerializeField] private float ultCooldown = 11f;
    [SerializeField] private float ultWindup = 1.4f;        // Cast3 = 양팔 대시전(긴 예고)
    [SerializeField] private float ultTelegraph = 1.0f;
    [SerializeField] private float ultRecover = 1.2f;
    [SerializeField] private float ultRadius = 9f;
    [SerializeField] private float ultDmgMul = 2.6f;
    [Header("[07-29] 궁극 배러지 (용성군 - 플레이어 주변 추적 낙하 후 중앙 대폭발)")]
    [SerializeField] private int ultMeteorCount = 8;
    [SerializeField] private float ultMeteorSpread = 6f;
    [SerializeField] private float ultMeteorGap = 0.12f;
    [SerializeField] private float ultMeteorRadius = 2.6f;
    [SerializeField] private float ultMeteorDmgMul = 0.7f;
    [SerializeField] private float ultMeteorFall = 0.45f;

    [Header("가드 반격 (Block - 근접 압박 시 무적 후 광역 반격)")]
    [SerializeField] private float guardRange = 4f;         // 이 안에 플레이어가 있고
    [SerializeField] private float guardCooldown = 6f;      // 쿨이 차면 가드
    [SerializeField] private float guardDuration = 1.0f;    // 무적 시간(Block 클립 길이)
    [SerializeField] private string guardCounterState = "Attack2";   // 가드 후 반격(광역 스윙)
    [SerializeField] private float guardCounterWindup = 0.5f;
    [SerializeField] private float guardCounterDmgMul = 1.5f;
    [Tooltip("[07-29] 가드 무적 동안 두르는 얼음 방패 VFX. 비우면 chargeVfxBody(냉기 오라)로 폴백 = 별도 에셋 없이도 읽힘.")]
    [SerializeField] private GameObject guardVfx;

    [Header("[07-29] 혹한 Whiteout (P3 진입 시그니처. 판을 얼려 배러지+노바 폭풍 -> 대폭발)")]
    [SerializeField] private float whiteoutDuration = 4f;
    [SerializeField] private float whiteoutMeteorGap = 0.2f;
    [SerializeField] private float whiteoutSpread = 6f;
    [SerializeField] private float whiteoutMeteorRadius = 2.6f;
    [SerializeField] private float whiteoutMeteorDmgMul = 0.6f;
    [SerializeField] private float whiteoutMeteorFall = 0.4f;
    [SerializeField] private float whiteoutNovaGap = 1.3f;
    [SerializeField] private float whiteoutFinaleRadius = 10f;
    [SerializeField] private float whiteoutFinaleDmgMul = 2f;

    [Header("[07-29] 빙경 분신 Mirror (P2+ - 반투명 유령 분신 + 가짜 예고. 진짜만 실체=피해/피격)")]
    [Tooltip("진짜 포함 총 개수(진짜 1 + 가짜 나머지). 2~4.")]
    [SerializeField] private int mirrorCount = 3;
    [SerializeField] private float mirrorRange = 22f;       // 이 사거리 안이면 사용
    [SerializeField] private float mirrorCooldown = 13f;
    [SerializeField] private float mirrorWindup = 0.9f;     // 분열 전 시전
    [SerializeField] private float mirrorRadius = 5.5f;     // 플레이어 중심 배치 반경
    [SerializeField] private float mirrorTelegraph = 0.9f;  // 예고 -> 발동
    [SerializeField] private float mirrorHoldTime = 2.2f;   // 발동 후 분신 유지(진짜 찾아 딜 넣는 시간)
    [SerializeField] private float mirrorRecover = 0.7f;
    [SerializeField] private float mirrorBurstRadius = 6.5f;
    [SerializeField] private float mirrorDmgMul = 1.6f;     // 진짜 버스트 데미지
    [Tooltip("분신 색조(가짜 tell). 진짜는 원본색, 가짜는 이 서리색으로 물들어 유령처럼 보인다.")]
    [SerializeField] private Color mirrorPhantomTint = new Color(0.45f, 0.75f, 1f, 1f);
    [Tooltip("분신/재조립 붕괴(산산조각) VFX. 비우면 impactVfxMelee(얼음조각) 폴백.")]
    [SerializeField] private GameObject shatterVfx;

    [Header("[07-29] 산산조각 & 재조립 (P1 체력 0 -> 죽는 대신 부서졌다 풀피로 재조립하며 P2 진입)")]
    [SerializeField] private float shatterHang = 2f;         // 몸이 부서진 채 비산하는 시간 ([07-29] 테스트용 느리게. 나중에 0.5 권장)
    [SerializeField] private float shatterBurstRadius = 7f;  // 부서질 때 그 자리 AOE
    [SerializeField] private float shatterDmgMul = 1.4f;
    [SerializeField] private float reformDistance = 7f;      // 플레이어에서 이만큼 떨어진 곳에 재조립
    [SerializeField] private float reformTelegraph = 3f;     // 재조립 예고(서리 수렴 + HP 차오름) ([07-29] 테스트용 느리게. 나중에 1.1 권장)
    [SerializeField] private float reformSlamRadius = 7f;    // 재조립 강타 반경
    [SerializeField] private float reformSlamDmgMul = 2f;
    [SerializeField] private float reformRecover = 0.7f;
    [Tooltip("[07-29] P1 최대 체력 = SO 최대치 x 이 값. 테스트용 0.1(10%)로 전환 빨리 확인. P2 는 항상 풀 최대치.")]
    [SerializeField] private float p1HpMul = 0.1f;
    [Tooltip("[07-29] P2(재조립 후) 몸 크기 배율. 1.25 = 25% 큼. 강화형 차별화.")]
    [SerializeField] private float p2ScaleMul = 1.25f;
    [Tooltip("[07-29] P2 몸 색조(발열되듯 빨갛게). 원본 색에 곱해진다. 설산 눈밭이라 빨강이 확 티남.")]
    [SerializeField] private Color p2Tint = new Color(1f, 0.3f, 0.2f);
    [Tooltip("[07-29] P2 발열 글로우 색. 보스에 빨간 점광원을 켜서 몸+주변 눈을 빨갛게 물들임(재질 발광 무관하게 확실히 빛남).")]
    [SerializeField] private Color p2GlowColor = new Color(1f, 0.25f, 0.1f);
    [SerializeField] private float p2GlowRange = 14f;
    [SerializeField] private float p2GlowIntensity = 5f;

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
    [Tooltip("근접 타격 임팩트(얼음 파편). ★[07-29] 이제 산산조각 전용. 근접 타격에는 안 씀(안 어울림).")]
    [SerializeField] private GameObject impactVfxMelee;
    [Tooltip("[07-29] 근접/가드/돌진 '타격' VFX(슬래시감). 얼음블록(impactVfxMelee)은 산산조각에만. 비우면 impactVfxRanged(서리 스플래시) 폴백. 여기에 슬래시 VFX 넣으면 그걸로.")]
    [SerializeField] private GameObject meleeHitVfx;
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
    [SerializeField] private float wMirror = 1.1f;   // [07-29] 빙경 분신
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
    private enum AtkType { None, Melee, Beam, Rain, Nova, Guard, Dash, Ult, Mirror }
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
    private float _beamCd, _rainCd, _novaCd, _dashCd, _ultCd, _guardCd, _meleeGapCd, _mirrorCd;
    private float[] _atkCd;
    private int _phase;            // [07-29] 0=P1 / 1=P2 (2페이즈. 포효 임계값 폐지)
    private bool _transitioning;   // 산산조각 재조립 전환 중
    private Renderer[] _bodyRenderers;   // 산산조각 때 껐다 켤 보스 몸 렌더러
    private float _baseMaxHp = 1f;       // [07-29] SO 원래 최대치(P2용). P1 은 이 값 x p1HpMul.
    private Vector3 _baseScale = Vector3.one;   // [07-29] P2 확대 전 기준 크기
    private Light _p2Light;                      // [07-29] P2 발열 글로우 점광원
    private float _enrageCd = 1f;
    private float _enrageSpeed = 1f;

    // [07-29] 빙경 분신: 유령 GameObject 목록 + 복제용 모델 자식 캐시. 코루틴이 끊겨도 유령이 안 남게 목록으로 관리.
    private readonly List<GameObject> _phantoms = new List<GameObject>();
    private Transform _modelRoot;
    private Vector3 _modelLossyScale = Vector3.one;
    private Quaternion _modelLocalRot = Quaternion.identity;

    private void Awake()
    {
        _motor = new BossMotor(this, speedParam);
        _leash = new BossLeash(leashDistance, leashResetTime);
        _atkCd = new float[MeleeAttacks.Length];
        _motor.ApplyData(data);
    }

    private void Start()
    {
        _leash.Capture(transform);
        _motor.AcquirePlayer();
        if (_health != null)
        {
            _health.OnDeath += HandleDeath;
            _health.LethalGuard = OnLethalDamage;   // [07-29] P1 체력 0 = 죽는 대신 산산조각->재조립->P2
            _baseMaxHp = _health.maxHP;             // SO 최대치 = P2 용
            ApplyP1Health();                        // P1 = 축소 체력바(테스트 10%)
        }
        _feedback?.PlaySpawn();
        _bodyRenderers = GetComponentsInChildren<Renderer>(true);   // 산산조각 때 껐다 켤 몸 렌더러
        _baseScale = transform.localScale;                          // [07-29] P2 확대 기준 크기
    }

    // P1 체력바 축소(SO 최대치 x p1HpMul). Start + 리쉬 리셋 시 호출.
    private void ApplyP1Health()
    {
        if (_health == null) return;
        _health.maxHP = _baseMaxHp * Mathf.Max(0.01f, p1HpMul);
        _health.currentHP = _health.maxHP;
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
        SetBodyVisible(true);   // [07-29] 혹시 전환 중 사망 시 몸 복구(사망 애니용)
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
        int phase = RoarsDone();   // 0=P1 / 1=P2 (2페이즈)
        float meleeMax = data.attackRange * MeleeMaxReachMul;

        // 조건(페이즈/쿨/거리) 맞는 공격을 전부 후보로 모은다. 거리 조건이 근접/원거리를
        // 자연스럽게 갈라주므로, 현재 위치에서 쓸 수 있는 것들 중에서만 뽑힌다.
        _cand.Clear();
        if (phase >= 1 && _ultCd <= 0f && ultVfx != null && dist <= ultRange)
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
        if (_mirrorCd > 0f) _mirrorCd -= dt;
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

    // [07-29] 2페이즈. 0=P1 / 1=P2. 기존 escalation 호출부(고드름 발수/노바 반경 등)가 이 값을 그대로 쓴다.
    private int RoarsDone() => _phase;

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
            SpawnImpact(meleeHitVfx != null ? meleeHitVfx : impactVfxRanged, PlayerHitPos());   // 얼음 파편(임팩트)
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
            StartCoroutine(DropIceShard(spot, fall, rainShardRadius * rainShardScale, rainDmgMul, rainShardScale));   // 스폰+낙하 후 판정(크기 배율 반영)
            yield return new WaitForSeconds(gap);
        }

        ReturnToLocomotion();   // Rain 상태에서 복귀
        yield return new WaitForSeconds(rainRecover);
        _rainCd = rainCooldown * _enrageCd;
        _attacking = false;
    }

    // 고드름 한 발: 낙하 VFX 스폰(크기 배율) -> 낙하 시간 뒤 그 지점 반경 판정.
    // [07-29] 낙하/궁극 배러지/혹한 공용. vScale 로 비주얼 크기를 키운다.
    private IEnumerator DropIceShard(Vector3 spot, float fall, float radius, float dmgMul, float vScale)
    {
        if (rainVfx != null)
        {
            var g = Instantiate(rainVfx, spot, Quaternion.identity);
            if (vScale != 1f) g.transform.localScale *= vScale;
        }
        yield return new WaitForSeconds(fall);
        if (_dead) yield break;
        DealAreaDamage(spot, radius, data.attackDamage * dmgMul);
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

        // [07-29] 가드 가독성: 무적 동안 얼음 방패를 크게 둘러 "지금 붙으면 손해"를 보이게 한다(폴백 = 냉기 오라).
        GameObject shieldFx = null;
        {
            var sv = guardVfx != null ? guardVfx : chargeVfxBody;
            if (sv != null)
            {
                shieldFx = Instantiate(sv, transform.position + Vector3.up * 1f, transform.rotation, transform);
                shieldFx.transform.localScale *= 1.7f;
            }
        }

        yield return new WaitForSeconds(guardDuration);
        if (_health != null) _health.Invulnerable = false;
        if (shieldFx != null) Destroy(shieldFx);

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
                SpawnImpact(meleeHitVfx != null ? meleeHitVfx : impactVfxRanged, PlayerHitPos());
            }
            yield return new WaitForSeconds(0.5f);
        }

        _guardCd = guardCooldown * _enrageCd;
        _attacking = false;
    }

    // 활강 돌진(시그니처): 몸 65도 눕힌 Fly 포즈로 플레이어 방향 관통.
    // [07-29] 다단 돌진 = P2 2회 / P3 3회(매 회 재조준). 한 번 옆스텝으로 못 흘린다.
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

        int passes = Mathf.Max(1, dashPasses);   // [07-29] 여러 번 돌진(확확확), 매 회 재조준
        float hitR = dashRadius * dashRadiusMul;

        // 에이전트를 끄고 직접 이동(관통). 끝나면 반드시 되살린다.
        bool agentWasOn = _agent != null && _agent.enabled;
        if (agentWasOn) _agent.enabled = false;

        for (int p = 0; p < passes && !_dead; p++)
        {
            if (_player != null) _motor.FaceInstant(_player.position);   // 매 회 재조준
            _motor.PlayState(dashState);
            _feedback?.PlaySound(dashSound);
            GameObject vfx = dashVfx != null ? Instantiate(dashVfx, transform.position, transform.rotation, transform) : null;
            if (vfx != null) Destroy(vfx, dashDuration + 0.5f);

            Vector3 dir = transform.forward;
            float t = 0f;
            bool hitOnce = false;
            while (t < dashDuration && !_dead)
            {
                t += Time.deltaTime;
                transform.position += dir * dashSpeed * Time.deltaTime;
                if (!hitOnce && _player != null
                    && Vector3.Distance(transform.position, _player.position) <= hitR)
                {
                    hitOnce = true;   // 한 돌진당 1회
                    DealDamage(data.attackDamage * dashDmgMul);
                    _feedback?.PlaySound(meleeImpactSound);
                    SpawnImpact(meleeHitVfx != null ? meleeHitVfx : impactVfxRanged, PlayerHitPos());   // 스치는 얼음 파편
                }
                yield return null;
            }
            if (vfx != null) Destroy(vfx);

            if (p < passes - 1)   // 다음 돌진 전 짧게 재조준 텀
            {
                float g = 0f;
                while (g < dashRepassGap && !_dead) { g += Time.deltaTime; yield return null; }
            }
        }

        // 에이전트 복구 + 네비메시 스냅(안 하면 공중/벽 밖에 남는다)
        if (agentWasOn && _agent != null)
        {
            _agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, 8f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
        }

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

    // 궁극(P3): 긴 예고 후 [07-29] 플레이어 주변으로 고드름 배러지(용성군, 매 발 현재위치 추적) -> 마무리 중앙 대폭발.
    private IEnumerator Ultimate()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(ultState);
        SpawnCharge(chargeVfxBody, transform.position + Vector3.up * 1.5f, ultWindup);   // 대형 응축(긴 예고)
        _feedback?.PlaySound(ultimateChargeSound);
        yield return new WaitForSeconds(ultWindup);

        if (_dead || _player == null) { _attacking = false; yield break; }

        // 1) 용성군 배러지: 플레이어 주변에 고드름이 연달아 쏟아진다(매 발 현재 위치 = 도망쳐도 따라옴).
        for (int i = 0; i < ultMeteorCount && !_dead; i++)
        {
            Vector3 center = _player != null ? _player.position : transform.position;
            Vector2 rr = Random.insideUnitCircle * ultMeteorSpread;
            Vector3 mspot = GroundSpot(center + new Vector3(rr.x, 0f, rr.y));
            StartCoroutine(DropIceShard(mspot, ultMeteorFall, ultMeteorRadius, ultMeteorDmgMul, 1.2f));
            yield return new WaitForSeconds(ultMeteorGap);
        }

        // 2) 마무리: 플레이어 자리 중앙 대폭발.
        if (!_dead && _player != null)
        {
            Vector3 spot = GroundSpot(_player.position);   // 지면으로 스냅
            SpawnTelegraph(spot, ultRadius);
            yield return new WaitForSeconds(ultTelegraph);
            if (!_dead)
            {
                _feedback?.PlaySound(ultimateBurstSound);
                if (ultVfx != null) Instantiate(ultVfx, spot, Quaternion.identity);
                DealAreaDamage(spot, ultRadius, data.attackDamage * ultDmgMul);
                SpawnImpact(impactVfxHeavy, spot + Vector3.up * 0.3f);   // 대형 착탄
            }
        }

        yield return new WaitForSeconds(ultRecover);
        _ultCd = ultCooldown * _enrageCd;
        _attacking = false;
    }

    // ================= [07-29] 빙경 분신 (Mirror) =================

    // 분신 복제용 모델 자식 캐시(스킨드 메시를 담은, 루트 하위 서브트리). Start 에서 1회.
    private void CachePhantomModel()
    {
        var smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr == null) { _modelRoot = null; return; }
        Transform t = smr.transform;
        while (t.parent != null && t.parent != transform) t = t.parent;   // 루트의 직속 자식(모델 서브트리)까지 올라감
        if (t == null || t == transform) { _modelRoot = null; return; }
        _modelRoot = t;
        _modelLossyScale = t.lossyScale;                                       // 보스 scale(2.2) 반영
        _modelLocalRot = Quaternion.Inverse(transform.rotation) * t.rotation;  // 모델 로컬 회전 오프셋 보존
    }

    // 반투명 서리 유령 1기: 모델 복제 -> 콜라이더 제거(진짜만 피격) -> 애니메이터 보장 -> 서리 틴트 + 오라.
    private GameObject SpawnPhantom(Vector3 pos, Quaternion faceRot)
    {
        if (_modelRoot == null) return null;
        var g = Instantiate(_modelRoot.gameObject, pos, faceRot * _modelLocalRot);
        g.transform.localScale = _modelLossyScale;

        foreach (var c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);   // 순수 시각 인형

        var anim = g.GetComponentInChildren<Animator>();
        if (anim == null) anim = g.AddComponent<Animator>();
        if (_motor.Animator != null)
        {
            anim.runtimeAnimatorController = _motor.Animator.runtimeAnimatorController;
            anim.avatar = _motor.Animator.avatar;
        }
        anim.applyRootMotion = false;

        // 가짜 tell: 서리색 틴트(MaterialPropertyBlock = 공유 재질 안 건드림) + 냉기 오라.
        var mpb = new MaterialPropertyBlock();
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", mirrorPhantomTint);
            mpb.SetColor("_Color", mirrorPhantomTint);   // 셰이더별 프로퍼티명 대비
            r.SetPropertyBlock(mpb);
        }
        if (chargeVfxBody != null)
        {
            var aura = Instantiate(chargeVfxBody, g.transform.position + Vector3.up, g.transform.rotation, g.transform);
            aura.transform.localScale *= 1.4f;
        }

        Destroy(g, mirrorWindup + mirrorTelegraph + mirrorHoldTime + 4f);   // 안전 자폭(정리 누락 대비)
        _phantoms.Add(g);
        return g;
    }

    private void PlayPhantomState(GameObject g, string state)
    {
        if (g == null || string.IsNullOrEmpty(state)) return;
        var anim = g.GetComponentInChildren<Animator>();
        if (anim != null) anim.CrossFadeInFixedTime(state, 0.1f, 0);
    }

    // 유령 전부 즉시 제거(코루틴 끊김/사망/포효/리셋 안전 정리).
    private void ClearPhantoms()
    {
        for (int i = 0; i < _phantoms.Count; i++)
            if (_phantoms[i] != null) Destroy(_phantoms[i]);
        _phantoms.Clear();
    }

    // 유령 붕괴(산산조각 VFX + 제거).
    private void ShatterPhantoms()
    {
        var vfx = shatterVfx != null ? shatterVfx : impactVfxHeavy;
        for (int i = 0; i < _phantoms.Count; i++)
        {
            var ph = _phantoms[i];
            if (ph == null) continue;
            if (vfx != null) { var v = Instantiate(vfx, ph.transform.position + Vector3.up * 0.5f, Quaternion.identity); Destroy(v, 3f); }
            Destroy(ph);
        }
        _phantoms.Clear();
        _feedback?.PlaySound(novaSound);
    }

    // 빙경 분신(P2+): 흩어져 반투명 유령 + 진짜(순간이동)를 플레이어 주변 원형으로 세운다.
    // 전원 같은 공격 예고를 흘리지만 [진짜 하나만] 실제 발동 + [진짜만] 피격된다.
    // tell = 진짜는 원본 외형(가짜는 서리색 유령). 진짜 공격을 읽어 피하고, 진짜를 찾아 딜.
    private IEnumerator MirrorSplit()
    {
        _attacking = true;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(ultState);
        SpawnCharge(chargeVfxBody, transform.position + Vector3.up * 1.5f, mirrorWindup);
        _feedback?.PlaySound(ultimateChargeSound);
        yield return new WaitForSeconds(mirrorWindup);
        if (_dead) { _attacking = false; yield break; }

        ClearPhantoms();   // 혹시 남은 이전 분신
        Vector3 pc = _player != null ? GroundSpot(_player.position) : transform.position;
        int total = Mathf.Clamp(mirrorCount, 2, 4);
        int realSlot = Random.Range(0, total);
        float baseAng = Random.value * 360f;

        Vector3 realPos = transform.position;
        for (int i = 0; i < total; i++)
        {
            float ang = baseAng + 360f / total * i;
            Vector3 slot = GroundSpot(pc + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * mirrorRadius);
            Vector3 face = pc - slot; face.y = 0f;
            Quaternion rot = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face) : transform.rotation;
            if (i == realSlot) realPos = slot;
            else SpawnPhantom(slot, rot);
        }

        // 진짜 순간이동("안 움직인 게 진짜"라는 tell 방지)
        if (_motor.AgentReady()) _agent.Warp(realPos);
        else transform.position = realPos;
        if (_player != null) _motor.FaceInstant(_player.position);

        yield return new WaitForSeconds(0.3f);   // 등장 텀

        // 가짜 예고: 전원(진짜 포함) 자리에 예고 링 + 공격 모션
        _motor.PlayState(novaState);
        SpawnTelegraph(GroundSpot(transform.position), mirrorBurstRadius);
        foreach (var ph in _phantoms)
        {
            if (ph == null) continue;
            SpawnTelegraph(GroundSpot(ph.transform.position), mirrorBurstRadius);
            PlayPhantomState(ph, novaState);
        }

        yield return new WaitForSeconds(mirrorTelegraph);

        // 진짜만 실제 발동(가짜는 시각 폭발만 = 무해)
        if (!_dead)
        {
            Vector3 nc = GroundSpot(transform.position);
            if (novaVfx != null) Instantiate(novaVfx, nc, Quaternion.identity);
            DealAreaDamage(nc, mirrorBurstRadius, data.attackDamage * mirrorDmgMul);
            _feedback?.PlaySound(novaSound);
            foreach (var ph in _phantoms)
                if (ph != null && novaVfx != null) Instantiate(novaVfx, GroundSpot(ph.transform.position), Quaternion.identity);
        }

        // 분신 유지(진짜 찾아 딜 넣는 시간) 후 붕괴
        float held = 0f;
        while (held < mirrorHoldTime && !_dead) { held += Time.deltaTime; yield return null; }
        ShatterPhantoms();

        ReturnToLocomotion();
        yield return new WaitForSeconds(mirrorRecover);
        _mirrorCd = mirrorCooldown * _enrageCd;
        _attacking = false;
    }

    // [07-29] P1 에서 HP가 0에 닿으면 EnemyHealth 가 이걸 물어본다.
    // true = 죽지 마라(HP 1 보류) -> 산산조각 재조립으로 P2 진입. P2 에선 false = 진짜 사망.
    private bool OnLethalDamage()
    {
        if (_phase != 0 || _dead) return false;   // P2(또는 이미 사망) = 진짜 죽음
        if (!_transitioning)
        {
            _transitioning = true;
            if (_health != null) _health.Invulnerable = true;   // 즉시 무적(추가 피해 차단)
            StopAllCoroutines();      // 진행 중 공격 중단
            _attacking = false;
            StartCoroutine(ShatterReformTransition());
        }
        return true;   // 전환 중엔 계속 사망 보류
    }

    // 산산조각 -> 비산 -> 플레이어 근처서 재조립(풀피) -> P2. 얼음정령의 시그니처 순간.
    // (와이번=자가회복 타이밍에, 얼음정령은 몸을 부쉈다 다시 뭉치며 두 번째 체력바로 넘어간다)
    private IEnumerator ShatterReformTransition()
    {
        _attacking = true;
        _motor.StopMove();
        bool agentWasOn = _agent != null && _agent.enabled;

        // 1) 산산조각: 몸 렌더러 OFF + 얼음 조각 폭발 + 그 자리 AOE(붙어있던 플레이어 타격)
        Vector3 p0 = GroundSpot(transform.position);
        var shatter = shatterVfx != null ? shatterVfx : impactVfxMelee;
        if (shatter != null) { var s = Instantiate(shatter, transform.position + Vector3.up, Quaternion.identity); s.transform.localScale *= 2.2f; Destroy(s, 3f); }
        if (novaVfx != null) Instantiate(novaVfx, p0, Quaternion.identity);
        SetBodyVisible(false);
        _feedback?.PlaySound(novaSound);
        DealAreaDamage(p0, shatterBurstRadius, data.attackDamage * shatterDmgMul);

        if (agentWasOn && _agent != null) _agent.enabled = false;   // 텔레포트 위해 끔

        yield return new WaitForSeconds(shatterHang);   // 비산

        // 2) 재조립 위치 = 플레이어 근처(압박)
        Vector3 dest = p0;
        if (_player != null)
        {
            Vector3 dir = transform.position - _player.position; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dest = _player.position + dir.normalized * reformDistance;
        }
        dest = GroundSpot(dest);

        // 재조립 예고: 서리 수렴(FrostAura) + 지면 텔레그래프
        GameObject converge = chargeVfxBody != null ? Instantiate(chargeVfxBody, dest + Vector3.up, Quaternion.identity) : null;
        if (converge != null) { converge.transform.localScale *= 1.8f; Destroy(converge, reformTelegraph + 0.5f); }
        SpawnTelegraph(dest, reformSlamRadius);
        _feedback?.PlaySound(ultimateChargeSound);

        // 재조립하면서 체력바가 서서히 100%까지 차오른다(P2 최대치 복원 + 램프. 바가 매프레임 읽어 눈에 보인다).
        if (_health != null) _health.maxHP = _baseMaxHp;
        float ft = 0f, startHp = _health != null ? _health.currentHP : 0f;
        while (ft < reformTelegraph && !_dead)
        {
            ft += Time.deltaTime;
            if (_health != null) _health.currentHP = Mathf.Lerp(startHp, _health.maxHP, ft / reformTelegraph);
            yield return null;
        }

        // 3) 재조립: 위치 이동 + 렌더러 ON + 풀피 + P2 + 강타
        transform.position = dest;
        if (agentWasOn && _agent != null)
        {
            _agent.enabled = true;
            if (NavMesh.SamplePosition(dest, out var hit, 8f, NavMesh.AllAreas)) _agent.Warp(hit.position);
        }
        if (_player != null) _motor.FaceInstant(_player.position);
        SetBodyVisible(true);
        transform.localScale = _baseScale * Mathf.Max(0.1f, p2ScaleMul);   // [07-29] P2 = 더 큰 몸
        SetBodyTint(p2Tint);                                                // [07-29] P2 = 빨간 몸
        EnableP2Glow(true);                                                 // [07-29] P2 = 발열 글로우
        _motor.PlayState(roarState);

        _phase = 1;                                          // P2 진입
        if (_health != null) _health.ResetToFull();          // 바 즉시 100% 회복
        Enrage();                                            // P2 = 광폭화
        BossRoarDebuff.Trigger(debuffDuration, debuffDrainMult, debuffDarkness, debuffVignetteStrength, debuffVignetteFalloff);   // 극적 전환(화면 서리)

        if (impactVfxHeavy != null) Instantiate(impactVfxHeavy, dest + Vector3.up * 0.3f, Quaternion.identity);
        _feedback?.PlaySound(roarSound);
        DealAreaDamage(dest, reformSlamRadius, data.attackDamage * reformSlamDmgMul);   // 재조립 강타

        if (_health != null) _health.Invulnerable = false;   // 무적 해제 = P2 전투 시작

        yield return new WaitForSeconds(reformRecover);
        _transitioning = false;
        _attacking = false;
    }

    // 산산조각/재조립 시 보스 몸(모델+파티클 렌더러) 껐다 켜기.
    private void SetBodyVisible(bool visible)
    {
        if (_bodyRenderers == null) return;
        for (int i = 0; i < _bodyRenderers.Length; i++)
            if (_bodyRenderers[i] != null) _bodyRenderers[i].enabled = visible;
    }

    // [07-29] P2 몸 색조(MaterialPropertyBlock = 공유 재질 안 건드림. 셰이더별 프로퍼티명 둘 다 세팅).
    private void SetBodyTint(Color c)
    {
        if (_bodyRenderers == null) return;
        var mpb = new MaterialPropertyBlock();
        for (int i = 0; i < _bodyRenderers.Length; i++)
        {
            var r = _bodyRenderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            mpb.SetColor("_EmissionColor", c);   // 발광 키워드 켜진 재질이면 글로우(아니면 무해)
            r.SetPropertyBlock(mpb);
        }
    }

    // 색조 원복(P1 = 원본색). 프로퍼티 블록 비워서 재질 원래 값으로.
    private void ClearBodyTint()
    {
        if (_bodyRenderers == null) return;
        for (int i = 0; i < _bodyRenderers.Length; i++)
            if (_bodyRenderers[i] != null) _bodyRenderers[i].SetPropertyBlock(null);
    }

    // [07-29] P2 발열 글로우: 보스에 빨간 점광원(설산 눈밭에 확 티남). 재질 발광 무관하게 확실히 빛난다.
    private void EnableP2Glow(bool on)
    {
        if (on)
        {
            if (_p2Light == null)
            {
                var go = new GameObject("P2_HeatGlow");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 0.6f;
                _p2Light = go.AddComponent<Light>();
                _p2Light.type = LightType.Point;
                _p2Light.shadows = LightShadows.None;
            }
            _p2Light.color = p2GlowColor;
            _p2Light.range = p2GlowRange;
            _p2Light.intensity = p2GlowIntensity;
            _p2Light.enabled = true;
        }
        else if (_p2Light != null) _p2Light.enabled = false;
    }

    // [07-29] 혹한(Whiteout): P3 진입 1회. 판을 얼려 플레이어 주변으로 고드름을 퍼붓고(추적)
    // 노바 충격파를 주기적으로 터뜨리며 몇 초간 생존을 강요, 마지막에 대폭발로 마무리.
    // 얼음정령의 시그니처 순간(와이번=자가회복 / 모래=감금 / 얼음=판 장악).
    private IEnumerator Whiteout()
    {
        _motor.StopMove();
        _motor.PlayState(ultState);
        SpawnCharge(chargeVfxBody, transform.position + Vector3.up * 1.5f, whiteoutDuration);
        _feedback?.PlaySound(ultimateChargeSound);

        float t = 0f, novaT = 0f;
        while (t < whiteoutDuration && !_dead)
        {
            Vector3 center = _player != null ? _player.position : transform.position;
            Vector2 rr = Random.insideUnitCircle * whiteoutSpread;
            Vector3 mspot = GroundSpot(center + new Vector3(rr.x, 0f, rr.y));
            StartCoroutine(DropIceShard(mspot, whiteoutMeteorFall, whiteoutMeteorRadius, whiteoutMeteorDmgMul, 1.2f));

            novaT += whiteoutMeteorGap;
            if (novaT >= whiteoutNovaGap)
            {
                novaT = 0f;
                Vector3 nc = GroundSpot(transform.position);
                if (novaVfx != null) Instantiate(novaVfx, nc, Quaternion.identity);
                DealAreaDamage(nc, novaRadius * 1.25f, data.attackDamage * novaDmgMul * 0.6f);
                _feedback?.PlaySound(novaSound);
            }

            float g = 0f;
            while (g < whiteoutMeteorGap && !_dead) { g += Time.deltaTime; yield return null; }
            t += whiteoutMeteorGap;
        }

        // 마무리 대폭발(플레이어 자리)
        if (!_dead && _player != null)
        {
            Vector3 spot = GroundSpot(_player.position);
            SpawnTelegraph(spot, whiteoutFinaleRadius);
            yield return new WaitForSeconds(0.7f);
            if (!_dead)
            {
                _feedback?.PlaySound(ultimateBurstSound);
                if (ultVfx != null) Instantiate(ultVfx, spot, Quaternion.identity);
                DealAreaDamage(spot, whiteoutFinaleRadius, data.attackDamage * whiteoutFinaleDmgMul);
                SpawnImpact(impactVfxHeavy, spot + Vector3.up * 0.3f);
            }
        }
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

        _phase = 0; _transitioning = false;   // [07-29] P1 로 리셋
        SetBodyVisible(true);                 // 산산조각 중 리셋 대비 몸 복구
        transform.localScale = _baseScale;    // [07-29] P1 크기 복원
        ClearBodyTint();                      // [07-29] P1 색 복원
        EnableP2Glow(false);                  // [07-29] 발열 글로우 끔
        if (_health != null) _health.Invulnerable = false;
        _enrageCd = 1f;
        _enrageSpeed = 1f;
        for (int i = 0; i < _atkCd.Length; i++) _atkCd[i] = 0f;
        _beamCd = 0f; _rainCd = 0f; _novaCd = 0f; _dashCd = 0f; _ultCd = 0f; _guardCd = 0f; _meleeGapCd = 0f; _mirrorCd = 0f;
        ClearPhantoms();   // [07-29] 리쉬 리셋 시 분신 정리

        // 돌진 중 리셋되면 에이전트가 꺼진 채 남는다 -> 되살린다
        if (_agent != null && !_agent.enabled) _agent.enabled = true;

        _motor.ResetToSpawn(_leash.SpawnPos, _leash.SpawnRot, data);
        ApplyP1Health();   // [07-29] 리셋 = P1 축소 체력으로
        BossHealthBarUI.Hide();
    }

    private void LateUpdate()
    {
        _motor.TickFacing(_dead || _attacking, ResolveTarget(), data);
    }
}
