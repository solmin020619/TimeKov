using System;
using UnityEngine;

// 헬 몬스터(용암 바이옴) 전용 데이터. MeleeEnemyData 를 상속해서
// EnemyHealth / EnemyFeedback / 도감 / 드롭 연동은 기존 그대로 살린다.
//
// 지상 4종(헬하운드/헬뱃/헬버그/헬사이클롭)이 같은 회사 에셋이라 클립 규격을 공유한다.
// 그래서 컨트롤러는 1벌이고, 몹별 차이는 전부 이 SO 의 attacks 배열로 낸다.
// 공격 방식. 근접만 고집하지 않고 섞는다.
public enum HellAttackKind
{
    Melee,      // 직접 때린다
    Ranged,     // 입에서 모아서 쏜다
}

// 전조 종류. ★근접 평타에 차지 오브를 붙이면 어울리지 않는다.
//   모으는 구슬은 "쏘려는" 예고지 할퀴기 예고가 아니다. 그래서 형태를 나눈다.
public enum HellTelegraphKind
{
    None,           // 애니 준비동작만으로 충분한 빠른 평타
    Charge,         // 입 앞에 모여드는 구슬. 원거리 발사 전용. 입을 따라다닌다
    GroundCharge,   // 같은 구슬을 지면 목표 지점에 고정. 도약 착지 예고용
    Ground,         // 저작된 시전 마법진. 범위 근접(내려찍기 등)
    FillCircle,     // ★착지 지점에 원이 뜨고 전조 시간에 맞춰 다 차오른다. 다 차는 순간 공격
}

[CreateAssetMenu(fileName = "HellData_", menuName = "TimeKov/Enemy/Hell Monster Data")]
public class HellMonsterData : MeleeEnemyData
{
    [Header("전조 공통")]
    [Tooltip("입 앞에 모여드는 차지 VFX(원거리용).")]
    public GameObject telegraphVfx;
    [Tooltip("바닥에 까는 범위 예고(범위 근접용). 저작된 시전 마법진.")]
    public GameObject groundTelegraphVfx;
    [Tooltip("차오르는 원 VFX(도약 착지 예고용). 재생속도를 전조 시간에 맞춰 늘리거나 줄인다.")]
    public GameObject fillCircleVfx;
    [Tooltip("차오르는 원 색. ★용암맵에서 빨간 원은 바닥에 묻힌다. " +
             "색조가 아니라 명도로 대비를 준다(하얗게 달아오른 쇳물 색). 알파 0 이면 공통 전조색을 쓴다.")]
    public Color fillCircleColor = new Color(1f, 0.82f, 0.55f, 1f);
    [Tooltip("차오르는 원이 스케일 1 일 때의 실제 반경(m). 코드가 자동 실측하며, 실패했을 때만 이 값을 쓴다.")]
    public float fillCircleUnitRadius = 5f;
    [Tooltip("원이 처음 뜰 때의 크기 비율. 여기서 1 까지 차오른다.")]
    public float fillCircleFromScale = 0.05f;
    [Tooltip("공격 시작 후 원이 남아있는 시간(초). 착지까지 보이게 넉넉히.")]
    public float fillCircleLinger = 1.2f;
    [Tooltip("테두리 밝기 비율. 안쪽 채움보다 어둡게 해서 채워지는 게 도드라지게 한다.")]
    public float fillOutlineDim = 0.45f;
    [Tooltip("위 VFX 가 스케일 1 일 때 갖는 실제 반경(m). 원하는 반경에 맞추려고 나눈다.")]
    public float groundTelegraphUnitRadius = 1.2f;
    [Tooltip("전조가 뜨고 실제 공격이 나가기까지의 기본 시간(초). 패턴별로 덮어쓸 수 있다.")]
    public float telegraphTime = 1f;
    [Tooltip("전조/투사체 색. ★0~1 범위로 넣어라. 파티클 색은 8비트라 1을 넘으면 잘려서 " +
             "빨강을 넣어도 노랗게 나온다(코드가 색조는 지켜주지만 애초에 0~1 로 주는 게 정확하다).")]
    public Color telegraphColor = new Color(1f, 0.28f, 0.04f, 1f);
    [Tooltip("차지 전조 크기 배율.")]
    public float telegraphScale = 1f;
    [Tooltip("입/총구 앵커가 없을 때 쓰는 높이(발밑 기준).")]
    public float telegraphHeight = 1.2f;

    [Header("원거리 발사체")]
    [Tooltip("WyvernFireball 이 붙은 투사체 프리팹. 빌더가 조립한다.")]
    public GameObject projectilePrefab;
    [Tooltip("발사 순간 입에서 터지는 섬광.")]
    public GameObject muzzleVfx;

    [Header("공격 패턴 (몹별 개성)")]
    [Tooltip("가중치 랜덤으로 하나를 고른다. 비어 있으면 공격을 안 한다.")]
    public HellAttack[] attacks;
    [Tooltip("직전에 쓴 패턴의 가중치를 이 비율로 깎는다. 같은 패턴 연속을 줄인다.")]
    [Range(0f, 1f)] public float repeatPenalty = 0.35f;
    [Tooltip("★행동이 끝난 뒤 다음 행동까지의 최소 간격(초). 이게 없으면 쿨이 도는 대로 " +
             "쉴 새 없이 몰아쳐서 플레이어가 반격할 틈이 없다. 이 틈에 피격 반응도 나온다.")]
    public float actionGapMin = 1.2f;
    [Tooltip("행동 간격 최대치(초). 최소~최대 사이에서 무작위로 고른다.")]
    public float actionGapMax = 2f;

    [Header("도약 돌진 (JumpStart/JumpFly/JumpEnd 보유 몹)")]
    public bool useLeap = false;
    public float leapWeight = 1f;
    [Tooltip("이 거리 밖일 때만 도약으로 붙는다.")]
    public float leapMinRange = 6f;
    public float leapMaxRange = 14f;
    public float leapCooldown = 9f;
    [Tooltip("도약 체공 시간(초).")]
    public float leapFlyTime = 0.7f;
    [Tooltip("도약 포물선 최고 높이(m). 몸집이 크면 같이 키워야 뛰는 느낌이 난다.")]
    public float leapArcHeight = 2.2f;
    [Tooltip("착지 예고 시간(초). 이 동안 걸어 나오면 헛착지한다.")]
    public float leapTelegraphTime = 0.9f;
    [Tooltip("착지 예고 VFX 크기 배율.")]
    public float leapTelegraphScale = 1f;
    public float leapDamageMul = 1.2f;
    public float leapRadius = 2.6f;
    public GameObject leapImpactVfx;
    public string leapStartState = "JumpStart";
    public string leapFlyState = "JumpFly";
    public string leapEndState = "JumpEnd";

    [Header("잠복 (Submerge/Emerge 보유 몹)")]
    public bool useBurrow = false;
    public float burrowWeight = 1f;
    public float burrowCooldown = 14f;
    [Tooltip("땅속에 있는 시간(초). 이 동안 무적이 아니라 그냥 안 보인다.")]
    public float burrowUnderTime = 1.4f;
    [Tooltip("플레이어 발밑에서 이만큼 떨어진 곳으로 튀어나온다.")]
    public float burrowEmergeDistance = 2.5f;
    public float burrowDamageMul = 1.3f;
    public float burrowRadius = 3f;
    public string burrowInState = "Submerge";
    public string burrowOutState = "Emerge";
    public GameObject burrowImpactVfx;

    [Header("피격 반응")]
    [Tooltip("맞으면 흠칫하는 모션을 낸다. 공격/도약 중에는 끊지 않는다(패턴이 씹히면 답답하다).")]
    public bool useHitReaction = true;
    [Tooltip("피격 모션 상태 이름. 여러 개면 번갈아 쓴다.")]
    public string[] hitStates = { "GetHit" };
    [Tooltip("피격 모션 길이(초). 이 동안 이동이 멈춘다.")]
    public float hitReactionTime = 0.35f;
    [Tooltip("피격 모션 재생 최소 간격(초). 연타 맞을 때 계속 리셋되면 몹이 굳어버린다.")]
    public float hitReactionCooldown = 0.7f;
    [Tooltip("맞은 방향으로 밀리는 거리(m). 0 이면 안 밀린다.")]
    public float knockbackDistance = 0.35f;
    [Tooltip("밀리는 데 걸리는 시간(초).")]
    public float knockbackTime = 0.15f;

    [Header("애니 상태 이름")]
    public string idleState = "Idle";
    public string moveState = "Locomotion";
    public string roarState = "BattleRoar";
    public string dieState = "Die";
    [Tooltip("발견 시 포효를 한 번 하고 달려든다. 비우면 생략.")]
    public bool roarOnDetect = true;
    public float roarTime = 1.2f;
}

// 공격 패턴 1개. 전조 -> 공격 -> 타격 -> 회복 이 한 덩어리다.
[Serializable]
public class HellAttack
{
    [Tooltip("구분용 이름(로그/인스펙터에서만 쓴다).")]
    public string label = "Attack";
    [Tooltip("애니메이터 상태 이름.")]
    public string state = "Attack1";
    [Tooltip("가중치. 클수록 자주 나온다.")]
    public float weight = 1f;

    [Header("종류")]
    public HellAttackKind kind = HellAttackKind.Melee;
    [Tooltip("전조 형태. 빠른 평타는 None, 범위 근접은 Ground, 원거리는 Charge 가 어울린다.")]
    public HellTelegraphKind telegraph = HellTelegraphKind.None;
    [Tooltip("이 패턴의 전조 시간(초). 0 이면 SO 공통값을 쓴다.")]
    public float telegraphTime = 0f;
    [Tooltip("이 패턴의 전조 크기 배율(1=SO 기본). 너무 크면 줄인다.")]
    public float telegraphScaleMul = 1f;

    [Header("원거리 (kind=Ranged 일 때)")]
    [Tooltip("한 번에 쏘는 발수.")]
    public int shots = 1;
    [Tooltip("연발 간격(초).")]
    public float shotGap = 0.12f;
    [Tooltip("부채꼴로 흩뿌리는 좌우 각도(도). 0 이면 정면 집중.")]
    public float spreadAngle = 0f;
    [Tooltip("유도 여부. 전탄 유도는 근접 캐릭터에게 가혹하니 조심.")]
    public bool homing = false;

    [Header("사거리")]
    public float minRange = 0f;
    public float maxRange = 3f;

    [Header("타이밍 (전조 시간은 SO 공통값을 쓴다)")]
    [Tooltip("공격 모션 시작 후 타격이 들어가는 시점(초).")]
    public float hitTime = 0.45f;
    [Tooltip("공격 모션 전체 길이(초). 이 시간이 끝나야 다음 행동으로 넘어간다.")]
    public float totalTime = 1.4f;
    [Tooltip("이 패턴 전용 쿨타임(초).")]
    public float cooldown = 3f;

    [Header("판정")]
    [Tooltip("attackDamage 에 곱할 배율.")]
    public float damageMul = 1f;
    [Tooltip("0 이면 부채꼴 근접 판정, 0 보다 크면 그 반경의 범위 판정.")]
    public float radius = 0f;
    [Tooltip("부채꼴 근접일 때 좌우 각도(도).")]
    public float halfAngle = 70f;
    [Tooltip("판정 사거리. 0 이면 SO 의 attackRange 를 쓴다.")]
    public float reach = 0f;

    [Header("연출")]
    public GameObject impactVfx;
    [Tooltip("이 패턴만 전조 색을 다르게 하고 싶을 때. 알파 0 이면 SO 공통색을 쓴다.")]
    public Color telegraphColorOverride = new Color(0f, 0f, 0f, 0f);
    [Tooltip("전조 중 회전을 고정한다. 착지점을 미리 정하는 도약처럼 방향이 바뀌면 안 되는 경우.")]
    public bool lockFacing = false;
}
