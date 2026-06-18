using UnityEngine;
using UnityEngine.UI;

// 해제(X) 모드 동안 화면 가장자리를 테두리(프레임)로 감싸 "지금 해제 모드"임을 알린다.
// Canvas / Image / 테두리 텍스처를 코드로 전부 생성하므로 별도 에셋이나 UI 셋업이 필요 없다.
// BuildManager 가 붙은 오브젝트(또는 빈 오브젝트)에 이 컴포넌트만 추가하면 동작한다.
public class DemolishModeOverlay : MonoBehaviour
{
    [SerializeField] private BuildManager buildManager;

    [Header("Border")]
    [Tooltip("테두리 색 (알파는 maxAlpha 로 따로 제어하니 1 로 둬도 됨)")]
    [SerializeField] private Color borderColor = new Color(0.9f, 0.12f, 0.12f, 1f);
    [Range(0f, 1f)]
    [Tooltip("최대 진하기 (화면 가림 정도)")]
    [SerializeField] private float maxAlpha = 0.45f;
    [Range(0.02f, 0.5f)]
    [Tooltip("테두리 두께 (화면 짧은 변 대비 비율)")]
    [SerializeField] private float thickness = 0.1f;
    [Tooltip("페이드 속도 (클수록 빠르게 나타나고 사라짐)")]
    [SerializeField] private float fadeSpeed = 8f;

    private CanvasGroup canvasGroup;
    private Image image;
    private Texture2D borderTex;
    private float currentAlpha;
    private GameObject _canvasGO;

    private int builtW = -1;
    private int builtH = -1;
    private float builtThickness = -1f;

    private void Awake()
    {
        if (buildManager == null)
            buildManager = GetComponent<BuildManager>();
        if (buildManager == null)
            buildManager = FindFirstObjectByType<BuildManager>();

        BuildOverlayUI();
        ApplyAlpha(0f);
    }

    private void Update()
    {
        // 화면 크기나 두께가 바뀌면 테두리 텍스처 재생성 (둘 다 안 바뀌면 무비용)
        if (Screen.width != builtW || Screen.height != builtH || !Mathf.Approximately(thickness, builtThickness))
            RebuildBorderTexture();

        if (image != null)
            image.color = borderColor;

        bool on = buildManager != null && buildManager.IsBuildMode && buildManager.IsDemolishMode;
        float target = on ? 1f : 0f;

        // timeScale 영향 없이 부드럽게 페이드
        currentAlpha = Mathf.MoveTowards(currentAlpha, target, fadeSpeed * Time.unscaledDeltaTime);
        ApplyAlpha(currentAlpha);
    }

    private void ApplyAlpha(float t)
    {
        if (canvasGroup != null) canvasGroup.alpha = t * maxAlpha;

        // 안 보일 땐 캔버스 자체를 꺼서 렌더 비용 0
        bool active = t > 0.001f;
        if (_canvasGO != null && _canvasGO.activeSelf != active)
            _canvasGO.SetActive(active);
    }

    private void BuildOverlayUI()
    {
        var canvasGO = new GameObject("DemolishBorderCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvasGO = canvasGO;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000; // 다른 UI 위로

        var imgGO = new GameObject("Border");
        imgGO.transform.SetParent(canvasGO.transform, false);

        canvasGroup = imgGO.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false; // 클릭이 통과해야 해제 클릭을 막지 않음

        image = imgGO.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = borderColor;

        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        RebuildBorderTexture();
    }

    // 가장자리에서 안쪽으로 갈수록 투명해지는, 4면 두께가 일정한 테두리 텍스처.
    // 화면 비율로 만들어 풀스크린 stretch 해도 테두리 두께가 균일하게 유지된다.
    private void RebuildBorderTexture()
    {
        int w = Mathf.Max(16, Screen.width / 4);
        int h = Mathf.Max(16, Screen.height / 4);

        if (borderTex == null || borderTex.width != w || borderTex.height != h)
        {
            if (borderTex != null) Destroy(borderTex);
            borderTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        float thicknessPix = Mathf.Max(1f, thickness * Mathf.Min(w, h));
        var pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 네 변 중 가장 가까운 가장자리까지의 픽셀 거리
                float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                float a = 1f - Mathf.SmoothStep(0f, thicknessPix, edge);
                pixels[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }

        borderTex.SetPixels(pixels);
        borderTex.Apply();

        if (image != null)
        {
            var old = image.sprite;
            image.sprite = Sprite.Create(borderTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            if (old != null) Destroy(old);
        }

        builtW = Screen.width;
        builtH = Screen.height;
        builtThickness = thickness;
    }

    private void OnDestroy()
    {
        if (image != null && image.sprite != null) Destroy(image.sprite);
        if (borderTex != null) Destroy(borderTex);
    }
}
