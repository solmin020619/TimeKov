using UnityEngine;
using UnityEngine.UI;

// 볼륨 슬라이더 옆 아이콘 클릭 = 음소거 토글. 슬라이더 값을 0 ↔ 이전 값으로 오가며,
// 다른 설정과 동일하게 슬라이더의 onValueChanged를 거쳐 staged(_pending) 값으로만 반영된다
// ("설정 적용"을 눌러야 실제로 엔진/저장에 반영됨).
public class MuteToggleButton : MonoBehaviour
{
    public Slider slider;
    public Image icon;
    public Sprite unmutedSprite;
    public Sprite mutedSprite;

    private float _lastNonZeroValue = 1f;

    void OnEnable()
    {
        if (slider == null || icon == null) return;
        if (slider.value > 0f) _lastNonZeroValue = slider.value;
        RefreshIcon(slider.value);
        slider.onValueChanged.AddListener(RefreshIcon);

        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(ToggleMute);
    }

    void OnDisable()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(RefreshIcon);
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.RemoveListener(ToggleMute);
    }

    void ToggleMute()
    {
        if (slider == null) return;
        if (slider.value > 0f)
        {
            _lastNonZeroValue = slider.value;
            slider.value = 0f;
        }
        else
        {
            slider.value = _lastNonZeroValue > 0f ? _lastNonZeroValue : 1f;
        }
    }

    void RefreshIcon(float v)
    {
        if (v > 0f) _lastNonZeroValue = v;
        icon.sprite = v <= 0f ? mutedSprite : unmutedSprite;
    }
}
