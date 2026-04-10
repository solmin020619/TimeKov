using UnityEngine;

public class TurretController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EnemyDataSO data;

    [Header("References")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectilePrefab;

    [Header("Target")]
    [SerializeField] private LayerMask targetMask;

    private Transform target;
    private float lastFireTime;
    private AudioSource audioSource;

    private void Start()
    {
        if (firePoint == null)
            firePoint = transform;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 5f;
        audioSource.maxDistance = 30f;
        audioSource.volume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
    }

    private void PlayClip(AudioClip clip, float volumeScale = 1f)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip, volumeScale);
    }

    private void SpawnVFX(GameObject prefab, Vector3 position, Quaternion rotation, float lifeTime = 1f)
    {
        if (prefab == null) return;

        GameObject vfx = Instantiate(prefab, position, rotation);

        if (lifeTime > 0f)
            Destroy(vfx, lifeTime);
    }

    private void Update()
    {
        if (data == null)
        {
            Debug.LogWarning("[TurretController] EnemyDataSO가 연결되지 않았습니다.");
            return;
        }

        if (data.enemyType != EnemyType.Turret)
        {
            Debug.LogWarning("[TurretController] data.enemyType이 Turret이 아닙니다.");
            return;
        }

        FindTarget();

        if (target == null)
            return;

        RotateTowardsTarget();

        float distSqr = (target.position - transform.position).sqrMagnitude;
        float rangeSqr = data.turretRange * data.turretRange;

        if (distSqr <= rangeSqr && Time.time >= lastFireTime + data.turretFireCooldown)
        {
            Fire();
            lastFireTime = Time.time;
        }
    }

    private void FindTarget()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc == null)
        {
            target = null;
            return;
        }

        float distSqr = (pc.transform.position - transform.position).sqrMagnitude;
        float rangeSqr = data.turretRange * data.turretRange;

        if (distSqr <= rangeSqr)
            target = pc.transform;
        else
            target = null;
    }

    private void RotateTowardsTarget()
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            data.turretRotateSpeed * Time.deltaTime
        );
    }

    private void Fire()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[TurretController] projectilePrefab이 없습니다.");
            return;
        }

        Vector3 spawnPos = firePoint.position;
        Vector3 dir = (target.position - firePoint.position).normalized;

        SpawnVFX(data.turretMuzzleVFXPrefab, firePoint.position, firePoint.rotation, 1f);
        PlayClip(data.turretFireSound);

        GameObject projectileObj = Instantiate(
            projectilePrefab,
            spawnPos,
            Quaternion.LookRotation(dir)
        );

        TurretProjectile turretProjectile = projectileObj.GetComponent<TurretProjectile>();
        if (turretProjectile != null)
        {
            turretProjectile.Init(
                dir,
                data.turretProjectileSpeed,
                data.attackDamage,
                data.turretProjectileLifeTime,
                targetMask,
                data.hitVFXPrefab,
                data.hitSound
            );
            return;
        }

        Rigidbody rb = projectileObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = dir * data.turretProjectileSpeed;
            Destroy(projectileObj, data.turretProjectileLifeTime);
            return;
        }

        Debug.LogWarning("[TurretController] 발사체에 TurretProjectile 또는 Rigidbody가 없습니다.");
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, data.turretRange);
    }
}