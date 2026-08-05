using UnityEngine;
using TMPro;

public class BuildModeHintsLocalizer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI demolishText;
    [SerializeField] private TextMeshProUGUI rotateText;

    private void OnEnable()
    {
        Refresh();
        Loc.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        Loc.OnLanguageChanged -= Refresh;
    }


    private void Refresh()
    {
        if (demolishText != null) demolishText.text = Loc.Get("해제 모드");
        if (rotateText   != null) rotateText.text   = Loc.Get("설비 회전");
    }
}
