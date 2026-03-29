// =====================================================================
// ConveyorBelt.cs
// A설비의 출력 버퍼 → B설비의 입력 버퍼를 잇는 운반 채널.
//
// Inspector 연결:
//   source    : 아이템을 뱉는 설비
//   target    : 아이템을 받는 설비
//   spawnPoint / endPoint : 시각 연출용 Transform (없어도 로직은 동작)
//
// 연결 상태:
//   source / target 중 하나라도 없으면 → Disconnected (🔴)
//   둘 다 있으면                        → Connected    (🟢)
//   Connected 일 때만 폴링 루프가 아이템을 운반한다.
// =====================================================================

using System.Collections;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class ConveyorBelt : MonoBehaviour
    {
        [Header("연결 설비")]
        public MachineBase source;
        public MachineBase target;

        [Header("시각 연출 (선택)")]
        [Tooltip("벨트 위를 이동할 아이템 프리팹 (없으면 연출 생략)")]
        public GameObject itemVisualPrefab;
        public Transform spawnPoint;
        public Transform endPoint;
        [Tooltip("출발 → 도착 이동 시간(초)")]
        public float travelTime = 1.2f;

        // ---------------------------------------------------------------
        // 상태 (외부에서 읽기용)
        // ---------------------------------------------------------------
        public bool IsConnected => source != null && target != null;

        // ---------------------------------------------------------------
        // Unity 이벤트
        // ---------------------------------------------------------------
        private void Start()
        {
            if (!IsConnected)
                Debug.LogWarning($"[ConveyorBelt] '{name}' 연결 불량 — source 또는 target 없음");

            // 설비에 연결 상태 알림
            source?.NotifyOutputConnected(this);
            target?.NotifyInputConnected(this);
        }

        // ---------------------------------------------------------------
        // 외부(MachineBase)에서 호출: 아이템 운반 요청
        // ---------------------------------------------------------------
        public bool TryTransport(string itemId, int amount)
        {
            if (!IsConnected) return false;
            StartCoroutine(TransportRoutine(itemId, amount));
            return true;
        }

        // ---------------------------------------------------------------
        // 운반 코루틴
        // ---------------------------------------------------------------
        private IEnumerator TransportRoutine(string itemId, int amount)
        {
            // 시각 연출
            GameObject visual = SpawnVisual(itemId, amount);

            float elapsed = 0f;
            Vector3 from = spawnPoint != null ? spawnPoint.position : source.transform.position;
            Vector3 to   = endPoint   != null ? endPoint.position   : target.transform.position;

            while (elapsed < travelTime)
            {
                elapsed += Time.deltaTime;
                if (visual != null)
                    visual.transform.position = Vector3.Lerp(from, to, elapsed / travelTime);
                yield return null;
            }

            if (visual != null) Destroy(visual);

            // 목적지 설비에 아이템 전달
            target.Receive(itemId, amount);
        }

        // ---------------------------------------------------------------
        // 시각 오브젝트 생성 (프리팹/Transform 없으면 생략)
        // ---------------------------------------------------------------
        private GameObject SpawnVisual(string itemId, int amount)
        {
            if (itemVisualPrefab == null || spawnPoint == null) return null;
            var go = Instantiate(itemVisualPrefab, spawnPoint.position, Quaternion.identity);
            // 아이콘 세팅은 ItemIconVisual 컴포넌트가 담당
            // (ItemData 조회는 다른 팀원 코드 ItemDatabase.Get(itemId) 활용)
            if (go.TryGetComponent<ItemIconVisual>(out var vis))
                vis.Setup(itemId, amount);
            return go;
        }
    }
}
