// =====================================================================
// WorldSelectUI.cs
// 타이틀 화면에서 띄우는 월드(세이브 슬롯) 선택 패널 — 팰월드 WORLD SELECT 참고.
// 행 클릭 = 선택(하이라이트), 더블클릭 = 입장. 하단 우측 액션 바(게임 시작/삭제)가 현재
// 선택된 슬롯을 대상으로 동작한다. "+ 신규 월드 생성하기"는 별도 — 이름 입력
// 모달 -> 확정 -> 곧장 입장. 뒤로가기는 별도 버튼 없이 ESC로 처리(모달이 열려있으면
// 모달부터 닫고, 아니면 메인메뉴로 복귀).
//
// [글자는 전부 씬에 있어야 한다]
//   팀원이 씬을 훑어 번역할 문구를 모은다. 코드가 만든 라벨은 그 수집에서 통째로 빠지므로
//   이 스크립트는 TMP_Text 를 하나도 만들지 않는다. 안내 문구·글자수·취소 버튼·빈 목록
//   안내·삭제 확인창은 전부 씬 오브젝트를 인스펙터로 받아서 쓴다(아래 [Header] 참고).
//   코드가 하는 건 그것들의 색·크기·위치를 규격(MenuModalStyle)에 맞추는 일뿐이다.
//
//   예외는 값이 매번 달라지는 두 곳뿐 — 이름 안내 문구와 삭제 확인 본문. 이 둘은 코드가
//   .text 를 쓰므로 ★LocalizedLabel 을 붙이면 안 된다(붙이면 서로 덮어쓴다).
//   대신 아래 Loc.Get(...) 의 한글 원문이 곧 시트 키다.
//
// [한글 입력(IME)]
//   이 화면은 게임에서 유일하게 사람이 글자를 치는 곳이라 IME 를 직접 신경 써야 한다.
//   조합 중(예: "월ㄷ")에는 아직 InputField.text 에 그 글자가 없고, Enter 는 조합
//   확정용으로 먼저 쓰인다. 그리고 입력칸을 켜둔 채 화면을 넘기면 IME 조합 모드가 켜진
//   채로 남아, 게임에 들어가서 첫 입력들이 먹히지 않는 것처럼 보인다.
//   -> 나가는 길은 전부 ReleaseTextInput() 을 지나가게 하고, 조합이 남아 있으면 확정을
//      먼저 시킨 뒤 이름을 읽는다. 자세한 이유는 각 메서드 주석에.
//      (ESC 는 예외 — 조합 중이든 아니든 그냥 창을 닫는다. 아래 Update 참고)
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [Tooltip("모달 안 '취소' 버튼. 라벨은 씬 텍스트 그대로 쓴다(LocalizedLabel 붙여도 됨).")]
    [SerializeField] Button cancelCreateButton;

    [Tooltip("입력칸 아래 왼쪽 안내 문구. ★코드가 .text 를 쓰므로 LocalizedLabel 을 붙이지 말 것.")]
    [SerializeField] TMP_Text nameHintText;

    [Tooltip("입력칸 아래 오른쪽 글자 수(예: 5 / 20). 숫자뿐이라 번역 대상이 아니다.")]
    [SerializeField] TMP_Text nameCounterText;

    [Header("삭제 확인 모달")]
    [Tooltip("월드 삭제 확인창의 루트. 비워두면 삭제가 막힌다 — 확인 없이 지울 수는 없으므로.")]
    [SerializeField] GameObject deleteModal;

    [Tooltip("삭제 확인 본문. ★코드가 월드 이름을 넣어 .text 를 쓰므로 LocalizedLabel 을 붙이지 말 것.")]
    [SerializeField] TMP_Text deleteMessageText;

    [SerializeField] Button deleteConfirmButton;   // "삭제"
    [SerializeField] Button deleteCancelButton;    // "취소"
    [SerializeField] Button deleteBackdropButton;  // 딤 영역 클릭 시 취소

    [Header("목록이 비었을 때")]
    [Tooltip("월드가 하나도 없을 때만 켜지는 안내 줄(RowContainer/EmptyRow). 문구는 씬 텍스트 그대로.")]
    [SerializeField] GameObject emptyRow;

    [Tooltip("이 패널이 떠 있는 동안 가려야 할 메인메뉴 세로 버튼 목록(게임 시작/옵션/제작진/게임 종료). 비워두면 무시.")]
    [SerializeField] GameObject mainMenuList;

    [Tooltip("이 패널이 떠 있는 동안 같이 가려야 할 메인메뉴 장식 요소(로고/언더라인/태그라인 등) — 목록과 겹쳐 보이는 것 방지.")]
    [SerializeField] GameObject[] hideWhileOpen;

    [Tooltip("슬롯 확정 후 로딩을 거쳐 진입할 실제 플레이 씬 이름.")]
    [SerializeField] string gameplaySceneName = "World";

    [Tooltip("신규 월드 생성 시 경유할 프롤로그 씬 이름. 비워두면 gameplaySceneName으로 바로 진입.")]
    [SerializeField] string prologueSceneName = "Prologue";

    // ── 안내 문구 색 (설정창 계열과 맞춤) ──────────────────────────────
    static readonly Color HintNeutral = new Color(0.604f, 0.604f, 0.604f, 1f);   // #9A9A9A
    static readonly Color HintError   = new Color(1f, 0.565f, 0.518f, 1f);       // #FF9084

    readonly List<WorldSelectRow> _spawnedRows = new();
    readonly HashSet<string> _takenNames = new();   // Normalize 를 거친 기존 월드 이름

    // 아직 프롤로그를 안 본 슬롯. meta.needsPrologue 를 목록 갱신 때 모아 둔다.
    readonly HashSet<string> _prologueSlots = new();
    WorldSelectRow _selectedRow;

    bool _createStyled, _deleteStyled, _hiding, _menuRefsReady;
    string _pendingDeleteSlotId;

    // 패널이 떠 있는 동안 같이 가려야 하는데 hideWhileOpen 에 안 들어간 것들.
    static readonly string[] ExtraHideNames = { "VersionChip" };
    readonly List<GameObject> _extraHide = new();

    bool DeleteModalOpen => MenuPanelAnim.IsOpen(deleteModal);
    bool CreateModalOpen => MenuPanelAnim.IsOpen(createModal);

    void Awake()
    {
        // mainMenuList는 예전에 에디터 빌더가 "MenuList"를 통째로
        // 지우고 새로 만든다 — 이 WorldSelectUI를 다시 빌드하지 않은 채로 그 builder를
        // 나중에 또 돌리면 여기 직렬화된 참조가 파괴된 옛 GameObject를 가리키게 되어(=null
        // 취급) 메인메뉴 버튼들이 안 가려지는 버그가 난다. 빌드 순서에 의존하지 않도록
        // 참조가 비어있으면 이름으로 직접 찾아 자가 복구한다.
        EnsureMenuRefs();

        // panelRoot는 보통 이 컴포넌트와 같은 GameObject — 에디터 빌더가 이미 비활성 상태로
        // 저장해두므로 여기서 다시 SetActive(false)하면 Show()가 막 활성화한 직후 자기 자신을
        // 도로 꺼버리는 꼴이 된다. 그래서 Awake에서는 건드리지 않는다.
        // ★소리는 '누른 순간'에만 낸다. 그래서 버튼은 Close~ 를 직접 물지 않고 OnClick~ 을 문다 —
        //   CloseCreateModal / CloseDeleteModal 은 패널을 닫을 때(Hide) 나 생성 성공 뒤에도
        //   불리는 상태 정리용이라, 거기에 소리를 넣으면 누르지도 않았는데 딸깍 소리가 난다.
        if (createRowButton != null) createRowButton.onClick.AddListener(OpenCreateModal);
        if (confirmCreateButton != null) confirmCreateButton.onClick.AddListener(OnClickCreateNewWorld);
        if (modalBackdropButton != null) modalBackdropButton.onClick.AddListener(OnClickCloseCreate);
        if (cancelCreateButton != null) cancelCreateButton.onClick.AddListener(OnClickCloseCreate);
        if (enterButton != null) enterButton.onClick.AddListener(OnClickEnter);
        if (deleteButton != null) deleteButton.onClick.AddListener(OnClickDelete);

        if (deleteConfirmButton != null) deleteConfirmButton.onClick.AddListener(ConfirmDelete);
        if (deleteCancelButton != null) deleteCancelButton.onClick.AddListener(OnClickCloseDelete);
        if (deleteBackdropButton != null) deleteBackdropButton.onClick.AddListener(OnClickCloseDelete);
        // 모달은 반드시 닫힌 채로 시작한다. 누군가 켠 채로 씬을 저장하면
        // 월드 목록이 창에 덮인 상태로 열린다(삭제 확인창은 최근에 만든 것이라 특히 쉽다).
        if (createModal != null) createModal.SetActive(false);
        if (deleteModal != null) deleteModal.SetActive(false);

        // 딤은 '뒤를 막고 누르면 닫기'만 한다. 눌림 축소가 붙으면 화면 전체가 줄어든다.
        MenuModalStyle.MakeBackdrop(modalBackdropButton);
        MenuModalStyle.MakeBackdrop(deleteBackdropButton);

        // ★씬에는 이 둘이 '비활성(m_Interactable = 0)'으로 저장돼 있다 —
        //   예전엔 월드를 고를 때만 코드가 켜 줬기 때문이다. 이제는 항상 눌리게 두고
        //   RequireSelection() 이 안 되는 이유를 말해 주므로, 여기서 한 번 켜 둔다.
        //   (이 줄이 없으면 씬의 false 가 그대로 남아 버튼이 영영 안 눌린다)
        if (deleteButton != null) deleteButton.interactable = true;
        if (enterButton != null) enterButton.interactable = true;
        if (confirmCreateButton != null) confirmCreateButton.interactable = true;

        // 하단 액션 바 두 버튼의 눌림 연출을 바로잡는다(아래 메서드 주석 참고).
        FixPressLook(deleteButton, "Btn_Delete_Border", MenuModalStyle.BorderQuiet);
        FixPressLook(enterButton,  "Btn_Enter_Border",  MenuModalStyle.BorderMain);

        if (newWorldNameInput != null)
        {
            newWorldNameInput.onValueChanged.AddListener(OnNameChanged);
            newWorldNameInput.onSubmit.AddListener(OnNameSubmit);

            // 길이 제한이 없으면 목록에서 옆 칸(날짜/레벨)까지 밀고 들어간다.
            newWorldNameInput.characterLimit = WorldNameRules.MaxLength;
            newWorldNameInput.lineType = TMP_InputField.LineType.SingleLine;
            // 붙여넣기로 들어오는 줄바꿈·꺾쇠를 애초에 막는다(뒤에서 Sanitize 가 또 거르지만,
            // 화면에 한 번이라도 보였다가 사라지면 사용자는 글자를 잃었다고 느낀다).
            newWorldNameInput.onValidateInput = ValidateChar;
        }
    }

    void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;
        if (_hiding) return;   // 닫히는 중에는 입력을 받지 않는다

        // 삭제 확인창이 떠 있으면 그쪽이 먼저다. Enter = 삭제, ESC = 취소.
        // 키로 닫아도 버튼으로 닫은 것과 같은 소리가 나야 한다 — 그래서 OnClick~ 을 그대로 탄다.
        if (DeleteModalOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) OnClickCloseDelete();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmDelete();
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // 한글 조합 중이어도 ESC 는 그대로 창을 닫는다. 글자는 Backspace 로 지우지
        // ESC 로 지우지 않으므로, 입력 중 ESC = "그만두기" 로 읽는 게 맞다.
        if (CreateModalOpen) OnClickCloseCreate();
        else { GameSfx.Play(SfxId.MenuClick); Hide(); }
    }

    /// <summary>IME 가 아직 확정하지 않은 글자를 들고 있는가.</summary>
    bool IsComposing =>
        newWorldNameInput != null && newWorldNameInput.isFocused &&
        !string.IsNullOrEmpty(Input.compositionString);

    // ==================================================================
    //  열고 닫기
    // ==================================================================
    public void Show()
    {
        _hiding = false;
        MenuPanelAnim.Open(panelRoot);
        SetMainMenuVisible(false);
        Refresh();
    }

    public void Hide()
    {
        if (_hiding) return;
        _hiding = true;

        // 안쪽 창들은 연출 없이 바로 닫는다. 패널 자신이 곧 꺼지는데 같은 길이의 연출을
        // 태우면, 패널이 먼저 꺼져 그 코루틴이 죽고 창이 '켜진 채 투명한' 상태로 남는다.
        CloseCreateModal(instant: true);
        CloseDeleteModal(instant: true);

        // 메뉴는 먼저 켜 둔다. 패널이 옅어지면서 그 뒤로 드러나야 자연스럽다.
        SetMainMenuVisible(true);
        MenuPanelAnim.Close(this, panelRoot, () => _hiding = false);
    }

    void SetMainMenuVisible(bool visible)
    {
        EnsureMenuRefs();

        if (mainMenuList != null) mainMenuList.SetActive(visible);
        foreach (var go in _extraHide)
            if (go != null) go.SetActive(visible);

        if (hideWhileOpen == null) return;
        foreach (var go in hideWhileOpen)
            if (go != null) go.SetActive(visible);
    }

    /// <summary>가릴 대상들을 이름으로 찾아 채운다.
    ///
    /// ★includeInactive 를 켜야 한다 — 이 패널이 아직 꺼져 있는 동안 호출되면
    ///   기본 GetComponentInParent 는 자기 자신이 비활성이라 null 을 돌려준다.
    ///   그래서 메뉴가 안 가려진 채 월드 목록과 겹쳐 보였다.
    ///   (게다가 MenuList 는 씬에서 WorldSelectPanel 보다 뒤 순서라 위에 그려진다 —
    ///    가리지 못하면 그냥 겹치는 게 아니라 목록을 덮는다)</summary>
    void EnsureMenuRefs()
    {
        if (_menuRefsReady) return;

        var canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null) return;              // 아직 못 찾았다 — 다음에 다시 시도
        var root = canvas.rootCanvas.transform;
        _menuRefsReady = true;

        // mainMenuList 는 예전에 에디터 빌더가 "MenuList"를 통째로 지우고 새로 만들었다.
        // 그래서 씬에 직렬화된 참조가 파괴된 옛 오브젝트를 가리키는 일이 있었다(=null 취급).
        if (mainMenuList == null) mainMenuList = root.Find("MenuList")?.gameObject;

        // hideWhileOpen 에 빠져 있는 장식. 패널 제목과 겹쳐 보여서 같이 가린다
        // (인스펙터를 고치지 않아도 되도록 이름으로 찾는다).
        foreach (var n in ExtraHideNames)
        {
            var t = root.Find(n);
            if (t != null) _extraHide.Add(t.gameObject);
        }
    }

    // ==================================================================
    //  목록
    // ==================================================================
    void Refresh()
    {
        SelectRow(null);   // 지우기 전에 선택부터 푼다(파괴 예정 오브젝트를 건드리지 않게)

        // ★Destroy 는 이번 프레임 '끝'에 처리된다. 그 사이 옛 줄들은 여전히
        //   VerticalLayoutGroup 의 자식이라, 새로 만든 줄과 함께 배치돼 한 프레임 동안
        //   목록이 두 배로 늘어난 채 그려진다(삭제 직후 깜빡임). 끄면 레이아웃이 즉시 무시한다.
        foreach (var row in _spawnedRows)
        {
            if (row == null) continue;
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }
        _spawnedRows.Clear();

        var slots = SaveSlotManager.Instance != null
            ? SaveSlotManager.Instance.ListSlots()
            : new List<SaveSlotMeta>();

        // 중복 판정을 입력 한 글자마다 디스크를 훑어 하면 타이핑이 끊긴다. 목록을
        // 새로 읽는 지금 한 번만 모아 둔다. 프롤로그 여부도 같은 김에 모은다.
        _takenNames.Clear();
        _prologueSlots.Clear();
        foreach (var meta in slots)
        {
            _takenNames.Add(WorldNameRules.Normalize(meta.worldName));
            if (meta.needsPrologue) _prologueSlots.Add(meta.slotId);
        }

        if (rowPrefab != null && rowContainer != null)
        {
            foreach (var meta in slots)
            {
                var row = Instantiate(rowPrefab, rowContainer);
                string slotId = meta.slotId;
                // 소리는 '누른 순간'에만. SelectRow 자체는 목록 갱신·생성 직후에도 불린다.
                row.Set(meta,
                        () => { GameSfx.Play(SfxId.MenuClick); SelectRow(row); },
                        () => EnterSlot(slotId));
                _spawnedRows.Add(row);
            }
        }

        if (emptyRow != null)
        {
            emptyRow.SetActive(slots.Count == 0);
            emptyRow.transform.SetAsFirstSibling();
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

        // ★버튼을 끄지 않는다. 회색으로 죽어 있으면 눌러도 아무 일이 없어서
        //   "고장 났나?" 가 되는데, 정작 알려줘야 할 것(월드를 고르라는 말)은 못 한다.
        //   대신 눌리게 두고 아래 RequireSelection() 이 이유를 말해 준다.
    }

    /// <summary>선택된 월드가 있어야 하는 동작의 문지기. 없으면 왜 안 되는지 알려주고 false.</summary>
    bool RequireSelection()
    {
        if (_selectedRow != null) return true;

        if (_spawnedRows.Count > 0)
        {
            ToastManager.Warning(Loc.Get("월드를 선택해 주세요."));
            return false;
        }

        ToastManager.Warning(Loc.Get("월드를 생성해 주세요."));
        // 어디를 눌러야 하는지 눈으로 짚어 준다. 설정창이 안내 문구를 튕길 때 쓰는 그 연출.
        // (localScale 만 건드리므로 세로 레이아웃이 다시 계산되지 않는다 — 옆 줄이 안 밀린다)
        if (createRowButton != null)
            GameSettingsUI.UITween.Punch((RectTransform)createRowButton.transform);
        return false;
    }

    void OnClickEnter()
    {
        GameSfx.Play(SfxId.MenuClick);
        if (!RequireSelection()) return;
        EnterSlot(_selectedRow.SlotId);
    }

    // ==================================================================
    //  삭제 — 되돌릴 수 없으니 반드시 한 번 물어본다
    // ==================================================================
    void OnClickDelete()
    {
        GameSfx.Play(SfxId.MenuClick);
        if (!RequireSelection()) return;

        // 확인창이 연결돼 있지 않으면 지우지 않는다. 되돌릴 수 없는 동작을 확인 없이
        // 실행하느니, 세팅이 빠졌다고 알리는 편이 낫다.
        if (deleteModal == null)
        {
            Debug.LogError("[WorldSelectUI] 삭제 확인창(deleteModal)이 연결되지 않아 삭제를 막았습니다. " +
                           "인스펙터에서 연결해 주세요.", this);
            ToastManager.Error(Loc.Get("삭제 확인창을 찾을 수 없습니다."));
            return;
        }

        _pendingDeleteSlotId = _selectedRow.SlotId;

        // 본문만 코드가 쓴다(월드 이름이 들어가므로). 나머지 문구는 전부 씬 그대로.
        if (deleteMessageText != null)
            deleteMessageText.text = string.Format(Loc.Get("{0} 월드를 삭제할까요?"),
                                                   WorldNameRules.Display(_selectedRow.WorldName));

        StyleDeleteModal();
        MenuPanelAnim.Open(deleteModal);
        // 창이 뜰 때 따로 '패널 여는 소리'를 얹지 않는다. 버튼을 눌러서 열린 것이므로
        // 다른 버튼과 같은 소리가 나야 하는데, 두 소리가 겹치면 이 버튼만 유독 다르게 들린다.
    }

    void CloseDeleteModal(bool instant = false)
    {
        _pendingDeleteSlotId = null;
        if (instant) MenuPanelAnim.CloseInstant(deleteModal);
        else MenuPanelAnim.Close(this, deleteModal);
    }

    void OnClickCloseDelete()
    {
        GameSfx.Play(SfxId.MenuClick);
        CloseDeleteModal();
    }

    void ConfirmDelete()
    {
        GameSfx.Play(SfxId.MenuClick);
        string slotId = _pendingDeleteSlotId;
        CloseDeleteModal();
        if (string.IsNullOrEmpty(slotId)) return;

        SaveSlotManager.Instance?.DeleteSlot(slotId);
        Refresh();
        ToastManager.Success(Loc.Get("월드를 삭제했습니다."));
    }

    // ==================================================================
    //  이름 입력 모달
    // ==================================================================
    void OpenCreateModal()
    {
        GameSfx.Play(SfxId.MenuClick);
        if (newWorldNameInput != null) newWorldNameInput.text = string.Empty;
        if (createModal != null) createModal.SetActive(true);
        StyleCreateModal();           // 배치를 먼저 잡고 연출을 태운다
        MenuPanelAnim.Open(createModal);
        OnNameChanged();        // 비운 직후라 '결정'은 꺼진 채로 시작한다
        FocusNameInput();
    }

    void CloseCreateModal(bool instant = false)
    {
        ReleaseTextInput();
        if (instant) MenuPanelAnim.CloseInstant(createModal);
        else MenuPanelAnim.Close(this, createModal);
    }

    void OnClickCloseCreate()
    {
        GameSfx.Play(SfxId.MenuClick);
        CloseCreateModal();
    }

    /// <summary>입력칸을 확실히 끄고 IME 를 놓아준다.
    /// 모달 오브젝트만 꺼버리면 TMP_InputField 의 정리 코드가 돌지 않아 IME 조합 모드가
    /// 켜진 채로 남는다. 그 상태로 씬을 넘어가면 게임 안 첫 입력들이 조합기로 먹혀
    /// "한/영 켜고 이름 짓고 들어가면 키가 안 먹는다"가 된다. 나가는 길은 전부 여기를 지난다.</summary>
    void ReleaseTextInput()
    {
        if (newWorldNameInput != null && newWorldNameInput.isActiveAndEnabled)
            newWorldNameInput.DeactivateInputField();

        var es = EventSystem.current;
        if (es != null && newWorldNameInput != null &&
            es.currentSelectedGameObject == newWorldNameInput.gameObject)
            es.SetSelectedGameObject(null);

        Input.imeCompositionMode = IMECompositionMode.Auto;
    }

    void FocusNameInput()
    {
        if (newWorldNameInput == null || !isActiveAndEnabled) return;
        StartCoroutine(FocusNextFrame());
    }

    // 모달을 켠 그 프레임에 포커스를 주면, EventSystem 이 아직 직전 선택("+ 신규 월드
    // 생성하기" 버튼)을 정리하는 중이라 곧바로 풀린다. 한 프레임 뒤에 잡는다.
    IEnumerator FocusNextFrame()
    {
        yield return null;
        if (!CreateModalOpen || newWorldNameInput == null) yield break;

        EventSystem.current?.SetSelectedGameObject(newWorldNameInput.gameObject);
        newWorldNameInput.ActivateInputField();
        newWorldNameInput.caretPosition = newWorldNameInput.text.Length;
    }

    // 붙여넣기·조합 확정으로 들어오는 글자 중 이름에 남으면 곤란한 것들을 문 앞에서 막는다.
    static char ValidateChar(string text, int charIndex, char added)
    {
        if (char.IsControl(added)) return '\0';          // 줄바꿈·탭
        if (added == '<' || added == '>') return '\0';   // TMP 가 서식 태그로 읽는다
        return added;
    }

    void OnNameChanged(string _ = null)
    {
        string raw = newWorldNameInput != null ? newWorldNameInput.text : null;
        string name = WorldNameRules.Sanitize(raw);

        bool empty = string.IsNullOrEmpty(name);
        bool duplicate = !empty && WorldNameRules.IsTaken(_takenNames, name);

        // ★'결정'을 끄지 않는다. 회색으로 죽여 두면, 이름을 안 친 사람이 눌렀을 때
        //   아무 일도 안 일어나고 이유도 안 알려준다(삭제·게임 시작에서 겪은 그 문제).
        //   눌리게 두고 CreateWorldNow() 가 입력칸 아래 안내로 이유를 말해 준다.

        // 비어 있을 땐 아무 말도 하지 않는다 — 아직 아무것도 안 친 사람을 나무라는 꼴이 된다.
        if (duplicate) SetHint(Loc.Get("이미 같은 이름의 월드가 있습니다."), true);
        else SetHint(null, false);

        if (nameCounterText != null) nameCounterText.text = $"{name.Length} / {WorldNameRules.MaxLength}";
    }

    // Enter 로 확정. 한글은 마지막 글자를 확정하는 데도 Enter 를 쓰므로, 조합이 남아 있으면
    // 이번 Enter 는 그쪽 몫으로 넘기고 포커스만 돌려준다(두 번째 Enter 부터 생성).
    void OnNameSubmit(string _)
    {
        if (!string.IsNullOrEmpty(Input.compositionString)) { FocusNameInput(); return; }
        OnClickCreateNewWorld();
    }

    void OnClickCreateNewWorld()
    {
        GameSfx.Play(SfxId.MenuClick);
        if (SaveSlotManager.Instance == null) return;

        // 마우스로 '결정'을 누르는 순간에도 입력칸은 포커스를 쥐고 있고, 조합 중이던 마지막
        // 글자(예: "월드"의 '드')는 아직 .text 에 들어오지 않았다. 먼저 확정시키고 한 프레임
        // 뒤에 읽어야 이름이 잘리지 않는다.
        if (IsComposing) { StartCoroutine(CommitThenCreate()); return; }
        CreateWorldNow();
    }

    IEnumerator CommitThenCreate()
    {
        newWorldNameInput.DeactivateInputField();   // 조합 확정
        yield return null;
        CreateWorldNow();
    }

    void CreateWorldNow()
    {
        if (SaveSlotManager.Instance == null) return;

        string name = WorldNameRules.Sanitize(newWorldNameInput != null ? newWorldNameInput.text : null);

        // 버튼을 거치지 않는 길(Enter 제출 등)로도 들어오므로 여기서 한 번 더 본다.
        if (string.IsNullOrEmpty(name))
        {
            SetHint(Loc.Get("월드 이름을 입력해 주세요."), true);
            FocusNameInput();
            return;
        }
        if (WorldNameRules.IsTaken(_takenNames, name))
        {
            SetHint(Loc.Get("이미 같은 이름의 월드가 있습니다."), true);
            FocusNameInput();
            return;
        }

        var meta = SaveSlotManager.Instance.CreateSlot(name);
        CloseCreateModal();

        // ★만들자마자 들어가지 않는다. 목록으로 돌아와 방금 만든 월드를 골라 둘 뿐이고,
        //   실제 진입은 '게임 시작'을 눌러야 일어난다. 이름을 확정하는 것과 게임을 시작하는
        //   것은 다른 결정이라, 한 번의 클릭에 둘 다 묶여 있으면 되돌릴 방법이 없다.
        Refresh();
        SelectSlot(meta.slotId);
    }

    void SelectSlot(string slotId)
    {
        foreach (var row in _spawnedRows)
            if (row != null && row.SlotId == slotId) { SelectRow(row); return; }
    }

    void EnterSlot(string slotId, string overrideScene = null)
    {
        if (SaveSlotManager.Instance == null) return;
        if (!SaveSlotManager.Instance.LoadSlot(slotId))
        {
            // 폴더가 지워졌거나 파일이 깨진 경우. 조용히 아무 일도 없으면 버튼이 고장 난 줄 안다.
            ToastManager.Error(Loc.Get("월드를 불러오지 못했습니다."));
            Refresh();
            return;
        }

        // 아직 프롤로그를 안 본 월드는 프롤로그부터. 예전엔 "방금 만들었으니 프롤로그" 였는데,
        // 생성과 진입이 분리되면서 그 순간을 알 수 없게 되어 슬롯 자신에게 묻는다.
        string scene = overrideScene;
        if (string.IsNullOrEmpty(scene))
            scene = (_prologueSlots.Contains(slotId) && !string.IsNullOrEmpty(prologueSceneName))
                    ? prologueSceneName : gameplaySceneName;

        ReleaseTextInput();   // IME 를 켠 채로 씬을 넘기지 않는다
        CoreUtilities.LoadViaLoading(scene);
    }


    // ==================================================================
    //  겉모습 맞추기 — 색·크기·위치만 손댄다. 글자는 전부 씬 것 그대로.
    // ==================================================================

    void SetHint(string message, bool isError)
    {
        if (nameHintText == null) return;
        nameHintText.text = message ?? string.Empty;
        nameHintText.color = isError ? HintError : HintNeutral;
    }

    // '월드 생성' 창은 씬에 구워져 있고, 확인창들과 생김새가 달랐다. 같은 화면에서 창마다
    // 톤이 다른 게 제일 어설퍼 보이므로 처음 열 때 한 번 규격에 맞춘다.
    // 씬 파일은 건드리지 않는다(팀원과 자주 충돌하는 큰 씬이라).
    void StyleCreateModal()
    {
        if (_createStyled || newWorldNameInput == null) return;
        var input = (RectTransform)newWorldNameInput.transform;
        var box = input.parent as RectTransform;
        if (box == null) return;
        _createStyled = true;

        const float BoxW = 880f, BoxH = 300f;
        float halfH  = BoxH * 0.5f;
        float inputY = halfH - 116f;                        //  34
        float sepY   = -halfH + MenuModalStyle.SepInset;    // -44
        float btnY   = -halfH + MenuModalStyle.BtnInset;    // -88

        MenuModalStyle.ApplyBackdrop(createModal != null ? createModal.transform : null);
        MenuModalStyle.ApplyBox(box, new Vector2(BoxW, BoxH));
        MenuModalStyle.ApplyBoxTicks(box);
        MenuModalStyle.ApplyStrip(box.Find("LabelStrip") as RectTransform,
                                  new Vector2(420f, MenuModalStyle.StripH),
                                  halfH - MenuModalStyle.StripInset);
        MenuModalStyle.ApplySep(box.Find("Sep") as RectTransform, sepY, BoxW - 120f);

        // 입력칸과 그 테두리
        input.anchoredPosition = new Vector2(input.anchoredPosition.x, inputY);
        var inputBorder = box.Find("InputBorder") as RectTransform;
        if (inputBorder != null)
            inputBorder.anchoredPosition = new Vector2(inputBorder.anchoredPosition.x, inputY);

        // 입력칸 아래 한 줄 — 왼쪽에 못 만드는 이유, 오른쪽에 글자 수.
        // 둘 다 씬에 만들어 연결한 것이라 여기서는 자리와 크기만 잡아준다.
        float inputW = input.sizeDelta.x;
        float lineY  = inputY - input.sizeDelta.y * 0.5f - 16f;
        if (nameHintText != null)
        {
            Place(nameHintText.rectTransform,
                  new Vector2(-inputW * 0.5f + 185f, lineY), new Vector2(370f, 26f));
            nameHintText.alignment = TextAlignmentOptions.Left;
            nameHintText.fontSize = 20f;
        }
        if (nameCounterText != null)
        {
            Place(nameCounterText.rectTransform,
                  new Vector2(inputW * 0.5f - 40f, lineY), new Vector2(80f, 26f));
            nameCounterText.alignment = TextAlignmentOptions.Right;
            nameCounterText.fontSize = 19f;
            nameCounterText.color = HintNeutral;
        }

        // 취소가 왼쪽, 결정이 오른쪽 — 확인창들과 손 가는 방향을 맞춘다.
        MenuModalStyle.ApplyButton(cancelCreateButton, box.Find("Btn_Cancel_Border") as RectTransform,
                                   new Vector2(-MenuModalStyle.BtnOffsetX, btnY),
                                   danger: false, primary: false);
        MenuModalStyle.ApplyButton(confirmCreateButton, box.Find("Btn_Confirm_Border") as RectTransform,
                                   new Vector2(MenuModalStyle.BtnOffsetX, btnY),
                                   danger: false, primary: true);
    }

    // 삭제 확인창. 씬에서 어떤 크기로 만들어 뒀든 여기서 규격에 맞춘다.
    void StyleDeleteModal()
    {
        if (_deleteStyled || deleteModal == null) return;
        _deleteStyled = true;

        MenuModalStyle.ApplyBackdrop(deleteModal.transform);

        var box = deleteModal.transform.Find("Box") as RectTransform;
        if (box == null) return;

        const float BoxW = 880f, BoxH = 320f;
        float halfH = BoxH * 0.5f;
        float btnY  = -halfH + MenuModalStyle.BtnInset;

        MenuModalStyle.ApplyBox(box, new Vector2(BoxW, BoxH));
        MenuModalStyle.ApplyBoxTicks(box);
        MenuModalStyle.ApplyStrip(box.Find("LabelStrip") as RectTransform,
                                  new Vector2(420f, MenuModalStyle.StripH),
                                  halfH - MenuModalStyle.StripInset);
        MenuModalStyle.ApplySep(box.Find("Sep") as RectTransform,
                                -halfH + MenuModalStyle.SepInset, BoxW - 120f);

        // 본문은 두 줄까지 잡아둔다 — 월드 이름이 최대 길이(20자)면 한 줄에 안 들어가는데,
        // 한 줄뿐이면 "…월드를 삭제할" 처럼 뒤가 잘려 무슨 말인지 알 수 없게 된다.
        if (deleteMessageText != null)
        {
            Place(deleteMessageText.rectTransform, new Vector2(0f, 34f), new Vector2(BoxW - 120f, 76f));
            deleteMessageText.alignment = TextAlignmentOptions.Center;
            deleteMessageText.textWrappingMode = TextWrappingModes.Normal;
        }
        Place(box.Find("SubMessage") as RectTransform, new Vector2(0f, -26f), new Vector2(BoxW - 120f, 34f));

        MenuModalStyle.ApplyButton(deleteCancelButton, box.Find("Btn_Cancel_Border") as RectTransform,
                                   new Vector2(-MenuModalStyle.BtnOffsetX, btnY),
                                   danger: false, primary: false);
        MenuModalStyle.ApplyButton(deleteConfirmButton, box.Find("Btn_Confirm_Border") as RectTransform,
                                   new Vector2(MenuModalStyle.BtnOffsetX, btnY),
                                   danger: true, primary: true);
    }

    /// <summary>하단 액션 바 버튼이 눌릴 때 제대로 보이게 두 가지를 고친다.
    ///   1) 피벗이 우하단이라 왼쪽·위만 말려들어갔다 → 가운데로.
    ///   2) 테두리가 버튼보다 2px 큰 별개의 판이라, 버튼만 줄면 그 판이 드러나 회색 띠가
    ///      생겼다 → 버튼 자신의 Outline 으로 옮겨 같이 줄어들게.
    /// 둘 다 씬 저장 없이 실행 중에 교정된다.</summary>
    void FixPressLook(Button btn, string borderName, Color borderColor)
    {
        if (btn == null) return;
        CenterPivot(btn);

        var border = panelRoot != null ? panelRoot.transform.Find(borderName) as RectTransform : null;
        MenuModalStyle.MoveBorderToOutline(btn.GetComponent<Image>(), border, borderColor);
    }

    /// <summary>화면상 위치는 그대로 두고 피벗만 가운데로 옮긴다.
    /// 눌림 연출(UIButtonPressEffect)이 localScale 로 동작하는데, 스케일은 피벗을 중심으로
    /// 걸리기 때문이다. 피벗이 구석에 있으면 "가운데로 작아졌다 커지는" 게 아니라
    /// 반대쪽 두 변만 안으로 말려들어가 눌린 것처럼 안 보인다.
    /// (테두리는 스케일되지 않으므로 건드리지 않는다 — 그대로 제자리에 남아야 한다)</summary>
    static void CenterPivot(Button btn)
    {
        if (btn == null) return;
        var rt = (RectTransform)btn.transform;

        Vector2 p = rt.pivot;
        if (Mathf.Approximately(p.x, 0.5f) && Mathf.Approximately(p.y, 0.5f)) return;

        // 피벗을 옮기면 사각형이 그만큼 밀린다. 같은 크기만큼 반대로 당겨 제자리에 둔다.
        Rect r = rt.rect;
        rt.anchoredPosition += new Vector2((0.5f - p.x) * r.width, (0.5f - p.y) * r.height);
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
