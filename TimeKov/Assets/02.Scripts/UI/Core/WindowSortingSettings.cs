// =====================================================================
// WindowSortingSettings.cs
// UI 레이어별 Canvas sortingOrder를 SO 한 곳에서 관리
//
// 컨벤션: 100단위. 사이 끼워넣기 여유 확보.
// 모든 UI Canvas는 코드에서 sortingSettings.GetOrder(layer)로 적용 (매직넘버 금지).
//
// 사용 예 (씬 부팅 시):
//   var canvas = GetComponent<Canvas>();
//   canvas.overrideSorting = true;
//   canvas.sortingOrder = WindowManager.I.SortingSettings.GetOrder(UILayer.Window);
// =====================================================================

using UnityEngine;

namespace TimeKov.UI
{
    [CreateAssetMenu(fileName = "WindowSortingSettings", menuName = "TimeKov/UI/Window Sorting Settings")]
    public class WindowSortingSettings : ScriptableObject
    {
        [Header("Layer → SortingOrder 매핑")]
        [Tooltip("월드 공간 UI. 네임플레이트, 데미지 숫자")]
        public int worldOrder   = 0;

        [Tooltip("상시 표시 HUD. 미니맵, 체력, 스태미나, 핫바, 퀘스트 트래커")]
        public int hudOrder     = 100;

        [Tooltip("열고 닫는 패널. 인벤·퀘스트 팝업·스킬·공장 UI — 동시 표시 가능")]
        public int windowOrder  = 200;

        [Tooltip("배타적 모달. 확인 팝업, 설정, 분할 — 뒤 차단")]
        public int modalOrder   = 300;

        [Tooltip("토스트 알림. 자동 사라짐")]
        public int toastOrder   = 400;

        [Tooltip("오버레이. 로딩, 사망, 페이드 — 항상 최상단")]
        public int overlayOrder = 500;

        public int GetOrder(UILayer layer)
        {
            switch (layer)
            {
                case UILayer.World:   return worldOrder;
                case UILayer.HUD:     return hudOrder;
                case UILayer.Window:  return windowOrder;
                case UILayer.Modal:   return modalOrder;
                case UILayer.Toast:   return toastOrder;
                case UILayer.Overlay: return overlayOrder;
                default:              return 0;
            }
        }
    }
}
