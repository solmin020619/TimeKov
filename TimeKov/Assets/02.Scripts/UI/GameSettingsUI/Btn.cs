// Btn.cs — 호버/클릭 색·스케일 피드백 + 씬 배치용 액션 디스패치
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
    public class Btn : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                       IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        enum Mode { Color, Text, Icon }
        Mode mode = Mode.Color;
        Graphic target; Color n, h, a;
        RectTransform scaleT; float hoverScale = 1f, pressScale = 1f;
        Action onClick; bool hover;

        // 씬에 배치된 버튼용. 람다는 직렬화가 안 되므로 "무슨 일을 하는지"만 저장해 두고
        // 실제 처리는 상위의 패널 컨트롤러가 받는다. 코드로 만들 때는 onClick이 채워지므로
        // 그쪽이 우선한다(둘 다 있으면 onClick만 실행 — 중복 실행 방지).
        [Header("씬 배치용 동작")]
        public SettingsAction action;
        [Tooltip("옵션 인덱스(SelectOption) / 액션 인덱스(StartRebind) 등")]
        public int actionParam;
        bool colorFx = true;   // false면 배경색 피드백만 끈다(스케일·클릭은 유지)

        /// 배경 호버/클릭 색을 켜고 끈다. 이미 선택된 탭처럼 배경을 덮으면 안 되는 경우에 사용.
        public void SetColorFeedback(bool on)
        {
            if (colorFx == on) return;
            colorFx = on;
            if (mode != Mode.Color || !target) return;
            // 탭을 클릭하는 순간 그 탭은 호버 중 = 회색으로 가는 색 트윈이 돌고 있다.
            // 먼저 끊지 않으면 여기서 투명으로 되돌려도 트윈이 끝나며 회색으로 덮어쓰고,
            // 피드백이 꺼진 뒤라 아무도 되돌리지 않아 노란 인디케이터 위에 회색이 남는다.
            UITween.StopColor(target);
            target.color = on && hover ? h : n;
        }

        public void Init(Graphic target, Color n, Color h, Color a, RectTransform scaleT, float hoverScale, float pressScale, Action onClick)
        {
            mode = Mode.Color; this.target = target; this.n = n; this.h = h; this.a = a;
            this.scaleT = scaleT; this.hoverScale = hoverScale; this.pressScale = pressScale; this.onClick = onClick;
            if (target) target.color = n;
        }
        // 투명 히트 영역에 붙이고 label(실제로 보이는 텍스트)에 눌림 피드백을 준다.
        public void InitTextButton(Graphic label, Action onClick) { mode = Mode.Text; target = label; this.onClick = onClick; scaleT = null; }

        /// 기본(비호버) 색을 나중에 바꾼다. 드롭다운 선택이 옮겨갈 때 필요.
        /// 이걸 안 하면 Btn이 예전 기본색을 기억해, 호버 후 빠져나올 때 그 색으로 되돌아간다.
        public void SetNormal(Color c) { n = c; if (mode == Mode.Color && target && !hover) target.color = c; }
        public void InitIconButton(RectTransform wrap, Action onClick) { mode = Mode.Icon; this.onClick = onClick; scaleT = wrap; hoverScale = 1.1f; pressScale = 0.86f; }

        public void OnPointerEnter(PointerEventData e)
        {
            hover = true;
            if (colorFx && mode == Mode.Color && target) UITween.Color(target, h, UIAnim.HoverColor, Ease.Linear);
            if (scaleT && hoverScale != 1f) UITween.Scale(scaleT, hoverScale, UIAnim.HoverColor, Ease.OutQuad);
        }
        public void OnPointerExit(PointerEventData e)
        {
            hover = false;
            if (colorFx && mode == Mode.Color && target) UITween.Color(target, n, UIAnim.HoverColor, Ease.Linear);
            if (scaleT && hoverScale != 1f) UITween.Scale(scaleT, 1f, UIAnim.HoverColor, Ease.OutQuad);
            if (mode == Mode.Text) SetAlpha(1f);   // 누른 채로 벗어나도 복구
        }
        public void OnPointerDown(PointerEventData e)
        {
            if (colorFx && mode == Mode.Color && target) target.color = a;
            if (mode == Mode.Text) SetAlpha(0.6f);
            if (scaleT && pressScale != 1f) UITween.Scale(scaleT, pressScale, 0.06f, Ease.OutQuad);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (colorFx && mode == Mode.Color && target) UITween.Color(target, hover ? h : n, UIAnim.HoverColor, Ease.Linear);
            if (mode == Mode.Text) SetAlpha(1f);
            if (scaleT && pressScale != 1f) UITween.Scale(scaleT, hover && hoverScale != 1f ? hoverScale : 1f, 0.1f, Ease.OutQuad);
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (onClick != null) { onClick(); return; }
            if (action == SettingsAction.None) return;

            // 계층으로 먼저 찾되, 못 찾으면 현재 열린 패널을 쓴다.
            // 호스트 캔버스가 Overlay가 아닌 씬에서는 UI가 전용 Overlay 캔버스로 옮겨져
            // 빌더가 더 이상 조상이 아니게 되고, 그러면 GetComponentInParent가 null이 된다.
            var panel = GetComponentInParent<SettingsUIBuilder>();
            if (panel == null) panel = SettingsUIBuilder.Active;
            if (panel) panel.Dispatch(action, actionParam, this);
        }
        void SetAlpha(float v) { if (!target) return; var c = target.color; c.a = v; target.color = c; }
        void OnDisable()
        {
            if (mode == Mode.Color && target) target.color = n;
            if (mode == Mode.Text) SetAlpha(1f);
            if (scaleT) scaleT.localScale = Vector3.one;
        }
    }
}
