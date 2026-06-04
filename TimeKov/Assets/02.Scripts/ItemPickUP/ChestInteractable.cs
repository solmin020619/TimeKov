// =====================================================================
// ChestInteractable.cs
// 씬에 배치하는 파밍 상자
// F키 상호작용 → DropTable(Chest) 롤 → 인벤토리 즉시 지급 → ChestOpenUI 팝업
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public class ChestInteractable : MonoBehaviour, IInteractable
{
    [Header("드롭 설정")]
    [Tooltip("DropTable의 sourceId (예: LC_LOOT). sourceType=Chest 행과 매칭됨")]
    [SerializeField] private string sourceId = "LC_LOOT";

    [Header("비주얼 (선택)")]
    [Tooltip("상자 열리기 전 모델/오브젝트")]
    [SerializeField] private GameObject closedVisual;
    [Tooltip("상자 열린 후 모델/오브젝트 (없으면 closedVisual 유지)")]
    [SerializeField] private GameObject openedVisual;

    [Header("인터랙션")]
    [Tooltip("기지 결계 밖에서도 열 수 있는지 여부")]
    [SerializeField] private bool requireBase = false;

    // ── 상태 ──────────────────────────────────────────────────────────
    // 인벤토리 UI가 실제로 열려있는 동안만 차단 (플래그 stuck 방지)
    public bool CanInteract
    {
        get
        {
            var inv = InventoryUIController.Instance;
            if (inv == null) return true;
            return !inv.IsOpen;
        }
    }

    // ── IInteractable ──────────────────────────────────────────────────

    public void Interact(Player player)
    {
        if (player == null) return;

        // IsChestOpen이 stuck된 경우 안전 리셋
        if (InventoryUIController.IsChestOpen)
        {
            InventoryUIController.IsChestOpen = false;
            InventoryManager.ChestInstance?.ClearAllItems();
        }

        if (requireBase && !player.Stat.IsInBase)
        {
            Debug.Log("[Chest] 기지 내부에서만 열 수 있습니다.");
            return;
        }

        // ① 롤: DropTable(Chest) → 아이템 목록 생성
        List<(int itemId, int count)> items = Roll();

        if (items.Count == 0)
            Debug.LogWarning($"[Chest] sourceId='{sourceId}' — DropTable에 Chest 항목이 없거나 아이템 없음");

        // ② 상자 인벤토리 초기화 후 아이템 채우기
        var chestInv = InventoryManager.ChestInstance;
        if (chestInv != null)
        {
            chestInv.ClearAllItems();
            foreach (var (itemId, count) in items)
                chestInv.AddItem(itemId, count);
        }
        else
        {
            Debug.LogWarning("[Chest] ChestInstance 없음 — 씬에 Chest InventoryManager 오브젝트가 필요합니다.");
        }

        // ③ 창고 스타일로 인벤토리 UI 열기 (상자 패널 + 가방 패널)
        InventoryUIController.IsChestOpen = true;
        InventoryUIController.Instance?.Open();

        // ④ 비주얼 변경 (오픈 시에만, 닫을 때는 InventoryUIController에서 처리)
        if (closedVisual != null) closedVisual.SetActive(false);
        if (openedVisual != null) openedVisual.SetActive(true);
    }

    // ── 드롭 롤 ───────────────────────────────────────────────────────

    private List<(int itemId, int count)> Roll()
    {
        var result  = new List<(int itemId, int count)>();
        string myId = (sourceId ?? "").Trim();
        if (myId.Length == 0) return result;

        // DropTable에서 Chest + sourceId 매칭 행 수집
        var pool = new List<DropTableSheetData>();
        foreach (var row in GameDataHolder.I.DropTable.All)
        {
            string rowId = (row.sourceId ?? "").Trim();
            if (row.sourceType == SourceType.Chest && rowId == myId)
                pool.Add(row);
        }
        if (pool.Count == 0) return result;

        int pickCount = Mathf.Max(1, pool[0].pickCount);
        var available = new List<DropTableSheetData>(pool);

        for (int p = 0; p < pickCount && available.Count > 0; p++)
        {
            DropTableSheetData picked = WeightedPick(available);
            available.Remove(picked);

            int itemId = ExtractItemId(picked.SheetId);
            if (itemId <= 0)
            {
                Debug.LogWarning($"[Chest] itemId 추출 실패 — SheetId='{picked.SheetId}'. DropTable의 itemId 컬럼 값 확인 필요");
                continue;
            }

            // ItemData에 존재하는지 검증
            if (GameDataUtility.GetItem(itemId) == null)
            {
                Debug.LogWarning($"[Chest] itemId={itemId} — ItemData에 없는 아이템. 시트 데이터 확인 필요");
                continue;
            }

            int count = Random.Range(picked.minCount, picked.maxCount + 1);
            if (count > 0)
            {
                result.Add((itemId, count));
                Debug.Log($"[Chest] 롤됨: itemId={itemId} x{count}");
            }
        }

        Debug.Log($"[Chest] sourceId='{sourceId}' → 총 {result.Count}종 아이템 롤 완료");
        return result;
    }

    private DropTableSheetData WeightedPick(List<DropTableSheetData> pool)
    {
        int total = 0;
        foreach (var r in pool) total += Mathf.Max(0, r.dropWeight);
        if (total <= 0) return pool[0];

        int rand = Random.Range(0, total);
        int acc  = 0;
        foreach (var r in pool)
        {
            acc += Mathf.Max(0, r.dropWeight);
            if (rand < acc) return r;
        }
        return pool[pool.Count - 1];
    }

    // SheetId 복합키 "dropId_itemId" 에서 itemId 추출
    private int ExtractItemId(DropTableSheetId sheetId)
    {
        string s = sheetId;
        int u = s.LastIndexOf('_');
        if (u < 0 || u + 1 >= s.Length) return 0;
        return int.TryParse(s.Substring(u + 1), out int id) ? id : 0;
    }
}
