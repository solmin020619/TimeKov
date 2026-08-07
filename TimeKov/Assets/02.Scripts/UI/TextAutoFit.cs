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
//   -> LogOverlaps = true 로 켜면 그런 곳을 전부 찾아서 콘솔에 목록으로 찍어준다.
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

    /// <summary>줄이거나 되돌린 라벨을 콘솔에 찍는다. 어떤 문구가 넘치는지 훑을 때만 켠다.</summary>
    public static bool LogAdjustments = true;

    /// <summary>★옆 형제와 겹치는 라벨을 찾아 콘솔에 찍는다. 이 시스템이 못 고치는 종류라
    /// 목록을 뽑아 컨테이너를 수동으로 고치는 용도다. 한 라벨당 한 번만 찍는다.</summary>
    public static bool LogOverlaps = true;

    /// <summary>줄일 수 있는 하한(원래 크기 대비). 0.7 = 최대 30%까지만 작아진다.</summary>
    public static float MinScale = 0.7f;

    // 글자가 바뀐 라벨을 모았다가 '다음' 프레임에 처리한다(설계 메모 1번).
    private static readonly HashSet<TMP_Text> _incoming = new();   // 이번 프레임에 들어온 것
    private static readonly HashSet<TMP_Text> _pending  = new();   // 지난 프레임에 들어온 것 = 지금 처리할 것
    private static readonly List<TMP_Text> _work = new();
    private static readonly HashSet<int> _overlapReported = new();  // 겹침 로그 중복 방지

    // 넘침 판정 여유. 반올림 오차로 매 프레임 껐다 켜지는 걸 막는다.
    private const float Slack = 0.5f;
    // 되돌릴 때는 더 넉넉히 본다. 줄임<->되돌림이 매 프레임 번갈아 도는 걸 막는 이력(hysteresis).
    private const float RestoreSlack = 4f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Enabled) return;
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
        if (ShouldSkip(tmp, out RectTransform rt)) return;

        Rect r = rt.rect;
        if (r.width <= 1f || r.height <= 1f) return;   // 아직 레이아웃 안 잡힘

        if (LogOverlaps) ReportOverlap(tmp, rt);

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
            if (LogAdjustments)
                Debug.Log($"[TextAutoFit] 축소 '{Trim(tmp.text)}' ({Path(tmp)}) {baseSize:0.#} -> 최소 {tmp.fontSizeMin:0.#}");
            return;   // 다시 그려진 뒤 다음 패스에서 결과를 확인한다
        }

        // 2단계 - 하한까지 줄였는데도 넘치면 말줄임으로 자른다. 더 할 수 있는 게 없다.
        if (tmp.fontSize <= tmp.fontSizeMin + 0.01f && tmp.overflowMode == TextOverflowModes.Overflow)
        {
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            if (LogAdjustments)
                Debug.LogWarning($"[TextAutoFit] 하한까지 줄여도 안 들어가 말줄임 '{Trim(tmp.text)}' ({Path(tmp)}) "
                               + $"상자 {r.width:0}x{r.height:0}");
        }
    }

    // 상자가 넓어졌거나 글자가 짧아졌으면 원래 크기로 되돌린다.
    //   ★이게 없으면 한 번 잘못 판정한 라벨이 영원히 작은 채로 남는다.
    private static void TryRestore(TMP_Text tmp, Rect r)
    {
        if (!tmp.enableAutoSizing) return;
        if (tmp.fontSizeMax <= 0f) return;

        // 지금은 줄어든 크기로 재고 있다. 원래 크기로 폈을 때의 폭/높이를 비례로 환산해 본다.
        float cur = Mathf.Max(0.01f, tmp.fontSize);
        float scale = tmp.fontSizeMax / cur;
        bool wouldFit = tmp.textWrappingMode == TextWrappingModes.NoWrap
            ? tmp.preferredWidth  * scale <= r.width  - RestoreSlack
            : tmp.preferredHeight * scale <= r.height - RestoreSlack;
        if (!wouldFit) return;

        float back = tmp.fontSizeMax;
        tmp.enableAutoSizing = false;
        tmp.fontSize = back;
        if (tmp.overflowMode == TextOverflowModes.Ellipsis) tmp.overflowMode = TextOverflowModes.Overflow;
        if (LogAdjustments)
            Debug.Log($"[TextAutoFit] 원래 크기로 복구 '{Trim(tmp.text)}' ({Path(tmp)}) -> {back:0.#}");
    }

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
        int myIndex = rt.GetSiblingIndex();
        Rect mine = InkRect(rt, tmp);
        if (mine.width <= 1f) return;

        for (int i = myIndex + 1; i < parent.childCount; i++)   // 나보다 위에 그려지는 것만
        {
            var sib = parent.GetChild(i) as RectTransform;
            if (sib == null || !sib.gameObject.activeInHierarchy) continue;
            var g = sib.GetComponent<Graphic>();
            if (g == null || !g.enabled || g.color.a <= 0.01f) continue;

            Rect other = InkRect(sib, g as TMP_Text);
            if (other.width <= 1f) continue;
            if (other.Contains(new Vector2(mine.xMin, mine.yMin)) &&
                other.Contains(new Vector2(mine.xMax, mine.yMax))) continue;   // 배경처럼 나를 감싸는 건 제외

            if (!mine.Overlaps(other)) continue;
            Rect inter = Rect.MinMaxRect(Mathf.Max(mine.xMin, other.xMin), Mathf.Max(mine.yMin, other.yMin),
                                         Mathf.Min(mine.xMax, other.xMax), Mathf.Min(mine.yMax, other.yMax));
            float ratio = (inter.width * inter.height) / (mine.width * mine.height);
            if (ratio < 0.05f) continue;   // 살짝 스치는 건 무시

            int key = rt.GetInstanceID() ^ sib.GetInstanceID();
            if (!_overlapReported.Add(key)) return;
            Debug.LogWarning($"[TextAutoFit/겹침] '{Trim(tmp.text)}' 이(가) '{sib.name}' 과 {ratio:P0} 겹친다. "
                           + $"({Path(tmp)}) -> 이 줄은 컨테이너를 가로 레이아웃으로 바꿔야 한다.");
            return;
        }
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
        var b = tmp.textBounds;
        if (b.size.x <= 0.01f || b.size.y <= 0.01f) return WorldRect(rt);
        Vector3 a = rt.TransformPoint(new Vector3(b.min.x, b.min.y, 0f));
        Vector3 c = rt.TransformPoint(new Vector3(b.max.x, b.max.y, 0f));
        return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y),
                               Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
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
