using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HotbarSlotEffect : MonoBehaviour
{
    public Image slotBackground;

    private Color originalColor;

    [Header("효과 색상 설정")]
    public Color selectedColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    public Color flashColor = new Color(0f, 0f, 0f, 0.9f); 
    public float flashDuration = 0.05f;

    private Coroutine effectCoroutine;
    private bool isSelected = false;

    void Awake()
    {
        if (slotBackground != null)
        {
            originalColor = slotBackground.color;
        }
    }

    public void SetSelected(bool selected)
    {
        if (slotBackground == null) return;

        if (isSelected == selected) return;

        isSelected = selected;

        if (effectCoroutine != null) StopCoroutine(effectCoroutine);

        if (isSelected)
        {
            effectCoroutine = StartCoroutine(FlashAndKeepColor());
        }
        else
        {
            slotBackground.color = originalColor;
        }
    }

    IEnumerator FlashAndKeepColor()
    {
        slotBackground.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        slotBackground.color = selectedColor;
    }
}