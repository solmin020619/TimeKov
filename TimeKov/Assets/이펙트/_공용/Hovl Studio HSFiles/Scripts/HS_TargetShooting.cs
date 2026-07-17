using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hovl
{
    public class HS_TargetShooting : MonoBehaviour
    {
        [Header("Target")]
        public Collider targetCollider;

        [Header("Projectile")]
        public GameObject projectilePrefab;
        public Transform firePoint;
        public int poolSize = 20;

        [Header("Shooting")]
        public float shootingRate = 2f;
        public bool shootOnStart = true;

        [Header("Rotation")]
        public bool rotateToTarget = true;

        [Header("Flash Material")]
        public MeshRenderer targetMeshRenderer;
        public int materialIndex = 0;
        public string colorPropertyName = "_Emission_color";
        public Color shootFlashColor = Color.red;
        public float flashDuration = 0.1f;

        readonly List<GameObject> projectilePool = new List<GameObject>();

        float shootTimer;
        float shootInterval;
        Vector3 currentTargetPoint;

        Material runtimeMaterialInstance;
        Color originalColor;
        bool hasValidColorProperty;
        Coroutine flashRoutine;

        void Start()
        {
            if (firePoint == null)
                firePoint = transform;

            shootInterval = 1f / Mathf.Max(0.01f, shootingRate);

            CreatePool();
            SetupMaterialInstance();

            if (shootOnStart)
                shootTimer = shootInterval;
        }

        void Update()
        {
            if (targetCollider == null || projectilePrefab == null)
                return;

            shootTimer += Time.deltaTime;

            if (shootTimer >= shootInterval)
            {
                shootTimer = 0f;

                currentTargetPoint = GetRandomPointOnCollider(targetCollider);

                if (rotateToTarget)
                    RotateToPoint(currentTargetPoint);

                Shoot(currentTargetPoint);
                TriggerFlash();
            }
        }

        void CreatePool()
        {
            projectilePool.Clear();

            for (int i = 0; i < poolSize; i++)
            {
                GameObject proj = Instantiate(projectilePrefab);
                proj.SetActive(false);
                projectilePool.Add(proj);
            }
        }

        GameObject GetPooledProjectile()
        {
            for (int i = 0; i < projectilePool.Count; i++)
            {
                if (!projectilePool[i].activeInHierarchy)
                    return projectilePool[i];
            }

            GameObject proj = Instantiate(projectilePrefab);
            proj.SetActive(false);
            projectilePool.Add(proj);
            return proj;
        }

        void Shoot(Vector3 targetPoint)
        {
            GameObject proj = GetPooledProjectile();

            Vector3 shootDirection = (targetPoint - firePoint.position).normalized;
            if (shootDirection.sqrMagnitude < 0.0001f)
                shootDirection = firePoint.forward;

            proj.transform.position = firePoint.position;
            proj.transform.rotation = Quaternion.LookRotation(shootDirection);

            proj.SetActive(true);
        }

        void RotateToPoint(Vector3 point)
        {
            Vector3 dir = point - firePoint.position;

            if (dir.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        Vector3 GetRandomPointOnCollider(Collider col)
        {
            Bounds b = col.bounds;

            for (int i = 0; i < 12; i++)
            {
                Vector3 randomPoint = new Vector3(
                    Random.Range(b.min.x, b.max.x),
                    Random.Range(b.min.y, b.max.y),
                    Random.Range(b.min.z, b.max.z)
                );

                Vector3 p = col.ClosestPoint(randomPoint);

                if ((p - randomPoint).sqrMagnitude > 0.000001f)
                    return p;
            }

            return col.ClosestPoint(b.center);
        }

        void SetupMaterialInstance()
        {
            if (targetMeshRenderer == null)
                return;

            Material[] mats = targetMeshRenderer.materials;
            if (mats == null || mats.Length == 0)
                return;

            if (materialIndex < 0 || materialIndex >= mats.Length)
            {
                Debug.LogWarning($"{name}: Material Index is out of range.");
                return;
            }

            runtimeMaterialInstance = mats[materialIndex];

            if (runtimeMaterialInstance == null)
                return;

            hasValidColorProperty = runtimeMaterialInstance.HasProperty(colorPropertyName);

            if (!hasValidColorProperty)
            {
                Debug.LogWarning($"{name}: Material does not have color property '{colorPropertyName}'.");
                return;
            }

            originalColor = runtimeMaterialInstance.GetColor(colorPropertyName);
        }

        void TriggerFlash()
        {
            if (runtimeMaterialInstance == null || !hasValidColorProperty)
                return;

            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            flashRoutine = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            runtimeMaterialInstance.SetColor(colorPropertyName, shootFlashColor);
            yield return new WaitForSeconds(flashDuration);

            if (runtimeMaterialInstance != null)
                runtimeMaterialInstance.SetColor(colorPropertyName, originalColor);

            flashRoutine = null;
        }

        void OnDisable()
        {
            if (runtimeMaterialInstance != null && hasValidColorProperty)
                runtimeMaterialInstance.SetColor(colorPropertyName, originalColor);
        }

        void OnDrawGizmosSelected()
        {
            if (firePoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(firePoint.position, 0.05f);
                Gizmos.DrawLine(firePoint.position, currentTargetPoint);
            }

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(currentTargetPoint, 0.08f);
        }
    }
}