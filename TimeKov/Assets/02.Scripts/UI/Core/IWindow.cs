// =====================================================================
// IWindow.cs
// WindowManager가 관리하는 모든 UI 창의 공통 인터페이스
//
// 구현 방식:
//   1. UI 본체 MonoBehaviour가 직접 IWindow 구현 (단순한 신규 UI)
//   2. 어댑터 MonoBehaviour가 IWindow를 구현하고 기존 UI 매니저에 위임
//      (인벤토리처럼 다른 담당자 영역, 원본 코드 안 건드릴 때)
//
// 등록은 보통 OnEnable에서 WindowManager.I.Register(this),
// 해제는 OnDisable에서 WindowManager.I.Unregister(this) 호출.
// =====================================================================

namespace TimeKov.UI
{
    public interface IWindow
    {
        /// <summary>창의 레이어. sortingOrder, 차단·라이프사이클 정책 분기에 사용.</summary>
        UILayer Layer { get; }

        /// <summary>창 식별자. 정책 SO 키, 중복 오픈 방지, 디버그 로그에 사용. 창마다 고유해야 함.</summary>
        string WindowId { get; }

        /// <summary>true면 열려있는 동안 Time.timeScale = 0. 보통 Modal만 true.</summary>
        bool PausesGame { get; }

        /// <summary>true면 열려있는 동안 플레이어 입력 차단 + 커서 표시. Window 이상은 보통 true.</summary>
        bool LocksGameplayInput { get; }

        /// <summary>WindowManager.Open() 직후 호출. 자식 GO Activate, 데이터 바인딩.</summary>
        void OnOpen();

        /// <summary>WindowManager.Close() 직후 호출. 자식 GO Deactivate, 구독 해제.</summary>
        void OnClose();
    }
}
