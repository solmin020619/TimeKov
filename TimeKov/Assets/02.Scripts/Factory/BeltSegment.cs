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

                var machine = hit.GetComponentInParent<MachineBase>();
                if (machine != null)
                {
                    if (isFront) targetM = machine;
                    else sourceM = machine;

                    if (isFront) machine.inputBelt = GetChainHead();
                    else machine.outputBelt = GetChainHead();
                    continue;
                }

                var seg = hit.GetComponentInParent<BeltSegment>();
                if (seg != null && seg != this)
                {
                    bool neighborBackIsClose = seg.endpointBack != null &&
                        Vector3.Distance(endpointFront != null ? endpointFront.position : pos, seg.endpointBack.position)<
                        Vector3.Distance(endpointFront != null ? endpointFront.position : pos, seg.endpointFront != null ? seg.endpointFront.position : seg.transform.position);

                    bool neighborFrontIsClose = seg.endpointFront != null &&
                        Vector3.Distance(endpointBack != null ? endpointBack.position : pos, seg.endpointFront.position)<
                        Vector3.Distance(endpointBack != null ? endpointBack.position : pos, seg.endpointBack != null ? seg.endpointBack.position : seg.transform.position);

                    // ↓ 여기가 교체할 부분
                    if (isFront)
                    {
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
            // 1) 체인 전체 세그먼트 수집
            var chain = new List<BeltSegment>();
            BeltSegment cur = this;
            int safety = 200;
            while (cur != null && safety-- > 0)
            {
                chain.Add(cur);
                cur = cur.nextSegment;
            }

            Debug.Log($"[Belt] 체인: {string.Join(" → ", chain.ConvertAll(s => s.gameObject.name))}");

            // 2) 양 끝 설비 찾기
            MachineBase src = null;
            MachineBase tgt = null;

            Debug.Log($"[Belt] src={src?.name ?? "null"}, tgt={tgt?.name ?? "null"}");

            // Back쪽: chain[0].prevSegment 없고 sourceM 있으면 src
            src = chain[0].sourceM;
            tgt = chain[chain.Count - 1].targetM;

            // 3) 체인 전체에 전파
            foreach (var seg in chain)
            {
                seg.sourceM = src;
                seg.targetM = tgt;
            }

            // 4) source 설비의 outputBelt를 chain[0]으로 세팅
            if (src != null) src.outputBelt = chain[0];

            // 5) 연결 완성되면 헤드가 아이템 수신 준비
            if (src != null && tgt != null)
                Debug.Log($"[Belt] 체인 연결 완성: {src.name} → ({chain.Count}칸) → {tgt.name}");


        }

        // ============================================================
        // 아이템 운반 (체인 헤드가 호출 — MachineBase.Dispatch 에서)
        // ============================================================

        public bool TryTransport(int itemId, int amount)
        {
            if (!IsReady) return false;
            StartCoroutine(ChainTransportRoutine(itemId, amount));
            return true;
        }

        private IEnumerator ChainTransportRoutine(int itemId, int amount)
        {
            // 체인 전체 세그먼트 수집
            var chain = new List<BeltSegment>();
            BeltSegment cur = GetChainHead();
            int safety = 200;
            while (cur != null && safety-- > 0)
            {
                chain.Add(cur);
                cur = cur.nextSegment;
            }

            // 시각 오브젝트 생성
            GameObject visual = null;
            if (beltItemPrefab != null && chain[0].endpointBack != null)
            {
                visual = Instantiate(beltItemPrefab, chain[0].endpointBack.position, beltItemPrefab.transform.rotation);
                if (visual.TryGetComponent<BeltItemVisual>(out var vis))
                    vis.Setup(itemId, amount);
            }

            // 세그먼트를 하나씩 통과하며 이동
            for (int i = 0; i < chain.Count; i++)
            {
                Vector3 from = chain[i].endpointBack  != null
                    ? chain[i].endpointBack.position
                    : chain[i].transform.position;

                Vector3 to   = chain[i].endpointFront != null
                    ? chain[i].endpointFront.position
                    : chain[i].transform.position + chain[i].transform.forward;

                float elapsed = 0f;
                while (elapsed < travelTimePerSegment)
                {
                    elapsed += Time.deltaTime;
                    if (visual != null)
                        visual.transform.position =
                            Vector3.Lerp(from, to, elapsed / travelTimePerSegment);
                    yield return null;
                }
            }

            if (visual != null) Destroy(visual);

            // 목적지 설비에 전달
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
