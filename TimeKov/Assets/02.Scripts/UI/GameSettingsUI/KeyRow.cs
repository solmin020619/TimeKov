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

        CanvasGroup cg; bool listening;
        public void SetKey(string k) { if (!listening) label.text = k; }
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
                bg.color = UIColors.KeyBG;
                label.color = UIColors.TextValue;
                if (outline) outline.effectColor = UIColors.KeyBorder;
                // 리스닝 중 라벨은 "_"로 바꿔 놓았다. 현재 바인딩으로 되돌리지 않으면
                // 리바인딩이 실제로 됐는데도 "_"가 그대로 남아 실패한 것처럼 보인다.
                label.text = SettingsBinding.KeyLabel(actionIndex);
            }
        }
    }
}
