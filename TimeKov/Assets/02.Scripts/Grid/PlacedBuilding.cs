using System.Collections.Generic;
using UnityEngine;

public class PlacedBuilding : MonoBehaviour
{
    // ¦¡¦¡ ±âº» µ¥ÀÌÅÍ ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    [HideInInspector] public int facilityId;
    [HideInInspector] public int currentLevel = 1;
    [HideInInspector] public List<Vector2Int> occupiedCells = new List<Vector2Int>();
    [HideInInspector] public Renderer[] cachedRenderers;

    public Vector2Int originCell;

    // ¦¡¦¡ ÇÏÀÌ¶óÀÌÆ® »ö»ó ¼³Á¤ ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    [Header("Highlight Colors")]
    public Color demolishColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Color railConnectingColor = new Color(1f, 1f, 1f, 1f);
    [Range(0f, 1f)]
    public float railBlendStrength = 0.35f; // ¿ø·¡ »ö°ú Èò»öÀ» ¾ó¸¶³ª ¼¯À»Áö

    // ¦¡¦¡ ÇÏÀÌ¶óÀÌÆ® »óÅÂ (¿ëµµº° ºÐ¸®) ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    private bool _isDemolishHighlighted = false;
    private bool _isRailHighlighted = false;

    // ¦¡¦¡ MaterialPropertyBlock ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    // ¸ÓÆ¼¸®¾ó º¹Á¦ ¾øÀÌ ·»´õ·¯º° ÀÓ½Ã »ö»ó Àû¿ë
    private MaterialPropertyBlock _mpb;
    private static readonly int ColorPropID = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropID = Shader.PropertyToID("_BaseColor"); // URP

    // ¿ø·¡ »ö º¸Á¸¿ë
    private Color[] _originalColors;

    // ¦¡¦¡ ·¹ÀÌºí ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    private BuildingLabelUI _labelUI;

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    // ·»´õ·¯ Ä³½Ã
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    public void CacheRenderers()
    {
        var all = GetComponentsInChildren<Renderer>(true);

        // ÇÏÀÌ¶óÀÌÆ®¿¡ ÀÇ¹Ì ¾ø´Â ·»´õ·¯ Á¦¿Ü
        var filtered = new List<Renderer>(all.Length);
        foreach (var r in all)
        {
            if (r == null) continue;
            if (r is ParticleSystemRenderer) continue;
            if (r is TrailRenderer) continue;
            if (r is LineRenderer) continue;
            // _Color ¶Ç´Â _BaseColor ¾ø´Â ¸ÓÆ¼¸®¾óµµ Á¦¿Ü
            if (!HasColorProperty(r)) continue;

            filtered.Add(r);
        }

        cachedRenderers = filtered.ToArray();
        _originalColors = new Color[cachedRenderers.Length];
        _mpb = new MaterialPropertyBlock();

        for (int i = 0; i < cachedRenderers.Length; i++)
            _originalColors[i] = GetRendererColor(cachedRenderers[i]);
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    // ÇÏÀÌ¶óÀÌÆ® Public API
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    /// <summary>Ã¶°Å ¸ðµå ÁøÀÔ / ÇØÁ¦</summary>
    public void SetDemolishHighlight(bool on)
    {
        if (_isDemolishHighlighted == on) return;
        _isDemolishHighlighted = on;
        RefreshHighlight();
    }

    /// <summary>·¹ÀÏ ¿¬°á Áß Èò»ö Æ¾Æ® on / off</summary>
    public void SetRailConnectingHighlight(bool on)
    {
        if (_isRailHighlighted == on) return;
        _isRailHighlighted = on;
        RefreshHighlight();
    }

    /// <summary>¸ðµç ÇÏÀÌ¶óÀÌÆ® Áï½Ã ÇØÁ¦</summary>
    public void ClearAllHighlights()
    {
        _isDemolishHighlighted = false;
        _isRailHighlighted = false;
        RefreshHighlight();
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    // ÇÏÀÌ¶óÀÌÆ® ³»ºÎ ·ÎÁ÷
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    /// <summary>
    /// ÇöÀç È°¼º ÇÏÀÌ¶óÀÌÆ® »óÅÂ¸¦ °è»êÇØ¼­ ·»´õ·¯¿¡ ¹Ý¿µ.
    /// ¿ì¼±¼øÀ§: Ã¶°Å > ·¹ÀÏ > ¿ø·¡»ö
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
            // ¿ø·¡ »ö°ú Èò»öÀ» blendStrength ºñÀ²·Î ¼¯¾î¼­
            // ÅëÀ¸·Î ÇÏ¾é°Ô ³¯¾Æ°¡Áö ¾Ê°í ÀºÀºÇÏ°Ô ¹à¾ÆÁö´Â È¿°ú
            ApplyBlendedMPB(railConnectingColor, railBlendStrength);
        }
        else
        {
            ClearMPB();
        }
    }

    /// <summary>´Ü»öÀ¸·Î MPB Àû¿ë (Ã¶°Å¿ë)</summary>
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

    /// <summary>¿ø·¡ »ö + ¸ñÇ¥ »öÀ» strength·Î LerpÇØ¼­ MPB Àû¿ë (·¹ÀÏ Æ¾Æ®¿ë)</summary>
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

    /// <summary>MPB ºñ¿ö¼­ ¿ø·¡ ¸ÓÆ¼¸®¾ó »ö º¹¿ø</summary>
    private void ClearMPB()
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] == null) continue;
            // ºó MPB¸¦ ³ÖÀ¸¸é ¸ÓÆ¼¸®¾ó ±âº»°ªÀ¸·Î µ¹¾Æ°¨
            cachedRenderers[i].SetPropertyBlock(null);
        }
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    // ·¹°Å½Ã È£È¯ (BuildManagerÀÇ ±âÁ¸ SetHighlight / RestoreColor È£Ãâ À¯Áö)
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    /// <summary>
    /// ±âÁ¸ BuildManagerÀÇ Ã¶°Å ÇÏÀÌ¶óÀÌÆ® È£Ãâ È£È¯¿ë.
    /// »õ ÄÚµå´Â SetDemolishHighlight(true)¸¦ ¾²¼¼¿ä.
    /// </summary>
    public void SetHighlight(Color color)
    {
        if (color == demolishColor || color.r > 0.8f && color.g < 0.3f)
            SetDemolishHighlight(true);
        else
            SetRailConnectingHighlight(true);
    }

    /// <summary>
    /// ±âÁ¸ BuildManagerÀÇ Ã¶°Å ÇØÁ¦ È£Ãâ È£È¯¿ë.
    /// »õ ÄÚµå´Â SetDemolishHighlight(false)¸¦ ¾²¼¼¿ä.
    /// </summary>
    public void RestoreColor() => ClearAllHighlights();

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    // ·¹ÀÌºí Public API
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    /// <summary>BuildManagerÀÇ PlaceCurrentFacilityRoutine¿¡¼­ È£ÃâÇÏ¼¼¿ä.</summary>
    public void SetupLabel(string facilityName, int gridW = 5, int gridH = 5, float cellSize = 1f)
    {
        _labelUI = GetComponent<BuildingLabelUI>();
        if (_labelUI == null)
            _labelUI = gameObject.AddComponent<BuildingLabelUI>();

        Sprite icon = FacilityIconDatabase.Instance != null
            ? FacilityIconDatabase.Instance.GetIcon(facilityId)
            : null;

        // °Ç¹° Å©±â ±â¹Ý ÃÖ´ë ³Êºñ
        float buildingW = gridW * cellSize;
        float buildingH = gridH * cellSize;
        _labelUI.maxWidth = buildingW;

        // ÆùÆ® ¼¼ÆÃ (Awakeº¸´Ù ¸ÕÀú ÇØ¾ß CreateLabel¿¡¼­ Àû¿ëµÊ)
        if (FacilityIconDatabase.Instance != null && FacilityIconDatabase.Instance.labelFont != null)
            _labelUI.fontAsset = FacilityIconDatabase.Instance.labelFont;

        // °Ç¹° Á¤Áß¾Ó, ³ôÀÌ´Â ³Ë³ËÇÏ°Ô
        _labelUI.labelOffset = new Vector3(0f, 10.0f, 0f);

        _labelUI.Setup(facilityId, facilityName, icon);
    }

    public void ShowLabel() => _labelUI?.ShowLabel();
    public void HideLabel() => _labelUI?.HideLabel();

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    // ÇïÆÛ
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

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
        // URP´Â _BaseColor, Built-inÀº _Color
        if (r.sharedMaterial.HasProperty(BaseColorPropID))
            mpb.SetColor(BaseColorPropID, color);
        else
            mpb.SetColor(ColorPropID, color);
    }
}