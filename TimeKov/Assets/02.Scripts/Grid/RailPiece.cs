using UnityEngine;

public class RailPiece : MonoBehaviour
{
    public Vector2Int cell;
    public bool up;
    public bool down;
    public bool left;
    public bool right;

    [HideInInspector] public Vector2Int flowFrom;
    [HideInInspector] public int pathIndex;

    private GameObject currentVisual;

    private static readonly int PathOffsetId = Shader.PropertyToID("_PathOffset");

    [Header("Rotation Offset")]
    [SerializeField] private float straightRotationOffsetY = 0f;
    [SerializeField] private float cornerRotationOffsetY = 90f;

    public void ApplyVisual(GameObject straightPrefab, GameObject cornerPrefab)
    {
        if (currentVisual != null)
            Destroy(currentVisual);

        int count = 0;
        if (up) count++;
        if (down) count++;
        if (left) count++;
        if (right) count++;

        bool useStraight = count <= 1 || (up && down) || (left && right);

        float appliedYRot;
        Quaternion flipRotation = Quaternion.identity;
        Vector3 flipScale = Vector3.one;

        if (useStraight)
        {
            currentVisual = Instantiate(straightPrefab, transform);
            currentVisual.transform.localPosition = Vector3.zero;

            float yRot = 0f;
            if (left || right) yRot = 90f;
            else if (up || down) yRot = 0f;

            appliedYRot = yRot + straightRotationOffsetY;

            if (flowFrom != Vector2Int.zero)
            {
                Vector2Int meshForward = YRotToGridDir(appliedYRot);
                if (meshForward != flowFrom)
                    flipRotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }
        else
        {
            currentVisual = Instantiate(cornerPrefab, transform);
            currentVisual.transform.localPosition = Vector3.zero;

            float cornerYRot = 0f;
            if (up && right) cornerYRot = 0f;
            else if (right && down) cornerYRot = 90f;
            else if (down && left) cornerYRot = 180f;
            else if (left && up) cornerYRot = 270f;

            appliedYRot = cornerYRot + cornerRotationOffsetY;

            if (flowFrom != Vector2Int.zero)
            {
                Vector2Int naturalEntry = YRotToGridDir(appliedYRot);
                if (flowFrom != naturalEntry)
                {
                    flipRotation = Quaternion.Euler(0f, 90f, 0f);
                    flipScale = new Vector3(1f, 1f, -1f);
                }
            }
        }

        currentVisual.transform.localRotation = Quaternion.Euler(0f, appliedYRot, 0f) * flipRotation;
        currentVisual.transform.localScale = flipScale;

        ApplyPathOffset();
    }

    private void ApplyPathOffset()
    {
        if (currentVisual == null) return;

        Renderer[] renderers = currentVisual.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            Material[] mats = r.materials;
            foreach (Material m in mats)
            {
                if (m.HasProperty(PathOffsetId))
                    m.SetFloat(PathOffsetId, pathIndex);
            }
            r.materials = mats;
        }
    }

    private Vector2Int YRotToGridDir(float yRot)
    {
        float normalized = ((yRot % 360f) + 360f) % 360f;

        if (normalized < 45f || normalized >= 315f) return Vector2Int.up;
        if (normalized < 135f) return Vector2Int.right;
        if (normalized < 225f) return Vector2Int.down;
        return Vector2Int.left;
    }
}