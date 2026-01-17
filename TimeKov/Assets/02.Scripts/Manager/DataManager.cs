using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance; // 싱글톤

    [Header("모든 데이터 파일 연결")]
  
    public ItemDatabase itemDB;

    private void Awake()
    {
        // 싱글톤 세팅
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 파괴되지 않음
    }

    private void Start() // 로그로 테스트용
    {
        if (itemDB == null)
        {
            Debug.LogError("[DB] itemDB is NULL. Inspector에 ItemDatabase 연결 필요");
            return;
        }

        // 파싱/조회 최소 검증 로그 (Count 없이도 충분)
        var pistol = GetItem(1401);
        var ak = GetItem(1201);

        //Debug.Log($"[DB] Pistol(1401)={(pistol != null ? pistol.itemName : "NULL")}");    // 테스트용 로그
        //Debug.Log($"[DB] AK(1201)={(ak != null ? ak.itemName : "NULL")}");                // 테스트용 로그
    }

    // 어디서든 아이템 정보를 가져올 수 있게 해주는 함수
    public ItemInfo GetItem(int id)
    {
        if (itemDB == null) return null;
        return itemDB.GetItemById(id);
    }
}
