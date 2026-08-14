using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ── 모델 디졸브 (사라짐 / 나타남) ─────────────────────────────────────────────
// 플레이어 사망 연출(PlayerDeathFade)이 쓰는 것과 '같은' 디졸브를 아무 오브젝트에나 쓴다.
//   원리: 렌더러 머티리얼 인스턴스의 셰이더를 TIMEKOV/PlayerDissolve 로 갈아끼우고
//         _DissolveAmount 를 움직인다. 위(_MaxY)에서 아래(_MinY)로 녹고, 반대로 돌리면 차오른다.
//
// ★셰이더 교체 순간을 '보여주지 않는' 것이 중요하다.
//   디졸브 셰이더가 아무리 URP/Lit 을 따라가도 그림자 패스·GI 경로까지 완전히 같을 수는 없어서,
//   보이는 상태에서 갈아끼우면 재질이 달라 보인다.
//   그래서 준비(Prepare) / 진행(Animate) / 복구(Restore)를 나눠, 교체와 복구는 화면이 검을 때
//   호출할 수 있게 했다. 한 번에 처리하고 싶으면 Run 을 쓰면 된다.
public static class ModelDissolve
{
    private static readonly int DissolveId    = Shader.PropertyToID("_DissolveAmount");
    private static readonly int MinYId        = Shader.PropertyToID("_MinY");
    private static readonly int MaxYId        = Shader.PropertyToID("_MaxY");
    private static readonly int NoiseScaleId  = Shader.PropertyToID("_NoiseScale");
    private static readonly int NoiseAmountId = Shader.PropertyToID("_NoiseAmount");
    private static readonly int EdgeWidthId   = Shader.PropertyToID("_EdgeWidth");
    private static readonly int EdgeColorId   = Shader.PropertyToID("_EdgeColor");
    private static readonly int EdgeIntId     = Shader.PropertyToID("_EdgeIntensity");
    private static readonly int RimIntId      = Shader.PropertyToID("_RimIntensity");
    private static readonly int FresnelId     = Shader.PropertyToID("_FresnelPower");

    [System.Serializable]
    public class Style
    {
        [Tooltip("사라지는 경계 발광 색(HDR). 블룸이 켜져 있으면 빛난다.")]
        [ColorUsage(true, true)] public Color edgeColor = Color.white;
        [Range(0.001f, 0.4f)] public float edgeWidth = 0.1f;
        public float edgeIntensity = 3f;
        [Tooltip("실루엣 외곽이 빛나는 정도(프레넬 림).")]
        public float rimIntensity = 2f;
        public float fresnelPower = 3f;
        [Tooltip("노이즈 빈도(클수록 잘게 얼룩짐).")]
        public float noiseScale = 8f;
        [Tooltip("0=위에서 아래로 깔끔한 그라디언트, 1=완전 노이즈.")]
        [Range(0f, 1f)] public float noiseAmount = 0.5f;
    }

    // 진행 중인 대상별 상태. 복구 때 원본 머티리얼을 되돌리려고 들고 있는다.
    private class Session
    {
        public Renderer[] renderers;
        public Material[][] original;
        public List<Material> mats = new List<Material>();
    }

    private static readonly Dictionary<GameObject, Session> _active = new Dictionary<GameObject, Session>();

    /// 디졸브 셰이더로 갈아끼우고 시작값을 적용한다. (교체가 보이면 안 되므로 화면이 검을 때 호출)
    ///   startAmount : 0 = 온전한 모습,  1.05 = 완전히 사라진 상태
    public static bool Prepare(GameObject target, float startAmount, Style style)
    {
        if (target == null || _active.ContainsKey(target)) return false;

        var shader = Shader.Find("TIMEKOV/PlayerDissolve");
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        if (shader == null || renderers.Length == 0) return false;

        style ??= new Style();

        var s = new Session { renderers = renderers, original = new Material[renderers.Length][] };
        GetWorldYBounds(renderers, target.transform, out float minY, out float maxY);

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            s.original[i] = r.sharedMaterials;
            r.enabled = true;

            foreach (var m in r.materials)   // 인스턴스 복제본
            {
                if (m == null) continue;
                m.shader = shader;
                m.SetFloat(MinYId, minY);
                m.SetFloat(MaxYId, maxY);
                m.SetFloat(NoiseScaleId, style.noiseScale);
                m.SetFloat(NoiseAmountId, style.noiseAmount);
                m.SetFloat(EdgeWidthId, style.edgeWidth);
                m.SetColor(EdgeColorId, style.edgeColor);
                m.SetFloat(EdgeIntId, style.edgeIntensity);
                m.SetFloat(RimIntId, style.rimIntensity);
                m.SetFloat(FresnelId, style.fresnelPower);
                m.SetFloat(DissolveId, startAmount);
                s.mats.Add(m);
            }
        }

        _active[target] = s;
        return true;
    }

    /// 준비된 대상의 디졸브 진행도를 from → to 로 움직인다.
    public static IEnumerator Animate(GameObject target, float from, float to, float duration)
    {
        if (target == null || !_active.TryGetValue(target, out var s)) yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;   // 연출 중 timeScale 과 무관하게 진행
            float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            for (int i = 0; i < s.mats.Count; i++) if (s.mats[i] != null) s.mats[i].SetFloat(DissolveId, a);
            yield return null;
        }
        for (int i = 0; i < s.mats.Count; i++) if (s.mats[i] != null) s.mats[i].SetFloat(DissolveId, to);
    }

    /// 원본 머티리얼로 되돌린다. (복구가 보이면 안 되므로 화면이 검을 때 호출)
    ///   disableRenderers : 사라진 상태로 끝낼 때 true — 렌더러를 꺼 둔다.
    public static void Restore(GameObject target, bool disableRenderers = false)
    {
        if (target == null || !_active.TryGetValue(target, out var s)) return;
        _active.Remove(target);

        for (int i = 0; i < s.renderers.Length; i++)
        {
            var r = s.renderers[i];
            if (r == null) continue;
            if (s.original[i] != null) r.sharedMaterials = s.original[i];
            if (disableRenderers) r.enabled = false;
        }
        foreach (var m in s.mats) if (m != null) Object.Destroy(m);
    }

    /// 준비 → 진행 → 복구를 한 번에. (교체·복구가 보여도 되는 경우용)
    ///   appear=false → 사라짐(0→1),  appear=true → 나타남(1→0)
    public static IEnumerator Run(GameObject target, bool appear, float duration, Style style)
    {
        float start = appear ? 1.05f : 0f;
        float end   = appear ? 0f    : 1.05f;

        if (!Prepare(target, start, style))
        {
            // 셰이더나 렌더러가 없으면 상태만 맞추고 끝낸다(연출만 빠지고 흐름은 유지).
            if (!appear && target != null)
                foreach (var r in target.GetComponentsInChildren<Renderer>(true)) if (r) r.enabled = false;
            yield break;
        }

        yield return Animate(target, start, end, duration);
        Restore(target, !appear);
    }

    private static void GetWorldYBounds(Renderer[] renderers, Transform fallback, out float minY, out float maxY)
    {
        bool any = false;
        Bounds b = default;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any) { minY = fallback.position.y; maxY = minY + 2f; return; }
        minY = b.min.y;
        maxY = b.max.y;
    }
}
