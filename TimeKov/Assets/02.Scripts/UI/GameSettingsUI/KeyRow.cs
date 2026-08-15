// KeyRow.cs — 키 바인딩 행
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
    public class KeyRow : MonoBehaviour
    {
        [Header("담당 액션")]
        [Tooltip("GlobalSettingsManager.RebindActions의 인덱스")]
        public int actionIndex;

        [Header("참조")]
        public Image bg;
        public TMP_Text label;
        public RectTransform root;
        public UnityEngine.UI.Outline outline;
        public Btn hoverFx;

        CanvasGroup cg; bool listening; bool conflict;
        public void SetKey(string k) { if (!listening) label.text = k; }

        /// 다른 액션과 키가 겹치는 상태인가. 겹치면 행 전체를 붉게 물들여
        /// "여기가 문제다"를 바로 보이게 한다. (적용 자체는 패널이 막는다)
        public void SetConflict(bool on)
        {
            if (conflict == on) return;
            conflict = on;
            ApplyIdleColors();
        }

        // 리스닝이 아닐 때의 색. 평상/충돌 두 가지뿐이라 한곳에서 결정한다.
        //   ★호버 색까지 같이 바꿔야 한다. Btn 이 기억한 예전 색으로 마우스를 떼는 순간
        //     되돌아가서, 충돌인데도 회색으로 보이는 일이 생긴다.
        void ApplyIdleColors()
        {
            if (listening) return;   // 리스닝 색이 우선 — 해제될 때 여기로 다시 온다

            bg.color    = conflict ? UIColors.KeyConflictBG   : UIColors.KeyBG;
            label.color = conflict ? UIColors.KeyConflictText : UIColors.TextValue;
            if (outline) outline.effectColor = conflict ? UIColors.KeyConflictBorder : UIColors.KeyBorder;

            if (hoverFx)
            {
                hoverFx.hoverColor   = conflict ? UIColors.KeyConflictHover  : UIColors.KeyBGHover;
                hoverFx.pressedColor = conflict ? UIColors.KeyConflictActive : UIColors.KeyBGActive;
                hoverFx.SetNormal(conflict ? UIColors.KeyConflictBG : UIColors.KeyBG);
            }
        }

        public void SetListening(bool on)
        {
            listening = on;
            if (hoverFx) hoverFx.enabled = !on;
            if (cg == null) { cg = root.gameObject.GetComponent<CanvasGroup>(); if (cg == null) cg = root.gameObject.AddComponent<CanvasGroup>(); }
            if (on)
            {
                label.text = "_";
                bg.color = UIColors.KeyListeningBG;
                label.color = UIColors.KeyListeningAccent;
                if (outline) outline.effectColor = UIColors.KeyListeningAccent;
                UITween.Pulse(cg);
            }
            else
            {
                UITween.Stop(cg, "pulse"); cg.alpha = 1f;
                ApplyIdleColors();
                // 리스닝 중 라벨은 "_"로 바꿔 놓았다. 현재 바인딩으로 되돌리지 않으면
                // 리바인딩이 실제로 됐는데도 "_"가 그대로 남아 실패한 것처럼 보인다.
                label.text = SettingsBinding.KeyLabel(actionIndex);
            }
        }
    }
}
