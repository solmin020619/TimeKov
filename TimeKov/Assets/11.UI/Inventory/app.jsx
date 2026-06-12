// app.jsx — state, layouts, interactions, tweaks. Renders into #root.
// Depends on: data.jsx, icons.jsx, components.jsx, tweaks-panel.jsx

const { useState, useEffect, useRef, useMemo } = React;

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/ {
  gradeVariant: '언더라인',
  glowStyle: '소프트',
  glow: 72,
  blur: 6,
  accent: '#5fc4ff',
} /*EDITMODE-END*/;

const GV_MAP = { '언더라인': 'underline', '코너': 'corner', '풀테두리': 'fulledge' };
const GL_MAP = { '소프트': 'soft', '링': 'ring', '엣지': 'edge' };
const CAP = { bag: 35, warehouse: 50 };
const COLS = { bag: 7, warehouse: 10 };

function hexToRgb(h) {
  const n = parseInt(h.slice(1), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}
function lighten(h, amt) {
  const [r, g, b] = hexToRgb(h);
  const f = (c) => Math.round(c + (255 - c) * amt);
  return `rgb(${f(r)}, ${f(g)}, ${f(b)})`;
}

const catOrder = Object.fromEntries(CATEGORIES.map((c, i) => [c.key, i]));

function derive(items, filter, sortKey, dir) {
  const acq = new Map(items.map((it, i) => [it.id, i]));
  let list = items.filter((it) => filter === 'all' || it.cat === filter);
  const cmp = {
    recent: (a, b) => acq.get(a.id) - acq.get(b.id),
    name: (a, b) => a.name.localeCompare(b.name, 'ko'),
    grade: (a, b) => GRADES[a.grade].order - GRADES[b.grade].order || b.qty - a.qty,
    qty: (a, b) => a.qty - b.qty,
    cat: (a, b) => catOrder[a.cat] - catOrder[b.cat] || GRADES[b.grade].order - GRADES[a.grade].order,
  }[sortKey];
  list = list.slice().sort((a, b) => cmp(a, b) || acq.get(a.id) - acq.get(b.id));
  if (dir === 'desc') list.reverse();
  return list;
}

function InvPanel({
  id, title, items, seed, ui, tweaks,
  selectedId, hoveredId, onSelect, onHover,
  onFilter, onSort, onDir, actions,
}) {
  const cols = COLS[id];
  const cap = CAP[id];
  const slots = useMemo(() => {
    const list = derive(items, ui.filter, ui.sort, ui.dir).slice(0, cap);
    const arr = list.slice();
    while (arr.length < cap) arr.push(null);
    return arr;
  }, [items, ui.filter, ui.sort, ui.dir, cap]);

  const width = cols * 64 + (cols - 1) * 9 + 52;

  return (
    <div className="panel" style={{ width }}>
      <CornerBrackets />
      <PanelHeader title={title} count={items.length} cap={cap} seed={seed} />
      <CategoryRow active={ui.filter} onPick={onFilter} />
      <div className="p-divider" />
      <Grid
        slots={slots}
        cols={cols}
        gradeVariant={GV_MAP[tweaks.gradeVariant]}
        glowStyle={GL_MAP[tweaks.glowStyle]}
        selectedId={selectedId}
        hoveredId={hoveredId}
        onSelect={(iid) => onSelect(id, iid)}
        onHover={(iid) => onHover(id, iid)}
      />
      <div className="bottom-bar">{actions}</div>
    </div>
  );
}

function App() {
  const [t, setTweak] = useTweaks(TWEAK_DEFAULTS);
  const [layout, setLayoutRaw] = useState(() => localStorage.getItem('invui_layout') || 'single');
  const [data, setData] = useState(makeInitialItems);
  const [ui, setUi] = useState({
    bag: { filter: 'all', sort: 'recent', dir: 'asc' },
    warehouse: { filter: 'all', sort: 'recent', dir: 'asc' },
  });
  const [sel, setSel] = useState(null); // {panel,id}
  const [hover, setHover] = useState(null); // {panel,id}
  const [gen, setGen] = useState(0);
  const [intro, setIntro] = useState(true);
  const [toast, setToast] = useState(null);
  const toastRef = useRef(0);

  const setLayout = (l) => {
    setLayoutRaw(l);
    localStorage.setItem('invui_layout', l);
    setSel(null);
    setGen((g) => g + 1);
  };

  const flash = (msg) => {
    setToast(msg);
    clearTimeout(toastRef.current);
    toastRef.current = setTimeout(() => setToast(null), 1700);
  };

  // opening fade — timer-bounded so resting state is always visible (never stuck hidden)
  useEffect(() => {
    setIntro(true);
    const id = setTimeout(() => setIntro(false), 480);
    return () => clearTimeout(id);
  }, [gen]);

  // keyboard: TAB = 단독 가방, F = 창고+가방, Esc = 선택 해제
  useEffect(() => {
    const onKey = (e) => {
      if (e.key === 'Tab') { e.preventDefault(); setLayout('single'); }
      else if (e.key === 'f' || e.key === 'F' || e.key === 'ㄹ') { setLayout('dual'); }
      else if (e.key === 'Escape') { setSel(null); }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const setPanelUi = (panel, patch) =>
    setUi((u) => ({ ...u, [panel]: { ...u[panel], ...patch } }));

  const onSelect = (panel, id) =>
    setSel((s) => (s && s.panel === panel && s.id === id ? null : { panel, id }));
  const onHover = (panel, id) => setHover(id ? { panel, id } : null);

  const doCompact = (panel) => {
    setPanelUi(panel, { sort: 'cat', dir: 'asc' });
    setData((d) => ({
      ...d,
      [panel]: d[panel].map((it) => (it.isNew ? { ...it, isNew: false } : it)),
    }));
    flash('정리 완료 — 분류순으로 자동 정렬했습니다');
  };

  const doTrash = (panel) => {
    if (!sel || sel.panel !== panel) { flash('버릴 아이템을 먼저 선택하세요'); return; }
    const item = data[panel].find((x) => x.id === sel.id);
    setData((d) => ({ ...d, [panel]: d[panel].filter((x) => x.id !== sel.id) }));
    setSel(null);
    flash((item ? item.name : '아이템') + ' 을(를) 버렸습니다');
  };

  const moveAll = (from, to) => {
    const filter = ui[from].filter;
    const src = data[from];
    const moving = src.filter((it) => filter === 'all' || it.cat === filter);
    if (moving.length === 0) { flash('이동할 아이템이 없습니다'); return; }
    const keep = src.filter((it) => !(filter === 'all' || it.cat === filter));
    const dst = data[to].map((x) => ({ ...x }));
    let movedCount = 0, blocked = 0;
    for (const it of moving) {
      const m = dst.find((x) => x.name === it.name && x.grade === it.grade);
      if (m) { m.qty += it.qty; movedCount++; }
      else if (dst.length < CAP[to]) { dst.push({ ...it, isNew: false }); movedCount++; }
      else { keep.push(it); blocked++; }
    }
    setData((d) => ({ ...d, [from]: keep, [to]: dst }));
    setSel(null);
    const dir = from === 'bag' ? '창고로 보관' : '가방으로 가져옴';
    flash(`${movedCount}종 ${dir}${blocked ? ` · ${blocked}종 공간부족` : ''}`);
  };

  // CSS vars from tweaks
  const accent = t.accent;
  const rootVars = {
    '--cyan': accent,
    '--cyan-bright': lighten(accent, 0.45),
    '--cyan-rgb': hexToRgb(accent).join(', '),
    '--glow': (t.glow / 100).toFixed(3),
    '--blur': t.blur + 'px',
  };

  const sortCtl = (panel) => (
    <SortControl
      sortKey={ui[panel].sort}
      dir={ui[panel].dir}
      onSort={(k) => setPanelUi(panel, { sort: k })}
      onDir={() => setPanelUi(panel, { dir: ui[panel].dir === 'asc' ? 'desc' : 'asc' })}
    />
  );

  const panelProps = (panel) => ({
    id: panel,
    items: data[panel],
    ui: ui[panel],
    tweaks: t,
    selectedId: sel && sel.panel === panel ? sel.id : null,
    hoveredId: hover && hover.panel === panel ? hover.id : null,
    onSelect, onHover,
    onFilter: (k) => setPanelUi(panel, { filter: k }),
  });

  return (
    <div className="app-root" style={rootVars}>
      <div className="ambiance" />

      <div className="topbar">
        <div className="seg">
          <button className={'seg-btn' + (layout === 'single' ? ' on' : '')} onClick={() => setLayout('single')}>
            단독 가방 <kbd>TAB</kbd>
          </button>
          <button className={'seg-btn' + (layout === 'dual' ? ' on' : '')} onClick={() => setLayout('dual')}>
            창고 + 가방 <kbd>F</kbd>
          </button>
        </div>
      </div>

      <div className="stage-wrap">
        <div className={'fade-root' + (intro ? ' intro' : '')} key={gen}>
          {layout === 'single' ? (
            <div className="layout-single">
              <button className="close-x" title="닫기 (Esc)" onClick={() => setGen((g) => g + 1)}>
                <Icon name="close" size={20} sw={1.8} />
              </button>
              <InvPanel
                {...panelProps('bag')}
                title="가방"
                seed={3}
                actions={
                  <React.Fragment>
                    {sortCtl('bag')}
                    <IconAction icon="compact" label="정리" onClick={() => doCompact('bag')} />
                    <IconAction icon="trash" label="휴지통" danger onClick={() => doTrash('bag')} />
                    <span className="bb-spacer" />
                    <IconAction icon="store" label="일괄보관" primary onClick={() => moveAll('bag', 'warehouse')} />
                  </React.Fragment>
                }
              />
            </div>
          ) : (
            <div className="layout-dual">
              <button className="close-x" title="닫기 (Esc)" onClick={() => setGen((g) => g + 1)}>
                <Icon name="close" size={20} sw={1.8} />
              </button>
              <InvPanel
                {...panelProps('warehouse')}
                title="창고"
                seed={11}
                actions={
                  <React.Fragment>
                    {sortCtl('warehouse')}
                    <IconAction icon="compact" label="정리" onClick={() => doCompact('warehouse')} />
                    <IconAction icon="trash" label="휴지통" danger onClick={() => doTrash('warehouse')} />
                  </React.Fragment>
                }
              />
              <div className="dual-divider" />
              <InvPanel
                {...panelProps('bag')}
                title="가방"
                seed={3}
                actions={
                  <React.Fragment>
                    <IconAction icon="store" label="일괄보관" onClick={() => moveAll('bag', 'warehouse')} />
                    <span className="bb-spacer" />
                    <IconAction icon="retrieve" label="일괄가져오기" primary onClick={() => moveAll('warehouse', 'bag')} />
                  </React.Fragment>
                }
              />
            </div>
          )}
        </div>
      </div>

      <div className={'toast' + (toast ? ' show' : '')}>{toast}</div>

      <TweaksPanel>
        <TweakSection label="등급 바 표현" />
        <TweakRadio label="형태" value={t.gradeVariant} options={['언더라인', '코너', '풀테두리']} onChange={(v) => setTweak('gradeVariant', v)} />
        <TweakSection label="슬롯 발광" />
        <TweakRadio label="스타일" value={t.glowStyle} options={['소프트', '링', '엣지']} onChange={(v) => setTweak('glowStyle', v)} />
        <TweakSlider label="발광 강도" value={t.glow} min={20} max={100} unit="%" onChange={(v) => setTweak('glow', v)} />
        <TweakSection label="패널" />
        <TweakSlider label="블러 강도" value={t.blur} min={0} max={14} unit="px" onChange={(v) => setTweak('blur', v)} />
        <TweakColor label="시안 포인트" value={t.accent} options={['#5fc4ff', '#5ad0e6', '#8ab4ff']} onChange={(v) => setTweak('accent', v)} />
      </TweaksPanel>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
