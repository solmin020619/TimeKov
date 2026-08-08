// UIRuntime.cs
// ============================================================================
// SettingsUIBuilder 지원 런타임 중 "컴포넌트가 아닌" 것들:
//   - Ease / Easing : 이징 곡선
//   - UISprites     : 런타임 생성 ↔ 구워둔 에셋 전환 지점
//   - Rounded       : 라운드 사각형(9-slice) 스프라이트 생성 캐시
//   - Icons / Ico   : 아이콘 SDF 래스터라이즈 캐시
//
// ⚠ MonoBehaviour(UITween/Btn/Dropdown/SegToggle/Slider3/SliderInput/KeyRow)는
//   각각 클래스명과 같은 파일에 있어야 한다. 유니티는 파일명이 일치해야 스크립트
//   에셋으로 인식하고, 아니면 씬/프리팹 저장 시 참조가 끊겨 Missing Script가 된다.
//   ("does not derive from MonoBehaviour" 에러의 실제 원인)
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace GameSettingsUI
{
    // ==================================================================
    //  Easing
    // ==================================================================
    public enum Ease { Linear, OutQuad, InOutCubic, OutBack, InOutSine }

    public static class Easing
    {
        public static float E(Ease e, float t)
        {
            t = Mathf.Clamp01(t);
            switch (e)
            {
                case Ease.OutQuad:    return 1 - (1 - t) * (1 - t);
                case Ease.InOutCubic: return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2f;
                case Ease.OutBack:    { float c1 = 1.2f, c3 = c1 + 1; float x = t - 1; return 1 + c3 * x * x * x + c1 * x * x; }
                case Ease.InOutSine:  return -(Mathf.Cos(Mathf.PI * t) - 1) / 2f;
                default:              return t;
            }
        }
    }

    // ==================================================================
    //  UISprites : 런타임 생성 ↔ 에셋 전환 지점
    // ==================================================================
    // 코드로 만든 Texture2D/Sprite는 씬에 직렬화할 수 없다(에셋 파일이 아니므로).
    // 에디터 베이크 시 Resolver를 꽂아두면 Rounded/Icons가 구워둔 PNG 에셋을 돌려준다.
    // 게임 실행 중에는 Resolver가 null이라 지금까지와 동일하게 동작한다.
    public static class UISprites
    {
        /// 이름("Rounded_18", "Icon_gear") → 에셋 스프라이트. 못 찾으면 null을 돌려주면 된다.
        public static Func<string, Sprite> Resolver;

        public static Sprite Find(string name) => Resolver?.Invoke(name);
    }

    public static class Rounded
    {
        static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();
        public static string AssetName(int radius) => "Rounded_" + Mathf.Max(0, radius);

        public static Sprite Get(int radius)
        {
            radius = Mathf.Max(0, radius);
            if (cache.TryGetValue(radius, out var s)) return s;
            var asset = UISprites.Find(AssetName(radius));   // 베이크 중이면 에셋 우선
            if (asset) return asset;
            int r = Mathf.Max(1, radius);
            int size = r * 2 + 4;                         // 중앙 2px 여유
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            Vector2 c = new Vector2(size / 2f, size / 2f);
            Vector2 half = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = BoxSDF(p, c, half, r);       // 음수=내부
                    float a = Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            var border = new Vector4(r, r, r, r);
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            cache[radius] = sp;
            return sp;
        }
        static float BoxSDF(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * r;
            return Mathf.Min(Mathf.Max(d.x, d.y), 0f) + new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude - r;
        }

    }

    // ==================================================================
    //  Icons : SDF 래스터라이즈 (24×24 viewBox 기준, y-down)
    // ==================================================================
    public static class Icons
    {
        const int RES = 96; const float VB = 24f;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static string AssetName(string key) => "Icon_" + key;

        static Sprite Make(string key, Action<Ico> draw)
        {
            if (cache.TryGetValue(key, out var s)) return s;
            var asset = UISprites.Find(AssetName(key));   // 베이크 중이면 에셋 우선
            if (asset) return asset;
            var ico = new Ico(RES, VB);
            draw(ico);
            var sp = ico.ToSprite();
            cache[key] = sp; return sp;
        }

        // 베이커가 굽는 목록. 여기 키가 곧 에셋 이름(Icon_<key>)이 되므로,
        // 아이콘을 추가하면 이 배열에도 넣어야 씬 배치 시 스프라이트가 붙는다.
        public static readonly (string key, Func<Sprite> make)[] All =
        {
            ("gear", Gear), ("note", Note), ("mouse", Mouse), ("chev", Chevron),
            ("spk", Speaker), ("mute", Mute), ("back", Back), ("reset", Reset),
            ("check", Check), ("close", Close),
        };

        static readonly Color WHITE = Color.white;
        static readonly Color GRAY = new Color(0.541f, 0.541f, 0.541f);   // #8a8a8a
        static readonly Color RED = new Color(0.878f, 0.353f, 0.302f);    // #e05a4d

        public static Sprite Gear() => Make("gear", i =>
        {
            i.Annulus(new Vector2(12, 12), 6.8f, 3.4f, WHITE);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                i.Seg(new Vector2(12, 12) + d * 6.0f, new Vector2(12, 12) + d * 9.0f, 1.7f, WHITE);
            }
        });

        public static Sprite Note() => Make("note", i =>
        {
            i.Seg(new Vector2(10.9f, 17.5f), new Vector2(10.9f, 6f), 0.9f, WHITE);
            i.Seg(new Vector2(19.9f, 15.5f), new Vector2(19.9f, 4f), 0.9f, WHITE);
            i.Seg(new Vector2(10.9f, 6f), new Vector2(19.9f, 4f), 1.2f, WHITE);
            i.Disc(new Vector2(8.4f, 17.5f), 2.5f, WHITE);
            i.Disc(new Vector2(17.4f, 15.5f), 2.5f, WHITE);
        });

        public static Sprite Mouse() => Make("mouse", i =>
        {
            i.RoundRectStroke(new Vector2(12, 13), new Vector2(6.2f, 9.5f), 6.2f, 1.05f, WHITE);
            i.Seg(new Vector2(5.9f, 12f), new Vector2(18.1f, 12f), 0.9f, WHITE);
            i.RoundRectFill(new Vector2(12, 7.6f), new Vector2(1.6f, 2.4f), 1.6f, WHITE);
        });

        public static Sprite Chevron() => Make("chev", i =>
            i.Poly(new[] { new Vector2(6, 9), new Vector2(12, 15), new Vector2(18, 9) }, 1.3f, WHITE));

        public static Sprite Speaker() => Make("spk", i =>
        {
            i.FillPoly(new[] { new Vector2(4, 9), new Vector2(8, 9), new Vector2(13, 4), new Vector2(13, 20), new Vector2(8, 15), new Vector2(4, 15) }, WHITE);
            i.Arc(new Vector2(11.5f, 12f), 5.5f, -48f, 48f, 1.0f, WHITE);
            i.Arc(new Vector2(11.5f, 12f), 8.5f, -48f, 48f, 1.0f, WHITE);
        });

        public static Sprite Mute() => Make("mute", i =>
        {
            i.FillPoly(new[] { new Vector2(4, 9), new Vector2(8, 9), new Vector2(13, 4), new Vector2(13, 20), new Vector2(8, 15), new Vector2(4, 15) }, GRAY);
            i.Seg(new Vector2(17, 9.5f), new Vector2(22, 14.5f), 1.05f, RED);
            i.Seg(new Vector2(22, 9.5f), new Vector2(17, 14.5f), 1.05f, RED);
        });

        public static Sprite Back() => Make("back", i =>
        {
            i.Poly(new[] { new Vector2(14, 3), new Vector2(19, 3), new Vector2(19, 21), new Vector2(14, 21) }, 1.0f, WHITE);
            i.Seg(new Vector2(3, 12), new Vector2(14, 12), 1.0f, WHITE);
            i.Poly(new[] { new Vector2(9, 8), new Vector2(14, 12), new Vector2(9, 16) }, 1.0f, WHITE);
        });

        public static Sprite Reset() => Make("reset", i =>
        {
            i.Arc(new Vector2(12, 12), 8f, 40f, 320f, 1.05f, WHITE);
            i.Poly(new[] { new Vector2(16.6f, 3.2f), new Vector2(18.9f, 6.9f), new Vector2(14.6f, 7.6f) }, 1.05f, WHITE);
        });

        public static Sprite Check() => Make("check", i =>
            i.Poly(new[] { new Vector2(5, 12), new Vector2(10, 17), new Vector2(20, 6) }, 1.35f, WHITE));

        public static Sprite Close() => Make("close", i =>
        {
            i.Seg(new Vector2(5, 5), new Vector2(19, 19), 1.35f, WHITE);
            i.Seg(new Vector2(19, 5), new Vector2(5, 19), 1.35f, WHITE);
        });
    }

    // 아이콘 래스터라이저 (viewBox 좌표는 y-down; 텍스처는 y-up으로 뒤집어 기록)
    public class Ico
    {
        readonly int N; readonly float scale; readonly float[] r, g, b, a;
        public Ico(int res, float viewBox) { N = res; scale = res / viewBox; r = new float[res * res]; g = new float[res * res]; b = new float[res * res]; a = new float[res * res]; }

        Vector2 UV(int px, int py) => new Vector2((px + 0.5f) / scale, (py + 0.5f) / scale); // py: top-origin

        void Plot(int px, int py, Color c, float cov)
        {
            if (cov <= 0) return; cov = Mathf.Clamp01(cov);
            int idx = py * N + px;
            float na = cov + a[idx] * (1 - cov);
            if (na <= 0.0001f) return;
            r[idx] = (c.r * cov + r[idx] * a[idx] * (1 - cov)) / na;
            g[idx] = (c.g * cov + g[idx] * a[idx] * (1 - cov)) / na;
            b[idx] = (c.b * cov + b[idx] * a[idx] * (1 - cov)) / na;
            a[idx] = na;
        }
        void Each(Func<Vector2, float> sdfInsideNeg, Color c)
        {
            for (int py = 0; py < N; py++)
                for (int px = 0; px < N; px++)
                {
                    float d = sdfInsideNeg(UV(px, py));
                    float cov = Mathf.Clamp01(0.5f - d * scale);
                    if (cov > 0) Plot(px, py, c, cov);
                }
        }

        // ---- 프리미티브 ----
        public void Disc(Vector2 c, float rad, Color col) => Each(p => (p - c).magnitude - rad, col);
        public void Annulus(Vector2 c, float rOut, float rIn, Color col) => Each(p => { float l = (p - c).magnitude; return Mathf.Max(l - rOut, rIn - l); }, col);
        public void Seg(Vector2 A, Vector2 B, float halfW, Color col) => Each(p => SegD(p, A, B) - halfW, col);
        public void RoundRectStroke(Vector2 c, Vector2 half, float rad, float halfW, Color col) => Each(p => Mathf.Abs(Box(p, c, half, rad)) - halfW, col);
        public void RoundRectFill(Vector2 c, Vector2 half, float rad, Color col) => Each(p => Box(p, c, half, rad), col);
        public void Poly(Vector2[] pts, float halfW, Color col) => Each(p => { float d = float.MaxValue; for (int i = 0; i < pts.Length - 1; i++) d = Mathf.Min(d, SegD(p, pts[i], pts[i + 1])); return d - halfW; }, col);
        public void FillPoly(Vector2[] pts, Color col) => Each(p => PolySDF(p, pts), col);
        public void Arc(Vector2 c, float rad, float aFrom, float aTo, float halfW, Color col)
        {
            int seg = 32; var pts = new Vector2[seg + 1];
            for (int i = 0; i <= seg; i++) { float t = Mathf.Lerp(aFrom, aTo, i / (float)seg) * Mathf.Deg2Rad; pts[i] = c + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * rad; }
            Poly(pts, halfW, col);
        }

        static float SegD(Vector2 p, Vector2 a, Vector2 b) { Vector2 pa = p - a, ba = b - a; float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-5f, Vector2.Dot(ba, ba))); return (pa - ba * h).magnitude; }
        static float Box(Vector2 p, Vector2 c, Vector2 half, float rad) { Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * rad; return Mathf.Min(Mathf.Max(d.x, d.y), 0f) + new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude - rad; }
        static float PolySDF(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]); float s = 1; int n = v.Length;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 bb = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Mathf.Max(1e-5f, Vector2.Dot(e, e)));
                d = Mathf.Min(d, Vector2.Dot(bb, bb));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        public Sprite ToSprite()
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[N * N];
            for (int py = 0; py < N; py++)          // py: top-origin
                for (int x = 0; x < N; x++)
                {
                    int src = py * N + x;
                    int dst = (N - 1 - py) * N + x;   // 텍스처 y-up
                    px[dst] = new Color32((byte)(r[src] * 255), (byte)(g[src] * 255), (byte)(b[src] * 255), (byte)(a[src] * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    // ==================================================================
    //  Btn : 호버/클릭 색·스케일·투명도 피드백
    // ==================================================================
}
