# TIMEKOV Main Screen — Spec

Design canvas size: **750 × 416** (matches the HTML preview).
In Unity this scales up to the real game resolution via Canvas Scaler.
All pixel coordinates below are in design space (750 × 416 reference).

---

## Background

- Image: `assets/bg-mountain.png` (1672 × 941, no UI baked in)
- Object fit: `cover` — fills the frame, crop overflow
- Color adjustments before display:
  - Saturation: 1.05× (slight)
  - Contrast: 1.03× (slight)

### Atmospheric scrims (drawn on top of the photo)

Two stacked overlays for contrast and mood. Both `pointer-events: none`.

**Scrim 1 — vertical gradient + center vignette**
```
linear-gradient(180deg,
  rgba(8,14,22,0.35) 0%,
  rgba(8,14,22,0) 25%,
  rgba(8,14,22,0) 65%,
  rgba(8,14,22,0.55) 100%),
radial-gradient(ellipse at 50% 55%,
  rgba(8,14,22,0) 35%,
  rgba(8,14,22,0.3) 100%)
```

**Scrim 2 — cool tint, overlay blend**
```
linear-gradient(180deg,
  rgba(120,180,230,0.04) 0%,
  rgba(80,120,180,0.02) 100%)
blend-mode: overlay
```

---

## Elements

### 1. Version chip (top-left)

- Position: `top: 18px, left: 22px`
- Font: **Rajdhani**, weight 400, size 10px, letter-spacing 3px, ALL CAPS
- Text shadow: `0 1px 6px rgba(0,0,0,0.6)`
- Two lines:
  - Line 1: `v 0.7.4 — closed beta` → color `rgba(220,235,255,0.55)`
  - Line 2: `build 2024.11.18` → color `rgba(180,210,240,0.4)`, marginTop 3px

### 2. TIMEKOV wordmark (centered)

- Position: horizontally + vertically centered in the frame
- Font: **Cinzel**, weight 600, size **62px**, letter-spacing 8% of size (≈ 5px)
- Line-height: 1
- Fill: vertical gradient applied via background-clip:
  ```
  linear-gradient(180deg,
    #ffffff 0%,
    #bfe4ff 40%,
    #7ea8c4 70%,
    #ffffff 100%)
  ```
- Glow: `text-shadow: 0 0 18.6px rgba(150,200,255,0.35)`
- Drop shadow: `filter: drop-shadow(0 2.5px 7.4px rgba(0,0,0,0.55))`

### 3. Underline accent (below wordmark)

- SVG path, ~273px wide × 11px tall, opacity 0.85
- Light cyan (`#bfe4ff`) gradient line that fades at both ends
- Small diamond shape at the midpoint:
  ```
  path d="M 0 9 L 200 9 L 215 2 L 225 9 L 240 9 L 440 9"
  ```
- A 2.5px-radius solid cyan circle sits on the diamond peak (x=220, y=9)

### 4. Tagline (below underline)

- Text: `Echoes of the Drift`
- Font: **Rajdhani**, weight 400, size 11.2px, letter-spacing 5px, ALL CAPS
- Color: `rgba(220,235,255,0.65)`
- marginTop: 3px above

### 5. Press-to-start prompt (bottom)

- Position: `bottom: 28px`, horizontally centered
- Three pieces side-by-side with 14px gap:
  - Left chevron `◂` — Rajdhani, 11px, color `rgba(190,220,255,0.7)`
  - Text: `화면을 클릭하여 시작` — Rajdhani 500, 13px, letter-spacing 5px,
    color `rgba(230,240,255,0.92)`, text-shadow `0 0 18px rgba(150,200,255,0.5)`
    (KOREAN — needs font fallback in Unity, see UNITY_GUIDE.md)
  - Right chevron `▸` — same as left
- **Animation `tk-pulse`** — opacity oscillates 0.55 ↔ 1.0 over 2.4s ease-in-out, infinite

### 6. Particles (atmospheric motes)

- Count: 18
- Each particle:
  - `Image` with solid color `rgba(200,225,255,0.6)`
  - Border-radius 50% (circle)
  - 0.3px blur filter (Unity: skip or simulate with a slightly soft sprite)
- Spawn pattern (per-index seed, NOT random — keeps preview deterministic):
  - `leftPercent = (i * 137.5) % 100`
  - `delay       = (i * 1.31) % 8` (seconds)
  - `duration    = 14 + (i * 7) % 10` (14–23 seconds)
  - `size        = 1 + (i % 3) * 0.5` (1, 1.5, or 2 px)
  - `opacity     = 0.25 + ((i * 17) % 50) / 100` (0.25–0.74)
- Position: bottom of the frame at `bottom: -5px`, `left: leftPercent%`
- **Animation `tk-rise`** — for each particle, looping:
  - 0%   `translateY(0) translateX(0)`, opacity 0
  - 10%  opacity 0.7
  - 90%  opacity 0.5
  - 100% `translateY(-460px) translateX(20px)`, opacity 0
  - Linear easing

---

## Animations (CSS keyframes — for reference)

```css
@keyframes tk-rise {
  0%   { transform: translateY(0)    translateX(0);  opacity: 0;   }
  10%  {                                              opacity: 0.7; }
  90%  {                                              opacity: 0.5; }
  100% { transform: translateY(-460px) translateX(20px); opacity: 0; }
}

@keyframes tk-pulse {
  0%, 100% { opacity: 0.55; }
  50%      { opacity: 1;    }
}
```

---

## Z-order (bottom → top)

1. Background image
2. Scrim 1 (vertical + vignette)
3. Scrim 2 (cool tint overlay)
4. Particles (animated, behind UI text)
5. Version chip
6. Wordmark + underline + tagline (centered group)
7. Press-to-start prompt
