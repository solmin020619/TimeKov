// =====================================================================
// ScreenBloodEffect.cs
// 플레이어 피격 시 전체 화면 혈흔 이미지 플래시 효과
// ScreenBlood 프리팹에 붙여서 사용
// =====================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenBloodEffect : MonoBehaviour
{
    [Header("효과 설정")]
    [SerializeField] private float flashAlpha      = 0.85f;  // 최대 불투명도 (0~1)
    [SerializeField] private float fadeInDuration  = 0.05f;  // 등장 속도 (빠를수록 강렬)
    [SerializeField] private float fadeOutDuration = 0.6f;   // 사라지는 속도

    // ── 내부 ──────────────────────────────────────────────────────────
    private Coroutine    _flashRoutine;
    private Player       _player;
    private CanvasGroup  _canvasGroup;

    // ── 라이프사이클 ──────────────────────────────────────────────────

    private void Awake()
    {
        // CanvasGroup이 없으면 자동 추가 (머티리얼 상관없이 알파 제어)
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _canvasGroup.blocksRaycasts = false;  // 클릭 차단 안 함
        _canvasGroup.interactable   = false;
        SetAlpha(0f);
    }

    private void Start()
    {
        _player = FindAnyObjectByType<Player>();
        if (_player != null)
        {
            _player.Stat.OnHurt += OnPlayerHurt;
        }
        else
        {
            Debug.LogWarning("[ScreenBloodEffect] Player를 찾을 수 없습니다.");
        }
    }

    private void OnDestroy()
    {
        if (_player != null)
            _player.Stat.OnHurt -= OnPlayerHurt;
    }

    // ── 피격 콜백 ─────────────────────────────────────────────────────

    private void OnPlayerHurt()
    {
        // 이전 플래시 중이면 즉시 재시작 (연속 피격 시 중첩)
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    // ── 코루틴 ────────────────────────────────────────────────────────

    private IEnumerator FlashRoutine()
    {
        // 빠르게 등장
        yield return StartCoroutine(FadeTo(flashAlpha, fadeInDuration));
        // 서서히 사라짐
        yield return StartCoroutine(FadeTo(0f, fadeOutDuration));
        _flashRoutine = null;
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        float start   = GetAlpha();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(start, target, elapsed / duration));
            yield return null;
        }

        SetAlpha(target);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    private void SetAlpha(float a)
    {
        if (_canvasGroup != null)
            _canvasGroup.alpha = a;
    }

    private float GetAlpha()
        => _canvasGroup != null ? _canvasGroup.alpha : 0f;
}
