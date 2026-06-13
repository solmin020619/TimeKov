// InventoryManager.cs
// 가방(Player) / 창고(Storage) / 상자(Chest) 인벤토리 데이터 관리
// 슬롯 생성, 아이템 추가/제거/이동/분할 처리
// 씬에 세 개 배치: ownerType=Player maxSlots=35 / ownerType=Storage maxSlots=50 / ownerType=Chest maxSlots=20

using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    // 인벤토리 소유자 유형
    public enum InventoryOwnerType { Player, Storage, Chest }

    [Header("설정")]
    public InventoryOwnerType ownerType = InventoryOwnerType.Player;

    [Tooltip("가방 35 / 창고 50 / 상자 20 권장")]
    public int maxSlots = 35;

    // 슬롯 목록 (인덱스 == slotIndex)
    private List<InventorySlot> _slots = new List<InventorySlot>();

    // 슬롯 데이터가 바뀔 때마다 발생하는 이벤트
    public event Action OnInventoryChanged;

    // [획득 로그] 아이템이 실제로 Player 인벤토리에 들어왔을 때 발생 (itemId, 실제 추가된 수량).
    // AddItem 단일 입구에서 발생하므로 모든 획득 경로(필드 드롭/공장 수령/벨트 자동/퀘스트 보상 등)를 커버.
    // Storage(창고) 인벤토리에는 발생하지 않음 — 필드 획득 로그와 분리.
    // static — 구독자(AcquireLogUI 등)가 Instance 생성 타이밍에 의존하지 않게.
    public static event Action<int, int> OnItemAddedToInventory;

    // Player 인벤토리 싱글톤
    public static InventoryManager Instance { get; private set; }

    // Storage 인벤토리 싱글톤
    public static InventoryManager StorageInstance { get; private set; }

    // Chest 인벤토리 싱글톤 (상자 파밍 전용, 열 때마다 초기화)
    public static InventoryManager ChestInstance { get; private set; }

    private void Awake()
    {
        if (ownerType == InventoryOwnerType.Player)
            Instance = this;
        else if (ownerType == InventoryOwnerType.Storage)
            StorageInstance = this;
        else if (ownerType == InventoryOwnerType.Chest)
            ChestInstance = this;

        CreateSlots();
    }

    /// <summary>모든 슬롯 비우기 (상자 열 때마다 초기화용)</summary>
    public void ClearAllItems()
    {
        foreach (var slot in _slots) slot.Clear();
        OnInventoryChanged?.Invoke();
    }

    // 슬롯 초기화
    public void CreateSlots()
    {
        _slots.Clear();
        for (int i = 0; i < maxSlots; i++)
        {
            var slot = new InventorySlot();
            slot.slotIndex = i;
            _slots.Add(slot);
        }
    }

    // UI 에서 슬롯 목록을 읽기 전용으로 접근
    public IReadOnlyList<InventorySlot> GetSlots() => _slots.AsReadOnly();

    // 현재 아이템이 있는 슬롯 수 반환
    public int GetUsedSlotCount()
    {
        int count = 0;
        foreach (var slot in _slots)
            if (!slot.IsEmpty) count++;
        return count;
    }

    // 최대 슬롯 수 반환
    public int GetMaxSlots() => maxSlots;

    // 특정 인덱스의 슬롯 반환
    public InventorySlot GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Count) return null;
        return _slots[index];
    }

    // 특정 아이템 총 수량 반환
    public int GetTotalItemCount(int itemId)
    {
        int total = 0;
        foreach (var slot in _slots)
            if (slot.itemId == itemId && !slot.IsEmpty)
                total += slot.amount;
        return total;
    }

    // 아이템 추가 (남은 수량 반환)
    public int AddItem(int itemId, int amount, bool markAsNew = false)
    {
        // GameDataHolder 에서 maxStack 조회
        var data = ItemDatabase.GetItem(itemId);
        int maxStack = data != null ? data.maxStack : 999;
        int remaining = amount;

        // 기존 슬롯에 스택 추가
        foreach (var slot in _slots)
        {
            if (remaining <= 0) break;
            if (slot.itemId == itemId && !slot.IsEmpty && slot.amount < maxStack)
            {
                int canAdd = maxStack - slot.amount;
                int adding = Mathf.Min(canAdd, remaining);
                slot.amount += adding;
                remaining -= adding;
            }
        }

        // 빈 슬롯에 새로 추가
        foreach (var slot in _slots)
        {
            if (remaining <= 0) break;
            if (slot.IsEmpty)
            {
                int adding = Mathf.Min(maxStack, remaining);
                slot.Set(itemId, adding, markAsNew);
                remaining -= adding;
            }
        }

        int added = amount - remaining;
        if (added > 0)
        {
            OnInventoryChanged?.Invoke();

            // [획득 로그] Player 인벤에 실제로 들어온 분량만 통지. Storage(창고)는 제외.
            if (ownerType == InventoryOwnerType.Player)
                OnItemAddedToInventory?.Invoke(itemId, added);
        }

        return remaining;
    }

    // 루팅으로 아이템 획득 (NEW 뱃지 자동 설정)
    public int TryAddItemFromLoot(int itemId, int count)
    {
        return AddItem(itemId, count, markAsNew: true);
    }

    // 아이템 소비 시도
    public bool TryConsumeItem(int itemId, int amount)
    {
        if (GetTotalItemCount(itemId) < amount) return false;

        int remaining = amount;
        foreach (var slot in _slots)
        {
            if (remaining <= 0) break;
            if (slot.itemId == itemId && !slot.IsEmpty)
            {
                if (slot.amount <= remaining)
                {
                    remaining -= slot.amount;
                    slot.Clear();
                }
                else
                {
                    slot.amount -= remaining;
                    remaining = 0;
                }
            }
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    // 특정 슬롯에서 지정 수량 제거 (버리기)
    public bool RemoveFromSlot(int slotIndex, int amount)
    {
        var slot = GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) return false;
        if (slot.amount < amount) return false;

        slot.amount -= amount;
        if (slot.amount <= 0) slot.Clear();

        OnInventoryChanged?.Invoke();
        return true;
    }

    // 슬롯 전체를 다른 인벤토리로 이동
    public bool MoveSlot(int slotIndex, InventoryManager other)
    {
        var slot = GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) return false;

        int leftOver = other.AddItem(slot.itemId, slot.amount);
        int moved = slot.amount - leftOver;

        if (moved > 0)
        {
            slot.amount -= moved;
            if (slot.amount <= 0) slot.Clear();
            OnInventoryChanged?.Invoke();
            return true;
        }
        return false;
    }

    // 모든 슬롯을 다른 인벤토리로 이동 (전부 보관)
    public void MoveAllTo(InventoryManager other)
    {
        bool changed = false;
        foreach (var slot in _slots)
        {
            if (slot.IsEmpty) continue;
            int leftOver = other.AddItem(slot.itemId, slot.amount);
            int moved = slot.amount - leftOver;
            if (moved > 0)
            {
                slot.amount -= moved;
                if (slot.amount <= 0) slot.Clear();
                changed = true;
            }
        }
        if (changed) OnInventoryChanged?.Invoke();
    }

    // 스택 분할 후 대상 인벤토리에 추가
    // 스택 분할 후 대상 인벤토리에 추가
    // 같은 인벤토리로 분할 시 빈 슬롯을 직접 찾아서 추가
    public bool SplitStack(int slotIndex, int amount, InventoryManager target)
    {
        var slot = GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) return false;
        // 분할 후 최소 1개는 남아야 함
        if (slot.amount <= amount || amount <= 0) return false;

        // 같은 인벤토리 내 분할
        // AddItem 쓰면 원본 슬롯에 다시 추가되는 버그가 있어서 빈 슬롯 직접 탐색
        if (target == this)
        {
            InventorySlot emptySlot = null;
            foreach (var s in _slots)
            {
                if (s.IsEmpty)
                {
                    emptySlot = s;
                    break;
                }
            }

            if (emptySlot == null)
            {
                Debug.LogWarning("[InventoryManager] 분할 실패: 빈 슬롯 없음");
                return false;
            }

            emptySlot.Set(slot.itemId, amount);
            slot.amount -= amount;
            OnInventoryChanged?.Invoke();
            return true;
        }

        // 다른 인벤토리로 분할
        int leftOver = target.AddItem(slot.itemId, amount);
        int added = amount - leftOver;

        if (added > 0)
        {
            slot.amount -= added;
            if (slot.amount <= 0) slot.Clear();
            OnInventoryChanged?.Invoke();
            return true;
        }

        Debug.LogWarning("[InventoryManager] 분할 실패: 대상 인벤토리 가득 참");
        return false;
    }

    // 정렬 기준 열거형
    public enum SortType
    {
        Name = 0,   // 이름순
        Category = 1,   // 카테고리순
        Grade = 2,   // 등급순
        Amount = 3    // 수량순
    }

    // 정렬 실행 (SortBarUI 에서 호출)
    public void SortSlots(SortType sortType = SortType.Name, bool ascending = true)
    {
        // 아이템 있는 슬롯 데이터만 추출
        var filled = new System.Collections.Generic.List<(int id, int qty, bool isNew)>();
        foreach (var slot in _slots)
            if (!slot.IsEmpty)
                filled.Add((slot.itemId, slot.amount, slot.isNew));

        // 정렬 기준별 비교 함수
        filled.Sort((a, b) =>
        {
            int result = 0;

            switch (sortType)
            {
                case SortType.Name:
                    var dataA = ItemDatabase.GetItem(a.id);
                    var dataB = ItemDatabase.GetItem(b.id);
                    string nameA = dataA != null ? dataA.itemName : "";
                    string nameB = dataB != null ? dataB.itemName : "";
                    result = string.Compare(nameA, nameB,
                        System.StringComparison.CurrentCulture);
                    break;

                case SortType.Category:
                    var catA = ItemDatabase.GetItem(a.id);
                    var catB = ItemDatabase.GetItem(b.id);
                    int categoryA = catA != null ? (int)catA.itemCategory : 0;
                    int categoryB = catB != null ? (int)catB.itemCategory : 0;
                    result = categoryA.CompareTo(categoryB);
                    break;

                case SortType.Grade:
                    var gradeDataA = ItemDatabase.GetItem(a.id);
                    var gradeDataB = ItemDatabase.GetItem(b.id);
                    int gradeA = gradeDataA != null ? (int)gradeDataA.itemGrade : 0;
                    int gradeB = gradeDataB != null ? (int)gradeDataB.itemGrade : 0;
                    result = gradeA.CompareTo(gradeB);
                    break;

                case SortType.Amount:
                    result = a.qty.CompareTo(b.qty);
                    break;
            }

            // 내림차순이면 결과 반전
            return ascending ? result : -result;
        });

        // 슬롯 초기화 후 정렬된 순서로 재배치
        foreach (var slot in _slots) slot.Clear();
        for (int i = 0; i < filled.Count; i++)
            _slots[i].Set(filled[i].id, filled[i].qty, filled[i].isNew);

        OnInventoryChanged?.Invoke();
    }

    // 같은 인벤토리 내 두 슬롯 교환 (드래그앤드롭)
    // 같은 아이템이면 maxStack 까지 병합, 대상이 maxStack 이면 스왑
    public void SwapSlots(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;

        var from = GetSlot(fromIndex);
        var to = GetSlot(toIndex);
        if (from == null || to == null || from.IsEmpty) return;

        if (to.IsEmpty)
        {
            // 빈 슬롯으로 이동
            to.Set(from.itemId, from.amount, from.isNew);
            from.Clear();
        }
        else if (to.itemId == from.itemId)
        {
            // 같은 아이템: 병합 시도
            var data = ItemDatabase.GetItem(from.itemId);
            int maxStack = data != null ? data.maxStack : 999;

            if (to.amount >= maxStack)
            {
                // 대상이 이미 maxStack: 스왑
                int tempId = from.itemId;
                int tempAmount = from.amount;
                bool tempNew = from.isNew;
                from.Set(to.itemId, to.amount, to.isNew);
                to.Set(tempId, tempAmount, tempNew);
            }
            else
            {
                // maxStack 까지 채우고 초과분은 원본에 유지
                int canAdd = maxStack - to.amount;
                int adding = Mathf.Min(canAdd, from.amount);
                to.amount += adding;
                from.amount -= adding;
                if (from.amount <= 0) from.Clear();
            }
        }
        else
        {
            // 다른 아이템: 스왑
            int tempId = from.itemId;
            int tempAmount = from.amount;
            bool tempNew = from.isNew;
            from.Set(to.itemId, to.amount, to.isNew);
            to.Set(tempId, tempAmount, tempNew);
            if (from.itemId < 0) from.Clear();
        }

        OnInventoryChanged?.Invoke();
    }

    // 다른 인벤토리의 특정 슬롯으로 이동 (드래그앤드롭)
    // 같은 아이템이면 병합, 다른 아이템이면 스왑
    public void MoveSlotTo(int fromIndex, InventoryManager other, int toIndex)
    {
        var from = GetSlot(fromIndex);
        var to = other.GetSlot(toIndex);
        if (from == null || to == null || from.IsEmpty) return;

        if (to.IsEmpty)
        {
            // 빈 슬롯으로 이동
            to.Set(from.itemId, from.amount, from.isNew);
            from.Clear();
            other.OnInventoryChanged?.Invoke();
        }
        else if (to.itemId == from.itemId)
        {
            // 같은 아이템: 병합 시도
            var data = ItemDatabase.GetItem(from.itemId);
            int maxStack = data != null ? data.maxStack : 999;

            if (to.amount >= maxStack)
            {
                // 대상이 이미 maxStack: 스왑
                int tempId = from.itemId;
                int tempAmount = from.amount;
                bool tempNew = from.isNew;
                from.Set(to.itemId, to.amount, to.isNew);
                to.Set(tempId, tempAmount, tempNew);
            }
            else
            {
                // maxStack 까지 채우고 초과분은 원본에 유지
                int canAdd = maxStack - to.amount;
                int adding = Mathf.Min(canAdd, from.amount);
                to.amount += adding;
                from.amount -= adding;
                if (from.amount <= 0) from.Clear();
            }

            other.OnInventoryChanged?.Invoke();
        }
        else
        {
            // 다른 아이템: 스왑
            int tempId = from.itemId;
            int tempAmount = from.amount;
            bool tempNew = from.isNew;
            from.Set(to.itemId, to.amount, to.isNew);
            to.Set(tempId, tempAmount, tempNew);
            other.OnInventoryChanged?.Invoke();
        }

        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// ALT+드래그용: fromIndex 슬롯에서 amount개만 other의 toIndex 슬롯으로 이동.
    /// 빈 슬롯 → 이동, 같은 아이템 → 병합, 다른 아이템 → 분할 이동 불가(취소).
    /// </summary>
    public bool MoveAmountToSlot(int fromIndex, int amount, InventoryManager other, int toIndex)
    {
        var from = GetSlot(fromIndex);
        var to   = other.GetSlot(toIndex);
        if (from == null || to == null || from.IsEmpty) return false;
        if (amount <= 0 || amount > from.amount) return false;

        if (to.IsEmpty)
        {
            to.Set(from.itemId, amount, from.isNew);
            from.amount -= amount;
            if (from.amount <= 0) from.Clear();
        }
        else if (to.itemId == from.itemId)
        {
            var data     = ItemDatabase.GetItem(from.itemId);
            int maxStack = data != null ? data.maxStack : 999;
            int canAdd   = maxStack - to.amount;
            int adding   = Mathf.Min(canAdd, amount);
            if (adding <= 0) return false;

            to.amount   += adding;
            from.amount -= adding;
            if (from.amount <= 0) from.Clear();
        }
        else
        {
            // 다른 아이템 슬롯에는 분할 이동 불가
            return false;
        }

        OnInventoryChanged?.Invoke();
        if (other != this) other.OnInventoryChanged?.Invoke();
        return true;
    }

    // 필터에 해당하는 아이템만 다른 인벤토리로 이동
    // null 이면 전체 이동
    public void MoveFilteredTo(InventoryManager other, ItemCategory? filter)
    {
        bool changed = false;
        foreach (var slot in _slots)
        {
            if (slot.IsEmpty) continue;

            // 필터 체크
            if (filter != null)
            {
                var data = ItemDatabase.GetItem(slot.itemId);
                if (data == null || data.itemCategory != filter.Value) continue;
            }

            int leftOver = other.AddItem(slot.itemId, slot.amount);
            int moved = slot.amount - leftOver;
            if (moved > 0)
            {
                slot.amount -= moved;
                if (slot.amount <= 0) slot.Clear();
                changed = true;
            }
        }

        if (changed)
        {
            OnInventoryChanged?.Invoke();
            other.OnInventoryChanged?.Invoke();
        }
    }

    // 창고 정리: 필터에 해당하는 아이템을 병합 후 등급/ID/수량 순 정렬
    // null 이면 전체 정리
    public void OrganizeFiltered(ItemCategory? filter)
    {
        // 정리 대상 슬롯 인덱스 수집
        var targetIndices = new List<int>();
        var itemTotals = new System.Collections.Generic.Dictionary<int, int>();

        foreach (var slot in _slots)
        {
            if (slot.IsEmpty) continue;

            if (filter != null)
            {
                var data = ItemDatabase.GetItem(slot.itemId);
                if (data == null || data.itemCategory != filter.Value) continue;
            }

            targetIndices.Add(slot.slotIndex);

            // 아이템별 총 수량 집계 (병합용)
            if (!itemTotals.ContainsKey(slot.itemId))
                itemTotals[slot.itemId] = 0;
            itemTotals[slot.itemId] += slot.amount;
        }

        if (targetIndices.Count == 0) return;

        // maxStack 기준으로 슬롯 목록 생성 (병합 결과)
        var merged = new List<(int itemId, int amount)>();
        foreach (var kvp in itemTotals)
        {
            int id = kvp.Key;
            int remaining = kvp.Value;
            var data = ItemDatabase.GetItem(id);
            int maxStack = data != null ? data.maxStack : 999;

            while (remaining > 0)
            {
                int take = Mathf.Min(maxStack, remaining);
                merged.Add((id, take));
                remaining -= take;
            }
        }

        // 정렬: itemGrade 오름차순 -> itemId 오름차순 -> amount 내림차순
        merged.Sort((a, b) =>
        {
            var dataA = ItemDatabase.GetItem(a.itemId);
            var dataB = ItemDatabase.GetItem(b.itemId);
            int gradeA = dataA != null ? (int)dataA.itemGrade : 99;
            int gradeB = dataB != null ? (int)dataB.itemGrade : 99;

            if (gradeA != gradeB) return gradeA.CompareTo(gradeB);
            if (a.itemId != b.itemId) return a.itemId.CompareTo(b.itemId);
            return b.amount.CompareTo(a.amount);
        });

        // filter==null(가방=전체 정리): 전부 비우고 0번부터 채워 빈칸 압축(기대 동작).
        // filter 있으면(창고 카테고리 정리) 해당 슬롯들 안에서만 정렬(다른 아이템은 그대로).
        if (filter == null)
        {
            foreach (var s in _slots) s.Clear();
            for (int i = 0; i < merged.Count; i++)
                _slots[i].Set(merged[i].itemId, merged[i].amount);
        }
        else
        {
            for (int i = 0; i < targetIndices.Count; i++)
            {
                var slot = GetSlot(targetIndices[i]);
                if (slot == null) continue;

                if (i < merged.Count)
                    slot.Set(merged[i].itemId, merged[i].amount);
                else
                    slot.Clear();
            }
        }

        OnInventoryChanged?.Invoke();
    }

    // NEW 뱃지 해제
    public void ClearNewFlag(int slotIndex)
    {
        var slot = GetSlot(slotIndex);
        if (slot != null) slot.isNew = false;
    }

    // UI 강제 갱신
    public void ForceRefreshUI()
    {
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// 모든 아이템을 꺼내고 인벤토리를 비운 뒤 아이템 목록을 반환
    /// 사망 시 아이템 드롭에 사용
    /// </summary>
    public List<(int itemId, int amount)> TakeAll()
    {
        var result = new List<(int, int)>();
        foreach (var slot in _slots)
        {
            if (!slot.IsEmpty)
            {
                result.Add((slot.itemId, slot.amount));
                slot.Clear();
            }
        }
        if (result.Count > 0)
            OnInventoryChanged?.Invoke();
        return result;
    }
}