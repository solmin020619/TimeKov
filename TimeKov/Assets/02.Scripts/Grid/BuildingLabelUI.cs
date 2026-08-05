using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class BuildingLabelUI : MonoBehaviour
{


    public TMP_FontAsset fontAsset;

    public Vector3 labelOffset = new Vector3(0f, 10.0f, 10.0f);

    public float fontSize = 5.2f;
    public float iconSize = 1.3f;

    public float iconTextGap = 0.1f;

    public float bgScale = 0.92f;
    public float bgPopHeight = 0.01f;
    public Color bgFillColor = new Color(0.05f, 0.06f, 0.08f, 0.6f);
    public Color bgBorderColor = new Color(0.25f, 0.55f, 0.95f, 0.55f);

    public float iconLiftOffset = 0.12f;
    public float shadowScale = 1.0f;
    public Vector2 shadowOffset = new Vector2(0f, -0.16f);
    public Color shadowColor = new Color(0f, 0f, 0f, 0.55f);

    public float textRotX = 0f;
    public float textRotY = 0f;
    public float textRotZ = 0f;

    [HideInInspector] public float maxWidth = 99f;
    [HideInInspector] public float bgHeight = 1.9f;


    private GameObject _root;
    private TextMeshPro _nameText;
    private MeshRenderer _iconRenderer;
    private MeshRenderer _iconBgRenderer;
    private MeshRenderer _iconShadowRenderer;
    private Texture2D _bgTexture;
    private Texture2D _shadowTexture;

    private string _facilityKey;

    public void Setup(int facilityId, string facilityKey, Sprite icon)
    {
        _facilityKey = facilityKey;

        Loc.OnLanguageChanged -= RefreshText;
        Loc.OnLanguageChanged += RefreshText;

        if (_root == null) CreateLabel();

        if (_nameText != null)
        {
            if (fontAsset != null) _nameText.font = fontAsset;
            _nameText.text = Loc.Get(facilityKey);
            _nameText.ForceMeshUpdate();
        }

        bool hasIcon = icon != null;
        if (_iconRenderer != null)
        {
            if (hasIcon)
            {
                _iconRenderer.material.mainTexture = icon.texture;
                _iconRenderer.gameObject.SetActive(true);
            }
            else
            {
                _iconRenderer.gameObject.SetActive(false);
            }
        }
        if (_iconBgRenderer != null)
            _iconBgRenderer.gameObject.SetActive(hasIcon);
        if (_iconShadowRenderer != null)
            _iconShadowRenderer.gameObject.SetActive(hasIcon);

        RepositionElements(hasIcon);
    }

    public void ShowLabel() { if (_root != null) _root.SetActive(true); }
    public void HideLabel() { if (_root != null) _root.SetActive(false); }

    private void OnDestroy()
    {
        Loc.OnLanguageChanged -= RefreshText;
    }

    private void RefreshText()
    {
        if (string.IsNullOrEmpty(_facilityKey) || _nameText == null) return;
        _nameText.text = Loc.Get(_facilityKey);
        _nameText.ForceMeshUpdate();
    }

    private void Awake() { }

    private void LateUpdate()
    {
        if (_root != null)
            _root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (_nameText != null)
        {
            _nameText.transform.localRotation = Quaternion.Euler(textRotX, textRotY, textRotZ);

            if (!string.IsNullOrEmpty(_facilityKey))
            {
                string expected = Loc.Get(_facilityKey);
                if (_nameText.text != expected)
                {
                    _nameText.text = expected;
                    _nameText.ForceMeshUpdate();
                }
            }
        }
    }


    private void CreateLabel()
    {
        _root = new GameObject("_BuildingLabel");
        _root.transform.SetParent(transform, false);
        _root.transform.localPosition = labelOffset;
        _root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _root.layer = gameObject.layer;

        var bgGO = CreateTextureQuad("IconBG", Vector3.zero, iconSize * bgScale, iconSize * bgScale);
        _iconBgRenderer = bgGO.GetComponent<MeshRenderer>();
        if (_bgTexture == null) _bgTexture = CreateBadgeTexture(64, bgFillColor, bgBorderColor);
        _iconBgRenderer.material.mainTexture = _bgTexture;
        _iconBgRenderer.material.color = Color.white;

        var shadowGO = CreateTextureQuad("IconShadow", new Vector3(0f, 0f, -bgPopHeight * 0.5f), iconSize * shadowScale, iconSize * shadowScale);
        _iconShadowRenderer = shadowGO.GetComponent<MeshRenderer>();
        if (_shadowTexture == null) _shadowTexture = CreateSoftDiscTexture(64, shadowColor);
        _iconShadowRenderer.material.mainTexture = _shadowTexture;
        _iconShadowRenderer.material.color = Color.white;

        var iconGO = CreateTextureQuad("Icon", new Vector3(0f, 0f, -bgPopHeight), iconSize, iconSize);
        _iconRenderer = iconGO.GetComponent<MeshRenderer>();

        var textGO = new GameObject("Name");
        textGO.layer = gameObject.layer;
        textGO.transform.SetParent(_root.transform, false);
        textGO.transform.localRotation = Quaternion.Euler(textRotX, textRotY, textRotZ);

        _nameText = textGO.AddComponent<TextMeshPro>();
        if (fontAsset != null) _nameText.font = fontAsset;
        _nameText.fontSize = fontSize;
        _nameText.color = Color.white;
        _nameText.fontStyle = FontStyles.Bold;
        _nameText.alignment = TextAlignmentOptions.MidlineLeft;
        _nameText.overflowMode = TextOverflowModes.Overflow;
        _nameText.textWrappingMode = TextWrappingModes.NoWrap;

        _nameText.outlineWidth = 0.2f;
        _nameText.outlineColor = new Color(0f, 0f, 0f, 1f);

        var mat = _nameText.fontMaterial;
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.95f));
        mat.SetFloat("_UnderlayOffsetX", 0f);
        mat.SetFloat("_UnderlayOffsetY", 0f);
        mat.SetFloat("_UnderlayDilate", 0.55f);
        mat.SetFloat("_UnderlaySoftness", 0.2f);
    }


    private void RepositionElements(bool hasIcon)
    {
        if (_nameText == null) return;

        float clampedIconSize = Mathf.Min(iconSize, maxWidth * 0.4f);
        float iconSlot = hasIcon ? (clampedIconSize + iconTextGap) : 0f;

        float maxTextW = Mathf.Max(maxWidth - iconSlot - 0.1f, 0.1f);

        var rect = _nameText.GetComponent<RectTransform>();
        if (rect != null) rect.sizeDelta = new Vector2(maxTextW, bgHeight);

        _nameText.ForceMeshUpdate();

        float textW = Mathf.Min(_nameText.preferredWidth, maxTextW);
        float totalW = Mathf.Min(iconSlot + textW, maxWidth);

        float left = -totalW * 0.5f;
        float iconX = left + clampedIconSize * 0.5f;

        float clampedBgSize = clampedIconSize * bgScale;

        if (_iconBgRenderer != null)
        {
            _iconBgRenderer.transform.localScale = new Vector3(clampedBgSize, clampedBgSize, 1f);
            _iconBgRenderer.transform.localPosition = hasIcon
                ? new Vector3(iconX, 0f, 0f)
                : new Vector3(-9999f, 0f, 0f);
        }

        float clampedShadowSize = clampedIconSize * shadowScale;
        if (_iconShadowRenderer != null)
        {
            _iconShadowRenderer.transform.localScale = new Vector3(clampedShadowSize, clampedShadowSize, 1f);
            _iconShadowRenderer.transform.localPosition = hasIcon
                ? new Vector3(iconX + shadowOffset.x * clampedIconSize, shadowOffset.y * clampedIconSize, -bgPopHeight * 0.5f)
                : new Vector3(-9999f, 0f, 0f);
        }

        if (_iconRenderer != null)
        {
            _iconRenderer.transform.localScale = new Vector3(clampedIconSize, clampedIconSize, 1f);
            _iconRenderer.transform.localPosition = hasIcon
                ? new Vector3(iconX, iconLiftOffset * clampedIconSize, -bgPopHeight)
                : new Vector3(-9999f, 0f, 0f);
        }

        float textRectCenterX = left + iconSlot + maxTextW * 0.5f;
        _nameText.transform.localPosition = new Vector3(textRectCenterX, 0f, -0.002f);
        if (rect != null) rect.sizeDelta = new Vector2(maxTextW, bgHeight);
    }


    private GameObject CreateTextureQuad(string n, Vector3 pos, float w, float h)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = n;
        go.layer = gameObject.layer;

        Destroy(go.GetComponent<MeshCollider>());

        go.transform.SetParent(_root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(w, h, 1f);

        var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent"));
        mat.color = Color.white;

        mat.mainTextureScale = new Vector2(1f, -1f);
        mat.mainTextureOffset = new Vector2(0f, 1f);

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;

        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return go;
    }


    private Texture2D CreateBadgeTexture(int size, Color fill, Color border)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float half = size * 0.5f;
        float radius = half - 1.5f;
        float borderWidth = size * 0.032f;
        var center = new Vector2(half, half);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - center.x;
                float dy = y + 0.5f - center.y;
                float outerDist = Mathf.Sqrt(dx * dx + dy * dy) - radius;

                if (outerDist > 1.5f)
                {
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                    continue;
                }

                float innerDist = outerDist + borderWidth;
                float edgeAlpha = 1f - Mathf.Clamp01(outerDist / 1.5f);

                Color c = innerDist <= 0f ? fill : border;
                c.a *= edgeAlpha;
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return tex;
    }


    private Texture2D CreateSoftDiscTexture(int size, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float half = size * 0.5f;
        var center = new Vector2(half, half);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - center.x;
                float dy = y + 0.5f - center.y;
                float dist01 = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / half);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, dist01);

                Color c = color;
                c.a *= fade;
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return tex;
    }
}