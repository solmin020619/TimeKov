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
    // 픽업 효과음은 GameSfx(SfxId.ItemPickup)로 통합 — GameSfxConfig 에서 관리(모든 박스 통일).

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
        // 픽업 사운드 재생 (통합 GameSfx)
        GameSfx.Play(SfxId.ItemPickup);

        // VFX 재생
        LootBoxVFX vfx = GetComponentInParent<LootBoxVFX>();
        if (vfx != null && player != null)
            vfx.PlayCollectEffect(transform.position, player.transform);

        // Player 인벤토리에 아이템 추가
        var inv = InventoryManager.Instance;
        if (inv != null && player != null)
        {
            bool movedToStorage = false;
            var storage = InventoryManager.StorageInstance;

            foreach (var (itemId, count) in _contents)
            {
                int remaining = inv.TryAddItemFromLoot(itemId, count);

                // 가방에 들어간 분량 (퀘스트 획득 이벤트)
                int addedToBag = count - remaining;
                if (addedToBag > 0)
                    GameEvents.RaiseItemAcquired(itemId, addedToBag);

                // 가방에 못 들어간 분량은 창고로 (창고는 거의 무한). 획득 자체는 인정.
                if (remaining > 0 && storage != null)
                {
                    StorageInflowNotice.SuppressBriefly();   // 자체 토스트가 있으니 공용 알림 중복 방지
                    int afterStore = storage.AddItem(itemId, remaining);
                    int addedToStore = remaining - afterStore;
                    if (addedToStore > 0)
                    {
                        movedToStorage = true;
                        GameEvents.RaiseItemAcquired(itemId, addedToStore);
                    }
                    remaining = afterStore;
                }

                if (remaining > 0)
                    Debug.LogWarning($"[LootBox] 가방·창고 모두 가득 — 손실 itemId={itemId} count={remaining}");
            }

            if (movedToStorage)
                ToastManager.Info(Loc.Get("인벤토리가 가득 차 창고로 이동했습니다"));
        }
        else if (inv == null)
        {
            Debug.LogWarning("[LootBox] InventoryManager.Instance 없음 — 아이템 추가 실패");
        }

        // 루트 오브젝트째 파괴
        Destroy(transform.root.gameObject);
    }
}
