using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class BeltSegment : MonoBehaviour
    {
        [Header("벨트 시각화")]
        public GameObject beltItemPrefab;
        public float travelTimePerSegment = 0.5f;

        [Tooltip("OutputBuffer에 아이템이 쌓여있을 때 다음 아이템 발송까지 대기 시간(초)")]
        public float dispatchInterval = 1.5f;

        [HideInInspector] public BeltSegment prevSegment;
        [HideInInspector] public BeltSegment nextSegment;
        [HideInInspector] public MachineBase sourceM;
        [HideInInspector] public MachineBase targetM;

        public bool IsHead => prevSegment == null && sourceM != null;
        public bool IsReady => sourceM != null && targetM != null;

        [Header("앞뒤 감지 포인트")]
        public Transform endpointFront;
        public Transform endpointBack;

        [Header("감지 반경")]
        public float detectRadius = 0.6f;

        [Header("감지 레이어")]
        public LayerMask detectMask;

        /// <summary>씬에 활성화된 BeltSegment 전체 목록. 그리드 연결 감지에 사용.</summary>
        public static readonly List<BeltSegment> All = new();

        // ── 그리드 셀 캐시 ───────────────────────────────────────────────
        private Vector2Int _beltCell;   // 이 벨트 자신의 셀
        private Vector2Int _frontCell;  // beltCell + 앞 방향 (다음 연결 대상 셀)
        private Vector2Int _backCell;   // beltCell + 뒤 방향 (이전 연결 대상 셀)
        private bool       _cellsInitialized;

        private void OnEnable() => All.Add(this);

        private void Start() => StartCoroutine(DetectNextFrame());

        private void OnDisable()
        {
            All.Remove(this);
            StopAllCoroutines();
        }

        private IEnumerator DetectNextFrame()
        {
            yield return null;
            DetectConnections();
            yield return null;
            PropagateChain();
        }

        // ── 그리드 셀 초기화 ─────────────────────────────────────────────

        private void InitCells()
        {
            if (endpointFront == null && endpointBack == null)
            {
                Debug.LogWarning("[BeltSegment] endpointFront/Back 모두 null → 물리 감지로 대체합니다.", this);
                return;
            }

            var bm = FindFirstObjectByType<BuildManager>();
            if (bm == null)
            {
                Debug.LogWarning("[BeltSegment] BuildManager를 찾을 수 없습니다. 물리 감지로 대체합니다.", this);
                return;
            }

            Vector3 origin   = bm.GridOriginPos;
            float   cellSize = 1f;

            // 이 벨트 자신의 셀 (transform 기준, FloorToInt로 확정)
            _beltCell = new Vector2Int(
                Mathf.FloorToInt((transform.position.x - origin.x) / cellSize),
                Mathf.FloorToInt((transform.position.z - origin.z) / cellSize));

            // endpoint가 어느 방향에 있는지로 인접 셀 계산
            // → 코너 피스처럼 front/back 모두 같은 셀 내에 있어도 정확히 처리
            if (endpointFront != null)
                _frontCell = _beltCell + WorldDirToGridDir(endpointFront.position - transform.position);

            if (endpointBack != null)
                _backCell  = _beltCell + WorldDirToGridDir(endpointBack.position  - transform.position);

            _cellsInitialized = true;

            Debug.Log($"[BeltSegment] {gameObject.name} 셀 초기화 — own:{_beltCell} front:{_frontCell} back:{_backCell}", this);
        }

        /// <summary>
        /// 월드 벡터를 가장 가까운 그리드 방향 (±1, 0) 또는 (0, ±1)로 변환한다.
        /// </summary>
        private static Vector2Int WorldDirToGridDir(Vector3 dir)
        {
            float ax = Mathf.Abs(dir.x);
            float az = Mathf.Abs(dir.z);
            if (ax >= az)
                return new Vector2Int(dir.x >= 0 ? 1 : -1, 0);
            else
                return new Vector2Int(0, dir.z >= 0 ? 1 : -1);
        }

        // ── 수동 재연결 (Inspector 우클릭 또는 인게임 호출) ──────────────

        [ContextMenu("연결 재감지 (Reconnect)")]
        public void Reconnect()
        {
            // 기존 연결 초기화
            if (prevSegment != null && prevSegment.nextSegment == this) prevSegment.nextSegment = null;
            if (nextSegment != null && nextSegment.prevSegment == this) nextSegment.prevSegment = null;
            sourceM?.RemoveOutputBelt(this);

            prevSegment       = null;
            nextSegment       = null;
            sourceM           = null;
            targetM           = null;
            _cellsInitialized = false;

            // 재감지 → 재전파
            StartCoroutine(DetectNextFrame());
            Debug.Log($"[BeltSegment] {gameObject.name} 재연결 요청", this);
        }

        // ── 연결 감지 (그리드 기반) ──────────────────────────────────────

        public void DetectConnections()
        {
            if (!_cellsInitialized) InitCells();

            if (_cellsInitialized)
                GridDetectConnections();
            else
                LegacyDetectConnections(); // BuildManager 없을 때 폴백
        }

        private void GridDetectConnections()
        {
            // ── 설비 감지: 물리 기반 유지 ─────────────────────────────────
            // 여러 벨트가 같은 설비 출력에 연결되는 라운드 로빈을 지원하려면
            // 설비 연결은 OverlapSphere 방식을 사용해야 한다.
            if (endpointBack  != null) DetectMachineAt(endpointBack.position);
            if (endpointFront != null) DetectMachineAt(endpointFront.position);

            // ── 벨트↔벨트 감지: 그리드 기반 ─────────────────────────────
            // A.frontCell == B.beltCell → A 다음에 B
            foreach (var other in All)
            {
                if (other == null || other == this || !other._cellsInitialized) continue;

                if (_frontCell == other._beltCell)
                {
                    if (nextSegment == null)       nextSegment       = other;
                    if (other.prevSegment == null) other.prevSegment = this;
                }

                if (other._frontCell == _beltCell)
                {
                    if (prevSegment == null)       prevSegment       = other;
                    if (other.nextSegment == null) other.nextSegment  = this;
                }
            }
        }

        /// <summary>설비 포트 물리 감지 — 여러 벨트가 같은 설비에 연결되는 라운드 로빈 지원.</summary>
        private void DetectMachineAt(Vector3 pos)
        {
            var hits = Physics.OverlapSphere(pos, detectRadius, detectMask);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                var machine = hit.GetComponentInParent<MachineBase>();
                if (machine == null) continue;

                bool nearOutput = machine.outputPort != null && machine.inputPort != null
                    ? Vector3.Distance(pos, machine.outputPort.position) <
                      Vector3.Distance(pos, machine.inputPort.position)
                    : machine.outputPort != null;

                if (nearOutput) sourceM = machine;
                else            targetM  = machine;
            }
        }

        // ── 레거시 물리 감지 (폴백) ──────────────────────────────────────

        private void LegacyDetectConnections()
        {
            if (endpointBack  != null) LegacyDetectAt(endpointBack.position,  false);
            if (endpointFront != null) LegacyDetectAt(endpointFront.position, true);
        }

        private void LegacyDetectAt(Vector3 pos, bool isFront)
        {
            var hits = Physics.OverlapSphere(pos, detectRadius, detectMask);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                var machine = hit.GetComponentInParent<MachineBase>();
                if (machine != null)
                {
                    bool nearOutput = machine.outputPort != null && machine.inputPort != null
                        ? Vector3.Distance(pos, machine.outputPort.position) <
                          Vector3.Distance(pos, machine.inputPort.position)
                        : machine.outputPort != null;

                    if (nearOutput) sourceM = machine;
                    else            targetM  = machine;
                    continue;
                }

                var seg = hit.GetComponentInParent<BeltSegment>();
                if (seg == null || seg == this) continue;

                if (isFront)
                {
                    bool neighborBackIsClose = seg.endpointBack != null &&
                        Vector3.Distance(pos, seg.endpointBack.position) <
                        Vector3.Distance(pos, seg.endpointFront != null
                            ? seg.endpointFront.position : seg.transform.position);

                    if (neighborBackIsClose)
                    {
                        if (nextSegment == null)      nextSegment      = seg;
                        if (seg.prevSegment == null)  seg.prevSegment  = this;
                    }
                    else
                    {
                        if (prevSegment == null)      prevSegment      = seg;
                        if (seg.nextSegment == null)  seg.nextSegment  = this;
                    }
                }
                else
                {
                    bool neighborFrontIsClose = seg.endpointFront != null &&
                        Vector3.Distance(pos, seg.endpointFront.position) <
                        Vector3.Distance(pos, seg.endpointBack != null
                            ? seg.endpointBack.position : seg.transform.position);

                    if (neighborFrontIsClose)
                    {
                        if (prevSegment == null)      prevSegment      = seg;
                        if (seg.nextSegment == null)  seg.nextSegment  = this;
                    }
                    else
                    {
                        if (nextSegment == null)      nextSegment      = seg;
                        if (seg.prevSegment == null)  seg.prevSegment  = this;
                    }
                }
            }
        }

        public BeltSegment GetChainHead()
        {
            BeltSegment cur = this;
            int safety = 100;
            while (cur.prevSegment != null && safety-- > 0)
                cur = cur.prevSegment;
            if (safety <= 0)
                Debug.LogWarning($"[BeltSegment] GetChainHead: 순환 참조 감지! ({gameObject.name})", this);
            return cur;
        }

        private void PropagateChain() => GetChainHead().TraverseAndSet();

        private void TraverseAndSet()
        {
            var chain = new List<BeltSegment>();
            var cur = this;
            int safety = 200;
            while (cur != null && safety-- > 0) { chain.Add(cur); cur = cur.nextSegment; }

            MachineBase src = null, tgt = null;
            foreach (var seg in chain)
            {
                if (seg.sourceM != null) src = seg.sourceM;
                if (seg.targetM != null) tgt = seg.targetM;
            }

            if (src == null || tgt == null || src == tgt) return;

            if (Vector3.Distance(chain[0].transform.position, tgt.transform.position)<
                Vector3.Distance(chain[0].transform.position, src.transform.position))
            {
                chain.Reverse();
                for (int i = 0; i < chain.Count; i++)
                {
                    chain[i].prevSegment = i > 0 ? chain[i - 1] : null;
                    chain[i].nextSegment = i < chain.Count - 1 ? chain[i + 1] : null;
                }
            }

            foreach (var seg in chain) { seg.sourceM = src; seg.targetM = tgt; }
            src.AddOutputBelt(chain[0]);

            // 벨트가 연결될 때 OutputBuffer에 이미 대기 중인 아이템이 있으면 즉시 첫 발송 시작
            src.TryDispatchPendingOutput();
        }

        // =====================================================================
        // 운송 중 아이템 추적 (벨트 삭제 / 레시피 불일치 시 창고 구조용)
        // =====================================================================

        /// <summary>코루틴 하나당 하나씩 생성되는 토큰. 아이템 생사 상태를 추적한다.</summary>
        private class InFlightItem
        {
            public int itemId;
            public int amount;
            /// <summary>설비에 정상 전달 완료되면 true.</summary>
            public bool isDelivered;
            /// <summary>창고로 우회 또는 이미 구조됐으면 true.</summary>
            public bool isRescued;
        }

        private readonly List<InFlightItem> _inFlightItems = new();
        /// <summary>현재 씬에 존재하는 시각 오브젝트 목록. OnDestroy에서 일괄 정리.</summary>
        private readonly List<GameObject> _activeVisuals = new();

        private void OnDestroy()
        {
            // ── 이웃 세그먼트 참조 정리 ─────────────────────────────────────
            // 재연결 시 DetectConnections가 null 체크로 자리를 채울 수 있도록
            // 이웃이 나를 가리키는 참조를 미리 끊어둔다.
            if (prevSegment != null && prevSegment.nextSegment == this)
                prevSegment.nextSegment = null;
            if (nextSegment != null && nextSegment.prevSegment == this)
                nextSegment.prevSegment = null;

            // 소스 머신의 outputBelts 목록에서 이 세그먼트 제거
            sourceM?.RemoveOutputBelt(this);

            // 이동 중이던 시각 오브젝트 제거
            for (int i = _activeVisuals.Count - 1; i >= 0; i--)
            {
                if (_activeVisuals[i] != null)
                    Destroy(_activeVisuals[i]);
            }
            _activeVisuals.Clear();

            // 아직 전달/구조되지 않은 아이템 → 창고(Storage)로 이동
            foreach (var token in _inFlightItems)
            {
                if (token.isDelivered || token.isRescued) continue;
                token.isRescued = true;
                InventoryManager.StorageInstance?.AddItem(token.itemId, token.amount);
                Debug.Log($"[Belt] 벨트 삭제 → 아이템 {token.itemId} x{token.amount} 창고로 이동");
            }
            _inFlightItems.Clear();
        }

        // =====================================================================

        /// <summary>현재 이 벨트 체인에서 이동 중인 아이템이 있으면 true.</summary>
        public bool IsBusy => _inFlightItems.Count > 0;

        public bool TryTransport(int itemId, int amount)
        {
            if (!IsReady) return false;
            StartCoroutine(ChainTransportRoutine(itemId, amount));
            // 아이템 출발 시점부터 dispatchInterval 후 다음 아이템 발송
            StartCoroutine(ScheduleNextDispatch());
            return true;
        }

        private IEnumerator ScheduleNextDispatch()
        {
            yield return new WaitForSeconds(dispatchInterval);
            sourceM?.TryDispatchPendingOutput();
        }

        private IEnumerator ChainTransportRoutine(int itemId, int amount)
        {
            // 운송 토큰 등록
            var token = new InFlightItem { itemId = itemId, amount = amount };
            _inFlightItems.Add(token);

            // ── 체인 스냅샷 (출발 시점 기준) ────────────────────────────────
            var chain = new List<BeltSegment>();
            var cur = GetChainHead();
            int safety = 200;
            while (cur != null && safety-- > 0) { chain.Add(cur); cur = cur.nextSegment; }

            if (chain.Count == 0)
            {
                token.isRescued = true;
                InventoryManager.StorageInstance?.AddItem(itemId, amount);
                _inFlightItems.Remove(token);
                yield break;
            }

            // ── 경로 웨이포인트 사전 계산 ────────────────────────────────────
            // back → transform(코너 경유) → front 순으로 각 세그먼트 추가
            var waypoints = new List<Vector3>();
            var seg0 = chain[0];
            waypoints.Add(seg0.endpointBack != null ? seg0.endpointBack.position : seg0.transform.position);
            foreach (var seg in chain)
            {
                waypoints.Add(seg.transform.position);
                waypoints.Add(seg.endpointFront != null
                    ? seg.endpointFront.position
                    : seg.transform.position + seg.transform.forward);
            }

            // 구간별 거리 사전 계산 (매 프레임 계산 방지)
            var segLengths = new float[waypoints.Count - 1];
            float totalLength = 0f;
            for (int i = 0; i < segLengths.Length; i++)
            {
                segLengths[i] = Vector3.Distance(waypoints[i], waypoints[i + 1]);
                totalLength  += segLengths[i];
            }

            // 전체 이동 시간
            float totalTime = 0f;
            foreach (var seg in chain) totalTime += seg.travelTimePerSegment;
            if (totalTime <= 0f) totalTime = 1f;

            // 거부 대상이면 마지막 세그먼트 절반 지점에서 정지
            bool willBeRejected = targetM == null || !targetM.CanReceive(itemId);
            float moveDuration  = willBeRejected
                ? Mathf.Max(0f, totalTime - chain[chain.Count - 1].travelTimePerSegment * 0.5f)
                : totalTime;

            // 시각 오브젝트 생성
            GameObject visual = null;
            if (beltItemPrefab != null)
            {
                visual = Instantiate(beltItemPrefab, waypoints[0], beltItemPrefab.transform.rotation);
                if (visual.TryGetComponent<BeltItemVisual>(out var vis))
                    vis.Setup(itemId, amount);
                _activeVisuals.Add(visual);
            }

            // ── 전체 경로를 일정 속도로 부드럽게 이동 ───────────────────────
            float elapsed    = 0f;
            bool  pathBroken = false;

            while (elapsed < moveDuration)
            {
                // 현재 진행률에 해당하는 세그먼트 생존 확인
                int segIdx = Mathf.Clamp(Mathf.FloorToInt(elapsed / totalTime * chain.Count), 0, chain.Count - 1);
                if (chain[segIdx] == null) { pathBroken = true; break; }

                float t = Mathf.Clamp01(elapsed / totalTime);
                if (visual != null)
                    visual.transform.position = GetPositionAlongPath(waypoints, segLengths, totalLength, t);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 마지막 위치 확정
            if (!pathBroken && visual != null)
                visual.transform.position = GetPositionAlongPath(
                    waypoints, segLengths, totalLength, Mathf.Clamp01(moveDuration / totalTime));

            // ── OnDestroy가 이미 처리한 경우 (방어 코드) ────────────────────
            if (token.isRescued)
            {
                CleanupVisual(visual);
                _inFlightItems.Remove(token);
                yield break;
            }

            // ── 결과 처리 ────────────────────────────────────────────────────
            if (pathBroken)
            {
                token.isRescued = true;
                InventoryManager.StorageInstance?.AddItem(itemId, amount);
                Debug.Log($"[Belt] 경로 파괴 → 아이템 {itemId} x{amount} 즉시 창고 이동");
            }
            else if (targetM == null)
            {
                token.isRescued = true;
                InventoryManager.StorageInstance?.AddItem(itemId, amount);
                Debug.Log($"[Belt] 목표 설비 없음 → 아이템 {itemId} x{amount} 창고 이동");
            }
            else if (!targetM.CanReceive(itemId))
            {
                yield return new WaitForSeconds(1.5f);
                if (!token.isRescued)
                {
                    token.isRescued = true;
                    InventoryManager.StorageInstance?.AddItem(itemId, amount);
                    Debug.Log($"[Belt] 레시피 불일치 → 아이템 {itemId} x{amount} 창고 이동");
                }
            }
            else
            {
                token.isDelivered = true;
                targetM.Receive(itemId, amount);
            }

            CleanupVisual(visual);
            _inFlightItems.Remove(token);
        }

        /// <summary>웨이포인트 목록 위에서 t(0~1) 비율에 해당하는 위치를 반환한다.</summary>
        private static Vector3 GetPositionAlongPath(List<Vector3> pts, float[] lengths, float totalLength, float t)
        {
            if (totalLength <= 0f) return pts[pts.Count - 1];

            float target = t * totalLength;
            float acc    = 0f;

            for (int i = 0; i < lengths.Length; i++)
            {
                if (acc + lengths[i] >= target - 0.0001f)
                {
                    float segT = lengths[i] > 0f
                        ? Mathf.Clamp01((target - acc) / lengths[i])
                        : 1f;
                    return Vector3.Lerp(pts[i], pts[i + 1], segT);
                }
                acc += lengths[i];
            }

            return pts[pts.Count - 1];
        }

        private void CleanupVisual(GameObject visual)
        {
            if (visual == null) return;
            _activeVisuals.Remove(visual);
            Destroy(visual);
        }

        private void OnDrawGizmosSelected()
        {
            if (_cellsInitialized)
            {
                // 자신의 셀 (파란색)
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);
                // 앞 방향 연결 셀 (초록)
                Gizmos.color = Color.green;
                if (endpointFront != null) Gizmos.DrawWireCube(endpointFront.position, Vector3.one * 0.3f);
                // 뒤 방향 연결 셀 (빨강)
                Gizmos.color = Color.red;
                if (endpointBack != null) Gizmos.DrawWireCube(endpointBack.position, Vector3.one * 0.3f);
            }
            else
            {
                Gizmos.color = Color.green;
                if (endpointFront != null) Gizmos.DrawWireSphere(endpointFront.position, detectRadius);
                Gizmos.color = Color.red;
                if (endpointBack  != null) Gizmos.DrawWireSphere(endpointBack.position,  detectRadius);
            }
        }
    }
}