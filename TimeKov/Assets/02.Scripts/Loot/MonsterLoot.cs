using System.Collections.Generic;
using UnityEngine;

public class MonsterLoot : MonoBehaviour
{
    [Header("Drop Key")]
    // CSV의 MonsterType 컬럼 값 (예: Chaser, Shooter_Pistol, Shooter, Exploder 등)
    public string monsterType = "Chaser";

    [Tooltip("여긴 더 이상 C_T1 같은 고정값을 박는 용도가 아님. " +
             "원하면 비워둬도 되고, 디버그용으로 마지막 선택된 tableId가 들어감.")]
    public string tableId = "";

    [Header("Tier Roll (T1~T5) - 맵 1개 고정 확률")]
    [Tooltip("맵 1개 기준. 값은 가중치(%)든 뭐든 상관없음. 비율만 사용됨.")]
    public float T1 = 50f;
    public float T2 = 35f;
    public float T3 = 15f;
    public float T4 = 0f;
    public float T5 = 0f;

    [Tooltip("T4/T5까지 확장하려면 5로 두면 됨.")]
    [Range(1, 5)] public int maxTier = 5;

    [Header("Roll Count (Bonus용)")]
    public int minRoll = 1;
    public int maxRoll = 2;

    [Header("DB")]
    public MonsterDropDatabase dropDb;
    public ItemDataBase itemDb;

    [Header("UI")]
    public GameObject lootPanelRoot;         // Canvas/Drop
    public GetItem[] lootSlots;              // Drop 패널 오른쪽 슬롯들
    public GameObject playerInventoryManagerGO;

    [Header("BG식(빈 슬롯 숨김/자동 정렬)")]
    [Tooltip("인벤토리처럼: 아이템 있는 슬롯만 보이게 + 아이템 빠지면 아래 아이템이 위로 당겨짐")]
    public bool enableBGStyleLoot = true;

    [Tooltip("루팅창이 열려있는 동안 슬롯 상태를 주기적으로 감지해서 자동 정렬/숨김 처리")]
    public float bgSyncInterval = 0.1f;

    private float _nextBgSyncTime = 0f;
    private int _lastSnapshotHash = 0;

    private bool _rolled = false;
    private int _rolledTier = -1;            // 1~5
    private string _rolledTableId = "";      // 예: C_T3

    public void Open()
    {
        EnsureRefs();

        if (lootPanelRoot == null)
        {
            Debug.LogError("[MonsterLoot] lootPanelRoot(Drop) 못 찾음. (Hierarchy에서 Canvas/Drop 이름 확인)");
            return;
        }

        if (UIStateManager.Instance != null)
            UIStateManager.Instance.ToggleLoot(lootPanelRoot);
        else
            lootPanelRoot.SetActive(true);

        if (UIStateManager.Instance != null &&
            UIStateManager.Instance.GetCurrentState() != UIStateManager.UIState.Loot)
            return;

        if (dropDb == null)
        {
            Debug.LogError("[MonsterLoot] dropDb 연결 안됨", gameObject);
            return;
        }

        if (lootSlots == null || lootSlots.Length == 0)
        {
            Debug.LogError("[MonsterLoot] lootSlots 비어있음. Drop 패널 하위에 GetItem 슬롯이 있어야 함");
            return;
        }

        if (playerInventoryManagerGO == null)
        {
            Debug.LogError("[MonsterLoot] playerInventoryManagerGO 못 찾음. 씬에 InventoryManager가 있어야 함");
            return;
        }

        if (!_rolled)
        {
            _rolledTier = RollTier();
            _rolledTableId = BuildTableId(monsterType, _rolledTier);
            tableId = _rolledTableId;

            RollAndFill(_rolledTableId);
            _rolled = true;

            Debug.Log($"[MonsterLoot] Rolled Tier=T{_rolledTier}, tableId={_rolledTableId} (monsterType={monsterType})");
        }
        else
        {
            Debug.Log($"[MonsterLoot] Re-open fixed loot 유지 (Tier=T{_rolledTier}, tableId={_rolledTableId})");
        }

        if (enableBGStyleLoot)
        {
            ApplyBGStyleVisibility();
            CompactAndRefreshIfNeeded(force: true);
        }
    }

    private void RollAndFill(string resolvedTableId)
    {
        EnsureRefs();
        ClearSlots();

        int guaranteedSlot = GetNextValidSlotIndex(0);
        if (guaranteedSlot == -1)
        {
            Debug.LogWarning("[MonsterLoot] 유효한 GetItem 슬롯이 없음");
            return;
        }

        FillOne(resolvedTableId, MonsterDropType.Guaranteed, guaranteedSlot);

        int rollCount = Random.Range(minRoll, maxRoll + 1);
        int filled = 0;

        for (int i = 0; i < lootSlots.Length && filled < rollCount; i++)
        {
            if (i == guaranteedSlot) continue;
            if (lootSlots[i] == null) continue;

            if (FillOne(resolvedTableId, MonsterDropType.Bonus, i))
                filled++;
        }

        if (enableBGStyleLoot)
        {
            ApplyBGStyleVisibility();
            CompactAndRefreshIfNeeded(force: true);
        }
    }

    private bool FillOne(string resolvedTableId, MonsterDropType type, int slotIndex)
    {
        EnsureRefs();

        if (dropDb == null) return false;
        if (lootSlots == null || lootSlots.Length == 0) return false;
        if (slotIndex < 0 || slotIndex >= lootSlots.Length) return false;
        if (lootSlots[slotIndex] == null) return false;
        if (playerInventoryManagerGO == null) return false;

        var list = dropDb.Get(monsterType, resolvedTableId, type);
        if (list == null || list.Count == 0)
        {
            Debug.LogWarning($"[MonsterLoot] Drop list empty: {monsterType}/{resolvedTableId}/{type}");
            return false;
        }

        var picked = PickByWeight(list);
        if (picked == null) return false;

        if (itemDb != null && itemDb.GetItemById(picked.ItemID) == null)
        {
            Debug.LogWarning($"[MonsterLoot] ItemDB에 없는 ItemID={picked.ItemID} 스킵");
            return true;
        }

        int count = 1;
        lootSlots[slotIndex].SetData(playerInventoryManagerGO, picked.ItemID, count);
        return true;
    }

    private int RollTier()
    {
        int cap = Mathf.Clamp(maxTier, 1, 5);

        float w1 = (cap >= 1) ? Mathf.Max(0f, T1) : 0f;
        float w2 = (cap >= 2) ? Mathf.Max(0f, T2) : 0f;
        float w3 = (cap >= 3) ? Mathf.Max(0f, T3) : 0f;
        float w4 = (cap >= 4) ? Mathf.Max(0f, T4) : 0f;
        float w5 = (cap >= 5) ? Mathf.Max(0f, T5) : 0f;

        float sum = w1 + w2 + w3 + w4 + w5;
        if (sum <= 0f) return 1;

        float r = Random.value * sum;
        float acc = 0f;

        acc += w1; if (r <= acc) return 1;
        acc += w2; if (r <= acc) return 2;
        acc += w3; if (r <= acc) return 3;
        acc += w4; if (r <= acc) return 4;
        return 5;
    }

    private string BuildTableId(string mType, int tier)
    {
        string prefix = GetPrefixFromMonsterType(mType);
        return $"{prefix}_T{tier}";
    }

    private string GetPrefixFromMonsterType(string mType)
    {
        switch (mType)
        {
            case "Chaser": return "C";
            case "Shooter_Pistol": return "SP";
            case "Shooter_Shotgun": return "SS";
            case "Shooter_Rifle": return "SR";
            case "Exploder": return "E";
        }

        if (mType == "Shooter") return "SR";
        if (mType.Contains("Pistol")) return "SP";
        if (mType.Contains("Shotgun")) return "SS";
        if (mType.Contains("Rifle")) return "SR";

        return "C";
    }

    private void EnsureRefs()
    {
        // 1) Drop 패널(비활성 포함) 찾기
        if (lootPanelRoot == null)
            lootPanelRoot = FindSceneObjectEvenIfInactive("Drop");

        // 2) InventoryManager 찾기 (비활성 포함)
        // ✅ 핵심 수정: "InventoryManager 타입이면 아무거나"가 아니라
        //   - 실제 타입이 정확히 InventoryManager인 컴포넌트만 (서브클래스/다른 매니저 제외)
        //   - 가능하면 GameObject 이름이 "InventoryManager"인 걸 우선
        if (playerInventoryManagerGO == null)
        {
            InventoryManager picked = null;
            var all = Resources.FindObjectsOfTypeAll<InventoryManager>();
            for (int i = 0; i < all.Length; i++)
            {
                var inv = all[i];
                if (inv == null) continue;
                if (!inv.gameObject.scene.IsValid()) continue; // 에셋/프리팹 제외

                // 서브클래스(예: WarehouseManager : InventoryManager)면 제외
                if (inv.GetType() != typeof(InventoryManager)) continue;

                if (inv.gameObject.name == "InventoryManager")
                {
                    picked = inv;
                    break;
                }

                if (picked == null) picked = inv;
            }

            if (picked != null)
                playerInventoryManagerGO = picked.gameObject;
        }

        // 3) GetItem 슬롯 찾기 (길이는 있는데 요소가 None인 케이스 포함 재빌드)
        bool needRebuildSlots = (lootSlots == null || lootSlots.Length == 0);
        if (!needRebuildSlots)
        {
            for (int i = 0; i < lootSlots.Length; i++)
            {
                if (lootSlots[i] == null) { needRebuildSlots = true; break; }
            }
        }

        if (needRebuildSlots && lootPanelRoot != null)
        {
            lootSlots = lootPanelRoot.GetComponentsInChildren<GetItem>(true);
        }
    }

    private int GetNextValidSlotIndex(int startIndex)
    {
        if (lootSlots == null) return -1;
        for (int i = Mathf.Max(0, startIndex); i < lootSlots.Length; i++)
        {
            if (lootSlots[i] != null) return i;
        }
        return -1;
    }

    private void ClearSlots()
    {
        if (lootSlots == null) return;
        for (int i = 0; i < lootSlots.Length; i++)
        {
            if (lootSlots[i] != null && playerInventoryManagerGO != null)
                lootSlots[i].SetData(playerInventoryManagerGO, 0, 0);
        }

        if (enableBGStyleLoot)
        {
            ApplyBGStyleVisibility();
            _lastSnapshotHash = 0;
        }
    }

    private void Update()
    {
        if (!enableBGStyleLoot) return;
        if (lootPanelRoot == null) return;
        if (!lootPanelRoot.activeInHierarchy) return;
        if (Time.unscaledTime < _nextBgSyncTime) return;

        _nextBgSyncTime = Time.unscaledTime + Mathf.Max(0.02f, bgSyncInterval);
        CompactAndRefreshIfNeeded(force: false);
    }

    private void CompactAndRefreshIfNeeded(bool force)
    {
        if (lootSlots == null || lootSlots.Length == 0) return;
        if (playerInventoryManagerGO == null) return;

        int snapshotHash = ComputeSnapshotHash();
        if (!force && snapshotHash == _lastSnapshotHash) return;
        _lastSnapshotHash = snapshotHash;

        var items = new List<(int id, int count)>(lootSlots.Length);
        for (int i = 0; i < lootSlots.Length; i++)
        {
            var slot = lootSlots[i];
            if (slot == null) continue;

            int id = ReadIntField(slot, "InsertID", "insertID", "itemId", "slotIndex");
            int count = ReadIntField(slot, "InsertItemCount", "insertItemCount", "count", "itemCount");

            if (id != 0 && count > 0)
                items.Add((id, count));
        }

        for (int i = 0; i < lootSlots.Length; i++)
        {
            if (lootSlots[i] == null) continue;
            if (i < items.Count)
                lootSlots[i].SetData(playerInventoryManagerGO, items[i].id, items[i].count);
            else
                lootSlots[i].SetData(playerInventoryManagerGO, 0, 0);
        }

        ApplyBGStyleVisibility();
    }

    private void ApplyBGStyleVisibility()
    {
        if (lootSlots == null) return;
        for (int i = 0; i < lootSlots.Length; i++)
        {
            var slot = lootSlots[i];
            if (slot == null) continue;

            int id = ReadIntField(slot, "InsertID", "insertID", "itemId", "slotIndex");
            int count = ReadIntField(slot, "InsertItemCount", "insertItemCount", "count", "itemCount");

            bool shouldShow = (id != 0 && count > 0);
            if (slot.gameObject.activeSelf != shouldShow)
                slot.gameObject.SetActive(shouldShow);
        }
    }

    private int ComputeSnapshotHash()
    {
        unchecked
        {
            int hash = 17;
            if (lootSlots == null) return hash;

            for (int i = 0; i < lootSlots.Length; i++)
            {
                var slot = lootSlots[i];
                if (slot == null) continue;

                int id = ReadIntField(slot, "InsertID", "insertID", "itemId", "slotIndex");
                int count = ReadIntField(slot, "InsertItemCount", "insertItemCount", "count", "itemCount");

                hash = hash * 31 + id;
                hash = hash * 31 + count;
            }
            return hash;
        }
    }

    private int ReadIntField(object obj, params string[] fieldNames)
    {
        if (obj == null) return 0;
        var type = obj.GetType();

        for (int i = 0; i < fieldNames.Length; i++)
        {
            var f = type.GetField(fieldNames[i]);
            if (f != null && f.FieldType == typeof(int))
                return (int)f.GetValue(obj);

            var p = type.GetProperty(fieldNames[i]);
            if (p != null && p.PropertyType == typeof(int) && p.CanRead)
                return (int)p.GetValue(obj, null);
        }
        return 0;
    }

    private GameObject FindSceneObjectEvenIfInactive(string targetName)
    {
        var all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (t.name == targetName) return t.gameObject;
        }
        return null;
    }

    private MonsterDropRow PickByWeight(List<MonsterDropRow> entries)
    {
        float sum = 0f;
        for (int i = 0; i < entries.Count; i++)
            sum += Mathf.Max(0f, entries[i].Weight);

        if (sum <= 0f) return null;

        float r = Random.value * sum;
        float acc = 0f;

        for (int i = 0; i < entries.Count; i++)
        {
            acc += Mathf.Max(0f, entries[i].Weight);
            if (r <= acc) return entries[i];
        }
        return entries[entries.Count - 1];
    }
}