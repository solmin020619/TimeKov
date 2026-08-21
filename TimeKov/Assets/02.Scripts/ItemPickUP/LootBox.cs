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

    /// <summary>내용물이 바뀔 때마다 올라가는 번호.
    /// ★가방이 가득 차 일부만 줍고 상자가 남는 경우가 생기면서 필요해졌다 — 상자 목록은 그대로인데
    ///   안에 든 것만 줄어드는 상황이라, 스캐너가 이 번호를 봐야 떠 있는 목록을 다시 그린다.</summary>
    public int ContentsVersion { get; private set; }

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
        ContentsVersion++;
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

    // 이 박스의 아이템을 Player 가방에 담는다. 다 담았으면 박스를 없앤다.
    //
    // ★가방이 가득 차면 '못 줍는다'. 예전에는 넘치는 분량을 창고로 자동 이송했는데,
    //   그러면 가방 관리를 할 이유가 없어지고 필드에서 주운 것이 어디로 갔는지도 안 보인다.
    //   지금은 안 들어간 만큼 박스에 그대로 남겨 둔다 — 자리를 비우고 다시 와서 주우면 된다.
    //   ★그래서 '전부 담았을 때만' 박스를 지운다. 무조건 지우면 못 담은 아이템이 증발한다.
    //     박스에는 수명(LifetimeSeconds)이 있으니 영원히 쌓이지는 않는다.
    public void Collect(Player player)
    {
        var inv = InventoryManager.Instance;
        if (inv == null)
        {
            Debug.LogWarning("[LootBox] InventoryManager.Instance 없음 — 아이템 추가 실패");
            return;
        }
        if (player == null) return;

        // 담고 남은 것만 모아 둔다. 다 담겼으면 비게 되고, 그때만 박스를 지운다.
        var left = new List<(int itemId, int count)>();
        int addedAny = 0;

        foreach (var (itemId, count) in _contents)
        {
            int remaining = inv.TryAddItemFromLoot(itemId, count);

            int addedToBag = count - remaining;
            if (addedToBag > 0)
            {
                addedAny += addedToBag;
                GameEvents.RaiseItemAcquired(itemId, addedToBag);   // 퀘스트 획득 이벤트
            }

            if (remaining > 0) left.Add((itemId, remaining));
        }

        // 한 개도 못 담았으면 소리·연출도 내지 않는다 — 주운 것처럼 보이면 안 된다.
        if (addedAny > 0)
        {
            GameSfx.Play(SfxId.ItemPickup);
            // 상자가 플레이어에게 빨려드는 연출이라, 상자가 남는 경우엔 쓰지 않는다.
            if (left.Count == 0)
            {
                LootBoxVFX vfx = GetComponentInParent<LootBoxVFX>();
                if (vfx != null) vfx.PlayCollectEffect(transform.position, player.transform);
            }
        }

        if (left.Count == 0)
        {
            Destroy(transform.root.gameObject);   // 다 담았다 — 박스 제거
            return;
        }

        // 남은 것은 박스에 그대로. 왜 안 주워졌는지 알려 주지 않으면 버그로 보인다.
        _contents.Clear();
        _contents.AddRange(left);
        ContentsVersion++;   // 떠 있는 아이템 목록을 다시 그리게 한다(LootBoxScanner)
        WarnBagFull();
    }

    // F 한 번에 범위 안 상자를 전부 줍기 때문에(LootBoxScanner.CollectAllInRange), 가방이
    //   가득 찬 상태에서는 상자 수만큼 같은 토스트가 쏟아진다. 짧은 시간 안에는 한 번만 띄운다.
    private static float _lastBagFullWarn = -999f;
    private const float BagFullWarnCooldown = 1.5f;

    private static void WarnBagFull()
    {
        if (Time.unscaledTime - _lastBagFullWarn < BagFullWarnCooldown) return;
        _lastBagFullWarn = Time.unscaledTime;
        ToastManager.Warning(Loc.Get("가방이 가득 찼습니다"));
    }
}
