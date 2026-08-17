// =====================================================================
// TimelinePuzzleUI.cs
// '시간선 복원' 퍼즐 패널. 5×5 의 모든 칸을 한 번씩 지나 기점에서 현재까지 한 획으로 잇는다.
//
// [규칙]
//   · 기점(또는 기점과 붙은 칸)에서만 시작한다
//   · 같은 획 안에서 되짚어 오면 지워진다
//   · 손을 떼면 즉시 초기화 — 경로가 끝에서 기점으로 빨려들어간다(시간 역행)
//   · 25칸을 채우고 현재에 닿는 순간 잠긴다
//
// [글자]
//   ★이 스크립트는 글자를 만들지 않는다. 패널에 있는 글자는 제목 하나뿐이고, 그것은
//     씬 오브젝트다(팀원이 씬을 훑어 번역 문구를 모으기 때문). 숫자 카운터는 번역 대상이
//     아니지만 같은 이유로 씬에 둔다. 나머지는 전부 도형이라 코드로 만든다.
//
// [색을 알파 대신 불투명색으로 굳힌 곳]
//   이 프로젝트는 Linear 컬러스페이스라 반투명 흰색이 의도보다 훨씬 밝게 합성된다.
//   그래서 '가만히 있는 장식'(모서리 눈금·띠 테두리·칸 눈금 등)은 목업에서 합성된
//   결과를 불투명색으로 계산해 넣었다. 반대로 '번쩍이며 사라지는 것'(파문·플래시)은
//   알파가 있어야 성립하므로 알파를 그대로 쓴다.
// =====================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimelinePuzzleUI : MonoBehaviour
{
    // ── 규격 (목업 좌표 그대로) ────────────────────────────────────────
    const int N = 5;
    const float PanelW = 620f, PanelH = 640f;
    const float Cell = 80f, Step = 90f;                 // 칸 80 + 간격 10
    const float DialY = -4f;                            // 다이얼/격자 중심 (목업 324 → 320-324)
    const float RingOuterD = 576f, RingInnerD = 468f, TickR = 270f;
    const float StripW = 320f, StripH = 44f, StripY = 275f;
    const float CloseSize = 45f, CloseX = 260.5f, CloseY = 274.5f;
    const float SegX0 = -214f, SegStep = 17.6f, SegY = -263.5f;

    // ── 색 ────────────────────────────────────────────────────────────
    static readonly Color BgInner = C("#13202F"), BgMid = C("#080D14"), BgOuter = C("#04070A");
    static readonly Color Frame   = C("#5F6970");       // 목업 #B9CBDE @0.5 합성 결과
    static readonly Color RingOut = C("#141F2D"), RingIn = C("#18232F");
    static readonly Color TickMaj = C("#2C3E54"), TickMin = C("#1B2836");
    static readonly Color Sweep   = C("#3C6C9E"), SweepTail = C("#25455F");
    static readonly Color StripBorder = C("#404B59"), StripFill = C("#151C26"), StripTick = C("#B4BFCC");
    static readonly Color CloseBorder = C("#586574"), CloseFill = C("#20262E"), CloseGlyph = C("#C6D4E4");

    static readonly Color PlateEmpty = C("#0D141C"), PlateHover = C("#182432");
    static readonly Color PlateStart = C("#1A1C1E"), PlateEnd = C("#101E2A");
    static readonly Color PlateOk = C("#173548"), PlateBad = C("#361D1B"), PlateWin = C("#3E3520");
    static readonly Color EdgeIdle = C("#1B2532"), EdgeHover = C("#33506B");
    static readonly Color EdgeOk = C("#2A5570"), EdgeBad = C("#5F2C27"), EdgeWin = C("#6A5A2C");
    static readonly Color NotchIdle = C("#1C2631");     // #2A3746 @0.5 합성 결과

    static readonly Color AOk = C("#6FA8E0"), ABad = C("#E2685C"), AWin = C("#F0C85A");
    static readonly Color CoreOk0 = C("#8FD3FF"), CoreOk1 = C("#5C90D8");
    static readonly Color CoreBad = C("#FFAFA6"), CoreWin = C("#FFE9A8");
    static readonly Color StartRing = C("#6C7F94"), StartDisc = C("#AFC2D6");
    static readonly Color EndDash = C("#527CA5"), SegEmpty = C("#1C2735");

    // ── 시간값 (목업과 동일) ──────────────────────────────────────────
    // 초/회전. ★부호는 아래 Spin() 에서 방향으로 붙인다 —
    //   목업은 바깥 링·초침·끝점이 시계 방향, 안쪽 링만 반시계다.
    //   유니티 UI 는 +Z 가 반시계이므로 시계 방향에는 음수를 준다(CSS 와 반대).
    const float SpinOuter = 60f, SpinInner = 40f, SpinSweep = 6f, SpinEnd = 14f;
    const float SpinOuterFast = 14f, SpinOuterWin = 3f;
    const float OpenStagger = .022f, OpenDelay = .12f, OpenDur = .26f;
    const float StepPop = 1.13f, StepPopDur = .20f, FlashDur = .26f;
    const float SegPop = 1.9f, SegPopDur = .24f, CntPop = 1.18f, CntPopDur = .18f;
    const float ShakeDur = .28f, WinStagger = .026f;
    const float DashPeriod = 1.1f;                      // 점선 한 주기(14+22=36px)가 흐르는 시간

    // ── 인스펙터 ──────────────────────────────────────────────────────
    [Header("씬 오브젝트 (글자는 씬에 있어야 한다)")]
    [Tooltip("제목 '시간선 복원'. LocalizedLabel 을 붙여 둔다.")]
    [SerializeField] TMP_Text titleLabel;
    [Tooltip("진행 숫자 (예: 12 / 25). 코드가 .text 를 쓰므로 LocalizedLabel 을 붙이지 말 것.")]
    [SerializeField] TMP_Text counterLabel;

    [Header("크기")]
    [Tooltip("화면을 얼마나 채울지. 1 이면 화면 높이를 꽉 채운다. 목업 비율은 그대로 유지된다.\n" +
             "고정 배율이 아니라 화면 크기에서 매번 계산하므로 해상도가 바뀌어도 같은 비율로 보인다.")]
    [Range(0.5f, 1f)]
    [SerializeField] float screenFill = 0.92f;

    // ── 상태 ──────────────────────────────────────────────────────────
    class CellView { public RectTransform rt; public Image plate; public Image[] notch; public CanvasGroup cg; }

    readonly List<Vector2Int> _path = new();
    readonly HashSet<Vector2Int> _blocked = new();
    CellView[,] _cells;
    Vector2Int _start, _end, _hover = new Vector2Int(-1, -1);
    bool _drag, _done, _busy, _built, _deadPrev;
    Action _onSolved;

    RectTransform _frame, _grid, _pathLayer, _marks, _fx, _shake;
    RectTransform _dialOuter, _dialInner, _sweep, _endSpin;
    RectTransform _startRing, _startDisc;
    Image _startRipple, _endPulse, _endDash;
    Image[] _segs;
    Graphic[] _endDiamond;
    float _spinOuterTarget = SpinOuter, _dashOffset, _rippleT;
    const float RippleDur = 1.8f;

    // 경로 3겹: 넓은 글로우 / 중간 / 흐르는 코어
    Image[] _segGlow, _segMid, _nodes;
    RawImage[] _segCore;

    // ==================================================================
    //  열고 닫기
    // ==================================================================
    public void Open(TimelinePuzzleConsole console, Action onSolved)
    {
        if (console == null) return;
        _start = console.StartCell;
        _end = console.EndCell;
        _blocked.Clear();
        foreach (var b in console.BlockedCells) _blocked.Add(b);
        _onSolved = onSolved;

        Build();        // 구조는 한 번만
        ApplyBoard();   // 판(기점·현재·막힌 칸)은 열 때마다

        // 커서 해제·조작 잠금·일시정지는 중앙(GameUIController.ApplyState)이 처리한다.
        // 직접 Cursor 를 만지면 다른 창과 상태가 어긋나 닫은 뒤 카메라가 안 돌아가는 일이 생긴다.
        var gui = GameUIController.Instance;
        if (gui != null)
        {
            gui.OpenTimelinePuzzle();
            // 다른 창이 이미 열려 있으면 중앙이 거절한다. 그때 억지로 띄우면 상태가 어긋난다.
            if (gui.GetCurrentState() != GameUIController.UIState.TimelinePuzzle) return;
        }
        else PlayerInputComponent.IsBlocked = true;   // 컨트롤러가 없는 씬(테스트용) 폴백

        MenuPanelAnim.Open(gameObject);
        GameSfx.Play(SfxId.UIPanelOpen);
        StartCoroutine(OpenCells());
    }

    /// <summary>닫기 요청. 중앙 상태를 None 으로 돌리면, 아래 Update 의 감시가 화면을 닫는다.</summary>
    public void Close()
    {
        var gui = GameUIController.Instance;
        if (gui != null) { gui.CloseTimelinePuzzle(); return; }

        PlayerInputComponent.IsBlocked = false;
        CloseVisual();
    }

    void CloseVisual()
    {
        GameSfx.Play(SfxId.UIPanelClose);
        MenuPanelAnim.Close(this, gameObject);
    }

    void Update()
    {
        if (!MenuPanelAnim.IsOpen(gameObject)) return;

        // ★ESC 를 직접 처리하지 않는다.
        //   GameUIController.Update 도 같은 프레임에 HandleEscape 를 돌린다. 내가 먼저 닫아
        //   상태를 None 으로 만들면 그쪽이 "열린 창이 없다"고 보고 설정창을 열어버린다
        //   (ESC 한 번에 퍼즐이 닫히고 설정창이 뜨는 꼴). 실행 순서는 보장되지 않으므로
        //   아예 손대지 않고, 중앙이 상태를 바꾸는 것만 지켜본다.
        //   이 한 가지 규칙이 ESC·일괄 닫기(CloseAll)·사망까지 전부 덮는다 —
        //   CloseAll 은 패널을 하나하나 이름으로 숨기는데 이 퍼즐은 그 목록에 없다.
        var gui = GameUIController.Instance;
        if (gui != null && gui.GetCurrentState() != GameUIController.UIState.TimelinePuzzle)
        {
            CloseVisual();
            return;
        }

        // 격자 밖에서 떼도 잡힌다(칸 위에서만 떼는 게 아니므로 여기서 본다).
        if (_drag && !Input.GetMouseButton(0)) Release();

        Spin();
        FlowDash();
        RippleTick();
    }

    // ★코루틴으로 돌리지 않는다. 패널을 닫으면 비활성 오브젝트의 코루틴은 정지되고 재개되지
    //   않아서, 한 번 닫은 뒤로는 파문이 영영 멈춰 있었다(글자 없이 시작 위치를 알려주는
    //   유일한 장치라 치명적이었다). 다이얼·맥동처럼 매 프레임 계산으로 바꿨다.
    void RippleTick()
    {
        if (_startRipple == null || !_startRipple.gameObject.activeSelf) return;

        _rippleT += Time.unscaledDeltaTime;
        if (_rippleT >= RippleDur) _rippleT -= RippleDur;

        float p = _rippleT / RippleDur;
        float k = 1f - (1f - p) * (1f - p);           // 목업의 ease-out
        float d = Mathf.Lerp(40f, 84f, k);
        _startRipple.rectTransform.sizeDelta = new Vector2(d, d);
        _startRipple.color = WithA(StartDisc, Mathf.Lerp(.55f, 0f, k));
    }

    // ==================================================================
    //  입력 (TimelinePuzzleCellInput 이 호출)
    // ==================================================================
    public void CellDown(int r, int c)
    {
        if (_done || _busy) return;
        var v = new Vector2Int(r, c);
        if (_blocked.Contains(v)) return;

        if (v == _start) { ResetPath(); _drag = true; FxStart(); Redraw(); return; }
        if (Adjacent(_start, v)) { ResetPath(); _path.Add(v); _drag = true; FxStart(); Redraw(); FxStep(r, c); }
    }

    public void CellEnter(int r, int c)
    {
        _hover = new Vector2Int(r, c);
        if (!_drag) { Redraw(); return; }
        if (_done || _busy) return;

        var v = new Vector2Int(r, c);
        if (_blocked.Contains(v)) return;

        int i = _path.IndexOf(v);
        if (i >= 0)
        {
            // 되짚어 오면 지워진다(같은 획 안에서만)
            if (i == _path.Count - 2) { _path.RemoveAt(_path.Count - 1); Redraw(); }
            return;
        }
        if (!Adjacent(_path[_path.Count - 1], v)) return;

        _path.Add(v);
        Redraw();
        FxStep(r, c);
        if (_done) FxWin();
    }

    public void CellExit(int r, int c)
    {
        if (_hover.x == r && _hover.y == c) { _hover = new Vector2Int(-1, -1); Redraw(); }
    }

    void Release()
    {
        _drag = false;
        if (_done || _busy || _path.Count <= 1) { Redraw(); return; }

        var pts = new List<Vector2Int>(_path);
        ResetPath();
        Redraw();
        StartCoroutine(FxRewind(pts));
    }

    void ResetPath() { _path.Clear(); _path.Add(_start); _done = false; _deadPrev = false; }

    /// <summary>판을 화면에 반영한다. ★반드시 열 때마다 불러야 한다 —
    /// 구조(Build)는 한 번만 만들지만 기점·현재의 '위치'는 장치마다 다르다.
    /// 이걸 빼먹으면 두 번째 장치를 열었을 때 표식이 첫 판 자리에 그대로 남는다.</summary>
    void ApplyBoard()
    {
        ApplyFit();   // 열 때마다 다시 — 해상도·창 크기가 바뀌어도 따라간다

        Vector2 sp = CellPos(_start), ep = CellPos(_end);
        Move(_startRipple.rectTransform, sp);
        Move(_startRing, sp);
        Move(_startDisc, sp);
        Move(_endPulse.rectTransform, ep);
        Move(_endDash.rectTransform, ep);
        Move(_endSpin, ep);

        // 막힌 칸이 있으면 채워야 할 개수가 줄어든다 — 세그먼트 바도 그만큼만 보여준다.
        int total = N * N - _blocked.Count;
        for (int i = 0; i < _segs.Length; i++) _segs[i].gameObject.SetActive(i < total);

        _rippleT = 0f;
        ResetPath();
        Redraw();
    }

    static void Move(RectTransform rt, Vector2 pos)
    { if (rt != null) rt.anchoredPosition = pos; }

    static bool Adjacent(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;

    // ==================================================================
    //  그리기
    // ==================================================================
    void Redraw()
    {
        var last = _path[_path.Count - 1];
        int total = N * N - _blocked.Count;
        _done = _path.Count == total && last == _end;

        bool dead = false;
        if (!_done)
        {
            dead = true;
            foreach (var d in Dirs)
            {
                var n = last + d;
                if (n.x < 0 || n.x >= N || n.y < 0 || n.y >= N) continue;
                if (_blocked.Contains(n) || _path.Contains(n)) continue;
                dead = false; break;
            }
        }
        if (dead && _drag && !_deadPrev) FxDead();
        _deadPrev = dead;

        Color a = _done ? AWin : (dead ? ABad : AOk);

        for (int r = 0; r < N; r++)
            for (int c = 0; c < N; c++)
            {
                var v = new Vector2Int(r, c);
                var cv = _cells[r, c];
                if (_blocked.Contains(v))
                {
                    cv.plate.color = C("#07090C");
                    foreach (var n in cv.notch) n.color = C("#131A22");
                    continue;
                }

                bool filled = _path.Contains(v);
                bool isLast = v == last;
                bool hot = !filled && !_done && Adjacent(last, v) && _hover == v;

                Color fill = filled ? (_done ? PlateWin : (dead ? PlateBad : PlateOk))
                                    : (hot ? PlateHover : PlateEmpty);
                if (!filled && v == _start) fill = PlateStart;
                if (!filled && v == _end) fill = PlateEnd;
                cv.plate.color = fill;

                // 테두리는 판 뒤에 깔지 않고 Outline 으로 준다 — 눌림/확대 연출에 같이 따라간다.
                var edge = cv.plate.GetComponent<UnityEngine.UI.Outline>();
                if (edge != null)
                    edge.effectColor = isLast ? a
                        : (filled ? (_done ? EdgeWin : (dead ? EdgeBad : EdgeOk)) : (hot ? EdgeHover : EdgeIdle));

                Color nc = filled ? (isLast ? a : Blend(a, filled ? fill : PlateEmpty, .55f)) : NotchIdle;
                foreach (var n in cv.notch) n.color = nc;
            }

        DrawPath(a, dead);

        // 기점의 대기 파문 — 아직 아무것도 잇지 않았고 끌고 있지도 않을 때만
        _startRipple.gameObject.SetActive(_path.Count == 1 && !_drag && !_busy);
        _endDash.gameObject.SetActive(!_done);
        _endDash.color = _done ? a : EndDash;
        _endPulse.gameObject.SetActive(!_done);
        foreach (var g in _endDiamond) g.color = _done ? CoreWin : (dead ? CoreBad : CoreOk0);

        for (int i = 0; i < _segs.Length; i++)
        {
            bool on = i < _path.Count;
            _segs[i].color = on ? a : SegEmpty;
            var rt = _segs[i].rectTransform;
            rt.sizeDelta = new Vector2(12f, on ? 7f : 4f);
        }
        if (counterLabel != null)
        {
            counterLabel.text = $"{_path.Count} / {total}";
            counterLabel.color = a;
        }

        if (_done && _onSolved != null)
        {
            var cb = _onSolved;
            _onSolved = null;

            // ★즉시 부른다. 예전엔 완료 연출(1.5초)을 다 보여준 뒤에 불렀는데, 그 사이에 ESC 를
            //   누르면 패널이 꺼지면서 코루틴이 죽어 콜백이 영영 실행되지 않았다
            //   (퍼즐을 풀었는데 문도 안 열리고 세이브도 안 되어 다시 풀어야 했다).
            //   먼저 결과를 확정하고, 연출은 그 뒤에 따로 재생한다 — 끊겨도 손해가 없다.
            cb.Invoke();
            StartCoroutine(CloseAfterWin());
        }
    }

    IEnumerator CloseAfterWin()
    {
        yield return Wait(1.9f);   // 완료 연출을 다 본 뒤 스스로 닫힌다
        Close();
    }

    void DrawPath(Color a, bool dead)
    {
        int segCount = _path.Count - 1;
        for (int i = 0; i < _segGlow.Length; i++)
        {
            bool on = i < segCount;
            _segGlow[i].gameObject.SetActive(on);
            _segMid[i].gameObject.SetActive(on);
            _segCore[i].gameObject.SetActive(on);
            if (!on) continue;

            Vector2 p0 = CellPos(_path[i]), p1 = CellPos(_path[i + 1]);
            Vector2 mid = (p0 + p1) * .5f;

            // ★진행 방향까지 각도에 반영한다. 0/90도만 쓰면 왼쪽·아래로 가는 선분에서
            //   점선이 거꾸로 흐른다(선분의 +x 축이 경로 방향과 반대가 되기 때문).
            float rot = Mathf.Abs(p1.x - p0.x) > .5f
                      ? (p1.x > p0.x ? 0f : 180f)
                      : (p1.y > p0.y ? 90f : -90f);

            // 코어 색: 목업의 좌상→우하 그라디언트를 선분 위치로 근사한다.
            float t = Mathf.Clamp01(((mid.x + 180f) / 360f + (176f - mid.y) / 360f) * .5f);
            Color core = _done ? CoreWin : (dead ? CoreBad : Color.Lerp(CoreOk0, CoreOk1, t));

            Place(_segGlow[i].rectTransform, mid, new Vector2(Step + 26f, 26f), rot);
            Place(_segMid[i].rectTransform, mid, new Vector2(Step + 12f, 12f), rot);
            Place(_segCore[i].rectTransform, mid, new Vector2(Step, 5f), rot);

            _segGlow[i].color = WithA(a, .13f);
            _segMid[i].color = WithA(a, .35f);
            _segCore[i].color = core;
            // 점선이 경로 전체를 따라 흐르게 — 선분마다 누적 거리를 uv 에 반영한다.
            _segCore[i].uvRect = new Rect(_dashOffset + i * (Step / 36f), 0f, Step / 36f, 1f);
        }

        // 꺾이는 자리를 메우는 마디. 목업의 경로는 round join 이라 코너가 둥글게 이어진다 —
        // 선분만 이어붙이면 코너에 2.5px 짜리 이가 빠져 보인다.
        for (int i = 0; i < _nodes.Length; i++)
        {
            bool on = i < _path.Count;
            _nodes[i].gameObject.SetActive(on);
            if (!on) continue;
            Place(_nodes[i].rectTransform, CellPos(_path[i]), new Vector2(5f, 5f), 0f);
            _nodes[i].color = _done ? CoreWin : (dead ? CoreBad : CoreOk0);
        }
    }

    // ==================================================================
    //  연출
    // ==================================================================
    void Spin()
    {
        float dt = Time.unscaledDeltaTime;
        Rot(_dialOuter, -360f / _spinOuterTarget * dt);   // 시계 방향
        Rot(_dialInner,  360f / SpinInner * dt);          // 반시계 (목업의 reverse)
        Rot(_sweep,     -360f / SpinSweep * dt);          // 시계 방향
        Rot(_endSpin,   -360f / SpinEnd * dt);            // 시계 방향

        // 끝점 맥동 — 목업과 같은 2초 주기(반지름 22↔30, 색은 합성 결과 사이를 오간다)
        float k = (Mathf.Sin(Time.unscaledTime * Mathf.PI) + 1f) * .5f;
        float d = Mathf.Lerp(44f, 60f, k);
        _endPulse.rectTransform.sizeDelta = new Vector2(d, d);
        _endPulse.color = Color.Lerp(C("#1D3143"), C("#304D68"), k);
    }
    static void Rot(RectTransform rt, float deg) { if (rt != null) rt.Rotate(0f, 0f, deg); }

    void FlowDash()
    {
        _dashOffset -= Time.unscaledDeltaTime / DashPeriod;   // 한 주기(36px)가 DashPeriod 초
        for (int i = 0; i < _segCore.Length; i++)
            if (_segCore[i].gameObject.activeSelf)
                _segCore[i].uvRect = new Rect(_dashOffset + i * (Step / 36f), 0f, Step / 36f, 1f);
    }

    void FxStart()
    {
        Ring(CellPos(_start), 32f, 128f, StartDisc, .48f);
        StartCoroutine(DialBurst(SpinOuterFast, .9f));
    }

    void FxStep(int r, int c)
    {
        Vector2 p = CellPos(new Vector2Int(r, c));
        StartCoroutine(Pop(_cells[r, c].rt, StepPop, StepPopDur));
        Flash(p, CoreOk0);
        Ring(p, 12f, 68f, CoreOk0, .38f);
        int i = _path.Count - 1;
        if (i < _segs.Length) StartCoroutine(Pop(_segs[i].rectTransform, SegPop, SegPopDur));
        if (counterLabel != null) StartCoroutine(Pop(counterLabel.rectTransform, CntPop, CntPopDur));

        // 한 판에 25번 연달아 나는 소리다. 클릭음은 그만큼 반복하면 귀에 박히므로
        // 호버음(원래 가볍게 만든 소리)을 쓰고 볼륨까지 낮춰 '틱틱' 정도로만 들리게 한다.
        GameSfx.Play(SfxId.UIItemHover, .3f);
    }

    // 막다른 길 = 실제 실패. 성공음(CoreUpgradeSuccess)과 짝이 되는 실패음을 쓴다.
    //   PlayerSkillUnavailable('스킬 못 씀' 삑) 보다 실패로 읽힌다.
    void FxDead()
    {
        StartCoroutine(Shake());
        GameSfx.Play(SfxId.CoreUpgradeFail);
    }

    void FxWin()
    {
        Ring(CellPos(_end), 40f, 140f, AWin, .52f);
        StartCoroutine(Pop(_endSpin, 1.5f, .42f));
        Ring(new Vector2(0f, DialY), 240f, 660f, AWin, .70f);
        StartCoroutine(WinSweep());
        StartCoroutine(DialBurst(SpinOuterWin, 1.4f));
        GameSfx.Play(SfxId.CoreUpgradeSuccess);
    }

    IEnumerator WinSweep()
    {
        for (int i = 0; i < _path.Count; i++)
        {
            var p = _path[i];
            Flash(CellPos(p), CoreWin);
            StartCoroutine(Pop(_cells[p.x, p.y].rt, 1.1f, .18f));
            yield return Wait(WinStagger);
        }
    }

    // 실패: 경로가 끝에서 기점 쪽으로 빨려들어간다(시간 역행). 그동안 입력을 막는다.
    IEnumerator FxRewind(List<Vector2Int> pts)
    {
        _busy = true;
        // ★무음이다. 마우스를 떼서 그리던 걸 되감는 건 '실패'가 아니라 그냥 다시 그리려는 동작이라,
        //   소리를 넣으면 몇 번만 반복해도 거슬린다. 실패음은 막다른 길(FxDead)에만 있다.

        // 목업과 같은 길이: 경로 픽셀 길이 × 0.9ms, 220~520ms 로 제한
        float len = (pts.Count - 1) * Step;
        float dur = Mathf.Clamp(len * .0009f, .22f, .52f);
        int n = pts.Count;
        float per = dur / Mathf.Max(1, n);

        for (int i = n - 1; i >= 0; i--)
        {
            Flash(CellPos(pts[i]), CoreBad);
            if (i < _segs.Length) StartCoroutine(Pop(_segs[i].rectTransform, .4f, .16f));
            yield return Wait(per);
        }
        _busy = false;
        Redraw();
    }

    IEnumerator DialBurst(float fast, float hold)
    {
        _spinOuterTarget = fast;
        yield return Wait(hold);
        _spinOuterTarget = SpinOuter;
    }

    IEnumerator OpenCells()
    {
        var order = new List<Vector2Int>();
        for (int r = 0; r < N; r++) for (int c = 0; c < N; c++) order.Add(new Vector2Int(r, c));

        // 목업: 배율 0.7 → 1 과 투명도 0 → 1 을 동시에, 260ms OutQuad
        foreach (var v in order)
        {
            var cv = _cells[v.x, v.y];
            cv.rt.localScale = Vector3.one * .7f;
            cv.cg.alpha = 0f;
        }
        yield return Wait(OpenDelay);
        foreach (var v in order)
        {
            StartCoroutine(PopIn(_cells[v.x, v.y], OpenDur));
            yield return Wait(OpenStagger);
        }
    }

    IEnumerator PopIn(CellView cv, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float k = 1f - (1f - p) * (1f - p);        // OutQuad
            cv.rt.localScale = Vector3.one * Mathf.Lerp(.7f, 1f, k);
            cv.cg.alpha = k;
            yield return null;
        }
        cv.rt.localScale = Vector3.one;
        cv.cg.alpha = 1f;
    }

    IEnumerator Pop(RectTransform rt, float peak, float dur)
    {
        if (rt == null) yield break;
        float up = dur * .35f, t = 0f;
        while (t < up) { t += Time.unscaledDeltaTime; rt.localScale = Vector3.one * Mathf.Lerp(1f, peak, t / up); yield return null; }
        t = 0f; float down = dur - up;
        while (t < down) { t += Time.unscaledDeltaTime; rt.localScale = Vector3.one * Mathf.Lerp(peak, 1f, t / down); yield return null; }
        rt.localScale = Vector3.one;
    }

    IEnumerator Shake()
    {
        float[] ofs = { 0f, -7f, 6f, -4f, 0f };
        float per = ShakeDur / (ofs.Length - 1);
        for (int i = 0; i < ofs.Length - 1; i++)
        {
            float t = 0f;
            while (t < per)
            {
                t += Time.unscaledDeltaTime;
                _shake.anchoredPosition = new Vector2(Mathf.Lerp(ofs[i], ofs[i + 1], t / per), 0f);
                yield return null;
            }
        }
        _shake.anchoredPosition = Vector2.zero;
    }

    void Ring(Vector2 pos, float from, float to, Color col, float dur)
    {
        var img = NewImage(_fx, "_ring", col);
        img.sprite = TimeUiSprites.Ring(to, 3f);
        Place(img.rectTransform, pos, new Vector2(from, from), 0f);
        StartCoroutine(RingCo(img, from, to, dur));
    }
    IEnumerator RingCo(Image img, float from, float to, float dur)
    {
        Color c0 = img.color; float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - (1f - t / dur) * (1f - t / dur);
            float d = Mathf.Lerp(from, to, k);
            img.rectTransform.sizeDelta = new Vector2(d, d);
            img.color = WithA(c0, Mathf.Lerp(.8f, 0f, k));
            yield return null;
        }
        Destroy(img.gameObject);
    }

    void Flash(Vector2 pos, Color col)
    {
        var img = NewImage(_fx, "_flash", WithA(col, .55f));
        Place(img.rectTransform, pos, new Vector2(Cell, Cell), 0f);
        StartCoroutine(FlashCo(img));
    }
    IEnumerator FlashCo(Image img)
    {
        Color c0 = img.color; float t = 0f;
        while (t < FlashDur)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - (1f - t / FlashDur) * (1f - t / FlashDur);
            img.color = WithA(c0, Mathf.Lerp(.55f, 0f, k));
            yield return null;
        }
        Destroy(img.gameObject);
    }

    static IEnumerator Wait(float s)
    {
        float t = 0f;
        while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
    }

    /// <summary>화면 크기에 맞춰 배율을 정한다.
    ///
    /// 고정 배율을 쓰면 캔버스 기준 해상도가 뭐냐에 따라 화면에서의 크기가 달라져서,
    /// 어떤 해상도에서는 퍼즐이 화면 한구석에 작게 뜬다. 그래서 매번 캔버스 실제 크기에서 계산한다.
    ///
    ///   ★루트가 아니라 _frame 의 배율을 건드린다. MenuPanelAnim 의 열기/닫기 연출이
    ///     루트 localScale 을 1.04→1 로 덮어쓰기 때문에, 루트에 배율을 넣으면 창이 열리는
    ///     순간 지워진다(예전 panelScale 이 아무 효과도 없던 이유). 배경은 루트에 있어서
    ///     여전히 화면 전체를 덮는다.
    ///   ★가로·세로 둘 중 좁은 쪽에 맞춘다 — 세로가 짧은 화면에서 위아래가 잘리지 않게.</summary>
    void ApplyFit()
    {
        if (_frame == null) return;   // 아직 조립 전 — Build 직후 ApplyBoard 가 다시 부른다

        var canvas = GetComponentInParent<Canvas>(true);   // 자기 자신이 비활성일 수 있다 → true
        if (canvas == null) return;

        var crt = canvas.rootCanvas.transform as RectTransform;
        if (crt == null) return;

        float w = crt.rect.width, h = crt.rect.height;
        if (w <= 1f || h <= 1f) return;   // 아직 레이아웃 전 — 다음 호출(열 때)에 다시 잡는다

        // ★Clamp 는 안전장치다. 이 필드는 이미 씬에 저장된 컴포넌트에 나중에 추가된 것이라,
        //   어떤 이유로든 0 이 들어오면 배율이 0 이 되어 퍼즐이 통째로 안 보인다.
        float fill = Mathf.Clamp(screenFill, 0.5f, 1f);

        float s = Mathf.Min(h * fill / PanelH, w * fill / PanelW);
        _frame.localScale = Vector3.one * s;
    }

    // ==================================================================
    //  조립 (도형만 만든다 — 글자는 씬에 있다)
    // ==================================================================
    void Build()
    {
        if (_built) return;
        _built = true;

        var root = (RectTransform)transform;

        // 배경 — 알파 없이 색으로 구운 방사형 그라디언트
        var bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.sprite = TimeUiSprites.RadialGradient(BgInner, BgMid, BgOuter);
        bg.color = Color.white;

        _frame = NewRect("_Frame", root); Center(_frame, Vector2.zero, new Vector2(PanelW, PanelH));
        CornerTicks(_frame, PanelW, PanelH, Frame, 16f, 2f);

        // 다이얼
        _dialOuter = NewRect("_DialOuter", _frame); Center(_dialOuter, new Vector2(0f, DialY), new Vector2(RingOuterD, RingOuterD));
        var ro = NewImage(_dialOuter, "_ring", RingOut); ro.sprite = TimeUiSprites.Ring(RingOuterD, 2f);
        Center(ro.rectTransform, Vector2.zero, new Vector2(RingOuterD, RingOuterD));
        for (int i = 0; i < 60; i++)
        {
            bool maj = i % 5 == 0;
            float len = maj ? 18f : 8f, w = maj ? 3f : 1f;
            float ang = i * 6f * Mathf.Deg2Rad;
            var t = NewImage(_dialOuter, "_tick", maj ? TickMaj : TickMin);
            Vector2 dir = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang));
            Place(t.rectTransform, dir * (TickR - len * .5f), new Vector2(w, len), -i * 6f);
        }

        _dialInner = NewRect("_DialInner", _frame); Center(_dialInner, new Vector2(0f, DialY), new Vector2(RingInnerD, RingInnerD));
        var ri = NewImage(_dialInner, "_ring", RingIn); ri.sprite = TimeUiSprites.Ring(RingInnerD, 2f);
        Center(ri.rectTransform, Vector2.zero, new Vector2(RingInnerD, RingInnerD));

        _sweep = NewRect("_Sweep", _frame); Center(_sweep, new Vector2(0f, DialY), new Vector2(4f, 4f));
        var sw = NewImage(_sweep, "_head", Sweep); Place(sw.rectTransform, new Vector2(0f, 279f), new Vector2(4f, 34f), 0f);
        var st = NewImage(_sweep, "_tail", SweepTail); Place(st.rectTransform, new Vector2(0f, 261f), new Vector2(1f, 70f), 0f);

        // 제목 띠 (글자는 씬의 titleLabel 을 그 위로 올린다)
        var sb = NewImage(_frame, "_StripBorder", StripBorder);
        Center(sb.rectTransform, new Vector2(0f, StripY), new Vector2(StripW + 2f, StripH + 2f));
        var sf = NewImage(_frame, "_Strip", StripFill);
        Center(sf.rectTransform, new Vector2(0f, StripY), new Vector2(StripW, StripH));
        CornerTicks(sf.rectTransform, StripW, StripH, StripTick, 11f, 2f);
        if (titleLabel != null)
        {
            var trt = titleLabel.rectTransform;
            trt.SetParent(_frame, false);
            Center(trt, new Vector2(0f, StripY), new Vector2(StripW, StripH));
            titleLabel.alignment = TextAlignmentOptions.Center;
            titleLabel.fontSize = 21f;
            titleLabel.fontWeight = FontWeight.Medium;
            titleLabel.characterSpacing = 7f;
            titleLabel.color = C("#F0F6FF");
            trt.SetAsLastSibling();
        }

        // 닫기 (오른쪽 위) — 아이콘만.
        //   ★테두리·바탕·X 를 한 묶음(_Close)에 담고, Button 과 눌림 연출을 그 묶음에 둔다.
        //     예전엔 테두리가 형제라 눌림 축소가 바탕에만 걸려 아이콘만 쪼그라들었다.
        var closeGroup = NewRect("_Close", _frame);
        Center(closeGroup, new Vector2(CloseX, CloseY), new Vector2(CloseSize + 2f, CloseSize + 2f));

        var cb = NewImage(closeGroup, "_CloseBorder", CloseBorder); Stretch(cb.rectTransform);
        var cf = NewImage(closeGroup, "Btn_Close", CloseFill);
        Center(cf.rectTransform, Vector2.zero, new Vector2(CloseSize, CloseSize));
        var g1 = NewImage(cf.rectTransform, "_x", CloseGlyph); Place(g1.rectTransform, Vector2.zero, new Vector2(27f, 2.6f), 45f);
        var g2 = NewImage(cf.rectTransform, "_x", CloseGlyph); Place(g2.rectTransform, Vector2.zero, new Vector2(27f, 2.6f), -45f);

        var btn = closeGroup.gameObject.AddComponent<Button>();
        // ★NewImage 는 장식용이라 raycastTarget 을 끈 채로 만든다 — 클릭을 받을 테두리만 다시 켠다.
        //   안 켜면 클릭이 어떤 그래픽에도 닿지 않아 onClick 이 아예 안 불린다(닫기가 안 먹던 원인).
        //   테두리가 가장 바깥(CloseSize+2)이라 버튼 영역 전체를 덮는다.
        cb.raycastTarget = true;
        btn.targetGraphic = cb;
        btn.transition = Selectable.Transition.None;   // 색 변화 없이 눌림 축소만
        btn.onClick.AddListener(Close);
        // 실행 중에 만든 버튼은 UIButtonPressInstaller 가 못 잡으므로 직접 붙인다.
        closeGroup.gameObject.AddComponent<UIButtonPressEffect>();

        // 흔들리는 묶음(격자 + 경로 + 표식 + 효과)
        _shake = NewRect("_Shake", _frame); Stretch(_shake);
        _grid = NewRect("_Grid", _shake); Stretch(_grid);
        _pathLayer = NewRect("_Path", _shake); Stretch(_pathLayer);
        _marks = NewRect("_Marks", _shake); Stretch(_marks);
        _fx = NewRect("_Fx", _shake); Stretch(_fx);

        BuildCells();
        BuildPathPool();
        BuildMarks();
        BuildSegments();

        if (counterLabel != null)
        {
            var crt = counterLabel.rectTransform;
            crt.SetParent(_frame, false);
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(.5f, .5f);
            crt.anchoredPosition = new Vector2(220f - 100f, -250f);
            crt.sizeDelta = new Vector2(200f, 28f);
            counterLabel.alignment = TextAlignmentOptions.Right;
            counterLabel.fontSize = 20f;
            counterLabel.fontWeight = FontWeight.Medium;
            crt.SetAsLastSibling();
        }
    }

    void BuildCells()
    {
        _cells = new CellView[N, N];
        for (int r = 0; r < N; r++)
            for (int c = 0; c < N; c++)
            {
                var holder = NewRect("Cell", _grid);
                Center(holder, CellPos(new Vector2Int(r, c)), new Vector2(Cell, Cell));

                var plate = NewImage(holder, "_plate", PlateEmpty);
                Stretch(plate.rectTransform);
                plate.raycastTarget = true;
                var outline = plate.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = EdgeIdle;
                outline.effectDistance = new Vector2(2f, 2f);
                outline.useGraphicAlpha = false;

                var notch = new Image[8];
                float h = Cell * .5f;
                var spec = new (float x, float y, float w, float hh)[]
                {
                    (-h + 4.5f,  h - 1f, 9f, 2f), (-h + 1f,  h - 4.5f, 2f, 9f),
                    ( h - 4.5f,  h - 1f, 9f, 2f), ( h - 1f,  h - 4.5f, 2f, 9f),
                    (-h + 4.5f, -h + 1f, 9f, 2f), (-h + 1f, -h + 4.5f, 2f, 9f),
                    ( h - 4.5f, -h + 1f, 9f, 2f), ( h - 1f, -h + 4.5f, 2f, 9f),
                };
                for (int i = 0; i < 8; i++)
                {
                    var n = NewImage(holder, "_notch", NotchIdle);
                    Place(n.rectTransform, new Vector2(spec[i].x, spec[i].y), new Vector2(spec[i].w, spec[i].hh), 0f);
                    notch[i] = n;
                }

                holder.gameObject.AddComponent<TimelinePuzzleCellInput>().Bind(this, r, c);
                // 열기 연출이 배율과 함께 투명도까지 올리므로 칸마다 CanvasGroup 이 필요하다.
                var cg = holder.gameObject.AddComponent<CanvasGroup>();
                _cells[r, c] = new CellView { rt = holder, plate = plate, notch = notch, cg = cg };
            }
    }

    void BuildPathPool()
    {
        int max = N * N - 1;
        _segGlow = new Image[max]; _segMid = new Image[max]; _segCore = new RawImage[max];
        for (int i = 0; i < max; i++)
        {
            _segGlow[i] = NewImage(_pathLayer, "_glow", WithA(AOk, .13f));
            _segGlow[i].sprite = TimeUiSprites.Capsule(26f);
            _segGlow[i].type = Image.Type.Sliced;

            _segMid[i] = NewImage(_pathLayer, "_mid", WithA(AOk, .35f));
            _segMid[i].sprite = TimeUiSprites.Capsule(12f);
            _segMid[i].type = Image.Type.Sliced;

            var go = new GameObject("_core", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(_pathLayer, false);
            var raw = go.GetComponent<RawImage>();
            raw.texture = TimeUiSprites.DashTexture(14, 22);
            raw.raycastTarget = false;
            _segCore[i] = raw;

            _segGlow[i].gameObject.SetActive(false);
            _segMid[i].gameObject.SetActive(false);
            go.SetActive(false);
        }

        _nodes = new Image[N * N];
        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i] = NewImage(_pathLayer, "_node", CoreOk0);
            _nodes[i].sprite = TimeUiSprites.Disc();
            _nodes[i].gameObject.SetActive(false);
        }
    }

    void BuildMarks()
    {
        Vector2 sp = CellPos(_start), epv = CellPos(_end);

        _startRipple = NewImage(_marks, "_startRipple", StartDisc);
        _startRipple.sprite = TimeUiSprites.Ring(84f, 2f);
        Place(_startRipple.rectTransform, sp, new Vector2(40f, 40f), 0f);

        var sr = NewImage(_marks, "_startRing", StartRing);
        sr.sprite = TimeUiSprites.Ring(40f, 2f);
        Place(sr.rectTransform, sp, new Vector2(40f, 40f), 0f);
        _startRing = sr.rectTransform;
        var sd = NewImage(_marks, "_startDisc", StartDisc);
        sd.sprite = TimeUiSprites.Disc();
        Place(sd.rectTransform, sp, new Vector2(24f, 24f), 0f);
        _startDisc = sd.rectTransform;

        _endPulse = NewImage(_marks, "_endPulse", C("#1D3143"));
        _endPulse.sprite = TimeUiSprites.Disc();
        Place(_endPulse.rectTransform, epv, new Vector2(44f, 44f), 0f);

        _endDash = NewImage(_marks, "_endDash", EndDash);
        _endDash.sprite = TimeUiSprites.Ring(68f, 2f);
        Place(_endDash.rectTransform, epv, new Vector2(68f, 68f), 0f);

        _endSpin = NewRect("_endSpin", _marks);
        Center(_endSpin, epv, new Vector2(48f, 48f));
        var d1 = NewImage(_endSpin, "_d1", CoreOk0); d1.sprite = TimeUiSprites.Ring(48f, 3f);
        Center(d1.rectTransform, Vector2.zero, new Vector2(48f, 48f));
        var d2 = NewImage(_endSpin, "_d2", CoreOk0); d2.sprite = TimeUiSprites.Ring(26f, 2f);
        Center(d2.rectTransform, Vector2.zero, new Vector2(26f, 26f));
        _endDiamond = new Graphic[] { d1, d2 };
    }

    void BuildSegments()
    {
        _segs = new Image[N * N];
        for (int i = 0; i < _segs.Length; i++)
        {
            var s = NewImage(_frame, "_seg", SegEmpty);
            Place(s.rectTransform, new Vector2(SegX0 + i * SegStep, SegY), new Vector2(12f, 4f), 0f);
            _segs[i] = s;
        }
    }

    static void CornerTicks(RectTransform target, float w, float h, Color col, float len, float thick)
    {
        float hw = w * .5f, hh = h * .5f;
        for (int i = 0; i < 4; i++)
        {
            float sx = (i == 0 || i == 2) ? -1f : 1f, sy = (i < 2) ? 1f : -1f;
            var a = NewImage(target, "_tick", col);
            Place(a.rectTransform, new Vector2(sx * (hw - len * .5f), sy * (hh - thick * .5f)), new Vector2(len, thick), 0f);
            var b = NewImage(target, "_tick", col);
            Place(b.rectTransform, new Vector2(sx * (hw - thick * .5f), sy * (hh - len * .5f)), new Vector2(thick, len), 0f);
        }
    }

    // ── 좌표 · 색 도구 ────────────────────────────────────────────────
    static readonly Vector2Int[] Dirs =
    { new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1) };

    // 목업의 격자 좌표를 그대로 옮긴 것. 행이 늘면 아래로 내려간다.
    static Vector2 CellPos(Vector2Int v) => new Vector2(-180f + Step * v.y, 176f - Step * v.x);

    static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, a);
    static Color Blend(Color fg, Color bg, float a) => Color.Lerp(bg, fg, a);

    static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static Image NewImage(RectTransform parent, string name, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        return img;
    }

    static void Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size, float rotZ)
    {
        Center(rt, pos, size);
        rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(.5f, .5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
