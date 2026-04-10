using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyWorldUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private DamageNumberSpawner damageNumberSpawner;

    [Header("Settings")]
    [SerializeField] private float visibleDuration = 2f;   // NEW: 맞았을 때 표시 유지 시간
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f); // NEW: UI 위치 보정

    private EnemyHealth targetHealth;
    private Transform targetTransform;
    private Camera cam;
    private Coroutine hideRoutine;

    public void Initialize(EnemyHealth health, string enemyName)
    {
        targetHealth = health;
        targetTransform = health.transform;
        cam = Camera.main;

        if (nameText != null)
            nameText.text = enemyName;

        RefreshHP();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        targetHealth.OnDamage += RefreshHP;
        targetHealth.OnDeath += HandleDeath;
        targetHealth.OnDamageUI += HandleDamageUI;
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
        }
    }

    public void OnDamaged()
    {
        RefreshHP();
        ShowTemporarily();
    }

    private void HandleDamageUI(float damage, bool isCritical, Vector3 worldPos)
    {
        if (damageNumberSpawner != null)
            damageNumberSpawner.ShowDamage(damage, isCritical, worldPos);

        ShowTemporarily();
    }

    private void RefreshHP()
    {
        if (targetHealth == null || hpSlider == null)
            return;

        hpSlider.maxValue = targetHealth.maxHP;
        hpSlider.value = targetHealth.currentHP;
    }

    private void ShowTemporarily()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(HideRoutine());
    }

    private IEnumerator HideRoutine()
    {
        yield return new WaitForSeconds(visibleDuration);

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        hideRoutine = null;
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
        targetHealth.OnDamageUI -= HandleDamageUI;
    }
}