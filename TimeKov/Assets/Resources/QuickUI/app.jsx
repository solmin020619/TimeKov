// app.jsx — 설비 퀵슬롯 바 (블루-입체): 1~8 + 구분선 + 레일 E, 강한 해금 연출
const { useState, useEffect, useRef, useCallback } = React;

const KINDS = ["gen", "tri", "hex", "diamond", "ring", "bars", "cross", "chev"];
const makeSlots = () =>
  Array.from({ length: 8 }).map((_, i) => ({
    id: i, num: i + 1, kind: KINDS[i],
    status: i < 4 ? "active" : "locked",
    phase: "idle", lock: "closed", fx: false, slam: false,
  }));

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "slot": 104,
  "showSprites": true
}/*EDITMODE-END*/;

function QSlot({ s, slot, selected, isRail, onSelect }) {
  const cls = ["qslot"];
  if (isRail) cls.push("rail");
  if (s.status === "active") cls.push("active");
  else if (s.status === "locked" || s.status === "pending") cls.push("locked");
  if (selected) cls.push("selected");
  if (s.phase === "shake") cls.push("shake");
  if (s.phase === "ignite") cls.push("ignite");
  const showLock = s.status !== "active";
  return (
    <div className={cls.join(" ")} style={{ "--slot": slot + "px" }} onClick={() => s.status === "active" && onSelect()}>
      <div className="qslot__inner">
        {s.status === "active" && (
          <div className={`qicon${s.slam ? " slam" : ""}`}><Facility kind={s.kind} size={slot * 0.46} /></div>
        )}
        {showLock && (
          <div className="qlock">
            <span className={s.lock === "broken" ? "broken" : ""}><LockClosed size={slot * 0.42} /></span>
            {s.lock === "broken" && <span><LockBroken size={slot * 0.42} /></span>}
          </div>
        )}
        <div className="qtick"><span /><span /><span /><span /><span /></div>
      </div>
      <span className="bracket tl" /><span className="bracket tr" /><span className="bracket bl" /><span className="bracket br" />
      <div className="qnum">{isRail ? "E" : s.num}</div>
      {s.fx && <UnlockFX />}
    </div>
  );
}

function App() {
  const [t, setTweak] = useTweaks(TWEAK_DEFAULTS);
  const [scale, setScale] = useState(1);
  const [slots, setSlots] = useState(makeSlots);
  const [rail, setRail] = useState({ id: "rail", kind: "rail", status: "active", phase: "idle", lock: "closed", fx: false, slam: false });
  const [selected, setSelected] = useState(0);
  const timers = useRef([]);

  useEffect(() => {
    const fit = () => setScale(Math.min(window.innerWidth/1920, window.innerHeight/1080));
    fit(); window.addEventListener("resize", fit); return () => window.removeEventListener("resize", fit);
  }, []);
  useEffect(() => () => timers.current.forEach(clearTimeout), []);

  const patch = useCallback((id, p) => setSlots((prev) => prev.map((s) => (s.id === id ? { ...s, ...p } : s))), []);

  const lockedCount = slots.filter((s) => s.status === "locked").length;
  const pendingCount = slots.filter((s) => s.status === "pending").length;

  const unlockNext = () => setSlots((prev) => {
    const idx = prev.findIndex((s) => s.status === "locked");
    if (idx < 0) return prev;
    return prev.map((s, i) => (i === idx ? { ...s, status: "pending" } : s));
  });

  // B 건설모드 → 강한 해금 시퀀스
  const enterBuild = () => {
    const pend = slots.filter((s) => s.status === "pending");
    pend.forEach((s, k) => {
      const b = k * 260;
      timers.current.push(setTimeout(() => patch(s.id, { phase: "shake" }), b));
      timers.current.push(setTimeout(() => patch(s.id, { phase: "ignite", fx: true, lock: "broken" }), b + 400));
      timers.current.push(setTimeout(() => patch(s.id, { status: "active", slam: true }), b + 660));
      timers.current.push(setTimeout(() => patch(s.id, { fx: false, phase: "idle" }), b + 1000));
      timers.current.push(setTimeout(() => patch(s.id, { slam: false, lock: "closed" }), b + 1240));
    });
  };

  const reset = () => { timers.current.forEach(clearTimeout); timers.current = []; setSlots(makeSlots()); setSelected(0); };

  useEffect(() => {
    const onKey = (e) => {
      const k = e.key.toLowerCase();
      if (k === "b") enterBuild();
      if (k === "u") unlockNext();
      if (k === "e") setSelected("rail");
      const n = parseInt(e.key, 10);
      if (n >= 1 && n <= 8) { const s = slots[n-1]; if (s && s.status === "active") setSelected(s.id); }
    };
    window.addEventListener("keydown", onKey); return () => window.removeEventListener("keydown", onKey);
  });

  const grassPatch = (x, y, c, w) => ({ left: x, top: y, width: w, height: w, background: c });

  return (
    <div className="stage">
      <div className="canvas" style={{ transform: `scale(${scale})` }}>
        <div className="grass">
          <span className="grass__patch" style={grassPatch("12%","30%","#a6da78",260)} />
          <span className="grass__patch" style={grassPatch("70%","20%","#5a9d40",320)} />
          <span className="grass__patch" style={grassPatch("46%","62%","#7cc057",300)} />
          <div className="grass__grid" />
          <div className="grass__tag">건설 모드 (탑뷰) — 밝은 잔디 위 대비 확인</div>
          <div className="ghost"><div className="ghost__tag">설비 배치 미리보기</div></div>
        </div>

        <div className="callout">
          <h1>설비 퀵슬롯 <b>바</b></h1>
          <p>1~8 설비 + 구분선 + 레일 E. 잠김=회색+자물쇠 / 해금=시안 프레임 / 선택=글로우. B 진입 시 강한 해금 연출.</p>
        </div>

        {/* 퀵슬롯 바 */}
        <div className="qbar-wrap">
          <div className="qbar">
            <div className="qbar__inner">
              {slots.map((s) => (
                <QSlot key={s.id} s={s} slot={t.slot} selected={selected === s.id} onSelect={() => setSelected(s.id)} />
              ))}
              <div className="divider" />
              <QSlot s={rail} slot={t.slot} isRail selected={selected === "rail"} onSelect={() => setSelected("rail")} />
            </div>
          </div>
        </div>

        {/* 데모 컨트롤 */}
        <div className="demo">
          <div className="demo__hint">데모: 숫자 1~8 / E 선택 · U 해금 · B 건설모드(연출)</div>
          <button className="btn" onClick={unlockNext} disabled={lockedCount === 0}><span className="kc">U</span> 설비 해금 — 다음 ({lockedCount})</button>
          <button className="btn primary" onClick={enterBuild} disabled={pendingCount === 0}><span className="kc">B</span> 건설모드 진입 — 해금 연출 ({pendingCount})</button>
          <button className="btn" onClick={reset}>↺ 리셋</button>
        </div>

        {/* 스프라이트 추출 */}
        {t.showSprites && (
          <div className="sprites">
            <div className="sprites__title">스프라이트 추출 요소</div>
            <div className="spritecard">
              <div className="checker"><div className="qslot active selected" style={{ "--slot": "62px" }}><div className="qslot__inner"><Facility kind="hex" size={28} /></div><span className="bracket tl" /><span className="bracket tr" /><span className="bracket bl" /><span className="bracket br" /><div className="qnum" style={{ minWidth: 16, height: 16, fontSize: 10 }}>3</div></div></div>
              <div className="spritecard__meta"><b>해금/선택 슬롯</b><span>프레임 + 코너 브래킷</span></div>
            </div>
            <div className="spritecard">
              <div className="checker"><div className="qslot locked" style={{ "--slot": "62px" }}><div className="qslot__inner"><div className="qlock"><LockClosed size={26} /></div></div><span className="bracket tl" /><span className="bracket tr" /><span className="bracket bl" /><span className="bracket br" /></div></div>
              <div className="spritecard__meta"><b>잠김 슬롯</b><span>회색 + 자물쇠(닫힘)</span></div>
            </div>
            <div className="spritecard">
              <div className="checker" style={{ display: "flex", gap: 8 }}><LockClosed size={30} /><LockBroken size={30} /></div>
              <div className="spritecard__meta"><b>자물쇠 2종</b><span>닫힘 / 깨짐</span></div>
            </div>
            <div className="spritecard">
              <div className="checker" style={{ position: "relative", width: 50, height: 50 }}>
                <div style={{ position: "absolute", inset: 0 }}><UnlockFX /></div>
              </div>
              <div className="spritecard__meta"><b>해금 FX</b><span>빛폭발 + 스파크/파편</span></div>
            </div>
          </div>
        )}
      </div>

      <TweaksPanel>
        <TweakSection label="퀵슬롯" />
        <TweakSlider label="슬롯 크기" value={t.slot} min={88} max={120} step={4} unit="px" onChange={(v) => setTweak("slot", v)} />
        <TweakToggle label="스프라이트 카드" value={t.showSprites} onChange={(v) => setTweak("showSprites", v)} />
        <TweakSection label="해금 연출 재생" />
        <TweakButton label="설비 해금 (다음)" onClick={unlockNext} />
        <TweakButton label="B 건설모드 — 연출" onClick={enterBuild} />
        <TweakButton label="리셋" onClick={reset} />
      </TweaksPanel>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);
