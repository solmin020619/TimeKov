using System.Collections.Generic;
using UnityEngine;

namespace KINEMATION.FPSAnimationPack.Scripts.Player
{
    [CreateAssetMenu(
        fileName = "FPSPlayerSettings_TimeKov",
        menuName = "TimeKov/FPS Player Settings"
    )]
    public class FPSPlayerSettings : ScriptableObject
    {
        [Header("Weapon Visuals")]
        public List<GameObject> weaponPrefabs;

        [Header("IK / ADS")]
        [Range(0f, 1f)] public float ikWeight = 1f;
        [Range(0f, 1f)] public float adsBlend = 1f;
    }
}
