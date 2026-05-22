using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyDropOnDeath : MonoBehaviour
{
    [Tooltip("DropTable의 sourceId — 이 몬스터의 드롭 출처 ID (예: MeleeBot_Ghoul)")]
    [SerializeField] private string sourceId;

    [Tooltip("스폰할 박스 프리팹 (LootBox 컴포넌트 포함)")]
    [SerializeField] private GameObject boxPrefab;

    /// <summary>RespawnManager 등 외부에서 같은 프리팹을 재사용할 때 참조</summary>
    public GameObject BoxPrefab => boxPrefab;

    [SerializeField] private float spawnHeightOffset = 0.5f;

    [Tooltip("박스가 여러 개일 때 죽은 자리 주변에 흩어지는 반경 (m)")]
    [SerializeField] private float scatterRadius = 1f;

    private EnemyHealth _health;

    void Awake()
    {
        _health = GetComponent<EnemyHealth>();
    }

    void OnEnable()
    {
        if (_health != null) _health.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (_health != null) _health.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        if (boxPrefab == null)
        {
            Debug.LogWarning("[Drop] boxPrefab이 비어 있음");
            return;
        }

        List<(int itemId, int count)> contents = Roll();
        Debug.Log($"[Drop] sourceId='{sourceId}' → 굴린 아이템 {contents.Count}개 → 박스 {contents.Count}개 스폰");
        if (contents.Count == 0) return;

        // 아이템 1개 = 박스 1개. 죽은 자리 주변에 흩어 놓는다.
        for (int i = 0; i < contents.Count; i++)
        {
            Vector2 off = Random.insideUnitCircle * scatterRadius;
            Vector3 pos = transform.position
                          + Vector3.up * spawnHeightOffset
                          + new Vector3(off.x, 0f, off.y);

            GameObject go = Instantiate(boxPrefab, pos, Quaternion.identity);

            LootBox box = go.GetComponentInChildren<LootBox>();
            if (box != null)
                box.Initialize(new List<(int itemId, int count)> { contents[i] });
            else
                Debug.LogWarning("[Drop] 스폰한 박스에서 LootBox를 못 찾음");
        }
    }

    private List<(int itemId, int count)> Roll()
    {
        var result = new List<(int itemId, int count)>();

        string myId = sourceId != null ? sourceId.Trim() : "";
        if (myId.Length == 0) return result;

        var pool = new List<DropTableSheetData>();
        foreach (var row in GameDataHolder.I.DropTable.All)
        {
            string rowId = row.sourceId != null ? row.sourceId.Trim() : "";
            if (row.sourceType == SourceType.Monster && rowId == myId)
                pool.Add(row);
        }
        if (pool.Count == 0) return result;

        int pickCount = Mathf.Max(1, pool[0].pickCount);

        for (int p = 0; p < pickCount && pool.Count > 0; p++)
        {
            DropTableSheetData picked = WeightedPick(pool);
            pool.Remove(picked);

            int itemId = ExtractItemId(picked.SheetId);
            if (itemId <= 0) continue;

            int count = Random.Range(picked.minCount, picked.maxCount + 1);
            if (count > 0) result.Add((itemId, count));
        }

        return result;
    }

    private DropTableSheetData WeightedPick(List<DropTableSheetData> pool)
    {
        int total = 0;
        for (int i = 0; i < pool.Count; i++)
            total += Mathf.Max(0, pool[i].dropWeight);

        if (total <= 0) return pool[0];

        int r = Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            acc += Mathf.Max(0, pool[i].dropWeight);
            if (r < acc) return pool[i];
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
