// =====================================================================
// FactoryOutputReceiver.cs
// 벨트 체인 맨 끝에 놓는 최종 수신기.
// 아이템이 도착하면 플레이어 인벤토리에 직접 넣는다.
//
// 사용 방법:
//   빈 오브젝트에 붙이고, 마지막 ConveyorBelt 의 target 으로 연결.
//   playerInventory 필드에 플레이어 인벤토리 드래그.
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
            StartCoroutine(AddNextFrame(itemId, amount));
        }

        private IEnumerator AddNextFrame(int itemId, int amount)
        {
            // DataManager 로드 완료 대기
            yield return new WaitUntil(() =>
                DataManager.Instance != null &&
                DataManager.Instance.GetItem(itemId) != null);

            if (playerInventory != null)
            {
                playerInventory.AddItem(itemId, amount);
                playerInventory.ForceRefreshUI();

                var item = DataManager.Instance.GetItem(itemId);
                Debug.Log($"[공장 출력] {item.itemName} x{amount} → 인벤토리 추가 완료");
            }
        }
    }
}
