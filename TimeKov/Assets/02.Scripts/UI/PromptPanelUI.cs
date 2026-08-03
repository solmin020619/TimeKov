using JeffGrawAssets.FlexibleUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [08-02] 상호작용 프롬프트 패널 공통 베이스.
//   화면 하단에 뜨는 "제목 + (진행바) + F/G 버튼" 패널. 현재 사용처는 상자(ChestPromptUI) 하나지만,
//   같은 모양의 프롬프트가 또 필요해지면 이 베이스를 상속하고 상태 문구만 쓰면 된다.
//   (원래 설비 해금 프롬프트가 같은 레이아웃 250줄을 통째로 복붙하고 있었는데, 해금이 전송률 보상으로 바뀌며 폐기됐다)
//
//   계층은 씬에 있는 실물이 원본이고, 이 클래스는 참조를 쓰기만 한다.
//   (생성용 에디터 빌더는 08-03 에 팀 합의로 제거. 다시 구워야 하면 git 이력에서 꺼낸다)
//   표시/숨김은 오브젝트 비활성이 아니라 자식 panel 만 토글한다(루트는 항상 활성 = Instance 를 항상 찾을 수 있음).
public abstract class PromptPanelUI : MonoBehaviour
{
    [Header("구성 요소 (빌더가 자동 연결)")]
    [Tooltip("표시/숨김 대상. 이 오브젝트만 켜고 끈다.")]
    [SerializeField] protected GameObject panel;
    [SerializeField] protected TMP_Text titleText;
    [Tooltip("진행 중일 때만 보이는 섹션(진행바 + 남은시간).")]
    [SerializeField] protected GameObject progressSection;
    [SerializeField] protected Image progressFill;
    [SerializeField] protected TMP_Text timerText;

    [Header("주 버튼 (F)")]
    [SerializeField] protected Button primaryBtn;
    [SerializeField] protected TMP_Text primaryKey;
    [SerializeField] protected TMP_Text primaryLabel;

    [Header("보조 버튼 (G)")]
    [SerializeField] protected Button secondaryBtn;
    [SerializeField] protected TMP_Text secondaryKey;
    [SerializeField] protected TMP_Text secondaryLabel;

    [Header("배경 블러")]
    [SerializeField] protected BlurredImage blur;

    private System.Action _onPrimary;
    private System.Action _onSecondary;

    protected virtual void Awake()
    {
        // 클릭 배선은 런타임에서 건다(에디터 영속 리스너로 저장하지 않는다 = 씬 diff 가 깔끔).
        if (primaryBtn != null) primaryBtn.onClick.AddListener(() => _onPrimary?.Invoke());
        if (secondaryBtn != null) secondaryBtn.onClick.AddListener(() => _onSecondary?.Invoke());

        if (progressSection != null) progressSection.SetActive(false);
        if (panel != null) panel.SetActive(false);
    }

    protected virtual void Start() => EnsureBlurCamera();

    /// <summary>패널을 숨긴다(완료/취소/이탈 공통).</summary>
    public virtual void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    // 파생 클래스가 쓰는 조립 도구

    protected void ShowPanel()
    {
        EnsureBlurCamera();
        if (panel != null) panel.SetActive(true);
    }

    protected void SetTitle(string t)
    {
        if (titleText != null) titleText.text = t;
    }

    /// <summary>진행 섹션 표시 + 값 갱신. show=false 면 섹션을 숨긴다.</summary>
    protected void SetProgress(bool show, float progress01 = 0f, float secsLeft = 0f)
    {
        if (progressSection != null) progressSection.SetActive(show);
        if (!show) return;
        if (progressFill != null) progressFill.fillAmount = progress01;
        if (timerText != null) timerText.text = $"{Mathf.CeilToInt(secsLeft)}" + Loc.Get("초");
    }

    /// <summary>actionText 가 비어 있으면 버튼을 숨긴다.</summary>
    protected void SetPrimary(string keyChar, string actionText, System.Action action)
    {
        _onPrimary = action;
        Apply(primaryBtn, primaryKey, primaryLabel, keyChar, actionText);
    }

    protected void SetSecondary(string keyChar, string actionText, System.Action action)
    {
        _onSecondary = action;
        Apply(secondaryBtn, secondaryKey, secondaryLabel, keyChar, actionText);
    }

    private static void Apply(Button btn, TMP_Text key, TMP_Text label, string keyChar, string actionText)
    {
        bool show = !string.IsNullOrEmpty(actionText);
        if (btn != null) btn.gameObject.SetActive(show);
        if (!show) return;
        if (key != null) key.text = keyChar ?? "";
        if (label != null) label.text = actionText;
    }

    // 블러는 카메라 참조가 있어야 동작하는데 Camera.main 은 실행 중에만 잡힌다.
    protected void EnsureBlurCamera()
    {
        if (blur != null && blur.Common.cameraReference == null)
            blur.Common.cameraReference = Camera.main;
    }
}
