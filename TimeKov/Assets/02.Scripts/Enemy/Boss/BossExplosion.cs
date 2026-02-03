using UnityEngine;

public class BossExplosion : MonoBehaviour
{
    public float damage = 20f;
    public float radius = 3.0f;
    public LayerMask targetLayer;

    void Start()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, targetLayer);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerTime pt = hit.GetComponent<PlayerTime>();
                if (pt != null)
                {
                    pt.TakeDamage(damage);
                }
                Debug.Log($"폭발 적중! 대상: {hit.name}");
            }
        }

        Destroy(gameObject, 2.0f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}