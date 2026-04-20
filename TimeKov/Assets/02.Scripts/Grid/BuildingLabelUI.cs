using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class BuildingLabelUI : MonoBehaviour
{


    public TMP_FontAsset fontAsset; // 텍스트 폰트

    //  레이블 전체 위치 (건물 기준 위치)
    public Vector3 labelOffset = new Vector3(0f, 10.0f, 10.0f);
    //  "아이콘 +이름" 전체를 위로 띄우는 위치
    //  이거 바꾸면 레이블 위치가 움직임

    public float fontSize = 5.2f;   // 글자 크기
    public float iconSize = 1.5f;   // 아이콘 크기

    public float iconTextGap = 0.1f; // 아이콘과 텍스트 사이 간격

    //  텍스트만 따로 회전시키고 싶을 때 사용
    public float textRotX = 0f;
    public float textRotY = 0f;
    public float textRotZ = 0f;

    // 내부 레이아웃 계산용 (Inspector에서는 안 보임)
    [HideInInspector] public float maxWidth = 99f;   // 최대 가로 길이 제한
    [HideInInspector] public float bgHeight = 1.9f;  // 텍스트 영역 높이

    // ─────────────────────────────────────────────
    //  런타임에서 생성되는 오브젝트들
    // ─────────────────────────────────────────────

    private GameObject _root;        // 레이블 전체 부모
    private TextMeshPro _nameText;  // 이름 텍스트
    private MeshRenderer _iconRenderer; // 아이콘 (Quad 렌더러)

  

    public void Setup(int facilityId, string facilityName, Sprite icon)
    {
        if (_root == null) CreateLabel(); // 최초 1회 생성

        // ── 텍스트 설정 ──
        if (_nameText != null)
        {
            if (fontAsset != null) _nameText.font = fontAsset;
            _nameText.text = facilityName;
            _nameText.ForceMeshUpdate(); // 크기 계산 위해 강제 업데이트
        }

        // ── 아이콘 설정 ──
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

        // 아이콘 + 텍스트 위치 재배치 (핵심)
        RepositionElements(hasIcon);
    }

    public void ShowLabel() { if (_root != null) _root.SetActive(true); }
    public void HideLabel() { if (_root != null) _root.SetActive(false); }

    private void Awake() { }

    private void LateUpdate()
    {
        //  항상 바닥에 눕혀놓기 (탑뷰 고정)
        if (_root != null)
            _root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // 텍스트 개별 회전 (필요할 때만)
        if (_nameText != null)
            _nameText.transform.localRotation = Quaternion.Euler(textRotX, textRotY, textRotZ);
    }

    // ─────────────────────────────────────────────
    // 실제 UI 생성
    // ─────────────────────────────────────────────

    private void CreateLabel()
    {
        _root = new GameObject("_BuildingLabel");
        _root.transform.SetParent(transform, false);
        _root.transform.localPosition = labelOffset; //  위치 적용
        _root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _root.layer = gameObject.layer;

        // ── 아이콘 생성 (Quad) ──
        var iconGO = CreateTextureQuad("Icon", Vector3.zero, iconSize, iconSize);
        _iconRenderer = iconGO.GetComponent<MeshRenderer>();

        // ── 텍스트 생성 ──
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
        _nameText.enableWordWrapping = false;

        // 외곽선
        _nameText.outlineWidth = 0.25f;
        _nameText.outlineColor = new Color(0f, 0f, 0f, 0.9f);
    }

    // ─────────────────────────────────────────────
    //  레이아웃 핵심 로직 (아이콘 + 텍스트 정렬)
    // ─────────────────────────────────────────────

    private void RepositionElements(bool hasIcon)
    {
        if (_nameText == null) return;

        // 아이콘이 있으면 공간 확보
        float iconSlot = hasIcon ? (iconSize + iconTextGap) : 0f;

        // 아이콘이 너무 크면 제한
        float clampedIconSize = Mathf.Min(iconSize, maxWidth * 0.4f);
        float clampedIconSlot = hasIcon ? (clampedIconSize + iconTextGap) : 0f;

        // 텍스트 최대 너비
        float maxTextW = Mathf.Max(maxWidth - clampedIconSlot - 0.1f, 0.1f);

        var rect = _nameText.GetComponent<RectTransform>();
        if (rect != null) rect.sizeDelta = new Vector2(maxTextW, bgHeight);

        _nameText.ForceMeshUpdate();

        float textW = Mathf.Min(_nameText.preferredWidth, maxTextW);
        float totalW = Mathf.Min(clampedIconSlot + textW, maxWidth);

        //  중심 기준으로 좌우 배치
        float right = totalW * 0.5f;

        // ── 아이콘 위치 ──
        if (_iconRenderer != null)
        {
            _iconRenderer.transform.localScale = new Vector3(clampedIconSize, clampedIconSize, 1f);
            _iconRenderer.transform.localPosition = hasIcon
                ? new Vector3(right - clampedIconSize * 0.5f, 0f, -0.001f)
                : new Vector3(-9999f, 0f, 0f); // 아이콘 없으면 화면 밖
        }

        // ── 텍스트 위치 ──
        if (_nameText != null)
        {
            float textX = right - clampedIconSlot - textW * 0.5f + 0.5f;

            _nameText.transform.localPosition =
                new Vector3(textX - textW * 0.5f, 0f, -0.002f);

            if (rect != null) rect.sizeDelta = new Vector2(maxTextW, bgHeight);
        }
    }

    // ─────────────────────────────────────────────
    //  아이콘용 Quad 생성
    // ─────────────────────────────────────────────

    private GameObject CreateTextureQuad(string n, Vector3 pos, float w, float h)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = n;
        go.layer = gameObject.layer;

        Destroy(go.GetComponent<MeshCollider>());

        go.transform.SetParent(_root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(w, h, 1f);

        //  스프라이트 표시용 머티리얼
        var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent"));
        mat.color = Color.white;

        // 텍스처 뒤집기 (Sprite UV 보정)
        mat.mainTextureScale = new Vector2(1f, -1f);
        mat.mainTextureOffset = new Vector2(0f, 1f);

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;

        // 그림자 제거
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return go;
    }
}