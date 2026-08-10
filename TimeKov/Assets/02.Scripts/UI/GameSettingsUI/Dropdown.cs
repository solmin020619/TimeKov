// Dropdown.cs — 설정 드롭다운 (버튼 + 팝업 + 옵션들)
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
    public class Dropdown : MonoBehaviour
    {
        // 값 접근은 델리게이트가 아니라 SettingId로 한다 — 씬에 배치할 때 람다는 직렬화가 안 된다.
        [Header("담당 설정 항목")]
        public SettingId setting;

        [Header("참조")]
        public RectTransform button;    // 닫힌 상태의 버튼(= 보통 이 오브젝트)
        public RectTransform popup;     // 열리는 옵션 패널
        public RectTransform chevron;   // ∨ 아이콘 (열리면 180° 회전)
        public RectTransform row;       // 이 드롭다운이 속한 설정 행 (열 때 최상단으로 올림)
        public RectTransform viewport;  // 스크롤 뷰포트 (위/아래 펼침 방향 판단 기준)
        public CanvasGroup popupCG;
        public TMP_Text valueLabel;
        public List<Image> optionImages = new List<Image>();
        public List<Btn>   optionButtons = new List<Btn>();

        const float PopupGap = 8f;   // 버튼과 팝업 사이 간격
        public bool isOpen;

        void OnEnable()  { Loc.OnLanguageChanged += RefreshDisplay; RefreshDisplay(); }
        void OnDisable() { Loc.OnLanguageChanged -= RefreshDisplay; }
        void RefreshDisplay() { SetValue(Current); }

        // 옵션 라벨은 직렬화하지 않는다 — 항목에서 항상 같은 순서로 나오므로 실행 시 받아온다.
        public string[] Options => SettingsBinding.Options(setting);
        public string Current => SettingsBinding.GetLabel(setting);
        public void Commit(string label)
        {
            SettingsBinding.SetLabel(setting, label);
            SetValue(label);
            SettingsBinding.PlayClick();   // 구 UI의 onValueChanged 선택음
        }

        public void SetValue(string v)
        {
            if (valueLabel) valueLabel.text = v;
            var vals = Options;
            for (int i = 0; i < optionImages.Count && i < vals.Length; i++)
            {
                var c = vals[i] == v ? UIColors.AccentYellowSoft : UIColors.Transparent(UIColors.OptHover);
                if (optionImages[i]) optionImages[i].color = c;
                if (i < optionButtons.Count && optionButtons[i]) optionButtons[i].SetNormal(c);   // Btn 기본색도 같이 갱신
            }
        }
        public void SetOpen(bool open)
        {
            if (open == isOpen) { if (!open && popup) popup.gameObject.SetActive(false); return; }
            isOpen = open;
            UITween.RotateZ(chevron, open ? 180f : 0f, UIAnim.Chevron, Ease.OutQuad);
            if (open)
            {
                if (row) row.SetAsLastSibling();   // 이 행을 패널 최상단으로 → 팝업이 아래 행들에 안 가려짐
                button.SetAsLastSibling();
                popup.gameObject.SetActive(true);

                // 아래 공간이 모자라면 위로 펼친다. 팝업은 스크롤 뷰포트의 RectMask2D에
                // 클리핑되므로, 뷰포트 경계를 기준으로 방향을 정해야 잘리지 않는다.
                bool up = ShouldFlipUp();
                popup.pivot = new Vector2(0.5f, up ? 0f : 1f);
                popup.anchoredPosition = new Vector2(0, up ? PopupGap : -(button.rect.height + PopupGap));

                popupCG.alpha = 0f; popup.localScale = Vector3.one * 0.98f;
                var basePos = popup.anchoredPosition;
                // ddIn: 버튼 쪽에서 8px 밀려 나오며 자리를 잡는다(펼침 방향에 따라 부호 반전).
                popup.anchoredPosition = basePos + new Vector2(0, up ? -PopupGap : PopupGap);
                UITween.Fade(popupCG, 1f, UIAnim.DropdownIn, Ease.OutQuad);
                UITween.Scale(popup, 1f, UIAnim.DropdownIn, Ease.OutQuad);
                UITween.AnchorY(popup, basePos.y, UIAnim.DropdownIn, Ease.OutQuad);
            }
            else popup.gameObject.SetActive(false);
        }

        // 뷰포트 기준으로 버튼 위/아래 여유를 재서 펼칠 방향을 고른다.
        bool ShouldFlipUp()
        {
            if (!viewport) return false;
            var b = new Vector3[4]; button.GetWorldCorners(b);    // 0=좌하 1=좌상 2=우상 3=우하
            var v = new Vector3[4]; viewport.GetWorldCorners(v);
            float scale = Mathf.Abs(button.lossyScale.y);
            float need  = (popup.rect.height + PopupGap) * scale;
            float below = b[0].y - v[0].y;   // 버튼 아랫변 ~ 뷰포트 아랫변
            float above = v[1].y - b[1].y;   // 뷰포트 윗변 ~ 버튼 윗변
            if (below >= need) return false; // 아래로 충분 → 기본(아래)
            if (above >= need) return true;  // 위로만 충분 → 뒤집기
            return above > below;            // 둘 다 부족하면 넓은 쪽
        }
    }
}
