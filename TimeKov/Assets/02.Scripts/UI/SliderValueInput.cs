using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 슬라이더 옆 숫자칸 — 평소엔 현재 값을 보여주고, 클릭해서 직접 숫자를 입력하면 슬라이더 값이 바뀐다.
// scale=100이면 입력칸은 0~100 정수, 슬라이더 자체는 0~1로 환산해서 적용한다.
public class SliderValueInput : MonoBehaviour
{
    public Slider slider;
    public TMP_InputField input;
    public float scale = 100f;

    void OnEnable()
    {
        if (slider == null || input == null) return;
        UpdateFromSlider(slider.value);
        slider.onValueChanged.AddListener(UpdateFromSlider);
        input.onEndEdit.AddListener(OnSubmit);
    }

    void OnDisable()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(UpdateFromSlider);
        if (input != null) input.onEndEdit.RemoveListener(OnSubmit);
    }

    void UpdateFromSlider(float v)
    {
        input.SetTextWithoutNotify(Mathf.RoundToInt(v * scale).ToString());
    }

    void OnSubmit(string text)
    {
        if (!int.TryParse(text, out int n))
        {
            UpdateFromSlider(slider.value); // 잘못 입력하면 원래 값으로 되돌림
            return;
        }
        int min = Mathf.RoundToInt(slider.minValue * scale);
        int max = Mathf.RoundToInt(slider.maxValue * scale);
        n = Mathf.Clamp(n, min, max);
        slider.value = n / scale;
        input.SetTextWithoutNotify(n.ToString());
    }
}
