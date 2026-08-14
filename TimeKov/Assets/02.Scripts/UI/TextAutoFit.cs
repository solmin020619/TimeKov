// =====================================================================
// TextAutoFit.cs
// 번역 때문에 글자가 상자를 넘치는 문제를 전역에서 자동으로 막는다.
//
// [왜 필요한가]
//   한국어 대비 폭이 영어 2.2배 / 프랑스어 3.1배로 늘어난다(로컬라이징 시트 실측).
//   짧은 라벨은 6~11배까지 간다("헬뱃" -> "Hellbat"). 그런데 씬+프리팹 TMP 1,213개 중
//   자동 크기조절이 켜진 건 3개뿐이고 1,209개가 Overflow(그냥 삐져나감)다.
//   라벨마다 손으로 고치면 언어를 추가할 때마다 처음부터 다시 해야 한다.
//
// [어떻게 잡나]
//   TMP 는 글자를 다시 그릴 때마다 TEXT_CHANGED_EVENT 를 쏜다. 씬이든 프리팹이든
//   런타임 생성이든 예외가 없다. 여기 하나만 물리면 프로젝트 전체가 커버된다.
//
// [설계 메모]
//   1) ★한 프레임 미뤄서 처리한다. TEXT_CHANGED 는 글자를 다시 그리는 도중에 오고,
//      유니티의 레이아웃 재계산(CanvasUpdateRegistry)은 LateUpdate '뒤'
//      Canvas.willRenderCanvases 에서 돈다. 같은 프레임에 재면 상자가 아직
//      안 늘어난 상태라 "안 들어간다"고 오판해서, 상자가 넓어졌는데도 글자를
//      줄이고 말줄임까지 박는 일이 생긴다(실제로 겪음). 다음 프레임엔 다 끝나 있다.
//   2) 원래 글자 크기를 따로 저장하지 않는다. 처음 켤 때 fontSizeMax 에 넣어두고
//      그 뒤로는 그걸 원래 크기로 읽는다. 딕셔너리를 쓰면 씬 전환에서 어긋난다.
//      덕분에 몇 번 돌려도 결과가 같아서(멱등) 재진입 방지 플래그가 필요 없다.
//   3) ★되돌릴 수 있어야 한다. 상자가 나중에 넓어지거나 글자가 짧아지면
//      원래 크기로 복구한다. 한 방향으로만 줄이면 한 번 잘못 판정한 게 영구히 남는다.
//   4) 이미 알아서 맞는 라벨은 건드리지 않는다(ShouldSkip).
//   5) 씬에 아무것도 안 놓는다. RuntimeInitializeOnLoadMethod 로 스스로 뜬다.
//
// [이 시스템이 못 잡는 것]
//   '제 상자는 안 넘쳤는데 옆 아이콘과 겹치는' 경우. 라벨 rect 가 원래부터 아이콘
//   자리까지 뻗어 있고 한국어가 짧아 안 보였을 뿐이라, 라벨 입장에선 넘친 게 아니다.
//   그건 컨테이너를 HorizontalLayoutGroup 등으로 고쳐야 한다.
//   -> 그런 곳은 [가려짐] 진단(LogOverlaps, 기본 ON)이 콘솔에 목록으로 찍어준다.
//
// [끄는 법] TextAutoFit.Enabled = false  (또는 라벨에 TextAutoFitIgnore 부착)
// =====================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TextAutoFit : MonoBehaviour
{
    /// <summary>전체 끄기. 문제가 생기면 이거부터 false 로 두고 원인을 좁힌다.</summary>
    public static bool Enabled = true;

    /// <summary>줄이거나 되돌린 라벨을 콘솔에 찍는다. 어떤 문구가 넘치는지 훑을 때만 켠다.
    /// ★평소엔 꺼둔다 - 글자가 바뀔 때마다 찍혀서 콘솔이 도배되고 정작 볼 경고가 묻힌다.</summary>
    public static bool LogAdjustments = false;

    /// <summary>★[확실] 글자가 자기 상자 밖으로 나갔거나 잘린 것을 찍는다.
    /// 좌표/TMP 내부값으로 판정하는 '측정된 사실'이라 뜨면 실제 문제다. 기본 ON.</summary>
    public static bool LogSpill = true;

    /// <summary>[가려짐] 라벨 잉크 위에 '나중에 그려지는' 형제가 실제로 덮이는 곳을 찍는다.
    /// 글자를 줄여도 안 풀리는, 컨테이너를 고쳐야 하는 곳의 목록이다.
    /// 예전에 오탐이 심해 꺼뒀었는데 3대 원인이 전부 제거돼 켠다(08-08):
    ///   (1)UI 가 두 벌이라 자기 쌍둥이와 겹치던 것 - 중복 정리로 소멸
    ///   (2)알파 0 으로 숨긴 라벨이 검사를 타던 것 - IsVisible 게이트 추가
    ///   (3)꺼진 패널의 그래픽이 '덮는 쪽'으로 잡히던 것 - 상대편도 그룹 알파까지 확인
    /// 그래도 그리기 순서 규칙에 기대는 만큼 넘침/잘림보다 한 단계 추정이 섞여 있다.
    /// 오탐이 보이면 규칙을 조이든가 도로 끈다.</summary>
    public static bool LogOverlaps = true;

    /// <summary>줄일 수 있는 하한(원래 크기 대비). 0.7 = 최대 30%까지만 작아진다.</summary>
    public static float MinScale = 0.7f;

    // 글자가 바뀐 라벨을 모았다가 '다음' 프레임에 처리한다(설계 메모 1번).
    private static readonly HashSet<TMP_Text> _incoming = new();   // 이번 프레임에 들어온 것
    private static readonly HashSet<TMP_Text> _pending  = new();   // 지난 프레임에 들어온 것 = 지금 처리할 것
    private static readonly List<TMP_Text> _work = new();
    private static readonly HashSet<int> _overlapReported = new();  // 겹침 로그 중복 방지
    private static readonly HashSet<int> _spillReported = new();    // 넘침/잘림 로그 중복 방지

    // ★줄였을 당시의 (상자 크기, 글자) 기록. 조건이 그대로면 복구를 시도조차 안 한다.
    //   복구 판정은 '원래 크기로 폈을 때'를 비례로 어림하는데, 줄바꿈 글자는 크게 하면
    //   줄 수가 늘어서 이 어림이 낙관적으로 틀린다. 그 결과 줄임<->복구가 매 프레임
    //   반복되며 글자가 위아래로 떨렸다(도감 아이템 이름에서 실제 발생). 상자나 글자가
    //   바뀌었을 때만 다시 시도하면 최악에도 한 번 출렁이고 멈춘다.
    private static readonly Dictionary<int, (Vector2 size, int text)> _shrunkAt = new();

    // 겹침 검사 대상 수집용(할당 재사용). MaxProbe = 한 라벨당 볼 그래픽 상한 - 진단이 프레임을 먹지 않게.
    private const int MaxProbe = 48;
    private static readonly List<Graphic> _probe = new();
    private static readonly List<Graphic> _inner = new();

    // 넘침 판정 여유. 반올림 오차로 매 프레임 껐다 켜지는 걸 막는다.
    private const float Slack = 0.5f;
    // 되돌릴 때는 더 넉넉히 본다. 줄임<->되돌림이 매 프레임 번갈아 도는 걸 막는 이력(hysteresis).
    private const float RestoreSlack = 4f;

    // ★플레이 재진입 대비. 도메인 리로드를 끄면 static 이 살아남아
    //   (a) 줄임/겹침 기록이 남아 두 번째 플레이부터 진단이 안 뜨고
    //   (b) 아래 Boot 이 또 돌아 펌프가 중복 생성돼 같은 라벨을 두 번 처리한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        _incoming.Clear(); _pending.Clear(); _work.Clear();
        _overlapReported.Clear(); _spillReported.Clear(); _shrunkAt.Clear();
        _booted = false;
    }

    private static bool _booted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Enabled || _booted) return;
        _booted = true;
        var go = new GameObject("[TextAutoFit]") { hideFlags = HideFlags.HideAndDontSave };
        go.AddComponent<TextAutoFit>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()  => TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
    private void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

    private static void OnTextChanged(Object obj)
    {
        if (!Enabled) return;
        if (obj is TMP_Text t) _incoming.Add(t);
    }

    private void LateUpdate()
    {
        // 지난 프레임에 들어온 것만 처리한다. 그 사이에 레이아웃 재계산이 끝나 있다.
        if (_pending.Count > 0)
        {
            _work.Clear();
            _work.AddRange(_pending);
            _pending.Clear();
            for (int i = 0; i < _work.Count; i++) Fit(_work[i]);
        }

        if (_incoming.Count > 0)
        {
            foreach (var t in _incoming) _pending.Add(t);
            _incoming.Clear();
        }
    }

    private static void Fit(TMP_Text tmp)
    {
        if (tmp == null || !tmp.isActiveAndEnabled) return;

        RectTransform rt0 = tmp.rectTransform;
        if (rt0 == null) return;

        // ★겹침 '진단'은 크기조절 로직보다 먼저, 그리고 독립적으로 돈다.
        //   예전엔 ShouldSkip 뒤에 있어서 레이아웃이 크기를 정하는 라벨과 키캡류가
        //   진단에서 통째로 빠졌다. 겹침은 글자를 줄일 수 있는지와 아무 상관이 없는데도.
        //   (우주선 수리 제목이 pip 과 겹친 건을 이 순서 때문에 놓쳤다)
        if (rt0.rect.width > 1f && rt0.rect.height > 1f)
        {
            if (LogSpill) ReportSpill(tmp, rt0);
            if (LogOverlaps) ReportOverlap(tmp, rt0);
        }

        if (ShouldSkip(tmp, out RectTransform rt)) return;

        Rect r = rt.rect;
        if (r.width <= 1f || r.height <= 1f) return;   // 아직 레이아웃 안 잡힘

        // ★글자 하나도 못 담는 상자는 '일부러 넘치게' 만든 디자인이다.
        //   키캡(B/Q/E)이 대표 - 글자 상자를 10x10 으로 두고 Overflow 로 그리게 돼 있다.
        //   이런 걸 줄이고 말줄임까지 걸면 글자가 통째로 사라진다(HUD B 키 증발 사고).
        //   넘침을 보는 축과 같은 축으로만 잰다: 한줄(NoWrap)은 가로, 줄바꿈은 세로(한 줄 높이).
        float authored = tmp.enableAutoSizing ? tmp.fontSizeMax : tmp.fontSize;
        bool tinyBox = tmp.textWrappingMode == TextWrappingModes.NoWrap
            ? r.width  < authored
            : r.height < authored;
        if (tinyBox) return;

        bool over = Overflow(tmp, r, Slack);

        if (!over)
        {
            TryRestore(tmp, r);
            return;
        }

        // 1단계 - 자동 크기조절을 켠다. 이때 현재 크기를 상한으로 박아두면
        //   그 값이 곧 '원래 크기' 기억장치가 된다(다음 패스에서 다시 안 덮어씀).
        if (!tmp.enableAutoSizing)
        {
            float baseSize = tmp.fontSize;
            tmp.fontSizeMax = baseSize;
            tmp.fontSizeMin = Mathf.Max(1f, baseSize * MinScale);
            tmp.enableAutoSizing = true;
            _shrunkAt[tmp.GetInstanceID()] = (new Vector2(r.width, r.height), TextHash(tmp));
            if (LogAdjustments)
                Debug.Log($"[TextAutoFit] 축소 '{Trim(tmp.text)}' ({Path(tmp)}) {baseSize:0.#} -> 최소 {tmp.fontSizeMin:0.#}");
            return;   // 다시 그려진 뒤 다음 패스에서 결과를 확인한다
        }

        // ★[08-14] 하한까지 줄여도 안 들어갈 때 말줄임을 걸던 로직을 없앴다(종욱 지시).
        //   말줄임은 고친 게 아니다 - 글자가 안 보이면 넘치는 것과 똑같이 버그인데,
        //   화면상 '깔끔해 보여서' 아무도 신고하지 않고 로그도 안 뜬다(진단이 자기가 만든
        //   결과를 보고할 리 없다). 메인메뉴 'Commencer la partie' 가 그렇게 묻혀 있었다.
        //   넘치게 두면 눈에 띄고 [넘침] 으로 보고되어 상자를 고칠 기회가 생긴다.
    }

    // 상자가 넓어졌거나 글자가 짧아졌으면 원래 크기로 되돌린다.
    //   ★이게 없으면 한 번 잘못 판정한 라벨이 영원히 작은 채로 남는다.
    private static void TryRestore(TMP_Text tmp, Rect r)
    {
        if (!tmp.enableAutoSizing) return;
        if (tmp.fontSizeMax <= 0f) return;

        // ★줄였을 때와 상자/글자가 그대로면 시도하지 않는다(클래스 상단 _shrunkAt 주석 참고).
        int id = tmp.GetInstanceID();
        int hash = TextHash(tmp);
        var now = new Vector2(r.width, r.height);
        if (_shrunkAt.TryGetValue(id, out var at) && at.size == now && at.text == hash) return;

        // 지금은 줄어든 크기로 재고 있다. 원래 크기로 폈을 때의 폭/높이를 비례로 환산해 본다.
        float cur = Mathf.Max(0.01f, tmp.fontSize);
        float scale = tmp.fontSizeMax / cur;
        bool wouldFit = tmp.textWrappingMode == TextWrappingModes.NoWrap
            ? tmp.preferredWidth  * scale <= r.width  - RestoreSlack
            : tmp.preferredHeight * scale <= r.height - RestoreSlack;
        if (!wouldFit)
        {
            _shrunkAt[id] = (now, hash);   // 조건은 바뀌었는데 여전히 안 들어감 - 새 조건을 기록해 재시도 차단
            return;
        }

        float back = tmp.fontSizeMax;
        tmp.enableAutoSizing = false;
        tmp.fontSize = back;
        if (tmp.overflowMode == TextOverflowModes.Ellipsis) tmp.overflowMode = TextOverflowModes.Overflow;
        _shrunkAt.Remove(id);
        if (LogAdjustments)
            Debug.Log($"[TextAutoFit] 원래 크기로 복구 '{Trim(tmp.text)}' ({Path(tmp)}) -> {back:0.#}");
    }

    private static int TextHash(TMP_Text tmp)
        => string.IsNullOrEmpty(tmp.text) ? 0 : tmp.text.GetHashCode();

    // 넘쳤는가. 줄바꿈 여부에 따라 봐야 할 축이 다르다.
    //   NoWrap  : 무조건 한 줄이므로 가로만 본다(preferredWidth = 한 줄로 폈을 때 폭).
    //   줄바꿈 O : 가로는 알아서 접히니 의미가 없다. 접힌 결과 높이만 본다.
    private static bool Overflow(TMP_Text tmp, Rect r, float slack)
    {
        return tmp.textWrappingMode == TextWrappingModes.NoWrap
            ? tmp.preferredWidth  > r.width  + slack
            : tmp.preferredHeight > r.height + slack;
    }

    private static bool ShouldSkip(TMP_Text tmp, out RectTransform rt)
    {
        rt = tmp.rectTransform;
        if (rt == null) return true;

        if (tmp.GetComponent<TextAutoFitIgnore>() != null) return true;

        // 스크롤 뷰/마스킹/페이지 넘김은 넘치는 게 정상 동작이다.
        var m = tmp.overflowMode;
        if (m == TextOverflowModes.Masking || m == TextOverflowModes.ScrollRect ||
            m == TextOverflowModes.Page    || m == TextOverflowModes.Linked) return true;

        // ★상자가 글자에 맞춰 늘어나는 구조면 글자를 줄이는 게 오히려 손해다.
        //   상단 배너처럼 '알아서 잘 되는' 것들이 여기 걸린다.
        var csf = tmp.GetComponent<ContentSizeFitter>();
        if (csf != null && csf.enabled)
        {
            bool grows = tmp.textWrappingMode == TextWrappingModes.NoWrap
                ? csf.horizontalFit != ContentSizeFitter.FitMode.Unconstrained
                : csf.verticalFit   != ContentSizeFitter.FitMode.Unconstrained;
            if (grows) return true;
        }

        // ★부모 LayoutGroup 이 글자 상자 크기를 직접 정하는 경우도 같은 이유로 제외.
        //   레이아웃이 preferredWidth 를 보고 상자를 다시 늘려주는데, 그 재계산과 우리의
        //   측정이 서로 쫓아가며 어긋난다(영상 팝업 하단 안내줄에서 실제 발생).
        var lg = rt.parent != null ? rt.parent.GetComponent<LayoutGroup>() : null;
        if (lg != null && lg.enabled && lg is HorizontalOrVerticalLayoutGroup hv)
        {
            bool driven = tmp.textWrappingMode == TextWrappingModes.NoWrap
                ? hv.childControlWidth
                : hv.childControlHeight;
            if (driven) return true;
        }
        return false;
    }

    // ── 겹침 진단 ────────────────────────────────────────────────────────
    // 라벨이 '자기 위에 그려지는 형제'와 겹치는지 본다. 겹치면 글자를 줄여도 안 풀린다
    //   (라벨 입장에선 안 넘쳤으므로). 컨테이너를 고쳐야 하는 곳의 목록을 만드는 용도다.
    //
    // ★rect 가 아니라 '실제로 그려진 글자 영역'으로 비교한다. rect 로 비교하면
    //   "왼쪽정렬 짧은 라벨(rect 는 전체폭) + 오른쪽에 놓인 형제" 같은 정상 배치가
    //   전부 겹침으로 잡힌다(도감 DROPS/RECIPES 라벨이 87~99% 로 오탐났다).
    //   눈에 보이는 충돌만 잡아야 목록이 쓸모 있다.
    private static void ReportOverlap(TMP_Text tmp, RectTransform rt)
    {
        var parent = rt.parent as RectTransform;
        if (parent == null) return;

        // ★빈 라벨은 그리는 게 없으니 겹칠 수도 없다. 이걸 안 막으면 아래 InkRect 가
        //   '글자 경계 0 -> 상자 전체' 폴백을 타서, 스킬 슬롯의 숨은 쿨타임 숫자 같은
        //   빈 라벨이 아이콘/링과 100% 겹친 것으로 보고된다(전부 오탐이었다).
        if (string.IsNullOrWhiteSpace(tmp.text)) return;

        // ★안 보이는 라벨도 마찬가지다. 넘침 검사에는 이 게이트가 있는데 여기엔 없어서,
        //   알파 0 으로 숨겨둔 카테고리 탭 이름 같은 것이 겹침으로만 계속 오탐났다.
        if (!IsVisible(tmp)) return;

        // 나도 마스크 안이면 잘린 만큼만 보인다. 안 보이는 부분이 덮이는 건 겹침이 아니다.
        Rect mine = ClipByMasks(rt, InkRect(rt, tmp));
        if (mine.width <= 1f) return;

        // ★'나보다 뒤에 그려지는 형제'만 본다. 앞에 그려지는 건 내 글자 아래 깔리는
        //   배경/장식이라 겹치는 게 정상이다(버튼 배경, 제목 후광, 배경 오로라...).
        //   한때 이 제한을 없앴더니 사망 화면 한 곳에서만 오탐이 6건 나왔다.
        //   가려지는 쪽은 '위에 덮는 것'뿐이므로 순서는 반드시 봐야 한다.
        int myIndex = rt.GetSiblingIndex();
        _probe.Clear();
        for (int i = myIndex + 1; i < parent.childCount && _probe.Count < MaxProbe; i++)
        {
            var sib = parent.GetChild(i) as RectTransform;
            if (sib == null || sib == rt || !sib.gameObject.activeInHierarchy) continue;

            var g = sib.GetComponent<Graphic>();
            if (g != null) { _probe.Add(g); continue; }

            // ★그래픽이 없는 '빈 컨테이너'면 그 안에 실제로 그려지는 것들을 본다.
            //   컨테이너만 보고 넘기면 그 안의 내용과 겹쳐도 못 잡는다
            //   (우주선 수리의 pip 줄이 정확히 이 모양이라 진단을 통째로 빠져나갔다).
            sib.GetComponentsInChildren(false, _inner);
            for (int k = 0; k < _inner.Count && _probe.Count < MaxProbe; k++) _probe.Add(_inner[k]);
        }

        for (int i = 0; i < _probe.Count; i++)
        {
            var g = _probe[i];
            // 덮는 쪽도 실제로 그려지는 상태여야 한다. 자기 알파만 보면 꺼진
            // 패널(CanvasGroup 알파 0) 안의 그래픽이 덮는 것으로 잡힌다.
            if (g == null || !g.enabled || !GroupVisible(g.transform)) continue;

            // ★거의 투명한 장식은 글자를 가릴 수 없다. 카드 위를 지나가는 반짝임(sweep),
            //   후광, 스캔라인 같은 것들이다. 실측: 보상 카드의 rwSweep 은 알파 0.06 인데
            //   제목을 48% 덮는다고 보고됐다 - 화면에선 글자가 멀쩡히 다 보인다.
            //   글자끼리는 흐려도 읽히니 기존 기준(0.01)을 유지한다.
            float minAlpha = (g is TMP_Text) ? 0.01f : 0.2f;
            if (g.color.a <= minAlpha) continue;
            var srt = g.rectTransform;
            if (srt == null || srt.IsChildOf(rt)) continue;   // 내 장식(밑줄 등)은 겹침이 아니다

            Rect other = ClipByMasks(srt, InkRect(srt, g as TMP_Text));
            if (other.width <= 1f) continue;
            if (other.Contains(new Vector2(mine.xMin, mine.yMin)) &&
                other.Contains(new Vector2(mine.xMax, mine.yMax))) continue;   // 배경처럼 나를 감싸는 건 제외

            if (!mine.Overlaps(other)) continue;
            Rect inter = Rect.MinMaxRect(Mathf.Max(mine.xMin, other.xMin), Mathf.Max(mine.yMin, other.yMin),
                                         Mathf.Min(mine.xMax, other.xMax), Mathf.Min(mine.yMax, other.yMax));
            float mineArea = mine.width * mine.height;
            float ratio = (inter.width * inter.height) / mineArea;

            // ★글자와 이미지는 재는 기준이 다르다.
            //   글자는 잉크(글리프) 경계로 정확히 재지만, 이미지는 RectTransform 상자로 잰다.
            //   스프라이트 안의 투명 여백까지 상자에 포함되므로, 화면에서는 확실히 떨어져 있어도
            //   상자끼리는 닿는다. 실측 사례: 건축바 키캡 스프라이트의 오른쪽 여백이 25%(580px 중 146px)라
            //   'Quitter la vue' 가 7% 덮인다고 보고됐는데 눈으로는 한참 떨어져 있었다.
            //   그래서 이미지가 덮는 경우는 기준을 올리고, 실제로 파고든 폭도 함께 본다.
            bool otherIsText = g is TMP_Text;
            float minRatio = otherIsText ? 0.05f : 0.20f;
            float minBite  = otherIsText ? 0f    : 8f;     // 이 폭 미만은 스프라이트 여백으로 본다
            if (ratio < minRatio || inter.width < minBite) continue;

            int key = rt.GetInstanceID() ^ srt.GetInstanceID();
            if (!_overlapReported.Add(key)) continue;   // 이 조합만 건너뛴다(예전엔 여기서 통째로 빠져나갔다)
            Debug.LogWarning($"[TextAutoFit/가려짐] '{Trim(tmp.text)}' 위에 '{srt.name}' 이(가) {ratio:P0} 덮인다. "
                           + $"({Path(tmp)}) -> 이 줄은 컨테이너를 가로 레이아웃으로 바꾸거나 글자 폭만큼 옆을 밀어야 한다.");
        }

    }

    // ── [확실] 넘침/잘림 검사 ────────────────────────────────────────────
    // 추측이 하나도 없다. 재는 것은 두 가지뿐이고 둘 다 사실이다.
    //   (1) 실제로 그려진 글자(잉크)가 자기 상자를 벗어났는가 - 좌표 비교
    //   (2) 말줄임이 걸려 글자가 잘렸는가 - TMP 가 알려주는 값
    // '무엇이 배경인가' 같은 추측을 안 하므로, 여기 뜨는 건 눈으로 확인할 필요가 없다.
    private static void ReportSpill(TMP_Text tmp, RectTransform rt)
    {
        if (string.IsNullOrWhiteSpace(tmp.text)) return;
        if (tmp.GetComponent<TextAutoFitIgnore>() != null) return;
        if (!IsVisible(tmp)) return;   // 안 보이는 글자는 넘쳐도 화면에 아무 일이 없다

        // 넘치는 게 정상인 표시 방식(스크롤/마스킹/페이지)과, 상자가 글자를 따라 늘어나는 구조는 제외.
        var m = tmp.overflowMode;
        if (m == TextOverflowModes.Masking || m == TextOverflowModes.ScrollRect ||
            m == TextOverflowModes.Page    || m == TextOverflowModes.Linked) return;
        var csf = tmp.GetComponent<ContentSizeFitter>();
        if (csf != null && csf.enabled) return;

        // (2) 잘림 - 표시할 글자 수보다 실제로 그린 글자 수가 적으면 뒤가 날아간 것이다.
        var info = tmp.textInfo;
        if (info != null && tmp.overflowMode == TextOverflowModes.Ellipsis)
        {
            int shown = info.characterCount;
            int want  = tmp.text.Length;
            if (shown > 0 && want - shown > 2)   // 태그/공백 오차 여유
            {
                if (_spillReported.Add(rt.GetInstanceID()))
                    Debug.LogWarning($"[TextAutoFit/잘림] '{Trim(tmp.text)}' 이(가) 말줄임으로 잘렸다({shown}/{want}자). "
                                   + $"({Path(tmp)}) -> 상자를 넓히거나 문구를 줄여야 한다.");
                return;
            }
        }

        // (1) 상자 이탈 - 잉크가 자기 rect 를 넘어간 픽셀 수.
        //   상자 자체가 글자보다 작게 설계된 것(키캡 등)은 의도된 넘침이라 뺀다.
        Rect box = WorldRect(rt);
        Rect ink = InkRect(rt, tmp);
        if (ink.width <= 1f || box.width <= 1f) return;
        float authored = tmp.enableAutoSizing ? tmp.fontSizeMax : tmp.fontSize;
        float scale = rt.lossyScale.y <= 0.0001f ? 1f : rt.lossyScale.y;
        if (box.height < authored * scale) return;   // 글자 한 줄도 못 담는 상자 = 의도된 디자인

        float outL = Mathf.Max(0f, box.xMin - ink.xMin);
        float outR = Mathf.Max(0f, ink.xMax - box.xMax);
        float outX = outL + outR;
        if (outX <= box.width * 0.02f) return;       // 2% 미만은 글자 외곽선 수준

        // ★비율만 보면 좁은 상자에서 노이즈가 걸린다. 상자가 30px 이면 1px 만 나가도 3% 다.
        //   1.5px 미만은 화면에서 볼 수 없으니 보고하지 않는다 - 고칠 것도 없는데 목록만 늘린다.
        //   (메인메뉴 '제작진' 이 좌우 0px 인데 3% 로 잡힌 게 이 경우다)
        if (outX < 1.5f) return;

        if (!_spillReported.Add(rt.GetInstanceID())) return;
        Debug.LogWarning($"[TextAutoFit/넘침] '{Trim(tmp.text)}' 이(가) 자기 상자를 가로로 {outX / box.width:P0} 벗어났다"
                       + $"(왼쪽 {outL:0}px / 오른쪽 {outR:0}px). ({Path(tmp)}) -> 상자를 넓히거나 문구를 줄여야 한다.");
    }

    // ★마스크로 잘려 나간 부분은 화면에 안 그려진다. rect 만 보면 '겹친다'가 되지만 실제로는 안 보인다.
    //   스크롤 목록이 헤더 밑을 지나갈 때가 대표적이다 - 설정창에서 키설정 행이 창 제목을
    //   58% 덮는다고 나온 게 이 경우였다(행은 뷰포트 RectMask2D 안이라 잘려 있었다).
    //   부모를 타고 올라가며 마스크 영역과 계속 교집합을 낸다(마스크가 겹쳐 있어도 맞는다).
    private static Rect ClipByMasks(RectTransform rt, Rect r)
    {
        var t = rt != null ? rt.parent as RectTransform : null;
        while (t != null)
        {
            bool clips = false;
            var rm = t.GetComponent<RectMask2D>();
            if (rm != null && rm.enabled) clips = true;
            else
            {
                var m = t.GetComponent<Mask>();
                if (m != null && m.enabled && m.graphic != null) clips = true;
            }

            if (clips)
            {
                Rect c = WorldRect(t);
                r = Rect.MinMaxRect(Mathf.Max(r.xMin, c.xMin), Mathf.Max(r.yMin, c.yMin),
                                    Mathf.Min(r.xMax, c.xMax), Mathf.Min(r.yMax, c.yMax));
                if (r.width <= 0f || r.height <= 0f) return new Rect(0f, 0f, 0f, 0f);
            }
            t = t.parent as RectTransform;
        }
        return r;
    }

    private static Rect WorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    // 실제로 잉크가 찍힌 영역. 텍스트면 글자 경계(textBounds), 이미지면 rect 그대로.
    //   textBounds 는 정렬까지 반영된 로컬 좌표라, 왼쪽정렬 짧은 글자는 rect 가 넓어도
    //   왼쪽 조각만 돌려준다. 그래서 정상 배치를 겹침으로 오인하지 않는다.
    private static Rect InkRect(RectTransform rt, TMP_Text tmp)
    {
        if (tmp == null) return WorldRect(rt);
        // 글자가 없는 TMP 는 '상자 전체' 로 폴백하면 안 된다. 안 그리는 것을 크게 잡아
        // 겹침으로 오인한다(상대편으로 걸릴 때도 마찬가지).
        if (string.IsNullOrWhiteSpace(tmp.text)) return Rect.zero;
        var b = tmp.textBounds;
        if (b.size.x <= 0.01f || b.size.y <= 0.01f) return WorldRect(rt);
        Vector3 a = rt.TransformPoint(new Vector3(b.min.x, b.min.y, 0f));
        Vector3 c = rt.TransformPoint(new Vector3(b.max.x, b.max.y, 0f));
        return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y),
                               Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
    }

    // ★'화면에 실제로 보이는 글자'인지. 안 보이는 것을 재면 전부 헛수고이자 오탐이다.
    //   대표 사례: 카테고리 필터 탭은 선택된 하나만 이름을 펼치고 나머지는 알파 0 으로 숨긴다.
    //   그 숨은 라벨들이 좁은 아이콘 상자를 넘친다고 보고돼 인벤토리를 열 때마다 5줄이 떴다.
    //   글자 자체의 알파와, 패널을 통째로 숨기는 CanvasGroup 둘 다 본다.
    private static bool IsVisible(TMP_Text tmp)
        => tmp.color.a > 0.01f && GroupVisible(tmp.transform);

    // CanvasGroup 사슬로 통째로 숨겨져 있지 않은가(패널 페이드/숨김 대응).
    // 라벨 쪽(IsVisible)과 덮는 쪽(겹침 상대) 양쪽이 같이 쓴다.
    private static bool GroupVisible(Transform t)
    {
        var cg = t.GetComponentInParent<CanvasGroup>();
        while (cg != null)
        {
            if (cg.alpha <= 0.01f) return false;
            if (cg.ignoreParentGroups) break;
            var parent = cg.transform.parent;
            cg = parent != null ? parent.GetComponentInParent<CanvasGroup>() : null;
        }
        return true;
    }

    private static string Trim(string s)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= 24 ? s : s.Substring(0, 24) + "...");

    private static string Path(TMP_Text tmp)
    {
        var t = tmp.transform;
        string p = t.name;
        for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
