using UnityEngine;

// ── 전송률 게이지의 구간 아이콘 (자연 / 설원 / 사막 / 용암) ────────────────────
// 게이지가 초록→파랑→주황→빨강으로 차오르는데, 뒷부분이 빨강이라 "여기까지 채우면
// 안 되는 것 아닌가" 하고 읽히는 문제가 있었다. 각 구간이 '맵 테마'라는 뜻을 주려고
// 구간 한가운데에 테마 아이콘을 얹는다.
//
// ★아이콘 에셋을 새로 만들지 않고 코드로 그린다 — 이 UI 가 이미 물결 텍스처를
//   런타임 Texture2D 로 만들고 있어 방식이 같고, 4개짜리 단순 실루엣이라 그림 파일이 아깝다.
//   (설정창처럼 PNG 로 구워야 하면 SettingsSpriteBaker 방식으로 옮기면 된다)
//
// 흰색 실루엣으로만 만들고 색은 Image.color 로 입힌다 → 도달/미도달 상태에 따라
// 같은 스프라이트를 지역색/흐린색으로 재사용할 수 있다.
public static class TransmissionRegionIcons
{
    private const int Size = 48;   // 게이지 안에 들어가는 크기라 이 정도면 충분
    private const int SS   = 3;    // 한 픽셀당 3x3 슈퍼샘플링(계단 방지)

    private static Sprite[] _cache;

    /// 0=자연(잎) 1=설원(눈결정) 2=사막(태양) 3=용암(불꽃)
    public static Sprite Get(int regionIndex)
    {
        if (_cache == null)
        {
            _cache = new Sprite[4];
            _cache[0] = Make(Leaf);
            _cache[1] = Make(Snowflake);
            _cache[2] = Make(Sun);
            _cache[3] = Make(Flame);
        }
        return _cache[Mathf.Clamp(regionIndex, 0, 3)];
    }

    // ── 스프라이트 생성 ──────────────────────────────────────────────────────
    private delegate bool InsideTest(float x, float y);   // 좌표계: -1..1, y 위쪽이 +

    private static Sprite Make(InsideTest inside)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.HideAndDontSave,
        };

        var px = new Color32[Size * Size];
        float step = 2f / Size;

        for (int j = 0; j < Size; j++)
        for (int i = 0; i < Size; i++)
        {
            int hit = 0;
            for (int sj = 0; sj < SS; sj++)
            for (int si = 0; si < SS; si++)
            {
                float x = -1f + (i + (si + 0.5f) / SS) * step;
                float y = -1f + (j + (sj + 0.5f) / SS) * step;
                if (inside(x, y)) hit++;
            }
            byte a = (byte)(255f * hit / (SS * SS));
            px[j * Size + i] = new Color32(255, 255, 255, a);   // 흰 실루엣 — 색은 Image.color 로
        }

        tex.SetPixels32(px);
        tex.Apply();

        var sp = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    // ── 도형들 (작게 보이므로 디테일보다 실루엣 가독성 위주) ──────────────────

    // 자연 = 잎. 렌즈(원 두 개 교집합)만 쓰면 폭이 좁아 '흰 조각'처럼 보인다 →
    //   ① 렌즈를 더 통통하게  ② 밑동을 원으로 둥글려 위만 뾰족하게  ③ 줄기와 잎맥을 넣어
    //   한눈에 잎으로 읽히게 한다. 마지막에 살짝 기울인다.
    private static bool Leaf(float x, float y)
    {
        // 30도 기울임 — 곧게 세우면 물방울처럼 보인다.
        const float ca = 0.866f, sa = 0.5f;
        float px = x * ca + y * sa;
        float py = -x * sa + y * ca;

        // 줄기(밑동에서 아래로)
        if (py > -0.98f && py < -0.55f && Mathf.Abs(px) < 0.065f) return true;

        // 잎몸 = 렌즈 ∪ 밑동 원  (렌즈는 위아래가 뾰족하므로 아래만 둥글린다)
        const float c = 0.40f, r = 0.80f;
        bool lens = (px + c) * (px + c) + py * py < r * r
                 && (px - c) * (px - c) + py * py < r * r;
        bool baseRound = px * px + (py + 0.34f) * (py + 0.34f) < 0.30f * 0.30f;
        if (!(lens || baseRound)) return false;

        // 잎맥 — 가운데를 가늘게 비워 잎이라는 단서를 준다(작은 크기에서도 잘 읽힌다).
        if (Mathf.Abs(px) < 0.05f && py > -0.48f && py < 0.55f) return false;

        return true;
    }

    // 설원 = 눈결정. 60도 간격 가지 6개 + 각 가지에 잔가지 2쌍.
    private static bool Snowflake(float x, float y)
    {
        for (int k = 0; k < 6; k++)
        {
            float a = k * Mathf.PI / 3f;
            float ca = Mathf.Cos(-a), sa = Mathf.Sin(-a);
            float px = x * ca - y * sa;      // 가지를 +x 축으로 정렬
            float py = x * sa + y * ca;

            if (px >= -0.06f && px <= 0.86f && Mathf.Abs(py) < 0.085f) return true;   // 몸통

            // 잔가지 — 가지 위 두 지점에서 ±55도로 뻗는다.
            if (Branch(px, py, 0.36f) || Branch(px, py, 0.60f)) return true;
        }
        return false;
    }

    private static bool Branch(float px, float py, float at)
    {
        const float len = 0.26f, th = 0.07f;
        float bx = px - at;
        // ±55도 두 방향을 |py| 로 접어서 한 번에 검사
        float ay = Mathf.Abs(py);
        float c = 0.574f, s = 0.819f;         // cos55, sin55
        float u = bx * c + ay * s;            // 가지 방향 성분
        float v = -bx * s + ay * c;           // 수직 성분
        return u >= 0f && u <= len && Mathf.Abs(v) < th;
    }

    // 사막 = 태양. 가운데 원 + 광선 8개.
    private static bool Sun(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        if (r < 0.34f) return true;                       // 해

        if (r >= 0.50f && r <= 0.86f)                     // 광선
        {
            float a = Mathf.Atan2(y, x);
            float seg = Mathf.PI / 4f;                    // 45도 간격 = 8개
            float d = Mathf.Abs(Mathf.Repeat(a + seg * 0.5f, seg) - seg * 0.5f);
            if (d < 0.16f) return true;
        }
        return false;
    }

    // 용암 = 불꽃. 큰 몸통 원 위에 봉우리 둘(높은 것 + 낮은 것)이 얹힌 구조.
    //   ★작은 크기에서 불로 읽히는 핵심은 '둥근 몸통 + 위쪽 두 봉우리 사이의 골'이다.
    //     - 매끈한 물방울 하나 → 물방울로 읽힌다
    //     - 폭을 물결치게(sin) → 22px 에선 전혀 안 보인다
    //     - 혀 둘을 같은 높이로 세움 → 뭉개져서 깨져 보인다
    //     몸통이 아래 60%를 차지하고 봉우리 높이를 확실히 다르게 줘야 비로소 불꽃이 된다.
    private static bool Flame(float x, float y)
    {
        // 몸통 — 아래쪽 대부분을 차지하는 큰 원.
        if (x * x + (y + 0.32f) * (y + 0.32f) < 0.54f * 0.54f) return true;

        // 높은 봉우리 — 살짝 왼쪽에서 시작해 위로 길게.
        if (y >= -0.20f && y <= 0.88f)
        {
            float t  = (y + 0.20f) / 1.08f;
            float cx = -0.11f + 0.14f * t;
            float w  = 0.29f * Mathf.Pow(1f - t, 0.85f);
            if (Mathf.Abs(x - cx) < w) return true;
        }

        // 낮은 봉우리 — 오른쪽에서 절반 높이까지. 둘 사이의 골이 불꽃의 인상을 만든다.
        if (y >= -0.20f && y <= 0.44f)
        {
            float t2  = (y + 0.20f) / 0.64f;
            float cx2 = 0.33f + 0.05f * t2;
            float w2  = 0.26f * Mathf.Pow(1f - t2, 0.85f);
            if (Mathf.Abs(x - cx2) < w2) return true;
        }

        return false;
    }
}
