// =====================================================================
// CanvasLayerAssigner.cs
// Canvas에 부착해서 UILayer enum 하나 고르면, WindowSortingSettings SO의
// 값으로 자동으로 sortingOrder를 채워줌.
//
// 운용 방식:
//   - 새 Canvas 만들 때 이 컴포넌트 부착 + layer 선택만 하면 끝
//   - 모든 Canvas의 sortingOrder를 한 번에 바꾸고 싶으면
//     WindowSortingSettings SO 값만 수정 → 다음 OnEnable에 일괄 반영
//
// Script Execution Order:
//   WindowManager가 먼저 Awake되어야 함. 안전을 위해 WindowManager.OnEnable에서
//   씬의 모든 CanvasLayerAssigner를 찾아 Apply()를 한 번 더 호출해줌.
// =====================================================================

using UnityEngine;

namespace TimeKov.UI
{
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class CanvasLayerAssigner : MonoBehaviour
    {
        [Tooltip("이 Canvas의 UI 레이어. WindowSortingSettings SO에서 숫자 매핑.")]
        public UILayer layer = UILayer.Window;

        [Tooltip("OnEnable마다 sortingOrder 재적용. 런타임에 SO 값 바뀐 것도 반영 가능.")]
        public bool reapplyOnEnable = true;

        Canvas _canvas;

        void Awake()
        {
            CacheCanvas();
            Apply();
        }

        void OnEnable()
        {
            if (reapplyOnEnable) Apply();
        }

        public void Apply()
        {
            if (_canvas == null) CacheCanvas();
            if (_canvas == null) return;

            var wm = WindowManager.I;
            if (wm == null || wm.SortingSettings == null) return;

            int order = wm.SortingSettings.GetOrder(layer);
            _canvas.overrideSorting = true;
            if (_canvas.sortingOrder != order)
                _canvas.sortingOrder = order;
        }

        void CacheCanvas()
        {
            _canvas = GetComponent<Canvas>();
        }

#if UNITY_EDITOR
        // 인스펙터에서 layer 바꾸면 에디터에서도 미리 보여줌
        void OnValidate()
        {
            if (!Application.isPlaying) return;
            Apply();
        }
#endif
    }
}
