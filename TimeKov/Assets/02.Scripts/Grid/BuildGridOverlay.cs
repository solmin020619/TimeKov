using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 엔드필드 스타일 격자 오버레이.
/// - 꼭짓점: 작은 사각형(Triangles 서브메시)
/// - 엣지:   꼭짓점과 여백을 두고 떨어진 선(Lines 서브메시)
/// - 커서/프리뷰 주변 patchRadius 범위에만 표시 (시설/레일 모드 모두 지원)
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BuildGridOverlay : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private BuildManager buildManager;

    [Header("Grid Patch")]
    [Tooltip("커서를 중심으로 몇 셀 반경까지 격자를 그릴지 (예: 6이면 12x12 패치)")]
    [SerializeField] private int patchRadius = 6;

    [Tooltip("프리뷰가 없을 때도 커서 위치를 따라 표시할지")]
    [SerializeField] private bool followCursorWhenNoPreview = false;

    [Header("Style")]
    [Tooltip("꼭짓점용 머티리얼 (Unlit/Transparent 계열 추천)")]
    [SerializeField] private Material dotMaterial;

    [Tooltip("엣지 선용 머티리얼 (Unlit/Transparent 계열 추천)")]
    [SerializeField] private Material lineMaterial;

    [Tooltip("꼭짓점 점의 한 변 크기 (월드 단위)")]
    [SerializeField, Range(0.01f, 0.5f)] private float dotSize = 0.08f;

    [Tooltip("꼭짓점과 선 사이의 여백 (월드 단위). 0이면 선이 꼭짓점까지 이어짐")]
    [SerializeField, Range(0f, 0.5f)] private float edgeGap = 0.1f;

    [Header("Visual")]
    [SerializeField] private float yOffset = 0.03f;
    [SerializeField] private bool showOnlyInBuildMode = true;
    [SerializeField] private bool showOnlyInTopViewMode = true;

    [Tooltip("등장/소멸 페이드 시간 (머티리얼이 Transparent여야 효과 보임)")]
    [SerializeField] private float fadeDuration = 0.1f;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh gridMesh;

    // 메시 리빌드 판단용 캐시
    private float cachedCellSize = -1f;
    private int cachedPatchRadius = -1;
    private float cachedDotSize = -1f;
    private float cachedEdgeGap = -1f;

    // 머티리얼 캐시
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
            buildManager = FindObjectOfType<BuildManager>();

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
        if (buildManager.TryGetOverridePatchRadius(out int overrideRadius))
        {
            int margin = Mathf.Max(2, patchRadius / 2);
            effectiveRadius = Mathf.Max(patchRadius, overrideRadius + margin);
        }

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

        // 시설 or 레일 프리뷰 위치
        if (buildManager.TryGetActivePreviewPosition(out centerWorld))
            return true;

        // 프리뷰 없을 때 커서 따라가기 옵션
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

        // 둘 중 하나만 할당돼 있으면 같은 걸로 채워넣음 (편의)
        Material d = dotMaterial != null ? dotMaterial : lineMaterial;
        Material l = lineMaterial != null ? lineMaterial : dotMaterial;

        if (d == null && l == null)
        {
            // 아무것도 없으면 기존 sharedMaterial 재사용
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

        int side = Mathf.Max(1, radius) * 2;    // 한 변의 셀 개수
        int n = side + 1;                            // 꼭짓점 행/열 개수
        float dot = Mathf.Max(0.001f, dotSize);
        float half = dot * 0.5f;
        // gap이 셀 절반을 넘으면 선이 사라지므로 클램프
        float gap = Mathf.Clamp(edgeGap, 0f, cellSize * 0.5f - 0.001f);

        // --- 1) 꼭짓점 quads (Triangles 서브메시) ---
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

                // 위에서 내려다 봤을 때(+Y) 보이도록 CCW 감기
                dotIndices[di++] = baseIdx + 0;
                dotIndices[di++] = baseIdx + 2;
                dotIndices[di++] = baseIdx + 1;
                dotIndices[di++] = baseIdx + 0;
                dotIndices[di++] = baseIdx + 3;
                dotIndices[di++] = baseIdx + 2;
            }
        }

        // --- 2) 엣지 선 (Lines 서브메시) ---
        // 가로 선: n개의 z행 × side개의 x세그먼트
        // 세로 선: n개의 x열 × side개의 z세그먼트
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

        // --- 3) 단일 메시에 서브메시 2개로 합치기 ---
        Vector3[] allVerts = new Vector3[dotVertCount + lineVertCount];
        Array.Copy(dotVerts, 0, allVerts, 0, dotVertCount);
        Array.Copy(lineVerts, 0, allVerts, dotVertCount, lineVertCount);

        // 선 인덱스는 뒤쪽으로 오프셋
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
        // 서브메시 0(점), 1(선) 각각에 알파 적용
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