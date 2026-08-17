// =====================================================================
// TimeUiSprites.cs
// '시간' 계열 UI(제작진 화면, 시간선 복원 퍼즐)가 쓰는 절차적 스프라이트.
// 원·링·방사형 그라디언트·점선처럼 에셋으로 두기엔 자잘하고, 코드로 굽는 게 확실한 것들.
//
// [왜 코드로 굽나]
//   지름/두께가 화면마다 달라서 PNG 로 두면 종류가 계속 늘어난다. 그리고 이건 '글자'가
//   아니라 장식이라 런타임 생성이 규칙에 걸리지 않는다(글자는 반드시 씬에 실물로).
//
// [캐시]
//   같은 규격은 한 번만 만든다. ★파괴 여부를 반드시 확인한다 —
//   에디터에서 Domain Reload 를 끄면 static 이 살아남아 파괴된 텍스처를 돌려주고,
//   그러면 두 번째 Play 부터 그림이 안 보인다(제작진 화면에서 실제로 겪은 함정).
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public static class TimeUiSprites
{
    static readonly Dictionary<string, Sprite> _cache = new();

    static Sprite Get(string key, System.Func<Sprite> make)
    {
        if (_cache.TryGetValue(key, out var s) && s != null) return s;   // ★파괴 검사
        s = make();
        _cache[key] = s;
        return s;
    }

    /// <summary>가운데→가장자리 3단 방사형 그라디언트. ★알파가 아니라 '색'으로 굽는다 —
    /// 이 프로젝트는 Linear 컬러스페이스라 반투명이 의도보다 훨씬 밝게 합성된다.</summary>
    public static Sprite RadialGradient(Color inner, Color mid, Color outer, float midStop = 0.7f)
    {
        string k = $"rg:{inner}{mid}{outer}{midStop}";
        return Get(k, () =>
        {
            const int RES = 128;
            var tex = New(RES);
            var px = new Color32[RES * RES];
            float c = RES * 0.5f;
            for (int y = 0; y < RES; y++)
                for (int x = 0; x < RES; x++)
                {
                    float d = Mathf.Sqrt((x + .5f - c) * (x + .5f - c) + (y + .5f - c) * (y + .5f - c)) / c;
                    d = Mathf.Clamp01(d);
                    px[y * RES + x] = d <= midStop
                        ? Color.Lerp(inner, mid, d / midStop)
                        : Color.Lerp(mid, outer, (d - midStop) / (1f - midStop));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, RES, RES), new Vector2(.5f, .5f));
        });
    }

    /// <summary>지름 size 로 쓸 링. thickness 는 화면 픽셀 기준 두께.</summary>
    public static Sprite Ring(float size, float thickness)
    {
        int res = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.RoundToInt(size)), 64, 512);
        string k = $"ring:{res}:{thickness:0.##}:{size:0.#}";
        return Get(k, () =>
        {
            float outer = res * .5f - 1f;
            float inner = outer - Mathf.Max(1.2f, thickness * res / size);
            var tex = New(res);
            var px = new Color32[res * res];
            float c = res * .5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x + .5f - c) * (x + .5f - c) + (y + .5f - c) * (y + .5f - c));
                    float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                    px[y * res + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(.5f, .5f));
        });
    }

    public static Sprite Disc() => Get("disc", () =>
    {
        const int RES = 128;
        var tex = New(RES);
        var px = new Color32[RES * RES];
        float c = RES * .5f, r = c - 1f;
        for (int y = 0; y < RES; y++)
            for (int x = 0; x < RES; x++)
            {
                float d = Mathf.Sqrt((x + .5f - c) * (x + .5f - c) + (y + .5f - c) * (y + .5f - c));
                px[y * RES + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, RES, RES), new Vector2(.5f, .5f));
    });

    /// <summary>모서리가 둥근 막대. 경로 선분에 쓴다(끝이 둥글어 꺾이는 자리가 메워진다).
    /// 9-slice 로 만들어 길이를 늘여도 둥근 끝 모양이 유지된다.</summary>
    public static Sprite Capsule(float thickness)
    {
        int r = Mathf.Max(2, Mathf.RoundToInt(thickness * .5f));
        return Get($"cap:{r}", () =>
        {
            int size = r * 2 + 4;
            var tex = New(size);
            var px = new Color32[size * size];
            Vector2 c = new Vector2(size * .5f, size * .5f), half = c;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + .5f, y + .5f);
                    Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * r;
                    float sdf = Mathf.Min(Mathf.Max(d.x, d.y), 0f)
                              + new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude - r;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(.5f - sdf) * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f),
                                 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        });
    }

    /// <summary>가로로 반복되는 점선 텍스처. RawImage 의 uvRect 를 굴려 '흐르는' 효과를 낸다
    /// (머티리얼을 복제하지 않아도 컴포넌트마다 독립적으로 움직인다).</summary>
    public static Texture2D DashTexture(int on, int off)
    {
        string k = $"dash:{on}:{off}";
        if (_dash.TryGetValue(k, out var t) && t != null) return t;
        int w = on + off;
        var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        var px = new Color32[w];
        for (int x = 0; x < w; x++) px[x] = new Color32(255, 255, 255, (byte)(x < on ? 255 : 0));
        tex.SetPixels32(px); tex.Apply();
        _dash[k] = tex;
        return tex;
    }
    static readonly Dictionary<string, Texture2D> _dash = new();

    static Texture2D New(int res) => new Texture2D(res, res, TextureFormat.RGBA32, false)
    { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
}
