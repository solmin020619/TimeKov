using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyDropOnDeath : MonoBehaviour
{
    [Tooltip("DropTable의 sourceId — 이 몬스터의 드롭 출처 ID (예: MeleeBot_Ghoul)")]
    [SerializeField] private string sourceId;

    [Tooltip("스폰할 박스 프리팹 (LootBox 컴포넌트 포함)")]
    [SerializeField] private GameObject boxPrefab;

    [SerializeField] private float spawnHeightOffset = 0.5f;

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
        if (boxPrefab == null) return;

        List<(int itemId, int count)> contents = Roll();
        if (contents.Count == 0) return;

        Vector3 pos = transform.position + Vector3.up * spawnHeightOffset;
        GameObject go = Instantiate(boxPrefab, pos, Quaternion.identity);

        LootBox box = go.GetComponent<LootBox>();
        if (box != null) box.Initialize(contents);
    }

    private List<(int itemId, int count)> Roll()
    {
        var result = new List<(int itemId, int count)>();
        if (string.IsNullOrEmpty(sourceId)) return result;

        var pool = new List<DropTableSheetData>();
        foreach (var row in GameDataHolder.I.DropTable.All)
        {
            if (row.sourceType == SourceType.Monster && row.sourceId == sourceId)
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
