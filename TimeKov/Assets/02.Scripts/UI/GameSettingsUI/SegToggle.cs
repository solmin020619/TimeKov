// SegToggle.cs — 표시 모드 2-세그먼트 토글
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
    public class SegToggle : MonoBehaviour
    {
        [Header("참조")]
        public RectTransform knob;   // 슬라이드하는 흰 노브
        public TMP_Text full, win;   // 좌/우 세그먼트 라벨

        public void Apply(DisplayMode m, bool instant)
        {
            float x = m == DisplayMode.Full ? 4f : 4f + UIAnim.ToggleKnobTravel;
            UITween.AnchorX(knob, x, instant ? 0 : UIAnim.ToggleKnob, Ease.InOutCubic);
            full.color = m == DisplayMode.Full ? UIColors.TextDark : UIColors.ToggleTextOff;
            win.color = m == DisplayMode.Window ? UIColors.TextDark : UIColors.ToggleTextOff;
        }
    }
}
