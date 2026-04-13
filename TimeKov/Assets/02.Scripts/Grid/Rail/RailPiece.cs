using UnityEngine;

public class RailPiece : MonoBehaviour
{
    public Vector2Int cell;
    public bool up;
    public bool down;
    public bool left;
    public bool right;

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

        // 이 구조에서는 최대 2연결만 허용.
        // 0개 / 1개 연결도 끝 레일처럼 straight를 사용한다.
        bool useStraight = count <= 1 || (up && down) || (left && right);

        if (useStraight)
        {
            currentVisual = Instantiate(straightPrefab, transform);
            currentVisual.transform.localPosition = Vector3.zero;

            float yRot = 0f;

            if (left || right)
                yRot = 90f;
            else if (up || down)
                yRot = 0f;

            currentVisual.transform.localRotation = Quaternion.Euler(0f, yRot + straightRotationOffsetY, 0f);
            return;
        }

        currentVisual = Instantiate(cornerPrefab, transform);
        currentVisual.transform.localPosition = Vector3.zero;

        float cornerYRot = 0f;

        if (up && right) cornerYRot = 0f;
        else if (right && down) cornerYRot = 90f;
        else if (down && left) cornerYRot = 180f;
        else if (left && up) cornerYRot = 270f;

        currentVisual.transform.localRotation = Quaternion.Euler(0f, cornerYRot + cornerRotationOffsetY, 0f);
    }
}
