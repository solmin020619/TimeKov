using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// 한글 폴백 폰트(Pretendard-ExtraBold SDF, Static 아틀라스)에 안 구워진 글자(엘리트 칭호의 '왕/군' 등)를 추가한다.
// Static 폰트라 새 글자가 □로 떠서, 소스 otf를 다시 연결하고 TryAddCharacters로 아틀라스에 구워넣는다.
// 한 번 구워두면 Static 유지(추가 churn 없음) + □ 사라짐. 칭호/이름에 새 글자 쓰게 되면 CHARS에 더하고 재실행.
// 메뉴: Tools > VFX > Patch Korean Font Glyphs
public static class FontGlyphPatcher
{
    const string FontPath = "Assets/11.Font/Pretendard-ExtraBold SDF.asset";
    const string SourceOtf = "Assets/11.Font/Pretendard-ExtraBold.otf";

    // 엘리트 칭호 + 흔한 칭호 글자(이미 구워진 건 자동 스킵). 새 이름 글자 생기면 여기 추가.
    const string CHARS = "아귀군주늑대왕거미여언데드쥐해골장정예대괴물우두머리흉포광폭";

    [MenuItem("Tools/TIMEKOV/VFX/한글 폰트 글리프 패치 (왕군 등)")]
    public static void Patch()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) { Debug.LogError("[FontGlyphPatcher] 폰트 못 찾음: " + FontPath); return; }

        // 1) 소스 폰트 재연결 (없으면 새 글자 렌더 불가)
        var src = AssetDatabase.LoadAssetAtPath<Font>(SourceOtf);
        if (src == null)
        {
            EditorUtility.DisplayDialog("소스 폰트 없음",
                $"{SourceOtf} 가 없어 스크립트로 못 굽는다.\n" +
                "Window > TextMeshPro > Font Asset Creator 로 직접 구워야 함.", "확인");
            return;
        }
        var so = new SerializedObject(font);
        var sp = so.FindProperty("m_SourceFontFile");
        if (sp != null) sp.objectReferenceValue = src;
        var spp = so.FindProperty("m_SourceFontFilePath");
        if (spp != null) spp.stringValue = SourceOtf;
        so.ApplyModifiedProperties();

        // 2) Dynamic 으로 잠시 바꿔 글자 굽고 -> 다시 Static
        var prevMode = font.atlasPopulationMode;
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.ReadFontAssetDefinition();

        bool ok = font.TryAddCharacters(CHARS, out string missing);

        font.atlasPopulationMode = prevMode;   // Static 복귀(churn 방지)
        font.ReadFontAssetDefinition();

        // 3) 저장(아틀라스 텍스처 포함)
        EditorUtility.SetDirty(font);
        if (font.atlasTextures != null)
            foreach (var t in font.atlasTextures) if (t != null) EditorUtility.SetDirty(t);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FontGlyphPatcher] 글자 추가 성공={ok}  못추가='{missing}'  (대상 폰트: {font.name})\n" +
                  "도감/HP바에서 '왕/군' 등이 □ 대신 제대로 보이는지 확인.\n" +
                  "안 되면 Window > TextMeshPro > Font Asset Creator 로 Pretendard-ExtraBold.otf 다시 구우면 확실.");
    }
}
