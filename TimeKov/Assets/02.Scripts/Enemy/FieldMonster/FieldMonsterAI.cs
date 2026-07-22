using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 신규 필드 몬스터 전용 AI. 기존 EnemyBrain/BehaviorGraph 를 쓰지 않고 자체 FSM 으로 돈다.
/// (기존 스크립트는 하나도 수정하지 않고, 필요한 것만 '참조'해서 재사용)
///
/// 리듬:  대기/어슬렁 → 발견 → [오프닝 스텝] → 접근 → 전조 번쩍 → 공격 → [스텝] → 재공격
///        ※ 발견하자마자 때리지 않는다. 공격 사이엔 반드시 옆/뒤로 빠진다.
///
/// 재사용(수정 안 함):
///   EnemyHealth   - 데미지/사망(플레이어 공격이 여기로 들어옴). OnDamage/OnDeath 구독.
///   EnemyFeedback - spawn/detect/hit/death VFX·사운드. EnemyBrain 이 꺼져 있으니 SetData 를 여기서 호출.
///   VisionSensor  - 시야 감지.
///   EnemyBrain    - 프리팹에 '비활성'으로 붙어만 있음(데이터 운반용).
///                   EnemyHealth 가 brain.Data 에서 enemyId(퀘스트 킬)·enemyName(HP바)·
///                   deathAnimDuration 을 읽기 때문. 로직은 전부 이 스크립트가 담당.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class FieldMonsterAI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private FieldMonsterData data;
    public FieldMonsterData Data => data;

    [Header("References")]
    [SerializeField] private VisionSensor visionSensor;
    [Tooltip("전조 VFX가 번쩍일 위치 — 눈/턱 본. 비우면 몸통 기준(티가 안 남)")]
    [SerializeField] private Transform telegraphAnchor;
    [SerializeField] private Animator animator;
    [Tooltip("컨트롤러 복구용 폴백. 씬에 놓인 낡은 인스턴스가 컨트롤러를 잃었을 때 런타임에 다시 물린다.")]
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private AudioSource audioSource;

    [Header("Animator Params")]
    [SerializeField] private string speedParam = "Speed";
    [Tooltip("Locomotion 2D 블렌드 X축: -1 왼쪽걷기 / +1 오른쪽걷기")]
    [SerializeField] private string strafeDirParam = "StrafeDir";
    [Tooltip("Locomotion 2D 블렌드 Y축: +1 정면걷기 / -1 뒤로걷기")]
    [SerializeField] private string moveDirParam = "MoveDir";
    [Tooltip("Locomotion 재생 배속. 실제 이동속도/walkAnimRefSpeed 가 들어가 발 미끄러짐을 줄인다.")]
    [SerializeField] private string speedMulParam = "SpeedMul";
    [SerializeField] private string attackTrigger = "Attack";
    [Tooltip("피격 경직(Hit 애니)을 확률로 켜는 게이트 bool. AnyState→Hit 전이가 Hit && Stagger 를 요구.")]
    [SerializeField] private string staggerParam = "Stagger";
    [Tooltip("휴면 복귀(붕괴) 트리거. AnyState→Crumble→Dormant. 휴면형만 사용.")]
    [SerializeField] private string sleepTrigger = "Sleep";
    [Tooltip("공격 변형 선택 bool(2종 클립 랜덤: false=기본/A, true=변형/B). 컨트롤러에 있으면 매 공격 랜덤.")]
    [SerializeField] private string attackAltParam = "AttackAlt";
    [Tooltip("돌진 종료 트리거. ChargeLoop→ChargeEnd. 돌진 공격만 사용.")]
    [SerializeField] private string chargeEndTrigger = "ChargeEnd";
    [Tooltip("이중 공격 근접 선택 bool. Attack && AttackMelee → 근접 상태. 이중 공격만 사용.")]
    [SerializeField] private string attackMeleeParam = "AttackMelee";

    [Header("Rotation")]
    [Tooltip("이 속도 이상으로 움직이면 진행 방향을 봄. 그보다 느리면 타깃을 봄.")]
    [SerializeField] private float moveFaceThreshold = 0.1f;

    private NavMeshAgent nav;
    private EnemyHealth health;
    private EnemyFeedback feedback;
    private Transform playerTf;
    private PlayerStatComponent playerStat;

    private int speedHash, strafeHash, moveDirHash, speedMulHash, staggerHash, sleepHash, attackAltHash, chargeEndHash, attackMeleeHash;
    private bool hasStrafeParam, hasMoveDirParam, hasSpeedMulParam, hasStaggerParam, hasSleepParam, hasAttackAltParam, hasChargeEndParam, hasAttackMeleeParam;
    private bool attackAlt;    // 이번 공격이 변형(B/반대손)인지 — 근접 타격 VFX 좌우 미러링에 사용
    private bool attackMelee;  // 이중 공격에서 이번 공격이 근접인지(true) 원거리인지(false)

    // 스텝(옆/뒤걸음)은 NavMeshAgent.Move() 로 직접 미는데, Move() 는 agent.velocity 를 갱신하지 않는다.
    // 그대로 두면 Animator 의 Speed 가 0 -> Idle 상태로 남아 "애니 없이 미끄러지는" 그림이 된다.
    // 스텝 중에는 이 값(>=0)을 Speed 로 대신 넣는다. -1 이면 평소대로 velocity 사용.
    private float speedOverride = -1f;
    // 옆걸음 애니 배속 오버라이드(>=0). 옆걸음은 느리게 이동시켜도 애니는 이 배속으로 밟게 해서
    // 슬로모션처럼 보이는 걸 막는다. -1 이면 실제 속도 기준(발 미끄러짐 방지 로직) 사용.
    private float strafeAnimMul = -1f;
    private bool faceTarget;      // 스텝/공격 중엔 진행 방향 말고 타깃을 계속 주시
    private bool freezeFacing;    // 회전 완전 고정(공격 후 스텝: 공격 시점 시선 유지). faceTarget보다 우선.
    private bool dead;
    private bool awake;           // 휴면형: 기상(조립) 완료 상태. 휴면(바위 더미) 중엔 false → 피격 경직 억제.
    private Transform driftBone;  // 루트 모션 드리프트 제거용 스킨 루트 본(cancelRootDrift 시)
    private Vector3 driftBoneInit;
    private Vector3 homePos;
    private GameObject activeTelegraph;   // 현재 떠 있는 전조. 발사(hitDelay) 순간 즉시 제거해 싱크 맞춤.
    // sync 전조: PlayTelegraph 가 잰 '전조 원본 총길이(초)'. AttackOnce 가 이만큼 공격을 늦춰
    //   전조를 배속(안 보임) 대신 원속도로 다 보여주고 마지막 '터짐'에 타격을 맞춘다. 0=미측정/비sync.
    private float pendingSyncDuration;

    /// <summary>추적 대상. 죽었거나 기지(결계) 안이면 무시.</summary>
    private Transform Target
    {
        get
        {
            var t = visionSensor != null ? visionSensor.SpottedTarget : null;
            if (t == null) return null;
            if (playerStat != null && (playerStat.IsDead || playerStat.IsInBase)) return null;
            return t;
        }
    }

    private void Awake()
    {
        nav = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        feedback = GetComponent<EnemyFeedback>();
        if (visionSensor == null) visionSensor = GetComponentInChildren<VisionSensor>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // 씬에 놓인 낡은 인스턴스(프리팹 구조 변경/재빌드로 컨트롤러 참조가 끊긴 것)를 런타임에 자가복구.
        // 이게 있으면 "Animator is not playing an AnimatorController" 로 애니가 안 나오던 게 스스로 고쳐진다.
        if (animator != null && animator.runtimeAnimatorController == null && animatorController != null)
            animator.runtimeAnimatorController = animatorController;

        speedHash = Animator.StringToHash(speedParam);
        strafeHash = Animator.StringToHash(strafeDirParam);
        moveDirHash = Animator.StringToHash(moveDirParam);
        speedMulHash = Animator.StringToHash(speedMulParam);
        staggerHash = Animator.StringToHash(staggerParam);
        sleepHash = Animator.StringToHash(sleepTrigger);
        attackAltHash = Animator.StringToHash(attackAltParam);
        chargeEndHash = Animator.StringToHash(chargeEndTrigger);
        attackMeleeHash = Animator.StringToHash(attackMeleeParam);
        if (animator != null)
        {
            animator.applyRootMotion = false;   // 위치 권위는 NavMeshAgent(SO moveSpeed)
            // 파라미터가 없는 컨트롤러면 Set 경고가 나므로 미리 확인
            foreach (var p in animator.parameters)
            {
                if (p.nameHash == sleepHash) { hasSleepParam = true; continue; }   // Sleep 트리거
                if (p.nameHash == chargeEndHash) { hasChargeEndParam = true; continue; }   // ChargeEnd 트리거
                if (p.type == AnimatorControllerParameterType.Bool && p.nameHash == attackMeleeHash) { hasAttackMeleeParam = true; continue; }
                if (p.type == AnimatorControllerParameterType.Bool && p.nameHash == attackAltHash) { hasAttackAltParam = true; continue; }
                if (p.type == AnimatorControllerParameterType.Bool && p.nameHash == staggerHash) { hasStaggerParam = true; continue; }
                if (p.type != AnimatorControllerParameterType.Float) continue;
                if (p.nameHash == strafeHash) hasStrafeParam = true;
                else if (p.nameHash == moveDirHash) hasMoveDirParam = true;
                else if (p.nameHash == speedMulHash) hasSpeedMulParam = true;
            }
        }
        SetLocomotion(CombatStepUtil.Forward);   // 기본은 정면 걷기

        if (nav != null) nav.updateRotation = false;   // 회전은 아래에서 직접(공격 중에도 돌아야 함)
        homePos = transform.position;

        if (data == null) { Debug.LogError($"[FieldMonsterAI] data 없음: {name}", this); return; }

        // EnemyBrain 이 비활성이라 걔가 하던 초기화를 여기서 대신한다.
        feedback?.SetData(data);
        if (health != null)
        {
            health.maxHP = data.maxHP;
            health.currentHP = data.maxHP;
        }
        SyncNav();
        if (visionSensor != null)
        {
            visionSensor.ApplyVisionParameters(data.visionRange, data.visionAngle);
            visionSensor.ApplyLostMemory(data.targetLostMemory);
        }
    }

    private void SyncNav()
    {
        if (nav == null) return;
        nav.speed = data.moveSpeed;
        nav.acceleration = data.acceleration;
        nav.angularSpeed = data.angularSpeed;
        nav.stoppingDistance = 0f;
    }

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { playerTf = p.transform; playerStat = p.GetComponent<PlayerStatComponent>(); }

        // 원거리(브레스/발사체) 앵커 보정 — 머리 본을 이름으로 못 찾았으면 스킨 본 중 '가장 앞+위'
        //   (주둥이/머리)를 찾아 앵커로 삼는다. 발사·분사가 머리(입)에서 나가게.
        if (telegraphAnchor == null && data != null && (data.breathAttack || data.dualAttack || data.ranged))
            telegraphAnchor = FindHeadBone();

        // 루트 모션 드리프트 제거 대상 본 캡처 — 스킨 rootBone 에서 '아마추어 최상위 본'(Animator 직속 자식,
        //   예: DragonRoot)까지 올라간다. 걷기 이동은 보통 그 최상위 본에 실려서 밀리기 때문.
        if (data != null && data.cancelRootDrift)
        {
            var smr = GetComponentInChildren<SkinnedMeshRenderer>();
            Transform b = smr != null ? smr.rootBone : null;
            if (b != null && animator != null)
                while (b.parent != null && b.parent != animator.transform) b = b.parent;
            if (b != null) { driftBone = b; driftBoneInit = b.localPosition; }
        }

        if (health != null)
        {
            health.OnDamage += OnTookDamage;
            health.OnDeath  += OnDied;
        }

        feedback?.PlaySpawn();
        StartCoroutine(Brain());
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamage -= OnTookDamage;
            health.OnDeath  -= OnDied;
        }
    }

    // 뒤에서 맞아도 즉시 인지
    private void OnTookDamage()
    {
        if (visionSensor != null && playerTf != null) visionSensor.ForceSetTarget(playerTf);

        // 피격 경직(Hit 애니)을 확률로 게이트. EnemyHealth.TakeDamage 는 OnDamage(=여기) → PlayHit(Hit 트리거)
        //   순서라, 여기서 Stagger bool 을 먼저 정해두면 뒤이어 걸리는 Hit 트리거가 Stagger=true 일 때만 전이된다.
        //   -> 매 피격마다 모션이 끊기지 않고 확률적으로만 경직. staggerChance=1 이면 항상(기존 동작).
        //   ※휴면(바위 더미) 중엔 경직 억제 — 안 그러면 피격 Hit 애니가 골렘을 어정쩡하게 세운다.
        bool dormant = data.startDormant && !awake;
        if (hasStaggerParam && animator != null)
            animator.SetBool(staggerHash, !dormant && Random.value < data.staggerChance);
    }

    // EnemyHealth 는 EnemyBrain 만 꺼준다 -> 이 AI 는 스스로 멈춰야 시체가 안 따라온다.
    private void OnDied()
    {
        dead = true;
        DestroyTelegraph();           // 시전 중 죽으면 전조가 남지 않게
        StopAllCoroutines();          // 스텝 중이었다면 DoStep 의 정리 코드가 안 돌므로
        speedOverride = -1f;          // 여기서 직접 되돌린다(안 하면 죽은 채 걷는 모션)
        strafeAnimMul = -1f;
        freezeFacing = false; faceTarget = false;
        SetLocomotion(CombatStepUtil.Forward);
        if (nav != null && nav.enabled) { nav.isStopped = true; nav.ResetPath(); }
        enabled = false;
    }

    private void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        // 스텝 중엔 Move() 로 밀어서 velocity 가 0 이므로 실제 스텝 속도를 대신 넣는다.
        float s = speedOverride >= 0f
            ? speedOverride
            : (nav != null ? nav.velocity.magnitude : 0f);
        animator.SetFloat(speedHash, s);

        // 애니 배속.
        //   옆걸음 중(strafeAnimMul>=0): 이동 속도와 분리된 고정 배속 -> 느린 옆걸음이 슬로모션이 안 됨.
        //   그 외: 실제 속도/기준속도 비율 = 발 미끄러짐 방지(제자리 클립이라 수동 매칭).
        if (hasSpeedMulParam)
        {
            float mul;
            if (strafeAnimMul >= 0f)
                mul = strafeAnimMul;
            else
            {
                mul = s / Mathf.Max(0.01f, data.walkAnimRefSpeed);
                mul = Mathf.Clamp(mul, data.walkAnimSpeedClamp.x, data.walkAnimSpeedClamp.y);
            }
            animator.SetFloat(speedMulHash, mul);
        }
    }

    private void LateUpdate()
    {
        if (dead) return;

        // 루트 모션 드리프트 제거 — 루트 모션 노드 없는 리그에서 걷기 클립이 앞으로 밀렸다 루프에서
        //   되돌아오는(텔레포트) 현상 방지. 스킨 루트 본의 수평(XZ)을 매 프레임 제자리로(=제자리 걷기).
        if (driftBone != null)
        {
            var lp = driftBone.localPosition;
            driftBone.localPosition = new Vector3(driftBoneInit.x, lp.y, driftBoneInit.z);
        }

        // 시선 완전 고정 — 공격 후 스텝에서 '공격한 시점의 시선'을 유지.
        // 매 프레임 플레이어를 다시 쳐다보면 이동 방향(고정)과 어긋나 미끄러져 보인다.
        if (freezeFacing) return;

        Vector3 faceDir = Vector3.zero;

        // 스텝/공격 중 — 진행 방향을 무시하고 타깃을 노려본다(안 하면 옆걸음이 문워크가 됨)
        if (faceTarget)
        {
            var t = Target;
            if (t != null) { faceDir = t.position - transform.position; faceDir.y = 0f; }
        }

        // 그 외 — 이동 중이면 진행 방향, 거의 멈췄으면 타깃
        if (faceDir.sqrMagnitude < 0.0001f && nav != null)
        {
            Vector3 v = nav.velocity; v.y = 0f;
            if (v.sqrMagnitude > moveFaceThreshold * moveFaceThreshold) faceDir = v;
            else
            {
                var t = Target;
                if (t != null) { faceDir = t.position - transform.position; faceDir.y = 0f; }
            }
        }

        if (faceDir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(faceDir), data.angularSpeed * Time.deltaTime);
    }

    // ── 메인 루프 ──────────────────────────────────────────────────
    private IEnumerator Brain()
    {
        awake = !data.startDormant;   // 휴면형이면 처음엔 잠들어 있음 → 첫 발견에만 기상(Detect) 연출
        while (!dead)
        {
            // 1) 타깃 없음 — 대기/배회.
            if (Target == null)
            {
                if (data.startDormant && !awake)
                {
                    yield return Idle();                          // 휴면: 바위 더미로 제자리 대기(배회 X)
                }
                else if (data.startDormant && awake && data.sleepAfterIdle > 0f)
                {
                    // 각성 상태로 타깃 없음 — 배회하며 재조우를 기다리다, 오래되면 붕괴 → 휴면 복귀.
                    float sleepAt = Time.time + data.sleepAfterIdle;
                    while (Target == null && !dead && Time.time < sleepAt)
                        yield return data.wander ? Wander() : Idle();
                    if (!dead && Target == null) { yield return Crumble(); awake = false; }
                }
                else
                {
                    yield return data.wander ? Wander() : Idle();  // 일반 몹
                }
                continue;
            }

            // 2) 발견 연출 — 일반 몹은 매번 포효, 휴면형은 '첫 기상'에만(재조우 시 생략).
            if (!data.startDormant || !awake)
            {
                feedback?.PlayDetect();
                yield return DetectPause();
            }
            awake = true;
            yield return DoStep(data.openingStep, data.openingStepDuration);

            // 3) 전투 루프 — 접근 → 전조+공격 → 스텝 → 반복
            while (Target != null && !dead)
            {
                yield return Chase();
                if (Target == null || dead) break;

                yield return AttackOnce();
                if (dead) break;

                // 공격 후 잠깐 경직(회복) — 바로 안 빠지고 멈춰서 플레이어에게 반격할 틈을 준다.
                yield return Recover(data.postAttackPause);
                if (dead) break;

                // 공격 후: 랜덤 시간 동안 플레이어에게서 멀어짐(Retreat=보면서 뒤/대각선 뒤).
                // 매번 시간이 달라 공격 간격이 들쭉날쭉 = 예측 어려운 리듬.
                var r = data.afterAttackStepDurationRange;
                float stepDur = Random.Range(Mathf.Min(r.x, r.y), Mathf.Max(r.x, r.y));
                yield return DoStep(data.afterAttackStep, stepDur, freezeFace: true);
                if (data.attackCooldown > 0f) yield return new WaitForSeconds(data.attackCooldown);
            }
        }
    }

    /// <summary>휴면 복귀 — 붕괴(Sleep 트리거) 애니 재생 후 다시 바위 더미(Dormant)로. 그 뒤엔 첫 발견처럼 재기상.</summary>
    private IEnumerator Crumble()
    {
        Stop();
        if (nav != null && nav.enabled) { nav.isStopped = true; nav.ResetPath(); }
        freezeFacing = true;
        if (animator != null && hasSleepParam) animator.SetTrigger(sleepTrigger);
        float end = Time.time + Mathf.Max(0.1f, data.sleepAnimDuration);
        while (Time.time < end && !dead) yield return null;   // 붕괴 애니 재생 동안 대기(중간에 튀지 않게)
        freezeFacing = false;
    }

    private IEnumerator Idle()
    {
        Stop();
        yield return null;
    }

    /// <summary>스폰 지점 주변을 어슬렁. 가만히 서 있으면 죽어있는 것처럼 보임.</summary>
    private IEnumerator Wander()
    {
        Vector3 target = homePos + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized
                                   * Random.Range(1f, data.wanderRadius);

        if (NavMesh.SamplePosition(target, out var hit, 2f, NavMesh.AllAreas) && nav != null && nav.enabled)
        {
            nav.speed = data.moveSpeed * data.wanderSpeedMul;
            nav.isStopped = false;
            nav.SetDestination(hit.position);

            float giveUp = Time.time + 8f;   // 길 막히면 포기
            while (!dead && Target == null && Time.time < giveUp)
            {
                if (!nav.pathPending && nav.remainingDistance <= 0.4f) break;
                yield return null;
            }
        }

        Stop();
        SyncNav();
        float pause = Random.Range(data.wanderPauseRange.x, data.wanderPauseRange.y);
        float end = Time.time + pause;
        while (!dead && Target == null && Time.time < end) yield return null;
    }

    /// <summary>발견 모션용 정지. 0이면 즉시 진행.</summary>
    private IEnumerator DetectPause()
    {
        if (data.detectStunDuration <= 0f) yield break;
        float dur = data.detectStunDuration;
        float end = Time.time + dur;

        // 휴면형(기상): 발견 순간엔 부동, 조립되는 '전체 시간에 걸쳐' 서서히 플레이어 쪽으로 돈다.
        //   목표 각도는 발견 순간에 '고정'(그 뒤 플레이어가 움직여도 끝에서 홱 튀지 않게) → 조립 후 전투에서
        //   낮춘 angularSpeed 로 현재 플레이어를 부드럽게 마저 추적.
        if (data.startDormant)
        {
            freezeFacing = true;                       // LateUpdate 즉시회전 차단(직접 Slerp)
            Quaternion startRot = transform.rotation;
            Quaternion targetRot = startRot;
            var t0 = Target;
            if (t0 != null)
            {
                Vector3 d = t0.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.0001f) targetRot = Quaternion.LookRotation(d);
            }
            while (Time.time < end && !dead)
            {
                Stop();
                float k = Mathf.Clamp01(1f - (end - Time.time) / dur);   // 0→1 조립 진행도
                transform.rotation = Quaternion.Slerp(startRot, targetRot, k);
                yield return null;
            }
            freezeFacing = false;
            yield break;
        }

        // 일반 몹: 발견 동안 타깃 주시(기존).
        faceTarget = true;
        while (Time.time < end && !dead) { Stop(); yield return null; }
        faceTarget = false;
    }

    /// <summary>공격 사거리 안까지 접근.</summary>
    private IEnumerator Chase()
    {
        SyncNav();
        float reach = data.attackRange * data.attackApproachRatio;
        Vector3 lastDest = transform.position + Vector3.forward * 999f;   // 첫 프레임에 반드시 갱신되게

        while (!dead)
        {
            var t = Target;
            if (t == null) yield break;

            float d = Vector3.Distance(transform.position, t.position);
            if (d <= reach) { Stop(); yield break; }

            if (nav != null && nav.enabled)
            {
                nav.isStopped = false;
                // ★매 프레임 재경로 X — 타깃이 0.5m 이상 움직였을 때만 SetDestination.
                //   (매 프레임 재계산 시 경로 첫 코너가 뒤로 잡혀 순간 앞뒤로 튀는 현상 방지)
                if ((t.position - lastDest).sqrMagnitude > 0.25f)
                {
                    nav.SetDestination(t.position);
                    lastDest = t.position;
                }
            }
            yield return null;
        }
    }

    /// <summary>전조 번쩍 → hitDelay 뒤 타격 → 모션 끝까지 잠금.</summary>
    private IEnumerator AttackOnce()
    {
        Stop();

        // 공격 시작 순간 플레이어를 향해 1회 '조준'한 뒤, 공격 내내 시선을 '고정'한다.
        //   공격 중에도 계속 플레이어를 따라보면 회피가 무의미하고 부자연스럽다.
        //   여기서 방향을 커밋 -> 플레이어는 옆/뒤로 피할 수 있다(엔드필드식).
        var tgt = Target;
        if (tgt != null)
        {
            Vector3 d = tgt.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }
        faceTarget = false;
        freezeFacing = true;                   // ★공격 동안 시선 고정(안 따라감)

        // 돌진 공격 — 제자리 타격 대신 커밋 방향으로 달려들어 접촉 피해. 전조/스윙 로직 미사용.
        if (data.chargeAttack)
        {
            yield return ChargeRoutine();
            freezeFacing = false;
            yield break;
        }

        PlayTelegraph();                       // ★ 전조 먼저 재생(원속도). pendingSyncDuration = 전조 총길이.

        // 싱크 = 전조를 스윙보다 '먼저' 띄워 리드타임을 준다(배속·애니지연 X, 전부 원속도).
        //   전조 마지막 '터짐'(≈총길이×0.92)이 스윙 접점(hitDelay)에 딱 오도록,
        //   lead = 터짐시각 − hitDelay 만큼 전조를 앞세운 뒤 스윙을 시작한다.
        //   그동안 몬스터는 제자리에서 기(전조)를 모으고, 다 모여 터지는 순간 내려친다.
        if (data.telegraphSyncToHit && pendingSyncDuration > 0.05f)
        {
            float burstAt = pendingSyncDuration * 0.92f;                  // 터짐이 도는 대략 시점
            float lead = Mathf.Clamp(burstAt - data.hitDelay, 0f, 2.5f);  // 스윙 전 기 모으는 시간
            float w = 0f;
            while (w < lead && !dead) { w += Time.deltaTime; yield return null; }
        }
        if (dead) { freezeFacing = false; yield break; }

        // 이중 공격(근접+원거리) — 지금 거리로 결정. 근접이면 AttackMelee bool 세팅(애니 라우팅).
        //   fireRanged: 실제 타격 방식(원거리 발사체 vs 근접 접촉). 클립별 타이밍도 근접/원거리 분리.
        attackMelee = data.dualAttack && DistanceToPlayer() <= data.meleeRange;
        if (data.dualAttack && hasAttackMeleeParam) animator.SetBool(attackMeleeHash, attackMelee);
        bool fireRanged = data.dualAttack ? !attackMelee : data.ranged;
        bool breathing = fireRanged && data.breathAttack;
        // 타격/분사 시각 — 브레스는 애니 입 벌리는 순간(breathStartDelay)에 맞춘다(hitDelay 50% 아님).
        float fireAt  = attackMelee ? data.meleeHitDelay
                      : breathing  ? data.breathStartDelay
                      : data.hitDelay;
        // 잠금 길이 — 근접/원거리(발사체)/브레스 각각. 브레스는 분사 끝까지 제자리 고정.
        float lockLen = attackMelee ? data.meleeAnimLength
                      : breathing  ? data.breathStartDelay + data.breathDuration + 0.2f
                      : data.animLength;

        // 공격 변형 랜덤(2종 클립: A/B = 왼손/오른손). 이중 공격이면 라우팅에 AttackMelee 를 쓰므로 미사용.
        attackAlt = !data.dualAttack && hasAttackAltParam && Random.value < 0.5f;
        if (hasAttackAltParam && !data.dualAttack) animator.SetBool(attackAltHash, attackAlt);

        feedback?.PlayAttack();                // 이제 스윙 시작 → hitDelay 뒤 접점 = 전조 터짐과 일치
        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);

        float t = 0f;
        bool damaged = false;
        while (t < lockLen && !dead)
        {
            t += Time.deltaTime;
            if (!damaged && t >= fireAt)
            {
                damaged = true;
                // sync 전조는 마지막 '터지는' 연출까지 보이게 즉시 삭제하지 않는다(폴백 수명으로 정리).
                // 그 외(원거리 차징 등)는 발사 순간 끝내서 발사와 싱크.
                if (!data.telegraphSyncToHit) DestroyTelegraph();
                if (fireRanged)
                {
                    if (data.breathAttack)       StartCoroutine(BreathRoutine());      // 브레스(제자리 원뿔)
                    else if (data.skyfallAttack) StartCoroutine(SkyfallRoutine());     // 하늘 낙하
                    else                         StartCoroutine(FireProjectileBurst()); // 발사체
                }
                else { ApplyDamage(); SpawnMeleeImpact(); }               // 근접 = 즉시 타격(+VFX)
            }
            yield return null;
        }

        freezeFacing = false;
    }

    /// <summary>플레이어와의 수평 거리(m). 타깃 없으면 매우 큰 값.</summary>
    private float DistanceToPlayer()
    {
        var t = Target;
        if (t == null) return 9999f;
        Vector3 d = t.position - transform.position; d.y = 0f;
        return d.magnitude;
    }

    /// <summary>돌진 공격 — 준비(웅크림, 플레이어 조준) → 돌진 시작에 방향 커밋 → 전진(접촉 피해 1회) → 마무리.
    /// 준비 동안 플레이어를 조준하고 '돌진 시작 순간'의 방향으로 직진 → 돌진 중 옆으로 피하면 회피 가능.</summary>
    private IEnumerator ChargeRoutine()
    {
        feedback?.PlayAttack();
        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);      // ChargeStart(준비)

        // 준비(웅크림) — 이 동안 플레이어를 향해 조준한다(freezeFacing 이라 직접 회전).
        float w = 0f;
        while (w < data.chargeWindup && !dead)
        {
            var pt = Target;
            if (pt != null)
            {
                Vector3 d = pt.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, Quaternion.LookRotation(d), data.angularSpeed * Time.deltaTime);
            }
            w += Time.deltaTime;
            yield return null;
        }
        if (dead) yield break;

        // 돌진 방향 커밋 — 준비가 끝난 '지금'의 플레이어 방향으로. 이후 직진(옆으로 피하면 회피).
        Vector3 dir = transform.forward; dir.y = 0f;
        var t0 = Target;
        if (t0 != null)
        {
            Vector3 d = t0.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) { dir = d.normalized; transform.rotation = Quaternion.LookRotation(dir); }
        }
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        // 돌진 — 앞으로 전진하며 접촉 피해(1회). 데미지는 캐시된 playerStat 으로 확실히(Target GetComponent 의존 X).
        if (nav != null && nav.enabled) { nav.isStopped = true; nav.ResetPath(); }
        float t = 0f;
        while (t < data.chargeDuration && !dead)
        {
            t += Time.deltaTime;
            Vector3 step = dir * data.chargeSpeed * Time.deltaTime;
            if (nav != null && nav.enabled) nav.Move(step);   // navmesh 위에서 전진
            else                            transform.position += step;

            if (playerStat != null && playerTf != null && !playerStat.IsDead && !playerStat.IsInBase)
            {
                Vector3 fd = playerTf.position - transform.position; fd.y = 0f;
                if (fd.magnitude <= data.chargeHitRadius)
                {
                    playerStat.TakeDamage(data.attackDamage, transform.position);
                    SpawnMeleeImpact();      // 접촉 지점에 충돌 VFX
                    break;                   // ★히트하면 그 자리에서 돌진 종료(관통 X)
                }
            }
            yield return null;
        }

        // 마무리(ChargeEnd) — 감속/정지 모션.
        if (nav != null && nav.enabled) { nav.isStopped = true; nav.ResetPath(); }
        if (animator != null && hasChargeEndParam) animator.SetTrigger(chargeEndTrigger);
        float e = 0f;
        while (e < data.chargeEndDuration && !dead) { e += Time.deltaTime; yield return null; }
    }

    /// <summary>공격 직후 잠깐 멈춤(경직). 그 자리에 서서 반격을 허용한다. 시선은 공격 시점 그대로 고정.</summary>
    private IEnumerator Recover(float duration)
    {
        if (duration <= 0f) yield break;
        Stop();                 // 정지 -> Speed 0 -> Idle. 안 움직이니 때리기 쉬움
        freezeFacing = true;    // 공격 때 커밋한 시선을 회복 중에도 유지(플레이어 안 따라감)
        float end = Time.time + duration;
        while (Time.time < end && !dead) yield return null;
        freezeFacing = false;
    }

    /// <summary>공격 사이 옆/뒤로 빠지는 스텝. 타깃을 본 채 이동.</summary>
    private IEnumerator DoStep(CombatStepKind kind, float duration, bool freezeFace = false)
    {
        if (kind == CombatStepKind.None || duration <= 0f) yield break;
        if (nav == null || !nav.enabled) yield break;

        var t0 = Target;
        if (t0 == null) yield break;

        // Retreat = 플레이어를 보며 멀어짐(뒤/대각선 뒤). 별도 처리(매 프레임 방향·블렌드 갱신).
        if (kind == CombatStepKind.Retreat)
        {
            yield return Retreat(duration);
            yield break;
        }

        var k = CombatStepUtil.Resolve(kind);
        // 옆걸음은 strafeSpeedMul, 그 외(뒤 등)는 stepSpeedMul
        bool isStrafe = k == CombatStepKind.StrafeLeft || k == CombatStepKind.StrafeRight;
        float speed = data.moveSpeed * (isStrafe ? data.strafeSpeedMul : data.stepSpeedMul);
        float end = Time.time + duration;

        // ★방향은 스텝 시작 시 1회만 정하고 고정한다.
        //   매 프레임 다시 구하면 "타깃 기준 오른쪽"이 이동할수록 계속 돌아가서
        //   직선이 아니라 타깃 주위를 도는 궤도(둥글게 걷기)가 된다.
        Vector3 dir = CombatStepUtil.Direction(k, transform, t0);
        if (dir.sqrMagnitude < 0.0001f) yield break;

        // freezeFace=true : 시선을 지금(=공격 시점) 각도로 고정. 이동 방향도 이 각도 기준이라 어긋남 없음.
        // false           : 이동 중에도 타깃을 계속 노려봄(오프닝 스텝 등).
        if (freezeFace) freezeFacing = true;
        else            faceTarget = true;

        SetLocomotion(CombatStepUtil.BlendParam(k));   // 좌/우/뒤에 맞는 걷기 모션
        speedOverride = speed;                        // Move() 는 velocity 를 안 올려주므로 직접
        strafeAnimMul = isStrafe ? data.strafeAnimSpeedMul : -1f;   // 옆걸음 애니는 이동속도와 분리
        nav.isStopped = true;
        nav.ResetPath();

        while (Time.time < end && !dead)
        {
            if (Target == null) break;
            nav.Move(dir * speed * Time.deltaTime);   // 고정 방향 = 직선
            yield return null;
        }

        speedOverride = -1f;                     // 다시 velocity 기준으로
        strafeAnimMul = -1f;                     // 옆걸음 애니 배속 해제
        SetLocomotion(CombatStepUtil.Forward);   // 스텝 끝 -> 정면 걷기로 복귀
        freezeFacing = false;
        faceTarget = false;
        if (nav != null && nav.enabled) nav.isStopped = false;
    }

    /// <summary>
    /// 플레이어를 '보면서' 멀어짐(정후방~대각선 뒤). 치고 빠지기의 후퇴.
    ///  - 이동: 매 프레임 '현재 플레이어 반대쪽' + 고정 대각선 각도 -> 항상 멀어지려 함(플레이어 따라 조정).
    ///  - 시선: 플레이어를 계속 주시(faceTarget) -> 뒷걸음이 자연스럽다(옆으로 안 보고 걷던 어색함 제거).
    ///  - 블렌드: 이동방향을 '바라보는 방향' 기준으로 분해해 매 프레임 갱신 -> 애니가 실제 이동과 매칭(미끄러짐 없음).
    /// </summary>
    private IEnumerator Retreat(float duration)
    {
        var t0 = Target;
        if (t0 == null) yield break;

        // 대각선 각도는 스텝 시작 시 1회 고정(좌/우 어느 쪽으로 비스듬히 뺄지).
        float diag = Random.Range(-data.retreatDiagonalMaxAngle, data.retreatDiagonalMaxAngle);
        float speed = data.moveSpeed * data.stepSpeedMul;

        faceTarget = true;       // ★플레이어를 보며 물러남
        speedOverride = speed;
        strafeAnimMul = -1f;     // 뒷걸음은 이동 속도에 맞춰 재생(발 미끄러짐 방지)
        nav.isStopped = true;
        nav.ResetPath();

        float end = Time.time + duration;
        while (Time.time < end && !dead)
        {
            var t = Target;
            if (t == null) break;

            Vector3 away = transform.position - t.position; away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            away.Normalize();
            Vector3 dir = Quaternion.AngleAxis(diag, Vector3.up) * away;   // 멀어짐 + 대각선

            // 이동방향을 '바라보는 방향(플레이어쪽)' 기준 로컬로 분해 -> 뒤/대각선뒤 블렌드
            Vector3 local = transform.InverseTransformDirection(dir);
            SetLocomotion(new Vector2(local.x, local.z));

            nav.Move(dir * speed * Time.deltaTime);
            yield return null;
        }

        speedOverride = -1f;
        SetLocomotion(CombatStepUtil.Forward);
        faceTarget = false;
        if (nav != null && nav.enabled) nav.isStopped = false;
    }

    // ── helpers ────────────────────────────────────────────────────
    /// <summary>전조/발사 기준 위치. 앵커(눈/턱/머리 본) + 몸 기준 오프셋(앞/위)으로 얼굴 앞을 잡는다.</summary>
    private Vector3 MuzzlePos()
    {
        Transform a = telegraphAnchor != null ? telegraphAnchor : transform;
        Vector3 basePos = telegraphAnchor != null ? a.position : transform.position + Vector3.up * data.projectileSpawnHeight;
        Vector3 o = data.telegraphLocalOffset;
        // 오프셋은 '몸(루트) 기준' — 본의 로컬축이 제멋대로라도 항상 앞/위가 일관됨.
        return basePos + transform.right * o.x + transform.up * o.y + transform.forward * o.z;
    }

    private void PlayTelegraph()
    {
        pendingSyncDuration = 0f;   // 전조 없음/비sync 기본값(이전 공격의 리드타임이 남지 않게 항상 초기화)
        if (data.telegraphVFX != null)
        {
            Transform a = telegraphAnchor != null ? telegraphAnchor : transform;
            // 몬스터에 부착 -> 움직여도 눈/턱에 붙어 따라감
            var vfx = Instantiate(data.telegraphVFX, MuzzlePos(), a.rotation, a);
            if (data.telegraphScale > 0f && !Mathf.Approximately(data.telegraphScale, 1f))
                vfx.transform.localScale *= data.telegraphScale;

            // 지정된 자식 오브젝트 제거(예: 중복 원 'Circle_blast'). 원본은 그대로, 이 인스턴스만.
            if (data.telegraphStripObjects != null && data.telegraphStripObjects.Length > 0)
            {
                foreach (var tr in vfx.GetComponentsInChildren<Transform>(true))
                {
                    if (tr == null || tr == vfx.transform) continue;
                    foreach (var nm in data.telegraphStripObjects)
                    {
                        if (!string.IsNullOrEmpty(nm) && tr.name == nm)
                        {
                            tr.gameObject.SetActive(false);   // 이번 프레임 즉시 정지
                            Destroy(tr.gameObject);
                            break;
                        }
                    }
                }
            }

            // VFX 팩에 딸려온 데모 스크립트(예: RotateGunOnMouse — 카메라 없으면 매 프레임 "No Camera" 로그
            // 폭탄 → 에디터 렉)를 제거한다. 전조는 순수 시각효과라 게임플레이 스크립트가 필요 없다.
            foreach (var mb in vfx.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) Destroy(mb);

            // 파티클을 Local 시뮬레이션으로 → 머리(앵커)가 움직여도 방출된 입자가 따라온다.
            // + 루프 OFF → 전조가 한 번만 재생(끝에 원이 한 번 더 나오는/2번 재생되는 것 제거).
            var systems = vfx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.loop = false;
            }

            // 타격 싱크 — VFX는 '원속도 그대로'(배속 X, 안 보이던 원인 제거) 재생하고,
            //   전조 원본 총길이를 재서 AttackOnce 가 그만큼 공격을 늦추게 한다(공격이 VFX에 맞춰짐).
            //   ★startDelay 포함해서 재야 지연 후 터지는 서브(Circle_blast_2 등)까지 총길이에 잡힌다.
            //   -> 모이는 건 준비동작 내내 원속도로, 터지는 건 끝에서, 타격은 그 '터짐'에 맞춰 들어간다.
            if (data.telegraphSyncToHit && systems.Length > 0)
            {
                float syncTotal = 0f;
                foreach (var ps in systems)
                {
                    var m = ps.main;
                    syncTotal = Mathf.Max(syncTotal,
                        m.startDelay.constantMax + m.duration + m.startLifetime.constantMax);
                }
                pendingSyncDuration = syncTotal;   // AttackOnce 가 이 값으로 공격 타이밍을 늦춘다
            }

            GameObject toDestroy = vfx;

            // 차징 연출 — 발사(hitDelay) 순간에 딱 최대 크기가 되도록 그때까지 커진다.
            // ★래퍼(피벗)를 '머리 중앙(총구)'에 고정하고, 이펙트의 시각적 중심을 그 지점에 맞춘 뒤
            //   피벗을 스케일한다. -> 처음부터 머리 중앙에서 나와 제자리에서 사방으로 커진다(치우침·이동 없음).
            if (data.telegraphGrow)
            {
                Vector3 head = MuzzlePos();
                var pivot = new GameObject("TelegraphGrowPivot").transform;
                pivot.SetParent(a, worldPositionStays: true);
                pivot.position = head;
                pivot.rotation = a.rotation;
                vfx.transform.SetParent(pivot, worldPositionStays: true);

                // 이펙트 콘텐츠 중심이 피벗(머리 중앙)에 오도록 한 번 보정 -> 옆에서 시작하는 현상 제거.
                Vector3 c = VfxCenter(vfx, head);
                vfx.transform.position += head - c;

                var grow = pivot.gameObject.AddComponent<FieldMonsterVfxGrow>();
                grow.Play(Vector3.one * Mathf.Clamp01(data.telegraphGrowFrom), Vector3.one,
                          Mathf.Max(0.05f, data.hitDelay));
                toDestroy = pivot.gameObject;   // 피벗을 지우면 자식 vfx도 함께 정리
            }

            activeTelegraph = toDestroy;                          // 발사 순간 제거하려고 들고 있음
            // 삭제 시각 — sync 전조는 원속도로 pendingSyncDuration 만큼 재생되므로 그 직후 정리(버스트 페이드까지).
            //   그 외는 기존 수명 기준(발사 순간 즉시 제거되므로 폴백일 뿐).
            float destroyAfter = data.telegraphSyncToHit
                ? Mathf.Max(data.hitDelay, pendingSyncDuration) + 0.6f
                : data.telegraphLifeTime + 0.5f;
            if (destroyAfter > 0f) Destroy(toDestroy, destroyAfter);
        }
        if (audioSource != null && data.telegraphSound != null)
            audioSource.PlayOneShot(data.telegraphSound);
    }

    /// <summary>전조 즉시 제거. 발사 순간 호출해 "전조 끝 = 발사"를 맞춘다.</summary>
    private void DestroyTelegraph()
    {
        if (activeTelegraph != null) Destroy(activeTelegraph);
        activeTelegraph = null;
    }

    /// <summary>이펙트의 시각적 중심(렌더러 합 바운즈 중심). 렌더러가 없으면 fallback(총구).</summary>
    private static Vector3 VfxCenter(GameObject go, Vector3 fallback)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return fallback;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b.size.sqrMagnitude < 1e-6f ? fallback : b.center;
    }

    private void ApplyDamage()
    {
        var t = Target;
        if (t == null) return;

        // 전조 보고 빠졌으면 안 맞는다 — 타격 순간 다시 거리 확인
        if (Vector3.Distance(transform.position, t.position) > data.attackRange) return;

        var ps = t.GetComponent<PlayerStatComponent>();
        if (ps != null) ps.TakeDamage(data.attackDamage);
    }

    /// <summary>근접 타격 순간의 슬램/클랩 VFX. 명중 여부와 무관하게 재생(헛쳐도 이펙트는 난다).
    /// 몸 기준 오프셋 위치의 '월드'에 스폰 → 몸을 따라가지 않고 그 자리에 남았다 소멸.</summary>
    private void SpawnMeleeImpact()
    {
        if (data.meleeImpactVFX == null) return;

        // 2종 공격(왼손/오른손)일 땐 변형(B)에서 좌우 오프셋을 미러링 → 때린 손 쪽에서 이펙트가 난다.
        Vector3 o = data.meleeImpactOffset;
        float sideX = (hasAttackAltParam && attackAlt) ? -o.x : o.x;
        Vector3 pos = transform.position + transform.right * sideX + Vector3.up * o.y + transform.forward * o.z;
        var fx = Instantiate(data.meleeImpactVFX, pos, transform.rotation);

        if (data.meleeImpactScale > 0f && !Mathf.Approximately(data.meleeImpactScale, 1f))
            fx.transform.localScale *= data.meleeImpactScale;

        // VFX 팩에 딸린 데모 스크립트 제거(카메라 없으면 로그 폭탄 등). 순수 시각효과만.
        foreach (var mb in fx.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null) Destroy(mb);

        Destroy(fx, Mathf.Max(0.2f, data.meleeImpactLifeTime));
    }

    /// <summary>머리(주둥이) 본 추정 — 이름으로 못 찾을 때. 스킨 본 중 '전방+위'로 가장 뻗은 본을 고른다.
    /// (아이들 포즈 기준: 머리가 가장 앞·위, 꼬리는 뒤, 다리는 아래). 브레스를 여기 부착해 머리를 따라가게.</summary>
    private Transform FindHeadBone()
    {
        var skinned = GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinned == null || skinned.bones == null || skinned.bones.Length == 0) return null;

        Transform best = null;
        float bestScore = float.NegativeInfinity;
        Vector3 fwd = transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward; else fwd.Normalize();

        foreach (var b in skinned.bones)
        {
            if (b == null) continue;
            Vector3 rel = b.position - transform.position;
            // 앞으로 뻗은 정도 + 높이. 앞이 우세하게(주둥이) 가중.
            float score = Vector3.Dot(rel, fwd) * 1.0f + Mathf.Max(0f, rel.y) * 0.6f;
            if (score > bestScore) { bestScore = score; best = b; }
        }
        return best;
    }

    /// <summary>브레스 — 입(총구)에 스트림 VFX 를 부착해 제자리에서 전방으로 분사하고,
    /// 분사 동안 '전방 원뿔(각+사거리)' 안의 플레이어에게 틱 피해. 발사체가 아니라 뿜는 느낌.</summary>
    private IEnumerator BreathRoutine()
    {
        Vector3 fwd = transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward; else fwd.Normalize();

        // 방향(고정): breathStreamAxis 0 이면 몸 정면, 아니면 그 로컬 축을 전방에 정렬.
        //   ★부모로 붙이지 '않는다'(월드 스폰) — 머리 본에 부모로 붙이면 그 본의 회전·스케일이 VFX 를 왜곡.
        //   대신 위치만 매 프레임 머리 본(MuzzlePos)으로 갱신 → 방향은 그대로, 위치만 머리를 따라간다.
        Quaternion rot = data.breathStreamAxis.sqrMagnitude > 0.0001f
            ? Quaternion.FromToRotation(data.breathStreamAxis.normalized, fwd)
            : transform.rotation;

        GameObject fx = null;
        if (data.breathVFX != null)
            fx = Instantiate(data.breathVFX, MuzzlePos(), rot);   // 부모 없음 → 스케일/회전 왜곡 방지

        float end = Time.time + data.breathDuration;
        float nextTick = Time.time;              // 시작하자마자 1틱 판정
        while (Time.time < end && !dead)
        {
            if (fx != null)
            {
                fx.transform.position = MuzzlePos();   // 머리(주둥이) 위치 추적
                fx.transform.rotation = rot;           // 방향은 고정(왜곡 없음)
            }
            if (Time.time >= nextTick && playerStat != null && playerTf != null
                && !playerStat.IsDead && !playerStat.IsInBase)
            {
                Vector3 toP = playerTf.position - transform.position; toP.y = 0f;
                if (toP.magnitude <= data.breathRange && Vector3.Angle(fwd, toP) <= data.breathAngle * 0.5f)
                {
                    playerStat.TakeDamage(data.attackDamage, transform.position);
                    nextTick = Time.time + Mathf.Max(0.05f, data.breathTickInterval);
                }
            }
            yield return null;
        }
        if (fx != null) Destroy(fx);
    }

    /// <summary>스폰된 VFX 인스턴스에서 지정 이름의 오브젝트를 제거(원본은 그대로). 거슬리는 서브(레이저/균열 등) 제거용.
    /// 이름이 '루트'와 매칭되면(예: 레이저가 루트 자체 파티클) 전체를 지우지 않고 루트의 파티클·렌더러만 끈다.</summary>
    private void StripNamedChildren(GameObject go, string[] names)
    {
        if (go == null || names == null || names.Length == 0) return;
        foreach (var tr in go.GetComponentsInChildren<Transform>(true))
        {
            if (tr == null) continue;
            string tn = tr.name.Replace("(Clone)", "");   // 인스턴스 루트는 "이름(Clone)"
            bool match = false;
            foreach (var nm in names)
                if (!string.IsNullOrEmpty(nm) && tn == nm) { match = true; break; }
            if (!match) continue;

            if (tr == go.transform)
            {
                // 루트 매칭 — 통째로 지우면 이펙트 전체가 사라지므로 '루트 자신의' 파티클/렌더러만 끈다(자식은 유지).
                var ps = tr.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var em = ps.emission; em.enabled = false;
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(false);
                }
                foreach (var rend in tr.GetComponents<Renderer>()) rend.enabled = false;
            }
            else
            {
                tr.gameObject.SetActive(false);   // 이번 프레임 즉시 정지
                Destroy(tr.gameObject);
            }
        }
    }

    /// <summary>지정 이름의 파티클을 '수평 빌보드'로 바꿔 바닥에 눕힌다(카메라 향하던 눈꽃 문양 등을 지면에).</summary>
    private void FlattenParticles(GameObject go, string[] names)
    {
        if (go == null || names == null || names.Length == 0) return;
        foreach (var psr in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (psr == null) continue;
            string tn = psr.name.Replace("(Clone)", "");
            foreach (var nm in names)
            {
                if (!string.IsNullOrEmpty(nm) && tn == nm)
                {
                    psr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;   // 바닥에 눕힘
                    break;
                }
            }
        }
    }

    /// <summary>하늘 낙하 — 플레이어 주변 여러 지점에 얼음이 위에서 떨어져 착지 반경 피해.
    /// projectileCount 만큼 projectileInterval 간격으로 낙하 지점을 흩뿌린다(회피 가능).</summary>
    private IEnumerator SkyfallRoutine()
    {
        int n = Mathf.Max(1, data.projectileCount);
        for (int i = 0; i < n; i++)
        {
            if (dead) yield break;
            SpawnOneSkyfall();
            if (i < n - 1 && data.projectileInterval > 0f)
                yield return new WaitForSeconds(data.projectileInterval);
        }
    }

    private void SpawnOneSkyfall()
    {
        Vector3 center = playerTf != null ? playerTf.position : (Target != null ? Target.position : transform.position);
        Vector2 r = Random.insideUnitCircle * Mathf.Max(0f, data.skyfallSpread);
        Vector3 ground = new Vector3(center.x + r.x, center.y, center.z + r.y);   // 플레이어 주변 무작위(x,z)

        // ★항상 '땅'에 — 그 x,z 위쪽에서 아래로 레이캐스트해 실제 지면을 찾는다.
        //   플레이어가 공중이어도 아래 땅에, 경사면이면 그 표면에 정확히 붙는다(뚫려 보이는 것 방지).
        Vector3 rayStart = new Vector3(ground.x, center.y + 40f, ground.z);
        int mask = data.projectileBlockMask;   // 지면/지형 포함(발사체 차단 마스크 재사용)
        Vector3 normal = Vector3.up;
        if (mask != 0 && Physics.Raycast(rayStart, Vector3.down, out var hit, 300f, mask, QueryTriggerInteraction.Ignore))
        {
            normal = hit.normal;                                          // 경사면 정렬용
            ground = hit.point + normal * Mathf.Max(0.01f, data.skyfallGroundClearance);   // 법선 방향으로 띄움(곡면 묻힘 완화)
        }

        StartCoroutine(SkyfallOne(ground, normal));
    }

    // 지면 법선에 맞춰 VFX 를 기울인다(경사에 밀착). maxTilt 로 절벽 과회전 방지. 0 이면 수평 유지.
    private Quaternion GroundAlign(Vector3 normal)
    {
        if (data.skyfallMaxTilt <= 0f) return Quaternion.identity;
        Vector3 n = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        float ang = Vector3.Angle(Vector3.up, n);
        if (ang > data.skyfallMaxTilt)
            n = Vector3.RotateTowards(Vector3.up, n, data.skyfallMaxTilt * Mathf.Deg2Rad, 0f);
        return Quaternion.FromToRotation(Vector3.up, n);
    }

    private IEnumerator SkyfallOne(Vector3 ground, Vector3 normal)
    {
        Quaternion align = GroundAlign(normal);   // 경사면 밀착 회전(수평이면 identity)

        // 낙하 VFX 는 '자체적으로 하늘에서 떨어지는' 연출을 착지점에 스폰(이동 X → 자체 연출 유지).
        GameObject fx = null;
        if (data.skyfallVFX != null)
        {
            fx = Instantiate(data.skyfallVFX, ground, align * Quaternion.Euler(data.skyfallVfxEuler));   // 경사 정렬 + 눕히기 보정
            if (data.projectileScale > 0f && !Mathf.Approximately(data.projectileScale, 1f))
                fx.transform.localScale *= data.projectileScale;
            StripNamedChildren(fx, data.impactStripObjects);      // 레이저/균열 등 거슬리는 서브 제거
            FlattenParticles(fx, data.flattenParticles);          // 눈꽃 등 빌보드 파티클을 바닥에 눕힘
        }

        // 착지 시점까지 대기(=플레이어 회피 시간).
        float end = Time.time + Mathf.Max(0.1f, data.skyfallFallTime);
        while (Time.time < end && !dead) yield return null;
        if (fx != null) Destroy(fx, 2f);   // 잔여 페이드 후 정리

        // 착지 — 추가 임팩트 VFX + 반경 피해(1회). 지정 자식(흙먼지 등)은 제거.
        if (data.impactVFX != null)
        {
            var imp = Instantiate(data.impactVFX, ground, align);   // 경사면 정렬
            if (data.projectileScale > 0f && !Mathf.Approximately(data.projectileScale, 1f))
                imp.transform.localScale *= data.projectileScale;   // 낙하 VFX 와 동일 배율로 축소
            StripNamedChildren(imp, data.impactStripObjects);
        }
        if (playerStat != null && playerTf != null && !playerStat.IsDead && !playerStat.IsInBase)
        {
            Vector3 d = playerTf.position - ground; d.y = 0f;
            if (d.magnitude <= data.skyfallImpactRadius)
                playerStat.TakeDamage(data.attackDamage, ground);
        }
    }

    /// <summary>연속 발사(팡팡). projectileCount 만큼 interval 간격으로 쏜다. 매 발사마다 현재 플레이어 재조준.</summary>
    private IEnumerator FireProjectileBurst()
    {
        int n = Mathf.Max(1, data.projectileCount);
        for (int i = 0; i < n; i++)
        {
            if (dead) yield break;
            FireProjectile();
            if (i < n - 1 && data.projectileInterval > 0f)
                yield return new WaitForSeconds(data.projectileInterval);
        }
    }

    /// <summary>원거리 공격 — 총구(눈/턱)에서 얼음/마법 발사체를 쏜다.
    /// 발사 순간 플레이어(가슴)를 향해 조준하고, 이후 이동/명중은 발사체가 스스로 처리(회피 가능).</summary>
    private void FireProjectile()
    {
        var t = Target;
        if (t == null || data.projectileVFX == null) return;

        Transform muzzle = telegraphAnchor != null ? telegraphAnchor : transform;
        Vector3 origin = MuzzlePos();

        Vector3 aim = t.position + Vector3.up * data.projectileAimHeight;
        Vector3 dir = aim - origin;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        // 머즐 플래시(선택) — 총구에 부착해 잠깐 번쩍.
        if (data.muzzleVFX != null)
        {
            var mz = Instantiate(data.muzzleVFX, origin, Quaternion.LookRotation(dir.normalized), muzzle);
            foreach (var mb in mz.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) Destroy(mb);
            Destroy(mz, 1.5f);
        }

        // 발사체 — VFX 프리팹에 우리 구동 스크립트를 붙여 월드에 띄운다.
        var proj = Instantiate(data.projectileVFX, origin, Quaternion.LookRotation(dir.normalized));
        // VFX 팩에 딸려온 데모 이동/충돌 스크립트 제거 — 안 그러면 우리 구동과 충돌(이중 이동)한다.
        foreach (var mb in proj.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null) Destroy(mb);
        if (data.projectileScale > 0f && !Mathf.Approximately(data.projectileScale, 1f))
            proj.transform.localScale *= data.projectileScale;
        var mover = proj.AddComponent<FieldMonsterProjectile>();
        mover.Init(
            t, dir, data.projectileSpeed, data.attackDamage, data.projectileHitRadius,
            data.projectileLifeTime, data.projectileHomingDeg, data.projectileHomingDuration,
            data.projectileAimHeight, transform.position, data.impactVFX, data.projectileBlockMask);
    }

    /// <summary>제자리 정지(대기/공격 중). 스텝 속도 오버라이드도 해제해 Idle 로 떨어지게 한다.</summary>
    private void Stop()
    {
        speedOverride = -1f;
        strafeAnimMul = -1f;
        if (nav == null || !nav.enabled) return;
        nav.isStopped = true;
        nav.velocity = Vector3.zero;
        nav.ResetPath();
    }

    /// <summary>Locomotion 2D 블렌드 좌표 '즉시' 지정. x=좌우(straif), y=앞뒤(walk6/walk3).
    /// ※SimpleDirectional2D 는 중앙(0,0) 자식이 없어, 값을 천천히 바꾸면 (0,0) 부근에서
    ///   블렌드가 정의되지 않아 T-포즈가 난다. 그래서 damp 없이 즉시 스냅한다.</summary>
    private void SetLocomotion(Vector2 v)
    {
        if (animator == null) return;
        if (hasStrafeParam)  animator.SetFloat(strafeHash, v.x);
        if (hasMoveDirParam) animator.SetFloat(moveDirHash, v.y);
    }
}
