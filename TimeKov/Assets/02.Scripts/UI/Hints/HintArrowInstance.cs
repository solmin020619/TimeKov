// =====================================================================
// HintArrowInstance.cs
// HintArrowManager가 인스턴스화한 화살표 GO에 동적으로 부착되는 컴포넌트.
// 타깃을 매 프레임 추적하고 duration 만료 시 자기 자신 Destroy.
// =====================================================================

using System;
using UnityEngine;

namespace TimeKov.UI
{
    public class HintArrowInstance : MonoBehaviour
    {
        public event Action OnExpired;

        enum Mode { Static, WorldTarget, UITarget }
        Mode _mode = Mode.Static;

        // World 모드
        Transform _worldTarget;
        Vector3 _worldOffset;

        // UI 모드
        RectTransform _uiTarget;
        Vector2 _uiOffset;
        Canvas _canvas;
        RectTransform _selfRect;

        // 시간
        float _remaining;
        bool _infinite;

        // ── 타깃 설정 ────────────────────────────────────────────────

        public void SetWorldTarget(Transform target, Vector3 offset)
        {
            _mode = Mode.WorldTarget;
            _worldTarget = target;
            _worldOffset = offset;
            if (target != null) transform.position = target.position + offset;
        }

        public void SetWorldPosition(Vector3 worldPos)
        {
            _mode = Mode.Static;
            transform.position = worldPos;
        }

        public void SetUITarget(RectTransform target, Vector2 offset, Canvas canvas)
        {
            _mode = Mode.UITarget;
            _uiTarget = target;
            _uiOffset = offset;
            _canvas = canvas;
            _selfRect = transform as RectTransform;
            UpdateUIPosition();
        }

        public void SetDuration(float duration)
        {
            if (duration <= 0f) { _infinite = true; _remaining = 0f; }
            else { _infinite = false; _remaining = duration; }
        }

        public void ForceHide()
        {
            if (this != null && gameObject != null)
                Destroy(gameObject);
        }

        // ── 매 프레임 추적 ───────────────────────────────────────────

        void LateUpdate()
        {
            // 타깃 추적
            if (_mode == Mode.WorldTarget)
            {
                if (_worldTarget == null) { ForceHide(); return; }
                transform.position = _worldTarget.position + _worldOffset;
            }
            else if (_mode == Mode.UITarget)
            {
                if (_uiTarget == null) { ForceHide(); return; }
                UpdateUIPosition();
            }

            // 시간 만료
            if (!_infinite)
            {
                _remaining -= Time.unscaledDeltaTime;
                if (_remaining <= 0f)
                {
                    OnExpired?.Invoke();
                    ForceHide();
                }
            }
        }

        void UpdateUIPosition()
        {
            if (_selfRect == null || _uiTarget == null) return;

            // 타깃의 월드 좌표 → 자기 부모 RectTransform 좌표로 변환
            var targetWorld = _uiTarget.TransformPoint(Vector3.zero);

            var parent = _selfRect.parent as RectTransform;
            if (parent == null) { _selfRect.position = targetWorld; }
            else
            {
                var screenPoint = RectTransformUtility.WorldToScreenPoint(
                    _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                        ? null
                        : (_canvas != null ? _canvas.worldCamera : null),
                    targetWorld);

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parent,
                        screenPoint,
                        _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                            ? null
                            : (_canvas != null ? _canvas.worldCamera : null),
                        out var localPoint))
                {
                    _selfRect.anchoredPosition = localPoint + _uiOffset;
                }
            }
        }
    }
}
