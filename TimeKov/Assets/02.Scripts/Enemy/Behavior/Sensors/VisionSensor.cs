using UnityEngine;

public class VisionSensor : MonoBehaviour
{
    [Header("Vision")]
    [SerializeField] private float visionRange = 12f;
    [SerializeField, Range(0f, 360f)] private float visionAngle = 110f;
    [SerializeField] private float eyeHeight = 1.6f;

    [Header("Layers")]
    [SerializeField] private LayerMask targetMask;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Tick Rate")]
    [SerializeField] private float scanInterval = 0.1f;

    [Header("Lost Memory")]
    [Tooltip("시야에서 벗어난 후에도 이 시간 동안 마지막 타깃을 유지. 즉시 정지 방지.")]
    [SerializeField] private float lostMemory = 1.5f;

    public Transform SpottedTarget { get; private set; }

    private float scanTimer;
    private Transform lastSeen;
    private float lostTimer;
    private readonly Collider[] _hitBuffer = new Collider[8];   // OverlapSphereNonAlloc 재사용 버퍼(매 스캔 힙 할당 방지). targetMask=Player라 8개면 충분.

    public void ApplyVisionParameters(float range, float angle)
    {
        visionRange = range;
        visionAngle = angle;
    }

    public void ApplyLostMemory(float seconds)
    {
        lostMemory = seconds;
    }

    /// <summary>
    /// 외부에서 타깃을 강제로 설정 (피격 시 가해자 즉시 인식 등).
    /// 시야 raycast 우회 — 뒤에서 맞아도 인식 가능.
    /// </summary>
    public void ForceSetTarget(Transform target)
    {
        if (target == null) return;
        SpottedTarget = target;
        lastSeen = target;
        lostTimer = 0f;
    }

    private void Update()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = scanInterval;
        Scan();
    }

    private void Scan()
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        int count = Physics.OverlapSphereNonAlloc(origin, visionRange, _hitBuffer, targetMask);

        Transform best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Transform t = _hitBuffer[i].transform;
            Vector3 toTarget = (t.position + Vector3.up * eyeHeight) - origin;

            float angle = Vector3.Angle(transform.forward, toTarget);
            if (angle > visionAngle * 0.5f) continue;

            float distance = toTarget.magnitude;
            // QueryTriggerInteraction.Ignore 필수: 안 넣으면 트리거 콜라이더가 시야를 막는다.
            // 스포너 박스(isTrigger)가 적 시야를 가려서 특정 높이에서만 플레이어를 못 보던 원인.
            if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, distance, obstacleMask | targetMask, QueryTriggerInteraction.Ignore))
            {
                int hitLayerBit = 1 << hit.collider.gameObject.layer;
                if ((hitLayerBit & targetMask) == 0)
                    continue;
            }

            float sqr = toTarget.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }

        if (best != null)
        {
            // 시야 안에 들어옴 → 즉시 갱신 + 메모리 리셋
            SpottedTarget = best;
            lastSeen = best;
            lostTimer = 0f;
        }
        else if (lastSeen != null)
        {
            // 시야 밖 → 메모리 시간 동안 마지막 타깃 유지
            lostTimer += scanInterval;
            if (lostTimer >= lostMemory)
            {
                lastSeen = null;
                SpottedTarget = null;
            }
            else
            {
                SpottedTarget = lastSeen;
            }
        }
        else
        {
            SpottedTarget = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;

        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        Gizmos.DrawWireSphere(origin, visionRange);

        Vector3 left = Quaternion.Euler(0f, -visionAngle * 0.5f, 0f) * transform.forward * visionRange;
        Vector3 right = Quaternion.Euler(0f, visionAngle * 0.5f, 0f) * transform.forward * visionRange;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin, left);
        Gizmos.DrawRay(origin, right);

        if (SpottedTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, SpottedTarget.position + Vector3.up * eyeHeight);
        }
    }
}
