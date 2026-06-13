// icons.jsx — line-art SVG set. stroke=currentColor so callers tint via color.
// Exports: window.Icon, window.ICONS

const ICONS = {
  // ---- category filter icons (7) ----
  cat_all: (
    <g fill="currentColor" stroke="none">
      {[6, 12, 18].map((x) => [6, 12, 18].map((y) => <circle key={x + '-' + y} cx={x} cy={y} r="1.5" />))}
    </g>
  ),
  // 원재료 = rough faceted ore/crystal
  cat_raw: (
    <g>
      <path d="M12 3 L18.5 8.5 L15.5 20.5 L8.5 20.5 L5.5 8.5 Z" />
      <path d="M5.5 8.5 H18.5 M12 3 V20.5 M9 8.5 L12 20.5 M15 8.5 L12 20.5" opacity="0.6" />
    </g>
  ),
  // 1차 가공 = stacked metal INGOT bars (주괴/판재)
  cat_primary: (
    <g>
      <path d="M6 15 H18 L16.3 19 H7.7 Z" />
      <path d="M8.4 9.5 H15.6 L14.4 13.2 H9.6 Z" />
    </g>
  ),
  // 2차 가공 = microchip / circuit (정밀부품/회로)
  cat_secondary: (
    <g>
      <rect x="8" y="8" width="8" height="8" rx="1" />
      <rect x="11" y="11" width="2" height="2" />
      <path d="M10 8 V5 M14 8 V5 M10 16 V19 M14 16 V19 M8 10.5 H5 M8 13.5 H5 M16 10.5 H19 M16 13.5 H19" />
    </g>
  ),
  // 전술 소비 = medkit cross
  cat_tactical: (
    <g>
      <rect x="5" y="5" width="14" height="14" rx="3.5" />
      <path d="M12 9 V15 M9 12 H15" />
    </g>
  ),
  // 코어 강화 = hex core
  cat_core: (
    <g>
      <path d="M12 4 L19 8 V16 L12 20 L5 16 V8 Z" />
      <path d="M12 9 L15 10.6 V13.4 L12 15 L9 13.4 V10.6 Z" />
    </g>
  ),
  // 특수 = sparkle
  cat_special: (
    <path d="M12 4 C12.6 9 15 11.4 20 12 C15 12.6 12.6 15 12 20 C11.4 15 9 12.6 4 12 C9 11.4 11.4 9 12 4 Z" />
  ),

  // ---- function icons ----
  caret: <path d="M7 10 L12 15 L17 10" />,
  clock: (
    <g>
      <circle cx="12" cy="12" r="8.2" />
      <path d="M12 7.4 V12 L15.2 13.8" />
    </g>
  ),
  chevup: <path d="M7 14 L12 9 L17 14" />,
  ascdesc: (
    <g>
      <path d="M7.5 6 V18 M5 8.5 L7.5 6 L10 8.5" />
      <path d="M16.5 18 V6 M14 15.5 L16.5 18 L19 15.5" />
    </g>
  ),
  compact: (
    <g>
      <path d="M4 20 L12.5 11.5" />
      <path d="M16 4 L16.9 6.6 L19.5 7.5 L16.9 8.4 L16 11 L15.1 8.4 L12.5 7.5 L15.1 6.6 Z" fill="currentColor" stroke="none" />
      <path d="M6.5 5 L7 6.4 L8.4 6.9 L7 7.4 L6.5 8.8 L6 7.4 L4.6 6.9 L6 6.4 Z" fill="currentColor" stroke="none" opacity="0.8" />
    </g>
  ),
  trash: (
    <g>
      <path d="M5 7 H19" />
      <path d="M9 7 V5 H15 V7" />
      <path d="M6.7 7 L7.7 20 H16.3 L17.3 7" />
      <path d="M10 10.5 V16.5 M14 10.5 V16.5" />
    </g>
  ),
  filter: <path d="M4 5 H20 L14 12 V19 L10 17 V12 Z" />,
  bag: (
    <g>
      <rect x="4" y="8" width="16" height="12" rx="1.5" />
      <path d="M9 8 V6.5 A1.5 1.5 0 0 1 10.5 5 H13.5 A1.5 1.5 0 0 1 15 6.5 V8" />
      <path d="M4 12.5 H20" opacity="0.7" />
    </g>
  ),
  // sci-fi close: X with bracket tips at each end
  close: (
    <g>
      <path d="M7.5 7.5 L16.5 16.5 M16.5 7.5 L7.5 16.5" />
      <path d="M5 7.5 V5 H7.5 M16.5 5 H19 V7.5 M19 16.5 V19 H16.5 M7.5 19 H5 V16.5" opacity="0.7" />
    </g>
  ),
  // 일괄보관 (가방 → 창고) : box with arrow going IN (down)
  store: (
    <g>
      <path d="M4 10 L6 5.5 H18 L20 10" />
      <path d="M4 10 H20 V19.5 H4 Z" />
      <path d="M12 12 V16.5 M9.5 14.2 L12 16.7 L14.5 14.2" />
    </g>
  ),
  // 일괄가져오기 (창고 → 가방) : box with arrow coming OUT (up)
  retrieve: (
    <g>
      <path d="M4 10 L6 5.5 H18 L20 10" />
      <path d="M4 10 H20 V19.5 H4 Z" />
      <path d="M12 17 V12.5 M9.5 14.8 L12 12.3 L14.5 14.8" />
    </g>
  ),

  // ---- item pictograms ----
  crystal: <path d="M12 3 L18.5 8.5 L15.5 20.5 L8.5 20.5 L5.5 8.5 Z M5.5 8.5 H18.5 M12 3 V20.5" />,
  leaf: (
    <g>
      <path d="M5 19 C5 11 11 5 19 5 C19 13 13 19 5 19 Z" />
      <path d="M8 16 C11 13 14 10 17 8" />
    </g>
  ),
  fiber: <path d="M6 5 C9 9 9 15 6 19 M12 5 C15 9 15 15 12 19 M18 5 C15 9 15 15 18 19" />,
  vein: (
    <g>
      <path d="M5 9 L9 4 L17 6 L19.5 13 L14 20 L6 18 Z" />
      <path d="M9 4 L11 11 L6 18 M11 11 L19.5 13" opacity="0.6" />
    </g>
  ),
  ingot: <path d="M6 15 H18 L16.3 19 H7.7 Z M8.4 9.5 H15.6 L14.4 13.2 H9.6 Z" />,
  plate: (
    <g>
      <path d="M4 11 L13 8 L20 10 L11 13 Z" />
      <path d="M4 11 V14 L11 16 V13 M11 16 L20 13 V10" opacity="0.7" />
    </g>
  ),
  alloyrod: (
    <g>
      <rect x="4" y="10" width="16" height="4" rx="2" transform="rotate(-20 12 12)" />
      <path d="M7.5 14.5 L9 11" opacity="0.6" />
    </g>
  ),
  oil: (
    <g>
      <path d="M12 4 C12 4 6 11 6 15 A6 6 0 0 0 18 15 C18 11 12 4 12 4 Z" />
      <path d="M9 15 A3 3 0 0 0 12 18" opacity="0.6" />
    </g>
  ),
  precision: (
    <g>
      <path d="M12 4 L18 7.5 V16.5 L12 20 L6 16.5 V7.5 Z" />
      <circle cx="12" cy="12" r="3.2" />
    </g>
  ),
  circuit: (
    <g>
      <rect x="8" y="8" width="8" height="8" rx="1" />
      <rect x="11" y="11" width="2" height="2" />
      <path d="M10 8 V5 M14 8 V5 M10 16 V19 M14 16 V19 M8 10.5 H5 M8 13.5 H5 M16 10.5 H19 M16 13.5 H19" />
    </g>
  ),
  bearing: (
    <g>
      <circle cx="12" cy="12" r="8" />
      <circle cx="12" cy="12" r="3" />
      <circle cx="12" cy="5.5" r="1" fill="currentColor" stroke="none" />
      <circle cx="12" cy="18.5" r="1" fill="currentColor" stroke="none" />
      <circle cx="5.5" cy="12" r="1" fill="currentColor" stroke="none" />
      <circle cx="18.5" cy="12" r="1" fill="currentColor" stroke="none" />
    </g>
  ),
  lens: (
    <g>
      <circle cx="12" cy="12" r="8" />
      <circle cx="12" cy="12" r="4" />
      <path d="M9.5 9.5 A3.5 3.5 0 0 1 14.5 9.5" opacity="0.6" />
    </g>
  ),
  medkit: <path d="M5 5 H19 V19 H5 Z M12 9 V15 M9 12 H15" />,
  grenade: (
    <g>
      <circle cx="12" cy="14" r="6" />
      <path d="M10 7 H14 V9 H10 Z M14 8 L17 6 M15.5 5 L18 7" />
    </g>
  ),
  booster: (
    <g>
      <path d="M12 3 C15 6 16 10 16 13 A4 4 0 0 1 8 13 C8 10 9 6 12 3 Z" />
      <path d="M9.5 19 L12 16 L14.5 19" />
    </g>
  ),
  smoke: <path d="M7 16 A4 4 0 0 1 7.5 8 A5 5 0 0 1 17 8.5 A3.5 3.5 0 0 1 16.5 16 Z" />,
  corechip: <path d="M12 4 L19 8 V16 L12 20 L5 16 V8 Z M12 9 L15 10.6 V13.4 L12 15 L9 13.4 V10.6 Z" />,
  module: (
    <g>
      <path d="M12 3 L20 7 V16 L12 20 L4 16 V7 Z" />
      <path d="M4 7 L12 11 L20 7 M12 11 V20" opacity="0.7" />
    </g>
  ),
  energycell: (
    <g>
      <rect x="6" y="6" width="12" height="13" rx="1.5" />
      <path d="M10 4 H14 V6 H10 Z" />
      <path d="M9 10 H15 M9 13 H15" opacity="0.7" />
    </g>
  ),
  key: (
    <g>
      <circle cx="8" cy="8" r="3.5" />
      <path d="M10.5 10.5 L19 19 M16 16 L18 14 M14 14 L16 12" />
    </g>
  ),
  datacube: (
    <g>
      <path d="M12 3 L20 7 V16 L12 20 L4 16 V7 Z" />
      <path d="M4 7 L12 11 L20 7 M12 11 V20" opacity="0.6" />
      <circle cx="8" cy="9.5" r="0.9" fill="currentColor" stroke="none" />
      <circle cx="12" cy="15" r="0.9" fill="currentColor" stroke="none" />
      <circle cx="16" cy="9.5" r="0.9" fill="currentColor" stroke="none" />
    </g>
  ),
  relic: (
    <g>
      <path d="M9 3 L17 7 L15 14 L18 18 L11 21 L5 16 L7 9 Z" />
      <path d="M9 3 L11 12 L18 18 M11 12 L5 16" opacity="0.55" />
    </g>
  ),
};

function Icon({ name, size = 24, sw = 1.6, style, className }) {
  const inner = ICONS[name];
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={sw}
      strokeLinecap="round"
      strokeLinejoin="round"
      style={style}
      className={className}
    >
      {inner || null}
    </svg>
  );
}

Object.assign(window, { Icon, ICONS });
