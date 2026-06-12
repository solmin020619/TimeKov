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
    /// 패널 닫기. 즉시 비활성화한다.
    /// [멈춤 버그 수정] 예전엔 접기 애니(0.15s) 후 SetActive(false) 했는데, 그 사이 다시 열면
    /// SetActive(true)가 no-op이 되어 펼치기가 안 돌고 돌던 접기 코루틴이 패널을 꺼버려
    /// '논리상 열림(입력잠금/일시정지) + 화면엔 없음'으로 멈췄다. ESC·인벤 연타 시 재현.
    /// → 닫기는 즉시 비활성화해 레이스를 제거한다(열기 펼침 연출은 유지).
    /// </summary>
    public void Close()
    {
        if (!gameObject.activeSelf) return;

        if (_currentCoroutine != null) { StopCoroutine(_currentCoroutine); _currentCoroutine = null; }

        if (_isInitialized) transform.localScale = _originalScale;
        if (dimOverlay != null) dimOverlay.alpha = 0f;
        gameObject.SetActive(false);
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
}
