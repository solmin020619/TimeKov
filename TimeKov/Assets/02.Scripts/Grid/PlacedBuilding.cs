using System.Collections.Generic;
using UnityEngine;

public class PlacedBuilding : MonoBehaviour
{
    // ���� �⺻ ������ ����������������������������������������������������������������������������������������������������������������
    [HideInInspector] public int facilityId;
    [HideInInspector] public int currentLevel = 1;
    [HideInInspector] public List<Vector2Int> occupiedCells = new List<Vector2Int>();
    [HideInInspector] public Renderer[] cachedRenderers;

    public Vector2Int originCell;

    // ���� ���̶���Ʈ ���� ���� ����������������������������������������������������������������������������������������������
    [Header("Highlight Colors")]
    public Color demolishColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Color railConnectingColor = new Color(1f, 1f, 1f, 1f);
    [Range(0f, 1f)]
    public float railBlendStrength = 0.35f; // ���� ���� ����� �󸶳� ������

    // ���� ���̶���Ʈ ���� (�뵵�� �и�) ��������������������������������������������������������������������������
    private bool _isDemolishHighlighted = false;
    private bool _isRailHighlighted = false;

    // ���� MaterialPropertyBlock ������������������������������������������������������������������������������������������
    // ��Ƽ���� ���� ���� �������� �ӽ� ���� ����
    private MaterialPropertyBlock _mpb;
    private static readonly int ColorPropID = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropID = Shader.PropertyToID("_BaseColor"); // URP

    // ���� �� ������
    private Color[] _originalColors;

    // ���� ���̺� ��������������������������������������������������������������������������������������������������������������������������
    private BuildingLabelUI _labelUI;

    // ��������������������������������������������������������������������������������������������������������������������������������������������
    // ������ ĳ��
    // ��������������������������������������������������������������������������������������������������������������������������������������������

    public void CacheRenderers()
    {
        var all = GetComponentsInChildren<Renderer>(true);

        // ���̶���Ʈ�� �ǹ� ���� ������ ����
        var filtered = new List<Renderer>(all.Length);
        foreach (var r in all)
        {
            if (r == null) continue;
            if (r is ParticleSystemRenderer) continue;
            if (r is TrailRenderer) continue;
            if (r is LineRenderer) continue;
            // _Color �Ǵ� _BaseColor ���� ��Ƽ���� ����
            if (!HasColorProperty(r)) continue;

            filtered.Add(r);
        }

        cachedRenderers = filtered.ToArray();
        _originalColors = new Color[cachedRenderers.Length];
        _mpb = new MaterialPropertyBlock();

        for (int i = 0; i < cachedRenderers.Length; i++)
            _originalColors[i] = GetRendererColor(cachedRenderers[i]);
    }

    // ��������������������������������������������������������������������������������������������������������������������������������������������
    // ���̶���Ʈ Public API
    // ��������������������������������������������������������������������������������������������������������������������������������������������

    /// <summary>ö�� ��� ���� / ����</summary>
    public void SetDemolishHighlight(bool on)
    {
        if (_isDemolishHighlighted == on) return;
        _isDemolishHighlighted = on;
        RefreshHighlight();
    }

    /// <summary>���� ���� �� ��� ƾƮ on / off</summary>
    public void SetRailConnectingHighlight(bool on)
    {
        if (_isRailHighlighted == on) return;
        _isRailHighlighted = on;
        RefreshHighlight();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 고스트 모드 (라우팅 중 빌딩 머티리얼을 hologram 으로 swap)
    // 끄면 원본 복원
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private bool _isGhostMode = false;
    private Material[][] _savedSharedMaterials;

    public void SetGhostMode(bool on, Material ghostMaterial = null)
    {
        if (_isGhostMode == on) return;

        if (cachedRenderers == null || cachedRenderers.Length == 0)
            CacheRenderers();

        if (on)
        {
            if (ghostMaterial == null) return;

            _savedSharedMaterials = new Material[cachedRenderers.Length][];

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] == null) continue;

                Material[] originals = cachedRenderers[i].sharedMaterials;
                _savedSharedMaterials[i] = originals;

                Material[] replaced = new Material[originals.Length];
                for (int j = 0; j < replaced.Length; j++)
                    replaced[j] = ghostMaterial;

                cachedRenderers[i].sharedMaterials = replaced;
            }

            _isGhostMode = true;
        }
        else
        {
            if (_savedSharedMaterials != null)
            {
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    if (cachedRenderers[i] == null) continue;
                    if (_savedSharedMaterials[i] == null) continue;

                    cachedRenderers[i].sharedMaterials = _savedSharedMaterials[i];
                }
                _savedSharedMaterials = null;
            }
            _isGhostMode = false;
        }
    }

    /// <summary>��� ���̶���Ʈ ��� ����</summary>
    public void ClearAllHighlights()
    {
        _isDemolishHighlighted = false;
        _isRailHighlighted = false;
        RefreshHighlight();
    }

    // ��������������������������������������������������������������������������������������������������������������������������������������������
    // ���̶���Ʈ ���� ����
    // ��������������������������������������������������������������������������������������������������������������������������������������������

    /// <summary>
    /// ���� Ȱ�� ���̶���Ʈ ���¸� ����ؼ� �������� �ݿ�.
    /// �켱����: ö�� > ���� > ������
    /// </summary>
    private void RefreshHighlight()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            CacheRenderers();

        if (_isDemolishHighlighted)
        {
            ApplyColorMPB(demolishColor, blendWithOriginal: false);
        }
        else if (_isRailHighlighted)
        {
            // ���� ���� ����� blendStrength ������ ���
            // ������ �Ͼ�� ���ư��� �ʰ� �����ϰ� ������� ȿ��
            ApplyBlendedMPB(railConnectingColor, railBlendStrength);
        }
        else
        {
            ClearMPB();
        }
    }

    /// <summary>�ܻ����� MPB ���� (ö�ſ�)</summary>
    private void ApplyColorMPB(Color color, bool blendWithOriginal)
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] == null) continue;

            cachedRenderers[i].GetPropertyBlock(_mpb);

            Color final = blendWithOriginal
                ? Color.Lerp(_originalColors[i], color, railBlendStrength)
                : color;

            SetColorOnMPB(_mpb, cachedRenderers[i], final);
            cachedRenderers[i].SetPropertyBlock(_mpb);
        }
    }

    /// <summary>���� �� + ��ǥ ���� strength�� Lerp�ؼ� MPB ���� (���� ƾƮ��)</summary>
    private void ApplyBlendedMPB(Color targetColor, float strength)
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] == null) continue;

            cachedRenderers[i].GetPropertyBlock(_mpb);

            Color blended = Color.Lerp(_originalColors[i], targetColor, strength);
            SetColorOnMPB(_mpb, cachedRenderers[i], blended);
            cachedRenderers[i].SetPropertyBlock(_mpb);
        }
    }

    /// <summary>MPB ����� ���� ��Ƽ���� �� ����</summary>
    private void ClearMPB()
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] == null) continue;
            // �� MPB�� ������ ��Ƽ���� �⺻������ ���ư�
            cachedRenderers[i].SetPropertyBlock(null);
        }
    }

    // ��������������������������������������������������������������������������������������������������������������������������������������������
    // ���Ž� ȣȯ (BuildManager�� ���� SetHighlight / RestoreColor ȣ�� ����)
    // ��������������������������������������������������������������������������������������������������������������������������������������������

    /// <summary>
    /// ���� BuildManager�� ö�� ���̶���Ʈ ȣ�� ȣȯ��.
    /// �� �ڵ�� SetDemolishHighlight(true)�� ������.
    /// </summary>
    public void SetHighlight(Color color)
    {
        if (color == demolishColor || color.r > 0.8f && color.g < 0.3f)
            SetDemolishHighlight(true);
        else
            SetRailConnectingHighlight(true);
    }

    /// <summary>
    /// ���� BuildManager�� ö�� ���� ȣ�� ȣȯ��.
    /// �� �ڵ�� SetDemolishHighlight(false)�� ������.
    /// </summary>
    public void RestoreColor() => ClearAllHighlights();

    // ��������������������������������������������������������������������������������������������������������������������������������������������
    // ���̺� Public API
    // ��������������������������������������������������������������������������������������������������������������������������������������������

    /// <summary>BuildManager�� PlaceCurrentFacilityRoutine���� ȣ���ϼ���.</summary>
    public void SetupLabel(string facilityName, int gridW = 5, int gridH = 5, float cellSize = 1f)
    {
        _labelUI = GetComponent<BuildingLabelUI>();
        if (_labelUI == null)
            _labelUI = gameObject.AddComponent<BuildingLabelUI>();

        Sprite icon = FacilityIconDatabase.Instance != null
            ? FacilityIconDatabase.Instance.GetIcon(facilityId)
            : null;

        // �ǹ� ũ�� ��� �ִ� �ʺ�
        float buildingW = gridW * cellSize;
        float buildingH = gridH * cellSize;
        _labelUI.maxWidth = buildingW;

        // ��Ʈ ���� (Awake���� ���� �ؾ� CreateLabel���� �����)
        if (FacilityIconDatabase.Instance != null && FacilityIconDatabase.Instance.labelFont != null)
            _labelUI.fontAsset = FacilityIconDatabase.Instance.labelFont;

        // �ǹ� ���߾�, ���̴� �˳��ϰ�
        _labelUI.labelOffset = new Vector3(0f, 10.0f, 0f);

        _labelUI.Setup(facilityId, facilityName, icon);
    }

    public void ShowLabel() => _labelUI?.ShowLabel();
    public void HideLabel() => _labelUI?.HideLabel();

    // ��������������������������������������������������������������������������������������������������������������������������������������������
    // ����
    // ��������������������������������������������������������������������������������������������������������������������������������������������

    private static bool HasColorProperty(Renderer r)
    {
        if (r.sharedMaterial == null) return false;
        return r.sharedMaterial.HasProperty(ColorPropID)
            || r.sharedMaterial.HasProperty(BaseColorPropID);
    }

    private static Color GetRendererColor(Renderer r)
    {
        if (r.sharedMaterial == null) return Color.white;
        if (r.sharedMaterial.HasProperty(BaseColorPropID))
            return r.sharedMaterial.GetColor(BaseColorPropID);
        if (r.sharedMaterial.HasProperty(ColorPropID))
            return r.sharedMaterial.GetColor(ColorPropID);
        return Color.white;
    }

    private static void SetColorOnMPB(MaterialPropertyBlock mpb, Renderer r, Color color)
    {
        // URP�� _BaseColor, Built-in�� _Color
        if (r.sharedMaterial.HasProperty(BaseColorPropID))
            mpb.SetColor(BaseColorPropID, color);
        else
            mpb.SetColor(ColorPropID, color);
    }
}