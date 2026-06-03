using UnityEngine;
using UnityEngine.UI;

// 해제(X) 모드 동안 화면 가장자리에 빨간 비네트를 띄워 "지금 해제 모드"임을 알린다.
// Canvas / Image / 비네트 텍스처를 코드로 전부 생성하므로 별도 에셋이나 UI 셋업이 필요 없다.
// BuildManager 가 붙은 오브젝트(또는 빈 오브젝트)에 이 컴포넌트만 추가하면 동작한다.
public class DemolishModeOverlay : MonoBehaviour
{
    [SerializeField] private BuildManager buildManager;

    [Header("Vignette")]
    [SerializeField] private Color vignetteColor = new Color(0.9f, 0.12f, 0.12f, 1f);
    [Range(0f, 1f)]
    [Tooltip("최대 진하기(화면 가림 정도)")]
    [SerializeField] private float maxAlpha = 0.5f;
    [Tooltip("페이드 속도(클수록 빠르게 나타나고 사라짐)")]
    [SerializeField] private float fadeSpeed = 8f;

    private CanvasGroup canvasGroup;
    private float currentAlpha;

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
        bool on = buildManager != null && buildManager.IsBuildMode && buildManager.IsDemolishMode;
        float target = on ? 1f : 0f;

        // timeScale 영향 없이 부드럽게 페이드
        currentAlpha = Mathf.MoveTowards(currentAlpha, target, fadeSpeed * Time.unscaledDeltaTime);
        ApplyAlpha(currentAlpha);
    }

    private void ApplyAlpha(float t)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = t * maxAlpha;

        // 안 보일 땐 캔버스 자체를 꺼서 렌더 비용 0
        bool active = t > 0.001f;
        if (canvasGroup.gameObject.activeSelf != active)
            canvasGroup.gameObject.SetActive(active);
    }

    private void BuildOverlayUI()
    {
        var canvasGO = new GameObject("DemolishVignetteCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000; // 다른 UI 위로

        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false; // 클릭이 통과해야 해제 클릭을 막지 않음

        var imgGO = new GameObject("Vignette");
        imgGO.transform.SetParent(canvasGO.transform, false);

        var image = imgGO.AddComponent<Image>();
        image.raycastTarget = false;
        image.sprite = CreateVignetteSprite();
        image.color = vignetteColor;

        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 중앙은 투명, 가장자리로 갈수록 불투명해지는 radial 비네트 스프라이트를 코드로 생성.
    private Sprite CreateVignetteSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDist = center.magnitude;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                // 안쪽 55% 는 투명, 거기서부터 가장자리까지 부드럽게 진해짐
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, d));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
