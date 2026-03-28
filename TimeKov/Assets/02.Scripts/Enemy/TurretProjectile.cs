using UnityEngine;

public class TurretProjectile : MonoBehaviour
{
    private Vector3 moveDir;
    private float moveSpeed;
    private float damage;
    private float lifeTime;
    private float timer;
    private LayerMask targetMask;

    public void Init(Vector3 dir, float speed, float damage, float lifeTime, LayerMask targetMask)
    {
        this.moveDir = dir.normalized;
        this.moveSpeed = speed;
        this.damage = damage;
        this.lifeTime = lifeTime;
        this.targetMask = targetMask;
    }

    private void Update()
    {
        transform.position += moveDir * moveSpeed * Time.deltaTime;

        timer += Time.deltaTime;
        if (timer >= lifeTime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[TurretProjectile] 충돌: {other.name}");

        if (((1 << other.gameObject.layer) & targetMask) == 0)
        {
            Debug.Log($"[TurretProjectile] targetMask 아님: {other.name}");
            return;
        }

        PlayerTime playerTime = other.GetComponentInParent<PlayerTime>();
        if (playerTime != null)
        {
            playerTime.TakeDamage(damage);
            Debug.Log($"[TurretProjectile] 데미지 적용 성공: {damage}");
        }
        else
        {
            Debug.LogWarning($"[TurretProjectile] PlayerTime 못 찾음: {other.name}");
        }

        Destroy(gameObject);
    }
}