// locks.jsx — 자물쇠(닫힘/깨짐) + 설비 아이콘 + 해금 폭발 FX (블루)
function LockClosed({ size = 44 }) {
  return (
    <svg width={size} height={size} viewBox="0 0 48 48">
      <path d="M16 23 v-6 a8 8 0 0 1 16 0 v6" fill="none" stroke="#c3ccd6" strokeWidth="3" strokeLinecap="round" />
      <rect x="11" y="22" width="26" height="18" rx="4" fill="#b3bcc8" />
      <rect x="11" y="22" width="26" height="18" rx="4" fill="none" stroke="#6b7686" strokeWidth="1.5" />
      <circle cx="24" cy="29" r="2.6" fill="#39414e" />
      <rect x="22.7" y="30" width="2.6" height="6" rx="1.3" fill="#39414e" />
    </svg>
  );
}
// 깨진 자물쇠 (갈라진 몸체 + 튕긴 고리)
function LockBroken({ size = 44 }) {
  return (
    <svg width={size} height={size} viewBox="0 0 48 48">
      <path d="M14 20 v-3 a8 8 0 0 1 15 -3" fill="none" stroke="#9fe6ff" strokeWidth="3" strokeLinecap="round" transform="rotate(-24 16 18)" />
      <path d="M11 22 h11 l-2 9 l3 0 l-2 9 h-9 a0 0 0 0 1 0 0 z" fill="#7fa9c8" stroke="#2f9be0" strokeWidth="1.4" strokeLinejoin="round" />
      <path d="M26 22 h11 v18 h-9 l3 -9 l-2 -9 z" fill="#5d8bad" stroke="#2f9be0" strokeWidth="1.4" strokeLinejoin="round" />
      <path d="M24 22 l-2 9 l3 0 l-2 9" fill="none" stroke="#cdeeff" strokeWidth="1.6" strokeLinecap="round" />
    </svg>
  );
}

// 설비 아이콘 — 단순 지오메트리 플레이스홀더 (실제 설비 아트로 교체)
function Facility({ kind, size = 48 }) {
  const s = { fill: "none", stroke: "url(#cy)", strokeWidth: 3, strokeLinecap: "round", strokeLinejoin: "round" };
  const defs = (
    <defs><linearGradient id="cy" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stopColor="#8fe0ff" /><stop offset="0.5" stopColor="#4fc0f5" /><stop offset="1" stopColor="#2f9be0" />
    </linearGradient></defs>
  );
  const V = "0 0 56 56";
  switch (kind) {
    case "gen": return <svg width={size} height={size} viewBox={V}>{defs}<rect x="16" y="16" width="24" height="24" rx="3" {...s} /><line x1="28" y1="22" x2="28" y2="34" {...s} /><line x1="22" y1="28" x2="34" y2="28" {...s} /></svg>;
    case "tri": return <svg width={size} height={size} viewBox={V}>{defs}<path d="M28 14 L42 40 L14 40 Z" {...s} /></svg>;
    case "hex": return <svg width={size} height={size} viewBox={V}>{defs}<path d="M28 13 L41 21 V35 L28 43 L15 35 V21 Z" {...s} /></svg>;
    case "diamond": return <svg width={size} height={size} viewBox={V}>{defs}<path d="M28 12 L42 28 L28 44 L14 28 Z" {...s} /></svg>;
    case "ring": return <svg width={size} height={size} viewBox={V}>{defs}<circle cx="28" cy="28" r="13" {...s} /><circle cx="28" cy="28" r="4" fill="#6fd2ff" stroke="none" /></svg>;
    case "bars": return <svg width={size} height={size} viewBox={V}>{defs}<line x1="20" y1="38" x2="20" y2="24" {...s} /><line x1="28" y1="38" x2="28" y2="18" {...s} /><line x1="36" y1="38" x2="36" y2="28" {...s} /></svg>;
    case "cross": return <svg width={size} height={size} viewBox={V}>{defs}<line x1="28" y1="16" x2="28" y2="40" {...s} /><line x1="16" y1="28" x2="40" y2="28" {...s} /><circle cx="28" cy="28" r="5" {...s} /></svg>;
    case "rail": return <svg width={size} height={size} viewBox={V}>{defs}<line x1="18" y1="16" x2="18" y2="40" {...s} /><line x1="38" y1="16" x2="38" y2="40" {...s} /><line x1="18" y1="23" x2="38" y2="23" {...s} strokeWidth="2.2" /><line x1="18" y1="33" x2="38" y2="33" {...s} strokeWidth="2.2" /></svg>;
    default: return <svg width={size} height={size} viewBox={V}>{defs}<polyline points="18,20 28,30 38,20" {...s} /><polyline points="18,30 28,40 38,30" {...s} /></svg>;
  }
}

// 해금 폭발 FX — 빛폭발 + 링 + 스파크 + 파편
function UnlockFX() {
  const sparks = Array.from({ length: 10 }).map((_, i) => {
    const a = (i / 10) * Math.PI * 2; const r = 44 + (i % 2) * 14;
    return { dx: `calc(-50% + ${Math.cos(a) * r}px)`, dy: `calc(-50% + ${Math.sin(a) * r}px)` };
  });
  const shards = Array.from({ length: 5 }).map((_, i) => {
    const a = (i / 5) * Math.PI * 2 + 0.5;
    return { dx: `calc(-50% + ${Math.cos(a) * 38}px)`, dy: `calc(-50% + ${Math.sin(a) * 38 + 16}px)` };
  });
  return (
    <div className="fx">
      <div className="fx__flash" />
      <div className="fx__ring" />
      {sparks.map((p, i) => <span key={i} className="spark" style={{ "--dx": p.dx, "--dy": p.dy }} />)}
      {shards.map((p, i) => <span key={"s" + i} className="shard" style={{ "--dx": p.dx, "--dy": p.dy }} />)}
    </div>
  );
}

Object.assign(window, { LockClosed, LockBroken, Facility, UnlockFX });
