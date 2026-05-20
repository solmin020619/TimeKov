// =====================================================================
// LootBox.cs
// 필드 드롭 박스 — F키 상호작용 시 Player 인벤토리에 아이템 추가
// EnemyDropOnDeath 가 스폰, LootBoxScanner 가 F키 감지 후 Collect() 호출
// 기획서 섹션 4.1: 반드시 Player InventoryManager 에만 AddItem
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public class LootBox : MonoBehaviour, IInteractable
{
    public static readonly List<LootBox> All = new List<LootBox>();

    private readonly List<(int itemId, int count)> _contents =
        new List<(int itemId, int count)>();

    public IReadOnlyList<(int itemId, int count)> Contents => _contents;

    public void Initialize(List<(int itemId, int count)> contents)
    {
        _contents.Clear();
        if (contents != null) _contents.AddRange(contents);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public bool CanInteract => true;

    // F키 입력 시 LootBoxScanner 가 호출
    public void Interact(Player player)
    {
        LootBoxScanner scanner = FindAnyObjectByType<LootBoxScanner>();
        if (scanner != null)
            scanner.CollectAllInRange(player);
        else
            Collect(player);
    }

    // 이 박스의 아이템을 Player 인벤토리에 추가하고 박스 제거
    // 완전 실패(공간 부족)한 아이템이 있으면 Debug.Log 출력
    // 일부 성공은 메시지 없이 UI 수량 변화로 확인 (기획서 섹션 20.1)
    public void Collect(Player player)
    {
        // VFX 재생
        LootBoxVFX vfx = GetComponentInParent<LootBoxVFX>();
        if (vfx != null && player != null)
            vfx.PlayCollectEffect(transform.position, player.transform);

        // Player 인벤토리에 아이템 추가
        var inv = InventoryManager.Instance;
        if (inv != null && player != null)
        {
            bool anyCompleteFail = false;

            foreach (var (itemId, count) in _contents)
            {
                int remaining = inv.TryAddItemFromLoot(itemId, count);

                // 실제 인벤에 들어간 양만큼 퀘스트 이벤트 발화
                int added = count - remaining;
                if (added > 0)
                    GameEvents.RaiseItemAcquired(itemId, added);

                // 전량 실패 시 공간 부족
                if (remaining == count)
                {
                    anyCompleteFail = true;
                    Debug.Log($"[LootBox] 가방 공간 부족: itemId={itemId} count={count}");
                }
            }

            if (anyCompleteFail)
                Debug.Log("[LootBox] 일부 아이템을 넣지 못했습니다 — 가방 공간 부족");
        }
        else if (inv == null)
        {
            Debug.LogWarning("[LootBox] InventoryManager.Instance 없음 — 아이템 추가 실패");
        }

        // 루트 오브젝트째 파괴
        Destroy(transform.root.gameObject);
    }
}
