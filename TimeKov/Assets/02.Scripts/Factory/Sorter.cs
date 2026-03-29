// =====================================================================
// Sorter.cs  (설비 #2 — 물자 분류기)
// 파밍 가방에서 넘어온 아이템을 itemId 기준으로 지정 벨트에 분배.
//
// Inspector 설정:
//   routingTable 에 [ itemId → 보낼 벨트 ] 쌍을 추가한다.
//   기획서 분류 기준:
//     파쇄가능  → 폐기물 파쇄기 벨트
//     용해가능  → 간이 용광로 벨트
//     추출가능  → 화학물질 추출기 벨트
//     판매전용  → 무역 창고 벨트
//     동력원    → 디젤 발전기 벨트
//     직접투입  → 해당 조립대 벨트
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    [System.Serializable]
    public struct SortRoute
    {
        [Tooltip("ItemData.itemId 와 동일한 값")]
        public string itemId;
        [Tooltip("이 아이템을 보낼 컨베이어 벨트")]
        public ConveyorBelt belt;
    }

    public class Sorter : MachineBase
    {
        [Header("라우팅 테이블 (itemId → 벨트)")]
        [SerializeField] private List<SortRoute> routingTable = new();

        private readonly Dictionary<string, ConveyorBelt> _routes = new();

        private void Awake()
        {
            foreach (var route in routingTable)
                if (route.belt != null)
                    _routes[route.itemId] = route.belt;

            // 분류기는 항상 "연결됨" 취급 (파밍 가방이 직접 밀어넣음)
            inputConnections  = 1;
            outputConnections = routingTable.Count;
        }

        protected override void OnReceived(string itemId, int amount)
        {
            if (_routes.TryGetValue(itemId, out var belt))
            {
                belt.TryTransport(itemId, amount);
            }
            else
            {
                Debug.LogWarning($"[Sorter] 라우팅 경로 없음: '{itemId}' — routingTable 확인 필요");
            }
        }
    }
}
