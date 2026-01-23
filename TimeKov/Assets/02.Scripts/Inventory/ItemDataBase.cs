using System.Collections.Generic;
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
public class ItemDatabase : ScriptableObject
{
    public List<ItemInfo> allItems = new List<ItemInfo>();

    public ItemInfo GetItemById(int id)
    {
        // 리스트를 뒤져서 ID가 같은 걸 찾아 반환. 없으면 null 반환.
        return allItems.Find(item => item.id == id);
    }

    [Header("CSV 파일 연결")]
    // 변수 이름 위에 우클릭
    [ContextMenuItem("이거 눌러서 데이터 로드하기", "LoadCSV")]
    public TextAsset csvFile;
    public void LoadCSV()
    {
        // 기존 데이터 비우기 (중복 방지)
        allItems.Clear();

        // 엔터(\n)를 기준으로 한 줄씩 
        string[] lines = csvFile.text.Split('\n');

        // i = 1 부터 시작 (0번 줄은 제목 줄이니까 건너뜀)
        for (int i = 2; i < lines.Length; i++)
        {
            // 데이터가 없는 빈 줄은 패스
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            // 콤마(,)를 기준으로 칸을 쪼갭니다.
            string[] data = lines[i].Split(',');

            // 새 아이템 생성
            ItemInfo newItem = new ItemInfo();

            newItem.id = int.Parse(data[0]);
            newItem.itemType = data[1];
            newItem.itemName = data[2];
            newItem.description = data[3];
            newItem.magazinesize = int.Parse(data[4]);
            newItem.duplicated = int.Parse(data[5]);
            newItem.overlapsCount = int.Parse(data[6]);
            newItem.saleTime = int.Parse(data[7]);
            newItem.iconImange = data[8];
            newItem.weight = float.Parse(data[9]);
            newItem.isAutomatic = int.Parse(data[10]);
            newItem.damage = float.Parse(data[11]);
            newItem.fireRate = float.Parse(data[12]);
            newItem.reloadTime = float.Parse(data[13]);
            newItem.effectiveRange = float.Parse(data[14]);
            newItem.useRecoilPattern = int.Parse(data[15]);
            newItem.randomRecoilAngle = int.Parse(data[16]);
            newItem.recoilResetTime = float.Parse(data[17]);
            newItem.pelletsPerShot = int.Parse(data[18]);
            newItem.spreadAngle = float.Parse(data[19]);

            // 리스트에 추가
            allItems.Add(newItem);
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
#endif

        Debug.Log("<color=green>데이터 로드 완료! 아이템 개수: " + allItems.Count + "</color>");
    }
}
