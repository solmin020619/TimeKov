using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyWorldUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("Display Settings")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] private float showDistance = 20f;
    [SerializeField] private bool alwaysShowBossLike = false;

    private EnemyHealth targetHealth;
    private Transform targetTransform;
    private Camera cam;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (hpSlider == null)
            hpSlider = GetComponentInChildren<Slider>(true);

        if (nameText == null)
            nameText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    public void Initialize(EnemyHealth health, string enemyName)
    {
        targetHealth = health;
        targetTransform = health.transform;
        cam = Camera.main;

        if (nameText != null)
            nameText.text = enemyName;

        RefreshHP();

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        targetHealth.OnDamage += RefreshHP;
        targetHealth.OnDeath += HandleDeath;
    }

    private void LateUpdate()
    {
        if (targetTransform == null)
            return;

        if (cam == null)
            cam = Camera.main;

        Vector3 worldPos = targetTransform.position + worldOffset;
        transform.position = worldPos;

        if (cam != null)
        {
            transform.forward = cam.transform.forward;
            UpdateVisibilityByDistance();
        }
    }

    private void UpdateVisibilityByDistance()
    {
        if (canvasGroup == null || cam == null || targetTransform == null)
            return;

        if (alwaysShowBossLike)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        float distance = Vector3.Distance(cam.transform.position, targetTransform.position);
        canvasGroup.alpha = distance <= showDistance ? 1f : 0f;
    }

    private void RefreshHP()
    {
        if (targetHealth == null || hpSlider == null)
            return;

        hpSlider.maxValue = targetHealth.maxHP;
        hpSlider.value = targetHealth.currentHP;
    }

    private void HandleDeath()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (targetHealth == null)
            return;

        targetHealth.OnDamage -= RefreshHP;
        targetHealth.OnDeath -= HandleDeath;
    }
}
