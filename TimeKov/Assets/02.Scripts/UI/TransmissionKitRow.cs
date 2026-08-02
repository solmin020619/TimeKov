using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// [08-02] 시간에너지 전송기 - "보유 충전 키트" 목록의 한 줄.
//
// 행 개수는 인벤토리 보유량에 따라 변하므로 씬에 고정으로 둘 수 없다.
//   -> 빌더가 씬에 템플릿 1개를 만들어 꺼두고, 실행 시 보유 키트 수만큼 복제한다.
// 이 부품은 "빌더가 물려둔 조각 참조"만 들고 있고, 색/글자는 TransmissionComputerUI 가 넣는다.
public class TransmissionKitRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("행 본체")]
    public Image background;
    public UnityEngine.UI.Outline rowOutline;   // 풀네임 필수(전역 3D Outline 이 UnityEngine.UI.Outline 을 가림)
    public CanvasGroup group;
    public Button button;
    public Image accentBar;                     // 선택 시 좌측에 차오르는 지역색 바

    [Header("아이콘 웰")]
    public Image well;
    public UnityEngine.UI.Outline wellOutline;

    [Header("등급 글리프 - 일반=사각 / 보스=마름모+링")]
    public GameObject glyphNormal;
    public Image glyphSquare;
    public GameObject glyphBoss;
    public Image glyphRing;
    public Image glyphGem;

    [Header("글자")]
    public TMP_Text nameText;
    public TMP_Text metaText;
    public TMP_Text qtyText;
    public TMP_Text gainText;

    private TransmissionComputerUI _owner;
    private int _index = -1;

    public int Index => _index;

    /// <summary>복제 직후 1회. 행이 사는 동안 안 바뀌는 것(이름/등급 모양/지역 색)을 세팅한다.</summary>
    public void Bind(TransmissionComputerUI owner, int index, string kitName, string gainLabel, bool isBoss, Color regionColor)
    {
        _owner = owner;
        _index = index;

        if (nameText != null) nameText.text = kitName;
        if (gainText != null) gainText.text = gainLabel;

        if (well != null) well.color = new Color(regionColor.r, regionColor.g, regionColor.b, isBoss ? 0.20f : 0.09f);
        if (wellOutline != null) wellOutline.effectColor = new Color(regionColor.r, regionColor.g, regionColor.b, isBoss ? 0.7f : 0.3f);

        if (glyphNormal != null) glyphNormal.SetActive(!isBoss);
        if (glyphBoss != null) glyphBoss.SetActive(isBoss);
        if (glyphSquare != null) glyphSquare.color = new Color(regionColor.r, regionColor.g, regionColor.b, 0.9f);
        if (glyphRing != null) glyphRing.color = new Color(regionColor.r, regionColor.g, regionColor.b, 0.55f);
        if (glyphGem != null) glyphGem.color = regionColor;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        GameSfx.Play(SfxId.UIButtonClick);
        if (_owner != null) _owner.OnKitRowClicked(_index);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (button != null && button.interactable) GameSfx.Play(SfxId.UIButtonHover);
        // 잠긴 행도 툴팁은 뜬다(사용 불가 사유를 보여줘야 하므로).
        if (_owner != null) _owner.ShowKitTooltip(_index, (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_owner != null) _owner.HideTooltip();
    }
}
