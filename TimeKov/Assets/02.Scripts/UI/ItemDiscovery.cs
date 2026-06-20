using System.Collections.Generic;
using UnityEngine;

// 아이템 도감 획득 추적 (세션 단위, 저장 없음 - CodexDiscovery/RecipeProgress와 동일 정책).
// 가방(OnItemAddedToInventory) + 창고(OnItemAddedToStorage) 입고를 모두 구독 = 어디로 들어오든 잡힌다.
// 커버: 필드 드롭 / 공장 '모두 받기' / 퀘스트 보상(가방) + 벨트->창고 자동입고 / 철거 환급(창고 직행).
// 정적 부트스트랩으로 구독 -> 도감 안 열어도 획득이 쌓인다.
public static class ItemDiscovery
{
    private static readonly HashSet<int> _obtained = new HashSet<int>();

    public static bool IsObtained(int itemId) => _obtained.Contains(itemId);
    public static int Count => _obtained.Count;

    // 처음 획득한 아이템이면 도감 알림(!) 점등.
    public static void MarkObtained(int itemId)
    {
        if (itemId <= 0) return;
        if (_obtained.Add(itemId))
            CodexNotice.MarkUnseen(CodexNotice.Item);
    }

    // 인벤 획득 이벤트 구독(씬 로드 후 1회). -=/+= 로 중복 구독 방지.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        InventoryManager.OnItemAddedToInventory -= OnAdded;
        InventoryManager.OnItemAddedToInventory += OnAdded;
        InventoryManager.OnItemAddedToStorage -= OnAdded;   // 창고 직행(벨트 자동입고/철거 환급)도 추적
        InventoryManager.OnItemAddedToStorage += OnAdded;
    }

    static void OnAdded(int itemId, int amount) => MarkObtained(itemId);

    // 세션 시작마다 초기화(저장 없음).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => _obtained.Clear();
}
