using UnityEngine;

// VFX 색을 갈아끼우는 공용 유틸.
// 같은 팩 이펙트를 몹마다 다른 색으로 재활용할 때 쓴다.
//
// ★여기 모아둔 이유: 색칠 지점이 여러 곳으로 흩어지면 한 곳을 빠뜨린다.
//   실제로 발사체만 물들이고 착탄 VFX 를 빠뜨려서, 맞는 순간에만 원래 색(보라)이 튀어나왔다.
public static class VfxTint
{
    // 오브젝트 하위 전체를 물들인다. 파티클 색과 재질 색 둘 다 건드려야 완전히 바뀐다.
    public static void Apply(GameObject go, Color c)
    {
        if (go == null) return;
        Color pc = ToParticleColor(c);

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(pc);
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        // 파티클 색만 바꾸면 일부가 원래 색으로 남는다.
        // 셰이더그래프가 자체 HDR 색 프로퍼티를 들고 있고 그게 최종 색을 지배하기 때문이다.
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.materials)
                TintMaterial(m, pc);
    }

    // ★파티클 색은 8비트 정점 스트림이라 1을 넘으면 잘린다.
    //   (8, 1.1, 0.1) 같은 HDR 값을 그대로 넣으면 (1, 1, 0.1) = 노랑이 돼버린다.
    //   최대 성분으로 나눠 색조를 보존한 채 0~1 로 내린다.
    public static Color ToParticleColor(Color c)
    {
        float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (m > 1f) return new Color(c.r / m, c.g / m, c.b / m, c.a);
        return c;
    }

    // 재질의 "보이는 색"만 색조를 바꾼다. 세기(HDR 배율)와 알파는 유지한다.
    //
    // ★Color 타입이라고 다 색이 아니다. 셰이더그래프는 마스크 채널 선택자나 UV 속도도
    //   Color 로 선언한다(_DissolveMaskChannel = (0,1,0,0) 은 "G 채널을 쓴다"는 뜻).
    //   그걸 덮어쓰면 디졸브가 깨져 안 보이거나 색이 이상해진다. 이름으로 걸러낸다.
    static void TintMaterial(Material m, Color hue)
    {
        if (m == null || m.shader == null) return;

        var sh = m.shader;
        int n = sh.GetPropertyCount();
        for (int i = 0; i < n; i++)
        {
            if (sh.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;

            string low = sh.GetPropertyName(i).ToLowerInvariant();
            if (low.Contains("mask") || low.Contains("channel") || low.Contains("speed")
             || low.Contains("offset") || low.Contains("tiling") || low.Contains("param")) continue;
            if (!(low.Contains("color") || low.Contains("tint") || low.Contains("emiss"))) continue;

            int id = sh.GetPropertyNameId(i);
            Color c = m.GetColor(id);

            // 검게 꺼둔 프로퍼티는 건드리지 않는다(켜버리면 없던 발광이 생긴다)
            float intensity = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (intensity <= 0.001f) continue;

            m.SetColor(id, new Color(hue.r * intensity, hue.g * intensity, hue.b * intensity, c.a));
        }
    }
}
