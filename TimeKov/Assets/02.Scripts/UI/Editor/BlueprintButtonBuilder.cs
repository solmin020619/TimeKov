// =====================================================================
// Editor/BlueprintButtonBuilder.cs
// Tools/TIMEKOV/UI/청사진 버튼 생성
//
// 건축 슬롯바(QuickSlotPanel.prefab)의 해제 모드 힌트 알약을 복제해서 바로 위에
// "청사진 모드 [N]" 알약을 만든다. 클릭하면 청사진 모드 진입(BlueprintModeButton).
//
// [찾는 기준]
//   해제 알약의 라벨은 프리팹에 "일괄 조작" 으로 구워져 있고(런타임에
//   BuildModeHintsLocalizer 가 "해제 모드" 로 덮는다) 키캡 글자는 "X" 다.
//   구조를 좌표로 하드코딩하지 않고 이 텍스트로 찾으므로 알약 위치가 바뀌어도 동작한다.
//
// [재실행 안전] 이미 만든 버튼("Blueprint_N")이 있으면 지우고 다시 만든다.
// [일회성] 결과가 프리팹에 저장되면 이 파일은 지워도 된다. 런타임 컴포넌트
//   BlueprintModeButton 은 계속 필요하다(지우지 말 것).
// =====================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public static class BlueprintButtonBuilder
{
    private const string PrefabPath = "Assets/05.Prefabs/UI/QuickSlotPanel.prefab";
    private const string CloneName = "Blueprint_N";

    // 프리팹에 구워진 해제 알약의 텍스트 (BuildModeHintsLocalizer 가 런타임에 덮기 전 값)
    private const string DemolishBakedLabel = "일괄 조작";
    private const string DemolishRuntimeLabel = "해제 모드";
    private const string DemolishKey = "X";

    // 간격 기준: 해제 알약과 회전 알약 사이 간격을 재서 그대로 위로 복제한다
    private const string RotateBakedLabel = "설비 회전";

    [MenuItem("Tools/TIMEKOV/UI/청사진 버튼 생성 (해제 알약 복제)")]
    public static void Build()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null) { Debug.LogError($"[청사진버튼] 프리팹을 못 열었다: {PrefabPath}"); return; }

        try
        {
            // 1) 해제/회전 알약 찾기 - 라벨 텍스트로 찾는다
            TextMeshProUGUI demolishLabel = null;
            TextMeshProUGUI rotateLabel = null;
            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                string s = t.text != null ? t.text.Trim() : "";
                if (demolishLabel == null && (s == DemolishBakedLabel || s == DemolishRuntimeLabel)) demolishLabel = t;
                else if (rotateLabel == null && s == RotateBakedLabel) rotateLabel = t;
            }
            if (demolishLabel == null)
            {
                Debug.LogError($"[청사진버튼] 해제 알약 라벨('{DemolishBakedLabel}')을 못 찾았다. 프리팹 구조 확인 필요.");
                return;
            }

            var source = demolishLabel.transform.parent as RectTransform;
            if (source == null)
            {
                Debug.LogError("[청사진버튼] 해제 알약 컨테이너가 RectTransform 이 아니다.");
                return;
            }

            // 2) 재실행 안전 - 기존 복제본 제거
            var parent = source.parent;
            for (int i = parent.childCount - 1; i >= 0; i--)
                if (parent.GetChild(i).name == CloneName)
                    Object.DestroyImmediate(parent.GetChild(i).gameObject);

            // 3) 복제 + 바로 위에 배치
            var clone = (RectTransform)Object.Instantiate(source.gameObject, parent).transform;
            clone.name = CloneName;
            clone.SetSiblingIndex(source.GetSiblingIndex() + 1);

            // 간격 = 해제 알약과 회전 알약 사이 간격 그대로 (아래 두 개와 같은 리듬).
            // 회전 알약을 못 찾으면 높이 + 8px 폴백.
            float step;
            var rotateContainer = rotateLabel != null ? rotateLabel.transform.parent as RectTransform : null;
            if (rotateContainer != null && rotateContainer.parent == source.parent)
                step = Mathf.Abs(source.anchoredPosition.y - rotateContainer.anchoredPosition.y);
            else
                step = Mathf.Max(source.sizeDelta.y, source.rect.height, 40f) + 8f;

            clone.anchorMin = source.anchorMin;
            clone.anchorMax = source.anchorMax;
            clone.pivot = source.pivot;
            clone.anchoredPosition = source.anchoredPosition + new Vector2(0f, step);

            // 4) 문구/키캡 교체 + 폰트 아틀라스에 없는 글자 굽기
            //    구운(Static) 폰트라 "청사진" 같은 새 글자는 폴백 폰트로 렌더돼 서체가 달라 보인다.
            //    FontGlyphPatcher 와 같은 방식으로 해당 폰트 아틀라스에 글자를 추가한다.
            TextMeshProUGUI cloneLabel = null;
            foreach (var t in clone.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                string s = t.text != null ? t.text.Trim() : "";
                if (s == DemolishBakedLabel || s == DemolishRuntimeLabel)
                {
                    t.text = "청사진 모드";
                    cloneLabel = t;
                    EnsureGlyphs(t.font, "청사진 모드");
                }
                else if (s == DemolishKey)
                {
                    t.text = "N";
                    EnsureGlyphs(t.font, "N");
                }
            }
            if (cloneLabel == null)
                Debug.LogWarning("[청사진버튼] 복제본에서 라벨을 못 찾아 문구 교체를 건너뛰었다.");

            // 5) 클릭 배선 - 이미지에 레이캐스트를 켜고(힌트 알약은 꺼져 있다) 런타임 컴포넌트 부착
            bool anyRaycast = false;
            foreach (var img in clone.GetComponentsInChildren<Image>(true))
            {
                img.raycastTarget = true;
                anyRaycast = true;
            }
            if (!anyRaycast)
                Debug.LogWarning("[청사진버튼] 복제본에 Image 가 없어 클릭 판정 대상이 없다.");

            var btnComp = clone.GetComponent<BlueprintModeButton>();
            if (btnComp == null) btnComp = clone.gameObject.AddComponent<BlueprintModeButton>();
            var so = new SerializedObject(btnComp);
            so.FindProperty("label").objectReferenceValue = cloneLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[청사진버튼] 생성 완료 -> {PrefabPath}\n" +
                      $"  '{source.name}' 복제 -> '{CloneName}' (원본 위 {step:0}px - 해제/회전 간격과 동일). 클릭 = 청사진 모드 토글, 키캡 N.\n" +
                      "  아이콘/스프라이트는 나중에 교체하면 된다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 구운(Static) 폰트 아틀라스에 없는 글자를 추가한다. FontGlyphPatcher 와 같은 방식:
    // 소스 폰트 파일을 재연결하고 잠시 Dynamic 으로 바꿔 TryAddCharacters 후 Static 복귀.
    // 소스 폰트가 없으면 수동 재굽기 안내만 남긴다(빌드는 계속 진행 - 글자는 폴백으로 보임).
    private static void EnsureGlyphs(TMP_FontAsset font, string chars)
    {
        if (font == null || string.IsNullOrEmpty(chars)) return;

        // 이미 다 있으면 아무것도 안 한다
        if (font.HasCharacters(chars, out var missingList) || missingList == null || missingList.Count == 0)
            return;

        // 소스 폰트: 에셋에 연결돼 있으면 그것, 없으면 폰트 이름으로 검색해 재연결
        var so = new SerializedObject(font);
        var srcProp = so.FindProperty("m_SourceFontFile");
        Font src = srcProp != null ? srcProp.objectReferenceValue as Font : null;
        if (src == null)
        {
            // "PretendardVariable SDF" -> "PretendardVariable" 로 원본 파일 검색
            string baseName = font.name.Replace(" SDF", "").Trim();
            foreach (var guid in AssetDatabase.FindAssets($"{baseName} t:Font"))
            {
                var f = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
                if (f != null) { src = f; break; }
            }
        }
        if (src == null)
        {
            Debug.LogWarning($"[청사진버튼] '{font.name}' 의 소스 폰트 파일을 못 찾아 글자를 못 굽는다. " +
                             $"누락 글자: {new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(missingList, c => (char)c)))} " +
                             "- Font Asset Creator 로 재굽기 필요(폴백 폰트로는 일단 보인다).");
            return;
        }

        if (srcProp != null && srcProp.objectReferenceValue == null)
        {
            srcProp.objectReferenceValue = src;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var prevMode = font.atlasPopulationMode;
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.ReadFontAssetDefinition();

        bool ok = font.TryAddCharacters(chars, out string stillMissing);

        font.atlasPopulationMode = prevMode;
        font.ReadFontAssetDefinition();

        EditorUtility.SetDirty(font);
        if (font.atlasTextures != null)
            foreach (var t in font.atlasTextures) if (t != null) EditorUtility.SetDirty(t);
        AssetDatabase.SaveAssets();

        Debug.Log($"[청사진버튼] 폰트 '{font.name}' 글자 추가 성공={ok}" +
                  (string.IsNullOrEmpty(stillMissing) ? "" : $"  못추가='{stillMissing}'"));
    }
}
