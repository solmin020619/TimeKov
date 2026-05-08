using System.Collections.Generic;
using UnityEngine;

public class QuestTrigger : MonoBehaviour
{
    public string triggerId;
    public bool IsPlayerInside { get; private set; }

    static readonly Dictionary<string, QuestTrigger> _registry = new();

    public static QuestTrigger Get(string id)
        => _registry.TryGetValue(id, out var t) ? t : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => _registry.Clear();

    void OnEnable()
    {
        if (string.IsNullOrEmpty(triggerId)) return;
        _registry[triggerId] = this;
    }

    void OnDisable()
    {
        if (_registry.TryGetValue(triggerId, out var t) && t == this)
            _registry.Remove(triggerId);
    }

    void OnTriggerEnter(Collider c)
    {
        if (!c.CompareTag("Player")) return;
        IsPlayerInside = true;
        GameEvents.RaiseTriggerEnter(triggerId);
    }

    void OnTriggerExit(Collider c)
    {
        if (!c.CompareTag("Player")) return;
        IsPlayerInside = false;
        GameEvents.RaiseTriggerExit(triggerId);
    }
}
