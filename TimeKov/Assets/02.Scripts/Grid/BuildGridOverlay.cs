using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BuildGridOverlay : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private BuildManager buildManager;
    [SerializeField] private Transform originTransform;

    [Header("Grid Size")]
    [SerializeField] private int width = 40;
    [SerializeField] private int height = 40;

    [Header("Visual")]
    [SerializeField] private float yOffset = 0.03f;
    [SerializeField] private bool showOnlyInBuildMode = true;
    [SerializeField] private bool showOnlyInTopViewMode = true;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh gridMesh;

    private float lastCellSize = -1f;
    private int lastWidth = -1;
    private int lastHeight = -1;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (buildManager == null)
            buildManager = FindObjectOfType<BuildManager>();

        RebuildGrid();
    }

    private void LateUpdate()
    {
        UpdateVisibility();
        UpdatePosition();

        if (buildManager == null)
            return;

        if (!Mathf.Approximately(lastCellSize, buildManager.cellSize) ||
            lastWidth != width ||
            lastHeight != height)
        {
            RebuildGrid();
        }
    }


    private void UpdateVisibility()
    {
        bool visible = true;

        if (buildManager != null)
        {
            if (showOnlyInBuildMode && !buildManager.IsBuildMode)
                visible = false;

            if (showOnlyInTopViewMode && !buildManager.IsTopViewMode)
                visible = false;
        }

        if (meshRenderer.enabled != visible)
            meshRenderer.enabled = visible;
    }

    private void UpdatePosition()
    {
        if (buildManager == null)
            return;

        Vector3 origin = originTransform != null ? originTransform.position : Vector3.zero;
        transform.position = new Vector3(origin.x, buildManager.fixedY + yOffset, origin.z);
        transform.rotation = Quaternion.identity;
    }

    [ContextMenu("Rebuild Grid")]
    public void RebuildGrid()
    {
        if (buildManager == null)
            return;

        float cellSize = buildManager.cellSize;

        lastCellSize = cellSize;
        lastWidth = width;
        lastHeight = height;

        if (gridMesh != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(gridMesh);
#else
            Destroy(gridMesh);
#endif
        }

        gridMesh = new Mesh();
        gridMesh.name = "BuildGridOverlayMesh";

        int verticalLineCount = width + 1;
        int horizontalLineCount = height + 1;
        int totalLineCount = verticalLineCount + horizontalLineCount;
        int totalVertexCount = totalLineCount * 2;

        Vector3[] vertices = new Vector3[totalVertexCount];
        int[] indices = new int[totalVertexCount];

        float totalWidth = width * cellSize;
        float totalHeight = height * cellSize;

        int v = 0;

        for (int x = 0; x <= width; x++)
        {
            float xPos = x * cellSize;
            vertices[v] = new Vector3(xPos, 0f, 0f);
            indices[v] = v;
            v++;

            vertices[v] = new Vector3(xPos, 0f, totalHeight);
            indices[v] = v;
            v++;
        }

        for (int z = 0; z <= height; z++)
        {
            float zPos = z * cellSize;
            vertices[v] = new Vector3(0f, 0f, zPos);
            indices[v] = v;
            v++;

            vertices[v] = new Vector3(totalWidth, 0f, zPos);
            indices[v] = v;
            v++;
        }

        gridMesh.vertices = vertices;
        gridMesh.SetIndices(indices, MeshTopology.Lines, 0);
        gridMesh.RecalculateBounds();

        meshFilter.sharedMesh = gridMesh;
    }
}