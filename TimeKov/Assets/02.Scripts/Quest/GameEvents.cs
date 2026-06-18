using System;
using UnityEngine;

public static class GameEvents
{
    // 기존
    public static event Action<float> OnPlayerMovedDelta;
    public static event Action<string> OnTriggerEntered;
    public static event Action<string> OnTriggerExited;
    public static event Action<string> OnEnemyKilled;

    // 튜토리얼 확장
    public static event Action<int, int> OnItemAcquired;             // itemId, count
    public static event Action<int> OnFacilityPlaced;                 // facilityId
    public static event Action<int, int, int> OnFacilityInput;        // facilityId, itemId, count
    public static event Action<int, int, int> OnFacilityProcessComplete; // facilityId, outputItemId, count
    public static event Action<int> OnFacilityInteract;               // facilityId — 설비 UI 실제 열림 (F 키 누름이 아니라 MachineUI.OpenFor 시점)
    public static event Action<int> OnItemUsed;                       // itemId
    public static event Action<int> OnFacilityUnlocked;               // facilityId — F로 설비 해금
    public static event Action<int> OnCoreUpgraded;                   // 강화 성공 후의 새 코어 레벨
    public static event Action OnRailConnected;                       // 레일 포트-포트 연결 완료
    public static event Action<int> OnFuelAdded;                      // facilityId — 설비 연료 슬롯에 연료 투입
    public static event Action OnBuildModeEntered;                    // 건설 모드 실제 진입 성공 (존 게이트 통과 후)
    public static event Action OnCoreUIOpened;                        // 코어 강화 단말(UI) 열림
    public static event Action<int, int, int> OnRailItemMoved;        // 레일(벨트)로 아이템이 다음 설비에 전달됨 (facilityId, itemId, count)
    public static event Action<int> OnQuickSlotRegistered;            // itemId — 소모품을 퀵슬롯(V)에 등록
    public static event Action<int> OnQuickSlotUsed;                  // itemId — 등록 소모품을 V로 사용 시도(만피로 막혀도 시도는 발생)

    public static void RaiseMovedDelta(float d) => OnPlayerMovedDelta?.Invoke(d);
    public static void RaiseTriggerEnter(string id) => OnTriggerEntered?.Invoke(id);
    public static void RaiseTriggerExit(string id) => OnTriggerExited?.Invoke(id);
    public static void RaiseEnemyKilled(string id) { Record(KeyEnemy(id), 1); OnEnemyKilled?.Invoke(id); }

    public static void RaiseItemAcquired(int itemId, int count) => OnItemAcquired?.Invoke(itemId, count);
    public static void RaiseFacilityPlaced(int facilityId) { Record(KeyPlaced(facilityId), 1); OnFacilityPlaced?.Invoke(facilityId); }
    public static void RaiseFacilityInput(int facilityId, int itemId, int count) { Record(KeyInput(facilityId, itemId), count); OnFacilityInput?.Invoke(facilityId, itemId, count); }
    public static void RaiseFacilityProcessComplete(int facilityId, int outputItemId, int count) => OnFacilityProcessComplete?.Invoke(facilityId, outputItemId, count);
    public static void RaiseFacilityInteract(int facilityId) { Record(KeyInteract(facilityId), 1); OnFacilityInteract?.Invoke(facilityId); }
    public static void RaiseItemUsed(int itemId) { Record(KeyUsed(itemId), 1); OnItemUsed?.Invoke(itemId); }
    public static void RaiseFacilityUnlocked(int facilityId) => OnFacilityUnlocked?.Invoke(facilityId);
    public static void RaiseCoreUpgraded(int newLevel) => OnCoreUpgraded?.Invoke(newLevel);
    public static void RaiseRailConnected() { Record(KeyRail, 1); OnRailConnected?.Invoke(); }
    public static void RaiseFuelAdded(int facilityId) { Record(KeyFuel(facilityId), 1); OnFuelAdded?.Invoke(facilityId); }
    public static void RaiseBuildModeEntered() => OnBuildModeEntered?.Invoke();
    public static void RaiseCoreUIOpened() { Record(KeyCoreOpen, 1); OnCoreUIOpened?.Invoke(); }
    public static void RaiseCoreUpgradeAttempt() => Record(KeyCoreUpgrade, 1);   // 강화 시도 기록 (lookback 갭 인정용)
    public static void RaiseRailItemMoved(int facilityId, int itemId, int count) { Record(KeyRailMove, count); OnRailItemMoved?.Invoke(facilityId, itemId, count); }
    public static void RaiseQuickSlotRegistered(int itemId) { Record(KeyQuickRegister, 1); OnQuickSlotRegistered?.Invoke(itemId); }
    public static void RaiseQuickSlotUsed(int itemId) { Record(KeyQuickUse, 1); OnQuickSlotUsed?.Invoke(itemId); }

    // ── 일회성 이벤트 lookback 캐시 ───────────────────────────────────────
    // 문제: 퀘↔퀘 사이 ~1.15초 갭 동안엔 활성 objective가 없어, 이때 플레이어가 다음 퀘가 시킬 행동을
    //       미리 하면 그 일회성 이벤트를 아무도 안 듣고 증발 → 다음 퀘 영구 미완료(B따닥과 동일 클래스).
    // 해결: 일회성 Raise를 (키+양+시각)으로 잠깐 기억해두고, 이벤트형 objective가 Activate 시
    //       LookbackWindow 내 자기 키의 발생량을 _count에 인정 + IsAlreadySatisfied로 즉시 완료.
    //       소비형(재료/연료)도 '이벤트가 났다'는 사실로 인정하므로 버퍼가 비어도 복구됨.
    const float LookbackWindow = 3.5f;   // 퀘 완료연출 갭(~1.15s) + 완료 타임아웃(3s) 위로 — 갭에 미리 한 행동도 인정(소프트락 방지)
    static readonly System.Collections.Generic.List<(string key, int amount, float time)> _recent = new();

    static void Record(string key, int amount)
    {
        float now = Time.unscaledTime;
        _recent.Add((key, amount, now));
        for (int i = _recent.Count - 1; i >= 0; i--)
            if (now - _recent[i].time > LookbackWindow) _recent.RemoveAt(i);
    }

    /// <summary>최근 LookbackWindow 내 발생한 key 이벤트의 누적량. objective가 Activate 때 갭에서 놓친 행동을 인정받는 용도.</summary>
    public static int RecentCount(string key)
    {
        float now = Time.unscaledTime;
        int sum = 0;
        for (int i = 0; i < _recent.Count; i++)
            if (_recent[i].key == key && now - _recent[i].time <= LookbackWindow) sum += _recent[i].amount;
        return sum;
    }

    // key 빌더 (Raise와 objective가 공유). facilityId==0(아무 설비나)은 갭-인정 미지원(라이브 경로는 동작).
    public static string KeyEnemy(string enemyId) => "enemy:" + enemyId;
    public static string KeyPlaced(int facilityId) => "place:" + facilityId;
    public static string KeyInput(int facilityId, int itemId) => "input:" + facilityId + ":" + itemId;
    public static string KeyInteract(int facilityId) => "interact:" + facilityId;
    public static string KeyUsed(int itemId) => "used:" + itemId;
    public static string KeyFuel(int facilityId) => "fuel:" + facilityId;
    public const string KeyRail = "rail";
    public const string KeyCoreOpen = "core_open";
    public const string KeyCoreUpgrade = "core_upgrade";   // 강화 시도 (성공/실패 무관)
    public const string KeyRailMove = "rail_move";         // 레일로 아이템 1회 전달
    public const string KeyQuickRegister = "quick_register"; // 소모품 퀵슬롯 등록 1회
    public const string KeyQuickUse = "quick_use";           // 퀵슬롯(V) 사용 시도 1회

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        OnPlayerMovedDelta = null;
        OnTriggerEntered = null;
        OnTriggerExited = null;
        OnEnemyKilled = null;
        OnItemAcquired = null;
        OnFacilityPlaced = null;
        OnFacilityInput = null;
        OnFacilityProcessComplete = null;
        OnFacilityInteract = null;
        OnItemUsed = null;
        OnFacilityUnlocked = null;
        OnCoreUpgraded = null;
        OnRailConnected = null;
        OnFuelAdded = null;
        OnBuildModeEntered = null;
        OnCoreUIOpened = null;
        OnRailItemMoved = null;
        OnQuickSlotRegistered = null;
        OnQuickSlotUsed = null;
        _recent.Clear();
    }
}
