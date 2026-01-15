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
    public float Weight;
    public int IsAutomatic;
    public float Damage;
    public float FireRate;
    public float ReloadTime;
    public float EffectiveRange;
    public int UseRecoilPattern;
    public int RandomRecoilAngle;
    public float RecoilResetTime;
    public int PelletsPerShot;
    public float SpreadAngle;




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

        // i = 2 부터 시작 (0번 줄은 제목 줄이니까 건너뜀)
        for (int i = 2; i < lines.Length; i++)
        {
            // 데이터가 없는 빈 줄은 패스
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            // 콤마(,)를 기준으로 칸을 쪼갭니다.
            string[] data = lines[i].Split(',');

            // 새 아이템 생성
            ItemInfo newItem = new ItemInfo();

            newItem.id = int.Parse(data[0]);                // 첫 번째 칸: ID
            newItem.itemType = data[1];                     // 두 번째 칸: 아이템 타입
            newItem.itemName = data[2];                     // 세 번째 칸: 이름
            newItem.description = data[3];                  // 네 번째 칸: 설명
            newItem.magazinesize = int.Parse(data[4]);      // 다섯 번째 칸: 장탄수
            newItem.Duplicated = int.Parse(data[5]);        // 여섯 번째 칸: 중첩여부
            newItem.overlapsCount = int.Parse(data[6]);     // 일곱 번째 칸: 중첩개수
            newItem.SaleTime = int.Parse(data[7]);          // 여덟 번째 칸: 판매시간
            newItem.iconImange = data[8];                   // 아홉 번째 칸: 아이콘 이미지 파일명
            newItem.Weight = float.Parse(data[9]);          // 열번재 칸: 무게
            newItem.IsAutomatic = int.Parse(data[10]);      // 열한번째 칸: 연사 가능 여부
            newItem.Damage = float.Parse(data[11]);           // 열두번째 칸: 총 데미지
            newItem.FireRate = float.Parse(data[12]);         // 열세번째 칸: 발사 속도
            newItem.ReloadTime = float.Parse(data[13]);       // 열네번째 칸: 장전 시간
            newItem.EffectiveRange = float.Parse(data[14]);   // 열다섯번째 칸: 유효 사거리
            newItem.UseRecoilPattern = int.Parse(data[15]); // 열여섯번째 칸: 반동패턴 사용여부
            newItem.RandomRecoilAngle = int.Parse(data[16]);// 열일곱번째 칸: 패턴에 추가되는 랜덤반동 여부
            newItem.RecoilResetTime = float.Parse(data[17]);  // 열여덟번째 칸: 사격 중단 후 반동이 원위치로 돌아오는 시간
            newItem.PelletsPerShot = int.Parse(data[18]);   // 열아홉번째 칸: 한 발 발사 시 생성되는 탄 수
            newItem.SpreadAngle = float.Parse(data[19]);      // 스무번째 칸: 탄 퍼짐 각도

            
           

            // 리스트에 추가
            allItems.Add(newItem);
        }

        Debug.Log("<color=green>데이터 로드 완료! 아이템 개수: " + allItems.Count + "</color>");
    }
}

