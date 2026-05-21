# Unity Implementation Guide

Target: **Unity 2022.3 LTS or newer** with **TextMeshPro** package.
Read `SPEC.md` first for the design values, then follow this guide step-by-step.

---

## 1. Project setup

- Install **TextMeshPro** (Package Manager) if not already present
- TMP Essentials imported (Window → TextMeshPro → Import TMP Essential Resources)
- Target resolution: design for 1920 × 1080, scale via Canvas Scaler

---

## 2. Canvas

Create a `Canvas` GameObject with:

- **Render Mode**: Screen Space - Camera (or Overlay if no camera blending needed)
- **Canvas Scaler**:
  - UI Scale Mode: `Scale With Screen Size`
  - Reference Resolution: `1920 × 1080`
  - Screen Match Mode: `Match Width Or Height` → `0.5` (balanced)

Hierarchy:

```
Canvas (1920×1080)
└─ MainScreen (RectTransform, stretch full screen)
   ├─ Background      (RawImage, stretch full)
   ├─ ScrimVertical   (Image, stretch full, gradient sprite or 9-slice)
   ├─ ScrimVignette   (Image, stretch full, radial vignette sprite)
   ├─ ScrimCool       (Image, stretch full, blend mode Overlay via shader)
   ├─ Particles       (RectTransform, stretch full, RectMask2D)
   │  └─ (ParticlePrefab instances at runtime — see §5)
   ├─ VersionChip     (anchored TopLeft)
   ├─ LogoGroup       (vertical layout, centered)
   │  ├─ Wordmark     (TMP_Text)
   │  ├─ Underline    (Image, custom sprite)
   │  └─ Tagline      (TMP_Text)
   └─ PressPrompt     (anchored BottomCenter, horizontal layout)
      ├─ ChevronLeft  (TMP_Text)
      ├─ Text         (TMP_Text)
      └─ ChevronRight (TMP_Text)
```

The 750×416 design coordinates from SPEC.md scale up to 1920×1080 by a
factor of **2.56× horizontally, 2.6× vertically**. Use TMP "Auto Size"
sparingly — explicit anchored values match the design better.

---

## 3. Fonts (TextMeshPro)

### Required fonts

Download all of these as `.ttf`/`.otf` and drop into `Assets/Fonts/`:

| Font | Source | Use |
|------|--------|-----|
| **Cinzel** SemiBold (600) | https://fonts.google.com/specimen/Cinzel | TIMEKOV wordmark |
| **Rajdhani** Regular (400), Medium (500) | https://fonts.google.com/specimen/Rajdhani | All Latin UI text |
| **Pretendard** Medium (500) | https://github.com/orioncactus/pretendard/releases | Korean text fallback |

Pretendard is recommended because it pairs cleanly with Rajdhani's
geometric forms. Alternatives: **Noto Sans KR**, **SUIT Variable**.

### Generate Font Assets

For **each** `.ttf` above:

1. Right-click in Project window → `Create → TextMeshPro → Font Asset`
2. Open the generated asset
3. Set **Atlas Population Mode** to `Dynamic`
   - This lets TMP rasterize new glyphs on demand at runtime (essential for
     Korean — Korean has 11,172 syllable combos, you can't pre-bake them all)
4. Set **Atlas Width** and **Atlas Height** to `1024 × 1024` minimum
   (`2048 × 2048` for Pretendard since it'll hold many Korean glyphs)
5. Set **Sampling Point Size** to `Auto Sizing` and **Padding** to `9`
6. Save

### Korean fallback chain

On the **Rajdhani-Medium** font asset:

1. Scroll to **Fallback Font Assets**
2. Add a list element → drag the **Pretendard-Medium** font asset in
3. Save

Now any TMP_Text using Rajdhani will automatically fall back to Pretendard
when it encounters Korean characters. The Korean characters in
"화면을 클릭하여 시작" will render in Pretendard while the (absent) Latin
letters would stay in Rajdhani.

Repeat for **Rajdhani-Regular** if you use both weights.

---

## 4. Wordmark material (the "ice metal" look)

The TIMEKOV wordmark uses 4 stacked text layers in CSS. In Unity we can
match it with **one** TMP_Text plus material settings.

### TMP_Text component

- Font Asset: `Cinzel-SemiBold SDF`
- Font Size: `159` (62 × 2.56)
- Font Style: **Bold** (B) ON
- Character Spacing: `-2` (or `0` — 0 matches CSS letter-spacing 8% better
  than the default 0; adjust to taste)
- Color: `#FFFFFF` (gradient applied separately, see below)
- Color Gradient: **Enabled**
  - Type: `Vertical`
  - Top: `#FFFFFF`
  - Bottom: `#3D6C8C`
- Material Preset: `Cinzel-SemiBold SDF Material — Titlescreen` (duplicate from default)

### Material settings (duplicate, name it "TIMEKOV-Titlescreen")

- **Face**
  - Color: `FFFFFFFF`
  - Dilate: `0.1` (slightly bold)
- **Outline**
  - Color: `#1A3850` (deep navy)
  - Thickness: `0.1`
- **Underlay** (drop shadow)
  - Color: `#000000FF`
  - Offset X: `0`
  - Offset Y: `-1.0`
  - Dilate: `0.3`
  - Softness: `0.8`
- **Lighting** ← enable Bevel + Light first
  - Light Angle: `3.1416` (light from directly above; PI radians)
  - Specular Color: `#FFFFFF`
  - Specular Power: `2.0`
  - Reflectivity: `10`
  - Diffuse Shadow: `0.5`
  - Ambient Shadow: `0.5`
- **Bevel**
  - Type: `Outer Bevel`
  - Amount: `0.5`
  - Offset: `0`
  - Width: `0.3`
  - Roundness: `0.3`
  - Clamp: `0.5`
- **Glow**
  - Color: `#7EC8E8` (cyan)
  - Offset: `0`
  - Inner: `0.3`
  - Outer: `0.4`
  - Power: `1.0`

Don't apply these on the default material — duplicate first, otherwise
every Cinzel text in the project will get this treatment.

---

## 5. Particles (atmospheric motes)

For UI-locked, deterministic 18-particle motion, use **UGUI Images +
script**, not Unity's ParticleSystem. ParticleSystem fights the Canvas
coordinate space and is overkill for 18 dots.

### Particle prefab

Create a prefab:
```
ParticleDot (RectTransform, pivot 0.5 0.5)
├─ Image
│   - Sprite: a 16×16 white circle (UI default knob works, or generate one)
│   - Color: 1.0, 1.0, 1.0, 1.0 (CanvasGroup will modulate alpha)
└─ CanvasGroup (alpha 0)
```

### Script — TimekovParticles.cs

Drop this on the `Particles` RectTransform in the hierarchy:

```csharp
using UnityEngine;

public class TimekovParticles : MonoBehaviour
{
    [SerializeField] private RectTransform frame;          // the screen / design frame
    [SerializeField] private GameObject particlePrefab;
    [SerializeField] private int count = 18;

    private const float DESIGN_W  = 750f;
    private const float DESIGN_H  = 416f;
    private const float RISE_PX   = 460f;   // CSS translateY
    private const float DRIFT_PX  = 20f;    // CSS translateX
    private static readonly Color BASE_COLOR =
        new Color(200f/255f, 225f/255f, 255f/255f, 1f);

    private struct P {
        public RectTransform rt;
        public CanvasGroup   cg;
        public float baseX, baseY;
        public float duration;
        public float maxAlpha;   // per-particle 0.25–0.74
        public float elapsed;
    }
    private P[] particles;

    void Start() {
        particles = new P[count];
        Vector2 size = frame.rect.size;
        float sx = size.x / DESIGN_W;
        float sy = size.y / DESIGN_H;

        for (int i = 0; i < count; i++) {
            var go = Instantiate(particlePrefab, frame);
            var rt = go.GetComponent<RectTransform>();
            var cg = go.GetComponent<CanvasGroup>();
            var img = go.GetComponentInChildren<UnityEngine.UI.Image>();
            if (img != null) img.color = BASE_COLOR;

            float leftPct  = (i * 137.5f) % 100f;
            float delay    = (i * 1.31f)  % 8f;
            float dur      = 14f + (i * 7) % 10;
            float sizePx   = 1f + (i % 3) * 0.5f;
            float opacity  = 0.25f + ((i * 17) % 50) / 100f;

            rt.sizeDelta = new Vector2(sizePx * sx, sizePx * sy);
            rt.anchorMin = rt.anchorMax = Vector2.zero; // bottom-left

            float baseXPx = (leftPct / 100f) * DESIGN_W;
            float baseYPx = -5f;

            particles[i] = new P {
                rt = rt, cg = cg,
                baseX = baseXPx * sx,
                baseY = baseYPx * sy,
                duration = dur,
                maxAlpha = opacity,
                elapsed = -delay
            };
            cg.alpha = 0;
        }
    }

    void Update() {
        Vector2 size = frame.rect.size;
        float sx = size.x / DESIGN_W;
        float sy = size.y / DESIGN_H;

        for (int i = 0; i < count; i++) {
            ref var p = ref particles[i];
            p.elapsed += Time.deltaTime;
            if (p.elapsed < 0) { p.cg.alpha = 0; continue; }

            float t = (p.elapsed % p.duration) / p.duration;

            float dx = DRIFT_PX * t * sx;
            float dy = RISE_PX  * t * sy;
            p.rt.anchoredPosition = new Vector2(p.baseX + dx, p.baseY + dy);

            float a;
            if      (t < 0.1f) a = Mathf.Lerp(0f,   0.7f, t / 0.1f);
            else if (t < 0.9f) a = Mathf.Lerp(0.7f, 0.5f, (t - 0.1f) / 0.8f);
            else               a = Mathf.Lerp(0.5f, 0f,   (t - 0.9f) / 0.1f);

            // Combine the keyframe alpha with the per-particle opacity ceiling
            p.cg.alpha = a * (p.maxAlpha / 0.7f);  // 0.7 == peak keyframe value
        }
    }
}
```

Important: add a `RectMask2D` on the `Particles` GameObject so motes
clip to the frame edges instead of bleeding into the wider canvas.

---

## 6. Pulsing prompt animation

For the "화면을 클릭하여 시작" prompt, you have two options:

### Option A (recommended): one-line script

```csharp
using TMPro;
using UnityEngine;

public class PulseAlpha : MonoBehaviour
{
    [SerializeField] private CanvasGroup target;
    [SerializeField] private float minA = 0.55f;
    [SerializeField] private float maxA = 1.0f;
    [SerializeField] private float duration = 2.4f;

    void Update() {
        float t = (Mathf.Sin(Time.time * Mathf.PI * 2f / duration) + 1f) * 0.5f;
        // ease-in-out approximation
        t = t * t * (3f - 2f * t);
        target.alpha = Mathf.Lerp(minA, maxA, t);
    }
}
```

Add a `CanvasGroup` to the `PressPrompt` GameObject, drag it into `target`.

### Option B: Animator

Create an Animator with a single clip that animates `CanvasGroup.alpha` on
a loop, 0–1.2–2.4s keyframes at 0.55, 1.0, 0.55.

---

## 7. Underline accent sprite

Either:
- (a) Author a 273×11 PNG matching the SPEC.md SVG path, drop into Assets
- (b) Convert the SVG to a mesh with **Unity's SVG Importer** package and
  use it as a `Sprite`

The SVG is simple enough that (a) is fine. Background must be transparent.

---

## 8. Verifying against the preview

Open `preview/index.html` in a browser. The Unity build at the same aspect
ratio should match it pixel-for-pixel (allowing for font rendering
differences between WebKit and FreeType). If something looks off:

- Wordmark too small/too big → check Font Size (159 for 1080p)
- Wordmark looks flat → bevel/lighting probably not enabled in material
- No glow → check Glow section enabled, Outer = 0.4
- Korean shows as `□□□□` → fallback font not set; revisit §3 Korean
- Particles invisible → CanvasGroup alpha clamped at 0; check RectMask2D
  isn't clipping them away; check sizeDelta (>0?)
- Particles too fast/slow → tweak `duration` range in TimekovParticles.cs
- Particles spawn in same spot → confirm the `(i * 137.5) % 100` formula

---

## 9. Tweaking knobs

The values in SPEC.md are the design intent, not commandments. Feel free
to:
- Bump wordmark size if 1920×1080 feels under-sized
- Soften the bevel if the metal look is too "trophy"
- Adjust particle count up to ~30 or down to ~12 for taste
- Move the version chip to the corner that matches your other UI screens
