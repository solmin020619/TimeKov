// =====================================================================
// ItemBuffer.cs
// 설비 내부 재고. itemId(int) → 수량 관리.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class ItemBuffer
    {
        private readonly Dictionary<int, int> _stock = new();

        public IReadOnlyDictionary<int, int> Stock => _stock;

        /// <summary>현재 쌓여 있는 아이템 총 개수(종류 무관). 출력 버퍼 상한 판정 등에 사용.</summary>
        public int TotalCount
        {
            get
            {
                int t = 0;
                foreach (var kv in _stock) t += kv.Value;
                return t;
            }
        }

        public void Add(int itemId, int amount)
        {
            if (itemId <= 0) return;
            _stock.TryGetValue(itemId, out int cur);
            _stock[itemId] = cur + amount;
        }

        public bool Consume(int itemId, int amount)
        {
            if (!Has(itemId, amount)) return false;
            _stock[itemId] -= amount;
            if (_stock[itemId] <= 0) _stock.Remove(itemId);
            return true;
        }

        public bool Has(int itemId, int amount)
        {
            _stock.TryGetValue(itemId, out int cur);
            return cur >= amount;
        }

        public bool HasAll(FactorySlot[] slots)
        {
            foreach (var s in slots)
                if (!Has(s.itemId, s.amount)) return false;
            return true;
        }

        public void ConsumeAll(FactorySlot[] slots)
        {
            foreach (var s in slots)
                Consume(s.itemId, s.amount);
        }

        public int GetAmount(int itemId)
        {
            _stock.TryGetValue(itemId, out int cur);
            return cur;
        }

        public void Clear() => _stock.Clear();
    }
}
