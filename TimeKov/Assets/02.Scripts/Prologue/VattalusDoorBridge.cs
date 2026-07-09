using UnityEngine;

/// <summary>
/// VattalusDoorController를 우리 IInteractable로 연결하는 브릿지.
/// 같은 GameObject에 추가하면 PlayerInteractComponent가 F키로 문을 열고 닫을 수 있다.
/// </summary>
[RequireComponent(typeof(VattalusDoorController))]
public class VattalusDoorBridge : MonoBehaviour, IInteractable, IInteractHint
{
    VattalusDoorController _door;

    void Awake() => _door = GetComponent<VattalusDoorController>();

    public bool CanInteract => true;
    public string HintLabel => "문 열기";

    public void Interact(Player player)
    {
        if (_door.isDoorOpen()) _door.CloseDoor();
        else _door.OpenDoor();
    }
}
