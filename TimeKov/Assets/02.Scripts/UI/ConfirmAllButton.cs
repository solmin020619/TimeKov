using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ConfirmAllButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image buttonImage;
    [SerializeField] private MachineUI machineUI;

    private Color _normalColor = new Color(1f, 1f, 1f, 1f);
    private Color _hoverColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    private Color _flashColor = new Color(1f, 1f, 1f, 0.3f);

    private Coroutine _flashRoutine;

    private void Awake()
    {
        if (buttonImage == null)
            buttonImage = GetComponent<Image>();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (buttonImage != null)
            buttonImage.color = _hoverColor;
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (buttonImage != null)
            buttonImage.color = _normalColor;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());

        machineUI?.TakeAll();
    }

    private IEnumerator FlashRoutine()
    {
        if (buttonImage == null) yield break;

        buttonImage.color = _flashColor;
        yield return new WaitForSeconds(0.05f);
        buttonImage.color = _hoverColor;
        yield return new WaitForSeconds(0.05f);
        buttonImage.color = _flashColor;
        yield return new WaitForSeconds(0.05f);
        buttonImage.color = _normalColor;
    }
}