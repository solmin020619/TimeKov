using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hovl
{
    public class HS_Impact_position : MonoBehaviour
    {
        public enum HitSourceMode
        {
            MouseClicks,
            ObjectCollisions,
            Both
        }

        [Header("Main")]
        public Collider targetCollider;
        public HitSourceMode hitSourceMode = HitSourceMode.MouseClicks;

        [Header("Shader settings")]
        [Min(1)] public int maxHits = 20;
        public string shaderPropertyName = "_hit";
        public float invalidHitTime = -9999f;

        [Header("Collision filtering")]
        public bool useCollisionTagFilter = false;
        public string requiredCollisionTag = "Projectile";
        public bool acceptCollisionEnter = true;
        public bool acceptTriggerEnter = true;

        MeshRenderer[] _meshRenderers;
        readonly List<Vector4> _hitPositions = new List<Vector4>(20);

        void Awake()
        {
            if (targetCollider == null)
                targetCollider = GetComponent<Collider>();

            _meshRenderers = GetComponentsInChildren<MeshRenderer>(true);

            if (_hitPositions.Capacity < maxHits)
                _hitPositions.Capacity = maxHits;
        }

        void OnValidate()
        {
            if (maxHits < 1)
                maxHits = 1;
        }

        void Update()
        {
            if (hitSourceMode == HitSourceMode.ObjectCollisions)
                return;

            bool clicked = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                clicked = Mouse.current.leftButton.wasPressedThisFrame;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!clicked)
                clicked = Input.GetMouseButtonDown(0);
#endif

            if (clicked)
                TryRegisterMouseHit();
        }

        void TryRegisterMouseHit()
        {
            if (targetCollider == null)
            {
                targetCollider = GetComponent<Collider>();
                if (targetCollider == null)
                    return;
            }

            Vector3 mousePos = Vector3.zero;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                mousePos = Mouse.current.position.ReadValue();
            else
#endif
                mousePos = Input.mousePosition;

            Camera cam = Camera.main;
            if (cam == null)
                cam = Camera.current;
            if (cam == null)
                return;

            Ray ray = cam.ScreenPointToRay(mousePos);

            if (!targetCollider.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
                return;

            RegisterHit(hit.point);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!acceptCollisionEnter)
                return;

            if (hitSourceMode == HitSourceMode.MouseClicks)
                return;

            if (!IsCollisionAllowed(collision.collider))
                return;

            if (collision.contactCount > 0)
            {
                RegisterHit(collision.GetContact(0).point);
            }
            else
            {
                Vector3 fallbackPoint = collision.collider.ClosestPoint(transform.position);
                RegisterHit(fallbackPoint);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!acceptTriggerEnter)
                return;

            if (hitSourceMode == HitSourceMode.MouseClicks)
                return;

            if (!IsCollisionAllowed(other))
                return;

            Vector3 hitPoint = other.ClosestPoint(transform.position);
            RegisterHit(hitPoint);
        }

        bool IsCollisionAllowed(Collider other)
        {
            if (other == null)
                return false;

            if (targetCollider != null && other == targetCollider)
                return false;

            if (useCollisionTagFilter && !other.CompareTag(requiredCollisionTag))
                return false;

            return true;
        }

        void RegisterHit(Vector3 worldHitPos)
        {
            Vector4 hitVec = new Vector4(worldHitPos.x, worldHitPos.y, worldHitPos.z, Time.time);
            _hitPositions.Add(hitVec);

            if (_hitPositions.Count > maxHits)
            {
                int removeCount = _hitPositions.Count - maxHits;
                _hitPositions.RemoveRange(0, removeCount);
            }

            ApplyHitsToMaterials();
        }

        void ApplyHitsToMaterials()
        {
            if (_meshRenderers == null || _meshRenderers.Length == 0)
                _meshRenderers = GetComponentsInChildren<MeshRenderer>(true);

            Vector4[] hitArray = new Vector4[maxHits];

            for (int i = 0; i < maxHits; i++)
                hitArray[i] = new Vector4(0f, 0f, 0f, invalidHitTime);

            int count = Mathf.Min(_hitPositions.Count, maxHits);
            for (int i = 0; i < count; i++)
                hitArray[i] = _hitPositions[i];

            foreach (var mr in _meshRenderers)
            {
                if (mr == null)
                    continue;

                Material[] mats = mr.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material mat = mats[i];
                    if (mat == null)
                        continue;

                    mat.SetVectorArray(shaderPropertyName, hitArray);
                }
            }
        }

        [ContextMenu("Clear Hits")]
        public void ClearHits()
        {
            _hitPositions.Clear();
            ApplyHitsToMaterials();
        }

        public void AddHitFromWorldPosition(Vector3 worldPosition)
        {
            RegisterHit(worldPosition);
        }
    }
}