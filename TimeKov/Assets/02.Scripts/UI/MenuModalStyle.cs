// =====================================================================
// MenuModalStyle.cs
// 메인메뉴 창(월드 생성 / 월드 삭제 / 게임 종료)이 공유하는 겉모습.
//
// [이 파일은 글자를 만들지 않는다]
//   문구는 전부 씬 오브젝트여야 한다. 팀원이 씬을 훑어 번역할 문구를 모으기 때문에,
//   코드가 만든 라벨은 그 수집에서 통째로 빠진다. 그래서 여기서 하는 일은
//   "이미 씬에 있는 것의 색·크기·위치"와 "글자가 아닌 장식(테두리·모서리 눈금)"뿐이다.
//
// [규격]  하단 액션 바(삭제 / 게임 시작)에서 역산한 값들.
//   딤 80% + 짙은 남색 상자 + 모서리 눈금 + 어두운 제목 띠 +
//   220×52 버튼 판 + 222×54 테두리 + 버튼마다 모서리 눈금 8개.
//   색을 바꿀 일이 생기면 여기 한 곳만 고치면 세 창이 같이 따라온다.
// =====================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class MenuModalStyle
{
    // ── 색 ────────────────────────────────────────────────────────────
    // 딤은 원래 0.55~0.6 이었는데 뒤 화면이 그대로 읽혀서 "확인을 받는 창"으로
    // 안 보였다. 뒤가 형체만 남을 정도로 눌러 둔다.
    public static readonly Color Backdrop    = new Color(0f, 0f, 0f, 0.80f);
    public static readonly Color Box         = new Color(0.039f, 0.063f, 0.094f, 1f);   // #0A1018
    public static readonly Color BoxTick     = new Color(1f, 1f, 1f, 0.471f);
    public static readonly Color Sep         = new Color(1f, 1f, 1f, 0.196f);

    // 제목 띠 — 회색 형광펜 같던 단색판(#5A5A5A)에서, 화면의 다른 요소와 같은 문법
    // (어두운 판 + 얇은 테두리 + 눈금)으로 바꾼다.
    public static readonly Color Strip       = new Color(0.086f, 0.106f, 0.137f, 1f);   // #161B23
    public static readonly Color StripBorder = new Color(1f, 1f, 1f, 0.28f);

    // ★버튼은 ColorTint 로 밝기를 '곱해' 쓴다. uGUI 틴트는 어둡게만 만들 수 있어서,
    //   바탕색을 호버 상태로 잡고 평소에는 0.82 를 곱해 실제 표시색으로 내려앉힌다.
    public static readonly Color BtnFill       = new Color(0.468f, 0.488f, 0.517f, 1f);   // ×0.82 → #62666C
    // 되돌릴 수 없는 버튼(삭제 / 종료). 회색 버튼과 같은 무게로 보이도록 명도를 맞춘 붉은색이라
    // 눈에는 띄지만 화면 안에서 혼자 튀지는 않는다.
    public static readonly Color BtnFillDanger = new Color(0.669f, 0.268f, 0.234f, 1f);   // ×0.82 → #8C3831
    public static readonly Color BtnTick       = new Color(1f, 1f, 1f, 0.80f);
    public static readonly Color BorderQuiet   = new Color(1f, 1f, 1f, 0.35f);   // 부차 동작 — '삭제' 버튼과 같은 밝기
    public static readonly Color BorderMain    = new Color(1f, 1f, 1f, 0.50f);   // 주 동작   — '게임 시작' 버튼과 같은 밝기

    // ── 치수 ──────────────────────────────────────────────────────────
    public const float BtnW = 220f, BtnH = 52f, BtnGap = 24f;
    public const float StripH = 44f;

    // ── 글자 (크기·색만. 내용은 절대 안 건드린다) ──────────────────────
    public static readonly Color TextTitle = Color.white;
    public static readonly Color TextBody  = new Color(0.933f, 0.933f, 0.933f, 1f);   // #EEEEEE
    public static readonly Color TextSub   = new Color(0.604f, 0.604f, 0.604f, 1f);   // #9A9A9A
    public const float FontTitle = 23f, FontBody = 26f, FontSub = 21f;

    /// 상자 안쪽 여백 규칙 — 제목 띠·구분선·버튼의 세로 위치를 정한다.
    public const float StripInset = 46f, SepInset = 106f, BtnInset = 62f;

    /// 버튼 두 개를 나란히 놓을 때 가운데에서 떨어지는 거리.
    public const float BtnOffsetX = (BtnW + BtnGap) * 0.5f;

    // 눈금 이름은 씬 컨벤션을 따른다 — 액션 바 버튼(Btn_Delete/Btn_Enter)의 장식이 '_tick'.
    // 담는 그릇은 '_ticks'. 같은 대상을 두 번 칠해도 겹쳐 쌓이지 않게 하는 표식도 겸한다.
    const string TickHolder = "_ticks";
    const string TickName   = "_tick";

    // 상자 모서리 눈금은 씬에 이 이름들로 구워져 있다(CreateModal / QuitConfirmModal 공통).
    static readonly string[] Corners = { "TL", "TR", "BL", "BR" };

    // ==================================================================
    //  조각별 적용
    // ==================================================================

    /// <summary>딤 배경을 규격 색으로. 이름이 다르면 조용히 넘어간다.</summary>
    public static void ApplyBackdrop(Transform modalRoot, string childName = "Backdrop")
    {
        if (modalRoot == null) return;
        var img = modalRoot.Find(childName)?.GetComponent<Image>();
        if (img != null) img.color = Backdrop;
    }

    /// <summary>상자 판 색 + (크기를 주면) 크기까지. 모서리 눈금은 ReplaceBoxTicks 로 따로.</summary>
    public static void ApplyBox(RectTransform box, Vector2? size = null)
    {
        if (box == null) return;
        if (size.HasValue) box.sizeDelta = size.Value;
        var img = box.GetComponent<Image>();
        if (img != null) img.color = Box;
    }

    /// <summary>상자 모서리 눈금을 지금 상자 크기에 맞춰 다시 놓는다.
    /// 씬에 구워진 TickTL_H 등이 있으면 ★그것들을 제자리로 옮긴다 — 끄고 새로 그리면
    /// 죽은 오브젝트 8개가 계층에 영영 남는다. 하나라도 없으면 그때만 새로 만든다.</summary>
    public static void ApplyBoxTicks(RectTransform box, float len = 12f, float thick = 2f)
    {
        if (box == null) return;

        float hw = box.rect.width * 0.5f, hh = box.rect.height * 0.5f;

        foreach (var c in Corners)
        {
            var h = box.Find($"Tick{c}_H") as RectTransform;
            var v = box.Find($"Tick{c}_V") as RectTransform;
            if (h == null || v == null)
            {
                // 씬에 눈금이 없는 창이다(새로 만든 확인창 등). 코드로 그린다.
                AddTicks(box, box.rect.width, box.rect.height, BoxTick, len, thick);
                return;
            }

            float sx = c[1] == 'L' ? -1f : 1f;   // TL/BL = 왼쪽
            float sy = c[0] == 'T' ?  1f : -1f;  // TL/TR = 위

            Put(h, new Vector2(sx * (hw - len * 0.5f), sy * (hh - thick * 0.5f)), new Vector2(len, thick));
            Put(v, new Vector2(sx * (hw - thick * 0.5f), sy * (hh - len * 0.5f)), new Vector2(thick, len));
            Tint(h); Tint(v);
        }
    }

    static void Put(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Tint(RectTransform rt)
    {
        var img = rt.GetComponent<Image>();
        if (img != null) img.color = BoxTick;
    }

    /// <summary>제목 띠 — 어두운 판 + 1px 테두리 + 모서리 눈금. 크기를 주면 크기도 맞춘다.</summary>
    public static void ApplyStrip(RectTransform strip, Vector2? size = null, float? y = null)
    {
        if (strip == null) return;

        if (size.HasValue) strip.sizeDelta = size.Value;
        if (y.HasValue) strip.anchoredPosition = new Vector2(strip.anchoredPosition.x, y.Value);

        var img = strip.GetComponent<Image>();
        if (img != null) img.color = Strip;

        // 테두리는 확인창처럼 뒤에 큰 판을 깔 수도 있지만, 씬 오브젝트 사이에 끼워 넣으려면
        // 형제 순서를 건드려야 한다. 결과가 같은 Outline 으로 대신한다.
        // ★Outline 은 풀네임으로 쓴다 — 프로젝트에 UnityEngine.Outline(외곽선 셰이더용)이 따로 있다.
        var outline = strip.GetComponent<UnityEngine.UI.Outline>()
                      ?? strip.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = StripBorder;
        outline.effectDistance = new Vector2(1f, 1f);

        AddTicks(strip, strip.rect.width, strip.rect.height, BtnTick, 10f, thick: 2f);

        // 띠 위 제목 글자 — 크기·색·정렬만. ★.text 는 씬 것 그대로 둔다.
        ApplyText(strip.GetComponentInChildren<TMP_Text>(true), FontTitle, TextTitle, true);
    }

    /// <summary>글자의 크기·색·굵기·정렬만 맞춘다. 내용(.text)은 절대 건드리지 않는다 —
    /// 씬 라벨에는 LocalizedLabel 이 달려 있어서, 직접 쓰면 그때부터 번역을 놓는다.</summary>
    public static void ApplyText(TMP_Text t, float size, Color color, bool bold,
                                 TextAlignmentOptions? align = null)
    {
        if (t == null) return;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.alignment = align ?? TextAlignmentOptions.Center;
    }

    /// <summary>구분선.</summary>
    public static void ApplySep(RectTransform sep, float y, float width)
    {
        if (sep == null) return;
        sep.anchoredPosition = new Vector2(0f, y);
        sep.sizeDelta = new Vector2(width, 1f);
        var img = sep.GetComponent<Image>();
        if (img != null) img.color = Sep;
    }

    /// <summary>버튼 하나 — 판·테두리·눈금·호버 반응까지. border 는 없어도 된다.</summary>
    public static void ApplyButton(Button btn, RectTransform border, Vector2 pos,
                                   bool danger = false, bool primary = true)
    {
        if (btn == null) return;

        var rt = (RectTransform)btn.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(BtnW, BtnH);

        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = danger ? BtnFillDanger : BtnFill;
            btn.targetGraphic = img;
        }
        MoveBorderToOutline(img, border, primary ? BorderMain : BorderQuiet);

        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor      = new Color(0.82f, 0.82f, 0.82f, 1f);
        cb.highlightedColor = Color.white;
        cb.pressedColor     = new Color(0.62f, 0.62f, 0.62f, 1f);
        cb.selectedColor    = new Color(0.82f, 0.82f, 0.82f, 1f);
        cb.disabledColor    = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        cb.fadeDuration     = 0.12f;
        btn.colors = cb;

        AddTicks(rt, BtnW, BtnH, BtnTick, 10f, 2f);

        // 라벨 크기만 맞춘다. ★.text 는 절대 건드리지 않는다 —
        //   씬 라벨에는 LocalizedLabel 이 달려 있어서, 직접 쓰면 그때부터 번역을 놓는다.
        var label = btn.GetComponentInChildren<TMP_Text>(true);
        if (label != null) { label.fontSize = 23f; label.fontStyle = FontStyles.Bold; }
    }

    /// <summary>전면을 덮는 딤(Backdrop) 버튼을 '연출 없이 눌리기만' 하게 만든다.
    ///
    /// ★전역 눌림 연출(UIButtonPressInstaller)은 Button 이면 무엇이든 찾아 붙는다.
    ///   딤은 화면 전체를 덮는 Button 이라, 누르는 순간 화면이 통째로 줄었다 커진다
    ///   (창 뒤로 바탕이 비집고 나온다). 색 트랜지션도 마찬가지로 화면 전체를 물들인다.
    ///   딤이 할 일은 '뒤를 막고, 누르면 닫기' 뿐이다.</summary>
    public static void MakeBackdrop(Button btn)
    {
        if (btn == null) return;

        btn.transition = Selectable.Transition.None;

        // 이미 붙어 버린 경우(설치기가 먼저 훑고 지나갔다면)를 위해 떼어내고 크기도 되돌린다.
        var fx = btn.GetComponent<UIButtonPressEffect>();
        if (fx != null)
        {
            Object.Destroy(fx);
            btn.transform.localScale = Vector3.one;
        }
        if (btn.GetComponent<UIButtonPressEffectIgnore>() == null)
            btn.gameObject.AddComponent<UIButtonPressEffectIgnore>();
    }

    /// <summary>뒤에 깔아 둔 테두리 '판'을 버튼 자신의 Outline 으로 옮긴다.
    ///
    /// ★왜: 전역 눌림 연출(UIButtonPressEffect)은 버튼의 localScale 을 96% 로 줄인다.
    ///   그런데 테두리가 버튼보다 2px 큰 별개의 판이라, 버튼만 줄어들면 그 판이 사방으로
    ///   드러나 회색 띠가 생긴다("버튼 뒤에 이상한 배경이 보인다"의 정체).
    ///   Outline 은 버튼 자신의 메시라 같이 줄어든다 — 드러날 판이 아예 없어진다.</summary>
    public static void MoveBorderToOutline(Graphic target, RectTransform legacyBorder, Color color)
    {
        if (legacyBorder != null) legacyBorder.gameObject.SetActive(false);
        if (target == null) return;

        // ★Outline 은 풀네임으로 — 프로젝트에 UnityEngine.Outline(외곽선 셰이더용)이 따로 있다.
        var o = target.GetComponent<UnityEngine.UI.Outline>();
        if (o == null) o = target.gameObject.AddComponent<UnityEngine.UI.Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(1f, 1f);
        o.useGraphicAlpha = false;
    }

    // ==================================================================
    //  모서리 눈금
    // ==================================================================
    /// <summary>네 모서리의 ㄱ자 눈금. 씬에 구워진 것들(상자 12×2, 액션 바 버튼 10×2)이
    /// 전부 같은 공식이라 하나로 묶었다 — 가로획은 모서리에서 len, 세로획은 thick 만큼
    /// 안쪽으로 들어온다. 같은 대상에 두 번 불러도 한 번만 그린다.</summary>
    public static void AddTicks(RectTransform target, float w, float h, Color color,
                                float len, float thick)
    {
        if (target == null) return;
        if (target.Find(TickHolder) != null) return;   // 이미 그렸다

        var holder = new GameObject(TickHolder, typeof(RectTransform));
        var hrt = (RectTransform)holder.transform;
        hrt.SetParent(target, false);
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
        hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;

        float hw = w * 0.5f, hh = h * 0.5f;
        for (int i = 0; i < 4; i++)
        {
            float sx = (i == 0 || i == 2) ? -1f : 1f;
            float sy = (i < 2) ? 1f : -1f;
            Tick(hrt, color, new Vector2(sx * (hw - len * 0.5f), sy * (hh - thick * 0.5f)),
                 new Vector2(len, thick));
            Tick(hrt, color, new Vector2(sx * (hw - thick * 0.5f), sy * (hh - len * 0.5f)),
                 new Vector2(thick, len));
        }
    }

    static void Tick(RectTransform parent, Color color, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(TickName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }
}
