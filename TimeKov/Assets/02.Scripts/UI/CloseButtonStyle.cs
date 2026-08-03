using UnityEngine;
using UnityEngine.UI;

// 닫기(X) 버튼의 호버/클릭 하이라이트를 인벤토리와 동일하게 통일한다.
// 버튼 루트 Image = 둥근 배경(평소 투명) + ColorTint 로 은은한 슬레이트 하이라이트.
// 값은 인벤토리 패널의 CloseButton 과 1:1(어긋남 방지). X 아이콘은 별도 자식(Icon).
public static class CloseButtonStyle
{
    public static void Apply(Button btn)
    {
        if (btn == null) return;

        var bg = btn.GetComponent<Image>();
        if (bg == null) bg = btn.gameObject.AddComponent<Image>();
        bg.sprite = UISpriteFactory.RoundedRect(64, 16);
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;          // ColorTint state 색이 그대로 보이게 base = 흰색
        bg.raycastTarget = true;

        btn.transition = Selectable.Transition.ColorTint;
        btn.targetGraphic = bg;

        var c = btn.colors;
        c.normalColor      = new Color(1f, 1f, 1f, 0f);               // 평소 = 투명
        c.highlightedColor = new Color(0.24f, 0.29f, 0.39f, 0.20f);   // 호버 = 은은한 쿨 슬레이트
        c.pressedColor     = new Color(0.20f, 0.24f, 0.34f, 0.36f);   // 누름 = 더 진하게
        c.selectedColor    = new Color(1f, 1f, 1f, 0f);
        c.disabledColor    = new Color(1f, 1f, 1f, 0f);
        c.colorMultiplier  = 1f;
        c.fadeDuration     = 0.1f;
        btn.colors = c;
    }
}
