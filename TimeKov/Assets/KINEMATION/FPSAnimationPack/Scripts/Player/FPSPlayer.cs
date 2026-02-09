// FPSPlayer.cs (ViewModel-only)
// - No CharacterController.Move
// - No player root yaw rotation
// - No Unity InputSystem callbacks (external controller should inject move/look)
// - Keeps weapon/IK/recoil/ADS pipeline intact

using KINEMATION.ProceduralRecoilAnimationSystem.Runtime;
using KINEMATION.FPSAnimationPack.Scripts.Sounds;
using KINEMATION.FPSAnimationPack.Scripts.Weapon;
using KINEMATION.KAnimationCore.Runtime.Core;

using System;
using System.Collections.Generic;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace KINEMATION.FPSAnimationPack.Scripts.Player
{
    [Serializable]
    public struct IKTransforms
    {
        public Transform tip;
        public Transform mid;
        public Transform root;
    }

    [AddComponentMenu("KINEMATION/FPS Animation Pack/Character/FPS Player")]
    public class FPSPlayer : MonoBehaviour
    {
        public float AdsWeight => _adsWeight;

        [Header("Settings")]
        public FPSPlayerSettings playerSettings;

        [Header("Skeleton")]
        [SerializeField] private Transform skeletonRoot;
        [SerializeField] private Transform weaponBone;
        [SerializeField] private Transform weaponBoneAdditive;
        [SerializeField] private Transform cameraPoint;
        [SerializeField] private IKTransforms rightHand;
        [SerializeField] private IKTransforms leftHand;

        // IK
        private KTwoBoneIkData _rightHandIk;
        private KTwoBoneIkData _leftHandIk;

        // Recoil/ADS
        private RecoilAnimation _recoilAnimation;
        private float _adsWeight;

        // Weapons
        private readonly List<FPSWeapon> _weapons = new List<FPSWeapon>();
        private readonly List<FPSWeapon> _prefabComponents = new List<FPSWeapon>();
        private int _activeWeaponIndex;

        // Animator
        private Animator _animator;

        private static readonly int RIGHT_HAND_WEIGHT = Animator.StringToHash("RightHandWeight");
        private static readonly int TAC_SPRINT_WEIGHT = Animator.StringToHash("TacSprintWeight");
        private static readonly int GRENADE_WEIGHT = Animator.StringToHash("GrenadeWeight");
        private static readonly int THROW_GRENADE = Animator.StringToHash("ThrowGrenade");
        private static readonly int GAIT = Animator.StringToHash("Gait");
        private static readonly int IS_IN_AIR = Animator.StringToHash("IsInAir");
        private static readonly int INSPECT = Animator.StringToHash("Inspect");

        private int _tacSprintLayerIndex;
        private int _triggerDisciplineLayerIndex;
        private int _rightHandLayerIndex;

        // Inputs (injected)
        private bool _isAiming;
        private Vector2 _moveInput;   // normalized 0..1
        private float _smoothGait;

        // _lookPitch = pitch degrees for viewmodel alignment
        private float _lookPitch;

        private bool _bSprinting;
        private bool _bTacSprinting;

        private FPSPlayerSound _playerSound;

        // IK Motion
        private float _ikMotionPlayBack;
        private KTransform _ikMotion = KTransform.Identity;
        private KTransform _cachedIkMotion = KTransform.Identity;
        private IKMotion _activeMotion;

        // Cached camera-point local transform
        private KTransform _localCameraPoint;

        // -------------------------
        // Public API (Inject Inputs)
        // -------------------------

        /// <summary>
        /// move01: -1..1 (x=strafe, y=forward). Will be clamped to magnitude 1.
        /// sprint/tacSprint: movement state (drives gait 2/3).
        /// </summary>
        public void SetMoveInput(Vector2 move01, bool sprint, bool tacSprint = false)
        {
            _moveInput = Vector2.ClampMagnitude(move01, 1f);
            _bSprinting = sprint;
            _bTacSprinting = tacSprint && sprint;
        }

        /// <summary>
        /// pitchDelta: positive when mouse moves up (typical Input.GetAxis("Mouse Y")).
        /// This script only uses pitch for viewmodel alignment.
        /// </summary>
        public void AddLookPitchDelta(float pitchDelta)
        {
            float sens = (playerSettings != null) ? playerSettings.sensitivity : 1f;
            _lookPitch = Mathf.Clamp(_lookPitch - pitchDelta * sens, -90f, 90f);
        }

        /// <summary>
        /// Aiming state (ADS blend).
        /// </summary>
        public void SetAiming(bool aiming)
        {
            bool wasAiming = _isAiming;
            _isAiming = aiming;

            if (_recoilAnimation != null) _recoilAnimation.isAiming = _isAiming;

            if (wasAiming != _isAiming && _playerSound != null)
            {
                _playerSound.PlayAimSound(_isAiming);
                if (playerSettings != null) PlayIkMotion(playerSettings.aimingMotion);
            }
        }

        // -------------------------
        // Weapon API (Optional bridge)
        // -------------------------

        public FPSWeapon GetActiveWeapon()
        {
            if (_weapons.Count == 0) return null;
            _activeWeaponIndex = Mathf.Clamp(_activeWeaponIndex, 0, _weapons.Count - 1);
            return _weapons[_activeWeaponIndex];
        }

        public FPSWeapon GetActivePrefab()
        {
            if (_prefabComponents.Count == 0) return null;
            _activeWeaponIndex = Mathf.Clamp(_activeWeaponIndex, 0, _prefabComponents.Count - 1);
            return _prefabComponents[_activeWeaponIndex];
        }

        private void SetWeaponVisible()
        {
            var w = GetActiveWeapon();
            if (w != null) w.gameObject.SetActive(true);
        }

        private void EquipWeapon_Incremental()
        {
            var w = GetActiveWeapon();
            if (w == null) return;

            w.gameObject.SetActive(false);
            _activeWeaponIndex = _activeWeaponIndex + 1 > _weapons.Count - 1 ? 0 : _activeWeaponIndex + 1;

            GetActiveWeapon().OnEquipped();
            Invoke(nameof(SetWeaponVisible), 0.05f);
        }

        private void EquipWeapon()
        {
            var w = GetActiveWeapon();
            if (w == null) return;

            w.gameObject.SetActive(false);
            w.OnEquipped(true);
            Invoke(nameof(SetWeaponVisible), 0.05f);
        }

        private void ThrowGrenade()
        {
            var w = GetActiveWeapon();
            if (w == null || playerSettings == null) return;

            w.gameObject.SetActive(false);
            Invoke(nameof(EquipWeapon), playerSettings.grenadeDelay);
        }

        private void OnLand()
        {
            if (_animator != null) _animator.SetBool(IS_IN_AIR, false);
        }

        public void OnThrowGrenade()
        {
            var w = GetActiveWeapon();
            if (_animator == null || w == null) return;

            _animator.SetTrigger(THROW_GRENADE);
            Invoke(nameof(ThrowGrenade), w.UnEquipDelay);
        }

        public void OnChangeWeapon()
        {
            if (_weapons.Count <= 1) return;

            float delay = GetActiveWeapon().OnUnEquipped();
            Invoke(nameof(EquipWeapon_Incremental), delay);
        }

        public void OnChangeFireMode()
        {
            var w = GetActiveWeapon();
            if (w == null) return;

            var prevFireMode = w.ActiveFireMode;
            w.OnFireModeChange();

            if (prevFireMode != w.ActiveFireMode)
            {
                _playerSound?.PlayFireModeSwitchSound();
                if (playerSettings != null) PlayIkMotion(playerSettings.fireModeMotion);
            }
        }

        public void OnReload()
        {
            GetActiveWeapon()?.OnReload();
        }

        public void OnJump()
        {
            if (_animator == null) return;
            _animator.SetBool(IS_IN_AIR, true);
            Invoke(nameof(OnLand), 0.4f);
        }

        public void OnInspect()
        {
            if (_animator == null) return;
            _animator.CrossFade(INSPECT, 0.1f);
        }

        // Fire forwarding (optional)
        public void FirePressed() => GetActiveWeapon()?.OnFirePressed();
        public void FireReleased() => GetActiveWeapon()?.OnFireReleased();

        // -------------------------
        // Unity
        // -------------------------

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _recoilAnimation = GetComponent<RecoilAnimation>();
            _playerSound = GetComponent<FPSPlayerSound>();
        }

        private void Start()
        {
            if (_animator == null)
            {
                Debug.LogError("[FPSPlayer] Animator missing on the same GameObject.");
                enabled = false;
                return;
            }

            _triggerDisciplineLayerIndex = _animator.GetLayerIndex("TriggerDiscipline");
            _rightHandLayerIndex = _animator.GetLayerIndex("RightHand");
            _tacSprintLayerIndex = _animator.GetLayerIndex("TacSprint");

            if (playerSettings == null)
            {
                Debug.LogError("[FPSPlayer] PlayerSettings is not assigned.");
                enabled = false;
                return;
            }

            KTransform root = new KTransform(transform);
            _localCameraPoint = root.GetRelativeTransform(new KTransform(cameraPoint), false);

            // Instantiate weapons under weaponBone
            _weapons.Clear();
            _prefabComponents.Clear();

            foreach (var prefab in playerSettings.weaponPrefabs)
            {
                if (prefab == null) continue;

                var prefabComponent = prefab.GetComponent<FPSWeapon>();
                if (prefabComponent == null) continue;

                _prefabComponents.Add(prefabComponent);

                var instance = Instantiate(prefab, weaponBone, false);
                instance.SetActive(false);

                var component = instance.GetComponent<FPSWeapon>();
                component.Initialize(gameObject);

                // Cache poses
                KTransform weaponT = new KTransform(weaponBone);
                component.rightHandPose = new KTransform(rightHand.tip).GetRelativeTransform(weaponT, false);

                var localWeapon = root.GetRelativeTransform(weaponT, false);
                localWeapon.rotation *= prefabComponent.weaponSettings.rotationOffset;

                component.adsPose.position = _localCameraPoint.position - localWeapon.position;
                component.adsPose.rotation = Quaternion.Inverse(localWeapon.rotation);

                _weapons.Add(component);
            }

            if (_weapons.Count == 0)
            {
                Debug.LogError("[FPSPlayer] No weapon prefabs were initialized.");
                enabled = false;
                return;
            }

            _activeWeaponIndex = Mathf.Clamp(_activeWeaponIndex, 0, _weapons.Count - 1);
            GetActiveWeapon().gameObject.SetActive(true);
            GetActiveWeapon().OnEquipped();
        }

        private float GetDesiredGait()
        {
            if (_bTacSprinting) return 3f;
            if (_bSprinting) return 2f;
            return _moveInput.magnitude; // 0..1
        }

        private void Update()
        {
            // ADS weight
            _adsWeight = Mathf.Clamp01(
                _adsWeight + playerSettings.aimSpeed * Time.deltaTime * (_isAiming ? 1f : -1f)
            );

            // Gait smoothing
            _smoothGait = Mathf.Lerp(
                _smoothGait,
                GetDesiredGait(),
                KMath.ExpDecayAlpha(playerSettings.gaitSmoothing, Time.deltaTime)
            );

            _animator.SetFloat(GAIT, _smoothGait);
            _animator.SetLayerWeight(_tacSprintLayerIndex, Mathf.Clamp01(_smoothGait - 2f));

            var w = GetActiveWeapon();
            if (w == null) return;

            bool triggerAllowed = w.weaponSettings.useSprintTriggerDiscipline;

            _animator.SetLayerWeight(
                _triggerDisciplineLayerIndex,
                triggerAllowed ? _animator.GetFloat(TAC_SPRINT_WEIGHT) : 0f
            );

            _animator.SetLayerWeight(_rightHandLayerIndex, _animator.GetFloat(RIGHT_HAND_WEIGHT));

            // ViewModel camera alignment ONLY (no player movement/rotation)
            Vector3 cameraPosition = -_localCameraPoint.position;

            transform.localRotation = Quaternion.Euler(_lookPitch, 0f, 0f);
            transform.localPosition = transform.localRotation * cameraPosition - cameraPosition;
        }

        // -------------------------
        // IK Pipeline (Original)
        // -------------------------

        private void SetupIkData(ref KTwoBoneIkData ikData, in KTransform target, in IKTransforms transforms, float weight = 1f)
        {
            ikData.target = target;

            ikData.tip = new KTransform(transforms.tip);
            ikData.mid = ikData.hint = new KTransform(transforms.mid);
            ikData.root = new KTransform(transforms.root);

            ikData.hintWeight = weight;
            ikData.posWeight = weight;
            ikData.rotWeight = weight;
        }

        private void ApplyIkData(in KTwoBoneIkData ikData, in IKTransforms transforms)
        {
            transforms.root.rotation = ikData.root.rotation;
            transforms.mid.rotation = ikData.mid.rotation;
            transforms.tip.rotation = ikData.tip.rotation;
        }

        private void ProcessOffsets(ref KTransform weaponT)
        {
            var root = transform;
            KTransform rootT = new KTransform(root);
            var weaponOffset = GetActiveWeapon().weaponSettings.ikOffset;

            float mask = 1f - _animator.GetFloat(TAC_SPRINT_WEIGHT);
            weaponT.position = KAnimationMath.MoveInSpace(rootT, weaponT, weaponOffset, mask);

            var settings = GetActiveWeapon().weaponSettings;
            KAnimationMath.MoveInSpace(root, rightHand.root, settings.rightClavicleOffset, mask);
            KAnimationMath.MoveInSpace(root, leftHand.root, settings.leftClavicleOffset, mask);
        }

        private void ProcessAdditives(ref KTransform weaponT)
        {
            KTransform rootT = new KTransform(skeletonRoot);
            KTransform additive = rootT.GetRelativeTransform(new KTransform(weaponBoneAdditive), false);

            float weight = Mathf.Lerp(1f, 0.3f, _adsWeight) * (1f - _animator.GetFloat(GRENADE_WEIGHT));

            weaponT.position = KAnimationMath.MoveInSpace(rootT, weaponT, additive.position, weight);
            weaponT.rotation = KAnimationMath.RotateInSpace(rootT, weaponT, additive.rotation, weight);
        }

        private void ProcessRecoil(ref KTransform weaponT)
        {
            KTransform recoil = new KTransform()
            {
                rotation = _recoilAnimation.OutRot,
                position = _recoilAnimation.OutLoc,
            };

            KTransform root = new KTransform(transform);
            weaponT.position = KAnimationMath.MoveInSpace(root, weaponT, recoil.position, 1f);
            weaponT.rotation = KAnimationMath.RotateInSpace(root, weaponT, recoil.rotation, 1f);
        }

        private void ProcessAds(ref KTransform weaponT)
        {
            var weaponOffset = GetActiveWeapon().weaponSettings.ikOffset;
            var adsPose = weaponT;

            KTransform aimPoint = KTransform.Identity;

            aimPoint.position = -weaponBone.InverseTransformPoint(GetActiveWeapon().aimPoint.position);
            aimPoint.position -= GetActiveWeapon().weaponSettings.aimPointOffset;
            aimPoint.rotation = Quaternion.Inverse(weaponBone.rotation) * GetActiveWeapon().aimPoint.rotation;

            KTransform root = new KTransform(transform);
            adsPose.position = KAnimationMath.MoveInSpace(root, adsPose, GetActiveWeapon().adsPose.position - weaponOffset, 1f);
            adsPose.rotation = KAnimationMath.RotateInSpace(root, adsPose, GetActiveWeapon().adsPose.rotation, 1f);

            KTransform cameraPose = root.GetWorldTransform(_localCameraPoint, false);

            float adsBlendWeight = GetActiveWeapon().weaponSettings.adsBlend;
            adsPose.position = Vector3.Lerp(cameraPose.position, adsPose.position, adsBlendWeight);
            adsPose.rotation = Quaternion.Slerp(cameraPose.rotation, adsPose.rotation, adsBlendWeight);

            adsPose.position = KAnimationMath.MoveInSpace(root, adsPose, aimPoint.rotation * aimPoint.position, 1f);
            adsPose.rotation = KAnimationMath.RotateInSpace(root, adsPose, aimPoint.rotation, 1f);

            float weight = KCurves.EaseSine(0f, 1f, _adsWeight);

            weaponT.position = Vector3.Lerp(weaponT.position, adsPose.position, weight);
            weaponT.rotation = Quaternion.Slerp(weaponT.rotation, adsPose.rotation, weight);
        }

        private KTransform GetWeaponPose()
        {
            KTransform defaultWorldPose =
                new KTransform(rightHand.tip).GetWorldTransform(GetActiveWeapon().rightHandPose, false);

            float weight = _animator.GetFloat(RIGHT_HAND_WEIGHT);
            return KTransform.Lerp(new KTransform(weaponBone), defaultWorldPose, weight);
        }

        private void PlayIkMotion(IKMotion newMotion)
        {
            _ikMotionPlayBack = 0f;
            _cachedIkMotion = _ikMotion;
            _activeMotion = newMotion;
        }

        private void ProcessIkMotion(ref KTransform weaponT)
        {
            if (_activeMotion == null) return;

            _ikMotionPlayBack = Mathf.Clamp(
                _ikMotionPlayBack + _activeMotion.playRate * Time.deltaTime,
                0f,
                _activeMotion.GetLength()
            );

            Vector3 positionTarget = _activeMotion.translationCurves.GetValue(_ikMotionPlayBack);
            positionTarget.x *= _activeMotion.translationScale.x;
            positionTarget.y *= _activeMotion.translationScale.y;
            positionTarget.z *= _activeMotion.translationScale.z;

            Vector3 rotationTarget = _activeMotion.rotationCurves.GetValue(_ikMotionPlayBack);
            rotationTarget.x *= _activeMotion.rotationScale.x;
            rotationTarget.y *= _activeMotion.rotationScale.y;
            rotationTarget.z *= _activeMotion.rotationScale.z;

            _ikMotion.position = positionTarget;
            _ikMotion.rotation = Quaternion.Euler(rotationTarget);

            if (!Mathf.Approximately(_activeMotion.blendTime, 0f))
            {
                _ikMotion = KTransform.Lerp(_cachedIkMotion, _ikMotion, _ikMotionPlayBack / _activeMotion.blendTime);
            }

            var root = new KTransform(transform);
            weaponT.position = KAnimationMath.MoveInSpace(root, weaponT, _ikMotion.position, 1f);
            weaponT.rotation = KAnimationMath.RotateInSpace(root, weaponT, _ikMotion.rotation, 1f);
        }

        private void LateUpdate()
        {
            var w = GetActiveWeapon();
            if (w == null) return;

            KAnimationMath.RotateInSpace(
                transform,
                rightHand.tip,
                w.weaponSettings.rightHandSprintOffset,
                _animator.GetFloat(TAC_SPRINT_WEIGHT)
            );

            KTransform weaponTransform = GetWeaponPose();

            weaponTransform.rotation = KAnimationMath.RotateInSpace(
                weaponTransform,
                weaponTransform,
                w.weaponSettings.rotationOffset,
                1f
            );

            KTransform rightHandTarget = weaponTransform.GetRelativeTransform(new KTransform(rightHand.tip), false);
            KTransform leftHandTarget = weaponTransform.GetRelativeTransform(new KTransform(leftHand.tip), false);

            ProcessOffsets(ref weaponTransform);
            ProcessAds(ref weaponTransform);
            ProcessAdditives(ref weaponTransform);
            ProcessIkMotion(ref weaponTransform);
            ProcessRecoil(ref weaponTransform);

            weaponBone.position = weaponTransform.position;
            weaponBone.rotation = weaponTransform.rotation;

            rightHandTarget = weaponTransform.GetWorldTransform(rightHandTarget, false);
            leftHandTarget = weaponTransform.GetWorldTransform(leftHandTarget, false);

            SetupIkData(ref _rightHandIk, rightHandTarget, rightHand, playerSettings.ikWeight);
            SetupIkData(ref _leftHandIk, leftHandTarget, leftHand, playerSettings.ikWeight);

            KTwoBoneIK.Solve(ref _rightHandIk);
            KTwoBoneIK.Solve(ref _leftHandIk);

            ApplyIkData(_rightHandIk, rightHand);
            ApplyIkData(_leftHandIk, leftHand);
        }

        // Called by animation event
        private void OnFire()
        {
            _recoilAnimation?.Play();
        }
        public void SetActiveWeaponIndex(int index)
        {
            if (_weapons == null || _weapons.Count == 0) return;

            var cur = GetActiveWeapon();
            if (cur != null) cur.gameObject.SetActive(false);

            _activeWeaponIndex = Mathf.Clamp(index, 0, _weapons.Count - 1);

            var next = GetActiveWeapon();
            if (next != null)
            {
                next.gameObject.SetActive(true);
                next.OnEquipped(true);
            }
        }

        public Transform GetActiveAimPoint()
        {
            var w = GetActiveWeapon();
            if (w == null) return null;
            return w.aimPoint != null ? w.aimPoint : w.transform;
        }

    }
}
