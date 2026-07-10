// =====================================================================
// UISpriteFactory.cs
// 절차 생성 UI 스프라이트 캐시 (런타임). 디자인 스프라이트 없이 코드로 모양 생성.
// 둥근 사각(흰색, 9-slice) / 수직 그라디언트 둥근 사각 / 소프트 디스크 / 링 / 자물쇠.
// 흰색 스프라이트는 Image.color 로 틴트해서 재사용.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public static class UISpriteFactory
{
    private static readonly Dictionary<string, Sprite> _cache = new();

    // 흰색 둥근 사각 (9-slice). Image.color로 틴트.
    public static Sprite RoundedRect(int size = 64, int radius = 16)
    {
        string key = $"rr_{size}_{radius}";
        if (_cache.TryGetValue(key, out var c)) return c;

        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, radius, size - radius);
                float cy = Mathf.Clamp(fy, radius, size - radius);
                float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                float cov = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(cov * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        var border = new Vector4(radius, radius, radius, radius);
        var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        _cache[key] = s; return s;
    }

    // 수직 그라디언트 둥근 사각 (top→bottom 색 고정). 슬롯 안쪽 입체용.
    public static Sprite RoundedRectVGrad(Color32 top, Color32 bottom, int size = 64, int radius = 14)
    {
        string key = $"rrg_{size}_{radius}_{top.r}_{top.g}_{top.b}_{bottom.r}_{bottom.g}_{bottom.b}";
        if (_cache.TryGetValue(key, out var c)) return c;

        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float ty = 1f - (y + 0.5f) / size; // 0=하단,1=상단 → 위가 밝게
            byte r = (byte)Mathf.Lerp(bottom.r, top.r, ty);
            byte g = (byte)Mathf.Lerp(bottom.g, top.g, ty);
            byte b = (byte)Mathf.Lerp(bottom.b, top.b, ty);
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, radius, size - radius);
                float cy = Mathf.Clamp(fy, radius, size - radius);
                float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                float cov = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(r, g, b, (byte)(cov * 255));
            }
        }
        tex.SetPixels32(px); tex.Apply();
        var border = new Vector4(radius, radius, radius, radius);
        var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        _cache[key] = s; return s;
    }

    // 수직 알파 페이드 (흰색, 알파만 위/아래로 변화). Image.color 로 틴트.
    // RoundedRectVGrad 는 알파를 모양 coverage 로 덮어써서 페이드가 안 됨 - 페이드는 이걸 쓸 것.
    // topA = 텍스처 위쪽 알파, bottomA = 아래쪽 알파.
    public static Sprite VFade(byte topA, byte bottomA, int size = 64)
    {
        string key = $"vfade_{size}_{topA}_{bottomA}";
        if (_cache.TryGetValue(key, out var c)) return c;

        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float ty = (y + 0.5f) / size; // 0=하단, 1=상단 (SetPixels32 는 아래 행부터)
            byte a = (byte)Mathf.Lerp(bottomA, topA, ty);
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px); tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _cache[key] = s; return s;
    }

    // 소프트 디스크 (가운데 흰색 → 가장자리 투명). 빛폭발/글로우용.
    public static Sprite Disc(int size = 64)
    {
        string key = $"disc_{size}";
        if (_cache.TryGetValue(key, out var c)) return c;

        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        float cc = size * 0.5f, rr = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cc) * (x + 0.5f - cc) + (y + 0.5f - cc) * (y + 0.5f - cc)) / rr;
                float a = Mathf.Clamp01(1f - d);
                a = a * a; // 부드럽게
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _cache[key] = s; return s;
    }

    // 링 (테두리 원). 해금 폭발 링용.
    public static Sprite Ring(int size = 64, float thickness = 4f)
    {
        string key = $"ring_{size}_{thickness}";
        if (_cache.TryGetValue(key, out var c)) return c;

        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        float cc = size * 0.5f, r = size * 0.5f - thickness;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cc) * (x + 0.5f - cc) + (y + 0.5f - cc) * (y + 0.5f - cc));
                float cov = Mathf.Clamp01(thickness - Mathf.Abs(d - r) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(cov * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _cache[key] = s; return s;
    }

    // 자물쇠 실루엣 (닫힘). 흰색 → Image.color 틴트. 열쇠구멍은 투명.
    public static Sprite Lock(int N = 96)
    {
        string key = $"lock_{N}";
        if (_cache.TryGetValue(key, out var c)) return c;

        float S = N / 48f; // svg 48 기준
        var tex = NewTex(N, N);
        var px = new Color32[N * N];

        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float sx = (x + 0.5f) / S;
                float sy = 48f - (y + 0.5f) / S; // y 뒤집기 (svg y-down)

                float a = 0f;
                // 몸체: 둥근 사각 [11,22]~[37,40] r4
                a = Mathf.Max(a, RRCov(sx, sy, 11f, 22f, 37f, 40f, 4f));
                // 고리 윗부분: 중심(24,17) r8, 위쪽 반원, 두께 3
                if (sy >= 16.5f)
                {
                    float d = Mathf.Sqrt((sx - 24f) * (sx - 24f) + (sy - 17f) * (sy - 17f));
                    a = Mathf.Max(a, Mathf.Clamp01(1.5f - Mathf.Abs(d - 8f) + 0.5f));
                }
                // 고리 세로 두 기둥 (x=16, x=32, y 17~23)
                if (sy >= 17f && sy <= 23.5f)
                {
                    a = Mathf.Max(a, Mathf.Clamp01(1.5f - Mathf.Abs(sx - 16f) + 0.5f));
                    a = Mathf.Max(a, Mathf.Clamp01(1.5f - Mathf.Abs(sx - 32f) + 0.5f));
                }
                // 열쇠구멍 빼기: 원(24,29) r2.6 + 세로 슬릿
                float kd = Mathf.Sqrt((sx - 24f) * (sx - 24f) + (sy - 29f) * (sy - 29f));
                if (kd <= 2.6f) a = 0f;
                if (sx >= 22.7f && sx <= 25.3f && sy >= 30f && sy <= 36f) a = 0f;

                px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        _cache[key] = s; return s;
    }

    // 둥근 사각 coverage (svg 좌표)
    private static float RRCov(float px, float py, float x0, float y0, float x1, float y1, float r)
    {
        float cx = Mathf.Clamp(px, x0 + r, x1 - r);
        float cy = Mathf.Clamp(py, y0 + r, y1 - r);
        if (px >= x0 && px <= x1 && py >= y0 && py <= y1)
        {
            float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }
        return 0f;
    }

    // 금지 표시 (원형 테두리 + 대각선 슬래시). 흰색 → Image.color 로 빨강 등 틴트.
    public static Sprite NoEntry(int size = 96, float thickness = -1f)
    {
        if (thickness < 0f) thickness = size * 0.11f;
        string key = $"noentry_{size}_{thickness}";
        if (_cache.TryGetValue(key, out var c)) return c;

        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        float cc = size * 0.5f;
        float rOuter = size * 0.5f - 1f;
        float ringMid = rOuter - thickness * 0.5f;   // 링 중심 반경
        float rInner = rOuter - thickness;           // 링 안쪽 반경
        // 대각선 막대 (좌상→우하, 45도). 막대 길이축/두께축 분리.
        Vector2 dir = new Vector2(0.70710678f, 0.70710678f);
        float barHalfLen = rInner;
        float barHalfThick = thickness * 0.5f;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float dx = fx - cc, dy = fy - cc;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                // 링 coverage
                float ringCov = Mathf.Clamp01(thickness * 0.5f - Mathf.Abs(d - ringMid) + 0.5f);

                // 대각선 막대 coverage (원 안쪽 한정)
                float barCov = 0f;
                if (d <= rInner + 0.5f)
                {
                    float along = dx * dir.x + dy * dir.y;            // 길이축
                    float perp  = Mathf.Abs(-dx * dir.y + dy * dir.x); // 두께축
                    if (Mathf.Abs(along) <= barHalfLen)
                        barCov = Mathf.Clamp01(barHalfThick - perp + 0.5f);
                }

                float a = Mathf.Clamp01(Mathf.Max(ringCov, barCov));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _cache[key] = s; return s;
    }

    // 꽉 찬 원 (쿨다운 라디얼 등). 가장자리 AA.
    public static Sprite Circle(int size = 96)
    {
        string key = $"circle_{size}";
        if (_cache.TryGetValue(key, out var c)) return c;
        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        float cc = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cc) * (x + 0.5f - cc) + (y + 0.5f - cc) * (y + 0.5f - cc));
                float a = Mathf.Clamp01(cc - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _cache[key] = s; return s;
    }

    private static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
    {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp,
    };
}
