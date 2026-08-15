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

    // ── 수명 ──────────────────────────────────────────────────────────
    // 안 주운 상자가 땅에 영구히 남으면 사냥터가 상자로 뒤덮이고 세이브도 계속 커진다.
    // ★'플레이 중에만' 시간이 간다(접속 안 한 시간은 안 센다). 급한 일로 껐다 왔더니
    //   보스 드랍이 사라져 있는 건 유저가 가장 화내는 종류라, 실제 시각 기준은 쓰지 않는다.
    //   -> 남은 시간을 세이브에 같이 넣고 복원 때 이어받는다(LootBoxSaveBridge).
    [Tooltip("상자가 땅에 남아 있는 시간(초). 다 되면 사라진다.")]
    //   5분 = 마인크래프트(6000틱)와 같은 값. 다른 게임들이 대체로 2.5~5분이라 플레이어 감각이
    //   이 근처에 맞춰져 있다. "잠깐 정리하고 오면 있고 딴짓하고 오면 없다" 는 체감.
    public static float LifetimeSeconds = 300f;

    private float _remain = -1f;

    /// <summary>남은 수명(초). 세이브가 읽어 간다.</summary>
    public float RemainingLife => _remain;

    /// <summary>세이브 복원용 - 남은 수명을 이어받는다.</summary>
    public void SetRemainingLife(float seconds)
    {
        _remain = Mathf.Max(0.1f, seconds);
    }

    void Start()
    {
        if (_remain < 0f) _remain = LifetimeSeconds;
    }

    void Update()
    {
        if (_remain < 0f) return;
        _remain -= Time.deltaTime;
        if (_remain <= 0f) Destroy(transform.root.gameObject);
    }

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
