using UnityEngine;
using UnityEngine.UI;

// K 도감 HUD 아이콘에 붙이는 알림(!) 배지.
// 안 본 새 도감 항목(CodexNotice.AnyUnseen)이 있으면 아이콘 우상단 모서리에 Alert_amber 를 띄운다.
// 크기는 아이콘 크기에 비례해 자동 계산(작게 안 보이게), 위치는 모서리에 살짝 겹치게.
// 종욱이 할 것: 우상단 K 아이콘 GameObject 에 이 컴포넌트만 붙이면 됨(배지는 코드가 생성).
//   - 너무 크/작으면 Size Scale, 모서리서 더/덜 겹치려면 Corner Inset, 미세 위치는 Pixel Offset 으로.
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class CodexNoticeBadge : MonoBehaviour
{
    [Tooltip("알림 아이콘. 비우면 Resources/Image/UI_Icon/HUD/Alert_amber 자동 로드, 그것도 없으면 절차 폴백(앰버 점).")]
    public Sprite noticeIcon;

    [Tooltip("아이콘 짧은 변 대비 배지 크기 비율. 0.62 = 아이콘의 62%.")]
    [Range(0.3f, 1.3f)] public float sizeScale = 0.62f;

    [Tooltip("우상단 모서리서 안쪽으로 당기는 비율(배지 크기 기준). 0=모서리 정중앙(반쯤 튀어나옴), 0.25=꽤 안쪽.")]
    [Range(0f, 0.6f)] public float cornerInset = 0.2f;

    [Tooltip("추가 미세 위치 보정(px). 자동 위치가 살짝 어긋날 때만.")]
    public Vector2 pixelOffset = Vector2.zero;

    [Tooltip("아이콘 크기를 못 잴 때 쓰는 폴백 배지 크기(px).")]
    public float fallbackSize = 30f;

    private const string ResourcePath = "Image/UI_Icon/HUD/Alert_amber";

    private GameObject _badge;

    void OnEnable()
    {
        EnsureBadge();
        CodexNotice.OnChanged += Apply;
        Apply();
    }

    void OnDisable()
    {
        CodexNotice.OnChanged -= Apply;
    }

    void EnsureBadge()
    {
        if (_badge != null) return;

        var prt = transform as RectTransform;
        float refLen = prt != null ? Mathf.Min(Mathf.Abs(prt.rect.width), Mathf.Abs(prt.rect.height)) : 0f;
        float sz = refLen > 8f ? refLen * sizeScale : fallbackSize;   // 아이콘 비례, 못 재면 폴백

        var go = new GameObject("CodexNoticeBadge");
        var brt = go.AddComponent<RectTransform>();
        brt.SetParent(transform, false);
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);   // 아이콘 우상단 모서리
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(sz, sz);
        brt.anchoredPosition = new Vector2(-sz * cornerInset + pixelOffset.x, -sz * cornerInset + pixelOffset.y);

        var img = go.AddComponent<Image>();
        var sprite = noticeIcon != null ? noticeIcon : Resources.Load<Sprite>(ResourcePath);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
        }
        else
        {
            img.color = new Color(0.96f, 0.62f, 0.12f, 1f);   // 스프라이트 못 찾으면 앰버 점 폴백
        }
        img.raycastTarget = false;

        _badge = go;
    }

    void Apply()
    {
        if (_badge == null) EnsureBadge();
        if (_badge != null) _badge.SetActive(CodexNotice.AnyUnseen);
    }
}
