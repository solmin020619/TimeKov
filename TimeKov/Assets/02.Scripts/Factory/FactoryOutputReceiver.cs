// =====================================================================
// FactoryOutputReceiver.cs
// 공장 완성품 자동 인벤토리 수납
// 구버전 DataStore.IsLoaded / DataStore.GetItem → DataBoot / GameDataUtility 교체
// =====================================================================

using System.Collections;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class FactoryOutputReceiver : MachineBase
    {
        [Header("완성품 넣을 인벤토리")]
        public InventoryManager playerInventory;

        public override void Receive(int itemId, int amount)
        {
            StartCoroutine(AddWhenReady(itemId, amount));
        }

        private IEnumerator AddWhenReady(int itemId, int amount)
        {
            // 구버전: DataStore.IsLoaded → DataBoot.IsLoaded
            yield return new WaitUntil(() =>
                DataBoot.IsLoaded && GameDataUtility.GetItem(itemId) != null);

            if (playerInventory == null) yield break;

            playerInventory.AddItem(itemId, amount);
            playerInventory.ForceRefreshUI();

            // 구버전: DataStore.GetItem → GameDataUtility.GetItem
            var itemData = GameDataUtility.GetItem(itemId);
            Debug.Log($"[공장 출력] {itemData?.itemName ?? itemId.ToString()} x{amount} → 인벤토리 추가 완료");
        }
    }
}