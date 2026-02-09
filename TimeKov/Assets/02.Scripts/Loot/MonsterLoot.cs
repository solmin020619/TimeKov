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

        // Loot 상태가 아니면 중단(토글 실패 등)
        if (UIStateManager.Instance != null &&
            UIStateManager.Instance.GetCurrentState() != UIStateManager.UIState.Loot)
            return;

        if (dropDb == null)
        {
            Debug.LogError("[MonsterLoot] dropDb 연결 안됨", gameObject);
            return;
        }

        // 슬롯/인벤 연결 체크
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
            // ✅ 1단계: Tier 먼저 뽑고 tableId 만들기
            _rolledTier = RollTier();
            _rolledTableId = BuildTableId(monsterType, _rolledTier);
            tableId = _rolledTableId; // 인스펙터에서도 보이게

            RollAndFill(_rolledTableId);
            _rolled = true;

            Debug.Log($"[MonsterLoot] Rolled Tier=T{_rolledTier}, tableId={_rolledTableId} (monsterType={monsterType})");
        }
        else
        {
            // ✅ 재오픈해도 동일 테이블/동일 아이템 유지
            Debug.Log($"[MonsterLoot] Re-open fixed loot 유지 (Tier=T{_rolledTier}, tableId={_rolledTableId})");
        }
    }

    private void RollAndFill(string resolvedTableId)
    {
        EnsureRefs();
        ClearSlots();

        // Guaranteed: 첫 유효 슬롯에 1개
        int guaranteedSlot = GetNextValidSlotIndex(0);
        if (guaranteedSlot == -1)
        {
            Debug.LogWarning("[MonsterLoot] 유효한 GetItem 슬롯이 없음");
            return;
        }

        FillOne(resolvedTableId, MonsterDropType.Guaranteed, guaranteedSlot);

        // Bonus: min~max회
        int rollCount = Random.Range(minRoll, maxRoll + 1);
        int filled = 0;

        for (int i = 0; i < lootSlots.Length && filled < rollCount; i++)
        {
            if (i == guaranteedSlot) continue;
            if (lootSlots[i] == null) continue;

            if (FillOne(resolvedTableId, MonsterDropType.Bonus, i))
                filled++;
        }
    }

    private bool FillOne(string resolvedTableId, MonsterDropType type, int slotIndex)
    {
        EnsureRefs();

        // Null 가드 (NRE 방지)
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

        // ItemDB 체크 (없으면 스킵)
        if (itemDb != null && itemDb.GetItemById(picked.ItemID) == null)
        {
            Debug.LogWarning($"[MonsterLoot] ItemDB에 없는 ItemID={picked.ItemID} 스킵");
            return true;
        }

        int count = 1;
        lootSlots[slotIndex].SetData(playerInventoryManagerGO, picked.ItemID, count);
        return true;
    }

    // -------------------------
    // Tier Roll
    // -------------------------
    private int RollTier()
    {
        // maxTier 제한 반영 (예: 3이면 T1~T3만)
        int cap = Mathf.Clamp(maxTier, 1, 5);

        float w1 = (cap >= 1) ? Mathf.Max(0f, T1) : 0f;
        float w2 = (cap >= 2) ? Mathf.Max(0f, T2) : 0f;
        float w3 = (cap >= 3) ? Mathf.Max(0f, T3) : 0f;
        float w4 = (cap >= 4) ? Mathf.Max(0f, T4) : 0f;
        float w5 = (cap >= 5) ? Mathf.Max(0f, T5) : 0f;

        float sum = w1 + w2 + w3 + w4 + w5;
        if (sum <= 0f)
        {
            // 전부 0이면 안전하게 T1 고정
            return 1;
        }

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

    // MonsterType -> Table Prefix 매핑 (시트 오른쪽 설명 그대로)
    private string GetPrefixFromMonsterType(string mType)
    {
        // 정확히 일치하는 케이스들
        switch (mType)
        {
            case "Chaser": return "C";
            case "Shooter_Pistol": return "SP";
            case "Shooter_Shotgun": return "SS";
            case "Shooter_Rifle": return "SR";
            case "Exploder": return "E";
        }

        // 혹시 너희가 Shooter로 뭉쳐 쓰는 경우 대비
        if (mType == "Shooter") return "SR"; // 기본값(원하면 바꿔)
        if (mType.Contains("Pistol")) return "SP";
        if (mType.Contains("Shotgun")) return "SS";
        if (mType.Contains("Rifle")) return "SR";

        // 마지막 fallback: CSV에 맞춰 직접 쓰는 경우를 위해 그냥 mType 첫 글자 사용은 위험해서 C로 둠
        return "C";
    }

    // -------------------------
    // Slots / UI refs
    // -------------------------
    private void EnsureRefs()
    {
        // 1) Drop 패널(비활성 포함) 찾기
        if (lootPanelRoot == null)
            lootPanelRoot = FindSceneObjectEvenIfInactive("Drop");

        // 2) InventoryManager 찾기 (비활성 포함)
        if (playerInventoryManagerGO == null)
        {
            var inv = FindObjectOfType<InventoryManager>(true);
            if (inv != null) playerInventoryManagerGO = inv.gameObject;
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
    }

    private GameObject FindSceneObjectEvenIfInactive(string targetName)
    {
        // GameObject.Find는 비활성 못 찾음 -> Resources로 씬 오브젝트 전체 탐색
        var all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue; // 씬 오브젝트만
            if (t.name == targetName) return t.gameObject;
        }
        return null;
    }

    // -------------------------
    // Weight Pick
    // -------------------------
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
