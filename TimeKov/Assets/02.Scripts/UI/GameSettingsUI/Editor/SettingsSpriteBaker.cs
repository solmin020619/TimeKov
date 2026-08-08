// SettingsSpriteBaker.cs (Editor 전용)
// ============================================================================
// 런타임에 만들던 스프라이트(라운드 사각형 9-slice, 아이콘 SDF)를 PNG 에셋으로 굽고,
// 반대로 구워둔 에셋을 UISprites.Resolver에 꽂아 주는 역할.
//
// 왜 필요한가:
//   Rounded.Get()/Icons.*()는 Texture2D를 코드로 만들어 Sprite.Create로 감싼다.
//   이건 씬에 직렬화할 수 없어서(에셋 파일이 아니므로) UI를 미리 배치해 두면
//   모든 Image의 sprite가 None이 된다. 씬 배치로 넘어가려면 먼저 파일이어야 한다.
//
// 이름 규칙은 런타임 쪽과 공유한다 — Rounded.AssetName() / Icons.AssetName().
// 여기서 임의로 정하면 베이크한 계층이 스프라이트를 못 찾는다.
//
// 사용법: 메뉴 Tools ▸ GameSettingsUI ▸ 스프라이트 굽기
// ============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameSettingsUI.EditorTools
{
    public static class SettingsSpriteBaker
    {
        public const string OutDir = "Assets/02.Scripts/UI/GameSettingsUI/Generated";

        // 빌더가 Panel(..., radius, ...)로 쓰는 모든 반지름. 하나라도 빠지면 그 위젯만 각지게 나온다.
        static readonly int[] Radii = { 2, 9, 15, 16, 18, 20, 21, 22, 27, 28, 30, 31 };

        [MenuItem("Tools/GameSettingsUI/스프라이트 굽기")]
        public static void Bake()
        {
            Directory.CreateDirectory(OutDir);

            // 굽는 동안에는 리졸버를 꺼둔다. 켜져 있으면 이전에 구운 에셋을 그대로
            // 되돌려받아 자기 자신을 다시 굽는 꼴이 된다.
            var saved = UISprites.Resolver;
            UISprites.Resolver = null;

            var written = new List<(string path, int border)>();
            try
            {
                foreach (int r in Radii)
                    written.Add((SavePng(Rounded.AssetName(r), Rounded.Get(r).texture), r));

                foreach (var (key, make) in Icons.All)
                    written.Add((SavePng(Icons.AssetName(key), make().texture), 0));
            }
            finally { UISprites.Resolver = saved; }

            AssetDatabase.Refresh();

            // 임포트 설정은 파일이 존재해야 지정할 수 있으므로 Refresh 뒤에 건다.
            foreach (var (path, border) in written) ConfigureImporter(path, border);

            AssetDatabase.Refresh();
            Debug.Log($"[SettingsSpriteBaker] {written.Count}개 스프라이트를 {OutDir} 에 구웠습니다.");
        }

        /// 구워둔 에셋을 이름으로 찾아주는 리졸버. 씬 베이크 중에만 꽂는다.
        public static System.Func<string, Sprite> MakeResolver()
        {
            var map = new Dictionary<string, Sprite>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { OutDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp) map[Path.GetFileNameWithoutExtension(path)] = sp;
            }
            return name => map.TryGetValue(name, out var s) ? s : null;
        }

        // 런타임 텍스처는 isReadable이 아닐 수 있어 EncodeToPNG가 실패할 수 있다.
        // 픽셀을 새 읽기 가능 텍스처로 복사한 뒤 인코딩한다.
        static string SavePng(string name, Texture2D src)
        {
            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.SetPixels32(src.GetPixels32());
            copy.Apply();

            string path = $"{OutDir}/{name}.png";
            File.WriteAllBytes(path, copy.EncodeToPNG());
            Object.DestroyImmediate(copy);
            return path;
        }

        static void ConfigureImporter(string path, int border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.mipmapEnabled       = false;
            importer.filterMode          = FilterMode.Bilinear;
            importer.wrapMode            = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 100f;
            // 라운드 코너를 늘려도 안 뭉개지게 하는 9-slice 경계. 아이콘은 0(단순 스케일).
            importer.spriteBorder = border > 0
                ? new Vector4(border, border, border, border)
                : Vector4.zero;
            // 알파 그라데이션(안티에일리어싱)이 압축으로 뭉개지면 테두리가 지저분해진다.
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.SaveAndReimport();
        }
    }
}
#endif
