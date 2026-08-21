using System.Collections.Generic;
using UnityEngine;

// ── 에너지 공급 조건 트리거 (노드 채우면 개방) ──────────────────────────────────
// EnergyNode 여러 개가 '전부(또는 필요 개수)' 활성화되면 연결된 GimmickTarget 을 연다.
//   • 노드 1개만 넣어 '단일 노드 → 문/다리 활성화'로도, 여러 노드로도 쓸 수 있다.
//   • 각 노드는 스스로 연료(인벤토리 아이템)를 받아 활성화된다 — 여기선 완료 여부만 모은다.
//
//   requireAll=true  : 모든 노드가 활성화돼야 열림(대부분).
//   requireAll=false : 아래 requiredCount 개 이상 활성화되면 열림.
//   래치(latch, 부모)=true(기본)면 한 번 열리면 계속 열림.
public class EnergyConduit : GimmickTrigger
{
    [Header("에너지 노드")]
    [Tooltip("이 도관에 물린 노드들. 전부(또는 필요 개수) 활성화되면 타깃이 열린다.")]
    [SerializeField] private List<EnergyNode> nodes = new();

    [Header("조건")]
    [Tooltip("체크: 모든 노드가 활성화돼야 열림. 해제: 아래 '필요 개수'만 활성화되면 열림.")]
    [SerializeField] private bool requireAll = true;
    [Tooltip("requireAll 해제 시 — 몇 개가 활성화되면 열릴지.")]
    [Min(1)] [SerializeField] private int requiredCount = 1;

    private readonly List<EnergyNode> _subscribed = new();

    private void Start()
    {
        _subscribed.Clear();
        foreach (var n in nodes)
        {
            if (n == null || _subscribed.Contains(n)) continue;
            _subscribed.Add(n);
            n.OnChanged += OnNodeChanged;
        }
        // ★첫 판정은 instant — 세이브에서 채워진 채 복원된 노드 때문에 조건이 이미 맞을 수 있다.
        Evaluate(instant: true);
    }

    private void OnDestroy()
    {
        foreach (var n in _subscribed)
            if (n != null) n.OnChanged -= OnNodeChanged;
    }

    private void OnNodeChanged(EnergyNode _) => Evaluate();

    private void Evaluate(bool instant = false)
    {
        int active = 0;
        foreach (var n in _subscribed)
            if (n != null && n.IsActive) active++;

        int need = requireAll ? _subscribed.Count : Mathf.Max(1, requiredCount);
        SetSatisfied(_subscribed.Count > 0 && active >= need, instant);
    }
}
