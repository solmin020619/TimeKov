using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BuildGridOverlay : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private BuildManager buildManager;

    [Header("Grid Patch")]
    [SerializeField] private int patchRadius = 6;

    [SerializeField] private bool followCursorWhenNoPreview = false;

    [Header("Style")]
    [SerializeField] private Material dotMaterial;

    [SerializeField] private Material lineMaterial;

    [SerializeField, Range(0.01f, 0.5f)] private float dotSize = 0.08f;

    [SerializeField, Range(0f, 0.5f)] private float edgeGap = 0.1f;

    [Header("Visual")]
    [SerializeField] private float yOffset = 0.03f;
    [SerializeField] private bool showOnlyInBuildMode = true;
    [SerializeField] private bool showOnlyInTopViewMode = true;

    [SerializeField] private float fadeDuration = 0.1f;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh gridMesh;

    private float cachedCellSize = -1f;
    private int cachedPatchRadius = -1;
    private float cachedDotSize = -1f;
    private float cachedEdgeGap = -1f;

    private Material cachedDotMat;
    private Material cachedLineMat;

    private float currentAlpha = 0f;
    private MaterialPropertyBlock mpb;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mpb = new MaterialPropertyBlock();

        if (buildManager == null)
            buildManager = FindAnyObjectByType<BuildManager>();

        EnsureMaterialsAssigned();
    }

    private void LateUpdate()
    {
        if (buildManager == null)
            return;

        bool shouldShow = ResolveShouldShow(out Vector3 centerWorld);

        float target = shouldShow ? 1f : 0f;
        if (fadeDuration > 0f)
            currentAlpha = Mathf.MoveTowards(currentAlpha, target, Time.deltaTime / fadeDuration);
        else
            currentAlpha = target;

        bool rendererOn = currentAlpha > 0.001f;
        if (meshRenderer.enabled != rendererOn)
            meshRenderer.enabled = rendererOn;

        if (!rendererOn)
            return;

        float cellSize = buildManager.cellSize;

        int effectiveRadius = patchRadius;

        if (!Mathf.Approximately(cachedCellSize, cellSize) ||
            cachedPatchRadius != effectiveRadius ||
            !Mathf.Approximately(cachedDotSize, dotSize) ||
            !Mathf.Approximately(cachedEdgeGap, edgeGap))
        {
            RebuildPatchMesh(cellSize, effectiveRadius);
            cachedCellSize = cellSize;
            cachedPatchRadius = effectiveRadius;
            cachedDotSize = dotSize;
            cachedEdgeGap = edgeGap;
        }

        if (cachedDotMat != dotMaterial || cachedLineMat != lineMaterial)
            EnsureMaterialsAssigned();

        Vector3 origin = buildManager.gridOrigin != null ? buildManager.gridOrigin.position : Vector3.zero;
        int centerCellX = Mathf.FloorToInt((centerWorld.x - origin.x) / cellSize);
        int centerCellZ = Mathf.FloorToInt((centerWorld.z - origin.z) / cellSize);

        Vector3 patchCornerWorld = new Vector3(
            origin.x + (centerCellX - effectiveRadius) * cellSize,
            buildManager.fixedY + yOffset,
            origin.z + (centerCellZ - effectiveRadius) * cellSize
        );
        transform.position = patchCornerWorld;
        transform.rotation = Quaternion.identity;

        ApplyAlpha();
    }

    private bool ResolveShouldShow(out Vector3 centerWorld)
    {
        centerWorld = Vector3.zero;

        if (showOnlyInBuildMode && !buildManager.IsBuildMode)
            return false;

        if (showOnlyInTopViewMode && !buildManager.IsTopViewMode)
            return false;

        if (buildManager.TryGetActivePreviewPosition(out centerWorld))
            return true;

        if (followCursorWhenNoPreview && TryGetCursorWorld(out centerWorld))
            return true;

        return false;
    }

    private bool TryGetCursorWorld(out Vector3 world)
    {
        world = Vector3.zero;

        Camera cam = buildManager.mainCam;
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, buildManager.rayDistance, buildManager.groundMask))
        {
            world = hit.point;
            return true;
        }

        return false;
    }

    private void EnsureMaterialsAssigned()
    {
        cachedDotMat = dotMaterial;
        cachedLineMat = lineMaterial;

        Material d = dotMaterial != null ? dotMaterial : lineMaterial;
        Material l = lineMaterial != null ? lineMaterial : dotMaterial;

        if (d == null && l == null)
        {
            d = l = meshRenderer.sharedMaterial;
        }

        meshRenderer.sharedMaterials = new Material[] { d, l };
    }

    private void RebuildPatchMesh(float cellSize, int radius)
    {
        if (gridMesh != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(gridMesh);
#else
            Destroy(gridMesh);
#endif
        }

        int side = Mathf.Max(1, radius) * 2;
        int n = side + 1;
        float dot = Mathf.Max(0.001f, dotSize);
        float half = dot * 0.5f;
        float gap = Mathf.Clamp(edgeGap, 0f, cellSize * 0.5f - 0.001f);

        int dotQuadCount = n * n;
        int dotVertCount = dotQuadCount * 4;
        int dotIndexCount = dotQuadCount * 6;
        Vector3[] dotVerts = new Vector3[dotVertCount];
        int[] dotIndices = new int[dotIndexCount];

        int dv = 0, di = 0;
        for (int ix = 0; ix < n; ix++)
        {
            for (int iz = 0; iz < n; iz++)
            {
                float cx = ix * cellSize;
                float cz = iz * cellSize;

                int baseIdx = dv;
                dotVerts[dv++] = new Vector3(cx - half, 0f, cz - half);
                dotVerts[dv++] = new Vector3(cx + half, 0f, cz - half);
                dotVerts[dv++] = new Vector3(cx + half, 0f, cz + half);
                dotVerts[dv++] = new Vector3(cx - half, 0f, cz + half);

                dotIndices[di++] = baseIdx + 0;
                dotIndices[di++] = baseIdx + 2;
                dotIndices[di++] = baseIdx + 1;
                dotIndices[di++] = baseIdx + 0;
                dotIndices[di++] = baseIdx + 3;
                dotIndices[di++] = baseIdx + 2;
            }
        }

        int lineSegCount = n * side * 2;
        int lineVertCount = lineSegCount * 2;
        Vector3[] lineVerts = new Vector3[lineVertCount];
        int[] lineIndices = new int[lineVertCount];

        int lv = 0;
        for (int iz = 0; iz < n; iz++)
        {
            float zPos = iz * cellSize;
            for (int ix = 0; ix < side; ix++)
            {
                float xStart = ix * cellSize + gap;
                float xEnd = (ix + 1) * cellSize - gap;
                lineVerts[lv++] = new Vector3(xStart, 0f, zPos);
                lineVerts[lv++] = new Vector3(xEnd, 0f, zPos);
            }
        }
        for (int ix = 0; ix < n; ix++)
        {
            float xPos = ix * cellSize;
            for (int iz = 0; iz < side; iz++)
            {
                float zStart = iz * cellSize + gap;
                float zEnd = (iz + 1) * cellSize - gap;
                lineVerts[lv++] = new Vector3(xPos, 0f, zStart);
                lineVerts[lv++] = new Vector3(xPos, 0f, zEnd);
            }
        }
        for (int i = 0; i < lineVertCount; i++)
            lineIndices[i] = i;

        Vector3[] allVerts = new Vector3[dotVertCount + lineVertCount];
        Array.Copy(dotVerts, 0, allVerts, 0, dotVertCount);
        Array.Copy(lineVerts, 0, allVerts, dotVertCount, lineVertCount);

        int[] offsetLineIndices = new int[lineVertCount];
        for (int i = 0; i < lineVertCount; i++)
            offsetLineIndices[i] = lineIndices[i] + dotVertCount;

        gridMesh = new Mesh { name = "BuildGridOverlayPatch" };
        gridMesh.indexFormat = (allVerts.Length > 65000) ? IndexFormat.UInt32 : IndexFormat.UInt16;
        gridMesh.vertices = allVerts;
        gridMesh.subMeshCount = 2;
        gridMesh.SetIndices(dotIndices, MeshTopology.Triangles, 0);
        gridMesh.SetIndices(offsetLineIndices, MeshTopology.Lines, 1);
        gridMesh.RecalculateBounds();

        meshFilter.sharedMesh = gridMesh;
    }

    private void ApplyAlpha()
    {
        ApplyAlphaToSubmesh(0);
        ApplyAlphaToSubmesh(1);
    }

    private void ApplyAlphaToSubmesh(int idx)
    {
        Material[] mats = meshRenderer.sharedMaterials;
        if (mats == null || idx >= mats.Length || mats[idx] == null)
            return;

        meshRenderer.GetPropertyBlock(mpb, idx);

        Color baseColor = mats[idx].HasProperty("_Color")
            ? mats[idx].GetColor("_Color")
            : Color.white;

        baseColor.a *= currentAlpha;
        mpb.SetColor("_Color", baseColor);

        meshRenderer.SetPropertyBlock(mpb, idx);
    }

    [ContextMenu("Force Rebuild Patch")]
    public void ForceRebuild()
    {
        if (buildManager == null)
            return;

        RebuildPatchMesh(buildManager.cellSize, patchRadius);
        cachedCellSize = buildManager.cellSize;
        cachedPatchRadius = patchRadius;
        cachedDotSize = dotSize;
        cachedEdgeGap = edgeGap;
        EnsureMaterialsAssigned();
    }

    private void OnDestroy()
    {
        if (gridMesh != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(gridMesh);
#else
            Destroy(gridMesh);
#endif
        }
    }
}