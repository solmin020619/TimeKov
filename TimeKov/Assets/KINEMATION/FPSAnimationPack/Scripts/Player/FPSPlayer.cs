using KINEMATION.ProceduralRecoilAnimationSystem.Runtime;
using KINEMATION.KAnimationCore.Runtime.Core;

using UnityEngine;

namespace KINEMATION.FPSAnimationPack.Scripts.Player
{
    [AddComponentMenu("KINEMATION/FPS Animation Pack/Character/FPS Player (TimeKov View Only)")]
    public class FPSPlayer : MonoBehaviour
    {
        [Header("Settings")]
        public FPSPlayerSettings playerSettings;

        [Header("Bones")]
        [SerializeField] private Transform weaponBone;
        [SerializeField] private Transform cameraPoint;

        private RecoilAnimation _recoilAnimation;
        private Animator _animator;

        private KTransform _localCameraPoint;

        // === 외부(TimeKov)에서 호출 ===
        public void PlayFire()
        {
            _recoilAnimation?.Play();
        }

        public void SetAiming(bool aiming)
        {
            if (_recoilAnimation != null)
                _recoilAnimation.isAiming = aiming;
        }
        // ==============================

        private void Start()
        {
            _animator = GetComponent<Animator>();
            _recoilAnimation = GetComponent<RecoilAnimation>();

            if (cameraPoint == null || weaponBone == null)
            {
                Debug.LogError("[FPSPlayer] CameraPoint or WeaponBone not assigned.");
                enabled = false;
                return;
            }

            KTransform root = new KTransform(transform);
            _localCameraPoint = root.GetRelativeTransform(new KTransform(cameraPoint), false);
        }

        private void LateUpdate()
        {
            if (weaponBone == null) return;

            KTransform weaponT = new KTransform(weaponBone);
            KTransform root = new KTransform(transform);
            KTransform cameraPose = root.GetWorldTransform(_localCameraPoint, false);

            // ADS 보정
            float adsBlend = playerSettings != null ? playerSettings.adsBlend : 1f;

            weaponT.position = Vector3.Lerp(
                weaponT.position,
                cameraPose.position,
                adsBlend
            );

            weaponT.rotation = Quaternion.Slerp(
                weaponT.rotation,
                cameraPose.rotation,
                adsBlend
            );

            // 반동
            if (_recoilAnimation != null)
            {
                KTransform recoil = new KTransform
                {
                    position = _recoilAnimation.OutLoc,
                    rotation = _recoilAnimation.OutRot
                };

                weaponT.position = KAnimationMath.MoveInSpace(root, weaponT, recoil.position, 1f);
                weaponT.rotation = KAnimationMath.RotateInSpace(root, weaponT, recoil.rotation, 1f);
            }

            weaponBone.position = weaponT.position;
            weaponBone.rotation = weaponT.rotation;
        }
    }
}
