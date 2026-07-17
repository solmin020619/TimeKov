using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hovl
{
    public class HS_ObjectActivator : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] List<Renderer> targetRenderers = new List<Renderer>();
        [SerializeField] Collider targetCollider;
        [SerializeField] List<ParticleSystem> targetParticleSystems = new List<ParticleSystem>();
        [SerializeField] List<GameObject> objectsToToggle = new List<GameObject>();

        [Header("Shader")]
        [SerializeField] string shaderProperty = "_Shield_step";

        [Header("Animation")]
        [SerializeField] float transitionSpeed = 2f;
        [SerializeField, Range(0f, 1f)] float colliderDisableValue = 0.8f;

        [Header("Objects Toggle")]
        [SerializeField] float objectsActivateDelay = 0f;

        MaterialPropertyBlock propertyBlock;
        int propertyID;

        float currentValue;
        float targetValue;
        bool isOn;
        bool particlesPlaying;
        bool objectsActive;
        Coroutine objectsActivateCoroutine;

        void Awake()
        {
            if (targetRenderers.Count == 0)
            {
                Renderer rendererOnThisObject = GetComponent<Renderer>();
                if (rendererOnThisObject != null)
                    targetRenderers.Add(rendererOnThisObject);
            }

            if (targetCollider == null)
                targetCollider = GetComponent<Collider>();

            RemoveNullRenderers();
            RemoveNullParticles();
            RemoveNullObjects();

            if (targetRenderers.Count == 0)
            {
                enabled = false;
                return;
            }

            propertyID = Shader.PropertyToID(shaderProperty);
            propertyBlock = new MaterialPropertyBlock();

            currentValue = 0f;
            targetValue = 0f;
            isOn = false;
            particlesPlaying = false;
            objectsActive = false;

            ApplyValue(currentValue);
            UpdateColliderState(true);
            UpdateParticlesState(true);
            UpdateObjectsState(true);
        }

        void Update()
        {
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
            {
                isOn = !isOn;
                targetValue = isOn ? 1f : 0f;

                if (isOn && targetCollider != null)
                    targetCollider.enabled = true;

                if (!isOn)
                {
                    StopParticles();
                    CancelObjectsActivation();
                    SetObjectsActive(false);
                }
            }

            if (Mathf.Approximately(currentValue, targetValue))
                return;

            currentValue = Mathf.MoveTowards(currentValue, targetValue, transitionSpeed * Time.deltaTime);

            ApplyValue(currentValue);
            UpdateColliderState(false);
            UpdateParticlesState(false);
            UpdateObjectsState(false);
        }

        void ApplyValue(float value)
        {
            for (int i = 0; i < targetRenderers.Count; i++)
            {
                Renderer currentRenderer = targetRenderers[i];
                if (currentRenderer == null)
                    continue;

                currentRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(propertyID, value);
                currentRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        void UpdateColliderState(bool forceUpdate)
        {
            if (targetCollider == null)
                return;

            if (isOn)
            {
                if (forceUpdate || currentValue > 0f)
                    targetCollider.enabled = true;
            }
            else
            {
                if (forceUpdate || currentValue <= colliderDisableValue)
                    targetCollider.enabled = false;
            }
        }

        void UpdateParticlesState(bool forceUpdate)
        {
            if (targetParticleSystems.Count == 0)
                return;

            if (isOn)
            {
                if ((forceUpdate || Mathf.Approximately(currentValue, 1f)) && !particlesPlaying)
                    PlayParticles();
            }
            else
            {
                if ((forceUpdate || currentValue < 1f) && particlesPlaying)
                    StopParticles();
            }
        }

        void UpdateObjectsState(bool forceUpdate)
        {
            if (objectsToToggle.Count == 0)
                return;

            if (isOn)
            {
                if ((forceUpdate || Mathf.Approximately(currentValue, 1f)) && !objectsActive && objectsActivateCoroutine == null)
                {
                    if (objectsActivateDelay <= 0f)
                        SetObjectsActive(true);
                    else
                        objectsActivateCoroutine = StartCoroutine(ActivateObjectsWithDelay());
                }
            }
            else
            {
                CancelObjectsActivation();

                if (objectsActive)
                    SetObjectsActive(false);
            }
        }

        IEnumerator ActivateObjectsWithDelay()
        {
            yield return new WaitForSeconds(objectsActivateDelay);

            objectsActivateCoroutine = null;

            if (!isOn)
                yield break;

            if (!Mathf.Approximately(currentValue, 1f))
                yield break;

            SetObjectsActive(true);
        }

        void CancelObjectsActivation()
        {
            if (objectsActivateCoroutine == null)
                return;

            StopCoroutine(objectsActivateCoroutine);
            objectsActivateCoroutine = null;
        }

        void PlayParticles()
        {
            for (int i = 0; i < targetParticleSystems.Count; i++)
            {
                ParticleSystem ps = targetParticleSystems[i];
                if (ps == null)
                    continue;

                ps.Play();
            }

            particlesPlaying = true;
        }

        void StopParticles()
        {
            for (int i = 0; i < targetParticleSystems.Count; i++)
            {
                ParticleSystem ps = targetParticleSystems[i];
                if (ps == null)
                    continue;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            particlesPlaying = false;
        }

        void SetObjectsActive(bool state)
        {
            for (int i = 0; i < objectsToToggle.Count; i++)
            {
                GameObject obj = objectsToToggle[i];
                if (obj == null)
                    continue;

                obj.SetActive(state);
            }

            objectsActive = state;
        }

        void RemoveNullRenderers()
        {
            for (int i = targetRenderers.Count - 1; i >= 0; i--)
            {
                if (targetRenderers[i] == null)
                    targetRenderers.RemoveAt(i);
            }
        }

        void RemoveNullParticles()
        {
            for (int i = targetParticleSystems.Count - 1; i >= 0; i--)
            {
                if (targetParticleSystems[i] == null)
                    targetParticleSystems.RemoveAt(i);
            }
        }

        void RemoveNullObjects()
        {
            for (int i = objectsToToggle.Count - 1; i >= 0; i--)
            {
                if (objectsToToggle[i] == null)
                    objectsToToggle.RemoveAt(i);
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (transitionSpeed < 0f)
                transitionSpeed = 0f;

            if (objectsActivateDelay < 0f)
                objectsActivateDelay = 0f;

            colliderDisableValue = Mathf.Clamp01(colliderDisableValue);
        }
#endif
    }
}