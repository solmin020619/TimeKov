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
    public int Duplicated;
    public int overlapsCount;
    public int SaleTime;
    public string iconImange;

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
        for (int i = 1; i < lines.Length; i++)
        {
            // 데이터가 없는 빈 줄은 패스
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            // 콤마(,)를 기준으로 칸을 쪼갭니다.
            string[] data = lines[i].Split(',');

            // 새 아이템 생성
            ItemInfo newItem = new ItemInfo();

            newItem.id = int.Parse(data[0]);                // 첫 번째 칸: ID
            newItem.itemType = data[1];                     // 첫 번째 칸: 아이템 타입
            newItem.itemName = data[2];                     // 두 번째 칸: 이름
            newItem.description = data[3];                  // 세 번째 칸: 설명
            newItem.magazinesize = int.Parse(data[4]);      // 첫 번째 칸: 장탄수
            newItem.iconImange = data[5];                   // 첫 번째 칸: 아이콘 이미지 파일명
            newItem.Duplicated = int.Parse(data[6]);        // 첫 번째 칸: 중첩여부
            newItem.overlapsCount = int.Parse(data[7]);     // 첫 번째 칸: 중첩개수

            // 리스트에 추가
            allItems.Add(newItem);
        }

        Debug.Log("<color=green>데이터 로드 완료! 아이템 개수: " + allItems.Count + "</color>");
    }
}

