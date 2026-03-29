// =====================================================================
// ItemBuffer.cs
// 설비 내부 입출력 버퍼. itemId → 보유 수량 을 관리한다.
//
// ItemData 참조가 필요한 곳(UI 아이콘 등)은 ItemDatabase를 통해
// 다른 팀원 코드에서 조회한다. 여기서는 순수하게 수량만 다룬다.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class ItemBuffer
    {
        private readonly Dictionary<string, int> _stock = new();
        private readonly int _capacity;

        public ItemBuffer(int capacity = 999) => _capacity = capacity;

        // 수량 추가
        public void Add(string itemId, int amount)
        {
            _stock.TryGetValue(itemId, out int cur);
            _stock[itemId] = Mathf.Min(cur + amount, _capacity);
        }

        // 수량 소모 (부족하면 false)
        public bool Consume(string itemId, int amount)
        {
            if (!Has(itemId, amount)) return false;
            _stock[itemId] -= amount;
            if (_stock[itemId] == 0) _stock.Remove(itemId);
            return true;
        }

        // 보유 여부 확인
        public bool Has(string itemId, int amount)
        {
            _stock.TryGetValue(itemId, out int cur);
            return cur >= amount;
        }

        // 레시피 inputs 전체 충족 여부
        public bool HasAll(ItemSlot[] slots)
        {
            foreach (var slot in slots)
                if (!Has(slot.itemId, slot.amount)) return false;
            return true;
        }

        // 레시피 inputs 전체 소모
        public void ConsumeAll(ItemSlot[] slots)
        {
            foreach (var slot in slots)
                Consume(slot.itemId, slot.amount);
        }

        // 보유 목록 열거 (UI 갱신용)
        public IReadOnlyDictionary<string, int> Stock => _stock;
    }
}
