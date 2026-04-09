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
            // DataStore 로드 완료 대기
            yield return new WaitUntil(() =>
                DataStore.IsLoaded && DataStore.GetItem(itemId) != null);

            if (playerInventory == null) yield break;

            playerInventory.AddItem(itemId, amount);
            playerInventory.ForceRefreshUI();

            var row = DataStore.GetItem(itemId);
            Debug.Log($"[공장 출력] {row?.itemName ?? itemId.ToString()} x{amount} → 인벤토리 추가 완료");
        }
    }
}
