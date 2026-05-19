using UnityEngine;

namespace TIMEKOV.Factory
{
    public class FactoryOutputReceiver : MachineBase
    {
        public override void Receive(int itemId, int amount)
        {
            var inv = InventoryManager.Instance;
            if (inv != null)
            {
                inv.AddItem(itemId, amount);
                inv.ForceRefreshUI();
            }
            else
            {
                var itemData = GameDataUtility.GetItem(itemId);
                Debug.LogWarning($"[FactoryOutputReceiver] InventoryManager 없음 — {itemData?.itemName ?? itemId.ToString()} x{amount} 지급 불가");
            }
        }
    }
}