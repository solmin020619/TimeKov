using System.Collections;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BehaviorGraphAgent))]
public class EnemyBrain : MonoBehaviour
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

    private BehaviorGraphAgent btAgent;
    private EnemyFeedback feedback;
    private GameObject lastTarget;
    private Transform cachedPlayerTransform;
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

    private void Start()
    {
        btAgent.SetVariableValue(selfAgentVarName, gameObject);

        // 피격 시 즉시 가해자(=Player) 인식 — 뒤에서 맞아도 추적 시작
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) cachedPlayerTransform = p.transform;

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
        // SO → 모든 컴포넌트/Blackboard 매 프레임 동기화. Play 모드 중 SO 변경 즉시 반영.
        SyncAllFromData();

        // NavMeshAgent 속도 → Animator Speed (Idle ↔ Locomotion 전이용)
        if (animator != null && navAgent != null)
            animator.SetFloat(speedParamHash, navAgent.velocity.magnitude);

        if (visionSensor == null) return;

        Transform spotted = visionSensor.SpottedTarget;
        GameObject targetObj = spotted != null ? spotted.gameObject : null;
        btAgent.SetVariableValue(targetVarName, targetObj);

        if (lastTarget == null && targetObj != null)
        {
            feedback?.PlayDetect();
            StartCoroutine(DetectStun());
        }

        lastTarget = targetObj;
    }

    /// <summary>
    /// Detect 진입 시 detectStunDuration 동안 NavMeshAgent 정지.
    /// Roar/Howl 등 발견 모션 재생 중 적이 가만히 있다가 끝나면 추격 시작.
    /// </summary>
    private IEnumerator DetectStun()
    {
        if (data == null || data.detectStunDuration <= 0f) yield break;
        if (navAgent == null) yield break;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        yield return new WaitForSeconds(data.detectStunDuration);

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
        // NavigateToTargetAction의 도달 판정 거리 = attackRange (도달 즉시 공격 노드 전이)
        btAgent.SetVariableValue(distanceThresholdVarName, data.attackRange);
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
