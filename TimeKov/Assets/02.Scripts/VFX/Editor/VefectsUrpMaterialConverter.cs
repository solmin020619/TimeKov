using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Vefects "Anime Stylized VFX" 팩은 이 프로젝트에서 두 군데가 깨져 있다:
//   1) 셰이더가 Built-in 전용(_BIRP, #pragma surface) -> URP 에서 컴파일 안 됨 -> 마젠타
//   2) 머티리얼이 가리키는 텍스처 guid 가 실제 텍스처 파일 guid 와 안 맞음 -> 텍스처 링크 끊김 -> 안 보임
//
// 이 메뉴가 둘 다 고친다:
//   - 셰이더를 URP Particles/Unlit 로 교체 + 원본 블렌드(_Src/_Dst) 복제(불=additive / 연기=alpha)
//   - 깨진 텍스처 링크를 '이름'으로 다시 연결: M_VFX_Part_01 -> T_VFX_Part_01 식(머티리얼명 ~ 텍스처명 일치).
//     전용 텍스처가 없는 머티리얼(Flames/Flare/Flat 등)은 부드러운 Cloud 텍스처로 대체.
//   - additive 는 원래 그라데이션/emission 으로 밝았으니 URP 에선 HDR 밝기 부스트.
//   - 왜곡(distortion)은 URP 비호환이라 숨김.
//
// 블렌드(_Src/_Dst)는 디스크 .mat 직접 파싱(깨진 셰이더라 ShaderUtil 못 씀). 변환 전 머티리얼 폴더 git 원복 권장.
// 머티리얼은 공유 에셋이라 변환 즉시 와이번 VFX 프리팹에 자동 반영.
// 메뉴: Tools > VFX > Convert Vefects (Anime) Materials To URP
public static class VefectsUrpMaterialConverter
{
    const string MaterialsFolder = "Assets/Vefects/Anime Stylized VFX/Shared/Materials";
    const string TexturesFolder = "Assets/Vefects/Anime Stylized VFX/Shared/Textures";
    const string UrpParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";
    const string FallbackTexName = "T_VFX_Part_05";    // 전용 텍스처 없는 머티리얼용. 부드러운 회색 라디얼 글로우(=불).
                                                       // Cloud 는 꽉 찬 흰색이라 additive 에서 흰 박스가 됨(부적합).

    const float AdditiveBrightness = 1.0f;             // 1.0=부스트 없음(원본 밝기). 텍스처 연결됐으니 부스트 불필요.
                                                       // 높이면 큰 글로우 파티클이 하얗게 번져 박스처럼 됨(2.5는 그래서 안 됨).
    const int NameMatchThreshold = 3;                  // 이름 앞부분 최소 일치 글자수

    [MenuItem("Tools/VFX/Convert Vefects (Anime) Materials To URP")]
    public static void Convert()
    {
        var shader = Shader.Find(UrpParticlesUnlit);
        if (shader == null)
        {
            EditorUtility.DisplayDialog("변환 실패", $"URP 셰이더를 못 찾음:\n{UrpParticlesUnlit}", "확인");
            return;
        }
        if (!AssetDatabase.IsValidFolder(MaterialsFolder))
        {
            EditorUtility.DisplayDialog("변환 실패", $"머티리얼 폴더 없음:\n{MaterialsFolder}", "확인");
            return;
        }

        var textures = LoadTextures();                 // (정규화이름, 텍스처)
        var fallback = FindTexture(textures, FallbackTexName);

        var guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder });
        var mats = new List<Material>();
        foreach (var g in guids)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
            if (m != null) mats.Add(m);
        }
        if (mats.Count == 0)
        {
            EditorUtility.DisplayDialog("변환 대상 없음", $"{MaterialsFolder} 안에 머티리얼이 없음.", "확인");
            return;
        }

        bool ok = EditorUtility.DisplayDialog("Vefects -> URP 변환",
            $"{mats.Count}개 머티리얼을 URP Particles/Unlit 로 변환 + 깨진 텍스처를 이름으로 재연결합니다.\n\n" +
            "- 원본 블렌드 복제, additive 는 HDR 밝기 부스트\n" +
            $"- 전용 텍스처 없는 건 {FallbackTexName} 로 대체, 왜곡은 숨김\n\n진행?", "변환", "취소");
        if (!ok) return;

        int converted = 0, hidden = 0, remapped = 0, fellBack = 0;
        var summary = new List<string>();
        foreach (var mat in mats)
        {
            bool isDistortion = mat.name.ToLowerInvariant().Contains("disto");

            string text = ReadMatText(mat);
            // 원본은 _Src/_Dst, 이미 변환된 재실행은 _SrcBlend/_DstBlend 에서 읽음(둘 다 대응 = 멱등)
            float src = ReadDiskFloat(text, "_Src", float.NaN);
            if (float.IsNaN(src)) src = ReadDiskFloat(text, "_SrcBlend", (float)BlendMode.One);
            float dst = ReadDiskFloat(text, "_Dst", float.NaN);
            if (float.IsNaN(dst)) dst = ReadDiskFloat(text, "_DstBlend", (float)BlendMode.One);
            bool additive = Mathf.Approximately(dst, (float)BlendMode.One);

            // 이름으로 텍스처 재연결(깨진 guid 무시)
            Texture tex = null;
            string how = "숨김(distortion)";
            if (!isDistortion)
            {
                tex = MatchTextureByName(mat.name, textures, out bool exact);
                if (tex != null) { remapped++; how = $"{(exact ? "이름매칭" : "근사")}={tex.name}"; }
                else { tex = fallback; fellBack++; how = $"대체={(fallback != null ? fallback.name : "없음")}"; }
            }
            bool hide = isDistortion || tex == null;

            mat.shader = shader;
            SetUrpBlend(mat, src, dst, additive);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", hide ? null : tex);
            if (mat.HasProperty("_BaseColor"))
            {
                Color c = hide ? new Color(0f, 0f, 0f, 0f)
                        : additive ? new Color(AdditiveBrightness, AdditiveBrightness, AdditiveBrightness, 1f)
                        : Color.white;
                mat.SetColor("_BaseColor", c);
            }

            EditorUtility.SetDirty(mat);
            converted++;
            if (hide) hidden++;
            summary.Add($"  {mat.name}  [{(additive ? "ADD" : "ALPHA")}]  {how}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"[VefectsUrpConverter] {converted}개 변환 — 이름재연결 {remapped} / 대체 {fellBack} / 숨김 {hidden}.\n" +
            string.Join("\n", summary) + "\n\n" +
            "와이번 공격이 불처럼 보이는지 확인. 밝기는 AdditiveBrightness 조정.");
    }

    // ===== 텍스처 이름 매칭 =====

    static Dictionary<string, Texture> LoadTextures()
    {
        var dict = new Dictionary<string, Texture>();
        if (!AssetDatabase.IsValidFolder(TexturesFolder)) return dict;
        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesFolder }))
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(g));
            if (t == null) continue;
            string key = Norm(StripPrefix(t.name));   // "T_VFX_Part_01" -> "part01"
            if (!dict.ContainsKey(key)) dict[key] = t;
        }
        return dict;
    }

    static Texture FindTexture(Dictionary<string, Texture> textures, string rawName)
    {
        string key = Norm(StripPrefix(rawName));
        return textures.TryGetValue(key, out var t) ? t : null;
    }

    // 머티리얼명과 가장 비슷한(앞부분 일치 긴) 텍스처를 고른다. 임계값 미만이면 null(대체로).
    static Texture MatchTextureByName(string matName, Dictionary<string, Texture> textures, out bool exact)
    {
        exact = false;
        string matKey = Norm(StripPrefix(matName));    // "M_VFX_Part_01_Bright" -> "part01bright"
        Texture best = null; int bestLcp = 0;
        foreach (var kv in textures)
        {
            int lcp = CommonPrefix(matKey, kv.Key);
            if (lcp > bestLcp) { bestLcp = lcp; best = kv.Value; exact = (matKey == kv.Key); }
        }
        return bestLcp >= NameMatchThreshold ? best : null;
    }

    static string StripPrefix(string n)
    {
        // "T_VFX_Part_01" / "M_VFX_Part_01" 의 접두어 제거
        int i = n.IndexOf("VFX_", System.StringComparison.OrdinalIgnoreCase);
        return i >= 0 ? n.Substring(i + 4) : n;
    }

    static string Norm(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    static int CommonPrefix(string a, string b)
    {
        int n = Mathf.Min(a.Length, b.Length), i = 0;
        while (i < n && a[i] == b[i]) i++;
        return i;
    }

    // ===== 블렌드 셋업 / 디스크 파싱 =====

    static void SetUrpBlend(Material mat, float src, float dst, bool additive)
    {
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", additive ? 2f : 0f);
        mat.SetFloat("_SrcBlend", src);
        mat.SetFloat("_DstBlend", dst);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    static string ReadMatText(Material mat)
    {
        string p = AssetDatabase.GetAssetPath(mat);
        return File.Exists(p) ? File.ReadAllText(p) : "";
    }

    static float ReadDiskFloat(string text, string prop, float fallback)
    {
        if (string.IsNullOrEmpty(text)) return fallback;
        var m = Regex.Match(text, @"-\s*" + Regex.Escape(prop) + @":\s*([-0-9.eE]+)");
        if (m.Success && float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return v;
        return fallback;
    }
}
