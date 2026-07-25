using System.Collections.Generic;
using UnityEngine;

public class QuestTrigger : MonoBehaviour
{
    public string triggerId;
    public bool IsPlayerInside { get; private set; }

    // 앞뒤 공백은 무시한다 — 인스펙터에서 'transmit ' 처럼 실수로 공백이 들어가도 매칭되게(오타 방지).
    string Key => (triggerId ?? "").Trim();

    static readonly Dictionary<string, QuestTrigger> _registry = new();

    public static QuestTrigger Get(string id)
        => id != null && _registry.TryGetValue(id.Trim(), out var t) ? t : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => _registry.Clear();

    void OnEnable()
    {
        string k = Key;
        if (string.IsNullOrEmpty(k)) return;
        _registry[k] = this;
    }

    void OnDisable()
    {
        string k = Key;
        if (_registry.TryGetValue(k, out var t) && t == this)
            _registry.Remove(k);
    }

    void OnTriggerEnter(Collider c)
    {
        if (!c.CompareTag("Player")) return;
        IsPlayerInside = true;
        GameEvents.RaiseTriggerEnter(Key);
    }

    void OnTriggerExit(Collider c)
    {
        if (!c.CompareTag("Player")) return;
        IsPlayerInside = false;
        GameEvents.RaiseTriggerExit(Key);
    }
}
