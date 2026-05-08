using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

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
