using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO: 새 인벤토리 연결 후 AddItem/ForceRefreshUI 복원
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
    public KeyCode debugKey = KeyCode.F12;

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
        yield return new WaitUntil(() => DataBoot.IsLoaded);

        foreach (var info in startItems)
        {
            if (GameDataHolder.I.ItemData.TryGet(info.itemID.ToString(), out var data))
                Debug.Log($"[TestSpawner] {data.itemName} x{info.amount} 지급 예정 (인벤토리 연결 대기)");
            else
                Debug.LogWarning($"[TestSpawner] ID:{info.itemID} — 없는 아이템");
        }
    }
}