using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class ScreenVignette : MonoBehaviour
{
    [Header("비네트 설정")]
    [Range(0f, 1f)] public float vignetteStrength = 0.5f;
    [Range(0.05f, 0.8f)] public float vignetteFalloff = 0.4f;
    public int textureSize = 256;

    private RawImage _img;
    private Texture2D _tex;
    private float _lastStrength = -1f;
    private float _lastFalloff = -1f;

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (!Mathf.Approximately(_lastStrength, vignetteStrength) ||
            !Mathf.Approximately(_lastFalloff, vignetteFalloff))
            Apply();
    }

    private void Apply()
    {
        _img = GetComponent<RawImage>();
        if (_img == null) return;

        _img.raycastTarget = false;
        _img.color = Color.white;

        // 기존 텍스처 제거
        if (_tex != null)
        {
            if (Application.isPlaying) Destroy(_tex);
            else DestroyImmediate(_tex);
        }

        // 새 텍스처 생성
        _tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        _tex.filterMode = FilterMode.Bilinear;
        _tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float u = x / (float)(textureSize - 1);
                float v = y / (float)(textureSize - 1);

                // 중심에서 타원형 거리 (화면 비율 보정)
                float dx = (u - 0.5f) * 2f;
                float dy = (v - 0.5f) * 2f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // smoothstep으로 경계를 흐릿하게
                // falloff 값이 클수록 그래디언트 범위가 넓어져 경계가 더 부드러움
                float inner = 0.1f;                          // 완전 투명 구간
                float outer = inner + vignetteFalloff * 1.5f; // 그래디언트 구간 (넓게)
                float t = Mathf.Clamp01((dist - inner) / (outer - inner));
                // smoothstep 두 번 적용 → 경계가 매우 부드러워짐
                t = t * t * (3f - 2f * t);
                t = t * t * (3f - 2f * t);
                float alpha = t * vignetteStrength;
                byte a = (byte)(Mathf.Clamp01(alpha) * 255f);

                pixels[y * textureSize + x] = new Color32(0, 0, 0, a);
            }
        }

        _tex.SetPixels32(pixels);
        _tex.Apply();

        _img.texture = _tex;

        _lastStrength = vignetteStrength;
        _lastFalloff = vignetteFalloff;
    }

    private void OnDestroy()
    {
        if (_tex != null)
        {
            if (Application.isPlaying) Destroy(_tex);
            else DestroyImmediate(_tex);
        }
    }
}