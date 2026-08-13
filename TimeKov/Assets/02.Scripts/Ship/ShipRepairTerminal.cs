using UnityEngine;

// 폐우주선(공사 펜스) 상호작용 오브젝트. F -> 수리 UI 오픈.
// (CoreUpgradeTerminal 과 동일 패턴)
// ★[07-22] 콜라이더 자동 맞춤 로직은 넣었다가 제거했다.
//   우주선이 건축 구역 정중앙에 있던 시절엔 "펜스 = 건축 금지 범위" 를 정확히 맞춰야 해서
//   자동 산출이 의미가 있었는데, 우주선을 그리드 밖으로 빼기로 하면서 맞출 대상이 사라졌다.
//   지금은 그냥 인스펙터에서 콜라이더를 손으로 잡는 게 맞다.
public class ShipRepairTerminal : MonoBehaviour, IInteractable, IInteractHint
{
    [Header("근접 힌트")]
    [Tooltip("가까이 가면 켤 알약 UI.\nCanvas > Notifications > FacilityUnlockSelectPanel 을 넣어라.")]
    [SerializeField] private GameObject hintUI;

    [Tooltip("알약에 표시할 이름.")]
    [SerializeField] private string hintLabel = "우주선 수리";

    [Tooltip("알약 왼쪽 아이콘. 비우면 패널에 원래 박혀 있는 아이콘을 그대로 쓴다.")]
    [SerializeField] private Sprite hintIcon;

    [Tooltip("가까이 가면 외곽선을 켤 대상들. 비우면 이 오브젝트(부모에 붙였으면 펜스 전체)를 쓴다.\n" +
             "부모자식이 아니어도 된다 - 씬 어디 있는 오브젝트든 여러 개 넣을 수 있다.")]
    [SerializeField] private Transform[] outlineTargets;

    // ★[07-22] 자체 외곽선/월드 라벨 코드는 InteractHintPanel 로 옮겼다.
    //   알약 UI 를 새로 그리지 않고, 공장 설비/전리품 픽업이 쓰는 그 패널을 그대로 켠다.
    private InteractHighlight _highlight;

    private void Start()
    {
        _highlight = new InteractHighlight(outlineTargets, transform);
        InteractHintPanel.Prime(hintUI, this);
    }

    // 플레이어가 가장 가까운 상호작용 대상이 되면 켜지고, 벗어나면 꺼진다.
    // (PlayerInteractComponent 가 매 프레임 nearest 를 갱신하며 호출)
    public void ShowHint(bool show)
    {
        InteractHintPanel.Show(hintUI, show, Loc.Get(hintLabel), hintIcon);
        _highlight?.Set(show);
    }

    // ★[07-22] 항상 F 후보로 켜진다.
    //   예전엔 "수리 가능할 때만"(CanRepairNext) 이었다. 우주선이 공장 한복판에 있던 시절,
    //   주변 설비들의 F 와 겹치지 않게 하려던 장치다.
    //   우주선을 건축 구역 밖으로 빼면서 겹칠 상대가 사라졌고, 반대로
    //   "재료가 없으면 진행도조차 못 본다"는 문제만 남았다(UI 를 열 수가 없다).
    //   부족한 재료는 UI 안에서 "부품 부족 n/m" 으로 알려주므로 열어두는 쪽이 맞다.
    public bool CanInteract => ShipRepairManager.Instance != null;

    public void Interact(Player player)
    {
        if (player == null) return;

        // 행동 중 열림 방지
        if (player.Stat.IsDead || player.Stat.IsHurt ||
            player.Skill.IsExecuting || player.Dash.IsDashing)
            return;

        // F로 방금 닫은 직후 재오픈 방지(한 F가 닫기+재오픈으로 깜빡이는 것 방지)
        if (Time.frameCount - ShipRepairUI.LastCloseFrame <= 1) return;

        var mgr = ShipRepairManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[ShipRepairTerminal] ShipRepairManager 가 씬에 없음.");
            return;
        }

        // UI 패널이 있으면(꺼져 있어도 EnsureInstance 가 활성화) 열고, 없으면(빌더 미실행) 즉시 수리 폴백.
        var ui = ShipRepairUI.EnsureInstance();
        if (ui != null)
        {
            GameUIController.Instance?.OpenShipRepairUI();
            ui.Open();
        }
        else
        {
            Debug.LogWarning("[ShipRepairTerminal] 우주선 수리 UI 패널이 씬에 없어 즉시 수리로 대체한다. " +
                             "메뉴 Tools > TIMEKOV > 우주선 수리 UI 생성 을 실행해라.");
            mgr.TryRepairNext();
        }
    }
}
