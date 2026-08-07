using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// [08-02] 우주선 수리 패널의 특수 부품(전송 보상) 칩 1개.
//   레벨당 1개씩 있는 부품(선체 보강재/동력 안정기/엔진 등)을 상시 눈에 보이게 하는 작은 칩.
//   개수는 데이터(ShipRepairManager 의 레벨 정의)에 따라 달라져서, 빌더가 만든 템플릿을
//   런타임에 복제해서 쓴다. 계층/색/폰트는 전부 씬에 있고 여기서는 상태만 반영한다.
public class ShipExtraPartChip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("칩 배경. 상태(미획득/보유/사용됨)에 따라 틴트가 바뀐다.")]
    [SerializeField] private Image background;
    [Tooltip("부품 이름. 내용은 데이터에서 온다.")]
    [SerializeField] private TextMeshProUGUI nameText;

    private ShipRepairUI _owner;
    private int _level;
    private string _partName = "";

    /// <summary>복제 직후 1회. 이 칩이 어느 레벨의 부품인지와 표시 이름을 정한다.</summary>
    public void Setup(ShipRepairUI owner, int level, string partName)
    {
        _owner = owner;
        _level = level;
        _partName = partName ?? "";
        ApplyName();
    }

    // ★[08-07] 언어 변경 대응. 칩은 ShipRepairUI.BuildDynamic() 에서 만들어지는데 거기에
    //   _dynamicBuilt 가드가 있어 게임당 한 번만 돈다. Setup 에서 번역해 넣고 끝내면
    //   언어를 바꿔도 옛 글자가 남는다. 접두사를 떼는 후처리가 있어 LocalizedLabel 로는
    //   대체가 안 되므로 직접 구독한다(패널이 닫혀 있는 동안 바뀐 언어는 OnEnable 이 따라잡는다).
    private void OnEnable()
    {
        Loc.OnLanguageChanged += ApplyName;
        ApplyName();
    }

    private void OnDisable()
    {
        Loc.OnLanguageChanged -= ApplyName;
    }

    private void ApplyName()
    {
        if (nameText == null || string.IsNullOrEmpty(_partName)) return;
        // "우주선 " 접두사는 패널 안에서 군더더기라 뗀다(한국어일 때만 해당).
        string localized = Loc.Get(_partName);
        nameText.text = localized.StartsWith("우주선 ") ? localized.Substring(4) : localized;
    }

    /// <summary>상태 반영. bgTint = 배경 틴트, nameCol = 이름 색.</summary>
    public void SetState(Color bgTint, Color nameCol)
    {
        if (background != null) background.color = bgTint;
        if (nameText != null) nameText.color = nameCol;
    }

    public int Level => _level;

    public void OnPointerEnter(PointerEventData e)
    {
        if (_owner != null) _owner.ShowChipTooltip(_level, (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_owner != null) _owner.HideChipTooltip();
    }
}
