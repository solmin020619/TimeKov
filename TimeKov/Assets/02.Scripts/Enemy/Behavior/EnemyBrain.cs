using System.Collections;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BehaviorGraphAgent))]
public class EnemyBrain : MonoBehaviour, IEnemyDataSource
{
    [Header("Data")]
    [SerializeField] private MeleeEnemyData data;
    public MeleeEnemyData Data => data;

    [Header("Sensors")]
    [SerializeField] private VisionSensor visionSensor;

    [Header("Blackboard Variable Names")]
    [SerializeField] private string selfAgentVarName = "Self";
    [SerializeField] private string targetVarName = "Target";
    [SerializeField] private string attackRangeVarName = "AttackRange";
    [SerializeField] private string attackCooldownVarName = "AttackCooldown";
    [SerializeField] private string attackDamageVarName = "AttackDamage";
    [SerializeField] private string hitDelayVarName = "HitDelay";
    [SerializeField] private string animLengthVarName = "AnimLength";
    [SerializeField] private string moveSpeedVarName = "MoveSpeed";
    [SerializeField] private string distanceThresholdVarName = "DistanceThreshold";
    [SerializeField] private string patrolPointsVarName = "PatrolPoints";

    [Header("Animator")]
    [Tooltip("Animator의 Locomotion 전이용 float 파라미터 이름. NavMeshAgent.velocity.magnitude가 매 프레임 들어감.")]
    [SerializeField] private string speedParamName = "Speed";

    [Header("Rotation")]
    [Tooltip("이동 방향으로 회전하기 시작하는 최소 속도 (m/s). 이보다 빠르면 진행 방향을 바라보고, 느리면(정지/공격) 타깃을 바라봄.")]
    [SerializeField] private float moveFaceThreshold = 0.1f;

    private BehaviorGraphAgent btAgent;
    private EnemyFeedback feedback;
    private GameObject lastTarget;
    private Transform cachedPlayerTransform;
    private PlayerStatComponent playerStat;
    private EnemyHealth healthRef;
    private NavMeshAgent navAgent;
    private Animator animator;
    private int speedParamHash;

    private void Awake()
    {
        btAgent = GetComponent<BehaviorGraphAgent>();
        if (visionSensor == null)
            visionSensor = GetComponentInChildren<VisionSensor>();

        feedback = GetComponent<EnemyFeedback>();
        healthRef = GetComponent<EnemyHealth>();
        navAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        speedParamHash = Animator.StringToHash(speedParamName);

        // Root Motion이 켜져있으면 Animator가 위치 제어 → SO의 moveSpeed 무시.
        // SO 권위 유지 위해 강제 OFF.
        if (animator != null) animator.applyRootMotion = false;

        // NavMeshAgent의 자동 회전 비활성 — 공격 중(isStopped=true) 회전도 멈추는 문제 방지.
        // 회전은 LateUpdate에서 EnemyBrain이 직접 처리 (공격 중에도 타깃 향해 돌아감).
        if (navAgent != null) navAgent.updateRotation = false;

        // Health 초기화 (currentHP = maxHP). 이후엔 Update에서 maxHP만 동기화하고 currentHP는 게임 로직이 관리.
        if (healthRef != null && data != null)
        {
            healthRef.maxHP = data.maxHP;
            healthRef.currentHP = data.maxHP;
        }

        if (feedback != null && data != null)
            feedback.SetData(data);

        // 첫 프레임 전에 모든 SO 값을 컴포넌트/Blackboard에 1회 동기화
        SyncAllFromData();
    }

#if UNITY_EDITOR
    // Play 중 인스펙터에서 EnemyBrain 값을 바꾸면 즉시 반영(에디터 디버그 편의). 매 프레임 동기화 대체.
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        SyncAllFromData();
        if (feedback != null && data != null) feedback.SetData(data);
    }
#endif

    private void Start()
    {
        btAgent.SetVariableValue(selfAgentVarName, gameObject);

        // 피격 시 즉시 가해자(=Player) 인식 — 뒤에서 맞아도 추적 시작
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            cachedPlayerTransform = p.transform;
            playerStat = p.GetComponent<PlayerStatComponent>();
        }

        if (healthRef != null)
            healthRef.OnDamage += OnTookDamage;

        if (feedback != null)
            feedback.PlaySpawn();
    }

    private void OnDestroy()
    {
        if (healthRef != null)
            healthRef.OnDamage -= OnTookDamage;
    }

    private void OnTookDamage()
    {
        if (visionSensor == null || cachedPlayerTransform == null) return;
        visionSensor.ForceSetTarget(cachedPlayerTransform);
    }

    private void Update()
    {
        // SO -> 컴포넌트 동기화는 Awake에서 1회만 한다(매 프레임 호출은 적 수만큼 낭비라 제거).
        // Play 중 인스펙터에서 SO 값을 바꿔 즉시 반영하려면 아래 OnValidate가 처리.

        // NavMeshAgent 속도 → Animator Speed (Idle ↔ Locomotion 전이용)
        if (animator != null && navAgent != null)
            animator.SetFloat(speedParamHash, navAgent.velocity.magnitude);

        if (visionSensor == null) return;

        Transform spotted = visionSensor.SpottedTarget;
        GameObject targetObj = spotted != null ? spotted.gameObject : null;

        // 플레이어가 죽었거나 결계(기지) 안에 있으면 타깃 해제 —
        // 죽은 플레이어를 계속 공격하거나, 결계 안의 플레이어를 인식/추적하지 않도록.
        if (targetObj != null && playerStat != null && (playerStat.IsDead || playerStat.IsInBase))
            targetObj = null;

        // 타깃이 바뀐 프레임에만 블랙보드에 쓴다(안 바뀌면 같은 값 재기록 = 낭비). 22마리 x 60fps 절약.
        // EnemyBrain이 Target의 유일 작성자라 재기록 생략해도 BT가 읽는 값은 동일.
        if (targetObj != lastTarget)
            btAgent.SetVariableValue(targetVarName, targetObj);

        if (lastTarget == null && targetObj != null)
        {
            feedback?.PlayDetect();
            StartCoroutine(DetectStun());
        }

        lastTarget = targetObj;
    }

    private void LateUpdate()
    {
        // 내장 NavigateToTargetAction(BT)이 추적 시작 시 agent.speed 를 자기 Speed 로 덮어써 SO moveSpeed 를 무시한다.
        // SO 를 권위자로 유지하려고 BT Update 이후(LateUpdate)에 다시 박는다. (float 비교 1개라 비용 무시 가능)
        if (navAgent != null && data != null && !Mathf.Approximately(navAgent.speed, data.moveSpeed))
            navAgent.speed = data.moveSpeed;

        // [회전 기준] 이동 중이면 "진행 방향", 거의 멈춰 있으면(공격/대기) "타깃 방향"을 바라본다.
        // 기존엔 타깃이 있을 때 항상 타깃 방향만 봐서, 순찰 중(타깃 없음)엔 회전을 아예 안 하고
        // 추격 중엔 측면으로 접근하며 옆걸음·문워크가 발생했음. NavMeshAgent.updateRotation 은
        // 꺼둔 상태(공격 중에도 직접 회전 제어하려고)라, 회전은 여기서 전적으로 처리한다.
        Vector3 faceDir = Vector3.zero;

        // 1) 이동 중 — NavMeshAgent 속도(진행) 방향 우선
        if (navAgent != null)
        {
            Vector3 vel = navAgent.velocity;
            vel.y = 0f;
            if (vel.sqrMagnitude > moveFaceThreshold * moveFaceThreshold)
                faceDir = vel;
        }

        // 2) 거의 멈춤 — 타깃이 있으면 타깃 방향 (공격 중에도 플레이어를 향해 돌아감)
        if (faceDir.sqrMagnitude < 0.0001f && visionSensor != null)
        {
            var target = visionSensor.SpottedTarget;
            if (target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                faceDir = toTarget;
            }
        }

        if (faceDir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(faceDir);
        float angularSpeedDeg = data != null ? data.angularSpeed : 480f;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, angularSpeedDeg * Time.deltaTime);
    }

    /// <summary>
    /// Detect 진입 시 detectStunDuration 동안 NavMeshAgent 정지.
    /// Roar/Howl 등 발견 모션 재생 중 적이 가만히 있다가 끝나면 추격 시작.
    /// </summary>
    private IEnumerator DetectStun()
    {
        if (data == null || data.detectStunDuration <= 0f) yield break;
        if (navAgent == null) yield break;

        // BehaviorGraph(NavigateToTargetAction)가 매 프레임 SetDestination 호출하므로
        // isStopped를 매 프레임 강제로 true 유지해야 진짜 멈춤. 안 그러면 Detect 모션 중에 미끄러져 따라감.
        float endTime = Time.time + data.detectStunDuration;
        while (Time.time < endTime)
        {
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
                navAgent.velocity = Vector3.zero;
                navAgent.ResetPath();
            }
            yield return null;
        }

        if (navAgent != null && navAgent.enabled) navAgent.isStopped = false;
    }

    // ── SO → 컴포넌트 동기화 ─────────────────────────────────────────
    // SO가 단일 권위자. 매 프레임 모든 관련 컴포넌트/Blackboard에 SO 값 반영.
    // Play 모드 중 SO 인스펙터에서 값 바꾸면 즉시 게임에 반영됨.

    private void SyncAllFromData()
    {
        if (data == null) return;
        SyncNavAgentFromData();
        SyncHealthFromData();
        SyncVisionFromData();
        SyncBlackboardFromData();
    }

    private void SyncNavAgentFromData()
    {
        if (navAgent == null) return;
        navAgent.speed = data.moveSpeed;
        navAgent.acceleration = data.acceleration;
        navAgent.angularSpeed = data.angularSpeed;
        navAgent.stoppingDistance = data.stoppingDistance;
    }

    private void SyncHealthFromData()
    {
        if (healthRef == null) return;
        healthRef.maxHP = data.maxHP;
        // currentHP는 게임 로직이 관리. maxHP를 줄였을 때만 상한 클램프.
        if (healthRef.currentHP > data.maxHP) healthRef.currentHP = data.maxHP;
    }

    private void SyncVisionFromData()
    {
        if (visionSensor == null) return;
        visionSensor.ApplyVisionParameters(data.visionRange, data.visionAngle);
        visionSensor.ApplyLostMemory(data.targetLostMemory);
    }

    private void SyncBlackboardFromData()
    {
        if (btAgent == null) return;
        btAgent.SetVariableValue(attackRangeVarName, data.attackRange);
        btAgent.SetVariableValue(attackCooldownVarName, data.attackCooldown);
        btAgent.SetVariableValue(attackDamageVarName, data.attackDamage);
        btAgent.SetVariableValue(hitDelayVarName, data.hitDelay);
        btAgent.SetVariableValue(animLengthVarName, data.animLength);
        btAgent.SetVariableValue(moveSpeedVarName, data.moveSpeed);
        // NavigateToTargetAction의 도달 판정 거리 = attackRange × ratio. 약간 가까이 도달해야 MeleeAttack 진입 시 distance < attackRange 보장.
        btAgent.SetVariableValue(distanceThresholdVarName, data.attackRange * data.attackApproachRatio);
    }

    /// <summary>
    /// SpawnPoint가 적 생성 직후 호출. Patrol 영역(웨이포인트 리스트)을 Blackboard에 주입.
    /// Awake 이후 ~ Start 전 호출 가능 (btAgent 캐싱 보장).
    /// </summary>
    public void SetPatrolPoints(List<GameObject> points)
    {
        if (btAgent == null) btAgent = GetComponent<BehaviorGraphAgent>();
        if (btAgent == null || points == null) return;
        btAgent.SetVariableValue(patrolPointsVarName, points);
    }
}
