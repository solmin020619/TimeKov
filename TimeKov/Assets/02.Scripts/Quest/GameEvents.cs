using System;
using UnityEngine;

public static class GameEvents
{
    public static event Action<float> OnPlayerMovedDelta;
    public static event Action<string> OnTriggerEntered;
    public static event Action<string> OnTriggerExited;
    public static event Action<string> OnEnemyKilled;

    public static void RaiseMovedDelta(float d) => OnPlayerMovedDelta?.Invoke(d);
    public static void RaiseTriggerEnter(string id) => OnTriggerEntered?.Invoke(id);
    public static void RaiseTriggerExit(string id) => OnTriggerExited?.Invoke(id);
    public static void RaiseEnemyKilled(string id) => OnEnemyKilled?.Invoke(id);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        OnPlayerMovedDelta = null;
        OnTriggerEntered = null;
        OnTriggerExited = null;
        OnEnemyKilled = null;
    }
}
