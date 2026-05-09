using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if VFX_OUTPUTEVENT_HDRP_10_0_0_OR_NEWER
using UnityEngine.Rendering.HighDefinition;
#endif

namespace UnityEngine.VFX.Utility
{
    [RequireComponent(typeof(Light))]
#if VFX_OUTPUTEVENT_HDRP_10_0_0_OR_NEWER
    [RequireComponent(typeof(HDAdditionalLightData))]
#endif
    class VFXOutputEventPrefabAttributeLightHandler : VFXOutputEventPrefabAttributeAbstractHandler
    {
        public float brightnessScale = 1.0f;
        static readonly int k_Color = Shader.PropertyToID("color");

        public override void OnVFXEventAttribute(VFXEventAttribute eventAttribute, VisualEffect visualEffect)
        {
            var color = eventAttribute.GetVector3(k_Color);
            var intensity = color.magnitude;
            var c = new Color(color.x, color.y, color.z) / intensity;
            intensity *= brightnessScale;

#if VFX_OUTPUTEVENT_HDRP_10_0_0_OR_NEWER
            var hdlight = GetComponent<HDAdditionalLightData>();
            hdlight.SetColor(c);
#pragma warning disable CS0618 // Unity 샘플 — SetIntensity deprecated이지만 단위 변환 로직 따로 작업하기 부담, 경고만 억제
            hdlight.SetIntensity(intensity);
#pragma warning restore CS0618
#else
            var light = GetComponent<Light>();
            light.color = c;
            light.intensity = intensity;
#endif
        }
    }
}
