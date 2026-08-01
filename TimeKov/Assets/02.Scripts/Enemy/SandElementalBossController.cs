using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 모래정령(사막 보스) - 전용 상태머신.
// 와이번/얼음정령과 같은 방식(BT/EnemyBrain 미사용)이며 글루는 BossMotor 가 대신한다.
// [07-29] 리쉬 폐지 - 원거리 디스폰은 EnemySpawnPoint 원거리슬립(activateDistance)이 전담(보스 4종 통일).
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

    [Header("[07-30] 테스트 (배포 전 반드시 끄기)")]
    [Tooltip("테스트 기간용: 켜면 모든 공격 데미지를 1로 강제(패턴 관찰용). 배포 전 반드시 끈다.")]
    [SerializeField] private bool testDamageOne = false;
    [Tooltip("[07-30] testDamageOne 켜졌을 때 파도만 이 값으로(1뎀들과 구분해 착탄 확인용). off면 무시.")]
    [SerializeField] private float waveTestDmg = 10f;
    [Tooltip("[07-30] testDamageOne 켜졌을 때 궁극만 이 값으로(1뎀들과 구분). off면 무시.")]
    [SerializeField] private float ultTestDmg = 15f;
    [Tooltip("[07-30] 켜면 파도만 발동(다른 패턴+궁극+흡수회복 잠시 끔). 테스트 격리용. 배포 전 끄기.")]
    [SerializeField] private bool testOnlyWave = false;
    [Tooltip("[07-31] 켜면 자가회복(잠수->이동->솟구쳐 회복=모래 돔/구체)만 반복 발동, 공격 전부 끔 + HP조건 무시. 구체 조정 테스트용. 배포 전 끄기.")]
    [SerializeField] private bool testOnlyHeal = false;
    // [07-31] 파도 판정/VFX 위치 = 코드 고정값(인스펙터 직렬화 제거). 조절은 여기 숫자만 고치면 즉시 반영.
    //   (프리팹/씬에 직렬화값 없음 확인 - 원래도 코드값이 권위. SerializeField 빼서 앞으로 자동직렬화도 차단.)
    private float waveVfxFwd = 2f;          // 파도 VFX 생성 = 보스 정면 이만큼 앞(m). 줄이면 보스 쪽으로 당김.
    private float waveNearDist = 3f;        // 판정 레인 시작(근단, 뒤/보스쪽 끝) = 보스 정면 이만큼 앞(m). 뒤쪽 조절은 이 값.
    private float waveFarDist = 36f;        // 판정 레인 끝(원단, 앞쪽 끝) = 보스 정면 이만큼 앞(m). 앞으로 늘리려면 이 값만 키워라.
    private float waveTravelTime = 1.8f;    // 전선이 근단->원단 가는 시간(초) = 거리비례 '기울기'. 멀리서만 늦으면 이걸 줄여라(전선 속도 up), 멀리서만 이르면 키워라.
    private float waveFrontDelay = 0.3f;    // 전선 출발 전 대기(초) = 가까이/멀리 똑같이 미는 '상수 오프셋'. 전체적으로 늦으면 줄이고, 전체적으로 이르면 키워라.
    private float waveTrailTime = 0.5f;     // 전선이 원단 도달 후 판정 추가 유지(초). 원단 플레이어 놓침 방지.
    private float waveWidth = 3.5f;         // 판정 레인 좌우 반쪽 폭(m) = 노란 띠 폭의 절반.
    public MeleeEnemyData Data => data;   // 도감 등 외부서 보스 스탯 조회용(보스는 EnemyBrain 미사용)
    [SerializeField] private string bossSubtitle = "시간을 삼키는 사막의 지배자";

    [Header("사운드 (패턴별 전용 SFX. data 의 attackSound 는 슬롯이 하나뿐이라 여기서 패턴별로 관리)")]
    [SerializeField] private AudioClip meleeAttack1Sound;   // Attack1
    [SerializeField] private AudioClip meleeAttack2Sound;   // Attack2 (가드 반격 스윙도 재사용)
    [SerializeField] private AudioClip waveSound;
    [Tooltip("회오리 바라지 시전 시작 시 1회(지속 앰비언스). 개별 기둥마다가 아니라 바라지 시작에만 — 여러 개가 동시에 겹치면 너무 시끄러워진다.")]
    [SerializeField] private AudioClip tornadoSound;
    [SerializeField] private AudioClip quicksandSound;
    [Tooltip("모래관 생성(시전 완료) 시점.")]
    [SerializeField] private AudioClip coffinCastSound;
    [Tooltip("모래관 붕괴(폭발) 시점.")]
    [SerializeField] private AudioClip coffinBurstSound;
    [Tooltip("가드(무적) 돌입 시점.")]
    [SerializeField] private AudioClip guardSound;
    [SerializeField] private AudioClip diveDropSound;
    [SerializeField] private AudioClip diveLandSound;
    [SerializeField] private AudioClip ultimateSound;
    [Tooltip("페이즈 전환 포효 1회차(66%). data.detectSound(최초 발견)와는 별도.")]
    [SerializeField] private AudioClip roarSound;
    [Tooltip("페이즈 전환 포효 2회차(33%).")]
    [SerializeField] private AudioClip roar2Sound;

    [Header("모래 파도 (SandWave - 전방 부채꼴 스윕. 중거리 견제)")]
    [SerializeField] private GameObject waveVfx;
    [Tooltip("지면 VFX(모래 파도 등)를 지면에서 살짝 띄우는 높이(m). 0이면 지면과 딱 붙어 울퉁불퉁한 사막 지형을 뚫었다 안 뚫었다 하며 치지직거린다(z-fighting/클리핑). 여전하면 이 값을 키워라.")]
    [SerializeField] private float groundVfxLift = 0.1f;
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
    [SerializeField] private float diveRadius = 6.3f;       // [07-31] Sand_Smash 원 판정 반경. 8->7->6.5->6.3(종욱: 미세하게 밖에서 맞음). 프리팹이 이김=거기도 동기화. 밖에서 맞으면 더 줄이고, 안인데 안맞으면 키워라.
    [SerializeField] private float diveDmgMul = 1.5f;

    [Header("궁극 (Cast3 + Pyramid_Explosion - 페이즈3 전용)")]
    [SerializeField] private GameObject ultVfx;
    [SerializeField] private float ultRange = 16f;
    [SerializeField] private float ultCooldown = 13f;
    [SerializeField] private float ultWindup = 1.4f;        // Cast3 = 긴 채널(예고)
    [Tooltip("[07-30] 생성 후 VFX가 팡 터지는 시점(초). 종욱 측정 ~2.3s. 이때 Square_Decal 재서 판정. 팡 순간에 맞춰라.")]
    [SerializeField] private float ultTelegraph = 2.3f;
    [SerializeField] private float ultRecover = 1.2f;
    [Tooltip("[07-30] 궁극 판정 = 정사각형 반쪽 크기(m). Pyramid_Explosion의 Square_Decal(네모)에 맞춰라. 씬서 네모 한 변 재서 그 절반값.")]
    [SerializeField] private float ultHalfExtent = 6f;   // [07-31] 궁극 판정 = 착탄점 중심 이 반쪽길이의 고정 정사각. 시각 네모(피라미드 폭발)에 맞춰라. 9(18m)가 너무 커서 6(12m)으로. 밖에서 맞으면 더 줄이고, 안인데 안맞으면 키워라.
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
    [SerializeField] private float enrageCdMul = 0.82f;     // [07-30] 회복(흡수)마다 공격 쿨 x (누적)
    [SerializeField] private float enrageSpeedMul = 1.15f;  // [07-30] 회복(흡수)마다 이속 x (누적)

    [Header("[07-30] 시그니처: 모래 흡수 재생 + 침식 (단일 페이즈)")]
    [Tooltip("몸이 곧 체력바. HP 100% = 원래 크기 -> HP 0%면 이 비율까지 야윈다(침식). 흡수 회복하면 다시 커짐.")]
    [SerializeField] private float erodeMinScale = 0.6f;
    [Tooltip("침식/재생 스케일 추종 속도(부드럽게 따라감).")]
    [SerializeField] private float erodeLerp = 3f;
    [Tooltip("자가 회복(모래 흡수) 시도 간격(초). 싸움 중간중간 이 주기로 채널 발동.")]
    [SerializeField] private float healInterval = 14f;
    [Tooltip("흡수 채널 길이(초). 이 동안 계속 피격 가능 = 딜로 상쇄(파해법).")]
    [SerializeField] private float healChannelTime = 4f;
    [Tooltip("흡수 채널 중 초당 회복량(최대HP 비율). 플레이어 초당 딜(비율)이 이보다 낮으면 순증 = 못 깬다(스펙게이트).")]
    [SerializeField] private float healPctPerSec = 0.06f;   // [07-31] 회복률(초당 %maxHP). 종욱 조절: 딜컷 강화 위해 0.03->0.06(2배). 프리팹도 동기화(프리팹이 이김).
    [Tooltip("흡수 채널 VFX(바닥 모래가 몸으로 빨려 올라감). 비우면 chargeVfxBody 폴백.")]
    [SerializeField] private GameObject absorbVfx;
    [Tooltip("[07-30] 흡수 채널 모션 = SandAbsorb(Cower 웅크려 끌어모음). 빌더 재실행해야 상태 생김.")]
    [SerializeField] private string absorbState = "SandAbsorb";
    [Tooltip("[07-30] 흡수 중 주변에서 몸으로 빨려드는 모래 알갱이 VFX. 비우면 impactVfxMelee(모래 튐) 재사용.")]
    [SerializeField] private GameObject inflowPuffVfx;
    [Tooltip("빨려드는 모래가 시작되는 반경(보스 주변).")]
    [SerializeField] private float inflowRadius = 6f;
    [Tooltip("모래 알갱이 생성 간격(초). 작을수록 촘촘히 빨려듦.")]
    [SerializeField] private float inflowInterval = 0.1f;
    [Tooltip("모래 알갱이가 몸까지 빨려드는 시간(초).")]
    [SerializeField] private float inflowTravelTime = 0.45f;
    [Header("[07-30] 흡수 = 잠수 -> 이동 -> 솟구쳐 재등장 후 회복")]
    [Tooltip("잠수/솟구침 시 몸이 지면 아래로 내려가는 깊이(m).")]
    [SerializeField] private float sinkDepth = 4f;
    [Tooltip("가라앉는 시간(초).")]
    [SerializeField] private float sinkTime = 0.5f;
    [Tooltip("솟구치는 시간(초).")]
    [SerializeField] private float riseTime = 0.4f;
    [Tooltip("재등장 위치 = 플레이어에서 이만큼 떨어진 곳(회복하러 쫓아가게).")]
    [SerializeField] private float relocateDist = 13f;
    [Tooltip("재등장 지점이 벽(navmesh 경계)에서 최소 이만큼 떨어지게(넓은 보스가 벽에 안 끼게). 못 찾으면 원위치서 회복.")]
    [SerializeField] private float relocateClearance = 2.5f;
    [Tooltip("잠수 순간 터지는 모래 폭발 VFX. 비우면 impactVfxHeavy 재사용.")]
    [SerializeField] private GameObject sinkVfx;
    [Tooltip("솟구칠 때 터지는 대형 모래 분출 VFX. 비우면 ultVfx(피라미드)->slamVfx 재사용.")]
    [SerializeField] private GameObject emergeVfx;
    [Tooltip("회복 중 감싸는 모래 돔(Sand_shield) 크기. 불parented 고정 스케일(침식/HP와 무관해 안 흔들림). 1=기준(보스)크기, 널널하게 1.6~2.")]
    [SerializeField] private float healWrapScale = 1.7f;
    [Tooltip("돔을 지면에서 띄우는 높이(바닥부터 감싸는 느낌 조절).")]
    [SerializeField] private float healWrapYOffset = 0.5f;

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

    // [07-29] 리쉬 폐지 - 원거리 처리는 EnemySpawnPoint 원거리슬립(activateDistance)이 전담(보스 4종 통일).

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

    // 패턴 종류(가중 랜덤 선택용). 조건 맞는 후보를 모아 확률로 뽑는다(우선순위 사다리 대신).
    private enum AtkType { None, Melee, Wave, Tornado, Coffin, Quicksand, Guard, Dive, Ult }
    private AtkType _lastAttack = AtkType.None;
    private readonly List<(AtkType type, float weight)> _cand = new List<(AtkType, float)>();

    // 공용 모터 (보스 3종 공유)
    private BossMotor _motor;

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
    private float _enrageCd = 1f;
    private float _enrageSpeed = 1f;
    private Vector3 _baseScale = Vector3.one;   // [07-30] 침식 스케일 기준(Start서 캡처)
    private float _erodeK = 1f;                 // [07-30] 침식 배율(영속). 부드럽게 이동 후 매 프레임 하드셋(애니 스케일커브 위에서 이김)
    private Renderer[] _bodyRenderers;          // [07-30] 잠수 시 껐다 켤 몸 렌더러
    private float _healTimer;                   // [07-30] 흡수 회복 주기 타이머
    private bool _healing;                      // [07-30] 흡수 채널 진행 중
    private int _nHeals;                        // [07-30] 회복 횟수 = 에스컬레이션(옛 RoarsDone 대체)
    private Vector3 _homePos;                    // [07-31] 스폰(원래) 자리 = 타겟 상실 시 복귀 지점
    private Quaternion _homeRot = Quaternion.identity;
    private bool _homeCaptured;
    private bool _returningHome;                 // 복귀 중 = 회복 등 안 함
    private float homeArriveDist = 4f;           // 이 거리 안 들어오면 도착 처리(에이전트 stoppingDistance ~3.15보다 크게)

    // 감금 락(플레이어 이동 잠금) - StopAllCoroutines 로 코루틴이 끊겨도 반드시 풀어야 한다.
    private bool _coffinLockActive;
    private PlayerMovementComponent _lockedMove;

    private void Awake()
    {
        _motor = new BossMotor(this, speedParam);
        _atkCd = new float[MeleeAttacks.Length];
        _motor.ApplyData(data);
    }

    private void Start()
    {
        _motor.AcquirePlayer();
        if (_health != null) _health.OnDeath += HandleDeath;
        _feedback?.PlaySpawn();
        _baseScale = transform.localScale;   // [07-30] 침식 기준 크기(빌더 스케일 적용 후)
        _bodyRenderers = GetComponentsInChildren<Renderer>(true);   // [07-30] 잠수 껐다 켜기용
        _homePos = transform.position; _homeRot = transform.rotation; _homeCaptured = true;   // [07-31] 복귀 지점 캡처
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnDeath -= HandleDeath;
        ReleaseCoffinLock();   // 파괴 시 플레이어 락 잔존 방지
        if (_engaged) BattleBgm.End();   // [07-29] 원거리슬립 despawn 시 전투 브금 원복(리쉬 폐지)
    }

    private void OnDisable()
    {
        ReleaseCoffinLock();   // 비활성화(풀링/컬링 등)로 코루틴이 죽어도 플레이어 락은 반드시 푼다
    }

    private void HandleDeath()
    {
        _dead = true;
        BattleBgm.End();   // 처치 → 전투 브금 종료, 기존 BGM 재개
        if (_health != null) _health.Invulnerable = false;   // 가드 중 사망 시 무적 잔존 방지
        StopAllCoroutines();
        ReleaseCoffinLock();   // ★사망이 코루틴을 끊어도 감금 락은 반드시 푼다
        EndDiveCleanup();      // 다이브/잠수 중 사망 시 공중/지하에 멈추지 않게 지면 복구
        SetBodyVisible(true);  // [07-30] 잠수 중 사망해도 몸이 안 보이는 채로 남지 않게
        _motor.StopMove();
    }

    private void Update()
    {
        if (_dead) return;
        if (_player == null) _motor.AcquirePlayer();

        _motor.TickSpeedParam();
        TickCooldowns();
        TickHeal();   // [07-30] 주기적 모래 흡수 회복(옛 포효 페이즈 대체)
        if (_attacking) return;

        Transform target = ResolveTarget();
        if (target != null && !_engaged)
        {
            _engaged = true;
            BossHealthBarUI.Show(_health, data != null ? data.enemyName : "모래정령", bossSubtitle);
            _feedback?.PlayDetect();
            BattleBgm.Begin(SfxId.WyvernBattleBgm);   // 교전 시작 → 전투 브금(기존 BGM 일시정지). 보스 공통 브금 재사용.
        }
        if (target == null) { TickReturnHome(); return; }   // [07-31] 시야 밖 = 멈추지 말고 원래 자리로 복귀
        _returningHome = false;   // 타겟 있음 = 복귀 상태 해제
        if (data == null) return;

        float dist = _motor.PlanarDistance(target.position);
        float meleeMax = data.attackRange * MeleeMaxReachMul;

        // [07-30] 단일 페이즈: 페이즈 게이트 제거. 쿨/거리만으로 후보를 모은다(쿨이 리듬을 자연스레 잡음).
        _cand.Clear();
        if (!testOnlyHeal && _waveCd <= 0f && dist <= waveRange && dist > meleeMax)
            _cand.Add((AtkType.Wave, wWave));   // 파도는 근접 사거리 밖에서만(붙으면 근접이 낫다)

        int meleeIdx = -1;
        if (!testOnlyWave && !testOnlyHeal)   // [07-30] 파도만 / [07-31] 회복(구체)만 볼 땐 공격 전부 끔
        {
            if (_ultCd <= 0f && ultVfx != null && dist <= ultRange)
                _cand.Add((AtkType.Ult, wUlt));
            if (_diveCd <= 0f && slamVfx != null && dist <= diveRange)
                _cand.Add((AtkType.Dive, wDive));
            if (_quicksandCd <= 0f && dist <= quicksandRange)
                _cand.Add((AtkType.Quicksand, wQuicksand));
            if (_guardCd <= 0f && dist <= guardRange)
                _cand.Add((AtkType.Guard, wGuard));
            if (_coffinCd <= 0f && dist <= coffinRange && dist > meleeMax * 0.5f)
                _cand.Add((AtkType.Coffin, wCoffin));   // 감금 = 시그니처 CC
            if (_tornadoCd <= 0f && dist <= tornadoRange)
                _cand.Add((AtkType.Tornado, wTornado));
            if (dist <= meleeMax && _meleeGapCd <= 0f && TrySelectMelee(dist, out meleeIdx))
                _cand.Add((AtkType.Melee, wMelee));
        }

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

    // [07-31] 타겟 상실(플레이어가 어그로 밖 = 시야에서 사라짐) -> 원래 자리로 '걸어서' 복귀. 도착하면 디스인게이지.
    //   텔레포트 아님(리쉬 폐지=스폰슬립과 안 충돌). 복귀 중 플레이어 재진입하면 ResolveTarget이 다시 잡아 자동 재교전.
    private void TickReturnHome()
    {
        if (_dead || !_homeCaptured) return;
        if (_motor.PlanarDistance(_homePos) > homeArriveDist)
        {
            _returningHome = true;
            _motor.Chase(_homePos);   // Speed 파라미터가 걷기/뛰기 애니 자동 재생
            return;
        }
        _returningHome = false;
        _motor.StopMove();
        _motor.FaceInstant(transform.position + _homeRot * Vector3.forward);   // 원래 바라보던 방향
        if (_engaged) Disengage();
    }

    // 교전 해제(플레이어 놓치고 복귀 완료): 전투 브금/체력바 끄고 풀피로 되돌린다(쪼고 도망 치즈 방지). 재진입하면 다시 교전.
    private void Disengage()
    {
        _engaged = false;
        BattleBgm.End();
        BossHealthBarUI.Hide();
        if (_health != null) { _health.Invulnerable = false; _health.ResetToFull(); }
        _nHeals = 0; _healTimer = 0f;   // 에스컬레이션/회복 타이머 리셋
    }

    // [07-30] 단일 페이즈: 옛 포효 카운트 대신 '회복 횟수'로 에스컬레이션(회복할수록 강해짐). 이름 유지(호출부 재사용).
    private int RoarsDone() => Mathf.Min(_nHeals, 2);

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
        _feedback?.PlaySound(idx == 1 ? meleeAttack2Sound : meleeAttack1Sound);
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
        _feedback?.PlaySound(waveSound);

        if (!_dead && waveVfx != null)
        {
            // 파도 = 보스 정면 직사각 레인(정적 파티클). VFX 생성 위치(waveVfxFwd)와 판정(waveNearDist~waveFarDist) 분리.
            Vector3 vfxPos = GroundSpot(transform.position + transform.forward * waveVfxFwd);
            var wv = Instantiate(waveVfx, vfxPos + Vector3.up * groundVfxLift, transform.rotation);
            Destroy(wv, 2.5f);   // 루프 서브이미터라 자동 소멸 안 됨 -> 예약 삭제
            StartCoroutine(WaveSweepDamage(transform.position, transform.forward, data.attackDamage * waveDmgMul, waveTestDmg));
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
        _feedback?.PlaySound(tornadoSound);

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
        _feedback?.PlaySound(quicksandSound);

        // 늪을 깔고 보스는 곧 풀려난다(장판 데미지는 독립 티커가 유지). = "깔고 이동한다".
        // 보스가 채널에 묶여 서 있지 않으므로 반경 밖 회피가 '보스 무료 딜'로 이어지지 않는다.
        if (!_dead && _player != null)
        {
            Vector3 spot = GroundSpot(_player.position);
            // [07-30] 텔레그래프 원 제거 - QuickSand VFX 자체에 원 인디케이터가 있다. 판정(quicksandRadius)을 그 원에 맞춰라.
            GameObject field = quicksandVfx != null ? Instantiate(quicksandVfx, spot + Vector3.up * groundVfxLift, Quaternion.identity) : null;
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
        _feedback?.PlaySound(coffinCastSound);

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
            _feedback?.PlaySound(coffinBurstSound);
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
        _feedback?.PlaySound(guardSound);
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
            _feedback?.PlaySound(meleeAttack2Sound);
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
        if (slamVfx != null) { var sl = Instantiate(slamVfx, land + Vector3.up * groundVfxLift, Quaternion.identity); Destroy(sl, 3.5f); }   // 루프 서브이미터 -> 예약 삭제
        Vector3 over = new Vector3(land.x, land.y + diveHeight, land.z);
        _motor.FaceInstant(land);
        yield return MoveBetween(apexUp, over, diveHoverTime);

        // 3) 수직 급강하 -> hover+drop = 약 1.6s = Sand_Smash 예고 길이에 맞물림
        _feedback?.PlaySound(diveDropSound);
        yield return MoveBetween(over, land, diveDropTime);
        transform.position = land;

        // 4) 착지 판정
        _feedback?.PlaySound(diveLandSound);
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
        // [07-30] Pyramid_Explosion 네모 폭발이 범위. [07-31] 판정=시각 네모에 맞춘 고정 박스(ultHalfExtent) 수동매칭(자동측정 TryDecalHit 폐기).
        GameObject uv = ultVfx != null ? Instantiate(ultVfx, spot, Quaternion.identity) : null;
        if (uv != null) Destroy(uv, 5.5f);   // 루프 서브이미터 -> 예약 삭제

        yield return new WaitForSeconds(ultTelegraph);   // ★생성 후 VFX가 실제로 팡 터지는 시점(측정값 ~2.3s). 이때 Square_Decal 재서 판정
        if (!_dead)
        {
            _feedback?.PlaySound(ultimateSound);
            // [07-31] 자동측정(TryDecalHit=Square_Decal 파티클 바운드)이 시각 네모보다 커서 밖에서도 맞던 문제.
            //   파도 교훈대로 폐기 -> 시각 네모에 맞춘 '고정 박스'(ultHalfExtent) 수동 매칭으로 통일.
            DealBoxDamage(spot, ultHalfExtent, data.attackDamage * ultDmgMul, ultTestDmg);
            // [07-31] SpawnImpact(impactVfxHeavy=Sand_combo_Smash) 제거 - 감금과 중복 + 궁극은 이미 Pyramid_Explosion이 터짐(종욱: 같은 VFX 여러곳 쓰기 싫음). Sand_combo_Smash는 감금에만.
        }

        yield return new WaitForSeconds(ultRecover);
        _ultCd = ultCooldown * _enrageCd;
        _attacking = false;
    }

    // [07-30] 시그니처: 주기적으로 바닥 모래를 흡수해 자가 회복(단일 페이즈의 리듬 = 옛 포효 페이즈 대체).
    private void TickHeal()
    {
        if (_dead || !_engaged || _health == null || testOnlyWave || _returningHome) return;   // [07-30] 테스트 격리 / [07-31] 복귀 중엔 회복 안 함
        _healTimer += Time.deltaTime;
        if (_healing || _attacking) return;   // 공격/채널 중엔 시작 안 함(공격 사이 빈틈에 발동)
        if (testOnlyHeal)   // [07-31] 회복만 격리: HP 조건 무시 + 짧은 주기로 반복(구체 관찰용)
        {
            if (_healTimer >= 1.5f) StartCoroutine(SandAbsorbHeal());
            return;
        }
        if (_healTimer >= healInterval && _health.currentHP < _health.maxHP)
            StartCoroutine(SandAbsorbHeal());
    }

    // 모래 흡수 재생 = 잠수 -> 멀리 이동 -> 솟구쳐 재등장 -> 그 자리서 회복 채널.
    //   잠수/이동으로 플레이어에게서 벗어나므로(쫓아가야 함) 회복 저지가 자연스러움. ★재등장 후 채널은 피격 가능 = 딜로 상쇄(파해법).
    //   플레이어 초당 딜 > healPctPerSec 면 순감(이김), 낮으면 순증(스펙게이트). 회복 완료 후 포효로 강해짐(Enrage).
    private IEnumerator SandAbsorbHeal()
    {
        _healing = true;
        _attacking = true;
        _healTimer = 0f;
        _motor.StopMove();
        if (_player != null) _motor.FaceInstant(_player.position);
        _motor.PlayState(absorbState);   // Cower = 웅크려 파고들 준비

        bool agentWasOn = _agent != null && _agent.enabled;
        if (agentWasOn) { _agent.isStopped = true; _agent.ResetPath(); _agent.enabled = false; }

        // 1) 잠수: 바닥 소용돌이 '원' + 아래로 가라앉아 사라짐. [07-31] 잠수 폭발(burst) 제거 - 종욱 "회복인데 VFX 과함, 원만 남겨라".
        Vector3 sinkPos = GroundSpot(transform.position);
        var swirl = quicksandVfx != null ? Instantiate(quicksandVfx, sinkPos + Vector3.up * groundVfxLift, Quaternion.identity) : null;
        if (swirl != null) Destroy(swirl, sinkTime + 1.5f);
        _feedback?.PlaySound(quicksandSound);
        yield return MoveBetween(transform.position, sinkPos - Vector3.up * sinkDepth, sinkTime);
        SetBodyVisible(false);   // 완전히 잠수(안 보임)

        // 2) 재등장 위치 = 플레이어에서 relocateDist 떨어진 곳(쫓아가게). ★navmesh 위 + 벽에서 relocateClearance 이상 떨어진 곳만.
        Vector3 dest = sinkPos;   // 폴백 = 잠수한 자리(보스가 서 있던 곳 = 항상 유효+여유)
        if (_player != null)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector2 rd = Random.insideUnitCircle.normalized;
                Vector3 cand = _player.position + new Vector3(rd.x, 0f, rd.y) * relocateDist;
                if (!NavMesh.SamplePosition(cand, out var nh, 3f, NavMesh.AllAreas)) continue;   // navmesh 밖 = 벽 속
                if (NavMesh.FindClosestEdge(nh.position, out var edge, NavMesh.AllAreas) && edge.distance < relocateClearance) continue;   // 벽에 너무 붙음
                dest = nh.position; break;
            }
        }
        transform.position = dest - Vector3.up * sinkDepth;   // 지하 대기
        yield return new WaitForSeconds(0.3f);                 // "어디서 나오지" 긴장
        if (_dead) { SetBodyVisible(true); yield break; }

        // 3) 솟구침: 몸 재등장. [07-31] 대형 분출(erupt=emergeVfx 없으면 ultVfx=궁극 피라미드가 억지로 들어가던 것) 제거 - 종욱 요청. 구체(돔)만 남김.
        _motor.FaceInstant(_player != null ? _player.position : dest);
        SetBodyVisible(true);
        _feedback?.PlaySound(diveLandSound);
        yield return MoveBetween(dest - Vector3.up * sinkDepth, dest, riseTime);
        transform.position = dest;

        // 4) 회복 채널: 몸을 모래바람이 감싸고 + 주변 모래 빨려듦 + 초당 회복(피격 가능=상쇄, 플레이어가 쫓아와 때려야)
        _motor.PlayState(absorbState);
        // 돔은 불parented + 고정 스케일 = 침식(HP)으로 보스가 커졌다 작아졌다 해도 안 흔들리고 바닥에서 넉넉히 감싼다.
        GameObject wrap = null;
        var wrapSrc = absorbVfx != null ? absorbVfx : chargeVfxBody;
        if (wrapSrc != null)
        {
            wrap = Instantiate(wrapSrc, GroundSpot(transform.position) + Vector3.up * healWrapYOffset, Quaternion.identity);
            wrap.transform.localScale = _baseScale * Mathf.Max(0.1f, healWrapScale);
            Destroy(wrap, healChannelTime + 0.5f);
        }
        _feedback?.PlaySound(ultimateSound);
        // [07-31] 채널 중 주변 모래 빨림(SandInflowPuff) 제거 - 종욱 "구체만 남겨라". 돔(wrap)만 유지.
        float t = 0f;
        while (t < healChannelTime && !_dead)
        {
            t += Time.deltaTime;
            if (_health != null && _health.maxHP > 0f)
            {
                _health.currentHP = Mathf.Min(_health.maxHP,
                    _health.currentHP + _health.maxHP * healPctPerSec * Time.deltaTime);
                if (_health.currentHP >= _health.maxHP && !testOnlyHeal) break;   // [07-31] 회복격리 땐 안 끊고 채널 풀 유지(구체 관찰)
            }
            yield return null;
        }
        if (wrap != null) Destroy(wrap);

        // 5) 회복 후 조용히 강해진다(쿨↓/이속↑). 포효 비트는 뺌 - 솟구침+재응집이 이미 "돌아옴"을 충분히 보여줌.
        _nHeals++;
        if (!_dead) Enrage();

        // 6) 에이전트 복구 + navmesh 스냅(안 하면 공중/벽 밖 잔존)
        if (agentWasOn && _agent != null)
        {
            _agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, 8f, NavMesh.AllAreas)) _agent.Warp(hit.position);
        }
        ReturnToLocomotion();
        yield return new WaitForSeconds(0.3f);
        _attacking = false;
        _healing = false;
    }

    // [07-30] 흡수 채널 연출: 주변 모래 한 알갱이가 몸으로 쫙 빨려든다(여러 개 겹쳐 = 진공 흡입감).
    private IEnumerator SandInflowPuff()
    {
        GameObject vfx = inflowPuffVfx != null ? inflowPuffVfx : impactVfxMelee;
        if (vfx == null) yield break;
        float dur = Mathf.Max(0.05f, inflowTravelTime);
        Vector2 d = Random.insideUnitCircle.normalized;
        Vector3 ring = GroundSpot(transform.position + new Vector3(d.x, 0f, d.y) * inflowRadius) + Vector3.up * 0.4f;
        var g = Instantiate(vfx, ring, Quaternion.identity);
        Destroy(g, dur + 0.5f);   // 코루틴이 끊겨도(사망) 자체 정리
        if (inflowPuffVfx == null) g.transform.localScale *= 0.5f;   // 폴백(모래 튐)은 크니 줄임
        float t = 0f;
        while (t < dur && g != null && !_dead)
        {
            t += Time.deltaTime;
            g.transform.position = Vector3.Lerp(ring, transform.position + Vector3.up * 1f, t / dur);   // 몸통으로 수렴
            yield return null;
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
        var t = Instantiate(telegraphVfx, pos + Vector3.up * groundVfxLift, Quaternion.identity);
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

    private void DealDamage(float amount) => DealDamage(amount, 1f);   // 기본 테스트값 1
    // [07-30] testDamageOne 켜지면 amount 대신 testAmount(기본1, 파도/궁극만 다르게 넘김). off면 amount 그대로.
    // [07-31] TakeDamage 는 finalDamage = Max(1, amount - DEF). 누적 DEF가 testAmount(10 등)를 다 먹어 -1로만
    //   보이던 문제 -> 테스트 중엔 DEF만큼 얹어 넘겨 화면에 testAmount 그대로 뜨게 한다(파도=지정값, 근접=1로 구분).
    private void DealDamage(float amount, float testAmount)
    {
        if (_playerStat == null) return;
        if (testDamageOne)
        {
            _playerStat.TakeDamage(testAmount + _playerStat.DEF);   // -> finalDamage = Max(1, testAmount)
            return;
        }
        _playerStat.TakeDamage(amount);
    }

    private void DealAreaDamage(Vector3 center, float radius, float amount, float testAmount = 1f)
    {
        if (_player == null) return;
        Vector3 a = center; a.y = 0f;
        Vector3 b = _player.position; b.y = 0f;
        if (Vector3.Distance(a, b) <= radius) DealDamage(amount, testAmount);
    }

    // [07-30] 정사각형(축정렬) 판정. 궁극(Pyramid_Explosion)처럼 VFX 범위가 네모인 것용. 원과 달리 모서리까지 커버.
    private void DealBoxDamage(Vector3 center, float halfExtent, float amount, float testAmount = 1f)
    {
        if (_player == null) return;
        Vector3 d = _player.position - center;
        if (Mathf.Abs(d.x) <= halfExtent && Mathf.Abs(d.z) <= halfExtent) DealDamage(amount, testAmount);
    }

    // [07-30] VFX 경계 데칼의 실제 렌더 바운드(월드 AABB, XZ)로 판정 = 보이는 범위 == 딜(수동 측정 불필요, 위치/크기 자동).
    //   root 밑에서 decalName 인 첫 오브젝트(계층상 부모 우선)의 Renderer.bounds 사용. 못 찾거나 바운드 무효(파티클 미방출)면
    //   false 반환 -> 호출부가 기하 폴백(원/네모/콘). ★측정하려면 그 순간 데칼이 살아있어야 함(타이밍=데칼 표시 구간).
    private bool TryDecalHit(GameObject root, string decalName, out bool inside)
    {
        inside = false;
        if (root == null || _player == null) return false;
        var trs = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i].name != decalName) continue;
            var r = trs[i].GetComponent<Renderer>();
            if (r == null) continue;
            Bounds b = r.bounds;
            if (b.size.x < 0.05f && b.size.z < 0.05f) return false;   // 바운드 무효 -> 폴백
            Vector3 p = _player.position;
            inside = p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z;
            return true;
        }
        return false;
    }

    // [07-31] 파도 판정 = 근단(waveNearDist)에서 원단(waveFarDist)으로 나아가는 '전선(front)' 모델. 모래바람이
    //   실제로 날아가 닿는 순간에만 딜(선 생기자마자 딜 들어가던 문제 해결). front가 플레이어 전방거리를 넘으면 1회.
    //   waveTravelTime = 전선 속도(맞기 전에 딜 들어가면 이 값을 키워 느리게).
    private IEnumerator WaveSweepDamage(Vector3 origin, Vector3 forward, float amount, float testAmount)
    {
        Vector3 fwd = forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) yield break;
        fwd.Normalize();
        Vector3 rightv = new Vector3(fwd.z, 0f, -fwd.x);
        Vector3 o = origin; o.y = 0f;
        Vector3 nearEnd = o + fwd * waveNearDist;                    // 판정 시작(보스에서 waveNearDist 앞)
        float total = Mathf.Max(0.1f, waveFarDist - waveNearDist);   // 레인 길이(근단~원단)
        float travel = Mathf.Max(0.05f, waveTravelTime);
        float dur = waveFrontDelay + travel + Mathf.Max(0f, waveTrailTime);   // 초기 대기 + 이동 + 끝자락
        float t = 0f;
        while (t < dur && !_dead)
        {
            t += Time.deltaTime;
            float frontDist = (Mathf.Max(0f, t - waveFrontDelay) / travel) * total;   // 대기 후 근단에서 나아간 거리(m)
            if (_player != null)
            {
                Vector3 rel = _player.position - nearEnd; rel.y = 0f;
                float dFwd = Vector3.Dot(rel, fwd);                  // 근단 기준 전방거리
                float dLat = Vector3.Dot(rel, rightv);               // 좌우거리
                // 전선이 플레이어를 지났고(닿음) + 좌우폭 안 + 레인 길이 안 -> 그 순간 1회
                if (dFwd >= 0f && dFwd <= total && Mathf.Abs(dLat) <= waveWidth && frontDist >= dFwd)
                {
                    DealDamage(amount, testAmount);
                    SpawnImpact(impactVfxMelee, PlayerHitPos(), meleeImpactScale);
                    yield break;   // 한 번만
                }
            }
            yield return null;
        }
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

    // [07-30] 잠수/솟구침 시 몸(모델+파티클 렌더러) 껐다 켜기.
    private void SetBodyVisible(bool visible)
    {
        if (_bodyRenderers == null) return;
        for (int i = 0; i < _bodyRenderers.Length; i++)
            if (_bodyRenderers[i] != null) _bodyRenderers[i].enabled = visible;
    }

    private void LateUpdate()
    {
        _motor.TickFacing(_dead || _attacking, ResolveTarget(), data);

        // [07-30] 침식: 몸 스케일이 HP 비율을 따라간다(맞으면 야위고, 흡수 회복하면 다시 큰다) = 몸이 곧 체력.
        //   ★영속 _erodeK를 부드럽게 움직이고 매 프레임 하드셋 = 클립에 스케일 커브가 있어도 LateUpdate가 이긴다(와이번 교훈).
        if (!_dead && _health != null && _health.maxHP > 0f)
        {
            float hpR = Mathf.Clamp01(_health.currentHP / _health.maxHP);
            float targetK = Mathf.Lerp(erodeMinScale, 1f, hpR);
            _erodeK = Mathf.Lerp(_erodeK, targetK, Time.deltaTime * erodeLerp);
            transform.localScale = _baseScale * _erodeK;
        }
    }
}
