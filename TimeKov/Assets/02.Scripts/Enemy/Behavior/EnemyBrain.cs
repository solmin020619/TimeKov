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
    [SerializeField] private string patrolPointsVarName = "PatrolPoints";

    private BehaviorGraphAgent btAgent;
    private EnemyFeedback feedback;
    private GameObject lastTarget;

    private void Awake()
    {
        btAgent = GetComponent<BehaviorGraphAgent>();
        if (visionSensor == null)
            visionSensor = GetComponentInChildren<VisionSensor>();

        feedback = GetComponent<EnemyFeedback>();

        ApplyDataToComponents();

        if (feedback != null && data != null)
            feedback.SetData(data);
    }

    private void Start()
    {
        btAgent.SetVariableValue(selfAgentVarName, gameObject);

        ApplyDataToBlackboard();

        if (feedback != null)
            feedback.PlaySpawn();
    }

    private void Update()
    {
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

        var agent = GetComponent<NavMeshAgent>();
        if (agent == null) yield break;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        yield return new WaitForSeconds(data.detectStunDuration);

        if (agent != null && agent.enabled) agent.isStopped = false;
    }

    private void ApplyDataToComponents()
    {
        if (data == null) return;

        EnemyHealth health = GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.maxHP = data.maxHP;
            health.currentHP = data.maxHP;
        }

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.speed = data.moveSpeed;

        if (visionSensor != null)
        {
            visionSensor.ApplyVisionParameters(data.visionRange, data.visionAngle);
            visionSensor.ApplyLostMemory(data.targetLostMemory);
        }
    }

    private void ApplyDataToBlackboard()
    {
        if (data == null) return;

        btAgent.SetVariableValue(attackRangeVarName, data.attackRange);
        btAgent.SetVariableValue(attackCooldownVarName, data.attackCooldown);
        btAgent.SetVariableValue(attackDamageVarName, data.attackDamage);
        btAgent.SetVariableValue(hitDelayVarName, data.hitDelay);
        btAgent.SetVariableValue(animLengthVarName, data.animLength);
        btAgent.SetVariableValue(moveSpeedVarName, data.moveSpeed);
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
