using System.Collections.Generic;
using UnityEngine;

// ── 기믹 "조건"(Trigger) 공용 베이스 ─────────────────────────────────────────
// "어떤 조건이 충족되면 연결된 GimmickTarget 들을 연다"의 공통 뼈대.
//   파생 클래스(레이저 결계=KillZoneTrigger, 스위치=SwitchTrigger, 압력판, 에너지 노드 …)는
//   자기 조건을 판정해서 SetSatisfied(true/false) 만 호출하면 된다. 타깃 개폐/래치는 여기서 처리.
//
//   latch=true  : 한 번 충족되면 계속 열린 채 유지(조건이 깨져도 안 닫힘). 결계/봉인/문 등 대부분.
//   latch=false : 조건 상태를 그대로 따라감(충족 시 열림, 해제 시 닫힘). 압력판(무게 유지)처럼 유지형.
public abstract class GimmickTrigger : MonoBehaviour
{
    [Header("연동 대상 (이 조건이 충족되면 열림)")]
    [Tooltip("이 트리거가 열/닫을 GimmickTarget 들. 여러 개 연결 가능(문 여러 개 동시 개방 등).")]
    [SerializeField] protected List<GimmickTarget> targets = new();

    [Tooltip("체크: 한 번 충족되면 계속 열림 유지(조건이 다시 깨져도 안 닫힘).\n" +
             "해제: 조건 상태를 그대로 따라감(압력판처럼 '유지해야' 열려 있는 경우 끈다).")]
    [SerializeField] protected bool latch = true;

    private bool _fired;                       // latch 모드에서 이미 발동했는지
    public bool IsSatisfied { get; private set; }

    // 파생 클래스가 조건 충족/해제를 보고. 상태가 바뀔 때만 타깃에 반영.
    protected void SetSatisfied(bool satisfied)
    {
        if (satisfied == IsSatisfied) return;
        IsSatisfied = satisfied;

        if (latch)
        {
            if (satisfied && !_fired)
            {
                _fired = true;
                OpenTargets(true);
                OnFired();
            }
        }
        else
        {
            OpenTargets(satisfied);   // 유지형: 충족되면 열고, 풀리면 닫는다
        }
    }

    private void OpenTargets(bool open)
    {
        for (int i = 0; i < targets.Count; i++)
            if (targets[i] != null) targets[i].SetOpen(open);
    }

    // 래치 발동(최초 1회) 시 파생에서 추가 연출/사운드용 훅. 기본 무동작.
    protected virtual void OnFired() { }
}
