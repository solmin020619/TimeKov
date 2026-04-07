// =====================================================================
// ConveyorBelt.cs
// A설비 출력 → B설비 입력 운반 채널.
// 벨트 위를 실제 아이템 오브젝트가 이동하는 시각 연출 포함.
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

        [Header("벨트 위 아이템 시각화")]
        [Tooltip("벨트 위를 이동할 아이템 프리팹 (BeltItemVisual 컴포넌트 필요)")]
        public GameObject beltItemPrefab;

        [Tooltip("아이템이 출발하는 위치 (source 설비 배출구)")]
        public Transform spawnPoint;

        [Tooltip("아이템이 도착하는 위치 (target 설비 투입구)")]
        public Transform endPoint;

        [Tooltip("출발 → 도착 이동 시간(초)")]
        public float travelTime = 2f;

        public bool IsConnected => source != null && target != null;

        private void Start()
        {
            if (!IsConnected)
                Debug.LogWarning($"[ConveyorBelt] '{name}' — source 또는 target 미연결");

            // 설비에 이 벨트가 연결됐음을 알림
            if (source != null) source.outputBelt = this;
        }

        // ── 아이템 운반 요청 (MachineBase.Dispatch에서 호출) ────────
        public bool TryTransport(int itemId, int amount)
        {
            if (!IsConnected) return false;
            StartCoroutine(TransportRoutine(itemId, amount));
            return true;
        }

        private IEnumerator TransportRoutine(int itemId, int amount)
        {
            // 벨트 위 시각 오브젝트 생성
            GameObject visual = SpawnVisual(itemId, amount);

            Vector3 from = spawnPoint != null ? spawnPoint.position : source.transform.position;
            Vector3 to   = endPoint   != null ? endPoint.position   : target.transform.position;

            float elapsed = 0f;
            while (elapsed < travelTime)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / travelTime);
                if (visual != null)
                    visual.transform.position = Vector3.Lerp(from, to, t);
                yield return null;
            }

            if (visual != null) Destroy(visual);

            // 목적지 설비에 전달
            target.Receive(itemId, amount);
        }

        // ── 벨트 위 시각 오브젝트 생성 ─────────────────────────────
        private GameObject SpawnVisual(int itemId, int amount)
        {
            if (beltItemPrefab == null || spawnPoint == null) return null;

            var go = Instantiate(beltItemPrefab, spawnPoint.position, Quaternion.identity);
            if (go.TryGetComponent<BeltItemVisual>(out var vis))
                vis.Setup(itemId, amount);
            return go;
        }
    }
}
