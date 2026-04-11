using UnityEngine;

public class BuildTopViewCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Top View")]
    [SerializeField] private Vector3 topViewOffset = new Vector3(0f, 30f, 0f);
    [SerializeField] private Vector3 topViewEuler = new Vector3(90f, 0f, 0f);
    [SerializeField] private float moveLerpSpeed = 12f;
    [SerializeField] private float rotateLerpSpeed = 12f;

    [Header("Optional Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private bool followTargetInTopView = true;

    private bool isTopView;
    private bool isReturningFromTopView;

    private Vector3 normalPosition;
    private Quaternion normalRotation;

    public bool IsTopView => isTopView;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
        {
            normalPosition = targetCamera.transform.position;
            normalRotation = targetCamera.transform.rotation;
        }
    }

    public void EnterTopView()
    {
        if (targetCamera == null)
            return;

        normalPosition = targetCamera.transform.position;
        normalRotation = targetCamera.transform.rotation;

        isTopView = true;
        isReturningFromTopView = false;
    }

    public void ExitTopView()
    {
        if (targetCamera == null)
            return;

        isTopView = false;
        isReturningFromTopView = true;
    }

    public void ToggleTopView()
    {
        if (isTopView) ExitTopView();
        else EnterTopView();
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        if (isTopView)
        {
            Vector3 targetPos;
            Quaternion targetRot = Quaternion.Euler(topViewEuler);

            if (followTargetInTopView && followTarget != null)
                targetPos = followTarget.position + topViewOffset;
            else
                targetPos = topViewOffset;

            targetCamera.transform.position = Vector3.Lerp(
                targetCamera.transform.position,
                targetPos,
                Time.deltaTime * moveLerpSpeed
            );

            targetCamera.transform.rotation = Quaternion.Slerp(
                targetCamera.transform.rotation,
                targetRot,
                Time.deltaTime * rotateLerpSpeed
            );
        }
        else if (isReturningFromTopView)
        {
            targetCamera.transform.position = Vector3.Lerp(
                targetCamera.transform.position,
                normalPosition,
                Time.deltaTime * moveLerpSpeed
            );

            targetCamera.transform.rotation = Quaternion.Slerp(
                targetCamera.transform.rotation,
                normalRotation,
                Time.deltaTime * rotateLerpSpeed
            );

            float posDiff = Vector3.Distance(targetCamera.transform.position, normalPosition);
            float rotDiff = Quaternion.Angle(targetCamera.transform.rotation, normalRotation);

            if (posDiff < 0.05f && rotDiff < 0.5f)
            {
                targetCamera.transform.position = normalPosition;
                targetCamera.transform.rotation = normalRotation;
                isReturningFromTopView = false;
            }
        }
    }
}