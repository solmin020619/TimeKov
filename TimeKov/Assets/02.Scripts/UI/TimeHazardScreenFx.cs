using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ── 시간 급속 감소 구역 화면 효과 ─────────────────────────────────────────────
// 위험 구역(시간이 빨리 줄어드는 건물/지역) 안에 있는 동안 화면 전체에 계속 주는 효과.
//   "약간 어두워지면서 화면이 계속 일렁이는" 느낌 = 어둠 레이어 + 흐르는 노이즈 2겹.
//
// ★스프라이트/이미지 에셋을 일절 쓰지 않는다. 필요한 텍스처(단색·노이즈)를 전부 코드로 생성한다.
//   (프로젝트의 ScreenVignette 와 같은 방식 — Texture2D 를 런타임에 만들어 RawImage 에 물린다.)
//
//   일렁임 원리: 이음매 없는(타일링) 노이즈 텍스처 1장을 만들어 RawImage 2장에 물리고,
//   각각 uvRect 를 서로 다른 방향·속도로 흘린다. 두 겹이 겹치며 간섭해 물결처럼 계속 일렁인다.
//   여기에 uvRect 크기를 사인파로 미세하게 흔들어 '왜곡되는' 느낌을 더한다. (셰이더 불필요)
//
//   씬 세팅/프리팹 불필요 — 필요할 때 런타임에 스스로 만들어진다(지연 싱글톤).
[SingleInstance]
public class TimeHazardScreenFx : MonoBehaviour
{
    // 화면효과 설정값. 구역(TimeHazardZone) 인스펙터에서 넘어온다(코드 상수 아님).
    public struct Config
    {
        public Color darkColor;     // 어두워지는 색(보통 검정, 붉은기 섞어도 됨)
        public float darkAlpha;     // 어두워지는 정도(0~1). "약간"이면 0.2 안팎.
        public Color shimmerColor;  // 일렁임 색
        public float shimmerAlpha;  // 일렁임 세기(0~1)
        public float shimmerSpeed;  // 일렁임 흐르는 속도
        public float warpAmount;    // 일렁임 왜곡(uv 크기 흔들림) 정도
        public float fadeTime;      // 켜지고 꺼질 때 페이드(초)
    }

    private static TimeHazardScreenFx _instance;
    private static bool _quitting;

    // 노이즈 텍스처는 무거우니 한 번만 만들어 공유한다.
    private static Texture2D _noiseTex;
    private static Texture2D _whiteTex;

    private CanvasGroup _cg;
    private RawImage _dark;
    private RawImage _shimmerA;
    private RawImage _shimmerB;
    private Coroutine _fade;
    private Object _owner;      // 현재 효과를 켠 구역 — 겹칠 때 남의 효과를 끄지 않게
    private Config _cfg;
    private bool _running;      // 일렁임 애니메이션 진행 여부

    // ── 외부 API ─────────────────────────────────────────────────────────────
    public static void Show(Object owner, Config cfg)
    {
        if (_quitting) return;
        var inst = Instance;
        if (inst != null) inst.DoShow(owner, cfg);
    }

    public static void Hide(Object owner)
    {
        if (_quitting || _instance == null) return;
        _instance.DoHide(owner);
    }

    private static TimeHazardScreenFx Instance
    {
        get
        {
            if (_instance == null && !_quitting)
            {
                var go = new GameObject("TimeHazardScreenFx");
                _instance = go.AddComponent<TimeHazardScreenFx>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (UIDuplicateGuard.Report(_instance, this)) { Destroy(gameObject); return; }
        _instance = this;
        Build();
    }

    private void OnApplicationQuit() => _quitting = true;
    private void OnDestroy() { if (_instance == this) _instance = null; }

    // ── 캔버스 빌드 ──────────────────────────────────────────────────────────
    private void Build()
    {
        var canvasGo = new GameObject("TimeHazardFxCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // ★모든 UI보다 '뒤'에 깔린다. 이 효과는 화면을 어둡게 하므로 어떤 UI도 가리면 안 된다.
        //   Overlay 캔버스는 sortingOrder 가 음수여도 3D 월드 위에는 항상 그려진다.
        //   → 월드는 덮되 UI는 절대 못 덮는 위치. (프로젝트 UI 레이어: world 0 ~ overlay 500,
        //     그 밖에 5000·9000·32000 등을 쓰는 캔버스도 있어 넉넉히 아래로 내렸다.)
        canvas.sortingOrder = -10000;

        var groupGo = new GameObject("Group", typeof(RectTransform));
        groupGo.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)groupGo.transform);
        _cg = groupGo.AddComponent<CanvasGroup>();
        _cg.alpha = 0f; _cg.blocksRaycasts = false; _cg.interactable = false;

        // 1) 어둠 — 코드로 만든 1x1 흰 텍스처에 색을 곱해 단색 판으로 쓴다(스프라이트 없음).
        _dark = NewLayer(groupGo.transform, "Dark", WhiteTex());

        // 2) 일렁임 2겹 — 같은 노이즈를 서로 다르게 흘려 간섭시킨다.
        var noise = NoiseTex();
        _shimmerA = NewLayer(groupGo.transform, "ShimmerA", noise);
        _shimmerB = NewLayer(groupGo.transform, "ShimmerB", noise);
    }

    private static RawImage NewLayer(Transform parent, string name, Texture2D tex)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        var img = go.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ── 코드로 만드는 텍스처 ─────────────────────────────────────────────────
    private static Texture2D WhiteTex()
    {
        if (_whiteTex != null) return _whiteTex;
        _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _whiteTex.SetPixel(0, 0, Color.white);
        _whiteTex.Apply();
        _whiteTex.hideFlags = HideFlags.HideAndDontSave;
        return _whiteTex;
    }

    // 이음매 없이 반복되는 부드러운 구름 노이즈. 흘려도 경계가 안 보인다.
    private static Texture2D NoiseTex()
    {
        if (_noiseTex != null) return _noiseTex;

        const int size = 256;
        _noiseTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Repeat,   // uvRect 를 흘리려면 필수
            filterMode = FilterMode.Bilinear,
            hideFlags  = HideFlags.HideAndDontSave,
        };

        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // 옥타브를 겹쳐(fBm) 뭉게뭉게한 결. 격자 수가 size 를 나누어떨어져야 이음매가 없다.
            float n = 0.5f * Tileable(x, y, size, 4)
                    + 0.3f * Tileable(x, y, size, 8)
                    + 0.2f * Tileable(x, y, size, 16);

            // 대비를 세워 '결'이 보이게(밋밋한 회색 방지). 알파에만 담고 색은 흰색 고정
            //  → RawImage.color 로 원하는 색·세기를 입힌다.
            n = Mathf.Clamp01((n - 0.35f) / 0.4f);
            n = n * n * (3f - 2f * n);   // smoothstep

            px[y * size + x] = new Color32(255, 255, 255, (byte)(n * 255f));
        }
        _noiseTex.SetPixels32(px);
        _noiseTex.Apply();
        return _noiseTex;
    }

    // 격자 기반 값 노이즈 1옥타브. 격자 좌표를 grid 로 나머지 연산해 상하좌우가 이어진다.
    private static float Tileable(int x, int y, int size, int grid)
    {
        float fx = x / (float)size * grid;
        float fy = y / (float)size * grid;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3f - 2f * tx);   // smoothstep 보간 → 부드러운 결
        ty = ty * ty * (3f - 2f * ty);

        float v00 = Hash(x0 % grid, y0 % grid, grid);
        float v10 = Hash((x0 + 1) % grid, y0 % grid, grid);
        float v01 = Hash(x0 % grid, (y0 + 1) % grid, grid);
        float v11 = Hash((x0 + 1) % grid, (y0 + 1) % grid, grid);

        return Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), ty);
    }

    // 격자점마다 고정된 난수(0~1). 같은 좌표는 항상 같은 값 → 텍스처가 매번 동일.
    private static float Hash(int x, int y, int grid)
    {
        int h = x * 374761393 + y * 668265263 + grid * 1442695040;
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0x7FFFFFFF) / (float)0x7FFFFFFF;
    }

    // ── 표시/숨김 ────────────────────────────────────────────────────────────
    private void DoShow(Object owner, Config cfg)
    {
        _owner = owner;
        _cfg   = cfg;
        ApplyConfig(cfg);
        _running = true;

        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(FadeTo(1f, cfg.fadeTime));
    }

    private void DoHide(Object owner)
    {
        if (_owner != owner) return;   // 이미 다른 구역이 켠 효과면 건드리지 않는다
        _owner = null;
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(FadeOutThenStop(_cfg.fadeTime));
    }

    private void ApplyConfig(Config cfg)
    {
        if (_dark != null)
            _dark.color = new Color(cfg.darkColor.r, cfg.darkColor.g, cfg.darkColor.b, Mathf.Clamp01(cfg.darkAlpha));

        var c = cfg.shimmerColor;
        float a = Mathf.Clamp01(cfg.shimmerAlpha);
        // 두 겹의 세기를 살짝 다르게 → 규칙적으로 안 보이고 자연스럽게 섞인다.
        if (_shimmerA != null) _shimmerA.color = new Color(c.r, c.g, c.b, a);
        if (_shimmerB != null) _shimmerB.color = new Color(c.r, c.g, c.b, a * 0.7f);
    }

    private IEnumerator FadeTo(float target, float dur)
    {
        float from = _cg != null ? _cg.alpha : 0f;
        float t = 0f;
        while (t < dur && _cg != null)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / dur));
            yield return null;
        }
        if (_cg != null) _cg.alpha = target;
        _fade = null;
    }

    private IEnumerator FadeOutThenStop(float dur)
    {
        yield return FadeTo(0f, dur);
        _running = false;   // 다 사라진 뒤에 애니메이션 정지
    }

    // ── 일렁임 애니메이션 ────────────────────────────────────────────────────
    // 텍스처를 다시 만들지 않고 uvRect 만 흘린다(가벼움). 두 겹이 다른 방향·속도로 흘러 간섭 → 물결.
    private void Update()
    {
        if (!_running || _shimmerA == null || _shimmerB == null) return;

        float t = Time.unscaledTime * Mathf.Max(0f, _cfg.shimmerSpeed);

        // uv 크기를 사인파로 미세하게 늘였다 줄인다 → 화면이 왜곡되며 일렁이는 느낌.
        float w = _cfg.warpAmount;
        float sA = 1f + Mathf.Sin(t * 0.7f) * w;
        float sB = 1f + Mathf.Cos(t * 0.5f) * w;

        // 서로 다른 방향으로 흐른다(A는 우상향, B는 좌상향 + 더 느리게).
        _shimmerA.uvRect = new Rect(t * 0.05f, t * 0.03f, sA, sA);
        _shimmerB.uvRect = new Rect(-t * 0.032f, t * 0.045f, sB * 1.3f, sB * 1.3f);
    }
}
