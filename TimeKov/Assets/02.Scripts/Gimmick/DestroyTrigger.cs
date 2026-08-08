using System.Collections.Generic;
using UnityEngine;

// ── 파괴 조건 트리거 (파괴물 다 부수면 개방) ────────────────────────────────────
// Destructible 여러 개가 '전부(또는 필요 개수)' 부서지면 연결된 GimmickTarget 을 연다.
//   • 파괴물 1개만 넣어 '단일 파괴 → 문 열림'으로도, 여러 개로도 쓸 수 있다.
//   • 각 Destructible 은 스스로 타격을 받아 부서진다 — 여기선 파괴 여부만 모은다.
//
//   requireAll=true  : 모든 파괴물이 부서져야 열림(대부분).
//   requireAll=false : 아래 requiredCount 개 이상 부서지면 열림.
//   래치(latch, 부모)=true(기본)면 한 번 열리면 계속 열림.
public class DestroyTrigger : GimmickTrigger
{
    [Header("파괴물")]
    [Tooltip("이 조건에 묶인 Destructible 들. 전부(또는 필요 개수) 부서지면 타깃이 열린다.")]
    [SerializeField] private List<Destructible> targetsToBreak = new();

    [Header("조건")]
    [Tooltip("체크: 모두 부서져야 열림. 해제: 아래 '필요 개수'만 부서지면 열림.")]
    [SerializeField] private bool requireAll = true;
    [Tooltip("requireAll 해제 시 — 몇 개가 부서지면 열릴지.")]
    [Min(1)] [SerializeField] private int requiredCount = 1;

    private readonly List<Destructible> _subscribed = new();
    private int _broken;

    private void Start()
    {
        _subscribed.Clear();
        _broken = 0;
        foreach (var d in targetsToBreak)
        {
            if (d == null || _subscribed.Contains(d)) continue;
            _subscribed.Add(d);
            if (d.IsBroken) _broken++;
            else d.OnBroken += OnOneBroken;
        }
        Evaluate();
    }

    private void OnDestroy()
    {
        foreach (var d in _subscribed)
            if (d != null) d.OnBroken -= OnOneBroken;
    }

    private void OnOneBroken(Destructible _)
    {
        _broken++;
        Evaluate();
    }

    private void Evaluate()
    {
        int need = requireAll ? _subscribed.Count : Mathf.Max(1, requiredCount);
        SetSatisfied(_subscribed.Count > 0 && _broken >= need);
    }
}
