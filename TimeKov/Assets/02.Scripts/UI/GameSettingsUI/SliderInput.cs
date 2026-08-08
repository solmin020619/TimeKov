// SliderInput.cs — 슬라이더 트랙 드래그 입력
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
    public class SliderInput : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Slider3 s;          // 인스펙터 연결용
        RectTransform track;
        void Awake() { if (s) track = s.track; }
        public void Init(Slider3 slider) { s = slider; track = slider.track; }
        void Handle(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var lp);
            float localX = lp.x + track.rect.width * track.pivot.x;
            s.SetFromLocalX(localX);
        }
        public void OnPointerDown(PointerEventData e) => Handle(e);
        public void OnDrag(PointerEventData e) => Handle(e);
    }
}
