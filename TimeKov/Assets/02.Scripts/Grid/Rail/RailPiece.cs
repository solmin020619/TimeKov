using UnityEngine;

public class RailPiece : MonoBehaviour
{
    public Vector2Int cell;
    public bool up;
    public bool down;
    public bool left;
    public bool right;

    [HideInInspector] public Vector2Int flowFrom;

    private GameObject currentVisual;

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

        if (useStraight)
        {
            currentVisual = Instantiate(straightPrefab, transform);
            currentVisual.transform.localPosition = Vector3.zero;

            float yRot = 0f;
            if (left || right) yRot = 90f;
            else if (up || down) yRot = 0f;

            appliedYRot = yRot + straightRotationOffsetY;
            currentVisual.transform.localRotation = Quaternion.Euler(0f, appliedYRot, 0f);
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
            currentVisual.transform.localRotation = Quaternion.Euler(0f, appliedYRot, 0f);
        }

        ApplyShaderFlowDirection(appliedYRot);
    }

    private void ApplyShaderFlowDirection(float appliedYRot)
    {
        if (currentVisual == null || flowFrom == Vector2Int.zero)
            return;

        bool needsFlip;

        int count = 0;
        if (up) count++;
        if (down) count++;
        if (left) count++;
        if (right) count++;
        bool isCorner = count == 2 && !((up && down) || (left && right));

        if (isCorner)
        {
            Vector2Int naturalEntry;
            if (up && right) naturalEntry = Vector2Int.up;
            else if (right && down) naturalEntry = Vector2Int.right;
            else if (down && left) naturalEntry = Vector2Int.down;
            else naturalEntry = Vector2Int.left;

            needsFlip = (flowFrom != naturalEntry);
        }
        else
        {
            Vector2Int meshForwardGrid = YRotToGridDir(appliedYRot);

            // 수정된 핵심 로직
            needsFlip = (meshForwardGrid == -flowFrom);
        }

        float flowDir = needsFlip ? -1f : 1f;

        Renderer[] renderers = currentVisual.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            Material[] mats = r.materials;
            foreach (Material m in mats)
                m.SetFloat("_FlowDir", flowDir);
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