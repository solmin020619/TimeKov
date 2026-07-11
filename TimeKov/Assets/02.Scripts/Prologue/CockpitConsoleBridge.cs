using UnityEngine;

/// <summary>
/// 조종석 콘솔 버튼. EmergencyConsole로 대체됨 — 레거시 보존.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class CockpitConsoleBridge : MonoBehaviour, IInteractable
{
    [SerializeField] string interactId = "cockpit_console";

    void Awake()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
    }

    public bool CanInteract => true;

    public void Interact(Player player)
    {
        GameEvents.RaiseInteracted(interactId);
    }
}
