// =====================================================================
// WorldSelectRow.cs
// WorldSelectUI 목록 안에서 슬롯(월드) 한 줄을 표시하는 UI 컴포넌트.
// 행 클릭 = "선택"(하이라이트)만 함 — 실제 입장/삭제는 WorldSelectUI 하단의
// 공용 액션 버튼(입장하기/삭제/취소)이 현재 선택된 슬롯을 대상으로 처리한다.
// =====================================================================

using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WorldSelectRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image background;
    [SerializeField] UnityEngine.UI.Outline hoverOutline;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI dateText;
    [SerializeField] TextMeshProUGUI levelText;
    [SerializeField] Button selectButton;

    static readonly Color NormalColor   = new Color32(0x0A, 0x10, 0x18, 0xEB);
    static readonly Color SelectedColor = new Color32(0x1E, 0x46, 0x60, 0xF5);
    static readonly Color OutlineHover  = new Color32(0x4D, 0xC8, 0xFF, 0xCC);

    bool _isSelected;
    public string SlotId { get; private set; }

    /// <summary>목록에 보이는 월드 이름. 삭제 확인창이 "무엇을 지우는지" 보여주는 데 쓴다.</summary>
    public string WorldName { get; private set; }

    // 더블클릭으로 바로 입장. 두 번째 클릭까지 인정하는 간격(초).
    const float DoubleClickWindow = 0.35f;
    float _lastClickTime = -1f;

    bool _columnsReady;

    // 프리팹은 [이름 위 / 날짜 아래] 한 덩어리 + [레벨] 로 짜여 있다. 날짜를 이름 옆
    // 자기 열로 빼내고, 세 글자를 머리글과 같은 x 에 앉힌다(WorldListLayout).
    // ★레이아웃 그룹을 먼저 꺼야 한다. 켜져 있으면 매 프레임 자식 위치를 다시 잡아
    //   수동 배치를 덮어써서, 옮겨도 제자리로 돌아간 것처럼 보인다.
    void EnsureColumns()
    {
        if (_columnsReady) return;
        _columnsReady = true;

        var root = (RectTransform)transform;
        var hlg = GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        Detach(nameText, root);
        Detach(dateText, root);
        Detach(levelText, root);

        // 세 글자를 다 꺼냈으니 남은 껍데기(TextColumn)는 자리만 차지한다.
        var leftover = root.Find("TextColumn");
        if (leftover != null) leftover.gameObject.SetActive(false);

        WorldListLayout.Place(nameText  != null ? nameText.rectTransform  : null,
                              WorldListLayout.NameX,  WorldListLayout.NameW);
        WorldListLayout.Place(dateText  != null ? dateText.rectTransform  : null,
                              WorldListLayout.DateX,  WorldListLayout.DateW);
        WorldListLayout.Place(levelText != null ? levelText.rectTransform : null,
                              WorldListLayout.LevelX, WorldListLayout.LevelW);

        WorldListLayout.Style(nameText,  WorldListLayout.FontName,  TextAlignmentOptions.Left,  true);
        WorldListLayout.Style(dateText,  WorldListLayout.FontDate,  TextAlignmentOptions.Left,  false);
        WorldListLayout.Style(levelText, WorldListLayout.FontLevel, TextAlignmentOptions.Right, true);
    }

    static void Detach(TMP_Text t, RectTransform root)
    {
        if (t == null || t.transform.parent == root) return;
        t.transform.SetParent(root, false);
    }

    public void Set(SaveSlotMeta meta, Action onSelect, Action onActivate = null)
    {
        EnsureColumns();

        SlotId = meta.slotId;
        WorldName = meta.worldName;
        // 이름은 플레이어가 친 문자열이라 <b> 같은 게 섞여 있으면 TMP 가 서식으로 읽어
        // 글자가 사라진다. 그리기 직전에 한 번 거른다.
        if (nameText != null) nameText.text = WorldNameRules.Display(meta.worldName);
        if (dateText != null) dateText.text = FormatDate(meta.lastPlayedIso);
        if (levelText != null) levelText.text = $"Lv.{meta.coreLevelSnapshot}";

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() =>
            {
                onSelect?.Invoke();

                // 목록에서 줄을 두 번 누르면 들어가는 건 이런 화면의 기본 동작이다.
                // (첫 클릭은 선택이므로, 잘못 눌러도 선택만 바뀐 상태에서 시작한다)
                float now = Time.unscaledTime;
                bool doubled = _lastClickTime > 0f && now - _lastClickTime <= DoubleClickWindow;
                _lastClickTime = doubled ? -1f : now;
                if (doubled) onActivate?.Invoke();
            });
        }
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (background != null) background.color = selected ? SelectedColor : NormalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverOutline != null) hoverOutline.effectColor = OutlineHover;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverOutline != null) hoverOutline.effectColor = Color.clear;
    }

    // ★날짜 구분자를 따옴표로 감싼다. 포맷의 맨 '/' 는 리터럴이 아니라 "현재 문화권의
    //   날짜 구분자"로 치환되기 때문에, 한국어 환경에서는 하이픈으로 나왔다.
    static string FormatDate(string iso)
    {
        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToLocalTime().ToString("yyyy'/'MM'/'dd HH:mm", CultureInfo.InvariantCulture);
        return "-";
    }
}
