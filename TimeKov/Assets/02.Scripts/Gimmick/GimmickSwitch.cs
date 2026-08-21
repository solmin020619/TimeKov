using System;
using UnityEngine;

// ── 스위치/버튼 입력 하나 (원격 버튼 + 다중 스위치 공용) ──────────────────────────
// 플레이어가 F로 조작하는 물리 스위치. 자신은 문을 열지 않는다 — 켜짐/꺼짐(IsOn) 상태만
//   SwitchTrigger 에 알린다. SwitchTrigger 가 여러 스위치를 모아 조건을 판정하고 타깃을 연다.
//
//   • oneShot=true  : 한 번 누르면 계속 켜짐(원격 버튼). 다시 못 끈다 → 켜진 뒤엔 F 힌트도 사라짐.
//   • oneShot=false : 누를 때마다 On/Off 토글. 여러 개를 '동시에' 켜야 하는 조합 퍼즐용.
//
//   WarpPoint 와 같은 방식으로 F 알약(InteractHintPanel) + 근접 외곽선(InteractHighlight)을 쓴다.
[RequireComponent(typeof(Collider))]
public class GimmickSwitch : MonoBehaviour, IInteractable, IInteractHint
{
    [Header("모드")]
    [Tooltip("체크: 한 번 누르면 계속 켜짐(원격 버튼). 해제: 누를 때마다 On/Off 토글(조합 퍼즐).")]
    [SerializeField] private bool oneShot = true;

    [Header("On/Off 비주얼 (선택)")]
    [Tooltip("켜졌을 때만 켤 오브젝트(불 켜진 레버/버튼 등).")]
    [SerializeField] private GameObject onVisual;
    [Tooltip("꺼졌을 때만 켤 오브젝트(불 꺼진 레버/버튼 등).")]
    [SerializeField] private GameObject offVisual;

    [Header("사운드")]
    [Tooltip("켤 때 재생. None 이면 무음.")]
    [SerializeField] private SfxId pressOnSfx = SfxId.None;
    [Tooltip("끌 때 재생(토글 전용). None 이면 무음.")]
    [SerializeField] private SfxId pressOffSfx = SfxId.None;

    [Header("켜지면 움직이는 부품 (선택)")]
    [Tooltip("스위치가 켜지면 Activate() 로 움직이기 시작할 Turn_Move 들. 비우면 이 오브젝트/자식의 Turn_Move 를 자동으로 쓴다.\n" +
             "각 Turn_Move 는 autoStart 를 꺼두고 rampUpTime 으로 '천천히 가속'을 설정한다. 토글 스위치면 끌 때 멈춘다.")]
    [SerializeField] private Turn_Move[] activateMotions;
    [Tooltip("체크하면 켜지는 순간 이 오브젝트/자식의 Turn_Move 를 자동으로 찾아 함께 켠다(위 목록에 없어도).")]
    [SerializeField] private bool autoFindMotions = true;

    [Header("세이브")]
    [Tooltip("체크: 켜짐/꺼짐을 저장해서 다음에 들어와도 그대로 둔다.\n" +
             "해제: 저장하지 않는다 — 들어올 때마다 꺼진 채로 시작한다.")]
    [SerializeField] private bool persistState = true;
    [Tooltip("세이브에서 이 스위치를 구분하는 id. 비우면 계층 경로로 자동 생성한다(대부분 비워두면 된다).\n" +
             "★오브젝트를 옮기거나 이름을 바꾸면 자동 id 가 바뀌어 초기화된다. 그게 곤란하면 여기에 직접 적는다.")]
    [SerializeField] private string saveId = "";

    [Header("근접 힌트 (F)")]
    [Tooltip("가까이 가면 켤 F 알약 UI. 비우면 씬의 공용 패널(FacilityUnlockSelectPanel)을 자동으로 찾아 쓴다.")]
    [SerializeField] private GameObject hintUI;
    [Tooltip("알약에 표시할 이름.")]
    [SerializeField] private string hintLabel = "스위치";
    [Tooltip("알약 왼쪽 아이콘. 비우면 패널 기본 아이콘 사용.")]
    [SerializeField] private Sprite hintIcon;
    [Tooltip("가까이 가면 외곽선을 켤 대상들. 비우면 이 오브젝트를 쓴다.")]
    [SerializeField] private Transform[] outlineTargets;

    // 현재 켜짐 여부. SwitchTrigger 가 구독해서 조건을 판정한다.
    public bool IsOn { get; private set; }
    public event Action<GimmickSwitch> OnChanged;

    private InteractHighlight _highlight;
    private Turn_Move[]       _motions;   // 켜지면 움직일 부품(자동탐색 결과 캐시)

    // ★복원은 Awake 에서 한다. 조건 트리거(SwitchTrigger)가 Start 에서 스위치들을 훑어
    //   처음 판정을 하므로, 그보다 먼저 켜짐 상태가 확정돼 있어야 한다.
    private void Awake() => RestoreSaved();

    private void Start()
    {
        // PlayerInteractComponent 의 OverlapSphere 는 Interactable 레이어만 훑는다.
        int il = LayerMask.NameToLayer("Interactable");
        if (il >= 0) gameObject.layer = il;

        if (hintUI == null) hintUI = InteractHintPanel.FindSharedPanel();
        _highlight = new InteractHighlight(outlineTargets, transform);
        InteractHintPanel.Prime(hintUI, this);
        ResolveMotions();
        ApplyVisual();

        // 켜진 채로 복원됐으면 부품도 이미 '돌고 있던' 속도로 돌아야 한다.
        //   ★가속을 건너뛴다(skipRampUp). 0부터 가속하면 껐다 켤 때마다 풍차가 멈춰 있다가
        //     새로 시동 거는 것처럼 보인다 — 스위치를 켠 적도 없는데 지금 막 켠 것처럼 읽힌다.
        if (IsOn) SetMotions(true, skipRampUp: true);
    }

    // 켤 부품 목록 확정: 인스펙터 지정 + (옵션)자식 자동탐색을 합친다(중복 제거).
    private void ResolveMotions()
    {
        var set = new System.Collections.Generic.List<Turn_Move>();
        if (activateMotions != null)
            foreach (var m in activateMotions)
                if (m != null && !set.Contains(m)) set.Add(m);
        if (autoFindMotions)
            foreach (var m in GetComponentsInChildren<Turn_Move>(true))
                if (m != null && !set.Contains(m)) set.Add(m);
        _motions = set.ToArray();
    }

    /// <param name="skipRampUp">가속 없이 곧바로 원속도. 세이브 복원 전용.</param>
    private void SetMotions(bool on, bool skipRampUp = false)
    {
        if (_motions == null) return;
        foreach (var m in _motions)
        {
            if (m == null) continue;
            if (on) m.Activate(skipRampUp);
            else    m.Deactivate();
        }
    }

    // oneShot 이고 이미 켜졌으면 더 이상 F 후보가 아니다(힌트 사라짐). 토글은 항상 조작 가능.
    public bool CanInteract => !(oneShot && IsOn);

    public void Interact(Player player)
    {
        if (oneShot) { if (!IsOn) SetOn(true); }
        else         { SetOn(!IsOn); }
    }

    // SwitchTrigger 가 조건 판정 후 필요 시 강제로 끌 때 사용(토글 스위치만). oneShot 은 무시.
    public void ForceReset()
    {
        if (oneShot) return;
        SetOn(false);
    }

    private void SetOn(bool on)
    {
        if (IsOn == on) return;
        IsOn = on;
        // 지정 안 했으면(None) 기본 스위치음으로 폴백 → 배치된 스위치도 소리가 난다.
        SfxId sfx = on
            ? (pressOnSfx  == SfxId.None ? SfxId.GimmickSwitchOn  : pressOnSfx)
            : (pressOffSfx == SfxId.None ? SfxId.GimmickSwitchOff : pressOffSfx);
        GameSfx.Play(sfx, transform.position);
        ApplyVisual();
        SetMotions(on);   // 켜지면 부품 가동 시작(천천히 가속), 꺼지면 정지
        if (persistState) GimmickSave.Set(SaveKey, on);
        OnChanged?.Invoke(this);
    }

    // ── 세이브 ────────────────────────────────────────────────────────────
    // 스위치 하나하나가 켜짐/꺼짐을 따로 저장한다. 그래야 "3개 중 하나만 켜 둔" 상태로
    //   나갔다 와도 그 하나만 켜진 채로 돌아온다(조건 트리거는 그걸 보고 다시 판정한다).
    //
    //   ★복원은 Awake 에서, 소리·연출 없이. SetOn 을 쓰면 들어올 때마다 스위치 누르는 소리가
    //     나고 풍차가 '지금 막 켜진 것처럼' 천천히 가속한다.
    //     대신 Start 의 ApplyVisual/ResolveMotions 가 켜진 상태에 맞춰 정리한다.
    private string SaveKey => GimmickSave.Key("sw", this, saveId);

    private void RestoreSaved()
    {
        if (!persistState) return;
        IsOn = GimmickSave.GetBool(SaveKey);
    }

    private void ApplyVisual()
    {
        if (onVisual  != null) onVisual.SetActive(IsOn);
        if (offVisual != null) offVisual.SetActive(!IsOn);
    }

    public void ShowHint(bool show)
    {
        InteractHintPanel.Show(hintUI, show, Loc.Get(hintLabel), hintIcon);
        _highlight?.Set(show);
    }
}
