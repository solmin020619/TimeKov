using UnityEngine;

namespace TIMEKOV.Factory
{
    // TODO: 새 인벤토리 연결 후 Receive 구현
    public class FactoryOutputReceiver : MachineBase
    {
        public override void Receive(int itemId, int amount)
        {
            var itemData = GameDataUtility.GetItem(itemId);
            Debug.Log($"[공장 출력] {itemData?.itemName ?? itemId.ToString()} x{amount} → 인벤토리 연결 대기 중");
        }
    }
}