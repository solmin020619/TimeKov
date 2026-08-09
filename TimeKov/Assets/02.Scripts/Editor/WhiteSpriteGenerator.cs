#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;

// 선택한 텍스처(.png)의 색(채도)만 빼고 명암/광택/투명은 유지한 흰색 버전을 생성한다.
// 게이지 바 스프라이트를 무채색으로 만들어, Unity Image 의 Color(틴트)로 자유롭게 색칠하기 위함.
// png 파일을 직접 읽으므로 원본의 Read/Write Enabled 를 켤 필요가 없다.
public static class WhiteSpriteGenerator
{
    [MenuItem("Tools/TIMEKOV/UI/흰색 스프라이트 생성 (선택 텍스처)")]
    public static void Generate()
    {
        Object sel = Selection.activeObject;
        string path = sel != null ? AssetDatabase.GetAssetPath(sel) : null;

        if (string.IsNullOrEmpty(path) || !(path.EndsWith(".png") || path.EndsWith(".PNG")))
        {
            EditorUtility.DisplayDialog("흰색 스프라이트 생성",
                "Project 창에서 변환할 텍스처(.png)를 먼저 선택한 뒤 실행하세요.", "확인");
            return;
        }

        // 원본 png 바이트를 직접 읽어 readable 텍스처로 로드 (import 설정 무관)
        byte[] raw = File.ReadAllBytes(path);
        Texture2D src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!src.LoadImage(raw))
        {
            Object.DestroyImmediate(src);
            EditorUtility.DisplayDialog("흰색 스프라이트 생성", "이미지를 읽지 못했습니다.", "확인");
            return;
        }

        Color[] pixels = src.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            // 채도 제거 + 명도(밝기) 유지: 가장 밝은 채널값을 회색조로 사용 (HSV 의 S=0, V 유지)
            float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            pixels[i] = new Color(v, v, v, c.a);  // 알파(투명) 그대로
        }

        Texture2D dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        dst.SetPixels(pixels);
        dst.Apply();

        string dir = Path.GetDirectoryName(path);
        string baseName = Path.GetFileNameWithoutExtension(path);
        string outPath = Path.Combine(dir, baseName + "_White.png").Replace("\\", "/");
        File.WriteAllBytes(outPath, dst.EncodeToPNG());

        Object.DestroyImmediate(src);
        Object.DestroyImmediate(dst);

        AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);

        // 원본과 같은 Sprite 설정으로 import
        TextureImporter srcImp = AssetImporter.GetAtPath(path) as TextureImporter;
        TextureImporter dstImp = AssetImporter.GetAtPath(outPath) as TextureImporter;
        if (dstImp != null)
        {
            // dst 기본 설정을 읽어 색/sRGB/알파/압축은 그대로 두고, 크기.모양 관련만 원본에서 가져온다.
            // (원본 설정을 통째로 복사하면 색공간/알파가 달라져 바가 안 보이는 경우가 있어 분리)
            TextureImporterSettings dstS = new TextureImporterSettings();
            dstImp.ReadTextureSettings(dstS);
            if (srcImp != null)
            {
                TextureImporterSettings srcS = new TextureImporterSettings();
                srcImp.ReadTextureSettings(srcS);
                dstS.spriteMode          = srcS.spriteMode;
                dstS.spritePixelsPerUnit = srcS.spritePixelsPerUnit;
                dstS.spriteAlignment     = srcS.spriteAlignment;
                dstS.spritePivot         = srcS.spritePivot;
                // spriteBorder 는 복사하지 않음 — border 가 있으면 Image 가 Sliced 로 자동 전환돼
                // Filled(게이지) 동작이 깨짐
            }
            // Filled 게이지 바는 Full Rect 여야 채움.크기가 정확하다 (Tight 면 어긋남)
            dstS.spriteMeshType = SpriteMeshType.FullRect;
            dstImp.SetTextureSettings(dstS);
            dstImp.textureType = TextureImporterType.Sprite;
            dstImp.SaveAndReimport();
        }

        Sprite result = AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
        Selection.activeObject = result;
        EditorGUIUtility.PingObject(result);

        EditorUtility.DisplayDialog("흰색 스프라이트 생성",
            "생성 완료:\n" + outPath + "\n\nFill 의 Source Image 에 이 스프라이트를 연결하세요.", "확인");
    }
}
#endif
