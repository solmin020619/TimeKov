// =====================================================================
// StorageInflowNotice.cs
// 창고 "자동 유입" 공용 토스트.
// 플레이어가 직접 넣은 게 아닌데 아이템이 창고로 들어가는 모든 경로
// (철거 환급 / 레일 위 아이템 회수 / 벨트 자동 입고 등)를
// InventoryManager.OnItemAddedToStorage 한 곳에서 감지해 간단히 알린다.
// (종욱: 경로별 개별 토스트/케이스 처리 금지, 문구는 단순하게)
//
// - 한 번에 몰려 들어오는 유입(철거 한 방에 여러 아이템)은 1회로 묶음
// - 벨트 상시 자동입고가 돌 때 스팸이 안 되게, 유입이 끊겼다가(QuietRearmSeconds)
//   다시 시작될 때만 새로 띄운다
// - 플레이어 주도 경로(모두받기 넘침/상자 루팅 넘침 등 자체 토스트 있는 곳)는
//   AddItem 직전에 SuppressBriefly() 호출로 중복 알림을 끈다
// =====================================================================

using UnityEngine;

public static class StorageInflowNotice
{
    private const float QuietRearmSeconds = 6f;   // 이 시간 이상 조용했다가 들어오면 다시 알림

    private static float _lastInflowTime = -999f;
    private static float _suppressUntil = -999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        InventoryManager.OnItemAddedToStorage -= OnInflow;
        InventoryManager.OnItemAddedToStorage += OnInflow;
        _lastInflowTime = -999f;
        _suppressUntil = -999f;
    }

    /// <summary>플레이어가 직접 유발한 창고 입고 직전에 호출 - 잠깐 알림 억제(자체 토스트와 중복 방지).</summary>
    public static void SuppressBriefly(float seconds = 0.5f)
    {
        _suppressUntil = Time.unscaledTime + seconds;
    }

    private static void OnInflow(int itemId, int amount)
    {
        // 드래그 드롭 중 유입 = 플레이어 수동 입고(드롭존/접힘박스/슬롯 드롭 전부) - 알림 대상 아님.
        // (OnDrop 은 OnEndDrag 보다 먼저 실행되므로 이 시점 IsDragging 이 아직 true)
        var dh = InventoryDragHandler.Instance;
        if (dh != null && dh.IsDragging) return;

        float now = Time.unscaledTime;
        bool quietLongEnough = now - _lastInflowTime >= QuietRearmSeconds;
        _lastInflowTime = now;

        if (now < _suppressUntil) return;
        if (!quietLongEnough) return;   // 연속 유입(같은 철거/벨트 흐름)은 첫 1회만

        ToastManager.Info(Loc.Get("아이템이 창고로 이동했습니다"));
    }
}
