using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ReloadRingUI : MonoBehaviour
{
    [Header("Bind Target (optional)")]
    [SerializeField] private PlayerWeaponController weaponController;

    [Header("UI")]
    [SerializeField] private Image ringImage;              // Filled Image
    [SerializeField] private TextMeshProUGUI labelText;    // 선택 (없어도 됨)

    private Coroutine co;

    private void Awake()
    {
        if (ringImage == null) ringImage = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (weaponController == null)
            weaponController = FindAnyObjectByType<PlayerWeaponController>();

        if (weaponController != null)
        {
            weaponController.onReloadStart += OnReloadStart;
            weaponController.onReloadEnd += OnReloadEnd;
        }

        // 시작은 숨김 상태
        SetVisible(false);
    }

    private void OnDisable()
    {
        if (weaponController != null)
        {
            weaponController.onReloadStart -= OnReloadStart;
            weaponController.onReloadEnd -= OnReloadEnd;
        }
    }

    private void OnReloadStart(float duration)
    {
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(CoFill(duration));
    }

    private void OnReloadEnd()
    {
        if (co != null) StopCoroutine(co);
        co = null;
        SetVisible(false);
    }

    private IEnumerator CoFill(float duration)
    {
        SetVisible(true);

        if (ringImage != null)
            ringImage.fillAmount = 0f;

        float t = 0f;
        float d = Mathf.Max(0.01f, duration);

        while (t < d)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / d);

            if (ringImage != null)
                ringImage.fillAmount = n;

            yield return null;
        }

        if (ringImage != null)
            ringImage.fillAmount = 1f;

        // reloadEnd 이벤트가 와서 끄는게 정석
        // 혹시 누락되면 안전하게 0.1초 뒤 숨김
        yield return new WaitForSeconds(0.1f);
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);

        if (labelText != null)
            labelText.gameObject.SetActive(visible);
    }
}
