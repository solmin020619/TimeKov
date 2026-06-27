using UnityEngine;

namespace TIMEKOV.Factory
{
    /// <summary>
    /// 저장고 설비 — 벨트로 도착한 아이템을 바로 창고(StorageInstance)에 입고시킨다.
    /// 아이템 오브젝트는 BeltSegment가 Receive() 호출 직후 자동으로 사라진다.
    /// </summary>
    public class WarehouseReceiver : MachineBase
    {
        // 선택 패널·외곽선·F키 힌트 표시 안 함
        public override bool ShowInteraction => false;

        public override void Receive(int itemId, int amount)
        {
            var storage = InventoryManager.StorageInstance;
            if (storage != null)
            {
                storage.AddItem(itemId, amount);
                storage.ForceRefreshUI();
            }
            else
            {
                var itemData = GameDataUtility.GetItem(itemId);
                Debug.LogWarning($"[WarehouseReceiver] StorageInstance 없음 — {itemData?.itemName ?? itemId.ToString()} x{amount} 입고 불가");
            }
        }
    }
}
