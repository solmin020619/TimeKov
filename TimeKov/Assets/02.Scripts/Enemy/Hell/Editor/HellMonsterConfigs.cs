using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 헬 몬스터 지상 4종 설정 + 생성 메뉴.
/// 튜닝은 전부 여기서 한다. ★프리팹/SO 인스펙터에서 만지면 재빌드 때 날아간다.
/// 메뉴: Tools > Enemy > Hell
/// </summary>
public static class HellMonsterConfigs
{
    // Charge / Muzzle / Effect(투사체) / Hit 이 번호별로 짝이 맞는 팩이다.
    // 몹마다 번호 하나를 잡으면 모으기~날아감~착탄 톤이 자동으로 통일된다.
    const string VfxRoot = "Assets/00.창동에셋/VFX/VFX(전조)/ChargeProjectiles_Chargefx/Prefabs_Chargefx";
    static string Charge(int n) => $"{VfxRoot}/Charge/Charge_{n:00}.prefab";
    static string Muzzle(int n) => $"{VfxRoot}/Muzzle/Muzzle_{n:00}.prefab";
    static string Shot(int n) => $"{VfxRoot}/Projectile/Effect_{n:00}.prefab";
    static string Hit(int n) => $"{VfxRoot}/Hit/Hit_{n:00}.prefab";

    // 지면이 터져 올라오는 전용 VFX. 일반 착탄 VFX 와 확실히 구분된다.
    const string FloorBlast =
        "Assets/00.창동에셋/VFX/VFX(전조)/Anime VFX URP/Shared/Particles/VFX_Explosion_Floor.prefab";

    [MenuItem("Tools/Enemy/Hell/Build 헬하운드")]
    public static void BuildHound() => HellMonsterBuilder.Build(Hound());

    [MenuItem("Tools/Enemy/Hell/Build 헬뱃")]
    public static void BuildBat() => HellMonsterBuilder.Build(Bat());

    [MenuItem("Tools/Enemy/Hell/Build 헬버그")]
    public static void BuildBug() => HellMonsterBuilder.Build(Bug());

    [MenuItem("Tools/Enemy/Hell/Build 헬사이클롭")]
    public static void BuildCyclop() => HellMonsterBuilder.Build(Cyclop());

    [MenuItem("Tools/Enemy/Hell/Build 헬플라잉데몬")]
    public static void BuildDemon() => HellMonsterBuilder.Build(Demon());

    [MenuItem("Tools/Enemy/Hell/Build 5종 전부")]
    public static void BuildAll()
    {
        if (!EditorUtility.DisplayDialog("헬 5종 생성",
            "헬하운드 / 헬뱃 / 헬버그 / 헬사이클롭 / 헬플라잉데몬 을 전부 다시 굽는다.\n" +
            "프리팹과 SO 는 통째로 덮어써진다. 계속?", "생성", "취소")) return;
        BuildHound(); BuildBat(); BuildBug(); BuildCyclop(); BuildDemon();
    }

    // ── 헬하운드: 러셔. 물고 할퀴며 붙고, 멀면 도약으로 좁힌다.
    static HellConfig Hound() => new HellConfig
    {
        enemyName = "헬하운드", enemyId = "hell_hound", sourceId = "hell_hound",
        folderName = "헬하운드",
        // ★크기 1.5배. 콜라이더는 로컬값이라 scale 이 알아서 키우지만,
        //   월드 좌표로 도는 값들(사거리/시야높이/전조높이/입오프셋)은 여기서 직접 1.5배 해줬다.
        maxHP = 110, moveSpeed = 5.4f, attackDamage = 16f, attackRange = 3.9f, attackCooldown = 1.4f,
        visionRange = 20f, visionAngle = 300f,
        bodyHeight = 1.6f, bodyRadius = 0.6f, scale = 1.5f, eyeHeight = 1.8f,
        clipIdle = "Idle1", clipWalk = "Walk", clipRun = "Run",
        clipRoar = "BattleRoar1", clipDeath = "Death",
        roarTime = 1.3f,
        // jaw 본을 앵커로 잡으므로 거의 보정이 필요 없다. 살짝만 앞으로.
        // (모델 기준 값이라 scale 을 바꿔도 알아서 따라간다)
        muzzleBoneHint = "jaw",
        muzzleOffset = new Vector3(0f, 0f, 0.15f),
        // ★★종욱 승인 상태. 7번 팩(원래 보라/마젠타)에 보라 틴트를 먹인 조합이다.
        //   tintVfx 를 끄면 승인받은 그림이 아니게 되므로 이 몹만 명시적으로 켠다.
        telegraphVfxPath = Charge(7), muzzleVfxPath = Muzzle(7),
        projectileVfxPath = Shot(7), projectileHitVfxPath = Hit(7),
        tintVfx = true,
        telegraphTime = 0.85f, telegraphHeight = 1.65f, telegraphScale = 1.4f,
        // ★파티클 색은 0~1 로 넣는다. HDR(8, 1.1, 0.1) 처럼 넣으면 잘려서 노랗게 나온다.
        //   녹은 쇳물 느낌 = 빨강 베이스에 주황이 살짝.
        telegraphColor = new Color(0.72f, 0.16f, 1f, 1f),
        // 단발이라 크고 묵직하게. 판정도 비주얼에 맞춰 같이 키운다.
        projectileScale = 1.8f, projectileHitRadius = 1.3f, projectileExplodeRadius = 2.6f,
        projectileSpeed = 13f,
        // ★공격은 딱 2종. 밋밋한 근접 평타(물기/할퀴기/강타)는 전부 뺐다.
        //   멀면 불을 뿜고, 붙을 땐 덮친다. 두 개 다 전조가 확실해서 읽고 피할 수 있다.
        attacks = new List<HellAttackConfig>
        {
            // minRange 0 = 코앞에서도 뿜는다. 근접 평타를 뺐기 때문에 이게 없으면
            // 붙어 있을 때 도약 쿨 도는 동안 아무것도 안 하고 서 있는 구멍이 생긴다.
            // 3갈래로 흩뿌리면 하나하나가 빈약해 보인다. 큰 거 한 방이 낫다.
            new HellAttackConfig { label = "화염토해내기", state = "Spit", clipName = "BattleRoar2",
                kind = HellAttackKind.Ranged, telegraph = HellTelegraphKind.Charge,
                weight = 1f, minRange = 0f, maxRange = 18f,
                hitTime = 0.5f, totalTime = 1.8f, cooldown = 4f, damageMul = 1.1f,
                telegraphTime = 1f, telegraphScaleMul = 0.6f,
                shots = 1, spreadAngle = 0f },
        },
        // 도약이 곧 근접 공격이다. 붙어 있을 때도 쓸 수 있게 최소거리를 없앴다.
        useLeap = true, leapWeight = 1.2f, leapMinRange = 2.5f, leapMaxRange = 15f,
        // ★leapRadius = 보이는 원 반경(m) = 실제 피해 범위. 일반몹이라 3.2m 면 충분하다.
        //   메시 bounds 로 실측해 맞추므로 이 숫자 그대로 화면에 나온다.
        leapCooldown = 5f, leapDamageMul = 1.3f, leapRadius = 3.2f, leapImpactVfxPath = Hit(7),
        leapArcHeight = 3.2f,
        // 착지 자리에 원이 뜨고, 다 차오르는 순간 덮친다. 그 사이에 걸어 나오면 헛착지.
        leapTelegraphTime = 1.1f, leapTelegraphScale = 1f,
    };

    // ── 헬뱃: 날렵한 견제형. 짧게 치고 빠지는 두 방. 도약으로 거리 재조정.
    static HellConfig Bat() => new HellConfig
    {
        enemyName = "헬뱃", enemyId = "hell_bat", sourceId = "hell_bat",
        folderName = "헬뱃",
        maxHP = 80, moveSpeed = 5.2f, attackDamage = 13f, attackRange = 2.3f, attackCooldown = 1.2f,
        visionRange = 18f, visionAngle = 280f,
        bodyHeight = 1.5f, bodyRadius = 0.55f, scale = 1f, eyeHeight = 1.2f,
        clipIdle = "Idle1", clipWalk = "Walk", clipRun = "Run",
        clipRoar = "BattleRoar1", clipDeath = "Death",
        roarTime = 1.1f,
        // ★VFX 팩 4번 -> 3번. 4번은 스프라이트 자체가 연두/라임이라 용암 몹에 안 어울렸다(종욱 지적).
        //   3번은 진홍 + 황금이라 불 계열에 맞고, 헬버그(6번 빨강/주황)와도 구분된다.
        //   팩 색은 파티클이나 머티리얼이 아니라 스프라이트 그림에 박혀 있다.
        //   -> 틴트로 바꾸려 하면 그림 색과 곱해져 탁해진다. 팩 번호로 고르는 게 정답이다.
        telegraphVfxPath = Charge(3), muzzleVfxPath = Muzzle(3),
        projectileVfxPath = Shot(3), projectileHitVfxPath = Hit(3),
        telegraphTime = 0.75f, telegraphHeight = 1.2f,
        projectileSpeed = 17f,
        attacks = new List<HellAttackConfig>
        {
            // ★근접 평타에는 전조 VFX 를 안 붙인다(헬버그와 같은 처리, 종욱 승인).
            //   바닥에 원이 깔리면 평타 하나하나가 필살기처럼 무거워 보인다.
            //   준비동작(windupState) + 공격 모션 자체가 예고 역할을 한다.
            new HellAttackConfig { label = "할퀴기", state = "Attack1", clipName = "Attack1",
                weight = 1.3f, maxRange = 2.5f, hitTime = 0.35f, totalTime = 1f, cooldown = 1.8f, damageMul = 1f },
            new HellAttackConfig { label = "연타", state = "Attack2", clipName = "Attack2",
                weight = 1f, maxRange = 2.7f, hitTime = 0.45f, totalTime = 1.4f, cooldown = 3.2f,
                damageMul = 1.25f, impactVfxPath = Hit(3), impactScale = 0.7f },
            // ★원거리: 빠른 2연발 음파탄. 견제형이라 아프지 않게였는데, damageMul 0.7(13x0.7=9.1)이
            //   플레이어 DEF(10)보다 낮아 항상 최소 1 로 바닥쳤다. 체감되게 1.2 로(15.6 -> DEF 빼면 ~5/발).
            new HellAttackConfig { label = "음파탄", state = "Screech", clipName = "BattleRoar2",
                kind = HellAttackKind.Ranged, telegraph = HellTelegraphKind.Charge,
                weight = 1.2f, minRange = 4f, maxRange = 16f,
                hitTime = 0.4f, totalTime = 1.4f, cooldown = 4.5f, damageMul = 1.2f,
                telegraphTime = 0.8f, telegraphScaleMul = 0.55f,
                shots = 2, shotGap = 0.18f, spreadAngle = 8f },
        },
        useLeap = true, leapWeight = 1.1f, leapMinRange = 5f, leapMaxRange = 12f,
        leapCooldown = 6.5f, leapFlyTime = 0.6f, leapDamageMul = 1.1f, leapRadius = 2.3f,
        leapArcHeight = 2.8f, leapTelegraphTime = 0.95f,
        leapImpactVfxPath = Hit(3),
    };

    // ── 헬버그: 잠복형. 땅에 숨었다 플레이어 옆에서 튀어나온다. 시그니처.
    static HellConfig Bug() => new HellConfig
    {
        enemyName = "헬버그", enemyId = "hell_bug", sourceId = "hell_bug",
        folderName = "헬버그",
        maxHP = 130, moveSpeed = 3.6f, attackDamage = 18f, attackRange = 2.8f, attackCooldown = 1.8f,
        visionRange = 17f, visionAngle = 360f,
        bodyHeight = 1.4f, bodyRadius = 0.75f, scale = 1f, eyeHeight = 1.0f,
        clipIdle = "Idle", clipWalk = "Walk", clipRun = "Run",   // ★이 몹만 Idle1 이 아니라 Idle
        clipRoar = "BattleRoar", clipDeath = "Death",            // ★BattleRoar1 이 아니라 BattleRoar
        // ★이 몹만 피격 클립이 GetHit 하나뿐이다(다른 3종은 GetHit1/GetHit2).
        hitStates = new[] { "GetHit" }, hitClips = new[] { "GetHit" },
        roarTime = 1.2f,
        // ★★종욱 승인: 헬버그 원거리 VFX 는 이 6번 팩 그대로가 정답이다. 번호를 바꾸지 마라.
        //   tintVfx=false 라 팩 원래 색을 그대로 쓴다 -> 모을 때/나갈 때/맞을 때 색이 저절로 일치한다.
        telegraphVfxPath = Charge(6), muzzleVfxPath = Muzzle(6),
        projectileVfxPath = Shot(6), projectileHitVfxPath = Hit(6),
        telegraphTime = 1f, telegraphHeight = 0.9f,
        projectileSpeed = 11f,
        attacks = new List<HellAttackConfig>
        {
            // ★근접 평타에는 전조 VFX 를 안 붙인다(종욱 판단).
            //   바닥에 원이 깔리면 평타 하나하나가 필살기처럼 무거워 보인다.
            //   대신 준비동작(windupState) + 공격 모션 자체가 예고 역할을 한다.
            new HellAttackConfig { label = "물어뜯기", state = "Attack1", clipName = "Attack1",
                weight = 1.2f, maxRange = 3f, hitTime = 0.5f, totalTime = 1.4f, cooldown = 2.6f, damageMul = 1f },
            // 원 없이 넓은 원형 판정을 두면 안 보이는 데서 맞는다 -> 앞쪽 부채꼴로 바꾼다.
            // 휩쓰는 모션이 곧 판정 방향이라 눈으로 읽힌다.
            new HellAttackConfig { label = "휩쓸기", state = "Attack2", clipName = "Attack2",
                weight = 0.9f, maxRange = 3.4f, hitTime = 0.6f, totalTime = 1.7f, cooldown = 4f,
                damageMul = 1.3f, halfAngle = 120f, reach = 3.4f },
            // ★원거리: 산성 침을 넓게 뿌린다. 느린 탄이라 옆으로 걸으면 피해진다.
            new HellAttackConfig { label = "산성침", state = "Spit", clipName = "BattleRoar",
                kind = HellAttackKind.Ranged, telegraph = HellTelegraphKind.Charge,
                weight = 1f, minRange = 4.5f, maxRange = 15f,
                hitTime = 0.55f, totalTime = 1.9f, cooldown = 6.5f, damageMul = 0.75f,
                telegraphTime = 1.05f, telegraphScaleMul = 0.6f,
                shots = 3, shotGap = 0.1f, spreadAngle = 18f },
        },
        // ★튀어나오는 것 자체가 공격이다. 나온 뒤에 따로 뭘 쏘지 않는다.
        //   ★플레이어 발밑 정확히(emergeDistance 0) 나오고, 판정도 그 자리 기준으로 좁게 준다.
        //     떨어져 나오는데 딜이 들어가면 "왜 맞았는지" 가 안 읽힌다.
        //   commitTime 0.55초 = 자리 확정 후 솟구치기까지. 이게 회피 창이다.
        useBurrow = true, burrowWeight = 1.2f, burrowCooldown = 13f,
        burrowUnderTime = 1.6f, burrowCommitTime = 0.55f, burrowEmergeDistance = 0f,
        burrowDamageMul = 1.5f, burrowRadius = 2f,
        clipSubmerge = "Submerge", clipEmerge = "Emerge", burrowImpactVfxPath = Hit(6),
    };

    // ── 헬플라잉데몬: 부유 시전형. 땅에 안 닿고 떠서 다니며 주문을 쏜다.
    //
    // ★"비행체라 지상 몹으로 못 쓴다"는 판단은 틀렸다. 실제 클립을 보면
    //   Fly1/2/3(이동 3단계) + Idle1/2 + Attack1/2 + Death 가 다 있고, 없는 건 걷기/뛰기뿐이다.
    //   FlyDeath 와 별개로 지상용 Death 가 따로 있는 것부터가 제작사가 지상 사용을 상정했다는 뜻이다.
    //   -> 이동 블렌드트리에 Fly 클립을 넣고 hoverHeight 로 몸만 띄우면 그대로 지상 몹이 된다.
    //      길찾기는 지상 NavMesh 를 그대로 쓰므로 특별 처리가 전혀 없다.
    static HellConfig Demon() => new HellConfig
    {
        enemyName = "헬플라잉데몬", enemyId = "hell_flying_demon", sourceId = "hell_flying_demon",
        folderName = "헬플라잉데몬",
        maxHP = 90, moveSpeed = 4.4f, attackDamage = 15f, attackRange = 2.6f, attackCooldown = 1.6f,
        visionRange = 20f, visionAngle = 300f,
        bodyHeight = 1.7f, bodyRadius = 0.6f, scale = 1f, eyeHeight = 1.3f,
        // ★몸만 띄운다. 다리가 땅에 닿으면 걷기 클립이 없는 게 바로 티가 난다.
        hoverHeight = 1.1f,
        // ★Walk/Run 자리에 Fly 를 넣는다. 루트모션은 꺼져 있어서 클립의 전진은 무시되고
        //   실제 이동은 NavMeshAgent 가 한다. 속도에 따라 Fly1 -> Fly2 로 섞인다.
        clipIdle = "Idle1", clipWalk = "Fly1", clipRun = "Fly2",
        clipRoar = "BattleRoar", clipDeath = "Death",
        // 피격 클립이 3종이라 다 돌려쓴다.
        hitStates = new[] { "GetHit1", "GetHit2", "GetHit3" },
        hitClips = new[] { "GetHit1", "GetHit2", "GetHit3" },
        roarTime = 1.2f,
        // ★2번 팩(연두+마젠타)은 종욱이 반려. 형광 연두가 섞여 있어 용암 몹에 안 어울린다.
        //   남은 미사용 팩은 4번(연두/라임 - 헬뱃에서 이미 반려)과 5번(청색 - 사이클롭 1번과 중복)뿐이라
        //   새 후보가 없다. 다른 라이브러리도 뒤졌는데 Lightning 세트는 청록이라 같은 문제다.
        //   -> 승인된 6번(빨강/주황)을 쓴다. 같은 헬 계열이라 톤을 공유해도 어색하지 않고,
        //      헬버그는 잠복 위주라 원거리를 보여줄 일이 적어서 실제로 겹쳐 보일 일이 드물다.
        telegraphVfxPath = Charge(6), muzzleVfxPath = Muzzle(6),
        projectileVfxPath = Shot(6), projectileHitVfxPath = Hit(6),
        telegraphTime = 1f, telegraphHeight = 1.4f,
        // 한 발이라 크고 묵직하게. 판정도 비주얼에 맞춰 키운다.
        projectileScale = 1.5f, projectileHitRadius = 1.1f, projectileExplodeRadius = 2.2f,
        // ★★오른손 고정. 본이 hand_l / hand_r 두 개인데 "hand" 부분일치는 hand_l(왼손)을 먼저 잡는다.
        //   시전 모션은 오른손을 뻗어서 "왼손에서 모으고 오른손으로 쏘는" 그림이 됐다(종욱 지적).
        muzzleBoneHint = "hand_r",
        muzzleOffset = new Vector3(0f, 0f, 0.25f),
        projectileSpeed = 15f,
        // ★패턴 3종 = 전부 마법. 근접은 통째로 뺐다(종욱 판단).
        //   부유해서 떠 있는 몹이 할퀴려고 내려오는 게 어색했고, 패턴도 2개뿐이라 심심했다.
        //   시전 클립이 5종이나 있는 몹이니 그쪽으로 몰아주는 게 이 에셋을 제대로 쓰는 길이다.
        //   거리별 역할이 안 겹치게 짰다: 멀면 저주탄, 발밑엔 분출, 붙으면 절규.
        attacks = new List<HellAttackConfig>
        {
            // 1) 저주탄: 조준 유도탄. 옆으로 굴러야 피해진다.
            //    ★손을 뻗은 자세로 먼저 잡고, 그 손에서 모았다가 쏜다.
            //      대기 자세로 모으다가 발사 순간에 손을 뻗으면 앞뒤가 뒤바뀐 것처럼 보인다(종욱 지적).
            //      CastToTargetStart 는 비루프라 마지막 프레임(손 뻗은 자세)에서 멈추는데, 여기선 그게 정답이다.
            new HellAttackConfig { label = "저주탄", state = "Cast", clipName = "CastToTarget",
                windupState = "CastReady", windupClip = "CastToTargetStart",
                kind = HellAttackKind.Ranged, telegraph = HellTelegraphKind.Charge,
                weight = 1.2f, minRange = 4f, maxRange = 18f,
                hitTime = 0.35f, totalTime = 1.8f, cooldown = 4.5f, damageMul = 1f,
                telegraphTime = 1.1f, telegraphScaleMul = 0.6f,
                shots = 1, spreadAngle = 0f, homing = true },

            // 2) 지옥불 분출: 플레이어 발밑에 빨간 원이 차오르고, 다 차면 그 자리 땅이 터진다.
            //    ★원이 뜨는 순간의 자리로 못박히므로 걸어 나오면 헛방 = 회피가 성립한다.
            //    minRange 0 이라 코앞에서도 쓴다. 근접을 뺀 자리를 이게 메운다.
            //    ★착탄을 전용 "바닥 폭발" VFX 로 바꿨다. 일반 착탄 VFX 를 쓰니
            //      뭐가 올라오는 공격인지 안 읽혔다(종욱 지적).
            new HellAttackConfig { label = "지옥불분출", state = "Erupt", clipName = "CastStart",
                windupState = "CastReady", windupClip = "CastToTargetStart",
                telegraph = HellTelegraphKind.FillCircle, groundTargetsPlayer = true,
                weight = 1.1f, minRange = 0f, maxRange = 14f,
                hitTime = 0.35f, totalTime = 1.4f, cooldown = 6f, damageMul = 1.3f,
                radius = 2.6f, telegraphTime = 1.15f,
                impactVfxPath = FloorBlast, impactScale = 1.6f, impactLift = 0f },
        },
        // 3) ★강하 내려꽂기 = 이 몹만 할 수 있는 공격.
        //    떠 있다는 특성을 실제로 쓴다. 높이 떠올랐다가 표시된 자리로 내리꽂는다.
        //    앞의 두 패턴이 "제자리에서 마법"이라 서로 헷갈렸는데,
        //    이건 몹이 물리적으로 날아오므로 절대 안 헷갈린다.
        //    Jump 클립이 없어서 비행 클립(Fly3)을 상승/체공에, 착지는 안 쓰던 Attack2(후려치기)로 돌려썼다.
        useLeap = true, leapWeight = 1f, leapMinRange = 3f, leapMaxRange = 14f,
        leapCooldown = 8f, leapFlyTime = 0.75f, leapDamageMul = 1.4f, leapRadius = 3f,
        // 날개 달린 놈이라 포물선을 높게. 떠올랐다 꽂히는 게 보여야 한다.
        leapArcHeight = 5f, leapTelegraphTime = 1.2f,
        clipJumpStart = "Fly3", clipJumpFly = "Fly3", clipJumpEnd = "Attack2",
        leapImpactVfxPath = FloorBlast,
    };

    // ── 헬사이클롭: 준보스. 느리고 아프다. 전조가 제일 길어서 확실히 보고 피할 수 있다.
    static HellConfig Cyclop() => new HellConfig
    {
        enemyName = "헬사이클롭", enemyId = "hell_cyclop", sourceId = "hell_cyclop",
        folderName = "헬사이클롭",
        maxHP = 260, moveSpeed = 3.2f, attackDamage = 24f, attackRange = 3.4f, attackCooldown = 2.4f,
        visionRange = 22f, visionAngle = 270f,
        bodyHeight = 2.6f, bodyRadius = 0.95f, scale = 1f, eyeHeight = 2.0f,
        clipIdle = "Idle1", clipWalk = "Walk", clipRun = "Run",
        clipRoar = "BattleRoar", clipDeath = "Death",
        roarTime = 1.6f,
        // ★★종욱 승인: 원거리 VFX 는 이 1번 팩(푸른 계열) 그대로가 정답이다. 번호를 바꾸지 마라.
        //   외눈에서 나가는 광선이라 오히려 푸른 쪽이 준보스답게 읽힌다.
        //   tintVfx=false -> 팩 원래 색 유지 = 모을 때/나갈 때/맞을 때 저절로 일치.
        telegraphVfxPath = Charge(1), muzzleVfxPath = Muzzle(1),
        projectileVfxPath = Shot(1), projectileHitVfxPath = Hit(1),
        telegraphTime = 1.25f, telegraphHeight = 1.9f,
        telegraphScale = 1.3f,
        // ★차지를 눈 높이에서 모으게 머리 본을 직접 지정한다.
        //   자동 탐색은 jaw -> mouth -> ... -> head 순서라 턱뼈가 있으면 턱에서 모인다.
        //   외눈박이는 눈에서 나가는 게 맞으므로 머리로 못박고 앞으로 조금 뺀다.
        muzzleBoneHint = "head",
        muzzleOffset = new Vector3(0f, 0.15f, 0.55f),
        projectileSpeed = 13f, projectileExplodeRadius = 2.6f,
        attacks = new List<HellAttackConfig>
        {
            // ★내려찍기/양손내려치기는 넓은 원형 판정이라 바닥 표시가 반드시 필요하다.
            //   단 예전의 검은 시전진 말고, 종욱이 승인한 도약 착지 예고와 같은 "차오르는 빨간 원"을 쓴다.
            //   원이 다 차면 곧 내려찍는다 = 언어가 도약과 동일해진다.
            new HellAttackConfig { label = "내려찍기", state = "Attack1", clipName = "Attack1",
                weight = 1.2f, maxRange = 3.6f, hitTime = 0.65f, totalTime = 1.9f, cooldown = 3.5f,
                damageMul = 1f, radius = 2.6f, impactVfxPath = Hit(1),
                impactScale = 0.55f, impactLift = 0.6f,
                telegraph = HellTelegraphKind.FillCircle, telegraphTime = 0.9f },
            // 부채꼴은 앞쪽 100도만 위험한데 원을 깔면 사방이 위험한 것처럼 보인다.
            // 다른 몹 근접 평타와 같은 처리 = 전조 VFX 없이 준비동작 + 모션으로 읽힌다.
            new HellAttackConfig { label = "휘두르기", state = "Attack2", clipName = "Attack2",
                weight = 1f, maxRange = 4f, hitTime = 0.7f, totalTime = 2f, cooldown = 4.5f,
                damageMul = 1.2f, halfAngle = 100f, reach = 4f,
                impactScale = 0.5f, impactLift = 0.9f },
            new HellAttackConfig { label = "양손내려치기", state = "Attack4", clipName = "Attack4",
                weight = 0.7f, maxRange = 3.8f, hitTime = 0.85f, totalTime = 2.4f, cooldown = 7f,
                damageMul = 1.8f, radius = 3.4f, impactVfxPath = Hit(1),
                impactScale = 0.65f, impactLift = 0.6f,
                telegraph = HellTelegraphKind.FillCircle, telegraphTime = 1.2f },
            // ★원거리: 클립을 CastToTarget -> BattleRoar 로 바꿨다.
            //   CastToTarget 은 딱 발사 타이밍에 손을 드는 모션인데, 차지는 얼굴에서 모인다.
            //   "얼굴에 모아놓고 갑자기 손으로 쏘는" 그림이라 어색했다(종욱 지적).
            //   포효 모션이면 모으는 곳과 뿜는 곳이 둘 다 얼굴이라 눈에서 나가는 느낌이 된다.
            new HellAttackConfig { label = "화염구", state = "Cast", clipName = "BattleRoar",
                kind = HellAttackKind.Ranged, telegraph = HellTelegraphKind.Charge,
                weight = 1.1f, minRange = 5f, maxRange = 20f,
                hitTime = 0.7f, totalTime = 2.2f, cooldown = 6f, damageMul = 0.9f,
                telegraphTime = 1.25f, telegraphScaleMul = 0.7f,
                shots = 1, homing = true },
        },
        useLeap = true, leapWeight = 0.6f, leapMinRange = 7f, leapMaxRange = 15f,
        leapCooldown = 12f, leapFlyTime = 0.85f, leapDamageMul = 1.6f, leapRadius = 3.4f,
        // 덩치가 커서 포물선도 같이 키워야 뛰는 느낌이 난다(헬하운드에서 확인).
        leapArcHeight = 3.4f, leapTelegraphTime = 1.3f,
        clipJumpStart = "JumpStart", clipJumpFly = "JumpFly", clipJumpEnd = "JumpEnd",
        leapImpactVfxPath = Hit(1),
    };
}

/// 몹 1종의 전체 설정. 빌더가 이것만 보고 굽는다.
public class HellConfig
{
    public string enemyName = "", enemyId = "", sourceId = "", folderName = "";

    public float maxHP = 100, moveSpeed = 4f, attackDamage = 15f, attackRange = 2.5f, attackCooldown = 1.5f;
    public float visionRange = 18f, visionAngle = 300f;
    public float bodyHeight = 1.8f, bodyRadius = 0.6f, scale = 1f;
    // ★부유 높이(m). NavMeshAgent.baseOffset 에 들어간다.
    //   길은 지상 NavMesh 를 그대로 따라가되 몸만 이만큼 떠 있게 만든다.
    //   걷기/뛰기 클립이 없는 비행 모델을 지상 몹으로 쓰려면 이게 필요하다.
    public float hoverHeight = 0f;
    public float deathAnimDuration = 2f;
    // 사망 클립이 끝나고 시체가 남아있는 여유 시간(초). 툭 사라지면 어색하다.
    public float deathExtraTime = 0.8f;
    public float eyeHeight = 1.6f;   // 시야 레이 시작 높이. 큰 몹은 올린다.

    public string clipIdle = "Idle1", clipWalk = "Walk", clipRun = "Run";
    public string clipRoar = "BattleRoar1", clipDeath = "Death";
    // ★전조(준비동작) 중에 재생할 전투 대기 자세. 없는 몹은 빌더가 Idle 로 대체한다.
    public string clipWindup = "BattleIdle";
    public float walkSpeedRef = 2f, runSpeedRef = 5f;
    // 포효 길이는 클립에서 잰다. 이 값은 클립을 못 읽었을 때의 대비책.
    public float roarTime = 1.2f;
    public float roarRatio = 0.95f;
    // 도약 마무리(JumpEnd) 를 클립의 몇 %까지 볼지. 회복 꼬리는 조금 잘라도 자연스럽다.
    public float leapEndRatio = 0.8f;
    // ★행동 사이 휴식 때 설 거리 = attackRange x 이 값.
    //   0 이면 계속 플레이어 몸에 파고든다.
    public float standoffRatio = 0.85f;

    // 평상시 어슬렁(플레이어 발견 전). 가만히 서 있으면 죽어 있는 것처럼 보인다.
    // ★기존 BT 몹(오크트리 등)이 꽤 활발하다. 거기에 맞춰 텀은 짧게, 속도는 좀 더 빠르게.
    public bool wander = true;
    public float wanderRadius = 7f;
    public Vector2 wanderPauseRange = new Vector2(0.6f, 1.8f);
    public float wanderSpeedMul = 0.55f;

    public string telegraphVfxPath = "";
    // ★근접 전조(바닥 원).
    //   예전엔 VFX_Debuff_Cast(디버프 시전진)를 썼는데 이게 종욱이 지적한 "검정색 VFX" 였다.
    //   덩치 큰 몹일수록 크게 깔려서 땅에 박힌 것처럼 보였다.
    //   -> 도약 착지 예고에 쓰는 빨간 원(승인받은 것)과 같은 걸로 통일한다.
    //      바닥에 깔리는 원 = 항상 빨강 = 여기 맞는다, 로 언어가 하나가 된다.
    public string groundTelegraphVfxPath =
        "Assets/00.창동에셋/VFX/VFX(눈)/Elemental VFX Mega Bundle/Frost/AOE Magic Circle Snowburst.prefab";
    // 저작된 VFX 라 스케일 1 일 때의 실제 반경. 이걸로 나눠서 원하는 반경에 맞춘다.
    public float groundTelegraphUnitRadius = 2f;

    // 차오르는 원(도약 착지 예고). 저작된 AOE 마법진을 빨갛게 물들여 쓴다.
    public float fillCircleUnitRadius = 2f, fillCircleFromScale = 0.15f, fillCircleLinger = 1.2f;
    public Color fillCircleColor = new Color(1f, 0.34f, 0.06f, 1f);
    public float fillOutlineDim = 0.45f;
    public float actionGapMin = 1.2f, actionGapMax = 2f;
    // 원 색은 fillCircleColor + VfxTint 가 결정한다. 머티리얼을 따로 지정하지 않는다.
    // (예전엔 링 메시를 조립했는데 그 머티리얼이 파티클 전용이라 안 보였다 -> 저작 VFX 로 교체)
    public string fillCircleVfxPath =
        "Assets/00.창동에셋/VFX/VFX(눈)/Elemental VFX Mega Bundle/Frost/AOE Magic Circle Snowburst.prefab";

    // 피격 반응
    public bool useHitReaction = true;
    public string[] hitStates = { "GetHit1", "GetHit2" };
    public string[] hitClips = { "GetHit1", "GetHit2" };
    public float hitReactionTime = 0.35f, hitReactionCooldown = 0.7f;
    // ★피격 클립을 끝까지 본다(1.0). 예전엔 0.7 로 잘랐는데, 모션이 아직 움직이는 중에
    //   다음 행동이 시작돼서 "맞는 도중에 전조가 뜬다"로 보였다.
    public float hitReactionRatio = 1f;
    // 흠칫이 끝난 뒤 전투 자세로 한 박자 쉬는 시간. 이 틈이 피격과 다음 동작을 갈라준다.
    public float hitRecoveryTime = 0.3f;
    // 준비동작 중에 맞으면 공격이 끊긴다. 여러 마리가 몰려나오는 일반몹이라 켜도 무방하다.
    public bool hitCanInterruptAttack = true;
    public float knockbackDistance = 0.35f, knockbackTime = 0.15f;
    // 타격감. 히트스톱 = 맞는 순간 시간이 살짝 늦어짐.
    public float hitStopTime = 0.05f, hitStopScale = 0.08f;
    public float telegraphTime = 0.9f, telegraphScale = 1f, telegraphHeight = 1.2f;
    public Color telegraphColor = new Color(4f, 0.7f, 0.15f, 1f);
    // ★기본은 "VFX 팩 색을 안 건드린다".
    //   차지/총구/탄/착탄은 번호별 한 세트라 그대로 두면 색이 저절로 일치한다.
    //   틴트를 먹이면 파티클만 바뀌고 텍스처는 안 바뀌어서 모을 때/나갈 때/맞을 때가 어긋난다.
    public bool tintVfx = false;

    // 입 앵커. 빌더가 이름으로 찾는다. 위치가 어긋나면 정확한 본 이름을 여기 적는다.
    public string muzzleBoneHint = "";
    public Vector3 muzzleOffset = new Vector3(0f, 0f, 0.5f);

    // 원거리 (패턴에 kind=Ranged 가 하나라도 있으면 빌더가 투사체를 굽는다)
    public string projectileVfxPath = "", projectileHitVfxPath = "", muzzleVfxPath = "";
    public float projectileSpeed = 14f, projectileHitRadius = 0.9f, projectileExplodeRadius = 2f;
    public float projectileScale = 1f;
    public bool HasRanged => attacks != null && attacks.Exists(a => a.kind == HellAttackKind.Ranged);

    public List<HellAttackConfig> attacks = new List<HellAttackConfig>();

    public bool useLeap = false;
    public float leapWeight = 1f, leapMinRange = 6f, leapMaxRange = 14f, leapCooldown = 9f;
    public float leapFlyTime = 0.7f, leapDamageMul = 1.2f, leapRadius = 2.6f;
    public float leapArcHeight = 2.2f;
    public float leapTelegraphTime = 0.9f, leapTelegraphScale = 1f;
    public string clipJumpStart = "JumpStart", clipJumpFly = "JumpFly", clipJumpEnd = "JumpEnd";
    public string leapImpactVfxPath = "";

    public bool useBurrow = false;
    public float burrowWeight = 1f, burrowCooldown = 14f, burrowUnderTime = 1.4f;
    // ★자리를 확정하고 솟구치기까지의 시간 = 회피 창.
    //   emergeDistance 0 = 플레이어 발밑 정확히. 떨어뜨리면 "옆에서 나왔는데 왜 맞지"가 된다.
    public float burrowCommitTime = 0.5f;
    public float burrowEmergeDistance = 0f, burrowDamageMul = 1.3f, burrowRadius = 3f;
    public string clipSubmerge = "Submerge", clipEmerge = "Emerge";
    // 등장 모션의 몇 % 지점에서 타격이 들어가는지. 몸이 지면을 뚫고 올라온 순간에 맞춘다.
    public float burrowHitRatio = 0.45f;
    public string burrowImpactVfxPath = "";

    // 경로 규약
    public string AnimFolder => $"Assets/00.창동에셋/몬스터/{folderName}/Animations";
    public string modelPath => $"Assets/00.창동에셋/몬스터/{folderName}/Prefabs/{folderName}.prefab";
    public string WorkFolder => $"Assets/05.Prefabs/Enemy/{folderName}";
    public string CtrlPath => $"{WorkFolder}/{folderName}_Enemy.controller";
    public string SoFolder => "Assets/05.Prefabs/Enemy/SO";
    public string SoPath => $"{SoFolder}/HellData_{enemyId}.asset";
    public string PrefabName => $"Enemy_{enemyId}";
    public string PrefabPath => $"Assets/05.Prefabs/Enemy/{PrefabName}.prefab";
}

/// 공격 패턴 1개의 설정(빌더 입력용). 런타임 HellAttack 으로 변환된다.
public class HellAttackConfig
{
    public string label = "Attack", state = "Attack1", clipName = "Attack1";
    // 패턴 전용 준비 자세(상태명/클립). 비우면 SO 공통 windupState 를 쓴다.
    public string windupState = "", windupClip = "";
    public float weight = 1f;
    public float minRange = 0f, maxRange = 3f;
    // ★totalTime 은 이제 클립 길이에서 자동 계산된다(빌더가 실측). 여기 값은 클립을 못 읽었을 때의 대비책.
    //   totalTimeRatio = 클립의 몇 %를 볼지. 1 이면 끝까지, 0.9 면 회복 꼬리를 살짝 자른다.
    public float hitTime = 0.45f, totalTime = 1.4f, cooldown = 3f;
    public float totalTimeRatio = 0.92f;
    // 타격 이후 회복 동작을 최대 몇 초까지 볼지. 긴 클립(포효 등)을 공격으로 돌려쓸 때
    // 몹이 한참 굳어 있는 걸 막는 상한이다.
    public float tailMax = 0.9f;
    public float damageMul = 1f, radius = 0f, halfAngle = 70f, reach = 0f;
    public string impactVfxPath = "";
    // 착탄 VFX 크기/높이. 지면 강타는 크기를 줄이고 살짝 띄워야 안 묻힌다.
    public float impactScale = 1f, impactLift = 0.5f;

    public HellAttackKind kind = HellAttackKind.Melee;
    public HellTelegraphKind telegraph = HellTelegraphKind.None;
    public float telegraphTime = 0f, telegraphScaleMul = 1f;
    public int shots = 1;
    public float shotGap = 0.12f, spreadAngle = 0f;
    public bool homing = false;
    public bool lockFacing = false;
    // 범위 판정을 몹 앞이 아니라 플레이어 발밑에 잡는다(분출류).
    public bool groundTargetsPlayer = false;
}
