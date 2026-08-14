using UnityEngine;

/// <summary>
/// 상호작용 가능한 오브젝트(설비·전리품 상자 등)의 아웃라인 강조를 한 곳에서 통일 관리한다.
/// 색/두께를 여기서만 바꾸면 모든 하이라이트에 일괄 적용된다.
/// </summary>
public static class InteractOutline
{
    /// <summary>통일된 강조 색 — 잘 보이는 노란색.</summary>
    public static readonly Color Color = new Color(1f, 0.9f, 0.15f, 1f);

    /// <summary>통일된 강조 두께 (QuickOutline OutlineWidth). 설비 등에 과하지 않은 적정값.</summary>
    public const float Width = 6f;

    /// <summary>주어진 Outline에 통일 스타일(색/두께/모드)만 적용한다. enabled는 건드리지 않음.</summary>
    public static void Apply(Outline outline)
    {
        if (outline == null) return;
        outline.OutlineColor = Color;
        outline.OutlineWidth = Width;

        // 기본은 '가림 무시' — 풀·나무 등 앞을 가리는 물체 위에도 그려져 항상 보이게.
        //   단, 땅에 박혀 있는 큰 오브젝트는 그 탓에 묻힌 부분까지 다 비친다.
        //   그런 오브젝트는 루트에 InteractOutlineStyle 을 붙여 OutlineVisible 로 바꾼다
        //   (지형에 가려지면 안 그려짐 — 경사면도 그대로 따라간다).
        var style = outline.GetComponentInParent<InteractOutlineStyle>();
        outline.OutlineMode = style != null ? style.mode : Outline.Mode.OutlineAll;
    }

    /// <summary>주어진 Outline에 통일 스타일을 적용하고 켠다. null이면 무시.</summary>
    public static void Enable(Outline outline)
    {
        if (outline == null) return;
        Apply(outline);
        outline.enabled = true;
    }
}
