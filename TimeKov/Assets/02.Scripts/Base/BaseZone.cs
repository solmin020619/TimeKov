using System.Collections.Generic;
using UnityEngine;

public class BaseZone : MonoBehaviour
{
    // 씬에 떠 있는 결계존들. 위치 기반 동기 판정(Contains)에 사용.
    private static readonly List<BaseZone> _zones = new();
    private Collider _col;

    void Awake() => _col = GetComponent<Collider>();
    void OnEnable() { if (!_zones.Contains(this)) _zones.Add(this); }
    void OnDisable() => _zones.Remove(this);

    // 현재 위치가 결계존(트리거) 안인지 즉시(동기) 판정.
    // 트리거 enter/exit 캐시(부활 시 강제 true, 입력-물리 프레임 어긋남 등)에 의존하지 않아 stale 없음.
    public static bool Contains(Vector3 worldPos)
    {
        bool inside = false;
        string dbg = $"[BaseZone] Contains zones={_zones.Count} playerPos={worldPos}";
        for (int i = 0; i < _zones.Count; i++)
        {
            var z = _zones[i];
            if (z == null || z._col == null) { dbg += " | (null zone/col)"; continue; }
            float d2 = (z._col.ClosestPoint(worldPos) - worldPos).sqrMagnitude;
            dbg += $" | '{z.name}' {z._col.GetType().Name} trigger={z._col.isTrigger} boundsSize={z._col.bounds.size} d2={d2:F3}";
            if (d2 < 0.0001f) inside = true;
        }
        Debug.Log(dbg + "  => inside=" + inside);
        return inside;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Player>(out var player))
        {
            player.Stat.SetInBase(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<Player>(out var player))
        {
            player.Stat.SetInBase(false);
        }
    }
}