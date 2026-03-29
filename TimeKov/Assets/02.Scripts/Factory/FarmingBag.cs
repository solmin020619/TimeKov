// =====================================================================
// FarmingBag.cs  (설비 #1 — 파밍 가방)
// 플레이어가 레이드에서 들고 온 아이템을 공장 시스템에 투입하는 진입점.
// 연결된 Sorter(물자 분류기) 방향 벨트로 아이템을 밀어 넣는다.
//
// 플레이어 인벤토리 연동 시:
//   InsertItem(itemId, amount) 또는 InsertAll(list) 를 호출하면 된다.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class FarmingBag : MonoBehaviour
    {
        [Header("→ 물자 분류기로 가는 벨트")]
        [SerializeField] private ConveyorBelt outputBelt;

        // ---------------------------------------------------------------
        // 단일 아이템 투입
        // ---------------------------------------------------------------
        public void InsertItem(string itemId, int amount)
        {
            if (outputBelt == null || !outputBelt.IsConnected)
            {
                Debug.LogWarning("[FarmingBag] 출력 벨트 미연결");
                return;
            }
            outputBelt.TryTransport(itemId, amount);
        }

        // ---------------------------------------------------------------
        // 여러 아이템 일괄 투입 (플레이어 인벤토리 → 공장)
        // itemId/amount 쌍의 리스트를 넘겨받아 순차 투입한다.
        // ---------------------------------------------------------------
        public void InsertAll(List<(string itemId, int amount)> items)
        {
            foreach (var (id, amt) in items)
                InsertItem(id, amt);
        }
    }
}
