using JeffGrawAssets.FlexibleUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상자 상호작용 프롬프트(잠긴 상자 / 해제 중 / 해제 완료 / 열린 상자).
//
// [08-02] 런타임 자체생성 -> 씬 실물 오브젝트로 전환.
//   이전에는 Awake 에서 Canvas 부터 버튼까지 280줄로 만들어냈다. 에디터에 아무것도 없어서
//   위치/색을 못 고치고, 컴포넌트를 붙일 수도 없었다. 계층 생성은 ChestPromptUIBuilder(에디터)가 담당한다.
//
//   DontDestroyOnLoad 도 같이 제거했다: 씬을 넘나들며 살아남는 싱글톤은 재입장 시 낡은 상태가 남는 사고의 원인이라,
//   씬에 실물로 두고 씬과 함께 정리되게 하는 편이 안전하다.
//
//   초기 상태 규칙(HUD/오버레이): 오브젝트는 항상 활성, 표시/숨김은 자식 Panel 만 토글한다.
//   -> Instance 를 항상 찾을 수 있고 등록/코루틴 타이밍이 예측 가능해진다.
public class ChestPromptUI : MonoBehaviour
{
    public static ChestPromptUI Instance { get; private set; }

    [Header("구성 요소 (빌더가 자동 연결)")]
    [Tooltip("표시/숨김 대상. 이 오브젝트만 켜고 끈다.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [Tooltip("해제 중일 때만 보이는 진행 섹션.")]
    [SerializeField] private GameObject progressSection;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text timerText;

    [Header("주 버튼 (F)")]
    [SerializeField] private Button primaryBtn;
    [SerializeField] private TMP_Text primaryKey;
    [SerializeField] private TMP_Text primaryLabel;

    [Header("보조 버튼 (G)")]
    [SerializeField] private Button secondaryBtn;
    [SerializeField] private TMP_Text secondaryKey;
    [SerializeField] private TMP_Text secondaryLabel;

    [Header("배경 블러")]
    [SerializeField] private BlurredImage blur;

    private ChestInteractable _owner;
    private System.Action _onPrimary;
    private System.Action _onSecondary;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 클릭 배선은 런타임에서 건다(에디터 영속 리스너로 저장하지 않는다 = 씬 diff 가 깔끔).
        if (primaryBtn != null) primaryBtn.onClick.AddListener(() => _onPrimary?.Invoke());
        if (secondaryBtn != null) secondaryBtn.onClick.AddListener(() => _onSecondary?.Invoke());

        if (progressSection != null) progressSection.SetActive(false);
        if (panel != null) panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start() => EnsureBlurCamera();

    /// <summary>씬에 있는 인스턴스를 돌려준다(없으면 에러 로그).</summary>
    public static ChestPromptUI GetOrCreate()
    {
        if (Instance != null) return Instance;
        Instance = FindAnyObjectByType<ChestPromptUI>(FindObjectsInactive.Include);
        if (Instance == null)
            Debug.LogError("[ChestPromptUI] 씬 Canvas 에 상자 프롬프트가 없다. 메뉴 Tools/TIMEKOV/상자 프롬프트 UI 생성 을 실행해라.");
        return Instance;
    }

    // 공개 API

    public void ShowIdle(ChestInteractable owner, float instantCost,
                         System.Action onF, System.Action onG)
    {
        _owner = owner;
        EnsureBlurCamera();
        SetTitle("잠긴  상자");
        if (progressSection != null) progressSection.SetActive(false);
        SetPrimary("F", "잠금 해제", onF);
        SetSecondary("G", instantCost > 0
            ? $"즉시 열기   <color=#E05050>HP -{Mathf.CeilToInt(instantCost)}</color>"
            : null, onG);
        Show();
    }

    public void ShowOpening(ChestInteractable owner, float progress, float secsLeft,
                            float instantCost, System.Action onG)
    {
        _owner = owner;
        EnsureBlurCamera();
        SetTitle("해제  중");
        if (progressSection != null) progressSection.SetActive(true);
        if (progressFill != null) progressFill.fillAmount = progress;
        if (timerText != null) timerText.text = $"{Mathf.CeilToInt(secsLeft)}초";
        SetPrimary(null, null, null);
        SetSecondary("G", $"즉시 열기   <color=#E05050>HP -{Mathf.CeilToInt(instantCost)}</color>", onG);
        Show();
    }

    public void ShowReady(ChestInteractable owner, System.Action onF)
    {
        _owner = owner;
        EnsureBlurCamera();
        SetTitle("해제  완료");
        if (progressSection != null) progressSection.SetActive(false);
        SetPrimary("F", "열어보기", onF);
        SetSecondary(null, null, null);
        Show();
    }

    public void ShowOpened(ChestInteractable owner, System.Action onF)
    {
        _owner = owner;
        EnsureBlurCamera();
        SetTitle("열린  상자");
        if (progressSection != null) progressSection.SetActive(false);
        SetPrimary("F", "열어보기", onF);
        SetSecondary(null, null, null);
        Show();
    }

    public void HideIfOwner(ChestInteractable chest)
    {
        if (_owner == chest) Hide();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        _owner = null;
    }

    // 내부

    private void Show()
    {
        if (panel != null) panel.SetActive(true);
    }

    private void SetTitle(string t)
    {
        if (titleText != null) titleText.text = t;
    }

    private void SetPrimary(string keyChar, string actionText, System.Action action)
    {
        _onPrimary = action;
        bool show = !string.IsNullOrEmpty(actionText);
        if (primaryBtn != null) primaryBtn.gameObject.SetActive(show);
        if (!show) return;
        if (primaryKey != null) primaryKey.text = keyChar ?? "";
        if (primaryLabel != null) primaryLabel.text = actionText;
    }

    private void SetSecondary(string keyChar, string actionText, System.Action action)
    {
        _onSecondary = action;
        bool show = !string.IsNullOrEmpty(actionText);
        if (secondaryBtn != null) secondaryBtn.gameObject.SetActive(show);
        if (!show) return;
        if (secondaryKey != null) secondaryKey.text = keyChar ?? "";
        if (secondaryLabel != null) secondaryLabel.text = actionText;
    }

    // 블러는 카메라 참조가 있어야 동작하는데 Camera.main 은 실행 중에만 잡힌다.
    private void EnsureBlurCamera()
    {
        if (blur != null && blur.Common.cameraReference == null)
            blur.Common.cameraReference = Camera.main;
    }
}
