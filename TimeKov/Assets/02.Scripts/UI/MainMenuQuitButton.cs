// =====================================================================
// MainMenuQuitButton.cs
// 메인메뉴 '게임 종료' 항목. 곧바로 끄지 않고 씬의 확인창(QuitConfirmModal)을 띄운다.
//
// [원래 어땠나]
//   씬에는 확인창이 "게임을 종료하시겠습니까? / 예 / 아니요" 까지 다 만들어져 있고,
//   그 버튼들은 이 스크립트의 ConfirmQuit() / CancelQuit() 를 부르도록 연결돼 있었다.
//   그런데 스크립트에는 그 두 메서드가 없었다. 결과:
//     - '게임 종료'를 누르면 확인창을 건너뛰고 그 자리에서 게임이 꺼졌다
//     - 확인창은 열릴 일이 없었고, 열렸어도 예/아니오가 죽은 연결이라 무반응이었다
//   메서드를 채워 넣어 씬 연결을 그대로 살렸다(씬 파일은 건드리지 않는다).
//
// [겉모습]
//   MenuModalStyle 로 월드 삭제 확인창과 같은 규격으로 다시 칠한다.
//   ★색·크기·장식만 바꾼다. 문구는 전부 씬 오브젝트 그대로다 —
//     코드가 만든 라벨은 팀원의 번역 문구 수집에서 빠지기 때문.
// =====================================================================

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MainMenuQuitButton : MonoBehaviour
{
    [Tooltip("종료 확인창(QuitConfirmModal). 비워두면 같은 캔버스에서 이름으로 찾는다.")]
    [SerializeField] private GameObject confirmPanel;

    bool _styled;

    /// <summary>확인창이 떠 있는가(닫히는 연출 중은 제외). 다른 곳에서 ESC 를 가로채지
    /// 않게 하는 데 쓴다.</summary>
    public bool IsConfirmOpen => MenuPanelAnim.IsOpen(confirmPanel);

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(ShowConfirm);

        EnsureConfirmPanel();
        // 누군가 켠 채로 저장하면 메인메뉴가 확인창에 덮인 채로 시작한다. 확실히 닫아둔다.
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    private void Update()
    {
        if (!IsConfirmOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape)) CancelQuit();
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmQuit();
    }

    // 인스펙터 연결 없이도 동작하게 한다 — 이 버튼과 확인창은 같은 캔버스 아래 형제다.
    // (WorldSelectUI 의 mainMenuList 자가복구와 같은 방식)
    private void EnsureConfirmPanel()
    {
        if (confirmPanel != null) return;

        // includeInactive — 이 버튼이 꺼진 상태에서 불려도 부모 캔버스를 찾도록.
        var canvas = GetComponentInParent<Canvas>(true);
        if (canvas != null) confirmPanel = canvas.rootCanvas.transform.Find("QuitConfirmModal")?.gameObject;

        if (confirmPanel == null)
            Debug.LogWarning("[MainMenuQuitButton] QuitConfirmModal 을 찾지 못했습니다. " +
                             "확인 없이 바로 종료합니다.", this);
    }

    /// <summary>'게임 종료' 클릭 — 확인창을 띄운다.</summary>
    public void ShowConfirm()
    {
        GameSfx.Play(SfxId.MenuClick);
        if (confirmPanel == null) { QuitNow(); return; }   // 확인창이 없으면 예전 동작 그대로

        StyleOnce();                       // 배치를 먼저 잡고 연출을 태운다
        MenuPanelAnim.Open(confirmPanel);
        // 창이 뜰 때 '패널 여는 소리'를 따로 얹지 않는다 — 버튼을 눌러서 열린 것이라
        // 다른 메뉴 항목과 같은 소리가 나야 한다(월드 삭제 확인창과 동일한 규칙).
    }

    /// <summary>확인창의 '예'. (씬에서 직접 연결되어 있다)</summary>
    public void ConfirmQuit()
    {
        GameSfx.Play(SfxId.MenuClick);
        QuitNow();
    }

    /// <summary>확인창의 '아니요' / 딤 영역 클릭 / ESC. (씬에서 직접 연결되어 있다)</summary>
    public void CancelQuit()
    {
        GameSfx.Play(SfxId.MenuClick);
        MenuPanelAnim.Close(this, confirmPanel);
    }

    // 월드 삭제 확인창과 같은 규격으로 맞춘다. 이름으로 찾으므로 씬 구조가 바뀌면
    // 해당 부분만 조용히 넘어간다(창은 그대로 뜬다).
    private void StyleOnce()
    {
        if (_styled || confirmPanel == null) return;
        _styled = true;

        MenuModalStyle.ApplyBackdrop(confirmPanel.transform);
        // 딤은 '뒤를 막고 누르면 닫기'만 한다. 눌림 축소가 붙으면 화면 전체가 줄어든다.
        MenuModalStyle.MakeBackdrop(confirmPanel.transform.Find("Backdrop")?.GetComponent<Button>());

        var box = confirmPanel.transform.Find("Box") as RectTransform;
        if (box == null) return;

        MenuModalStyle.ApplyBox(box);
        MenuModalStyle.ApplyBoxTicks(box);
        MenuModalStyle.ApplyStrip(box.Find("LabelStrip") as RectTransform);
        MenuModalStyle.ApplySep(box.Find("Sep") as RectTransform, 10f, box.rect.width - 120f);

        // '예'가 되돌릴 수 없는 쪽. 씬에서는 예가 왼쪽인데, 삭제 확인창과 손 가는 방향을
        // 맞춰 예를 오른쪽으로 옮긴다.
        float y = -68f;
        MenuModalStyle.ApplyButton(box.Find("Btn_No")?.GetComponent<Button>(),
                                   box.Find("Btn_No_Border") as RectTransform,
                                   new Vector2(-MenuModalStyle.BtnOffsetX, y),
                                   danger: false, primary: false);
        MenuModalStyle.ApplyButton(box.Find("Btn_Yes")?.GetComponent<Button>(),
                                   box.Find("Btn_Yes_Border") as RectTransform,
                                   new Vector2(MenuModalStyle.BtnOffsetX, y),
                                   danger: true, primary: true);
    }

    private void QuitNow()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
