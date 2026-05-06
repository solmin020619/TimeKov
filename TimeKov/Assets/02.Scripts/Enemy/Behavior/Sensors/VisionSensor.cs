using UnityEngine;

/// <summary>
/// 시야 감지 컴포넌트. 일정 간격으로 시야 범위 안의 targetMask 콜라이더를 스캔하고,
/// 시야각 + 장애물(obstacleMask) 통과 여부를 검사해서 보이는 가장 가까운 타겟을 SpottedTarget 으로 노출한다.
/// EnemyBrain 이 이 결과를 BT Blackboard 의 Target 변수에 주입.
/// </summary>
public class VisionSensor : MonoBehaviour
{
    [Header("Vision")]
    [SerializeField] private float visionRange = 12f;
    [SerializeField, Range(0f, 360f)] private float visionAngle = 110f;
    [Tooltip("시야 원점이 적의 transform.position 보다 얼마나 위인지. 보통 머리 높이.")]
    [SerializeField] private float eyeHeight = 1.6f;

    [Header("Layers")]
    [Tooltip("타겟 후보 (예: Player layer).")]
    [SerializeField] private LayerMask targetMask;
    [Tooltip("시야를 가로막는 레이어 (예: Default / Ground / Wall).")]
    [SerializeField] private LayerMask obstacleMask;

    [Header("Tick Rate")]
    [Tooltip("스캔 주기 (초). 매 프레임 스캔 안 하고 0.1초마다 정도가 적당.")]
    [SerializeField] private float scanInterval = 0.1f;

    public Transform SpottedTarget { get; private set; }

    private float scanTimer;

    /// <summary>
    /// EnemyBrain 등이 SO 데이터로 시야 파라미터를 외부에서 덮어쓸 때 사용.
    /// </summary>
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

            // 시야각 체크
            float angle = Vector3.Angle(transform.forward, toTarget);
            if (angle > visionAngle * 0.5f) continue;

            // 장애물 가림 체크: target 까지 가는 사이에 obstacle 이 끼어있으면 스킵.
            // obstacleMask + targetMask 둘 다 포함시켜 raycast 후 처음 맞은 게 target 인지 확인.
            float distance = toTarget.magnitude;
            if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, distance, obstacleMask | targetMask))
            {
                int hitLayerBit = 1 << hit.collider.gameObject.layer;
                if ((hitLayerBit & targetMask) == 0)
                    continue; // 장애물에 막힘
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
