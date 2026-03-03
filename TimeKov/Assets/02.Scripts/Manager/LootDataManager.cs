using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // ✅ [추가]

public class LootDataManager : MonoBehaviour
{
    public static LootDataManager Instance { get; private set; }

    // ✅ [추가] 레이드 세션 번호 (레이드 씬 다시 들어올 때 증가)
    public static int CurrentRaidSession { get; private set; } = 0;

    [Header("CSV (TextAsset)")]
    public TextAsset containerTableCsv;      // LootContainerTable_2files.csv
    public TextAsset lootTableUnifiedCsv;    // LootTableUnified.csv

    [Serializable]
    public class ContainerDef
    {
        public string containerId;
        public string containerName;
        public int slotCount;
        public string lootTableId;
        public int reroll; // 0/1
    }

    [Serializable]
    public class LootEntry
    {
        public int itemId;
        public float probability;
        public int minCount;
        public int maxCount;
    }

    [Serializable]
    public class LootTableDef
    {
        public string lootTableId;
        public int minRoll;
        public int maxRoll;
        public int allowDuplicate; // 0/1
        public List<LootEntry> entries = new List<LootEntry>();
    }

    private readonly Dictionary<string, ContainerDef> _containerById = new Dictionary<string, ContainerDef>();
    private readonly Dictionary<string, LootTableDef> _lootTableById = new Dictionary<string, LootTableDef>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAll();
    }

    // ✅ [추가] 씬 로드 훅
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // ✅ [추가]
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ✅ [추가] 레이드 씬(= LootContainer가 존재하는 씬) 들어오면 세션 증가
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 주의: base 씬에 LootContainer가 없으면 세션 증가 안 함
        // 레이드 씬에 LootContainer가 하나라도 있으면 세션 증가
        var containers = FindObjectsOfType<LootContainer>(true);
        if (containers != null && containers.Length > 0)
        {
            CurrentRaidSession++;
            Debug.Log($"[LootDataManager] New Raid Session = {CurrentRaidSession} (scene={scene.name}, containers={containers.Length})");
        }
    }

    public bool TryGetContainer(string containerId, out ContainerDef def) => _containerById.TryGetValue(containerId, out def);
    public bool TryGetLootTable(string lootTableId, out LootTableDef def) => _lootTableById.TryGetValue(lootTableId, out def);

    private void LoadAll()
    {
        _containerById.Clear();
        _lootTableById.Clear();

        if (containerTableCsv == null || lootTableUnifiedCsv == null)
        {
            Debug.LogError("[LootDataManager] CSV TextAsset이 인스펙터에 연결 안됨");
            return;
        }

        LoadContainerTable(containerTableCsv.text);
        LoadLootUnifiedTable(lootTableUnifiedCsv.text);

        Debug.Log($"[LootDataManager] Loaded Containers={_containerById.Count}, LootTables={_lootTableById.Count}");
    }

    private void LoadContainerTable(string csv)
    {
        var lines = SplitLines(csv);
        if (lines.Count <= 1) return; // header only

        // header: ContainerID,ContainerName,SlotCount,LootTableID,Reroll
        for (int i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            var cols = SplitCsvLine(lines[i]);
            if (cols.Count < 5) continue;

            var def = new ContainerDef
            {
                containerId = cols[0].Trim(),
                containerName = cols[1].Trim(),
                slotCount = ToInt(cols[2]),
                lootTableId = cols[3].Trim(),
                reroll = ToInt(cols[4])
            };

            if (string.IsNullOrEmpty(def.containerId)) continue;
            _containerById[def.containerId] = def;
        }
    }

    private void LoadLootUnifiedTable(string csv)
    {
        var lines = SplitLines(csv);
        if (lines.Count <= 1) return;

        // header: LootTableID,MinRoll,MaxRoll,AllowDuplicate,ItemID,Probability,MinCount,MaxCount
        for (int i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            var cols = SplitCsvLine(lines[i]);
            if (cols.Count < 8) continue;

            string tableId = cols[0].Trim();
            if (string.IsNullOrEmpty(tableId)) continue;

            int minRoll = ToInt(cols[1]);
            int maxRoll = ToInt(cols[2]);
            int allowDup = ToInt(cols[3]);

            int itemId = ToInt(cols[4]);
            float prob = ToFloat(cols[5]);
            int minCount = ToInt(cols[6]);
            int maxCount = ToInt(cols[7]);

            if (!_lootTableById.TryGetValue(tableId, out var table))
            {
                table = new LootTableDef
                {
                    lootTableId = tableId,
                    minRoll = minRoll,
                    maxRoll = maxRoll,
                    allowDuplicate = allowDup,
                    entries = new List<LootEntry>()
                };
                _lootTableById[tableId] = table;
            }

            // table의 룰 값은 첫 줄 기준으로 유지(나중에 행마다 중복으로 들어오니까)
            // entries만 계속 추가
            table.entries.Add(new LootEntry
            {
                itemId = itemId,
                probability = prob,
                minCount = minCount,
                maxCount = maxCount
            });
        }
    }

    // ---------- CSV Utils ----------
    private static List<string> SplitLines(string text)
    {
        var list = new List<string>();
        using (var sr = new System.IO.StringReader(text))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
                list.Add(line);
        }
        return list;
    }

    // 따옴표 포함 CSV도 최소한 처리 (간단 파서)
    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var cur = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(cur.ToString());
                cur.Length = 0;
                continue;
            }

            cur.Append(c);
        }

        result.Add(cur.ToString());
        return result;
    }

    private static int ToInt(string s)
    {
        int.TryParse(s.Trim(), out int v);
        return v;
    }

    private static float ToFloat(string s)
    {
        float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
        return v;
    }
}