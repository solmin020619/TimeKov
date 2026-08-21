using System.Collections.Generic;
using UnityEngine;

// ── 스위치 조건 트리거 (원격 버튼 + 다중 스위치 공용) ─────────────────────────────
// GimmickSwitch 여러 개를 모아 조건을 판정하고 GimmickTarget 을 연다.
//   • 원격 버튼   : 스위치 1개 + requireAll → 누르면 열림.
//   • 다중 스위치 : 스위치 여러 개 → 전부(또는 필요 개수) 켜지면 열림.
//
//   requireAll=true  : 모든 스위치가 켜져야 열림.
//   requireAll=false : 아래 requiredCount 개 이상 켜지면 열림("3개 중 2개" 같은 조건).
//
//   latch(부모)=true  면 한 번 조건이 맞으면 계속 열린 채 유지(토글 스위치를 다시 꺼도 안 닫힘).
//   latch=false 면 조건이 유지되는 동안만 열림(다중 토글로 '유지해야 열리는' 문 등).
public class SwitchTrigger : GimmickTrigger
{
    [Header("스위치들 (원격 버튼이면 1개)")]
    [SerializeField] private List<GimmickSwitch> switches = new();

    [Header("조건")]
    [Tooltip("체크: 모든 스위치가 켜져야 열림. 해제: 아래 '필요 개수'만 켜지면 열림.")]
    [SerializeField] private bool requireAll = true;
    [Tooltip("requireAll 해제 시 — 몇 개가 켜지면 열릴지.")]
    [Min(1)] [SerializeField] private int requiredCount = 1;

    private void Start()
    {
        foreach (var sw in switches)
            if (sw != null) sw.OnChanged += OnSwitchChanged;
        // ★첫 판정은 instant — 세이브에서 켜진 채 복원된 스위치들 때문에 조건이 이미 맞을 수 있다.
        //   그때 연출이 돌면 들어올 때마다 결계가 다시 사라지는 장면이 재생된다.
        Evaluate(instant: true);
    }

    private void OnDestroy()
    {
        foreach (var sw in switches)
            if (sw != null) sw.OnChanged -= OnSwitchChanged;
    }

    private void OnSwitchChanged(GimmickSwitch _) => Evaluate();

    private void Evaluate(bool instant = false)
    {
        int on = 0;
        foreach (var sw in switches)
            if (sw != null && sw.IsOn) on++;

        int need = requireAll ? switches.Count : Mathf.Max(1, requiredCount);
        SetSatisfied(on >= need, instant);
    }
}
