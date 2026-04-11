using UnityEngine;

public class TurretProjectile : MonoBehaviour
{
    private Vector3 moveDir;
    private float moveSpeed;
    private float damage;
    private float lifeTime;
    private float timer;
    private LayerMask targetMask;

    private GameObject hitVfxPrefab;
    private AudioClip hitClip;

    public void Init(
        Vector3 dir,
        float speed,
        float damage,
        float lifeTime,
        LayerMask targetMask,
        GameObject hitVfxPrefab,
        AudioClip hitClip)
    {
        this.moveDir = dir.normalized;
        this.moveSpeed = speed;
        this.damage = damage;
        this.lifeTime = lifeTime;
        this.targetMask = targetMask;
        this.hitVfxPrefab = hitVfxPrefab;
        this.hitClip = hitClip;
    }

    private void Update()
    {
        transform.position += moveDir * moveSpeed * Time.deltaTime;

        timer += Time.deltaTime;
        if (timer >= lifeTime)
            Destroy(gameObject);
    }

    private void SpawnVFX(GameObject prefab, Vector3 position, float lifeTime = 1f)
    {
        if (prefab == null) return;

        GameObject vfx = Instantiate(prefab, position, Quaternion.identity);

        if (lifeTime > 0f)
            Destroy(vfx, lifeTime);
    }

    private void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, volume);
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

        SpawnVFX(hitVfxPrefab, transform.position, 1f);
        PlayClipAtPoint(hitClip, transform.position);

        Destroy(gameObject);
    }
}