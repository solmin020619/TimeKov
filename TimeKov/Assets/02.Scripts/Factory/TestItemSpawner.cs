using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestItemSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct SpawnInfo
    {
        [Tooltip("파싱된 데이터의 아이템 ID (예: 5104)")]
        public int itemID;
        public int amount;
    }

    [Header("시작 시 지급할 아이템 목록")]
    public List<SpawnInfo> startItems = new List<SpawnInfo>();

    [Header("인벤토리 시스템 연결")]
    public InventoryManager inventoryManager;

    [Header("단축키 지급 (선택)")]
    public KeyCode debugKey = KeyCode.F12;

    private void Start()
    {
        if (startItems.Count > 0)
        {
            StartCoroutine(GiveItemsRoutine());
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(debugKey))
        {
            Debug.Log("<color=cyan>[TestSpawner]</color> F12 키 눌림 인식됨!");
            StartCoroutine(GiveItemsRoutine());
        }
    }

    private IEnumerator GiveItemsRoutine()
    {
        if (startItems.Count == 0)
        {
            Debug.LogError("<color=red>[TestSpawner]</color> 인스펙터에 Start Items가 하나도 등록되지 않았습니다!");
            yield break;
        }

        Debug.Log($"<color=yellow>[TestSpawner]</color> 데이터 로딩 대기 시작... (찾는 ID: {startItems[0].itemID})");

        // 여기서 막혀있을 확률이 가장 높습니다!
        yield return new WaitUntil(() => DataManager.Instance != null && DataManager.Instance.GetItem(startItems[0].itemID) != null);

        Debug.Log("<color=yellow>[TestSpawner]</color> 데이터 로딩 완료! 인벤토리 넣기 시도...");

        if (inventoryManager == null)
        {
            Debug.LogError("<color=red>[TestSpawner]</color> InventoryManager가 연결되지 않았습니다!");
            yield break;
        }

        inventoryManager.CreateSlots();

        foreach (var info in startItems)
        {
            var itemData = DataManager.Instance.GetItem(info.itemID);
            if (itemData != null)
            {
                inventoryManager.AddItem(info.itemID, info.amount);
                Debug.Log($"<color=green>[TestSpawner]</color> {itemData.itemName}(ID:{info.itemID}) {info.amount}개 지급 완료!");
            }
        }

        inventoryManager.ForceRefreshUI();
    }
}