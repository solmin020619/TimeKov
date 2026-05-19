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
            src.outputBelt = chain[0];
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
            var cur = GetChainHead();
            int safety = 200;
            while (cur != null && safety-- > 0) { chain.Add(cur); cur = cur.nextSegment; }

            var waypoints = new List<Vector3>();
            for (int i = 0; i < chain.Count; i++)
            {
                Vector3 back = chain[i].endpointBack != null ? chain[i].endpointBack.position : chain[i].transform.position;
                Vector3 front = chain[i].endpointFront != null ? chain[i].endpointFront.position : chain[i].transform.position + chain[i].transform.forward;
                if (i == 0) waypoints.Add(back);
                waypoints.Add((back + front) * 0.5f);
                waypoints.Add(front);
            }

            GameObject visual = null;
            if (beltItemPrefab != null && waypoints.Count > 0)
            {
                visual = Instantiate(beltItemPrefab, waypoints[0], beltItemPrefab.transform.rotation);
                if (visual.TryGetComponent<BeltItemVisual>(out var vis))
                    vis.Setup(itemId, amount);
            }

            float timePerPoint = travelTimePerSegment / 2f;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector3 from = waypoints[i], to = waypoints[i + 1];
                float elapsed = 0f;
                while (elapsed < timePerPoint)
                {
                    elapsed += Time.deltaTime;
                    if (visual != null)
                        visual.transform.position = Vector3.Lerp(from, to, elapsed / timePerPoint);
                    yield return null;
                }
            }

            if (visual != null) Destroy(visual);
            targetM?.Receive(itemId, amount);
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