// components.jsx — reusable inventory UI pieces.
// Depends on window.Icon, window.GRADES, window.CAT_LABEL, window.SORTS, window.C
// Exports a bundle of components to window at the end.

const fmt = (n) => n.toLocaleString('en-US');

function NewBadge() {
  return <span className="new-dot" aria-label="신규" />;
}

function GradeBar({ grade, variant }) {
  const col = grade.color;
  if (!col) {
    // Common = 없음 (transparent)
    return null;
  }
  if (variant === 'corner') {
    return (
      <React.Fragment>
        <span className="gb-corner gb-bl" style={{ borderColor: col, boxShadow: `0 0 5px ${col}66` }} />
        <span className="gb-corner gb-br" style={{ borderColor: col, boxShadow: `0 0 5px ${col}66` }} />
      </React.Fragment>
    );
  }
  if (variant === 'fulledge') {
    return <span className="gb-full" style={{ boxShadow: `inset 0 0 0 1.5px ${col}, 0 0 8px ${col}44` }} />;
  }
  // default: underline + aurora glow rising up
  return (
    <React.Fragment>
      <span className="gb-aurora" style={{ background: `linear-gradient(to top, ${col}cc, ${col}55 35%, ${col}1f 65%, transparent)` }} />
      <span className="gb-underline" style={{ background: col, boxShadow: `0 0 10px ${col}, 0 -1px 4px ${col}cc` }} />
    </React.Fragment>
  );
}

function Tooltip({ item }) {
  const g = GRADES[item.grade];
  return (
    <div className="tooltip" role="tooltip">
      <div className="tt-name">{item.name}</div>
      {g.color && <span className="tt-grade" style={{ color: g.color }}>{g.label}</span>}
    </div>
  );
}

function Slot({ item, selected, hovered, gradeVariant, glowStyle, onClick, onEnter, onLeave }) {
  if (!item) {
    return <div className="slot empty" data-glow={glowStyle} />;
  }
  const g = GRADES[item.grade];
  const tint = g.color || '#e6ebf1';
  const cls = 'slot' + (selected ? ' sel' : '') + (item.grade === 'legend' ? ' is-legend' : '');
  return (
    <div
      className={cls}
      data-glow={glowStyle}
      onClick={onClick}
      onMouseEnter={onEnter}
      onMouseLeave={onLeave}
    >
      <Icon name={item.icon} size={60} sw={1.5} className="slot-icon" style={{ color: tint }} />
      {item.isNew && <NewBadge />}
      {item.qty > 1 && <span className="slot-qty">{fmt(item.qty)}</span>}
      <GradeBar grade={g} variant={gradeVariant} />
      {hovered && <Tooltip item={item} />}
    </div>
  );
}

function Grid({ slots, cols, gradeVariant, glowStyle, selectedId, hoveredId, onSelect, onHover }) {
  return (
    <div className="grid-scroll">
      <div className="grid" style={{ gridTemplateColumns: `repeat(${cols}, 90px)` }}>
        {slots.map((it, i) => (
          <Slot
            key={it ? 'i' + it.id : 'e' + i}
            item={it}
            selected={it && it.id === selectedId}
            hovered={it && it.id === hoveredId}
            gradeVariant={gradeVariant}
            glowStyle={glowStyle}
            onClick={it ? () => onSelect(it.id) : undefined}
            onEnter={it ? () => onHover(it.id) : undefined}
            onLeave={it ? () => onHover(null) : undefined}
          />
        ))}
      </div>
    </div>
  );
}

function Barcode({ seed = 7 }) {
  // deterministic pseudo-random bar widths for a sci-fi label strip
  const bars = [];
  let s = seed * 9301 + 49297;
  for (let i = 0; i < 22; i++) {
    s = (s * 9301 + 49297) % 233280;
    const w = 1 + (s % 4);
    const on = (s >> 3) % 3 !== 0;
    bars.push(<span key={i} style={{ width: w, background: on ? 'rgba(40,46,54,0.55)' : 'transparent', opacity: on ? 0.5 : 0 }} />);
  }
  return <span className="barcode">{bars}</span>;
}

function LiveClock() {
  const [now, setNow] = React.useState(() => new Date());
  React.useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(id);
  }, []);
  const p = (n) => String(n).padStart(2, '0');
  return (
    <span className="clock" title="세션 시각">
      <Icon name="clock" size={14} sw={1.7} />
      <span className="clock-t">{p(now.getHours())}:{p(now.getMinutes())}:<span className="clock-s">{p(now.getSeconds())}</span></span>
    </span>
  );
}

function CapacityGauge({ count, cap }) {
  const ratio = Math.min(1, count / cap);
  const near = ratio >= 0.9;
  const full = ratio >= 1;
  const col = full ? '#e0594a' : near ? '#e0a020' : null;
  return (
    <div className="cap">
      <span className="cap-label">용량</span>
      <span className="cap-num" style={col ? { color: col } : null}>
        {count}<span className="cap-cap">/{cap}</span>
      </span>
    </div>
  );
}

function PanelHeader({ title, onClose }) {
  return (
    <div className="p-head">
      <span className="title-ico" title="가방 아이콘 (PNG 교체용)"><Icon name="bag" size={28} sw={1.7} /></span>
      <span className="p-title">{title}</span>
      {onClose && (
        <button className="head-x" title="닫기 (Esc)" onClick={onClose}>
          <Icon name="close" size={40} sw={1.7} />
        </button>
      )}
    </div>
  );
}

function CategoryRow({ active, onPick }) {
  return (
    <div className="cat-row">
      {CATEGORIES.map((c) => (
        <button
          key={c.key}
          className={'cat-btn' + (active === c.key ? ' on' : '')}
          onClick={() => onPick(c.key)}
          title={c.label}
        >
          <Icon name={c.icon} size={30} sw={1.7} />
        </button>
      ))}
    </div>
  );
}

function SortControl({ sortKey, dir, onSort, onDir }) {
  const [open, setOpen] = React.useState(false);
  const cur = SORTS.find((s) => s.key === sortKey) || SORTS[0];
  return (
    <div className="sort-wrap">
      <button className="sort-btn" onClick={() => setOpen((o) => !o)}>
        <span>{cur.label}</span>
        <Icon name="caret" size={16} sw={1.8} />
      </button>
      <button className="icon-btn" title={dir === 'asc' ? '오름차순' : '내림차순'} onClick={onDir}>
        <Icon name="ascdesc" size={20} sw={1.6} />
        <span className="dir-mark">{dir === 'asc' ? '↑' : '↓'}</span>
      </button>
      {open && (
        <React.Fragment>
          <div className="sort-scrim" onClick={() => setOpen(false)} />
          <div className="sort-menu">
            {SORTS.map((s) => (
              <button
                key={s.key}
                className={'sort-item' + (s.key === sortKey ? ' on' : '')}
                onClick={() => {
                  onSort(s.key);
                  setOpen(false);
                }}
              >
                {s.label}
              </button>
            ))}
          </div>
        </React.Fragment>
      )}
    </div>
  );
}

function IconAction({ icon, label, onClick, danger, primary }) {
  return (
    <button
      className={'act-btn' + (danger ? ' danger' : '') + (primary ? ' primary' : '') + (icon ? '' : ' text-only')}
      onClick={onClick}
      title={label}
    >
      {icon && <Icon name={icon} size={20} sw={1.6} />}
      {label && <span className="act-label">{label}</span>}
    </button>
  );
}

function CornerBrackets() {
  return (
    <React.Fragment>
      <span className="br tl" />
      <span className="br tr" />
      <span className="br bl" />
      <span className="br br2" />
    </React.Fragment>
  );
}

Object.assign(window, {
  fmt,
  NewBadge,
  GradeBar,
  Tooltip,
  Slot,
  Grid,
  Barcode,
  LiveClock,
  CapacityGauge,
  PanelHeader,
  CategoryRow,
  SortControl,
  IconAction,
  CornerBrackets,
});
