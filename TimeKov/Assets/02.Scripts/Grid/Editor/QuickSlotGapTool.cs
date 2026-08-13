// =====================================================================
// Editor/QuickSlotGapTool.cs
// Tools/TIMEKOV/건축바/경계선 간격 맞추기
//
// 건축바 가운데 경계선을 사이에 두고, 왼쪽(숫자 칸 8번)과의 간격만큼
// 오른쪽(키 칸 E/T/G)도 똑같이 떨어지도록 키 칸 묶음을 통째로 옮긴다.
//
// [이 툴이 하는 일은 '이동'뿐이다]
//   오브젝트를 만들거나 지우지 않는다. 그래서 몇 번을 돌려도 결과가 같다
//   (맞춘 뒤 다시 돌리면 이동량이 0 이라 아무 일도 안 일어난다).
//   ★패널(QuickSlotPanel)은 절대 건드리지 않는다 - 그건 슬롯 바가 아니라
//     건설 모드 UI 전체라, 옮기면 배경까지 밀려 한쪽이 잘린다.
//
// [칸을 어떻게 찾나]
//   이름으로 찾지 않는다. 아이콘에서 위로 올라가며 '키캡(한 글자 라벨)'을
//   처음 품는 조상을 그 칸으로 본다. 숫자 키캡이면 숫자 칸, 아니면 키 칸.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TMPro;

public static class QuickSlotGapTool
{
    // ---- 조정값 (결과 보고 이 숫자만 바꾸면 된다) ----
    /// 키 칸(E/T/G) 사이의 빈 간격(px). 칸 가장자리에서 다음 칸 가장자리까지.
    /// 절대값이라 몇 번 돌려도 결과가 같다(배율은 칸 폭에 휘둘려 예측이 안 돼서 버렸다).
    private const float KeyCellGap = 44f;

    [MenuItem("Tools/TIMEKOV/건축바/경계선 간격 맞추기")]
    public static void Run()
    {
        var build = Object.FindAnyObjectByType<BuildManager>(FindObjectsInactive.Include);
        if (build == null) { Debug.LogError("[건축바] 씬에서 BuildManager 를 못 찾았다. World 씬을 열어라."); return; }

        // 칸 모으기 - 설비 슬롯(숫자) + 레일 슬롯(E). 레일은 배열이 아니라 별도 필드다.
        var numberCells = new List<RectTransform>();
        var keyCells = new List<RectTransform>();

        void Collect(Transform icon)
        {
            if (icon == null) return;
            var cell = FindCell(icon, out string cap);
            if (cell == null) return;
            bool isNumber = cap.Length == 1 && cap[0] >= '0' && cap[0] <= '9';
            var target = isNumber ? numberCells : keyCells;
            if (!target.Contains(cell)) target.Add(cell);
        }

        if (build.slotIconUIs != null)
            foreach (var s in build.slotIconUIs) if (s != null) Collect(s.transform);
        Collect(build.railSlotIcon);

        if (numberCells.Count == 0 || keyCells.Count == 0)
        {
            Debug.LogError($"[건축바] 칸을 못 찾았다(숫자 {numberCells.Count} / 키 {keyCells.Count}). " +
                           "키캡 텍스트가 있는지 확인해라.");
            return;
        }

        var parent = keyCells[0].parent;
        foreach (var c in keyCells)
            if (c.parent != parent) { Debug.LogError($"[건축바] 키 칸 '{c.name}' 의 부모가 달라 함께 못 옮긴다."); return; }

        var divider = FindDivider(parent, numberCells[0]);
        if (divider == null) { Debug.LogError("[건축바] 경계선을 못 찾았다. 칸보다 확연히 좁은 세로 장식이 있어야 한다."); return; }

        Undo.RecordObjects(keyCells.ToArray(), "건축바 간격 정리");

        // 1) 키 칸끼리의 간격을 숫자 칸 리듬에 맞춰 다시 깐다(맨 왼쪽 칸은 제자리).
        numberCells.Sort((a, b) => a.anchoredPosition.x.CompareTo(b.anchoredPosition.x));
        keyCells.Sort((a, b) => a.anchoredPosition.x.CompareTo(b.anchoredPosition.x));

        float numberGap = MeasureGap(numberCells);
        float oldKeyGap = MeasureGap(keyCells);
        if (keyCells.Count > 1)
        {
            float cursor = keyCells[0].anchoredPosition.x + keyCells[0].rect.width * 0.5f;   // 첫 칸 오른쪽 가장자리
            for (int i = 1; i < keyCells.Count; i++)
            {
                var c = keyCells[i];
                var p = c.anchoredPosition;
                p.x = cursor + KeyCellGap + c.rect.width * 0.5f;
                c.anchoredPosition = p;
                cursor = p.x + c.rect.width * 0.5f;
            }
            Debug.Log($"[건축바] 키 칸 간격 {oldKeyGap:0.#} -> {KeyCellGap:0.#}px (참고: 숫자 칸 간격 {numberGap:0.#}px).");
        }

        // 2) 경계선 양쪽의 빈 구간을 잰다.
        // ★RectTransform 이 아니라 '실제로 그려지는 그래픽'으로 잰다.
        //   칸의 RectTransform 은 눈에 보이는 상자보다 크고, 그 여백이 숫자칸과 키칸이 서로 달랐다.
        //   그래서 수치상 55.4 vs 55.4 로 완벽히 같은데도 화면에서는 오른쪽이 더 벌어져 보였다.
        Canvas.ForceUpdateCanvases();
        var space = parent as RectTransform;

        float numberRight = float.NegativeInfinity;
        foreach (var c in numberCells)
            if (VisibleXRange(c, space, out _, out float max)) numberRight = Mathf.Max(numberRight, max);

        float keyLeft = float.PositiveInfinity;
        foreach (var c in keyCells)
            if (VisibleXRange(c, space, out float min, out _)) keyLeft = Mathf.Min(keyLeft, min);

        if (!VisibleXRange(divider, space, out float divLeft, out float divRight))
        { Debug.LogError("[건축바] 경계선에서 그려지는 그래픽을 못 찾았다."); return; }

        float leftGap  = divLeft - numberRight;
        float rightGap = keyLeft - divRight;

        // 진단용 - 경계선을 엉뚱한 오브젝트로 잡으면 아래 계산이 통째로 틀린다.
        Debug.Log($"[건축바] 경계선 = '{divider.name}' (보이는 범위 {divLeft:0.#}~{divRight:0.#}) / " +
                  $"숫자칸 오른쪽 끝 {numberRight:0.#} / 키칸 왼쪽 끝 {keyLeft:0.#} / " +
                  $"왼쪽 간격 {leftGap:0.#} vs 오른쪽 간격 {rightGap:0.#}");
        float shift = leftGap - rightGap;   // 오른쪽이 더 벌어져 있으면 음수 = 왼쪽으로 당긴다

        if (Mathf.Abs(shift) > 0.5f)
        {
            foreach (var c in keyCells) c.anchoredPosition += new Vector2(shift, 0f);
            Debug.Log($"[건축바] 키 칸 묶음을 {shift:0.#}px 옮겨 경계선 양쪽을 {leftGap:0.#}px 로 맞췄다 " +
                      $"(전에는 오른쪽이 {rightGap:0.#}px 로 벌어져 있었다).");
        }

        EditorUtility.SetDirty(build);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(build.gameObject.scene);
        Debug.Log("[건축바] 정리 완료. 씬을 저장해라(Ctrl+S).");
    }

    /// 칸이 '실제로 그려지는' 가로 범위(space 기준 로컬 x).
    /// RectTransform 은 눈에 안 보이는 여백을 품고 있어서, 그걸로 재면 화면과 어긋난다.
    private static bool VisibleXRange(RectTransform cell, RectTransform space, out float min, out float max)
    {
        min = float.PositiveInfinity; max = float.NegativeInfinity;
        if (cell == null || space == null) return false;

        var corners = new Vector3[4];
        foreach (var g in cell.GetComponentsInChildren<UnityEngine.UI.Graphic>(false))
        {
            if (g == null || !g.enabled) continue;
            if (g.canvasRenderer != null && g.canvasRenderer.GetAlpha() <= 0.01f) continue;
            if (g.color.a <= 0.01f) continue;

            g.rectTransform.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++)
            {
                float x = space.InverseTransformPoint(corners[i]).x;
                if (x < min) min = x;
                if (x > max) max = x;
            }
        }
        return max > min;
    }

    /// 나란한 칸들 사이의 평균 빈 간격(칸 가장자리 기준). 칸이 하나면 0.
    private static float MeasureGap(List<RectTransform> cells)
    {
        if (cells.Count < 2) return 0f;
        float sum = 0f;
        for (int i = 1; i < cells.Count; i++)
        {
            var a = cells[i - 1];
            var b = cells[i];
            sum += (b.anchoredPosition.x - b.rect.width * 0.5f) - (a.anchoredPosition.x + a.rect.width * 0.5f);
        }
        return sum / (cells.Count - 1);
    }

    /// 슬롯 '한 칸' = 키캡(한 글자 라벨)을 처음 품는 조상. 함께 그 키캡 글자도 돌려준다.
    private static RectTransform FindCell(Transform icon, out string keyCap)
    {
        keyCap = "";
        for (Transform t = icon; t != null; t = t.parent)
        {
            var rt = t as RectTransform;
            if (rt == null) continue;
            var label = FindSingleCharLabel(rt);
            if (label != null) { keyCap = label.text.Trim(); return rt; }
            if (t.GetComponent<Canvas>() != null) break;   // 캔버스까지 가면 실패(패널을 잡으면 안 된다)
        }
        return null;
    }

    private static TMP_Text FindSingleCharLabel(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null || string.IsNullOrWhiteSpace(t.text)) continue;
            if (t.text.Trim().Length == 1) return t;
        }
        return null;
    }

    /// 숫자 칸과 키 칸 사이의 경계선. 칸보다 확연히 좁고, 칸만큼 세로로 긴 형제를 고른다.
    private static RectTransform FindDivider(Transform parent, RectTransform sampleCell)
    {
        float cellW = sampleCell.rect.width, cellH = sampleCell.rect.height;
        RectTransform best = null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var rt = parent.GetChild(i) as RectTransform;
            if (rt == null) continue;
            if (rt.rect.width >= cellW * 0.5f) continue;        // 칸 급이면 장식이 아니다
            if (rt.rect.height < cellH * 0.3f) continue;        // 너무 납작한 것도 제외
            if (best == null || rt.rect.width < best.rect.width) best = rt;
        }
        return best;
    }
}
