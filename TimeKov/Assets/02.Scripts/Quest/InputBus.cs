using System;
using UnityEngine;

public static class InputBus
{
    public static event Action<KeyCode> OnKeyDown;
    public static void RaiseKeyDown(KeyCode k) => OnKeyDown?.Invoke(k);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => OnKeyDown = null;
}
