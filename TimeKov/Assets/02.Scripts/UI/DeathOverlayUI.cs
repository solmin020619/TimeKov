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

    // ═══════════════════════════════════════════════════════════════

    void Awake()
    {
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
    }

    /// <summary>오버레이 페이드 아웃 후 숨김.</summary>
    public void Hide()
    {
        _counting = false;

        IsOpen = false;

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    // ── 내부 ──────────────────────────────────────────────────────

    void OnRespawnClicked()
    {
        SetButtonReady(false);
        _onRespawn?.Invoke();
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
