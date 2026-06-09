// scene.jsx — 게임 HUD 씬(맥락용) + 토스트 테스트 콘솔

function HudScene() {
  return (
    <div className="hud">
      {/* 배경: 기본 CSS 씬 (사용자가 스크린샷을 드롭하면 그 위에 오버레이) */}
      <image-slot
        id="toast-scene-bg"
        class="hud__bg"
        shape="rect"
        placeholder="게임 스크린샷을 여기에 드롭 — 실제 배경 위 가독성 테스트"
      ></image-slot>
      <div className="hud__scrim" aria-hidden="true"></div>

      {/* 좌상단 퀘스트 트래커 (기존 골든 박스 — 토스트와 별개 요소임을 보여줌) */}
      <div className="quest-tracker">
        <span className="quest-tracker__corner quest-tracker__corner--tl"></span>
        <span className="quest-tracker__corner quest-tracker__corner--br"></span>
        <div className="quest-tracker__label">현재 임무</div>
        <div className="quest-tracker__title">코어 정비 시설 가동</div>
        <div className="quest-tracker__step"><i></i>전력 코어 점검 <b>1 / 2</b></div>
      </div>

      {/* 우상단 미니맵 자리 */}
      <div className="minimap" aria-hidden="true">
        <span className="minimap__ping"></span>
      </div>

      {/* 하단 스킬바 + 스태미나 (토스트는 이 위에 뜸) */}
      <div className="skillbar">
        <div className="skillbar__slots">
          {["Q", "E", "R", "F"].map((k, i) => (
            <div className={"slot" + (i === 2 ? " slot--cd" : "")} key={k}>
              <span className="slot__key">{k}</span>
              {i === 2 ? <span className="slot__cd">3</span> : null}
            </div>
          ))}
        </div>
        <div className="vitals">
          <div className="vitals__bar vitals__bar--hp"><i style={{ width: "78%" }}></i></div>
          <div className="vitals__bar vitals__bar--st"><i style={{ width: "46%" }}></i></div>
        </div>
      </div>
    </div>
  );
}

// 실제 게임에서 토스트가 불릴 대표 시나리오 (브리프 4번 표 발췌)
const TEST_CASES = [
  { label: "건축 불가", msg: "건축 가능 지역이 아닙니다", style: "warning" },
  { label: "업데이트 완료", msg: "업데이트 완료", style: "success" },
  { label: "해금 필요", msg: "먼저 해금이 필요한 설비입니다", style: "warning" },
  { label: "설비 해금", msg: "분쇄기 설비를 해금했습니다!", style: "success" },
  { label: "가방 가득", msg: "가방이 가득 찼습니다", style: "warning" },
  { label: "창고 보관", msg: "가방이 가득 차 창고에 보관했습니다", style: "info" },
  { label: "보상 실패", msg: "보상을 받지 못했습니다. 가방을 비워주세요", style: "error" },
  { label: "스태미나 부족", msg: "스태미나가 부족합니다", style: "warning" },
  { label: "스킬 쿨다운", msg: "스킬이 아직 준비되지 않았습니다", style: "warning" },
  { label: "연료 없음", msg: "연료가 없습니다", style: "error" },
];

function TestConsole() {
  const fire = (msg, style, opts) => window.Toast && window.Toast.show(msg, style, opts);

  const burst = () => {
    const picks = [
      ["분쇄기 설비를 해금했습니다!", "success"],
      ["가방이 가득 찼습니다", "warning"],
      ["스태미나가 부족합니다", "warning"],
      ["연료가 없습니다", "error"],
      ["가방이 가득 차 창고에 보관했습니다", "info"],
    ];
    picks.forEach((p, i) => setTimeout(() => fire(p[0], p[1]), i * 260));
  };
  const spam = () => {
    for (let i = 0; i < 6; i++) setTimeout(() => fire("스태미나가 부족합니다", "warning"), i * 280);
  };

  return (
    <div className="console">
      <div className="console__head">
        <span className="console__dot"></span>
        토스트 테스트 콘솔
      </div>

      <div className="console__group">
        <div className="console__cap">스타일</div>
        <div className="console__row">
          {TOAST_STYLES.map((s) => (
            <button key={s} className={"chip chip--" + s}
              onClick={() => fire(`${s.toUpperCase()} 토스트 미리보기`, s)}>
              {s}
            </button>
          ))}
        </div>
      </div>

      <div className="console__group">
        <div className="console__cap">실제 시나리오</div>
        <div className="console__row console__row--wrap">
          {TEST_CASES.map((c) => (
            <button key={c.label} className={"chip chip--ghost chip--dot-" + c.style}
              onClick={() => fire(c.msg, c.style)}>
              {c.label}
            </button>
          ))}
        </div>
      </div>

      <div className="console__group">
        <div className="console__cap">동작</div>
        <div className="console__row">
          <button className="chip chip--ghost" onClick={burst}>연속 5개 (큐/스택)</button>
          <button className="chip chip--ghost" onClick={spam}>같은 메시지 도배 (디바운스)</button>
          <button className="chip chip--ghost" onClick={() => window.Toast && window.Toast.clear()}>모두 지우기</button>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { HudScene, TestConsole, TEST_CASES });
