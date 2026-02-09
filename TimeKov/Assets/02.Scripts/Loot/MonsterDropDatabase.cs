using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum MonsterDropType
{
    Guaranteed,
    Bonus,
    Weapon
}

[Serializable]
public class MonsterDropRow
{
    public string MonsterType;      // Shooter_Pistol 등
    public string TableID;          // SP_T3 등
    public MonsterDropType DropType;// Guaranteed/Bonus/Weapon
    public int ItemID;              // ItemDatabase에 존재하는 ID
    public float Weight;            // CSV Probability(%) -> 가중치로 사용
}

[CreateAssetMenu(fileName = "MonsterDropDatabase", menuName = "Game Data/Monster Drop Database")]
public class MonsterDropDatabase : ScriptableObject
{
    [Header("CSV 파일 (TextAsset)")]
    public TextAsset csv;

    [Header("파싱 결과")]
    public List<MonsterDropRow> rows = new List<MonsterDropRow>();

    // 빠른 조회 캐시
    private Dictionary<(string monsterType, string tableId, MonsterDropType dropType), List<MonsterDropRow>> _cache;

    public void BuildCache()
    {
        _cache = new Dictionary<(string, string, MonsterDropType), List<MonsterDropRow>>();

        foreach (var r in rows)
        {
            var key = (r.MonsterType, r.TableID, r.DropType);
            if (!_cache.TryGetValue(key, out var list))
            {
                list = new List<MonsterDropRow>();
                _cache[key] = list;
            }
            list.Add(r);
        }
    }

    public List<MonsterDropRow> Get(string monsterType, string tableId, MonsterDropType dropType)
    {
        if (_cache == null) BuildCache();
        var key = (monsterType, tableId, dropType);
        return _cache.TryGetValue(key, out var list) ? list : null;
    }

    [ContextMenu("Load From CSV")]
    public void LoadFromCSV()
    {
        rows.Clear();
        _cache = null;

        if (csv == null)
        {
            Debug.LogError("[MonsterDropDatabase] csv가 비어있음");
            return;
        }

        var lines = csv.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
        {
            Debug.LogError("[MonsterDropDatabase] CSV 라인이 부족함");
            return;
        }

        // 헤더 인덱싱
        var header = SplitCsvLine(lines[0]);
        int idxMonsterType = IndexOf(header, "MonsterType");
        int idxTableID = IndexOf(header, "TableID");
        int idxDropType = IndexOf(header, "DropType");
        int idxItemID = IndexOf(header, "ItemID");
        int idxProb = IndexOf(header, "Probability(%)");

        if (idxMonsterType < 0 || idxTableID < 0 || idxDropType < 0 || idxItemID < 0 || idxProb < 0)
        {
            Debug.LogError("[MonsterDropDatabase] 헤더가 예상과 다름. 필요한 헤더: MonsterType,TableID,DropType,ItemID,Probability(%)");
            return;
        }

        int loaded = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = SplitCsvLine(lines[i]);
            if (cols.Count <= Mathf.Max(idxMonsterType, idxTableID, idxDropType, idxItemID, idxProb))
                continue;

            string monsterType = cols[idxMonsterType].Trim();
            string tableId = cols[idxTableID].Trim();
            string dropTypeStr = cols[idxDropType].Trim();
            string itemIdStr = cols[idxItemID].Trim();
            string probStr = cols[idxProb].Trim();

            if (string.IsNullOrEmpty(monsterType) || string.IsNullOrEmpty(tableId) || string.IsNullOrEmpty(dropTypeStr))
                continue;

            if (!int.TryParse(itemIdStr, out int itemId)) continue;
            if (!float.TryParse(probStr, out float weight)) continue;

            if (!TryParseDropType(dropTypeStr, out MonsterDropType dropType))
                continue;

            rows.Add(new MonsterDropRow
            {
                MonsterType = monsterType,
                TableID = tableId,
                DropType = dropType,
                ItemID = itemId,
                Weight = weight
            });
            loaded++;
        }

        BuildCache();
        Debug.Log($"[MonsterDropDatabase] 로드 완료: {loaded} rows");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // ===== helper =====

    private static bool TryParseDropType(string s, out MonsterDropType t)
    {
        // CSV에 Guaranteed/Bonus/Weapon 그대로 들어온다고 가정
        if (Enum.TryParse(s, true, out t)) return true;
        t = default;
        return false;
    }

    private static int IndexOf(List<string> header, string name)
    {
        for (int i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    // 간단 CSV split (따옴표 포함 케이스 최소 대응)
    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        string cur = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '\"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(cur);
                cur = "";
                continue;
            }

            cur += c;
        }
        result.Add(cur);
        return result;
    }
}
