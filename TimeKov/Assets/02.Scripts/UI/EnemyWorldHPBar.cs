using UnityEngine;
using UnityEngine.UI;

public class EnemyWorldHPBar : MonoBehaviour
{
    [Header("Target")]
    public EnemyHealth target;

    [Header("UI")]
    public RectTransform fillRect; // Fill Image의 RectTransform
    public Canvas worldCanvas;

    [Header("Options")]
    public Vector3 worldOffset = new Vector3(0f, 2.0f, 0f);
    public bool faceCamera = true;

    private Camera cam;
    private float fullWidth;

    private void Awake()
    {
        cam = Camera.main;

        if (worldCanvas == null) worldCanvas = GetComponentInChildren<Canvas>();
        if (fillRect != null) fullWidth = fillRect.sizeDelta.x;

        if (target == null)
            target = GetComponentInParent<EnemyHealth>();
    }

    private void OnEnable()
    {
        if (target != null)
            target.OnDamage += Refresh;
    }

    private void OnDisable()
    {
        if (target != null)
            target.OnDamage -= Refresh;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 머리 위 위치 고정
        transform.position = target.transform.position + worldOffset;

        // 카메라 바라보기
        if (faceCamera && cam != null)
        {
            Vector3 dir = transform.position - cam.transform.position;
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    private void Start()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (target == null || fillRect == null) return;

        float max = Mathf.Max(0.01f, target.maxHP);
        float ratio = Mathf.Clamp01(target.currentHP / max);

        // 너비 줄이기
        fillRect.sizeDelta = new Vector2(fullWidth * ratio, fillRect.sizeDelta.y);
    }
}
