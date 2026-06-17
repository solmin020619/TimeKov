using System.Collections;
using UnityEngine;

namespace TIMEKOV.Factory
{
    /// <summary>
    /// BeltSegment.ReconnectAll() 의 실제 재감지를 "한 프레임 뒤"에 한 번만 수행하는 러너.
    ///
    /// 레일을 깔거나 철거하면 RailPiece.ApplyVisual 이 이웃 벨트 비주얼을 Destroy 후 재생성한다.
    /// Destroy 는 프레임 끝에 반영되므로, 호출 즉시 감지하면 곧 파괴될 옛 세그먼트에 잘못 엮여
    /// 체인이 끊긴다. 한 프레임 기다렸다가(죽은 세그먼트가 BeltSegment.All 에서 빠진 뒤) 감지하면
    /// 끊었다 다시 이어도 정상적으로 재연결된다.
    ///
    /// 같은 프레임에 ReconnectAll 이 여러 번 불려도(설비+레일 동시 정리 등) 한 번만 실행되도록
    /// 요청을 코얼레싱한다.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class BeltReconnectRunner : MonoBehaviour
    {
        private static BeltReconnectRunner _instance;
        private bool _pending;

        public static void Schedule()
        {
            if (_instance == null)
            {
                var go = new GameObject("[BeltReconnectRunner]");
                go.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<BeltReconnectRunner>();
            }
            _instance.RequestReconnect();
        }

        private void RequestReconnect()
        {
            if (_pending) return;   // 이미 이번 프레임 요청이 예약돼 있으면 무시(코얼레싱)
            _pending = true;
            StartCoroutine(ReconnectNextFrame());
        }

        private IEnumerator ReconnectNextFrame()
        {
            // 한 프레임 대기 → 비주얼 교체로 Destroy 된 옛 세그먼트가 All 에서 제거된 뒤 감지.
            yield return null;
            _pending = false;
            BeltSegment.ReconnectAllNow();
        }
    }
}
