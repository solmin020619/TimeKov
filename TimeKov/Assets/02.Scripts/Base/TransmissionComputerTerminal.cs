// =====================================================================
// TransmissionComputerTerminal.cs
// 기지 코어 건물 내부에 배치하는 '전송 컴퓨터' 상호작용 오브젝트 (기획서 §11)
// 플레이어가 F키로 상호작용 → 전송 컴퓨터 UI 오픈.
// 코어 강화 단말(CoreUpgradeTerminal)과 구분되는 별도 오브젝트.
// =====================================================================

using UnityEngine;

public class TransmissionComputerTerminal : MonoBehaviour, IInteractable, IInteractHint
{
    public bool CanInteract => true;

    [Header("근접 힌트")]
    [Tooltip("가까이 가면 켤 알약 UI.\nCanvas > Notifications > FacilityUnlockSelectPanel 을 넣어라.")]
    [SerializeField] private GameObject hintUI;

    [Tooltip("알약에 표시할 이름.")]
    [SerializeField] private string hintLabel = "시간 에너지 전송";

    [Tooltip("알약 왼쪽 아이콘. 비우면 패널에 원래 박혀 있는 아이콘을 그대로 쓴다.")]
    [SerializeField] private Sprite hintIcon;

    [Tooltip("외곽선을 켤 대상들. 비우면 이 오브젝트 이하 전체.\n" +
             "부모자식이 아니어도 된다 - 씬 어디 있는 오브젝트든 여러 개 넣을 수 있다.")]
    [SerializeField] private Transform[] outlineTargets;

    private InteractHighlight _highlight;

    private void Start()
    {
        _highlight = new InteractHighlight(outlineTargets, transform);
        InteractHintPanel.Prime(hintUI, this);
    }

    public void ShowHint(bool show)
    {
        InteractHintPanel.Show(hintUI, show, hintLabel, hintIcon);
        _highlight?.Set(show);
    }

    public void Interact(Player player)
    {
        if (player == null) return;

        // F로 방금 닫은 직후엔 다시 열지 않음 (한 F 입력이 닫기+재오픈으로 깜빡이는 것 방지)
        if (Time.frameCount - TransmissionComputerUI.LastCloseFrame <= 1) return;

        // 기지 내부에서만 사용 가능
        if (!player.Stat.IsInBase) return;

        // 플레이어 상태 조건 검사 (강화 단말과 동일)
        if (player.Stat.IsDead || player.Stat.IsHurt ||
            player.Skill.IsExecuting || player.Dash.IsDashing)
            return;

        // 전송 컴퓨터 UI 오픈. 씬에 실물이 없으면 GetOrCreate 가 에러를 찍고 null 을 준다
        // (메뉴 Tools/TIMEKOV/전송 컴퓨터 UI 생성 으로 만든다).
        GameUIController.Instance?.OpenTransmissionUI();
        TransmissionComputerUI.GetOrCreate()?.Open();
        // 소개 영상은 엔드게임 퀘(quest_end_02)의 영상 objective 가 전송기 도착 시점에 재생한다(코어와 동일).
        //   예전엔 여기서 RaiseInteracted("transmit") 로 발견 큐를 띄웠는데 UI 를 연 뒤라 타이밍이 어긋나 제거했다.
    }
}
