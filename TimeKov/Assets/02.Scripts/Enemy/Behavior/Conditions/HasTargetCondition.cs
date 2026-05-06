using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

/// <summary>
/// Blackboard 의 Target GameObject 가 set 됐는지 검사.
/// EnemyBrain 이 매 프레임 VisionSensor 결과를 Target 에 주입하므로,
/// 이 Condition 으로 'Patrol vs Chase' 분기를 만든다.
/// </summary>
[Serializable, GeneratePropertyBag]
[Condition(
    name: "Has Target",
    category: "Conditions",
    story: "[Target] is set",
    id: "e5d4c3b2a1908f7e6d5c4b3a29180706")]
internal partial class HasTargetCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    public override bool IsTrue()
    {
        return Target != null && Target.Value != null;
    }
}
