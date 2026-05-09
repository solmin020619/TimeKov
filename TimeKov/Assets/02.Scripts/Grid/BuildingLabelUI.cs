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
    public float iconSize = 1.5f;   // ������ ũ��

    public float iconTextGap = 0.1f; // �����ܰ� �ؽ�Ʈ ���� ����

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
    private TextMeshPro _nameText;  // �̸� �ؽ�Ʈ
    private MeshRenderer _iconRenderer; // ������ (Quad ������)

  

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

        // ������ + �ؽ�Ʈ ��ġ ���ġ (�ٽ�)
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

        // ���� ������ ���� (Quad) ����
        var iconGO = CreateTextureQuad("Icon", Vector3.zero, iconSize, iconSize);
        _iconRenderer = iconGO.GetComponent<MeshRenderer>();

        // ���� �ؽ�Ʈ ���� ����
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
        _nameText.overflowMode = TextOverflowModes.Ellipsis;
        _nameText.textWrappingMode = TextWrappingModes.NoWrap;

        // �ܰ���
        _nameText.outlineWidth = 0.25f;
        _nameText.outlineColor = new Color(0f, 0f, 0f, 0.9f);
    }

    // ������������������������������������������������������������������������������������������
    //  ���̾ƿ� �ٽ� ���� (������ + �ؽ�Ʈ ����)
    // ������������������������������������������������������������������������������������������

    private void RepositionElements(bool hasIcon)
    {
        if (_nameText == null) return;

        // �������� ������ ���� Ȯ��
        float iconSlot = hasIcon ? (iconSize + iconTextGap) : 0f;

        // �������� �ʹ� ũ�� ����
        float clampedIconSize = Mathf.Min(iconSize, maxWidth * 0.4f);
        float clampedIconSlot = hasIcon ? (clampedIconSize + iconTextGap) : 0f;

        // �ؽ�Ʈ �ִ� �ʺ�
        float maxTextW = Mathf.Max(maxWidth - clampedIconSlot - 0.1f, 0.1f);

        var rect = _nameText.GetComponent<RectTransform>();
        if (rect != null) rect.sizeDelta = new Vector2(maxTextW, bgHeight);

        _nameText.ForceMeshUpdate();

        float textW = Mathf.Min(_nameText.preferredWidth, maxTextW);
        float totalW = Mathf.Min(clampedIconSlot + textW, maxWidth);

        //  �߽� �������� �¿� ��ġ
        float right = totalW * 0.5f;

        // ���� ������ ��ġ ����
        if (_iconRenderer != null)
        {
            _iconRenderer.transform.localScale = new Vector3(clampedIconSize, clampedIconSize, 1f);
            _iconRenderer.transform.localPosition = hasIcon
                ? new Vector3(right - clampedIconSize * 0.5f, 0f, -0.001f)
                : new Vector3(-9999f, 0f, 0f); // ������ ������ ȭ�� ��
        }

        // ���� �ؽ�Ʈ ��ġ ����
        if (_nameText != null)
        {
            float textX = right - clampedIconSlot - textW * 0.5f + 0.5f;

            _nameText.transform.localPosition =
                new Vector3(textX - textW * 0.5f, 0f, -0.002f);

            if (rect != null) rect.sizeDelta = new Vector2(maxTextW, bgHeight);
        }
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
}