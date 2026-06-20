using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class BuildingLabelUI : MonoBehaviour
{


    public TMP_FontAsset fontAsset; // �ؽ�Ʈ ��Ʈ

    //  ���̺� ��ü ��ġ (�ǹ� ���� ��ġ)
    public Vector3 labelOffset = new Vector3(0f, 10.0f, 10.0f);
    //  "������ +�̸�" ��ü�� ���� ���� ��ġ
    //  �̰� �ٲٸ� ���̺� ��ġ�� ������

    public float fontSize = 5.2f;   // ���� ũ��
    public float iconSize = 1.3f;   // ������ ũ��

    public float iconTextGap = 0.1f; // �����ܰ� �ؽ�Ʈ ���� ����

    [Header("������ ��� BG (���̳��)")]
    public float bgScale = 0.92f;        // ������ ��� BG ũ�� (������ ��� ���) - ������ �Ŝ�ٿ��� ��¦ �튾������ ����
    public float bgPopHeight = 0.01f;    // ������ ��� ī���忡 ��� ��Ÿ�ϴ� ����(z) ����
    public Color bgFillColor = new Color(0.05f, 0.06f, 0.08f, 0.6f);
    public Color bgBorderColor = new Color(0.25f, 0.55f, 0.95f, 0.55f);

    [Header("������ ��Ǽ� (�Ƣ�ٿ� ȿ��)")]
    public float iconLiftOffset = 0.12f;     // ������ ��ó�� ��(localY)�� ��¦ ��Ⱦ (��¦�θ� ��Ÿ�ϵ���)
    public float shadowScale = 1.0f;         // ������ ��� ��� ũ�� (������ ��� ���)
    public Vector2 shadowOffset = new Vector2(0f, -0.16f); // ��� ������ ��(������ ��� ���) - ��Ⱦ�θ� ��¦ ��Ⱦ ä ��¦�θ� ��Ÿ�ϵ���
    public Color shadowColor = new Color(0f, 0f, 0f, 0.55f);

    //  �ؽ�Ʈ�� ���� ȸ����Ű�� ���� �� ���
    public float textRotX = 0f;
    public float textRotY = 0f;
    public float textRotZ = 0f;

    // ���� ���̾ƿ� ���� (Inspector������ �� ����)
    [HideInInspector] public float maxWidth = 99f;   // �ִ� ���� ���� ����
    [HideInInspector] public float bgHeight = 1.9f;  // �ؽ�Ʈ ���� ����

    // ������������������������������������������������������������������������������������������
    //  ��Ÿ�ӿ��� �����Ǵ� ������Ʈ��
    // ������������������������������������������������������������������������������������������

    private GameObject _root;        // ���̺� ��ü �θ�
    private TextMeshPro _nameText;   // �̸� �ؽ�Ʈ
    private MeshRenderer _iconRenderer; // ������ (Quad ������)
    private MeshRenderer _iconBgRenderer; // ������ ��� BG (Quad ������)
    private MeshRenderer _iconShadowRenderer; // ������ �Ƣ�ٿ� ��Ǽ� (Quad ������)
    private Texture2D _bgTexture;
    private Texture2D _shadowTexture;

  

    public void Setup(int facilityId, string facilityName, Sprite icon)
    {
        if (_root == null) CreateLabel(); // ���� 1ȸ ����

        // ���� �ؽ�Ʈ ���� ����
        if (_nameText != null)
        {
            if (fontAsset != null) _nameText.font = fontAsset;
            _nameText.text = facilityName;
            _nameText.ForceMeshUpdate(); // ũ�� ��� ���� ���� ������Ʈ
        }

        // ���� ������ ���� ����
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

        // ������ ��ġ ���ġ (���ѵ� �߽ɿ�)
        RepositionElements(hasIcon);
    }

    public void ShowLabel() { if (_root != null) _root.SetActive(true); }
    public void HideLabel() { if (_root != null) _root.SetActive(false); }

    private void Awake() { }

    private void LateUpdate()
    {
        //  �׻� �ٴڿ� �������� (ž�� ����)
        if (_root != null)
            _root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // �ؽ�Ʈ ���� ȸ�� (�ʿ��� ����)
        if (_nameText != null)
            _nameText.transform.localRotation = Quaternion.Euler(textRotX, textRotY, textRotZ);
    }

    // ������������������������������������������������������������������������������������������
    // ���� UI ����
    // ������������������������������������������������������������������������������������������

    private void CreateLabel()
    {
        _root = new GameObject("_BuildingLabel");
        _root.transform.SetParent(transform, false);
        _root.transform.localPosition = labelOffset; //  ��ġ ����
        _root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _root.layer = gameObject.layer;

        // ������ ��� BG (���̳�) - ������ ��ܿ� ��Ƽ� ������ ä��
        var bgGO = CreateTextureQuad("IconBG", Vector3.zero, iconSize * bgScale, iconSize * bgScale);
        _iconBgRenderer = bgGO.GetComponent<MeshRenderer>();
        if (_bgTexture == null) _bgTexture = CreateBadgeTexture(64, bgFillColor, bgBorderColor);
        _iconBgRenderer.material.mainTexture = _bgTexture;
        _iconBgRenderer.material.color = Color.white;

        // ������ �Ƣ�ٿ� ��Ǽ� - BG�� ������ ���̿� ��Ŀ, ��¥ "�떠 ����" �������� ��¦ ��ǵ�
        var shadowGO = CreateTextureQuad("IconShadow", new Vector3(0f, 0f, -bgPopHeight * 0.5f), iconSize * shadowScale, iconSize * shadowScale);
        _iconShadowRenderer = shadowGO.GetComponent<MeshRenderer>();
        if (_shadowTexture == null) _shadowTexture = CreateSoftDiscTexture(64, shadowColor);
        _iconShadowRenderer.material.mainTexture = _shadowTexture;
        _iconShadowRenderer.material.color = Color.white;

        // ���� ������ ���� (Quad) ���� - BG/��Ǽ�ó�� ��� ��Ÿ�ϵ��� ��(z) ��ġ
        var iconGO = CreateTextureQuad("Icon", new Vector3(0f, 0f, -bgPopHeight), iconSize, iconSize);
        _iconRenderer = iconGO.GetComponent<MeshRenderer>();

        // ���� �ؽ�Ʈ ���� ���� - BG ���� ���̳�(����/��Ǻ��)�� ��ó�� ��ü �������� ��ε崵 ��Ÿ�ϵ��� ��
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
        _nameText.overflowMode = TextOverflowModes.Overflow; // ��¦ ��Ⱦ�ǵ� "..."�� ª�ư���� ���ϰ� ��ü �ؽ�Ʈ ǥ��
        _nameText.textWrappingMode = TextWrappingModes.NoWrap;

        // ��ü ��輱 - ��Ŀ�� ��ü ��ġ�ϵ��� ��¦ ��ü
        _nameText.outlineWidth = 0.2f;
        _nameText.outlineColor = new Color(0f, 0f, 0f, 1f);

        // ��ε崵(underlay) - ���� ��ó�� ä�� ��Ⱦ�ϰ� ��Ŀ�� ��ü(BG ǻ�(����) ��ü, ���ʸ� ��ó�ϰ� ��ü)
        var mat = _nameText.fontMaterial;
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.95f));
        mat.SetFloat("_UnderlayOffsetX", 0f);
        mat.SetFloat("_UnderlayOffsetY", 0f);
        mat.SetFloat("_UnderlayDilate", 0.55f);
        mat.SetFloat("_UnderlaySoftness", 0.2f);
    }

    // ������������������������������������������������������������������������������������������
    //  ���̾ƿ� �ٽ� ���� (������ + �ؽ�Ʈ ����)
    // ������������������������������������������������������������������������������������������

    private void RepositionElements(bool hasIcon)
    {
        if (_nameText == null) return;

        // �������� ������ ���� Ȯ�� (���ʿ� ������, �����ʿ� �ؽ�Ʈ)
        float clampedIconSize = Mathf.Min(iconSize, maxWidth * 0.4f);
        float iconSlot = hasIcon ? (clampedIconSize + iconTextGap) : 0f;

        // �ؽ�Ʈ �ִ� �ʺ�
        float maxTextW = Mathf.Max(maxWidth - iconSlot - 0.1f, 0.1f);

        var rect = _nameText.GetComponent<RectTransform>();
        if (rect != null) rect.sizeDelta = new Vector2(maxTextW, bgHeight);

        _nameText.ForceMeshUpdate();

        float textW = Mathf.Min(_nameText.preferredWidth, maxTextW);
        float totalW = Mathf.Min(iconSlot + textW, maxWidth);

        //  �߽� �������� ���� ��ġ (����: ������, ������: �ؽ�Ʈ)
        float left = -totalW * 0.5f;
        float iconX = left + clampedIconSize * 0.5f;

        float clampedBgSize = clampedIconSize * bgScale;

        // ������ ��� BG ��ġ ���� (��(z)�� ��)
        if (_iconBgRenderer != null)
        {
            _iconBgRenderer.transform.localScale = new Vector3(clampedBgSize, clampedBgSize, 1f);
            _iconBgRenderer.transform.localPosition = hasIcon
                ? new Vector3(iconX, 0f, 0f)
                : new Vector3(-9999f, 0f, 0f);
        }

        // ������ �Ƣ�ٿ� ��Ǽ� ��ġ ���� (��¦ ��ǵ� ä ��(z)�� BG/������ ���̿�)
        float clampedShadowSize = clampedIconSize * shadowScale;
        if (_iconShadowRenderer != null)
        {
            _iconShadowRenderer.transform.localScale = new Vector3(clampedShadowSize, clampedShadowSize, 1f);
            _iconShadowRenderer.transform.localPosition = hasIcon
                ? new Vector3(iconX + shadowOffset.x * clampedIconSize, shadowOffset.y * clampedIconSize, -bgPopHeight * 0.5f)
                : new Vector3(-9999f, 0f, 0f);
        }

        // ���� ������ ��ġ ���� (��(localY)�� ��¦ ��Ⱦ, BG/��Ǽ�ó�� ��� ��Ÿ�ϵ��� ��(z) ��ġ)
        if (_iconRenderer != null)
        {
            _iconRenderer.transform.localScale = new Vector3(clampedIconSize, clampedIconSize, 1f);
            _iconRenderer.transform.localPosition = hasIcon
                ? new Vector3(iconX, iconLiftOffset * clampedIconSize, -bgPopHeight)
                : new Vector3(-9999f, 0f, 0f); // ������ ������ ȭ�� ��
        }

        // ���� �ؽ�Ʈ ��ġ ���� (������ �ٿ� ��ġ, MidlineLeft ��ǿ̱������� ��ġ ��ġ�� rect �߽�)
        float textRectCenterX = left + iconSlot + maxTextW * 0.5f;
        _nameText.transform.localPosition = new Vector3(textRectCenterX, 0f, -0.002f);
        if (rect != null) rect.sizeDelta = new Vector2(maxTextW, bgHeight);
    }

    // ������������������������������������������������������������������������������������������
    //  �����ܿ� Quad ����
    // ������������������������������������������������������������������������������������������

    private GameObject CreateTextureQuad(string n, Vector3 pos, float w, float h)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = n;
        go.layer = gameObject.layer;

        Destroy(go.GetComponent<MeshCollider>());

        go.transform.SetParent(_root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(w, h, 1f);

        //  ��������Ʈ ǥ�ÿ� ��Ƽ����
        var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent"));
        mat.color = Color.white;

        // �ؽ�ó ������ (Sprite UV ����)
        mat.mainTextureScale = new Vector2(1f, -1f);
        mat.mainTextureOffset = new Vector2(0f, 1f);

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;

        // �׸��� ����
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return go;
    }

    // ������������������������������������������������������������������������������������������
    //  ������ ��� BG ��Ƽ� ������ ���� (����-�ٿ�� ��Ʈ + ��Ƽ ��ξ)
    // ������������������������������������������������������������������������������������������

    private Texture2D CreateBadgeTexture(int size, Color fill, Color border)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float half = size * 0.5f;
        float radius = half - 1.5f;
        float borderWidth = size * 0.032f; // ��Ƣ ��¦�� ��¦�� ��¦
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

    // ������������������������������������������������������������������������������������������
    //  ��Ǽ��� ��¦ �β��ϰ� ������(soft) ��(disc) ������ ����
    // ������������������������������������������������������������������������������������������

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