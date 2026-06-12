// data.jsx — design tokens, categories, grades, sort options, sample items.
// Exports: window.C, window.CATEGORIES, window.GRADES, window.SORTS, window.makeInitialItems

const C = {
  bg0: '#0b1422',
  bg1: '#0e1a2c',
  slot: '#16263c',
  slotBorder: '#34516f',
  textMain: '#eaf3fb',
  textSub: '#aebfd0',
  cyan: '#5fc4ff',
  cyanBright: '#aee3ff',
};

const CATEGORIES = [
  { key: 'all', label: '전체', icon: 'cat_all' },
  { key: 'raw', label: '원재료', icon: 'cat_raw' },
  { key: 'primary', label: '1차 가공', icon: 'cat_primary' },
  { key: 'secondary', label: '2차 가공', icon: 'cat_secondary' },
  { key: 'tactical', label: '전술 소비', icon: 'cat_tactical' },
  { key: 'core', label: '코어 강화', icon: 'cat_core' },
  { key: 'special', label: '특수', icon: 'cat_special' },
];

const CAT_LABEL = Object.fromEntries(CATEGORIES.map((c) => [c.key, c.label]));

// Common = no bar (transparent). Advanced 파랑 / Rare 초록 / Hero 보라 / Legend 골드.
const GRADES = {
  common: { key: 'common', label: '커먼', color: null, order: 0 },
  advanced: { key: 'advanced', label: '어드밴스드', color: '#4f9be0', order: 1 },
  rare: { key: 'rare', label: '레어', color: '#34c06a', order: 2 },
  hero: { key: 'hero', label: '히어로', color: '#a64fe0', order: 3 },
  legend: { key: 'legend', label: '레전드', color: '#ffb020', order: 4 },
};

const SORTS = [
  { key: 'recent', label: '획득순' },
  { key: 'name', label: '이름순' },
  { key: 'grade', label: '등급순' },
  { key: 'qty', label: '수량순' },
  { key: 'cat', label: '분류순' },
];

let _uid = 1;
const mk = (name, cat, icon, grade, qty, isNew = false) => ({
  id: _uid++,
  name,
  cat,
  icon,
  grade,
  qty,
  isNew,
});

// Two pools. Bag = 가방, Warehouse = 창고.
function makeInitialItems() {
  _uid = 1;
  const bag = [
    mk('철 광석', 'raw', 'crystal', 'common', 184),
    mk('구리 광석', 'raw', 'crystal', 'common', 132),
    mk('약초 잎', 'raw', 'leaf', 'common', 76),
    mk('합성 섬유', 'raw', 'fiber', 'advanced', 41),
    mk('강철 주괴', 'primary', 'ingot', 'advanced', 58),
    mk('구리 주괴', 'primary', 'ingot', 'common', 47),
    mk('합금 판재', 'primary', 'plate', 'rare', 23),
    mk('정제유', 'primary', 'oil', 'advanced', 30),
    mk('정밀 기어', 'secondary', 'precision', 'rare', 12),
    mk('제어 회로', 'secondary', 'circuit', 'rare', 9, true),
    mk('광학 렌즈', 'secondary', 'lens', 'hero', 4),
    mk('응급 키트', 'tactical', 'medkit', 'advanced', 16),
    mk('파편 수류탄', 'tactical', 'grenade', 'rare', 8),
    mk('전투 부스터', 'tactical', 'booster', 'hero', 3),
    mk('코어 칩', 'core', 'corechip', 'hero', 5, true),
    mk('강화 모듈', 'core', 'module', 'rare', 7),
    mk('에너지 셀', 'core', 'energycell', 'advanced', 22),
    mk('암호 키', 'special', 'key', 'legend', 1, true),
    mk('데이터 큐브', 'special', 'datacube', 'hero', 2),
  ];
  const warehouse = [
    mk('철 광석', 'raw', 'crystal', 'common', 920),
    mk('구리 광석', 'raw', 'crystal', 'common', 640),
    mk('코발트 원석', 'raw', 'vein', 'advanced', 210),
    mk('약초 잎', 'raw', 'leaf', 'common', 305),
    mk('합성 섬유', 'raw', 'fiber', 'advanced', 188),
    mk('형광 결정', 'raw', 'crystal', 'rare', 64),
    mk('강철 주괴', 'primary', 'ingot', 'advanced', 240),
    mk('구리 주괴', 'primary', 'ingot', 'common', 198),
    mk('합금 판재', 'primary', 'plate', 'rare', 96),
    mk('합금 봉', 'primary', 'alloyrod', 'advanced', 73),
    mk('정제유', 'primary', 'oil', 'advanced', 145),
    mk('강화 유리', 'primary', 'plate', 'rare', 38),
    mk('정밀 기어', 'secondary', 'precision', 'rare', 54),
    mk('제어 회로', 'secondary', 'circuit', 'rare', 41),
    mk('정밀 베어링', 'secondary', 'bearing', 'hero', 19),
    mk('광학 렌즈', 'secondary', 'lens', 'hero', 12),
    mk('서보 모터', 'secondary', 'precision', 'hero', 8),
    mk('신호 증폭기', 'secondary', 'circuit', 'legend', 3, true),
    mk('응급 키트', 'tactical', 'medkit', 'advanced', 64),
    mk('파편 수류탄', 'tactical', 'grenade', 'rare', 28),
    mk('전투 부스터', 'tactical', 'booster', 'hero', 11),
    mk('연막탄', 'tactical', 'smoke', 'advanced', 34),
    mk('진통제', 'tactical', 'medkit', 'common', 88),
    mk('코어 칩', 'core', 'corechip', 'hero', 14),
    mk('강화 모듈', 'core', 'module', 'rare', 26),
    mk('에너지 셀', 'core', 'energycell', 'advanced', 110),
    mk('상위 코어 칩', 'core', 'corechip', 'legend', 2, true),
    mk('암호 키', 'special', 'key', 'legend', 4),
    mk('데이터 큐브', 'special', 'datacube', 'hero', 9),
    mk('유물 파편', 'special', 'relic', 'legend', 1, true),
  ];
  return { bag, warehouse };
}

Object.assign(window, { C, CATEGORIES, CAT_LABEL, GRADES, SORTS, makeInitialItems });
