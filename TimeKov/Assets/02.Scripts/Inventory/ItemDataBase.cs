using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[System.Serializable]
public class ItemInfo
{
    public int id;
    public string itemType;
    public string itemName;
    public string description;
    public int magazinesize;
    public int duplicated;
    public int overlapsCount;
    public int saleTime;
    public string iconImange;
    public float weight;
    public int isAutomatic;
    public float damage;
    public float fireRate;
    public float reloadTime;
    public float effectiveRange;
    public int useRecoilPattern;
    public int randomRecoilAngle;
    public float recoilResetTime;
    public int pelletsPerShot;
    public float spreadAngle;
}

[CreateAssetMenu(fileName = "ItemData", menuName = "Game Data/Item Database")]
public class ItemDataBase : ScriptableObject
{
    public List<ItemInfo> allItems = new List<ItemInfo>();

    private Dictionary<int, ItemInfo> itemLookup = new Dictionary<int, ItemInfo>();

    public ItemInfo GetItemById(int id)
    {
        if (id <= 0)
            return null;

        EnsureLookup();

        if (itemLookup.TryGetValue(id, out ItemInfo item))
            return item;

        return null;
    }

    [Header("CSV 파일 연결")]
    [ContextMenuItem("이거 눌러서 데이터 로드하기", "LoadCSV")]
    public TextAsset csvFile;

    public void LoadCSV()
    {
        allItems.Clear();
        itemLookup.Clear();

        if (csvFile == null)
        {
            Debug.LogWarning("[ItemDataBase] csvFile 이 비어있음");
            return;
        }

        string[] lines = csvFile.text.Split('\n');
        for (int i = 2; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string line = lines[i].Trim();
            string[] data = line.Split(',');

            if (data.Length < 20)
            {
                Debug.LogWarning($"[ItemDataBase] CSV 열 개수 부족 - line:{i + 1}");
                continue;
            }

            ItemInfo newItem = new ItemInfo
            {
                id = ParseInt(data[0]),
                itemType = data[1],
                itemName = data[2],
                description = data[3],
                magazinesize = ParseInt(data[4]),
                duplicated = ParseInt(data[5]),
                overlapsCount = ParseInt(data[6]),
                saleTime = ParseInt(data[7]),
                iconImange = data[8],
                weight = ParseFloat(data[9]),
                isAutomatic = ParseInt(data[10]),
                damage = ParseFloat(data[11]),
                fireRate = ParseFloat(data[12]),
                reloadTime = ParseFloat(data[13]),
                effectiveRange = ParseFloat(data[14]),
                useRecoilPattern = ParseInt(data[15]),
                randomRecoilAngle = ParseInt(data[16]),
                recoilResetTime = ParseFloat(data[17]),
                pelletsPerShot = ParseInt(data[18]),
                spreadAngle = ParseFloat(data[19]),
            };

            allItems.Add(newItem);
            itemLookup[newItem.id] = newItem;
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
#endif

        Debug.Log("<color=green>데이터 로드 완료! 아이템 개수: " + allItems.Count + "</color>");
    }

    private void OnEnable()
    {
        EnsureLookup();
    }

    private void EnsureLookup()
    {
        if (itemLookup == null)
            itemLookup = new Dictionary<int, ItemInfo>();

        if (itemLookup.Count == allItems.Count && itemLookup.Count > 0)
            return;

        itemLookup.Clear();

        for (int i = 0; i < allItems.Count; i++)
        {
            ItemInfo item = allItems[i];
            if (item == null) continue;

            itemLookup[item.id] = item;
        }
    }

    private int ParseInt(string value)
    {
        int.TryParse(Clean(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result);
        return result;
    }

    private float ParseFloat(string value)
    {
        float.TryParse(Clean(value), NumberStyles.Float, CultureInfo.InvariantCulture, out float result);
        return result;
    }

    private string Clean(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Trim().Replace("\r", "");
    }
}