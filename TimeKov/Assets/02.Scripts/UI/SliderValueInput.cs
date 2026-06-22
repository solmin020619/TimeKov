using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 슬라이더 옆 숫자칸 — 평소엔 현재 값을 보여주고, 클릭해서 직접 숫자를 입력하면 슬라이더 값이 바뀐다.
// scale=100, decimals=0이면 입력칸은 0~100 정수(볼륨 등 %). 감도처럼 "배율(x)"로 보여줄 땐
// scale=1, decimals=2, suffix="x"로 설정해서 "1.00x" 형식으로 표시한다.
public class SliderValueInput : MonoBehaviour
{
    public Slider slider;
    public TMP_InputField input;
    public float scale = 100f;
    public int decimals = 0;
    public string suffix = "";

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
        input.SetTextWithoutNotify((v * scale).ToString("F" + decimals) + suffix);
    }

    void OnSubmit(string text)
    {
        string numeric = !string.IsNullOrEmpty(suffix) && text.EndsWith(suffix)
            ? text.Substring(0, text.Length - suffix.Length)
            : text;

        if (!float.TryParse(numeric, out float n))
        {
            UpdateFromSlider(slider.value); // 잘못 입력하면 원래 값으로 되돌림
            return;
        }
        float min = slider.minValue * scale;
        float max = slider.maxValue * scale;
        n = Mathf.Clamp(n, min, max);
        slider.value = n / scale;
        UpdateFromSlider(slider.value); // 클램프/포맷 반영해서 다시 그림
    }
}
