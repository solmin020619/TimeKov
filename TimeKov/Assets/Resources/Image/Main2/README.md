# TIMEKOV — Main Screen Implementation Package

This is a self-contained handoff package for implementing the TIMEKOV main
title screen in **Unity (UGUI + TextMeshPro)**.

It is designed to be fed to Claude Code (or any coding agent) as the source
of truth. Everything Claude Code needs is in this folder — no guessing.

---

## What to build

A title screen with:
- Cool, cinematic mountain photo background (provided)
- Large **TIMEKOV** wordmark, centered, with metallic ice gradient + glow
- A tagline below the wordmark
- A small version chip in the top-left corner
- A pulsing "화면을 클릭하여 시작" prompt near the bottom
- 18 slow-rising atmospheric particles drifting up the screen

Visual reference: `assets/timekov-main-A-reference.png`
Live HTML preview: open `preview/index.html` in any browser

---

## Folder map

```
Main2/
├── README.md            ← you are here
├── SPEC.md              ← layout, sizing, colors, animations (read this 2nd)
├── UNITY_GUIDE.md       ← step-by-step Unity setup (read this 3rd)
├── assets/
│   ├── bg-mountain.png              ← background plate, 1672 × 941
│   └── timekov-main-A-reference.png ← target look at 750 × 416
└── preview/
    ├── index.html       ← runnable browser preview (live animations)
    ├── main-a.jsx       ← React source — the truth for layout + values
    └── bg-mountain.png  ← same as assets/, kept local for the preview
```

---

## Reading order for Claude Code

1. **README.md** — this file. Big picture.
2. **SPEC.md** — exact pixel positions, font sizes, colors, animation timings.
3. **UNITY_GUIDE.md** — concrete Unity setup with code:
   - Canvas + scaling
   - TextMeshPro font asset creation (including Korean fallback)
   - Material settings for the logo (bevel, glow, underlay)
   - Particle system as Image-based UGUI (C# script provided)

The HTML preview in `preview/` is the **visual ground truth**. When the
Unity build looks identical to the preview at 750×416 (scaled up), the job
is done.

---

## Notes about Korean text

The reference uses two Latin fonts (Cinzel, Rajdhani) which do not include
Korean glyphs. The prompt text "화면을 클릭하여 시작" needs a Korean fallback.

We recommend **Pretendard** (or Noto Sans KR) as the Korean fallback. Setup
details are in `UNITY_GUIDE.md` → "Korean text fallback".

---

## Versioning

This is package v1. If you change the design intent (e.g. new background,
different tagline, different mood), update `SPEC.md` and bump the version.
