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

    [Header("Rotation")]
    [Tooltip("이 속도 이상으로 움직이면 진행 방향을 봄. 그보다 느리면 타깃을 봄.")]
    [SerializeField] private float moveFaceThreshold = 0.1f;

    private NavMeshAgent nav;
    private EnemyHealth health;
    private EnemyFeedback feedback;
    private Transform playerTf;
    private PlayerStatComponent playerStat;

    private int speedHash, strafeHash, moveDirHash, speedMulHash;
    private bool hasStrafeParam, hasMoveDirParam, hasSpeedMulParam;

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
    private Vector3 homePos;

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
        if (animator != null)
        {
            animator.applyRootMotion = false;   // 위치 권위는 NavMeshAgent(SO moveSpeed)
            // 블렌드 파라미터가 없는 컨트롤러면 SetFloat 경고가 나므로 미리 확인
            foreach (var p in animator.parameters)
            {
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
    }

    // EnemyHealth 는 EnemyBrain 만 꺼준다 -> 이 AI 는 스스로 멈춰야 시체가 안 따라온다.
    private void OnDied()
    {
        dead = true;
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
        while (!dead)
        {
            // 1) 타깃 없음 — 어슬렁 or 대기
            while (Target == null && !dead)
                yield return data.wander ? Wander() : Idle();
            if (dead) yield break;

            // 2) 발견 — 발견 연출 후 오프닝 스텝(몬스터별 성격)
            feedback?.PlayDetect();
            yield return DetectPause();
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
        float end = Time.time + data.detectStunDuration;
        faceTarget = true;
        while (Time.time < end && !dead) { Stop(); yield return null; }
        faceTarget = false;
    }

    /// <summary>공격 사거리 안까지 접근.</summary>
    private IEnumerator Chase()
    {
        SyncNav();
        float reach = data.attackRange * data.attackApproachRatio;

        while (!dead)
        {
            var t = Target;
            if (t == null) yield break;

            float d = Vector3.Distance(transform.position, t.position);
            if (d <= reach) { Stop(); yield break; }

            if (nav != null && nav.enabled)
            {
                nav.isStopped = false;
                nav.SetDestination(t.position);
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

        PlayTelegraph();                       // ★ 공격 예고 — 이게 hitDelay 만큼 앞선다
        feedback?.PlayAttack();
        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);

        float t = 0f;
        bool damaged = false;
        while (t < data.animLength && !dead)
        {
            t += Time.deltaTime;
            if (!damaged && t >= data.hitDelay) { damaged = true; ApplyDamage(); }
            yield return null;
        }

        freezeFacing = false;
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
    private void PlayTelegraph()
    {
        if (data.telegraphVFX != null)
        {
            Transform a = telegraphAnchor != null ? telegraphAnchor : transform;
            // 몬스터에 부착 -> 움직여도 눈/턱에 붙어 따라감
            var vfx = Instantiate(data.telegraphVFX, a.position, a.rotation, a);

            // VFX 팩에 딸려온 데모 스크립트(예: RotateGunOnMouse — 카메라 없으면 매 프레임 "No Camera" 로그
            // 폭탄 → 에디터 렉)를 제거한다. 전조는 순수 시각효과라 게임플레이 스크립트가 필요 없다.
            foreach (var mb in vfx.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) Destroy(mb);

            if (data.telegraphLifeTime > 0f) Destroy(vfx, data.telegraphLifeTime);
        }
        if (audioSource != null && data.telegraphSound != null)
            audioSource.PlayOneShot(data.telegraphSound);
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
