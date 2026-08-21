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

    [Header("세이브")]
    [Tooltip("체크: 한 번 푼 조건을 저장해서 다음에 들어와도 풀린 채로 둔다(래치일 때만 의미 있음).\n" +
             "해제: 저장하지 않는다 — 들어올 때마다 다시 충족시켜야 하는 조건에 쓴다.")]
    [SerializeField] protected bool persistSatisfied = true;

    [Tooltip("세이브에서 이 조건을 구분하는 id. 비우면 계층 경로로 자동 생성한다(대부분 비워두면 된다).\n" +
             "★오브젝트를 옮기거나 이름을 바꾸면 자동 id 가 바뀌어 진행이 초기화된다. 그게 곤란하면 여기에 직접 적는다.")]
    [SerializeField] protected string saveId = "";

    private bool _fired;                       // latch 모드에서 이미 발동했는지
    public bool IsSatisfied { get; private set; }

    /// <summary>이 조건의 저장 키. 파생 종류별로 접두어가 달라 같은 오브젝트에 여러 기믹이 붙어도 안 겹친다.</summary>
    protected string SaveKey => GimmickSave.Key("trg." + GetType().Name, this, saveId);

    // ── 세이브 복원 ────────────────────────────────────────────────────────
    // ★Awake 에서 한다. 파생 트리거들은 Start 에서 입력(스위치/노드 …)을 훑어 처음 판정을
    //   하는데, 그보다 먼저 '이미 풀린 조건'을 확정해 둬야 그 판정이 덮어쓰지 않는다.
    //   (Awake 는 전부 Start 보다 먼저 돈다)
    // ★instant:true — 지금 푼 게 아니라 '이미 풀려 있던' 것을 되돌리는 것이므로
    //   결계가 사라지는 연출과 소멸음이 다시 나오면 안 된다.
    protected virtual void Awake()
    {
        if (latch && persistSatisfied && GimmickSave.GetBool(SaveKey))
            SetSatisfied(true, instant: true);
    }

    /// <summary>파생 클래스가 조건 충족/해제를 보고. 상태가 바뀔 때만 타깃에 반영.</summary>
    /// <param name="instant">true 면 타깃을 연출·사운드 없이 즉시 그 모습으로 만든다.
    /// 세이브에서 "이미 풀린 퍼즐"을 복원할 때만 쓴다 — 실제로 지금 푼 게 아니니
    /// 해제 연출과 소멸음이 다시 나오면 안 된다.</param>
    protected void SetSatisfied(bool satisfied, bool instant = false)
    {
        // ★한 번 발동한 래치는 무슨 일이 있어도 다시 안 풀린다.
        //   이게 없으면 타깃은 열린 채인데 IsSatisfied 만 false 로 돌아가는 어긋난 상태가 된다
        //   — 이미 연 자물쇠(KeyLock)에 F 가 다시 뜨고 열쇠를 한 번 더 먹는다.
        //   세이브 복원(Awake)이 발동시킨 뒤 파생 트리거가 Start 에서 다시 판정하는 흐름이
        //   생기면서 실제로 닿는 경로가 됐다.
        if (latch && _fired) return;
        if (satisfied == IsSatisfied) return;
        IsSatisfied = satisfied;

        if (latch)
        {
            if (satisfied)
            {
                _fired = true;
                OpenTargets(true, instant);
                if (!instant) OnFired();   // 복원일 땐 추가 연출도 생략
                if (persistSatisfied) GimmickSave.Set(SaveKey, true);
            }
        }
        else
        {
            OpenTargets(satisfied, instant);   // 유지형: 충족되면 열고, 풀리면 닫는다
        }
    }

    private void OpenTargets(bool open, bool instant)
    {
        for (int i = 0; i < targets.Count; i++)
            if (targets[i] != null) targets[i].SetOpen(open, instant);
    }

    // 래치 발동(최초 1회) 시 파생에서 추가 연출/사운드용 훅. 기본 무동작.
    protected virtual void OnFired() { }
}
