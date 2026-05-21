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
}
