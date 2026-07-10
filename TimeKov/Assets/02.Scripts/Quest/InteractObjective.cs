using UnityEngine;

/// <summary>
/// 씬 내 특정 interactId를 가진 오브젝트에 F키 상호작용하면 완료되는 퀘스트 목표.
/// CockpitConsoleBridge 등 IInteractable 구현체가 GameEvents.RaiseInteracted(id)를 호출해야 함.
/// </summary>
[CreateAssetMenu(menuName = "Quest/Objective/Interact")]
public class InteractObjective : ObjectiveSO
{
    [Tooltip("완료 조건이 되는 상호작용 ID. CockpitConsoleBridge의 interactId와 일치해야 함.")]
    public string interactId;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()   => GameEvents.OnInteracted += OnInteracted;
    public override void Deactivate() => GameEvents.OnInteracted -= OnInteracted;

    protected override bool IsAlreadySatisfied()
        => GameEvents.RecentCount(GameEvents.KeyInteracted(interactId)) > 0;

    void OnInteracted(string id)
    {
        if (id == interactId) Complete();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(interactId))
            UnityEngine.Debug.LogError($"[InteractObjective] '{name}' interactId 필수", this);
    }
#endif
}
