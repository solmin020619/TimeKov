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

        private void Start() => StartCoroutine(DetectNextFrame());

        private IEnumerator DetectNextFrame()
        {
            yield return null;
            DetectConnections();
            yield return null;
            PropagateChain();
        }

        public void DetectConnections()
        {
            if (endpointBack != null) DetectAt(endpointBack.position, false);
            if (endpointFront != null) DetectAt(endpointFront.position, true);
        }

        private void DetectAt(Vector3 pos, bool isFront)
        {
            var hits = Physics.OverlapSphere(pos, detectRadius, detectMask);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                var machine = hit.GetComponentInParent<MachineBase>();
                if (machine != null)
                {
                    bool nearOutput = machine.outputPort != null && machine.inputPort != null
                        ? Vector3.Distance(pos, machine.outputPort.position)<
                          Vector3.Distance(pos, machine.inputPort.position)
                        : machine.outputPort != null;

                    if (nearOutput) sourceM = machine;
                    else targetM = machine;
                    continue;
                }

                var seg = hit.GetComponentInParent<BeltSegment>();
                if (seg == null || seg == this) continue;

                if (isFront)
                {
                    bool neighborBackIsClose = seg.endpointBack != null &&
                        Vector3.Distance(pos, seg.endpointBack.position)<
                        Vector3.Distance(pos, seg.endpointFront != null
                            ? seg.endpointFront.position : seg.transform.position);

                    if (neighborBackIsClose)
                    {
                        if (nextSegment == null) nextSegment = seg;
                        if (seg.prevSegment == null) seg.prevSegment = this;
                    }
                    else
                    {
                        if (prevSegment == null) prevSegment = seg;
                        if (seg.nextSegment == null) seg.nextSegment = this;
                    }
                }
                else
                {
                    bool neighborFrontIsClose = seg.endpointFront != null &&
                        Vector3.Distance(pos, seg.endpointFront.position)<
                        Vector3.Distance(pos, seg.endpointBack != null
                            ? seg.endpointBack.position : seg.transform.position);

                    if (neighborFrontIsClose)
                    {
                        if (prevSegment == null) prevSegment = seg;
                        if (seg.nextSegment == null) seg.nextSegment = this;
                    }
                    else
                    {
                        if (nextSegment == null) nextSegment = seg;
                        if (seg.prevSegment == null) seg.prevSegment = this;
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
            return true;
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

            // 시각 오브젝트 생성
            BeltSegment first = chain[0];
            Vector3 spawnPos = (first != null && first.endpointBack != null)
                ? first.endpointBack.position
                : (first != null ? first.transform.position : Vector3.zero);

            GameObject visual = null;
            if (beltItemPrefab != null)
            {
                visual = Instantiate(beltItemPrefab, spawnPos, beltItemPrefab.transform.rotation);
                if (visual.TryGetComponent<BeltItemVisual>(out var vis))
                    vis.Setup(itemId, amount);
                _activeVisuals.Add(visual);
            }

            bool pathBroken = false;

            // ── 세그먼트 단위 실시간 이동 ────────────────────────────────────
            // 매 프레임마다 세그먼트 생존 여부를 확인하여
            // 삭제된 벨트를 즉시 감지한다.
            for (int i = 0; i < chain.Count; i++)
            {
                BeltSegment seg = chain[i];

                // 세그먼트가 이미 삭제됐거나 파괴됨
                if (seg == null) { pathBroken = true; break; }

                // 위치값을 미리 캡처 (삭제 이후에도 Vector3 값은 유지됨)
                Vector3 back  = seg.endpointBack  != null ? seg.endpointBack.position  : seg.transform.position;
                Vector3 front = seg.endpointFront != null ? seg.endpointFront.position : seg.transform.position + seg.transform.forward;
                Vector3 mid   = (back + front) * 0.5f;
                float   half  = seg.travelTimePerSegment / 2f;

                bool isLast = (i == chain.Count - 1);

                // 마지막 세그먼트에서 수락 불가 시 → 설비 내부(front)까지 이동 안 함
                // 거부된 아이템은 mid(벨트 중간)에서 대기
                bool willBeRejected = isLast && (targetM == null || !targetM.CanReceive(itemId));
                Vector3 dest = willBeRejected ? mid : front;

                // 구간 ①: back → mid
                float elapsed = 0f;
                while (elapsed < half)
                {
                    if (seg == null) { pathBroken = true; break; }
                    elapsed += Time.deltaTime;
                    if (visual != null)
                        visual.transform.position = Vector3.Lerp(back, mid, elapsed / half);
                    yield return null;
                }
                if (pathBroken) break;

                // 구간 ②: mid → dest  (거부 시 mid = dest이므로 즉시 완료)
                elapsed = 0f;
                while (elapsed < half)
                {
                    if (seg == null) { pathBroken = true; break; }
                    elapsed += Time.deltaTime;
                    if (visual != null)
                        visual.transform.position = Vector3.Lerp(mid, dest, elapsed / half);
                    yield return null;
                }
                if (pathBroken) break;

                // 다음 세그먼트가 이미 삭제됐으면 경로 파괴로 처리
                if (!isLast && chain[i + 1] == null)
                {
                    pathBroken = true;
                    break;
                }
            }

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
                // 경로 파괴(벨트 삭제) → 즉시 창고로
                token.isRescued = true;
                InventoryManager.StorageInstance?.AddItem(itemId, amount);
                Debug.Log($"[Belt] 경로 파괴 → 아이템 {itemId} x{amount} 즉시 창고 이동");
            }
            else if (targetM == null)
            {
                // 목표 설비 소멸 → 즉시 창고로
                token.isRescued = true;
                InventoryManager.StorageInstance?.AddItem(itemId, amount);
                Debug.Log($"[Belt] 목표 설비 없음 → 아이템 {itemId} x{amount} 창고 이동");
            }
            else if (!targetM.CanReceive(itemId))
            {
                // 레시피 불일치 → mid(설비 입구 직전)에서 1.5초 대기 후 창고로
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
                // 정상 전달
                token.isDelivered = true;
                targetM.Receive(itemId, amount);
            }

            CleanupVisual(visual);
            _inFlightItems.Remove(token);

            // 벨트가 비워졌으면 다음 OutputBuffer 아이템 발송
            // 스택된 아이템이 남아 있을 때만 2초 딜레이 (새로 생산된 아이템은 즉시 발송)
            if (!IsBusy)
            {
                if (sourceM != null && sourceM.OutputBuffer.Stock.Count > 0)
                    yield return new WaitForSeconds(2f);
                sourceM?.TryDispatchPendingOutput();
            }
        }

        private void CleanupVisual(GameObject visual)
        {
            if (visual == null) return;
            _activeVisuals.Remove(visual);
            Destroy(visual);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            if (endpointFront != null) Gizmos.DrawWireSphere(endpointFront.position, detectRadius);
            Gizmos.color = Color.red;
            if (endpointBack != null) Gizmos.DrawWireSphere(endpointBack.position, detectRadius);
        }
    }
}