// =====================================================================
// Editor/FacilityIconRenderer.cs
// Tools/TIMEKOV/설비/연마기 아이콘 렌더 (facility_10.png)
//
// 설비 UI 중앙 배경 + 건축바 아이콘으로 쓰는 500x500 투명 PNG 를
// 모델을 직접 찍어서 만든다. 기존 facility_1~9 와 같은 규격.
//
// [왜 직접 찍나]
//   기존 9장은 설계도가 아니라 '흰 배경 없는 3D 렌더'다. 같은 모델이 이미 프로젝트에
//   있으므로 그림을 새로 그리는 것보다 찍는 쪽이 정확하고 톤도 자동으로 맞는다.
//
// [투명 배경]
//   카메라 배경을 알파 0 으로 두고 ARGB32 로 렌더한다. URP 후처리는 알파를 뭉개므로 끈다.
//
// [조명]
//   씬 조명에 좌우되지 않도록 전용 조명을 임시로 만들어 쓰고, 렌더가 끝나면 전부 지운다.
//   (씬을 더럽히지 않는다 - 실행 후 하이어라키에 아무것도 남지 않는다)
// =====================================================================

using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;

public static class FacilityIconRenderer
{
    private const string ArtPath = "Assets/03.Model/Re_Base/3x3,5x5,object/5x5/연마기/연마기.prefab";
    private const string OutputPath = "Assets/Resources/Facilities/facility_10.png";
    private const int Size = 500;

    // ── 구도 조정값 (결과 보고 여기만 바꾸면 된다) ──────────────────
    /// 카메라 내려다보는 각도(도). 0 = 정면, 클수록 위에서 본다.
    private const float PitchDeg = 18f;
    /// 좌우 회전(도). 0 = 정면.
    private const float YawDeg = 0f;
    /// 여백. 1 = 딱 맞게, 1.15 = 15% 여유.
    private const float Padding = 1.12f;

    [MenuItem("Tools/TIMEKOV/설비/연마기 아이콘 렌더 (facility_10.png)")]
    public static void Render()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtPath);
        if (prefab == null) { Debug.LogError($"[아이콘렌더] 모델을 못 찾았다: {ArtPath}"); return; }

        // 씬의 다른 오브젝트와 겹치지 않도록 멀리 떨어진 곳에서 찍는다.
        Vector3 stage = new Vector3(0f, 10000f, 0f);

        var target = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        target.transform.position = stage;
        target.transform.rotation = Quaternion.identity;

        var camGo = new GameObject("[IconCam]");
        var cam = camGo.AddComponent<Camera>();
        var lightGo = new GameObject("[IconLight]");
        var fillGo  = new GameObject("[IconFill]");
        var rimGo   = new GameObject("[IconRim]");
        RenderTexture rt = null;
        Texture2D shot = null;

        try
        {
            // 모델 전체 크기 계산(렌더러 바운즈 합집합)
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { Debug.LogError("[아이콘렌더] 렌더러가 없다."); return; }

            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) if (r.enabled) b.Encapsulate(r.bounds);

            // 카메라 - 직교 투영이라 원근 왜곡 없이 규격이 일정하다
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // 투명
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * Padding;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 5000f;
            cam.transform.rotation = Quaternion.Euler(PitchDeg, YawDeg, 0f);
            cam.transform.position = b.center - cam.transform.forward * (b.size.magnitude * 2f + 10f);

            // URP 후처리/안티에일리어싱은 알파를 망친다 - 끈다
            var urp = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (urp == null) urp = camGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = false;
            urp.antialiasing = AntialiasingMode.None;
            urp.renderShadows = false;

            // 조명 3점 - 씬 조명에 휘둘리지 않게 밝게 깐다
            SetupLight(lightGo, new Vector3(35f, -25f, 0f), 1.35f);
            SetupLight(fillGo,  new Vector3(15f,  60f, 0f), 0.65f);
            SetupLight(rimGo,   new Vector3(-20f, 190f, 0f), 0.5f);

            Debug.Log($"[아이콘렌더] 모델 바운즈 center={b.center} size={b.size} / " +
                      $"카메라 pos={cam.transform.position} ortho={cam.orthographicSize:0.00} / 렌더러 {renderers.Length}개");

            // 렌더 - 알파를 살리기 위해 ARGB32 + sRGB
            rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 8;   // MSAA 로 계단 완화(후처리 AA 대신)
            cam.targetTexture = rt;

            // ★URP 에서는 cam.Render() 가 파이프라인을 제대로 타지 않아 빈 이미지가 나온다.
            //   SubmitRenderRequest 가 정식 경로 - 지원 안 하는 환경에서만 예전 방식으로 떨어진다.
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
            else
                cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            shot = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            shot.Apply();
            RenderTexture.active = prev;

            // 결과 검사 - 전부 투명이면 저장해봐야 빈 그림이다. 원인 후보를 알려주고 멈춘다.
            int opaque = 0;
            var px = shot.GetPixels32();
            for (int i = 0; i < px.Length; i += 7) if (px[i].a > 8) opaque++;
            if (opaque == 0)
            {
                Debug.LogError("[아이콘렌더] 렌더 결과가 비어 있다(전부 투명). 저장하지 않는다.\n" +
                               "  확인: 모델이 카메라 앞에 있는지(위 바운즈 로그), URP 렌더 요청 지원 여부.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllBytes(OutputPath, shot.EncodeToPNG());
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
            ApplyImportSettings(OutputPath);

            Debug.Log($"[아이콘렌더] 완료 -> {OutputPath} ({Size}x{Size}, 투명 배경)\n" +
                      $"  구도: 내려다보기 {PitchDeg}도 / 좌우 {YawDeg}도 / 여백 {Padding:0.00}\n" +
                      "  각도나 크기가 마음에 안 들면 이 파일 상단 값 3개만 바꿔 다시 실행하면 된다.");
        }
        finally
        {
            if (cam != null) cam.targetTexture = null;
            RenderTexture.active = null;
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            if (shot != null) Object.DestroyImmediate(shot);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(fillGo);
            Object.DestroyImmediate(rimGo);
            Object.DestroyImmediate(target);
        }
    }

    private static void SetupLight(GameObject go, Vector3 euler, float intensity)
    {
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        go.transform.rotation = Quaternion.Euler(euler);
    }

    // 기존 facility_1~9 와 같은 임포트 설정(스프라이트, 알파 투명, 2048)
    private static void ApplyImportSettings(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.maxTextureSize = 2048;
        imp.SaveAndReimport();
    }
}
