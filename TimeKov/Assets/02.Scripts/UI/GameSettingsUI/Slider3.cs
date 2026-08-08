// Slider3.cs — 설정 슬라이더 (+ 음소거)
// (MonoBehaviour는 클래스명과 파일명이 같아야 유니티가 스크립트 에셋으로 인식한다.
//  한 파일에 몰아두면 씬/프리팹에 저장할 때 참조가 끊겨 Missing Script가 된다.)
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace GameSettingsUI
{
    public class Slider3 : MonoBehaviour
    {
        // 값 접근은 SettingId 경유. meta도 항목에서 파생되므로 따로 들고 있지 않는다.
        [Header("담당 설정 항목")]
        public SettingId setting;
        [Tooltip("음소거 아이콘 버튼을 쓰는가 (오디오 슬라이더만)")]
        public bool hasMute;
        [Tooltip("값 표기를 '0.75x' 형식으로 (마우스 감도)")]
        public bool sensFmt;

        [Header("참조")]
        public RectTransform track, fill, handle;
        public TMP_Text valueLabel;
        public GameObject iconOn, iconOff;

        // 음소거는 기존 설정 시스템에 대응 항목이 없는 UI 고유 상태라 이 위젯이 직접 들고 있다.
        // 켤 때 볼륨을 0으로 내리고 직전 값을 기억했다가 끌 때 되돌린다.
        bool muted;
        float remembered;

        public SliderMeta meta => SettingsBinding.Meta(setting);
        float get() => SettingsBinding.GetValue(setting);
        void  set(float v) => SettingsBinding.SetValue(setting, v);

        public bool IsMuted => muted;
        public void ToggleMute()
        {
            muted = !muted;
            if (muted) { remembered = get(); set(0f); }
            else set(remembered > 0f ? remembered : meta.max);
            PopAndRefresh();
        }
        /// 패널을 다시 열거나 초기화했을 때 — 값을 건드리지 않고 음소거 표시만 해제한다.
        public void ClearMute() { muted = false; }

        public void SetFromLocalX(float localX)
        {
            // 음소거 상태에서 드래그하면 자동 해제. 되돌리지 않고 드래그값을 그대로 쓴다.
            if (hasMute && muted) muted = false;
            float w = track.rect.width;
            float f = Mathf.Clamp01(localX / Mathf.Max(1f, w));
            float v = meta.Snap(meta.min + f * (meta.max - meta.min));
            if (!Mathf.Approximately(v, get())) QueuePreview();   // 값이 실제로 바뀔 때만
            set(v); Refresh(false);
        }

        // 볼륨 미리듣기음. 구 UI의 SettingsSliderSound와 같은 동작 —
        // 드래그 중에는 안 울리고, 멈춘 뒤 PreviewDelay 초 지나면 한 번 울린다.
        // (그 컴포넌트는 유니티 Slider를 요구해서 이 커스텀 슬라이더에는 붙지 않는다)
        const float PreviewDelay = 0.35f;
        float previewAt = -1f;
        void QueuePreview() { if (hasMute) previewAt = Time.unscaledTime + PreviewDelay; }
        void Update()
        {
            if (previewAt < 0f || Time.unscaledTime < previewAt) return;
            previewAt = -1f;
            SettingsBinding.PlayVolumePreview();
        }
        public void PopAndRefresh()
        {
            var go = muted ? iconOff : iconOn;
            if (go) UITween.Pop((RectTransform)go.transform);
            Refresh(true);
        }
        public void Refresh(bool animated)
        {
            float v = get();
            bool muted = hasMute && this.muted;
            float shown = muted ? meta.min : v;
            float f = (shown - meta.min) / (meta.max - meta.min);
            float w = track.rect.width;
            float dur = animated ? UIAnim.SliderAudio : 0f;
            float fw = w * f;
            if (dur > 0)
            {
                UITween.AnchorX(handle, fw, dur, Ease.OutQuad);
                UITween.Run_Width(fill, fw, dur);
            }
            else { fill.sizeDelta = new Vector2(fw, fill.sizeDelta.y); var hp = handle.anchoredPosition; hp.x = fw; handle.anchoredPosition = hp; }
            valueLabel.text = sensFmt ? v.ToString("0.00") + "x" : Mathf.RoundToInt(shown).ToString();
            if (hasMute)
            {
                if (iconOn) iconOn.SetActive(!muted);
                if (iconOff) iconOff.SetActive(muted);
            }
        }
    }
}
