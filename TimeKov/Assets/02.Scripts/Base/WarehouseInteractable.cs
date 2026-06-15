// WarehouseInteractable.cs
// [폐기 2026-06-14] 구 창고 = 오브젝트에 F로 상호작용해서 여는 방식이었음.
// 이제 결계존(BaseZone) 안에서 TAB 하면 창고가 자동 연동된다(InventoryUIController.Open).
// F 상호작용은 더 이상 필요 없어 무력화함. 씬의 창고 오브젝트/이 컴포넌트는 지워도 됨.

using UnityEngine;

public class WarehouseInteractable : MonoBehaviour, IInteractable
{
    // 폐기: F 프롬프트/상호작용 없음 (결계존 기반 자동 연동으로 대체)
    public bool CanInteract => false;

    public void Interact(Player player) { /* 폐기됨 - 결계존 안 TAB 으로 창고 자동 연동 */ }
}
