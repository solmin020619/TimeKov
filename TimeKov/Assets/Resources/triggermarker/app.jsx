// app.jsx — 튜토리얼 강조 마커 (시간 테마) — 변형 3종 + 펄스
const { useState, useEffect } = React;

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "variant": "clock",
  "pulse": 1.8,
  "color": "#ffce4a",
  "offDir": 35
}/*EDITMODE-END*/;

const VARIANTS = [
  { id: "clock", name: "초침 링", desc: "회전 초침 + 눈금 링. 가장 직관적인 ‘시간’ 마커." },
  { id: "crack", name: "시간 균열", desc: "각진 시간균열 다이아. 임팩트 강한 sci-fi." },
  { id: "glass", name: "모래시계", desc: "모래시계 실루엣. 부드러운 시간 모티프." },
];

function MarkerSVG({ variant, size = 120, thumb = false, dir }) {
  const c = "var(--mk)";
  const sw = { fill: "none", stroke: c, strokeWidth: thumb ? 4 : 3, strokeLinecap: "round", strokeLinejoin: "round" };
  if (variant === "clock") {
    const ticks = Array.from({ length: 12 });
    return (
      <svg width={size} height={size} viewBox="0 0 120 120">
        {!thumb && <circle className="halo" cx="60" cy="60" r="44" fill="none" stroke={c} strokeWidth="2" opacity=".5" />}
        <circle cx="60" cy="60" r="40" fill="rgba(8,11,16,0.5)" stroke={c} strokeWidth="2.5" />
        <g className="spin-slow">
          {ticks.map((_, i) => (
            <line key={i} x1="60" y1="24" x2="60" y2={i % 3 === 0 ? 31 : 28} stroke={c} strokeWidth={i % 3 === 0 ? 3 : 1.8}
              strokeLinecap="round" transform={`rotate(${i * 30} 60 60)`} opacity={i % 3 === 0 ? 1 : .6} />
          ))}
        </g>
        {dir !== undefined ? (
          // 방향 시계침: 목표 방향을 가리킴 (꼬리 포함, 회전 고정). 침은 위가 기본 → dir+90
          <line x1="60" y1="68" x2="60" y2="26" stroke={c} strokeWidth="3.5" strokeLinecap="round"
            style={{ transformOrigin: "60px 60px", transform: `rotate(${dir + 90}deg)`, transition: "transform .4s cubic-bezier(.3,1,.3,1)" }} />
        ) : (
          <line className="second-hand" x1="60" y1="62" x2="60" y2="30" stroke={c} strokeWidth="3" strokeLinecap="round" />
        )}
        <circle cx="60" cy="60" r="4.5" fill={c} />
      </svg>
    );
  }
  if (variant === "crack") {
    return (
      <svg width={size} height={size} viewBox="0 0 120 120">
        {!thumb && <path className="halo" d="M60 14 L96 60 L60 106 L24 60 Z" fill="none" stroke={c} strokeWidth="2" opacity=".5" />}
        <path d="M60 18 L92 60 L60 102 L28 60 Z" fill="rgba(8,11,16,0.5)" {...sw} />
        <g className="spin-rev">
          <path d="M60 30 L60 90 M44 52 L60 60 L74 50" {...sw} strokeWidth="2.4" />
          <path d="M52 70 L60 60 L70 74" {...sw} strokeWidth="2.4" opacity=".7" />
        </g>
        <circle cx="60" cy="60" r="4" fill={c} />
      </svg>
    );
  }
  // glass — 모래시계
  return (
    <svg width={size} height={size} viewBox="0 0 120 120">
      {!thumb && <circle className="halo" cx="60" cy="60" r="42" fill="none" stroke={c} strokeWidth="2" opacity=".4" />}
      <g className="spin-slow" opacity=".5"><circle cx="60" cy="60" r="38" fill="none" stroke={c} strokeWidth="1.4" strokeDasharray="4 8" /></g>
      <path d="M40 32 L80 32 L62 60 L80 88 L40 88 L58 60 Z" fill="rgba(8,11,16,0.5)" {...sw} />
      <line x1="36" y1="32" x2="84" y2="32" {...sw} strokeWidth="3.4" />
      <line x1="36" y1="88" x2="84" y2="88" {...sw} strokeWidth="3.4" />
      <path d="M54 50 L66 50 L60 60 Z" fill={c} stroke="none" />
    </svg>
  );
}

function App() {
  const [t, setTweak] = useTweaks(TWEAK_DEFAULTS);
  const [scale, setScale] = useState(1);
  useEffect(() => {
    const fit = () => setScale(Math.min(window.innerWidth/1920, window.innerHeight/1080));
    fit(); window.addEventListener("resize", fit); return () => window.removeEventListener("resize", fit);
  }, []);

  const style = { transform: `scale(${scale})`, "--mk": t.color, "--pdur": `${t.pulse}s` };

  return (
    <div className="stage">
      <div className="canvas" style={style}>
        <div className="scene-tag">튜토리얼 강조 마커 — 시간 테마 (후순위)</div>
        <div className="world__floor" />

        <div className="callout">
          <h1>튜토리얼 <b>마커</b></h1>
          <p>기존 노란 원 마커 → 시간 모티프(초침/균열/모래시계)로 교체. 목표 위치에 펄스로 강조.</p>
        </div>

        <div className="markerscene">
          <div className="ground" />
          <div className="gpulse" />
          <div className="marker">
            <div className="tip">목표 지점</div>
            <MarkerSVG variant={t.variant} />
          </div>
        </div>

        {/* [B] 화면밖 방향 마커 — 같은 다이얼 + 방향 시계침 (추가) */}
        <div className="offmarker">
          <div className="offmarker__clock"><MarkerSVG variant="clock" size={104} dir={t.offDir} /></div>
          <div className="offmarker__dist">45m</div>
          <div className="offmarker__cap">화면 밖 — 가장자리 방향 시계침</div>
        </div>

        <div className="variants">
          {VARIANTS.map((v) => (
            <div key={v.id} className={`vcard${t.variant === v.id ? " on" : ""}`} onClick={() => setTweak("variant", v.id)}>
              <div className="vcard__top">
                <div className="vthumb"><MarkerSVG variant={v.id} size={48} thumb /></div>
                <div>
                  <div className="vcard__name">{v.name}</div>
                  <div className="vcard__desc">{v.desc}</div>
                </div>
              </div>
            </div>
          ))}
        </div>

        <div className="spec">
          <h3>펄스 / 모션 레퍼런스</h3>
          바닥 링 펄스 <span>{t.pulse}s</span> 확장+페이드 (무한 반복)<br />
          마커 본체 <span>bob</span> 위아래 ±12px / 2.4s<br />
          초침 회전 · 헤일로 확산 동기화<br />
          색 <span>{t.color}</span> (골드/앰버 팔레트)
        </div>
      </div>

      <TweaksPanel>
        <TweakSection label="마커" />
        <TweakRadio label="변형" value={t.variant} options={["clock", "crack", "glass"]} onChange={(v) => setTweak("variant", v)} />
        <TweakSlider label="펄스 속도" value={t.pulse} min={1} max={3} step={0.2} unit="s" onChange={(v) => setTweak("pulse", v)} />
        <TweakSlider label="화면밖 방향침 각도" value={t.offDir} min={0} max={359} step={1} unit="°" onChange={(v) => setTweak("offDir", v)} />
        <TweakColor label="색" value={t.color} options={["#ffce4a", "#ff9a2e", "#5fe6d6", "#c39cff"]} onChange={(v) => setTweak("color", v)} />
      </TweaksPanel>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);
