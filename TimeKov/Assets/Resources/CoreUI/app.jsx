// app.jsx — 코어 강화 UI: 코어→시계→코어 전환 + 깔끔 패널
const { useState, useEffect, useRef, useCallback } = React;

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "rate": 50,
  "haveKits": 3,
  "difficulty": 2,
  "perfectBonus": 45,
  "goodBonus": 22,
  "autoResult": "random"
}/*EDITMODE-END*/;

const C = 2 * Math.PI * 46; // 아크 둘레

// 타이밍 게임 시계판: 성공존(그린) + 퍼펙트존(골드) + 눈금. 바늘은 외부 레이어.
function ClockFace({ zoneHalf, perfectHalf }) {
  const ticks = Array.from({ length: 12 });
  const zoneLen = C * (2 * zoneHalf / 360);
  const perfLen = C * (2 * perfectHalf / 360);
  return (
    <svg className="clocksvg" width="320" height="320" viewBox="0 0 120 120">
      <defs>
        <radialGradient id="cface" cx="50%" cy="42%" r="60%">
          <stop offset="0" stopColor="#10324f" /><stop offset="1" stopColor="#081523" />
        </radialGradient>
      </defs>
      <circle cx="60" cy="60" r="52" fill="url(#cface)" stroke="#5fc4ff" strokeWidth="2.4" />
      <circle cx="60" cy="60" r="52" fill="none" stroke="#aee3ff" strokeWidth="0.6" opacity="0.5" />
      {/* 성공존 트랙 */}
      <circle cx="60" cy="60" r="46" fill="none" stroke="rgba(120,150,180,0.18)" strokeWidth="6" />
      {/* 성공존 (그린) — 상단 중앙 */}
      <circle cx="60" cy="60" r="46" fill="none" stroke="#46e08a" strokeWidth="6" strokeLinecap="round"
        strokeDasharray={`${zoneLen} ${C}`} transform={`rotate(${-90 - zoneHalf} 60 60)`}
        style={{ filter: "drop-shadow(0 0 5px rgba(70,224,138,0.7))" }} />
      {/* 퍼펙트존 (골드) */}
      <circle cx="60" cy="60" r="46" fill="none" stroke="#ffd66b" strokeWidth="6" strokeLinecap="round"
        strokeDasharray={`${perfLen} ${C}`} transform={`rotate(${-90 - perfectHalf} 60 60)`}
        style={{ filter: "drop-shadow(0 0 6px rgba(255,214,107,0.9))" }} />
      {ticks.map((_, i) => {
        const maj = i % 3 === 0;
        return <line key={i} x1="60" y1="17" x2="60" y2={maj ? 24 : 21} stroke={maj ? "#aee3ff" : "#5fc4ff"}
          strokeWidth={maj ? 2.4 : 1.3} strokeLinecap="round" opacity={maj ? 0.9 : 0.5} transform={`rotate(${i*30} 60 60)`} />;
      })}
      <circle cx="60" cy="60" r="5.5" fill="#0b1626" stroke="#5fc4ff" strokeWidth="2" />
      <circle cx="60" cy="60" r="2" fill="#aee3ff" />
    </svg>
  );
}

// 회전 바늘 (외부 레이어, ref로 구동)
const ClockHand = React.forwardRef(function ClockHand(props, ref) {
  return (
    <div className="handlayer" ref={ref}>
      <svg width="320" height="320" viewBox="0 0 120 120">
        <defs>
          <linearGradient id="chand" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stopColor="#ffffff" /><stop offset="1" stopColor="#5fc4ff" />
          </linearGradient>
        </defs>
        <polygon points="60,14 63.4,40 56.6,40" fill="#fff" style={{ filter: "drop-shadow(0 0 4px rgba(143,224,255,0.9))" }} />
        <line x1="60" y1="62" x2="60" y2="40" stroke="url(#chand)" strokeWidth="3.6" strokeLinecap="round" />
        <line x1="60" y1="60" x2="60" y2="70" stroke="#2f9be0" strokeWidth="3" strokeLinecap="round" />
      </svg>
    </div>
  );
});

// 시간(=체력)만 강화. 레벨당 +30s.
const TIME_BASE = 100, TIME_STEP = 30;

function App() {
  const [t, setTweak] = useTweaks(TWEAK_DEFAULTS);
  const [scale, setScale] = useState(1);
  const [level, setLevel] = useState(0);
  const [phase, setPhase] = useState("idle"); // idle | spin | judge | result
  const [result, setResult] = useState(null); // success | fail
  const [tier, setTier] = useState(null);      // perfect | good | miss
  const [bonus, setBonus] = useState(0);
  const [bump, setBump] = useState(0);
  const handRef = useRef(null);
  const angleRef = useRef(0);
  const rafRef = useRef(0);
  const startRef = useRef(0);
  const timers = useRef([]);
  const needKits = 3;

  // 난이도: 바늘 1회전 주기(ms) · 성공존 반각 · 퍼펙트 반각
  const period = 1400 - (t.difficulty - 1) * 220; // 난이도↑ = 빠름
  const zoneHalf = 30 - (t.difficulty - 1) * 4;
  const perfectHalf = 10 - (t.difficulty - 1) * 1.2;

  useEffect(() => {
    const fit = () => setScale(Math.min(window.innerWidth/1920, window.innerHeight/1080));
    fit(); window.addEventListener("resize", fit); return () => window.removeEventListener("resize", fit);
  }, []);
  useEffect(() => () => { cancelAnimationFrame(rafRef.current); timers.current.forEach((x) => { clearTimeout(x); }); }, []);

  const enough = t.haveKits >= needKits;
  const canStart = phase === "idle" && enough && level < 10;

  const startSpin = useCallback(() => {
    if (!canStart) return;
    setPhase("spin"); setResult(null); setTier(null); setBonus(0);
    startRef.current = performance.now();
    const loop = (now) => {
      const ang = (((now - startRef.current) / period) * 360) % 360;
      angleRef.current = ang;
      if (handRef.current) handRef.current.style.transform = `rotate(${ang}deg)`;
      rafRef.current = requestAnimationFrame(loop);
    };
    rafRef.current = requestAnimationFrame(loop);
    // 안전장치: 6초 내 미정지 시 자동 미스
    timers.current.push(setTimeout(() => { if (handRef.current) stopSpin(true); }, 6000));
  }, [canStart, period]);

  const stopSpin = useCallback((auto) => {
    cancelAnimationFrame(rafRef.current);
    // 상단(0deg) 기준 최소 각도 거리
    let a = angleRef.current % 360; if (a > 180) a -= 360; if (a < -180) a += 360;
    const dist = Math.abs(a);
    let tr, bn;
    if (auto || dist > zoneHalf) { tr = "miss"; bn = 0; }
    else if (dist <= perfectHalf) { tr = "perfect"; bn = t.perfectBonus; }
    else { // 성공존: 가장자리로 갈수록 보너스 감소
      const k = 1 - (dist - perfectHalf) / (zoneHalf - perfectHalf);
      tr = "good"; bn = Math.round(t.goodBonus * (0.4 + 0.6 * k));
    }
    setTier(tr); setBonus(bn); setPhase("judge");
    const eff = Math.max(0, Math.min(100, t.rate + bn));
    timers.current.push(setTimeout(() => {
      const ok = t.autoResult === "success" ? true : t.autoResult === "fail" ? false : Math.random() * 100 < eff;
      setResult(ok ? "success" : "fail"); setPhase("result");
      if (ok) { setLevel((l) => Math.min(10, l + 1)); setBump((b) => b + 1); }
      timers.current.push(setTimeout(() => { setPhase("idle"); setResult(null); setTier(null); setBonus(0); }, 1600));
    }, 1100));
  }, [zoneHalf, perfectHalf, t.rate, t.perfectBonus, t.goodBonus, t.autoResult]);

  // 스페이스/클릭으로 정지
  useEffect(() => {
    const onKey = (e) => {
      if (e.code === "Space") { e.preventDefault(); if (phase === "spin") stopSpin(false); else if (phase === "idle") startSpin(); }
    };
    window.addEventListener("keydown", onKey); return () => window.removeEventListener("keydown", onKey);
  }, [phase, startSpin, stopSpin]);

  const toClock = phase !== "idle";
  const effRate = Math.max(0, Math.min(100, t.rate + (phase === "spin" ? 0 : bonus)));
  const curTime = TIME_BASE + level * TIME_STEP;
  const afterTime = curTime + TIME_STEP;

  return (
    <div className="stage">
      <div className="canvas" style={{ transform: `scale(${scale})` }}>
        <div className="bg"><div className="bg__scrim" /></div>

        <div className="panel">
          <div className="panel__topline" />
          <div className="head">
            <div className="closebtn" title="닫기">
              <svg width="20" height="20" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round"><path d="M3 3 L13 13 M13 3 L3 13"/></svg>
            </div>
            <h1>코어 <span className="t">강화</span></h1>
            <div className={`lvbadge${bump ? " bump" : ""}`} key={bump}>
              <span className="lvbadge__cap">LEVEL</span>
              <span className="lvbadge__num"><b>{level}</b><i>/ 10</i></span>
            </div>
          </div>

          <div className="body">
            {/* 현재 — 체력 */}
            <div className="statcol">
              <div className="statcol__head">현재</div>
              <div className="stats">
                <div className="bigstat">
                  <span className="bigstat__lbl">체력</span>
                  <span className="bigstat__v">{curTime}<i>s</i></span>
                </div>
              </div>
            </div>

            {/* 코어 스테이지 */}
            <div className="corewrap">
              <div className="corering" />
              <div className="coreframe" />
              <div className={`coreaura${tier === "perfect" ? " perfect" : tier === "good" ? " good" : ""}`} />
              <div className="corestage">
                <img className={`coreimg${phase === "idle" ? " idle" : ""}`} src="core.png" alt="core"
                  style={{ opacity: toClock ? 0 : 1, transform: toClock ? "scale(.8) rotate(18deg)" : undefined }} />
                <div className="clock" style={{ opacity: toClock ? 1 : 0, transform: toClock ? "scale(1) rotate(0)" : "scale(.86) rotate(-20deg)" }}>
                  <ClockFace zoneHalf={zoneHalf} perfectHalf={perfectHalf} />
                  <ClockHand ref={handRef} />
                </div>
                {/* 타이밍 판정 칩 */}
                <div className={`tchip ${tier || ""}`}
                  style={{ opacity: (phase === "judge" || phase === "result") && tier ? 1 : 0,
                           transform: (phase === "judge" || phase === "result") && tier ? "translate(-50%, 0)" : "translate(-50%, -6px)" }}>
                  {tier === "perfect" ? "PERFECT" : tier === "good" ? "GOOD" : tier === "miss" ? "MISS" : ""}
                  {tier && tier !== "miss" && <span className="tchip__b">+{bonus}%</span>}
                </div>
                {/* 결과 */}
                <div className={`result ${result || ""}`} style={{ opacity: result ? 1 : 0, transform: result ? "translateX(-50%) translateY(0)" : "translateX(-50%) translateY(-8px)" }}>{result === "success" ? "강화 성공" : result === "fail" ? "강화 실패" : ""}</div>
              </div>
              {phase === "spin" && <div className="spinhint">멈춰서 <b>성공존</b>에 맞추세요!</div>}
            </div>

            {/* 강화 후 — 체력 */}
            <div className="statcol">
              <div className="statcol__head">강화 후</div>
              <div className="stats after">
                <div className="bigstat">
                  <span className="bigstat__lbl">체력</span>
                  <span className="bigstat__v up">{afterTime}<i>s</i></span>
                  <span className="bigstat__d">+{TIME_STEP}s ↑</span>
                </div>
              </div>
            </div>
          </div>

          {/* 하단 */}
          <div className="foot">
            <div className="probpanel">
              <div className="probpanel__row">
                <div className="matchip">
                  <span className="matchip__icon">
                    <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round"><path d="M12 3 L20 7.5 V16.5 L12 21 L4 16.5 V7.5 Z"/><path d="M12 3 V21 M4 7.5 L20 16.5 M20 7.5 L4 16.5" strokeWidth="1" opacity=".4"/></svg>
                  </span>
                  <span className="matchip__txt">
                    <span className="matchip__name">코어 키트 I</span>
                    <span className="matchip__cnt"><em>보유</em><b className={enough ? "ok" : "lack"}>{t.haveKits}</b><s>/</s><em>필요</em><b className="need">{needKits}</b></span>
                  </span>
                </div>
                <div className="probval">
                  <span className="probval__lbl">성공 확률</span>
                  <span className="probval__num">{effRate}<i>%</i>{bonus > 0 && phase !== "idle" && phase !== "spin" && <em>+{bonus}</em>}</span>
                </div>
              </div>
              <div className="gauge">
                <div className="gauge__fill" style={{ width: `${t.rate}%` }} />
                <div className="gauge__bonus" style={{ left: `${t.rate}%`, width: `${Math.max(0, effRate - t.rate)}%` }} />
              </div>
            </div>

            <div className="actions">
              {phase === "spin" ? (
                <button className="btn-enh stop" onClick={() => stopSpin(false)}>정지!</button>
              ) : (
                <button className="btn-enh" onClick={startSpin} disabled={!canStart}>
                  {phase === "judge" || phase === "result" ? "강화 중…" : level >= 10 ? "최대 레벨" : "강화 시작"}
                </button>
              )}
            </div>
          </div>

          <div className="hint">{"\u00a0"}</div>
        </div>
      </div>

      <TweaksPanel>
        <TweakSection label="강화 타이밍" />
        <TweakSlider label="기본 성공률" value={t.rate} min={0} max={100} step={5} unit="%" onChange={(v) => setTweak("rate", v)} />
        <TweakSlider label="난이도(바늘 속도)" value={t.difficulty} min={1} max={4} step={1} onChange={(v) => setTweak("difficulty", v)} />
        <TweakSlider label="퍼펙 보너스" value={t.perfectBonus} min={10} max={50} step={5} unit="%" onChange={(v) => setTweak("perfectBonus", v)} />
        <TweakSlider label="성공존 보너스" value={t.goodBonus} min={5} max={40} step={1} unit="%" onChange={(v) => setTweak("goodBonus", v)} />
        <TweakSection label="자원 / 데모" />
        <TweakSlider label="코어 키트 보유" value={t.haveKits} min={0} max={9} step={1} onChange={(v) => setTweak("haveKits", v)} />
        <TweakRadio label="판정 결과" value={t.autoResult} options={["random", "success", "fail"]} onChange={(v) => setTweak("autoResult", v)} />
        <TweakButton label="레벨 리셋" onClick={() => setLevel(0)} />
      </TweaksPanel>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);
