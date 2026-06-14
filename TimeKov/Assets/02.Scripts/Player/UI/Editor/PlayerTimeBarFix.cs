// =====================================================================
// PlayerTimeBarFix.cs (Editor Only)
// Tools/TIMEKOV/플레이어 시간바 원복(복구)
//
// 경위: 시간(체력)바 fill/배경이 "딱 안 맞는 느낌"이라 정렬을 시도했는데,
//       이 바는 표준 Slider 기하가 아니라 GhostHpBar 스크립트가 Fill/GhostFill 의
//       fillAmount 를 LateUpdate 에서 직접 구동한다(데미지 빨강/회복 초록 잔상).
//       정규화(full-stretch + GhostFill 비활성화)가 오히려 바를 깨뜨렸음.
//
// 이 메뉴는 그 변경을 "원래 값으로 복구"한다 (작동하던 상태로 되돌림).
//   slider 루트 600x60 (pos -28,25) / Fill Area (pos -23,-0.5, size -4,-1) /
//   Fill (size 0,350, scale 1,0.55) / GhostFill (활성, size 0,350, scale 1,0.6)
//
// 미세한 fill-배경 불일치를 더 잡고 싶으면 = 배경 스프라이트(hpbar_rect_512x64)에
// 맞춘 파란 fill 스프라이트를 디자인 받아 교체하는 게 가장 깔끔(인게임 눈으로 조절).
// =====================================================================

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PlayerTimeBarFix
{
    const string CanvasPath = "Assets/05.Prefabs/UI/Canvas.prefab";

    [MenuItem("Tools/TIMEKOV/플레이어 시간바 원복(복구)")]
    public static void Restore()
    {
        var root = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            Slider slider = null;
            foreach (var h in root.GetComponentsInChildren<PlayerHudUI>(true))
            {
                var hso = new SerializedObject(h);
                var sp = hso.FindProperty("hpSlider");
                if (sp != null && sp.objectReferenceValue is Slider s) { slider = s; break; }
            }
            if (slider == null) { Debug.LogError("[PlayerTimeBarFix] hpSlider 연결된 PlayerHudUI 못 찾음"); return; }

            var sliderRoot = slider.GetComponent<RectTransform>();
            var fillRect   = slider.fillRect;
            var fillArea   = fillRect != null ? fillRect.parent as RectTransform : null;

            // 원래(작동하던) 값으로 복구
            if (sliderRoot != null)
                SetRect(sliderRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                        new Vector2(-28f, 25f), new Vector2(600f, 60f), Vector3.one);

            if (fillArea != null)
                SetRect(fillArea, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                        new Vector2(-23f, -0.5f), new Vector2(-4f, -1f), Vector3.one);

            if (fillRect != null)
                SetRect(fillRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.5f),
                        new Vector2(0f, 2.5f), new Vector2(0f, 350f), new Vector3(1f, 0.55f, 1f));

            // GhostFill = Fill Area 의 다른 자식 (GhostHpBar 가 구동하는 잔상). 다시 활성화 + 원래 값.
            if (fillArea != null)
            {
                for (int i = 0; i < fillArea.childCount; i++)
                {
                    var c = fillArea.GetChild(i) as RectTransform;
                    if (c == null || c == fillRect) continue;
                    c.gameObject.SetActive(true);
                    SetRect(c, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
                            new Vector2(0f, 2.5f), new Vector2(0f, 350f), new Vector3(1f, 0.6f, 1f));
                }
            }

            EditorUtility.SetDirty(slider);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerTimeBarFix] 시간바 원복 완료 (GhostFill 재활성화 + 원래 rect 복구).");
            EditorUtility.DisplayDialog("완료",
                "플레이어 시간바를 원래 상태로 복구했습니다.\nPlay 로 확인.\n" +
                "(미세 정렬은 파란 fill 스프라이트를 배경에 맞춰 교체하는 방식 권장)", "확인");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
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
