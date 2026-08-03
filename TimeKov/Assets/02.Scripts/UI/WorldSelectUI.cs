// =====================================================================
// WorldSelectUI.cs
// 타이틀 화면에서 띄우는 월드(세이브 슬롯) 선택 패널 — 팰월드 WORLD SELECT 참고.
// 행 클릭 = 선택(하이라이트)만 함. 하단 우측 액션 바(게임 시작/삭제)가 현재
// 선택된 슬롯을 대상으로 동작한다. "+ 신규 월드 생성하기"는 별도 — 이름 입력
// 모달 -> 확정 -> 곧장 입장. 뒤로가기는 별도 버튼 없이 ESC로 처리(모달이 열려있으면
// 모달부터 닫고, 아니면 메인메뉴로 복귀).
// =====================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldSelectUI : MonoBehaviour
{
    [SerializeField] GameObject panelRoot;
    [SerializeField] Transform rowContainer;
    [SerializeField] WorldSelectRow rowPrefab;
    [SerializeField] Button createRowButton;   // "+ 신규 월드 생성하기" 행 — 이름 입력 모달을 띔

    [Header("하단 액션 바 (선택된 슬롯 대상)")]
    [SerializeField] Button enterButton;  // "게임 시작"
    [SerializeField] Button deleteButton; // "삭제"

    [Header("이름 입력 모달")]
    [SerializeField] GameObject createModal;
    [SerializeField] TMP_InputField newWorldNameInput;
    [SerializeField] Button confirmCreateButton; // 모달 안 "결정"
    [SerializeField] Button modalBackdropButton; // 모달 바깥(딤 영역) 클릭 시 닫기

    [Tooltip("이 패널이 떠 있는 동안 가려야 할 메인메뉴 세로 버튼 목록(게임 시작/옵션/제작진/게임 종료). 비워두면 무시.")]
    [SerializeField] GameObject mainMenuList;

    [Tooltip("이 패널이 떠 있는 동안 같이 가려야 할 메인메뉴 장식 요소(로고/언더라인/태그라인 등) — 목록과 겹쳐 보이는 것 방지.")]
    [SerializeField] GameObject[] hideWhileOpen;

    [Tooltip("슬롯 확정 후 로딩을 거쳐 진입할 실제 플레이 씬 이름.")]
    [SerializeField] string gameplaySceneName = "World";

    [Tooltip("신규 월드 생성 시 경유할 프롤로그 씬 이름. 비워두면 gameplaySceneName으로 바로 진입.")]
    [SerializeField] string prologueSceneName = "Prologue";

    readonly List<WorldSelectRow> _spawnedRows = new();
    WorldSelectRow _selectedRow;

    void Awake()
    {
        // mainMenuList는 예전에 에디터 빌더가 "MenuList"를 통째로
        // 지우고 새로 만든다 — 이 WorldSelectUI를 다시 빌드하지 않은 채로 그 builder를
        // 나중에 또 돌리면 여기 직렬화된 참조가 파괴된 옛 GameObject를 가리키게 되어(=null
        // 취급) 메인메뉴 버튼들이 안 가려지는 버그가 난다. 빌드 순서에 의존하지 않도록
        // 참조가 비어있으면 이름으로 직접 찾아 자가 복구한다.
        if (mainMenuList == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) mainMenuList = canvas.transform.Find("MenuList")?.gameObject;
        }

        // panelRoot는 보통 이 컴포넌트와 같은 GameObject — 에디터 빌더가 이미 비활성 상태로
        // 저장해두므로 여기서 다시 SetActive(false)하면 Show()가 막 활성화한 직후 자기 자신을
        // 도로 꺼버리는 꼴이 된다. 그래서 Awake에서는 건드리지 않는다.
        if (createRowButton != null) createRowButton.onClick.AddListener(OpenCreateModal);
        if (confirmCreateButton != null) confirmCreateButton.onClick.AddListener(OnClickCreateNewWorld);
        if (modalBackdropButton != null) modalBackdropButton.onClick.AddListener(CloseCreateModal);
        if (enterButton != null) enterButton.onClick.AddListener(OnClickEnter);
        if (deleteButton != null) deleteButton.onClick.AddListener(OnClickDelete);
    }

    void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (createModal != null && createModal.activeSelf) CloseCreateModal();
        else Hide();
    }

    public void Show()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        if (mainMenuList != null) mainMenuList.SetActive(false);
        SetHideWhileOpen(false);
        Refresh();
    }

    public void Hide()
    {
        CloseCreateModal();
        if (panelRoot != null) panelRoot.SetActive(false);
        if (mainMenuList != null) mainMenuList.SetActive(true);
        SetHideWhileOpen(true);
    }

    void SetHideWhileOpen(bool visible)
    {
        if (hideWhileOpen == null) return;
        foreach (var go in hideWhileOpen)
            if (go != null) go.SetActive(visible);
    }

    void OpenCreateModal()
    {
        if (newWorldNameInput != null) newWorldNameInput.text = string.Empty;
        if (createModal != null) createModal.SetActive(true);
    }

    void CloseCreateModal()
    {
        if (createModal != null) createModal.SetActive(false);
    }

    void Refresh()
    {
        foreach (var row in _spawnedRows)
            if (row != null) Destroy(row.gameObject);
        _spawnedRows.Clear();
        SelectRow(null);

        if (SaveSlotManager.Instance == null || rowPrefab == null || rowContainer == null) return;

        foreach (var meta in SaveSlotManager.Instance.ListSlots())
        {
            var row = Instantiate(rowPrefab, rowContainer);
            row.Set(meta, () => SelectRow(row));
            _spawnedRows.Add(row);
        }

        // "+ 신규 월드 생성하기" 행은 rowContainer의 정적 자식 — 데이터 행이 매번 다시 생성되며
        // 맨 끝에 추가되므로, 항상 마지막에 오도록 매번 다시 맨 뒤로 보낸다.
        if (createRowButton != null) createRowButton.transform.SetAsLastSibling();
    }

    void SelectRow(WorldSelectRow row)
    {
        if (_selectedRow != null) _selectedRow.SetSelected(false);
        _selectedRow = row;
        if (_selectedRow != null) _selectedRow.SetSelected(true);

        bool hasSelection = _selectedRow != null;
        if (enterButton != null) enterButton.interactable = hasSelection;
        if (deleteButton != null) deleteButton.interactable = hasSelection;
    }

    void OnClickEnter()
    {
        if (_selectedRow != null) EnterSlot(_selectedRow.SlotId);
    }

    void OnClickDelete()
    {
        if (_selectedRow == null) return;
        SaveSlotManager.Instance?.DeleteSlot(_selectedRow.SlotId);
        Refresh();
    }

    void OnClickCreateNewWorld()
    {
        if (SaveSlotManager.Instance == null) return;

        string name = newWorldNameInput != null ? newWorldNameInput.text : null;
        var meta = SaveSlotManager.Instance.CreateSlot(name);
        CloseCreateModal();

        // 신규 월드: 프롤로그 씬 경유 (prologueSceneName 비어있으면 바로 게임플레이 씬)
        string firstScene = string.IsNullOrEmpty(prologueSceneName) ? gameplaySceneName : prologueSceneName;
        EnterSlot(meta.slotId, firstScene);
    }

    void EnterSlot(string slotId, string overrideScene = null)
    {
        if (SaveSlotManager.Instance == null || !SaveSlotManager.Instance.LoadSlot(slotId)) return;
        CoreUtilities.LoadViaLoading(overrideScene ?? gameplaySceneName);
    }
}
