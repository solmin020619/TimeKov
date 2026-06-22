using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestItemSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct SpawnInfo
    {
        public int itemID;
        public int amount;
    }

    [Header("지급할 아이템 목록")]
    public List<SpawnInfo> startItems = new();

    [Header("재지급 단축키")]
    public KeyCode debugKey = KeyCode.F4;   // ★직렬화 필드 - 실제값은 인스펙터. F4 적용하려면 인스펙터에서도 바꿀 것.

    private void Start()
    {
        if (startItems.Count > 0) StartCoroutine(GiveItemsRoutine());
    }

    private void Update()
    {
        if (Input.GetKeyDown(debugKey))
            StartCoroutine(GiveItemsRoutine());
    }

    private IEnumerator GiveItemsRoutine()
    {
        // 로딩씬을 거쳐 진입하므로 데이터는 항상 로드 완료 상태
        var inv = InventoryManager.Instance;
        if (inv == null)
        {
            Debug.LogWarning("[TestSpawner] InventoryManager 인스턴스 없음 — 아이템 지급 불가");
            yield break;
        }

        foreach (var info in startItems)
        {
            if (GameDataHolder.I.ItemData.TryGet(info.itemID.ToString(), out var data))
            {
                inv.AddItem(info.itemID, info.amount);
            }
            else
            {
                Debug.LogWarning($"[TestSpawner] ID:{info.itemID} — 없는 아이템");
            }
        }

        inv.ForceRefreshUI();
    }
}