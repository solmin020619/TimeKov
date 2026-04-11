using UnityEngine;

public class HomingFruit : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private LayerMask playerMask;

    [Header("Stats")]
    [SerializeField] private float wakeRange = 8f;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float contactDamage = 10f;

    [Header("Option")]
    [SerializeField] private bool rotateToTarget = true;

    private bool isAwake = false;
    private float wakeRangeSqr;

    private void Awake()
    {
        wakeRangeSqr = wakeRange * wakeRange;

        if (target == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null)
                target = pc.transform;
        }
    }

    private void Update()
    {
        if (target == null)
            return;

        if (!isAwake)
        {
            float distSqr = (target.position - transform.position).sqrMagnitude;

            if (distSqr <= wakeRangeSqr)
                isAwake = true;
            else
                return;
        }

        Vector3 dir = (target.position - transform.position).normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;

        if (rotateToTarget && dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    public void DestroySelf()
    {
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerMask) == 0)
            return;

        PlayerTime playerTime = other.GetComponent<PlayerTime>();
        if (playerTime != null)
            playerTime.TakeDamage(contactDamage);

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, wakeRange);
    }
}