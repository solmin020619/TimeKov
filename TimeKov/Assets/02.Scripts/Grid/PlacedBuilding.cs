using System.Collections.Generic;
using UnityEngine;

public class PlacedBuilding : MonoBehaviour
{
    [HideInInspector] public int facilityId;
    [HideInInspector] public int currentLevel = 1;
    [HideInInspector] public List<Vector2Int> occupiedCells = new List<Vector2Int>();
    [HideInInspector] public Renderer[] cachedRenderers;

    private Color[] originalColors;

    public void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        originalColors = new Color[cachedRenderers.Length];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null && cachedRenderers[i].material.HasProperty("_Color"))
                originalColors[i] = cachedRenderers[i].material.color;
        }
    }

    public void SetHighlight(Color color)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            CacheRenderers();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null && cachedRenderers[i].material.HasProperty("_Color"))
                cachedRenderers[i].material.color = color;
        }
    }

    public void RestoreColor()
    {
        if (cachedRenderers == null || originalColors == null)
            return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null && cachedRenderers[i].material.HasProperty("_Color"))
                cachedRenderers[i].material.color = originalColors[i];
        }
    }
}