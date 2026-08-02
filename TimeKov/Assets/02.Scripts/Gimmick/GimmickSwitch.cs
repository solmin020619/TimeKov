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

    private void SetMotions(bool on)
    {
        if (_motions == null) return;
        foreach (var m in _motions)
        {
            if (m == null) continue;
            if (on) m.Activate();
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
        OnChanged?.Invoke(this);
    }

    private void ApplyVisual()
    {
        if (onVisual  != null) onVisual.SetActive(IsOn);
        if (offVisual != null) offVisual.SetActive(!IsOn);
    }

    public void ShowHint(bool show)
    {
        InteractHintPanel.Show(hintUI, show, hintLabel, hintIcon);
        _highlight?.Set(show);
    }
}
