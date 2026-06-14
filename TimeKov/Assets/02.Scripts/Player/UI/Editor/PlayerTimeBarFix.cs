// =====================================================================
// PlayerTimeBarFix.cs (Editor Only)
// Tools/TIMEKOV/플레이어 시간바 ...
//
// 시간(체력)바 구조: PlayerTime > TimeGaugeSlider(배경 프레임 Image hpbar_rect_512x64 + GhostHpBar)
//   > Slider 루트 > Fill Area > GhostFill, Fill.
// 표준 Slider 기하가 아니라 GhostHpBar 가 Fill/GhostFill 의 fillAmount 를 직접 구동한다.
//
// [원복(복구)]      : 정규화로 깨진 바를 원래 작동값으로 되돌림.
// [fill 스프라이트 적용]: clggdesign 의 캐비티-맞춤 fill 스프라이트(hpbar_fill_504x56)로 교체.
//   - 슬라이더/FillArea/Fill/GhostFill 을 배경 캐비티(프레임 사방 4px inset)에 정렬 + scale 1(눌림 제거).
//   - 참조(Slider.fillRect / GhostHpBar.ghostImage,mainFillImage)는 안 건드림 -> 기능 유지.
//   - 색은 런타임에 HpBarFeedback/GhostHpBar 가 틴트하므로 스프라이트만 교체.
// =====================================================================

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PlayerTimeBarFix
{
    const string CanvasPath = "Assets/05.Prefabs/UI/Canvas.prefab";
    // 미세 공백 메우기: fill 을 프레임보다 살짝 크게(px). 끝 모서리 빈틈 덮음. 공백 더 있으면 숫자만 올리면 됨.
    const float OvershootX = 2f;
    const float OvershootY = 1f;
    // aligned 버전 = 프레임(512x64)과 동일 크기/투명여백. 프레임과 같은 rect로 겹치면 막대가 정확히 일치.
    // (tight 504x56 은 여백이 없어 풀높이로 떠서 얇은 프레임 막대와 안 맞았음)
    const string FillSpritePath = "Assets/Resources/Image/UI_Icon/HUD/hpbar_fill_aligned_512x64.png";

    // ── fill 스프라이트 적용 (clggdesign 캐비티-맞춤) ──
    [MenuItem("Tools/TIMEKOV/플레이어 시간바 fill 스프라이트 적용")]
    public static void ApplyFillSprite()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FillSpritePath);
        if (sprite == null) { Debug.LogError("[PlayerTimeBarFix] fill 스프라이트 못 찾음: " + FillSpritePath); return; }

        var root = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            var slider = FindHpSlider(root);
            if (slider == null) { Debug.LogError("[PlayerTimeBarFix] hpSlider 못 찾음"); return; }

            var sliderRoot = slider.GetComponent<RectTransform>();
            var timeGauge  = sliderRoot != null ? sliderRoot.parent as RectTransform : null;   // TimeGaugeSlider (400x60 = 잘못된 크기)
            var fillRect   = slider.fillRect;
            var fillArea   = fillRect != null ? fillRect.parent as RectTransform : null;

            // ★진짜 원인 수정: TimeGaugeSlider(슬라이더 컨테이너)를 진짜 프레임(PlayerTime=부모)에 맞춘다.
            //   TimeGaugeSlider가 400x60이라 그 아래 fill이 프레임(500x28)의 2배로 컸던 것.
            if (timeGauge != null)
            {
                FullStretch(timeGauge, Vector2.zero, Vector2.zero);   // = PlayerTime(프레임) 크기로 (500x28)
                var tgImg = timeGauge.GetComponent<Image>();
                if (tgImg != null) { var col = tgImg.color; col.a = 0f; tgImg.color = col; }   // 중복 파란 backing 투명화(프레임이 bg)
            }
            // 슬라이더 = 프레임에 꽉 채우기. FillArea 는 살짝 키워(Overshoot) 끝 모서리 미세 공백 메움.
            if (sliderRoot != null) FullStretch(sliderRoot, Vector2.zero, Vector2.zero);
            if (fillArea != null) FullStretch(fillArea, new Vector2(-OvershootX, -OvershootY), new Vector2(OvershootX, OvershootY));
            // Fill = 캐비티 꽉 채우기 + 새 스프라이트 (눌림 제거: scale 1)
            if (fillRect != null)
            {
                FullStretch(fillRect, Vector2.zero, Vector2.zero);
                ApplyFilledImage(fillRect.GetComponent<Image>(), sprite);
            }
            // GhostFill(=Fill Area 의 다른 자식) = 캐비티 꽉 채우기 + 새 스프라이트 + 활성
            if (fillArea != null)
            {
                for (int i = 0; i < fillArea.childCount; i++)
                {
                    var c = fillArea.GetChild(i) as RectTransform;
                    if (c == null || c == fillRect) continue;
                    c.gameObject.SetActive(true);
                    FullStretch(c, Vector2.zero, Vector2.zero);
                    ApplyFilledImage(c.GetComponent<Image>(), sprite);
                }
            }

            EditorUtility.SetDirty(slider);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerTimeBarFix] fill 스프라이트 적용 완료 (캐비티 4px inset + scale1 + hpbar_fill_504x56).");
            EditorUtility.DisplayDialog("완료",
                "시간바 fill 스프라이트 적용 완료.\nPlay 로 확인.\n" +
                "끝 라운드/두께/inset 미세조정 필요하면 말해줘 (스크립트 값 조정).", "확인");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ── 원복(복구): 정규화로 깨졌을 때 원래 작동값으로 ──
    [MenuItem("Tools/TIMEKOV/플레이어 시간바 원복(복구)")]
    public static void Restore()
    {
        var root = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            var slider = FindHpSlider(root);
            if (slider == null) { Debug.LogError("[PlayerTimeBarFix] hpSlider 못 찾음"); return; }

            var sliderRoot = slider.GetComponent<RectTransform>();
            var timeGauge  = sliderRoot != null ? sliderRoot.parent as RectTransform : null;
            var fillRect   = slider.fillRect;
            var fillArea   = fillRect != null ? fillRect.parent as RectTransform : null;

            if (timeGauge != null)
            {
                SetRect(timeGauge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(65f, 0f), new Vector2(400f, 60f), Vector3.one);
                var tgImg = timeGauge.GetComponent<Image>();
                if (tgImg != null) { var col = tgImg.color; col.a = 0.9f; tgImg.color = col; }
            }
            if (sliderRoot != null)
                SetRect(sliderRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                        new Vector2(-28f, 25f), new Vector2(600f, 60f), Vector3.one);
            if (fillArea != null)
                SetRect(fillArea, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                        new Vector2(-23f, -0.5f), new Vector2(-4f, -1f), Vector3.one);
            if (fillRect != null)
                SetRect(fillRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.5f),
                        new Vector2(0f, 2.5f), new Vector2(0f, 350f), new Vector3(1f, 0.55f, 1f));
            if (fillArea != null)
                for (int i = 0; i < fillArea.childCount; i++)
                {
                    var c = fillArea.GetChild(i) as RectTransform;
                    if (c == null || c == fillRect) continue;
                    c.gameObject.SetActive(true);
                    SetRect(c, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
                            new Vector2(0f, 2.5f), new Vector2(0f, 350f), new Vector3(1f, 0.6f, 1f));
                }

            EditorUtility.SetDirty(slider);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerTimeBarFix] 시간바 원복 완료.");
            EditorUtility.DisplayDialog("완료", "플레이어 시간바를 원래 상태로 복구했습니다.", "확인");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ── 헬퍼 ──
    static Slider FindHpSlider(GameObject root)
    {
        foreach (var h in root.GetComponentsInChildren<PlayerHudUI>(true))
        {
            var hso = new SerializedObject(h);
            var sp = hso.FindProperty("hpSlider");
            if (sp != null && sp.objectReferenceValue is Slider s) return s;
        }
        return null;
    }

    // 부모를 채우는 stretch (offset 지정 가능, scale 1)
    static void FullStretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.localScale = Vector3.one;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void ApplyFilledImage(Image img, Sprite sprite)
    {
        if (img == null) return;
        img.sprite = sprite;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.preserveAspect = false;   // 캐비티 폭을 채우고 fillAmount 로 비율 -> aspect 유지 X
    }

    static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, Vector3 scale)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = scale;
    }
}
