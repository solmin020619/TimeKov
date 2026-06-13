// app.jsx — state, layouts, interactions, tweaks. Renders into #root.
// Depends on: data.jsx, icons.jsx, components.jsx, tweaks-panel.jsx

const { useState, useEffect, useRef, useMemo } = React;

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/ {
  gradeVariant: '언더라인',
  glowStyle: '소프트',
  glow: 64,
  blur: 26,
  chroma: '절제',
  scene: '실사',
  accent: '#4a525c',
} /*EDITMODE-END*/;

const GV_MAP = { '언더라인': 'underline', '코너': 'corner', '풀테두리': 'fulledge' };
const GL_MAP = { '소프트': 'soft', '링': 'ring', '엣지': 'edge' };
const CAP = { bag: 35, warehouse: 56 };
const COLS = { bag: 5, warehouse: 8 };

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

const colWidth = (id) => COLS[id] * 90 + (COLS[id] - 1) * 11 + 22;

function useSlots(items, ui, cap) {
  return useMemo(() => {
    const list = derive(items, ui.filter, ui.sort, ui.dir).slice(0, cap);
    const arr = list.slice();
    while (arr.length < cap) arr.push(null);
    return arr;
  }, [items, ui.filter, ui.sort, ui.dir, cap]);
}

// Bare column used inside the merged (창고+가방) frame — no frame/header/footer of its own.
function MergedColumn({
  id, items, ui, tweaks, selectedId, hoveredId, onSelect, onHover, onFilter,
}) {
  const slots = useSlots(items, ui, CAP[id]);
  return (
    <div className={'mcol mcol-' + id} style={{ width: colWidth(id) }}>
      <CategoryRow active={ui.filter} onPick={onFilter} />
      <div className="p-divider" />
      <Grid
        slots={slots}
        cols={COLS[id]}
        gradeVariant={GV_MAP[tweaks.gradeVariant]}
        glowStyle={GL_MAP[tweaks.glowStyle]}
        selectedId={selectedId}
        hoveredId={hoveredId}
        onSelect={(iid) => onSelect(id, iid)}
        onHover={(iid) => onHover(id, iid)}
      />
    </div>
  );
}

function InvPanel({
  id, title, items, seed, ui, tweaks,
  selectedId, hoveredId, onSelect, onHover,
  onFilter, onSort, onDir, actions, noCategory, onClose,
}) {
  const cols = COLS[id];
  const cap = CAP[id];
  const slots = useSlots(items, ui, cap);

  const width = cols * 96 + (cols - 1) * 14 + 24 + 48;

  return (
    <div className={'panel panel-' + id} style={{ width }}>
      <CornerBrackets />
      <PanelHeader title={title} onClose={onClose} />
      <div className="p-body">
        <div className="cap-row"><CapacityGauge count={items.length} cap={cap} /></div>
        {!noCategory && <CategoryRow active={ui.filter} onPick={onFilter} />}
        {!noCategory && <div className="p-divider" />}
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
      </div>
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
  const CHROMA = { '절제': 'calm', '상시': 'lit' };
  const SCENE = { '실사': 'game', '협곡': 'canyon', '야간': 'night', '끄기': 'off' };

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
    <div className={'app-root chroma-' + CHROMA[t.chroma]} style={rootVars}>
      <div className="world-bg" data-scene={SCENE[t.scene]} />
      <div className="ambiance" />

      <div className="brand">
        <Icon name="clock" size={20} sw={1.7} />
        <span className="brand-t">TIME<span className="brand-k">KOV</span></span>
      </div>

      <div className="blur-ctl">
        <span className="bc-label">블러 <b>{t.blur}</b>px</span>
        <input
          type="range"
          className="bc-range"
          min="0"
          max="40"
          value={t.blur}
          onChange={(e) => setTweak('blur', +e.target.value)}
        />
      </div>

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
              <InvPanel
                {...panelProps('bag')}
                title="가방"
                seed={3}
                noCategory
                onClose={() => setGen((g) => g + 1)}
                actions={
                  <React.Fragment>
                    <span className="bb-spacer" />
                    <IconAction label="정렬" onClick={() => doCompact('bag')} />
                  </React.Fragment>
                }
              />
            </div>
          ) : (
            <div className="merged" style={{ '--wh-w': colWidth('warehouse') + 'px', '--bag-w': colWidth('bag') + 'px' }}>
              <CornerBrackets />

              <div className="merged-head">
                <div className="mhcol mhcol-wh">
                  <PanelHeader title="창고" count={data.warehouse.length} cap={CAP.warehouse} seed={11} />
                </div>
                <div className="vsep vsep-head" />
                <div className="mhcol mhcol-bag">
                  <PanelHeader title="가방" count={data.bag.length} cap={CAP.bag} seed={3} />
                </div>
                <button className="close-x2" title="닫기 (Esc)" onClick={() => setGen((g) => g + 1)}>
                  <Icon name="close" size={20} sw={1.8} />
                </button>
              </div>

              <div className="merged-body">
                <MergedColumn {...panelProps('warehouse')} />
                <div className="vsep vsep-body" />
                <MergedColumn {...panelProps('bag')} />
              </div>

              <div className="merged-foot">
                <div className="mfcol mfcol-wh">
                  {sortCtl('warehouse')}
                  <IconAction icon="compact" label="정리" onClick={() => doCompact('warehouse')} />
                  <IconAction icon="trash" label="휴지통" danger onClick={() => doTrash('warehouse')} />
                </div>
                <div className="vsep vsep-foot" />
                <div className="mfcol mfcol-bag">
                  <IconAction icon="store" label="일괄보관" onClick={() => moveAll('bag', 'warehouse')} />
                  <span className="bb-spacer" />
                  <IconAction icon="retrieve" label="일괄가져오기" primary onClick={() => moveAll('warehouse', 'bag')} />
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      <div className={'toast' + (toast ? ' show' : '')}>{toast}</div>

      <TweaksPanel>
        <TweakSection label="간유리 / 배경" />
        <TweakRadio label="뒤 배경" value={t.scene} options={['실사', '협곡', '야간', '끄기']} onChange={(v) => setTweak('scene', v)} />
        <TweakSlider label="블러 강도" value={t.blur} min={0} max={40} unit="px" onChange={(v) => setTweak('blur', v)} />
        <TweakRadio label="크롬 채도" value={t.chroma} options={['절제', '상시']} onChange={(v) => setTweak('chroma', v)} />
        <TweakSection label="등급 바 표현" />
        <TweakRadio label="형태" value={t.gradeVariant} options={['언더라인', '코너', '풀테두리']} onChange={(v) => setTweak('gradeVariant', v)} />
        <TweakSection label="슬롯 발광" />
        <TweakRadio label="스타일" value={t.glowStyle} options={['소프트', '링', '엣지']} onChange={(v) => setTweak('glowStyle', v)} />
        <TweakSlider label="발광 강도" value={t.glow} min={20} max={100} unit="%" onChange={(v) => setTweak('glow', v)} />
        <TweakSection label="포인트 색" />
        <TweakColor label="포인트" value={t.accent} options={['#4a525c', '#5f7d72', '#8a7a6a']} onChange={(v) => setTweak('accent', v)} />
      </TweaksPanel>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
