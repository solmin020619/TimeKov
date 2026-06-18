using System;
using UnityEngine;

// 플레이어 소모품 퀵슬롯 (한 칸).
// 인벤토리 우클릭 메뉴에서 소모품을 등록해두면 전투 중 키 한 번(기본 V)으로 사용한다.
// 등록 정보(itemId)는 유지되고 개수는 인벤토리에서 실시간 조회한다(다 쓰면 비활성, 다시 모으면 재사용).
public class PlayerQuickSlotComponent : MonoBehaviour
{
    [Tooltip("연속 사용 사이 최소 간격(초). 한 번 눌러도 두 개 닳는 오발사 방지용 (밸런스 쿨다운 아님).")]
    [SerializeField] private float useCooldown = 0.3f;

    public int RegisteredItemId { get; private set; } = -1;

    // 등록/해제/사용 시 발생 (HUD 갱신용). 개수 변화는 InventoryManager.OnInventoryChanged 로 따로 추적.
    public event Action OnChanged;

    private Player _player;
    private PlayerInputComponent _input;
    private float _lastUseTime = -999f;

    public bool HasRegistered => RegisteredItemId > 0;

    public int Count =>
        (RegisteredItemId > 0 && InventoryManager.Instance != null)
            ? InventoryManager.Instance.GetTotalItemCount(RegisteredItemId)
            : 0;

    private void Awake()
    {
        _player = GetComponent<Player>();
        _input = GetComponent<PlayerInputComponent>();
    }

    private void Update()
    {
        if (_input != null && _input.QuickSlotPressed)
            TryUse();
    }

    // 우클릭 메뉴의 "퀵슬롯 등록" 에서 호출.
    public void Register(int itemId)
    {
        // 퀵슬롯은 체력(시간) 회복 앰플 전용 - 스탯 앰플 등은 등록 불가.
        if (!ConsumableEffectApplier.IsRecovery(itemId.ToString()))
        {
            ToastManager.Warning("체력 회복 앰플만 퀵슬롯에 등록할 수 있습니다.");
            return;
        }

        RegisteredItemId = itemId;
        OnChanged?.Invoke();
        ToastManager.Success("퀵슬롯에 등록했습니다.");
    }

    public void ClearSlot()
    {
        if (RegisteredItemId <= 0) return;
        RegisteredItemId = -1;
        OnChanged?.Invoke();
    }

    private void TryUse()
    {
        if (Time.timeScale == 0f) return;                  // 정지(영상/일시정지) 중 금지
        if (_player == null || _player.Stat == null || _player.Stat.IsDead) return;
        if (RegisteredItemId <= 0) return;                 // 빈 슬롯 = 조용히 무시
        if (Time.unscaledTime - _lastUseTime < useCooldown) return;

        var inv = InventoryManager.Instance;
        if (inv == null) return;

        int itemId = RegisteredItemId;

        if (inv.GetTotalItemCount(itemId) <= 0)
        {
            ToastManager.Warning("소모품이 없습니다.");
            return;
        }

        // 만피 회복 앰플 등 의미 없는 사용은 막고 아이템 보존 (우클릭 메뉴와 동일 규칙).
        if (!ConsumableEffectApplier.CanApply(itemId.ToString(), _player))
        {
            ToastManager.Warning("시간이 이미 가득 찼습니다.");
            return;
        }

        if (!inv.TryConsumeItem(itemId, 1)) return;

        bool applied = ConsumableEffectApplier.Apply(itemId.ToString(), _player);
        if (!applied)
            inv.AddItem(itemId, 1);   // 효과 적용 실패 시 복구

        _lastUseTime = Time.unscaledTime;
        OnChanged?.Invoke();
    }
}
