/* TIMEKOV — Main Screen A · Cinematic Minimal
   Self-contained preview component.
   Background image expected at ./bg-mountain.png (relative to index.html). */

const { useState, useEffect, useRef } = React;

/* ---------- TIMEKOV wordmark ---------- */
const TimekovLogo = ({ size = 62, accent = '#bfe4ff', tagline = null }) => (
  <div style={{
    display: 'inline-flex', flexDirection: 'column', alignItems: 'center',
    gap: size * 0.06, pointerEvents: 'none',
  }}>
    <div style={{
      fontFamily: 'Cinzel, serif',
      fontWeight: 600,
      fontSize: size,
      letterSpacing: size * 0.08,
      lineHeight: 1,
      color: '#e8f4ff',
      background: `linear-gradient(180deg, #ffffff 0%, ${accent} 40%, #7ea8c4 70%, #ffffff 100%)`,
      WebkitBackgroundClip: 'text',
      backgroundClip: 'text',
      WebkitTextFillColor: 'transparent',
      textShadow: `0 0 ${size * 0.3}px rgba(150,200,255,0.35)`,
      filter: `drop-shadow(0 ${size * 0.04}px ${size * 0.12}px rgba(0,0,0,0.55))`,
    }}>
      TIMEKOV
    </div>
    {/* underline accent with diamond mid-point */}
    <svg width={size * 4.4} height={size * 0.18} viewBox="0 0 440 18" style={{ opacity: 0.85 }}>
      <defs>
        <linearGradient id="ul-grad" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0%" stopColor={accent} stopOpacity="0" />
          <stop offset="50%" stopColor={accent} stopOpacity="1" />
          <stop offset="100%" stopColor={accent} stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d="M 0 9 L 200 9 L 215 2 L 225 9 L 240 9 L 440 9" stroke="url(#ul-grad)" strokeWidth="1.2" fill="none" />
      <circle cx="220" cy="9" r="2.5" fill={accent} />
    </svg>
    {tagline && (
      <div style={{
        fontFamily: 'Rajdhani, sans-serif',
        fontSize: size * 0.18,
        letterSpacing: size * 0.08,
        textTransform: 'uppercase',
        color: 'rgba(220,235,255,0.65)',
        fontWeight: 400,
        marginTop: size * 0.05,
        whiteSpace: 'nowrap',
      }}>{tagline}</div>
    )}
  </div>
);

/* ---------- Slow drifting particles (atmospheric motes) ---------- */
const Particles = ({ count = 18, color = 'rgba(200,225,255,0.6)' }) => {
  const dots = Array.from({ length: count }, (_, i) => {
    const left = (i * 137.5) % 100;
    const delay = (i * 1.31) % 8;
    const dur = 14 + ((i * 7) % 10);
    const size = 1 + (i % 3) * 0.5;
    const opacity = 0.25 + ((i * 17) % 50) / 100;
    return (
      <div key={i} style={{
        position: 'absolute',
        left: `${left}%`,
        bottom: -5,
        width: size,
        height: size,
        borderRadius: '50%',
        background: color,
        opacity,
        animation: `tk-rise ${dur}s linear ${delay}s infinite`,
        filter: 'blur(0.3px)',
      }} />
    );
  });
  return <div style={{ position: 'absolute', inset: 0, overflow: 'hidden', pointerEvents: 'none' }}>{dots}</div>;
};

/* ---------- Pulsing press-to-start prompt ---------- */
const PressPrompt = ({ children = '화면을 클릭하여 시작' }) => (
  <div style={{
    position: 'absolute', left: 0, right: 0, bottom: 28,
    display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 14,
    animation: 'tk-pulse 2.4s ease-in-out infinite',
    whiteSpace: 'nowrap',
  }}>
    <span style={{ color: 'rgba(190,220,255,0.7)', fontFamily: 'Rajdhani', letterSpacing: 4, fontSize: 11 }}>◂</span>
    <span style={{
      fontFamily: 'Rajdhani, "Pretendard Variable", "Noto Sans KR", sans-serif',
      fontSize: 13,
      letterSpacing: 5,
      color: 'rgba(230,240,255,0.92)',
      textTransform: 'uppercase',
      textShadow: '0 0 18px rgba(150,200,255,0.5)',
      whiteSpace: 'nowrap',
      fontWeight: 500,
    }}>{children}</span>
    <span style={{ color: 'rgba(190,220,255,0.7)', fontFamily: 'Rajdhani', letterSpacing: 4, fontSize: 11 }}>▸</span>
  </div>
);

/* ---------- Backdrop (clean plate + scrims + cool tint) ---------- */
const Backdrop = ({ children, scrim = 0.3 }) => (
  <div style={{
    position: 'relative', width: '100%', height: '100%',
    overflow: 'hidden', background: '#0a0e14',
  }}>
    <img
      src="bg-mountain.png"
      alt=""
      style={{
        position: 'absolute', inset: 0,
        width: '100%', height: '100%',
        objectFit: 'cover',
        filter: 'saturate(1.05) contrast(1.03)',
      }}
    />
    <div style={{
      position: 'absolute', inset: 0,
      background: `
        linear-gradient(180deg, rgba(8,14,22,${scrim + 0.05}) 0%, rgba(8,14,22,0) 25%, rgba(8,14,22,0) 65%, rgba(8,14,22,${scrim + 0.25}) 100%),
        radial-gradient(ellipse at 50% 55%, rgba(8,14,22,0) 35%, rgba(8,14,22,0.3) 100%)
      `,
      pointerEvents: 'none',
    }} />
    <div style={{
      position: 'absolute', inset: 0,
      background: 'linear-gradient(180deg, rgba(120,180,230,0.04) 0%, rgba(80,120,180,0.02) 100%)',
      mixBlendMode: 'overlay',
      pointerEvents: 'none',
    }} />
    {children}
  </div>
);

/* ---------- Main screen A · Cinematic Minimal ---------- */
const MainScreenA = () => (
  <Backdrop scrim={0.3}>
    {/* version chip top-left */}
    <div style={{
      position: 'absolute', top: 18, left: 22,
      fontFamily: 'Rajdhani', fontSize: 10, letterSpacing: 3,
      color: 'rgba(220,235,255,0.55)',
      textTransform: 'uppercase',
      whiteSpace: 'nowrap',
      textShadow: '0 1px 6px rgba(0,0,0,0.6)',
    }}>
      <div>v 0.7.4 — closed beta</div>
      <div style={{ marginTop: 3, color: 'rgba(180,210,240,0.4)' }}>build 2024.11.18</div>
    </div>

    {/* centered logo */}
    <div style={{
      position: 'absolute', inset: 0,
      display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
    }}>
      <TimekovLogo size={62} tagline="Echoes of the Drift" />
    </div>

    {/* press to start (pulsing chevrons) */}
    <PressPrompt>화면을 클릭하여 시작</PressPrompt>

    <Particles count={18} />
  </Backdrop>
);

ReactDOM.createRoot(document.getElementById('root')).render(<MainScreenA />);
