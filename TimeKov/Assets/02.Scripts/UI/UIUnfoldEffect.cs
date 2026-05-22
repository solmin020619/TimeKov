// UIUnfoldEffect.cs
// UI 패널 열기/닫기 애니메이션 (Y축 스케일 펼치기/접기)
//
// [닫기 사용법]
//   패널을 닫을 때 SetActive(false) 대신 GetComponent<UIUnfoldEffect>()?.Close() 호출
//   → GameUIController.SetPanelActive() 헬퍼가 자동으로 처리해줌
//
// [DimOverlay 설정]
//   DimBlocker 오브젝트에 CanvasGroup 컴포넌트 추가 후 dimOverlay 필드에 연결
//   → 스케일과 별도로 알파가 제어되어 딤 배경이 찌그러지지 않음

using UnityEngine;
using System.Collections;

public class UIUnfoldEffect : MonoBehaviour
{
    [Header("애니메이션 시간 (초)")]
    public float duration = 0.15f;

    [Header("애니메이션 커브")]
    public AnimationCurve unfoldCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("딤 오버레이 (선택)")]
    [Tooltip("DimBlocker의 CanvasGroup — 스케일과 별도로 알파 페이드 처리되어 찌그러짐 방지")]
    [SerializeField] private CanvasGroup dimOverlay;

    private Coroutine _currentCoroutine;
    private Vector3 _originalScale;
    private bool _isInitialized;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _originalScale = transform.localScale;
        _isInitialized = true;
    }

    private void OnEnable()
    {
        if (!_isInitialized) return;

        if (_currentCoroutine != null) StopCoroutine(_currentCoroutine);
        _currentCoroutine = StartCoroutine(UnfoldRoutine());
    }

    // ─── 공개 닫기 메서드 ────────────────────────────────────────────────────

    /// <summary>
    /// 닫기 애니메이션 재생 후 SetActive(false) 호출.
    /// GameUIController.SetPanelActive() 또는 외부에서 직접 호출.
    /// </summary>
    public void Close()
    {
        if (!gameObject.activeInHierarchy) return;

        if (_currentCoroutine != null) StopCoroutine(_currentCoroutine);
        _currentCoroutine = StartCoroutine(FoldRoutine());
    }

    // ─── 열기 애니메이션 ─────────────────────────────────────────────────────

    private IEnumerator UnfoldRoutine()
    {
        // 시작 상태: Y 스케일 0, 딤 알파 0
        transform.localScale = new Vector3(_originalScale.x, 0f, _originalScale.z);
        if (dimOverlay != null) dimOverlay.alpha = 0f;

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = unfoldCurve.Evaluate(Mathf.Clamp01(time / duration));

            transform.localScale = new Vector3(_originalScale.x, _originalScale.y * t, _originalScale.z);
            if (dimOverlay != null) dimOverlay.alpha = t;

            yield return null;
        }

        // 완료: 정확한 원래 크기로 복원
        transform.localScale = _originalScale;
        if (dimOverlay != null) dimOverlay.alpha = 1f;
        _currentCoroutine = null;
    }

    // ─── 닫기 애니메이션 ─────────────────────────────────────────────────────

    private IEnumerator FoldRoutine()
    {
        // 시작 상태: 현재 스케일에서 역방향 (열리다 멈춰도 자연스럽게 처리)
        float startYRatio = _originalScale.y > 0f
            ? transform.localScale.y / _originalScale.y
            : 0f;
        float startAlpha  = dimOverlay != null ? dimOverlay.alpha : 0f;

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            // 열기 커브의 역방향: 1→0
            float t = 1f - unfoldCurve.Evaluate(Mathf.Clamp01(time / duration));

            // startYRatio에서 0으로 줄어듦 (중간에 닫혀도 자연스럽게)
            float yScale = startYRatio * t;
            transform.localScale = new Vector3(_originalScale.x, _originalScale.y * yScale, _originalScale.z);
            if (dimOverlay != null) dimOverlay.alpha = startAlpha * t;

            yield return null;
        }

        // 완료: Y 스케일 0, 딤 알파 0, 비활성화
        transform.localScale = new Vector3(_originalScale.x, 0f, _originalScale.z);
        if (dimOverlay != null) dimOverlay.alpha = 0f;

        gameObject.SetActive(false);
        _currentCoroutine = null;
    }
}
