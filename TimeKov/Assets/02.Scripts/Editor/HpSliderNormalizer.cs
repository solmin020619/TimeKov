#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

// HP 게이지 슬라이더의 Fill / Fill Area / GhostFill 을 표준 구조로 정리한다.
// - Fill Area, Fill : anchor stretch(0,0~1,1) + offset 0 (부모를 꽉 채움)
// - Fill : Image Type 을 Simple 로 (Filled + Slider 의 이중 제어 충돌 제거)
// - scale 을 (1,1,1) 로 (왜곡 제거)
// Slider 컴포넌트가 Fill 너비를 value 만큼 제어하므로, 위 구조면 게이지가 정상 동작한다.
public static class HpSliderNormalizer
{
    [MenuItem("Tools/UI/HP 슬라이더 표준 정리 (선택한 Slider)")]
    public static void Normalize()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null) { Dialog("Hierarchy 에서 Slider(또는 그 부모, 예: TimeGaugeSlider)를 선택한 뒤 실행하세요."); return; }

        Slider slider = go.GetComponent<Slider>();
        if (slider == null) slider = go.GetComponentInChildren<Slider>(true);
        if (slider == null) { Dialog("선택한 오브젝트와 그 자식에서 Slider 컴포넌트를 찾지 못했습니다."); return; }

        RectTransform fill = slider.fillRect;
        if (fill == null) { Dialog("Slider 의 Fill Rect 가 비어 있습니다. 먼저 Fill 을 연결하세요."); return; }
        RectTransform fillArea = fill.parent as RectTransform;

        Undo.RegisterFullObjectHierarchyUndo(slider.gameObject, "HP 슬라이더 표준 정리");

        // Fill Area: 부모(Slider) 꽉 채움
        if (fillArea != null) StretchFull(fillArea);

        // Fill: Fill Area 꽉 채움 + Image Simple + scale 1
        StretchFull(fill);
        fill.localScale = Vector3.one;
        // Image Type 은 건드리지 않는다 — 이 게이지는 Filled 여야 렌더된다 (Simple 이면 안 보임)

        // Fill Area 안의 GhostFill(이름에 ghost 포함) 도 같이 표준화 (있으면)
        int ghostCount = 0;
        if (fillArea != null)
        {
            foreach (RectTransform child in fillArea)
            {
                if (child == fill) continue;
                if (child.name.ToLower().Contains("ghost"))
                {
                    StretchFull(child);
                    child.localScale = Vector3.one;
                    ghostCount++;
                }
            }
        }

        EditorUtility.SetDirty(slider.gameObject);

        // 진단 로그 — 어느 단계에서 세로 높이가 0/비정상인지 확인용
        RectTransform sliderRt = slider.transform as RectTransform;
        Debug.Log($"[HP정리] Slider: sizeDelta={sliderRt.sizeDelta} anchorMin={sliderRt.anchorMin} anchorMax={sliderRt.anchorMax} pivot={sliderRt.pivot}");
        if (fillArea != null)
            Debug.Log($"[HP정리] FillArea: sizeDelta={fillArea.sizeDelta} anchor=({fillArea.anchorMin}~{fillArea.anchorMax}) offMin={fillArea.offsetMin} offMax={fillArea.offsetMax}");
        Debug.Log($"[HP정리] Fill: sizeDelta={fill.sizeDelta} anchor=({fill.anchorMin}~{fill.anchorMax}) offMin={fill.offsetMin} offMax={fill.offsetMax} scale={fill.localScale}");

        Dialog(
            "정리 완료:\n" +
            "- Fill Area / Fill : anchor stretch + offset 0\n" +
            "- Fill : Image Type Simple, scale (1,1,1)\n" +
            (ghostCount > 0 ? "- GhostFill " + ghostCount + "개 동일 정리\n" : "") +
            "\n씬 저장(Ctrl+S) 후 플레이해서 게이지가 체력만큼 채워지고 배경에 맞는지 확인하세요."
        );
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Dialog(string msg) => EditorUtility.DisplayDialog("HP 슬라이더 표준 정리", msg, "확인");
}
#endif
