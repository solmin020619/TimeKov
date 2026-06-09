// toast.jsx — TimeKov 공통 토스트: 전역 매니저 + 뷰 + 아이콘
// 전역 호출 데모: window.Toast.show("메시지", "warning", { onHideStart })
// (Unity의 ToastManager.Show(msg, ToastStyle.Warning) 와 1:1 대응되는 시그니처)

const TOAST_STYLES = ["info", "success", "warning", "error"];

// ── 상태 글리프 (작은 UI 아이콘 — currentColor) ────────────────────
function ToastGlyph({ style }) {
  const sw = 2;
  const common = {
    width: 18, height: 18, viewBox: "0 0 24 24", fill: "none",
    stroke: "currentColor", strokeWidth: sw, strokeLinecap: "round", strokeLinejoin: "round",
  };
  if (style === "success") {
    return (<svg {...common}><path d="M20 6.5 9.2 17.3 4 12.1" /></svg>);
  }
  if (style === "warning") {
    return (
      <svg {...common}>
        <path d="M12 3.6 21.2 19.5H2.8L12 3.6Z" />
        <path d="M12 10v4.2" />
        <circle cx="12" cy="17.4" r="0.4" fill="currentColor" stroke="none" />
      </svg>
    );
  }
  if (style === "error") {
    return (
      <svg {...common}>
        <circle cx="12" cy="12" r="9" />
        <path d="M12 7.4v5" />
        <circle cx="12" cy="16.2" r="0.4" fill="currentColor" stroke="none" />
      </svg>
    );
  }
  // info
  return (
    <svg {...common}>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11.2v5.2" />
      <circle cx="12" cy="7.9" r="0.5" fill="currentColor" stroke="none" />
    </svg>
  );
}

// ── 단일 토스트 뷰 ──────────────────────────────────────────────────
function ToastItem({ toast, box, accent, showCount, leaving }) {
  return (
    <div
      className="toast"
      data-style={toast.style}
      data-box={box}
      data-accent={accent}
      data-leaving={leaving ? "1" : undefined}
    >
      <span className="toast__accent" aria-hidden="true"></span>
      <span className="toast__icon"><ToastGlyph style={toast.style} /></span>
      <span className="toast__body">
        <span className="toast__msg">{toast.message}</span>
        {showCount && toast.count > 1 ? (
          <span className="toast__count">×{toast.count}</span>
        ) : null}
      </span>
    </div>
  );
}

// ── 토스트 레이어 (전역 매니저 구독) ────────────────────────────────
function ToastLayer({ position, box, accent, inOut, hold, showCount, debounceMs, maxVisible }) {
  const [items, setItems] = React.useState([]);   // 화면에 보이는 토스트
  const stateRef = React.useRef({
    items: [],            // {id, message, style, count, onHideStart, bornAt}
    queue: [],            // 대기열
    leaving: new Set(),   // 퇴장 애니 중인 id
    lastByMsg: new Map(), // message -> {id, t}
    seq: 0,
  });
  const cfg = React.useRef({});
  cfg.current = { inOut, hold, debounceMs, maxVisible };

  const sync = () => setItems(stateRef.current.items.slice());

  const removeToast = React.useCallback((id) => {
    const S = stateRef.current;
    if (S.leaving.has(id)) return;
    S.leaving.add(id);
    sync();
    const outMs = cfg.current.inOut * 1000;
    setTimeout(() => {
      const cur = stateRef.current;
      cur.items = cur.items.filter((it) => it.id !== id);
      cur.leaving.delete(id);
      for (const [m, rec] of cur.lastByMsg) if (rec.id === id) cur.lastByMsg.delete(m);
      // 대기열에서 다음 토스트 승격
      if (cur.queue.length && cur.items.length < cfg.current.maxVisible) {
        const next = cur.queue.shift();
        spawn(next);
      }
      sync();
    }, outMs + 30);
  }, []);

  const scheduleHide = React.useCallback((toast) => {
    clearTimeout(toast._timer);
    toast._timer = setTimeout(() => {
      if (typeof toast.onHideStart === "function") {
        try { toast.onHideStart(); } catch (e) { /* noop */ }
      }
      removeToast(toast.id);
    }, cfg.current.hold * 1000);
  }, [removeToast]);

  const spawn = React.useCallback((toast) => {
    const S = stateRef.current;
    toast.bornAt = performance.now();
    S.items.push(toast);
    S.lastByMsg.set(toast.message, { id: toast.id, t: toast.bornAt });
    scheduleHide(toast);
    // 오버플로우 → 가장 오래된 것 강제 퇴장
    while (S.items.length - S.leaving.size > cfg.current.maxVisible) {
      const oldest = S.items.find((it) => !S.leaving.has(it.id));
      if (!oldest) break;
      removeToast(oldest.id);
    }
    sync();
  }, [scheduleHide, removeToast]);

  const show = React.useCallback((message, style = "info", opts = {}) => {
    if (!TOAST_STYLES.includes(style)) style = "info";
    const S = stateRef.current;
    const now = performance.now();
    // 동일 메시지 디바운스
    const prev = S.lastByMsg.get(message);
    if (prev && now - prev.t < cfg.current.debounceMs) {
      const live = S.items.find((it) => it.id === prev.id);
      if (live) {
        live.count += 1;
        live.style = style;
        prev.t = now;
        scheduleHide(live);      // 타이머만 갱신
        sync();
        return live.id;
      }
    }
    const toast = {
      id: ++S.seq, message, style,
      count: 1, onHideStart: opts.onHideStart || null,
    };
    if (S.items.length - S.leaving.size >= cfg.current.maxVisible) {
      S.queue.push(toast);
    } else {
      spawn(toast);
    }
    return toast.id;
  }, [scheduleHide, spawn]);

  // 전역 노출 (싱글톤 호출 데모)
  React.useEffect(() => {
    window.Toast = {
      show,
      info: (m, o) => show(m, "info", o),
      success: (m, o) => show(m, "success", o),
      warning: (m, o) => show(m, "warning", o),
      error: (m, o) => show(m, "error", o),
      clear: () => {
        const S = stateRef.current;
        S.items.forEach((it) => clearTimeout(it._timer));
        S.items = []; S.queue = []; S.leaving.clear(); S.lastByMsg.clear();
        sync();
      },
    };
    return () => { delete window.Toast; };
  }, [show]);

  const leaving = stateRef.current.leaving;
  return (
    <div className="toast-layer" data-pos={position}>
      {items.map((t) => (
        <ToastItem
          key={t.id}
          toast={t}
          box={box}
          accent={accent}
          showCount={showCount}
          leaving={leaving.has(t.id)}
        />
      ))}
    </div>
  );
}

Object.assign(window, { ToastLayer, ToastItem, ToastGlyph, TOAST_STYLES });
