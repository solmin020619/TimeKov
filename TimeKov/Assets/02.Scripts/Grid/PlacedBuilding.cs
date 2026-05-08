using System.Collections.Generic;
using UnityEngine;

public class PlacedBuilding : MonoBehaviour
{
    [HideInInspector] public int facilityId;
    [HideInInspector] public int currentLevel = 1;
    [HideInInspector] public List<Vector2Int> occupiedCells = new List<Vector2Int>();
    [HideInInspector] public Renderer[] cachedRenderers;

    public Vector2Int originCell;

    [Header("Highlight Colors")]
    public Color demolishColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Color railConnectingColor = new Color(1f, 1f, 1f, 1f);
    [Range(0f, 1f)]
    public float railBlendStrength = 0.35f;

    private bool _isDemolishHighlighted = false;
    private bool _isRailHighlighted = false;

    private MaterialPropertyBlock _mpb;
    private static readonly int ColorPropID = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropID = Shader.PropertyToID("_BaseColor");

    private Color[] _originalColors;

    private BuildingLabelUI _labelUI;

    public void CacheRenderers()
    {
        var all = GetComponentsInChildren<Renderer>(true);

        var filtered = new List<Renderer>(all.Length);
        foreach (var r in all)
        {
            if (r == null) continue;
            if (r is ParticleSystemRenderer) continue;
            if (r is TrailRenderer) continue;
            if (r is LineRenderer) continue;
            if (!HasColorProperty(r)) continue;

            filtered.Add(r);
        }

        cachedRenderers = filtered.ToArray();
        _originalColors = new Color[cachedRenderers.Length];
        _mpb = new MaterialPropertyBlock();

        for (int i = 0; i < cachedRenderers.Length; i++)
            _originalColors[i] = GetRendererColor(cachedRenderers[i]);
    }

    public void SetDemolishHighlight(bool on)
    {
        if (_isDemolishHighlighted == on) return;
        _isDemolishHighlighted = on;
        RefreshHighlight();
    }

    public void SetRailConnectingHighlight(bool on)
    {
        if (_isRailHighlighted == on) return;
        _isRailHighlighted = on;
        RefreshHighlight();
    }

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

    public void ClearAllHighlights()
    {
        _isDemolishHighlighted = false;
        _isRailHighlighted = false;
        RefreshHighlight();
    }

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
            ApplyBlendedMPB(railConnectingColor, railBlendStrength);
        }
        else
        {
            ClearMPB();
        }
    }

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

    private void ClearMPB()
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] == null) continue;
            cachedRenderers[i].SetPropertyBlock(null);
        }
    }

    public void SetHighlight(Color color)
    {
        if (color == demolishColor || color.r > 0.8f && color.g < 0.3f)
            SetDemolishHighlight(true);
        else
            SetRailConnectingHighlight(true);
    }

    public void RestoreColor() => ClearAllHighlights();

    public void SetupLabel(string facilityName, int gridW = 5, int gridH = 5, float cellSize = 1f)
    {
        _labelUI = GetComponent<BuildingLabelUI>();
        if (_labelUI == null)
            _labelUI = gameObject.AddComponent<BuildingLabelUI>();

        Sprite icon = FacilityIconDatabase.Instance != null
            ? FacilityIconDatabase.Instance.GetIcon(facilityId)
            : null;

        float buildingW = gridW * cellSize;
        float buildingH = gridH * cellSize;
        _labelUI.maxWidth = buildingW;

        if (FacilityIconDatabase.Instance != null && FacilityIconDatabase.Instance.labelFont != null)
            _labelUI.fontAsset = FacilityIconDatabase.Instance.labelFont;

        _labelUI.labelOffset = new Vector3(0f, 10.0f, 0f);

        _labelUI.Setup(facilityId, facilityName, icon);
    }

    public void ShowLabel() => _labelUI?.ShowLabel();
    public void HideLabel() => _labelUI?.HideLabel();

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
        if (r.sharedMaterial.HasProperty(BaseColorPropID))
            mpb.SetColor(BaseColorPropID, color);
        else
            mpb.SetColor(ColorPropID, color);
    }
}
