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

    public Transform SpottedTarget { get; private set; }

    private float scanTimer;

    public void ApplyVisionParameters(float range, float angle)
    {
        visionRange = range;
        visionAngle = angle;
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
        Collider[] hits = Physics.OverlapSphere(origin, visionRange, targetMask);

        Transform best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform t = hits[i].transform;
            Vector3 toTarget = (t.position + Vector3.up * eyeHeight) - origin;

            float angle = Vector3.Angle(transform.forward, toTarget);
            if (angle > visionAngle * 0.5f) continue;

            float distance = toTarget.magnitude;
            if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, distance, obstacleMask | targetMask))
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

        SpottedTarget = best;
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
