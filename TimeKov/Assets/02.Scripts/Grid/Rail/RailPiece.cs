using UnityEngine;

public class RailPiece : MonoBehaviour
{
    public Vector2Int cell;
    public bool up;
    public bool down;
    public bool left;
    public bool right;

    // 흐름이 어느 그리드 방향에서 진입하는지 (RailBuildManager가 경로 완성 후 설정)
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

        // 이 회전에서 메시의 local +Z가 가리키는 그리드 방향
        Vector2Int meshForwardGrid = YRotToGridDir(appliedYRot);

        // meshForwardGrid가 flowFrom 방향을 향하고 있으면 역방향 → flip
        bool needsFlip = (meshForwardGrid == flowFrom);

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mpb.SetFloat("_FlowDir", needsFlip ? -1f : 1f);

        Renderer[] renderers = currentVisual.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
            r.SetPropertyBlock(mpb);

        Debug.Log($"[Rail] {cell} flowFrom={flowFrom} meshFwd={meshForwardGrid} flip={needsFlip}");
    }

    private Vector2Int YRotToGridDir(float yRot)
    {
        float normalized = ((yRot % 360f) + 360f) % 360f;
        if (normalized < 45f || normalized >= 315f) return Vector2Int.up;    // 0°
        if (normalized < 135f) return Vector2Int.right;  // 90°
        if (normalized < 225f) return Vector2Int.down;   // 180°
        return Vector2Int.left;                                                  // 270°
    }
}
