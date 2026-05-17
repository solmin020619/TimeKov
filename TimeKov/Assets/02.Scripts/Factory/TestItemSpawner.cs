// =====================================================================
// TestItemSpawner.cs
// 테스트용 아이템 지급기
// 구버전 DataStore.IsLoaded / DataStore.GetItem 을 새 스키마로 교체
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestItemSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct SpawnInfo
    {
        [Tooltip("아이템 ID (예: 1101)")]
        public int itemID;
        public int amount;
    }

    [Header("지급할 아이템 목록")]
    public List<SpawnInfo> startItems = new List<SpawnInfo>();

    [Header("인벤토리 연결")]
    public InventoryManager inventoryManager;

    [Header("재지급 단축키")]
    public KeyCode debugKey = KeyCode.F12;

    private void Start()
    {
        if (startItems.Count > 0)
            StartCoroutine(GiveItemsRoutine());
    }

    private void Update()
    {
        if (Input.GetKeyDown(debugKey))
        {
            Debug.Log("<color=cyan>[TestSpawner]</color> 재지급 키 입력!");
            StartCoroutine(GiveItemsRoutine());
        }
    }

    private IEnumerator GiveItemsRoutine()
    {
        if (startItems.Count == 0)
        {
            Debug.LogError("[TestSpawner] Start Items가 비어있습니다");
            yield break;
        }

        if (inventoryManager == null)
        {
            Debug.LogError("[TestSpawner] InventoryManager가 연결되지 않았습니다");
            yield break;
        }

        // 구버전: DataStore.IsLoaded → 신버전: DataBoot.IsLoaded
        yield return new WaitUntil(() => DataBoot.IsLoaded);

        inventoryManager.CreateSlots();

        foreach (var info in startItems)
        {
            // 구버전: DataStore.GetItem(info.itemID) → 신버전: GameDataHolder.I.ItemData.TryGet
            if (GameDataHolder.I.ItemData.TryGet(info.itemID.ToString(), out var itemData))
            {
                inventoryManager.AddItem(info.itemID, info.amount);
                Debug.Log($"<color=green>[TestSpawner]</color> {itemData.itemName}(ID:{info.itemID}) {info.amount}개 지급 완료");
            }
            else
            {
                Debug.LogWarning($"[TestSpawner] ID:{info.itemID} — ItemDataTable에 없는 아이템");
            }
        }

        inventoryManager.ForceRefreshUI();
    }
}