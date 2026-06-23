// DeathOverlayUI.cs
// 팰월드 스타일 사망 화면
// ─ DEFEAT 타이틀
// ─ 아이템 드롭 안내 텍스트
// ─ 부활 카운트다운 (X 초)
// ─ 부활하기 버튼 (카운트다운 0이 되면 활성화)

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TimeKov.UI;   // WindowManager / UILayer / WindowSortingSettings (오버레이 sortingOrder)

public class DeathOverlayUI : MonoBehaviour
{
    /// <summary>GameUIController.RefreshCursorState에서 커서 잠금 예외 처리에 사용</summary>
    public static bool IsOpen { get; private set; } = false;
    // ── UI 참조 ────────────────────────────────────────────────────
    [Header("패널")]
    [SerializeField] private CanvasGroup overlayGroup;

    [Header("텍스트")]
    [SerializeField] private TMP_Text titleText;          // "DEFEAT"
    [SerializeField] private TMP_Text subtitleText;       // "모든 아이템을 잃어버렸다"
    [SerializeField] private TMP_Text countdownNumber;    // "5"
    [SerializeField] private TMP_Text countdownLabel;     // "부활 가능 시까지 X 초"

    [Header("버튼")]
    [SerializeField] private Button   respawnButton;      // 부활하기
    [SerializeField] private CanvasGroup buttonGroup;     // 버튼 투명도 제어

    // ── 설정 ───────────────────────────────────────────────────────
    [Header("설정")]
    [SerializeField] private float   fadeInDuration  = 0.5f;
    [SerializeField] private float   fadeOutDuration = 0.4f;
    [SerializeField] private string  titleString     = "DEFEAT";
    [SerializeField] private string  subtitleString  = "모든 아이템을 잃어버렸다";

    // ── 런타임 ─────────────────────────────────────────────────────
    private float   _countdown;
    private bool    _counting;
    private Action  _onRespawn;
    private Coroutine _fadeRoutine;
    private Coroutine _cursorRoutine;   // 오버레이 동안 프레임 끝에서 커서 강제 (가장 늦게 돌아 무조건 이김)

    // ═══════════════════════════════════════════════════════════════

    void Awake()
    {
        EnsureTopmostCanvas();   // 사망 화면은 항상 최상단(Overlay) — 다른 UI가 부활 버튼 가리는 것 차단

        if (overlayGroup != null)
        {
            overlayGroup.alpha          = 0f;
            overlayGroup.interactable   = false;
            overlayGroup.blocksRaycasts = false;
        }

        if (titleText    != null) titleText.text    = titleString;
        if (subtitleText != null) subtitleText.text = subtitleString;
        if (countdownNumber != null) countdownNumber.text = "";
        if (countdownLabel  != null) countdownLabel.text  = "";

        ApplyTone();   // 인벤/창고/도감과 톤 통일(차가운 sci-fi 간유리)

        SetButtonReady(false);

        if (respawnButton != null)
            respawnButton.onClick.AddListener(OnRespawnClicked);

        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_counting) return;

        _countdown -= Time.unscaledDeltaTime;

        int secs = Mathf.CeilToInt(Mathf.Max(0f, _countdown));

        if (countdownNumber != null)
            countdownNumber.text = secs.ToString();

        if (countdownLabel != null)
            countdownLabel.text = $"부활 가능 시까지   {secs}   초";

        if (_countdown <= 0f)
        {
            _counting = false;
            SetButtonReady(true);
        }
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>사망 오버레이 표시. respawnDelay 후 버튼 활성화, 클릭 시 onRespawn 콜백.</summary>
    public void Show(float respawnDelay, Action onRespawn)
    {
        _onRespawn = onRespawn;
        _countdown = respawnDelay;
        _counting  = true;

        SetButtonReady(false);

        IsOpen = true;

        gameObject.SetActive(true);

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeTo(1f, fadeInDuration));

        // 부활 버튼 클릭용 커서 강제. RefreshCursorState 가 사망 시 커서를 풀어주게 돼 있으나
        // 빌드에서 안 먹히는 정황(state==None=게임플레이로 보고 누군가 다시 잠금) -> 프레임 맨 끝에서
        // 강제해 '마지막 라이터'를 보장한다.
        if (_cursorRoutine != null) StopCoroutine(_cursorRoutine);
        _cursorRoutine = StartCoroutine(ForceCursorWhileOpen());
    }

    // 오버레이가 떠 있는 동안 매 프레임 '끝'(모든 Update/LateUpdate 이후)에 커서를 강제 표시+해제.
    // 어떤 스크립트가 LateUpdate 에서 커서를 다시 잠가도 이게 가장 늦게 돌아 이긴다.
    private IEnumerator ForceCursorWhileOpen()
    {
        var wait = new WaitForEndOfFrame();
        while (IsOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            yield return wait;
        }
    }

    /// <summary>오버레이 페이드 아웃 후 숨김.</summary>
    public void Hide()
    {
        _counting = false;

        IsOpen = false;

        if (_cursorRoutine != null) { StopCoroutine(_cursorRoutine); _cursorRoutine = null; }
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    // ── 내부 ──────────────────────────────────────────────────────

    void OnRespawnClicked()
    {
        SetButtonReady(false);
        _onRespawn?.Invoke();
    }

    // 사망 오버레이를 자체 Canvas로 띄워 sortingOrder를 Overlay(500)로 고정.
    // 코어키트 F목록/컨텍스트메뉴 등 나중에 생성되는 UI가 부활 버튼 위를 덮어 클릭이 안 먹던 버그 차단.
    // (overlayGroup.blocksRaycasts=true 와 합쳐져 사망 중엔 뒤 UI 클릭도 막는 올바른 모달이 됨)
    void EnsureTopmostCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();

        int order = 500;   // WindowSortingSettings.overlayOrder 폴백(매니저 없을 때)
        var wm = WindowManager.I;
        if (wm != null && wm.SortingSettings != null)
            order = wm.SortingSettings.GetOrder(UILayer.Overlay);

        canvas.overrideSorting = true;
        canvas.sortingOrder = order;

        // ★자체 Canvas를 붙이면 이 오브젝트 하위 그래픽이 이 Canvas에 등록된다.
        // 루트 GraphicRaycaster는 자기 Canvas 그래픽만 레이캐스트하므로, 여기에도 GraphicRaycaster가 없으면
        // 화면엔 보이지만(렌더는 sortingOrder로) 버튼 클릭이 안 먹는다. -> 자체 레이캐스터 부착 필수.
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
    }

    // 인벤/창고/도감과 톤 통일: 차가운 슬레이트 패널 + 밝은 텍스트 + 시안 카운트다운 + 어두운 딤.
    // 색/패널이 프리팹에 박혀 있어 코드에서 자동 재색(인스펙터 수동 배선 불필요).
    void ApplyTone()
    {
        var txtMain = new Color32(0xEA, 0xF2, 0xFB, 0xFF);   // 밝은 본문(어두운 패널 위)
        var txtSub  = new Color32(0xAE, 0xC0, 0xD6, 0xFF);   // 차가운 보조
        var cyan    = new Color32(0x5F, 0xC4, 0xFF, 0xFF);   // 시안 액센트(인벤/도감 공통)

        if (titleText      != null) titleText.color      = txtMain;
        if (subtitleText   != null) subtitleText.color   = txtSub;
        if (countdownLabel != null) countdownLabel.color = txtSub;
        if (countdownNumber!= null) countdownNumber.color= cyan;

        // 패널 박스 = 제목의 가장 가까운 부모 Image. 차가운 슬레이트(인벤 패널톤, 가독 위해 불투명도↑).
        Image panel    = titleText != null ? titleText.GetComponentInParent<Image>() : null;
        // 풀스크린 딤 = overlayGroup가 붙은 오브젝트의 Image(있으면).
        Image backdrop = overlayGroup != null ? overlayGroup.GetComponent<Image>() : null;

        if (panel != null && panel != backdrop)
            panel.color = new Color(26f / 255f, 32f / 255f, 42f / 255f, 0.92f);
        if (backdrop != null)
            backdrop.color = new Color(6f / 255f, 9f / 255f, 14f / 255f, 0.72f);

        // 부활 버튼: 차가운 슬레이트 베이스(테두리/라운드는 프리팹 유지).
        if (respawnButton != null)
        {
            var bImg = respawnButton.GetComponent<Image>();
            if (bImg != null) bImg.color = new Color(0.16f, 0.42f, 0.62f, 0.85f);
        }
    }

    void SetButtonReady(bool ready)
    {
        if (respawnButton != null)
            respawnButton.interactable = ready;

        // buttonGroup 필드가 연결돼 있으면 우선 사용
        if (buttonGroup != null)
        {
            buttonGroup.alpha          = ready ? 1f : 0.4f;
            buttonGroup.interactable   = ready;
            buttonGroup.blocksRaycasts = true;
        }
        else if (respawnButton != null)
        {
            // 연결 안 된 경우 버튼 오브젝트의 CanvasGroup 직접 처리
            var cg = respawnButton.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha          = ready ? 1f : 0.4f;
                cg.interactable   = ready;
                cg.blocksRaycasts = true;
            }
        }
    }

    IEnumerator FadeTo(float target, float duration)
    {
        if (overlayGroup == null) yield break;

        float start   = overlayGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            overlayGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        overlayGroup.alpha = target;

        // 페이드 인 완료 → 레이캐스트 활성화 (버튼 클릭 가능)
        // 페이드 아웃 완료 → 레이캐스트 비활성화
        bool fadedIn = target >= 1f;
        overlayGroup.interactable   = fadedIn;
        overlayGroup.blocksRaycasts = fadedIn;
    }

    IEnumerator FadeOutRoutine()
    {
        yield return FadeTo(0f, fadeOutDuration);
        gameObject.SetActive(false);
    }
}
