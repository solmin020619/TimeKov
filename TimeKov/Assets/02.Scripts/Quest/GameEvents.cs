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

    public static void RaiseMovedDelta(float d) => OnPlayerMovedDelta?.Invoke(d);
    public static void RaiseTriggerEnter(string id) => OnTriggerEntered?.Invoke(id);
    public static void RaiseTriggerExit(string id) => OnTriggerExited?.Invoke(id);
    public static void RaiseEnemyKilled(string id) => OnEnemyKilled?.Invoke(id);

    public static void RaiseItemAcquired(int itemId, int count) => OnItemAcquired?.Invoke(itemId, count);
    public static void RaiseFacilityPlaced(int facilityId) => OnFacilityPlaced?.Invoke(facilityId);
    public static void RaiseFacilityInput(int facilityId, int itemId, int count) => OnFacilityInput?.Invoke(facilityId, itemId, count);
    public static void RaiseFacilityProcessComplete(int facilityId, int outputItemId, int count) => OnFacilityProcessComplete?.Invoke(facilityId, outputItemId, count);
    public static void RaiseFacilityInteract(int facilityId) => OnFacilityInteract?.Invoke(facilityId);
    public static void RaiseItemUsed(int itemId) => OnItemUsed?.Invoke(itemId);

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
    }
}
