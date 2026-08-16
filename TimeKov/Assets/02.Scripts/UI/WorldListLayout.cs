// =====================================================================
// WorldListLayout.cs
// 월드 목록의 '열' 규격. 머리글(HeaderRow)과 각 줄(WorldSelectRow)이 같은 값을 써야
// 세로로 딱 맞아떨어지므로 한 곳에 모았다.
//   머리글은 에디터 빌더가 씬에 배치하고, 각 줄은 실행 중에 배치한다(프리팹 인스턴스라).
//   둘이 서로 다른 숫자를 들고 있으면 열이 어긋나므로 반드시 여기만 고칠 것.
//
// 좌표는 '줄 왼쪽 끝에서의 거리'다. 앵커를 좌측-가운데로 통일해 두면
// 머리글(높이 44)과 줄(높이 84)에 같은 x 를 그대로 쓸 수 있다.
// =====================================================================

using TMPro;
using UnityEngine;

public static class WorldListLayout
{
    public const float RowW = 1650f;

    //        x        폭        정렬
    public const float NameX  = 28f,   NameW  = 800f;    // 월드명 — 왼쪽
    public const float DateX  = 900f,  DateW  = 420f;    // 마지막 플레이 — 왼쪽
    public const float LevelX = 1392f, LevelW = 230f;    // 코어 레벨 — 오른쪽

    // 열 사이 경계선. 머리글 안에서만 긋는다(줄마다 그으면 표가 아니라 격자가 된다).
    public const float Sep1X = 864f, Sep2X = 1356f;
    public const float SepW = 1f, SepH = 26f;

    public const float FontHeader = 22f;   // 머리글 — 월드명과 같은 크기
    public const float FontName   = 22f;
    public const float FontDate   = 18f;   // 월드명보다 한 단계 작게
    public const float FontLevel  = 22f;

    /// <summary>왼쪽 끝 기준 x 로 열 하나를 앉힌다. 세로는 항상 가운데.</summary>
    public static void Place(RectTransform rt, float x, float w, float h = 30f)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(w, h);
    }

    /// <summary>글자의 크기·정렬만 맞춘다. ★.text 는 건드리지 않는다 —
    /// 머리글은 씬 라벨이라 LocalizedLabel 이 관리한다.</summary>
    public static void Style(TMP_Text t, float size, TextAlignmentOptions align, bool bold)
    {
        if (t == null) return;
        t.fontSize = size;
        t.alignment = align;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
    }
}
