using UnityEngine;

// 상자 상호작용 프롬프트(잠긴 상자 / 해제 중 / 해제 완료 / 열린 상자).
//
// [08-02] 런타임 자체생성 -> 씬 실물 오브젝트로 전환. 계층 생성은 ChestPromptUIBuilder(에디터)가 담당한다.
//   패널 구조/버튼 조작은 설비 프롬프트와 완전히 같아서 PromptPanelUI 베이스로 뺐다. 여기 남는 건 상자 전용 상태 문구뿐.
//   DontDestroyOnLoad 도 제거했다(씬 넘나드는 싱글톤은 재입장 시 낡은 상태가 남는 사고 원인).
public class ChestPromptUI : PromptPanelUI
{
    public static ChestPromptUI Instance { get; private set; }

    private ChestInteractable _owner;
    private static bool _warnedMissing;   // 매 프레임 호출되므로 경고는 1회만

    protected override void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        base.Awake();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>씬에 있는 인스턴스를 돌려준다(없으면 에러 로그).</summary>
    public static ChestPromptUI GetOrCreate()
    {
        if (Instance != null) return Instance;
        Instance = FindAnyObjectByType<ChestPromptUI>(FindObjectsInactive.Include);
        if (Instance == null && !_warnedMissing)
        {
            _warnedMissing = true;
            Debug.LogError("[ChestPromptUI] 씬 Canvas 에 상자 프롬프트가 없다. 메뉴 Tools/TIMEKOV/상자 프롬프트 UI 생성 을 실행해라.");
        }
        return Instance;
    }

    public void ShowIdle(ChestInteractable owner, float instantCost,
                         System.Action onF, System.Action onG)
    {
        _owner = owner;
        SetTitle(Loc.Get("잠긴 상자"));
        SetProgress(false);
        SetPrimary("F", Loc.Get("잠금 해제"), onF);
        SetSecondary("G", instantCost > 0 ? InstantLabel(instantCost) : null, onG);
        ShowPanel();
    }

    public void ShowOpening(ChestInteractable owner, float progress, float secsLeft,
                            float instantCost, System.Action onG)
    {
        _owner = owner;
        SetTitle(Loc.Get("해제 중"));
        SetProgress(true, progress, secsLeft);
        SetPrimary(null, null, null);
        SetSecondary("G", InstantLabel(instantCost), onG);
        ShowPanel();
    }

    public void ShowReady(ChestInteractable owner, System.Action onF)
    {
        _owner = owner;
        SetTitle(Loc.Get("해제 완료"));
        SetProgress(false);
        SetPrimary("F", Loc.Get("열어보기"), onF);
        SetSecondary(null, null, null);
        ShowPanel();
    }

    public void ShowOpened(ChestInteractable owner, System.Action onF)
    {
        _owner = owner;
        SetTitle(Loc.Get("열린 상자"));
        SetProgress(false);
        SetPrimary("F", Loc.Get("열어보기"), onF);
        SetSecondary(null, null, null);
        ShowPanel();
    }

    public void HideIfOwner(ChestInteractable chest)
    {
        if (_owner == chest) Hide();
    }

    public override void Hide()
    {
        base.Hide();
        _owner = null;
    }

    private static string InstantLabel(float cost)
        => $"{Loc.Get("즉시 열기")}   <color=#E05050>HP -{Mathf.CeilToInt(cost)}</color>";
}
