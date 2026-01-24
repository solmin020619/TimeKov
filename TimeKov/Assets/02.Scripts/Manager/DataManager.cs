using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance; // 싱글톤

    [Header("모든 데이터 파일 연결")]
    public ItemDataBase itemDB;

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

    // 어디서든 아이템 정보를 가져올 수 있게 해주는 함수
    public ItemInfo GetItem(int id)
    {
        return itemDB.GetItemById(id);
    }
}