// WarehouseInteractable.cs
// 창고 오브젝트에 붙이는 스크립트
// 플레이어가 F키로 상호작용하면 인벤토리를 창고 패널과 함께 열기

using UnityEngine;

public class WarehouseInteractable : MonoBehaviour, IInteractable
{
    public bool CanInteract => true;

    public void Interact(Player player)
    {
        InventoryUIController.IsInBase = true;
        InventoryUIController.Instance?.Open();
    }
}