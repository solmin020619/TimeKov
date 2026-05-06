using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 적의 stat 을 SO(MeleeEnemyData) 에서 읽어 EnemyHealth / NavMeshAgent / VisionSensor / BT Blackboard 에 주입하고,
/// VisionSensor 가 발견한 Target 을 매 프레임 BT Blackboard 에 동기화한다.
///
/// prefab 1개 + SO N개 = 변종 N개 패턴: prefab 의 Data 슬롯에 SO 만 갈아 끼우면 변종 완성.
/// 인스펙터 Blackboard 변수 이름은 그래프와 정확히 일치해야 한다 (대소문자 포함).
/// </summary>
[RequireComponent(typeof(BehaviorGraphAgent))]
public class EnemyBrain : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("이 적의 stat 을 정의하는 SO. 비워두면 컴포넌트 인스펙터 값이 그대로 사용됨.")]
    [SerializeField] private MeleeEnemyData data;

    [Header("Sensors")]
    [SerializeField] private VisionSensor visionSensor;

    [Header("Blackboard Variable Names")]
    [SerializeField] private string selfAgentVarName = "Self";
    [SerializeField] private string targetVarName = "Target";
    [SerializeField] private string attackRangeVarName = "AttackRange";
    [SerializeField] private string attackCooldownVarName = "AttackCooldown";
    [SerializeField] private string attackDamageVarName = "AttackDamage";

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

        // EnemyFeedback 이 있으면 같은 SO 데이터로 초기화 (VFX/Sound 슬롯 공유).
        if (feedback != null && data != null)
            feedback.SetData(data);
    }

    private void Start()
    {
        // BT Action 노드들이 Agent 변수로 자기 자신을 받게 주입.
        btAgent.SetVariableValue(selfAgentVarName, gameObject);

        // SO 의 stat 을 BT Blackboard 변수에 주입. 그래프의 default 값을 덮어쓴다.
        ApplyDataToBlackboard();

        // 스폰 피드백 (VFX/Sound) 한 번 재생.
        if (feedback != null)
            feedback.PlaySpawn();
    }

    private void Update()
    {
        if (visionSensor == null) return;

        // VisionSensor 가 본 Target 을 Blackboard 에 매 프레임 동기화.
        Transform spotted = visionSensor.SpottedTarget;
        GameObject targetObj = spotted != null ? spotted.gameObject : null;
        btAgent.SetVariableValue(targetVarName, targetObj);

        // null → not null 전환 시점에 Detect 피드백 1회 (어그로 신호).
        if (lastTarget == null && targetObj != null)
            feedback?.PlayDetect();

        lastTarget = targetObj;
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
            visionSensor.ApplyVisionParameters(data.visionRange, data.visionAngle);
    }

    private void ApplyDataToBlackboard()
    {
        if (data == null) return;

        btAgent.SetVariableValue(attackRangeVarName, data.attackRange);
        btAgent.SetVariableValue(attackCooldownVarName, data.attackCooldown);
        btAgent.SetVariableValue(attackDamageVarName, data.attackDamage);
    }
}
