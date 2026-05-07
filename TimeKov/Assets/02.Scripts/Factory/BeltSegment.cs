// =====================================================================
// BeltSegment.cs
// 1칸짜리 벨트 오브젝트에 붙이는 컴포넌트.
//
// 동작 원리:
//   - 오브젝트 앞뒤에 작은 Trigger(BeltEndpoint)가 두 개 붙어 있다.
//   - 배치 직후 양끝 Trigger에 닿는 것이 설비면 → source/target 확정.
//   - 닿는 것이 다른 BeltSegment면 → 체인을 따라가서 설비를 찾는다.
//   - 체인 양쪽 끝에 설비가 모두 연결되면 자동으로 아이템 운반 시작.
//
// 프리팹 구조:
//   BeltSegment (이 스크립트)
//     ├── Endpoint_Front   ← BoxCollider (isTrigger, 작은 크기)
//     └── Endpoint_Back    ← BoxCollider (isTrigger, 작은 크기)
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class BeltSegment : MonoBehaviour
    {
        [Header("벨트 시각화")]
        [Tooltip("벨트 위 아이템 오브젝트 프리팹 (BeltItemVisual 필요)")]
        public GameObject beltItemPrefab;

        [Tooltip("아이템이 이동하는 속도 (초/칸)")]
        public float travelTimePerSegment = 0.5f;

        // ── 런타임에 자동으로 채워지는 연결 정보 ───────────────────
        // 이 세그먼트의 앞뒤에 연결된 세그먼트 or 설비
        [HideInInspector] public BeltSegment prevSegment; // 아이템이 오는 방향
        [HideInInspector] public BeltSegment nextSegment; // 아이템이 가는 방향
        [HideInInspector] public MachineBase sourceM;    // 체인 시작 설비
        [HideInInspector] public MachineBase targetM;    // 체인 끝 설비

        // 체인에서 자신이 source쪽 첫 세그먼트인지
        public bool IsHead => prevSegment == null && sourceM != null;
        public bool IsReady => sourceM != null && targetM != null;

        // ── 앞뒤 Endpoint Transform (Inspector에서 직접 지정) ───────
        [Header("앞뒤 감지 포인트 (자식 오브젝트)")]
        public Transform endpointFront; // 아이템이 나가는 쪽
        public Transform endpointBack;  // 아이템이 들어오는 쪽

        [Header("감지 반경")]
        public float detectRadius = 0.6f;

        // 감지 레이어 (설비 + 벨트)
        [Header("감지 레이어 (설비 + BeltSegment 레이어 포함)")]
        public LayerMask detectMask;

        // ============================================================
        // 배치 직후 자동 감지
        // ============================================================

        private void Start()
        {
            // 한 프레임 뒤에 실행 (인접 오브젝트가 배치 완료되길 기다림)
            StartCoroutine(DetectNextFrame());
        }

        private IEnumerator DetectNextFrame()
        {
            yield return null;
            DetectConnections();

            yield return null;
            PropagateChain();
        }

        // ── 양끝 감지 ───────────────────────────────────────────────
        public void DetectConnections()
        {
            if (endpointBack  != null) DetectAt(endpointBack.position,  isFront: false);
            if (endpointFront != null) DetectAt(endpointFront.position, isFront: true);

            // 체인 전체 재연결
            //PropagateChain();
        }

        private void DetectAt(Vector3 pos, bool isFront)
        {
            var hits = Physics.OverlapSphere(pos, detectRadius, detectMask);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                // 설비 감지 — outputPort/inputPort 기준으로 source/target 결정
                var machine = hit.GetComponentInParent<MachineBase>();
                if (machine != null)
                {

                    bool nearOutput = false;

                    if (machine.outputPort != null && machine.inputPort != null)
                    {
                        nearOutput = Vector3.Distance(pos, machine.outputPort.position)<
                                     Vector3.Distance(pos, machine.inputPort.position);
                    }
                    else if (machine.outputPort != null)
                    {
                        nearOutput = true;
                    }
                    Debug.Log($"[Belt] {gameObject.name} 설비감지: {machine.name} nearOutput={nearOutput} outputPort={machine.outputPort?.name ?? "null"} inputPort={machine.inputPort?.name ?? "null"}");

                    if (nearOutput)
                        sourceM = machine;
                    else
                        targetM = machine;

                    continue;
                }

                // 세그먼트 연결
                var seg = hit.GetComponentInParent<BeltSegment>();
                if (seg != null && seg != this)
                {
                    if (isFront) // 내 앞쪽 endpoint
                    {
                        bool neighborBackIsClose = seg.endpointBack != null &&
                            Vector3.Distance(pos, seg.endpointBack.position)<
                            Vector3.Distance(pos, seg.endpointFront != null
                ? seg.endpointFront.position : seg.transform.position);

                        if (neighborBackIsClose) // 상대 Back이 가까움 → 내가 앞
                        {
                            if (nextSegment == null) nextSegment = seg;
                            if (seg.prevSegment == null) seg.prevSegment = this;
                        }
                        else // 상대 Front가 가까움 → 내가 뒤
                        {
                            if (prevSegment == null) prevSegment = seg;
                            if (seg.nextSegment == null) seg.nextSegment = this;
                        }
                    }
                    else // 내 뒤쪽 endpoint
                    {
                        bool neighborFrontIsClose = seg.endpointFront != null &&
                            Vector3.Distance(pos, seg.endpointFront.position)<
                            Vector3.Distance(pos, seg.endpointBack != null
                ? seg.endpointBack.position : seg.transform.position);

                        if (neighborFrontIsClose) // 상대 Front가 가까움 → 상대가 앞
                        {
                            if (prevSegment == null) prevSegment = seg;
                            if (seg.nextSegment == null) seg.nextSegment = this;
                        }
                        else // 상대 Back이 가까움 → 내가 앞
                        {
                            if (nextSegment == null) nextSegment = seg;
                            if (seg.prevSegment == null) seg.prevSegment = this;
                        }
                    }
                }
            }
        }

        // ── 체인 헤드(가장 앞 세그먼트) 반환 ───────────────────────
        public BeltSegment GetChainHead()
        {
            BeltSegment cur = this;
            int safety = 100;
            while (cur.prevSegment != null && safety-- > 0)
                cur = cur.prevSegment;
            return cur;
        }

        // ── 체인 전체에 source/target 전파 ─────────────────────────
        private void PropagateChain()
        {
            // 체인 헤드를 찾아서 끝까지 순회
            var head = GetChainHead();
            head.TraverseAndSet();
        }

        private void TraverseAndSet()
        {
            var chain = new List<BeltSegment>();
            BeltSegment cur = this;
            int safety = 200;
            while (cur != null && safety-- > 0)
            {
                chain.Add(cur);
                cur = cur.nextSegment;
            }

            // chain[0]/chain[last]만 보지 않고 체인 전체에서 찾기
            MachineBase src = null;
            MachineBase tgt = null;

            foreach (var seg in chain)
            {
                if (seg.sourceM != null) src = seg.sourceM;
                if (seg.targetM != null) tgt = seg.targetM;
            }

            Debug.Log($"[Belt] {gameObject.name} 체인길이={chain.Count} src={src?.name ?? "null"} tgt={tgt?.name ?? "null"}");

            if (src == null || tgt == null || src == tgt) return;

            foreach (var seg in chain)
            {
                seg.sourceM = src;
                seg.targetM = tgt;
            }

            src.outputBelt = chain[0];
            Debug.Log($"[Belt] 연결완성: {src.name} → {tgt.name}");
        }

        public bool TryTransport(int itemId, int amount)
        {
            if (!IsReady) return false;
            StartCoroutine(ChainTransportRoutine(itemId, amount));
            return true;
        }

        private IEnumerator ChainTransportRoutine(int itemId, int amount)
        {
            var chain = new List<BeltSegment>();
            BeltSegment cur = GetChainHead();
            int safety = 200;
            while (cur != null && safety-- > 0)
            {
                chain.Add(cur);
                cur = cur.nextSegment;
            }

            // 체인 방향 확인 — chain[0]이 sourceM에 가까워야 함
            // 반대면 뒤집기
            if (sourceM != null && chain.Count > 1)
            {
                float distFirst = Vector3.Distance(chain[0].transform.position, sourceM.transform.position);
                float distLast = Vector3.Distance(chain[chain.Count - 1].transform.position, sourceM.transform.position);
                if (distLast < distFirst)
                    chain.Reverse();
            }

            // 이하 기존 코드 동일
            GameObject visual = null;
            if (beltItemPrefab != null && chain[0].endpointBack != null)
            {
                visual = Instantiate(beltItemPrefab, chain[0].endpointBack.position, beltItemPrefab.transform.rotation);
                if (visual.TryGetComponent<BeltItemVisual>(out var vis))
                    vis.Setup(itemId, amount);
            }

            for (int i = 0; i < chain.Count; i++)
            {
                Vector3 from = chain[i].endpointBack != null
                    ? chain[i].endpointBack.position
                    : chain[i].transform.position;

                Vector3 to = chain[i].endpointFront != null
                    ? chain[i].endpointFront.position
                    : chain[i].transform.position + chain[i].transform.forward;

                float elapsed = 0f;
                while (elapsed < travelTimePerSegment)
                {
                    elapsed += Time.deltaTime;
                    if (visual != null)
                        visual.transform.position = Vector3.Lerp(from, to, elapsed / travelTimePerSegment);
                    yield return null;
                }
            }

            if (visual != null) Destroy(visual);
            targetM?.Receive(itemId, amount);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            if (endpointFront != null)
                Gizmos.DrawWireSphere(endpointFront.position, detectRadius);
            Gizmos.color = Color.red;
            if (endpointBack != null)
                Gizmos.DrawWireSphere(endpointBack.position, detectRadius);
        }
    }

}
