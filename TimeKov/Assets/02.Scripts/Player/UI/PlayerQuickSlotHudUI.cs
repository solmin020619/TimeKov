using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 소모품 퀵슬롯 HUD 위젯(한 칸). 등록된 소모품 아이콘 + 개수 + 사용 키.
// PlayerHudUI 가 런타임 생성. 디자인 스프라이트(Resources/ContextQuickslot)가 있으면 쓰고, 없으면 절차 생성.
public class PlayerQuickSlotHudUI : MonoBehaviour
{
    private PlayerQuickSlotComponent _quick;
    private Image _frame;
    private Image _icon;
    private TextMeshProUGUI _countText;
    private TextMeshProUGUI _keyText;
    private TextMeshProUGUI _emptyText;
    private int _shownItemId = int.MinValue;

    private Sprite _slotEmpty, _slotReady;
    private bool _hasSlotSkin;

    private static readonly Color FrameOn  = new Color32(30, 35, 44, 225);
    private static readonly Color FrameOff = new Color32(22, 25, 31, 150);
    private static readonly Color TextMain = new Color32(228, 234, 242, 255);
    private static readonly Color TextDim  = new Color32(120, 128, 140, 255);

    private static Sprite Load(string n) => Resources.Load<Sprite>("ContextQuickslot/" + n);

    public void Setup(RectTransform parent, PlayerQuickSlotComponent quick, KeyCode key)
    {
        _quick = quick;

        var rt = (RectTransform)transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(-230f, 116f);   // 임시 위치(스킬바 좌측). 디자인 확정 후 조정.
        rt.sizeDelta = new Vector2(64f, 64f);

        _slotEmpty = Load("qs_slot_empty");
        _slotReady = Load("qs_slot_ready");
        _hasSlotSkin = _slotEmpty != null && _slotReady != null;
        var keyBadge = Load("qs_key_badge");
        var countBadge = Load("qs_count_badge");

        _frame = gameObject.AddComponent<Image>();
        _frame.sprite = _hasSlotSkin ? _slotEmpty : UISpriteFactory.RoundedRect(40, 12);
        _frame.type = _hasSlotSkin ? Image.Type.Simple : Image.Type.Sliced;
        _frame.preserveAspect = true;
        _frame.color = _hasSlotSkin ? Color.white : FrameOff;
        _frame.raycastTarget = false;

        _icon = MakeImage("Icon", rt, null);
        StretchCenter(_icon.rectTransform, new Vector2(42, 42));
        _icon.preserveAspect = true;

        // 키 안내 배지(좌상단) + 글자
        if (keyBadge != null)
        {
            var kb = MakeImage("KeyBadge", rt, keyBadge);
            Anchor(kb.rectTransform, new Vector2(0f, 1f), new Vector2(3f, -3f), new Vector2(24, 24));
        }
        _keyText = MakeText("Key", rt, 13f, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(_keyText.rectTransform, new Vector2(0f, 1f), new Vector2(3f, -3f), new Vector2(24, 24));
        _keyText.color = Color.white;
        _keyText.text = KeyLabel(key);

        // 개수 배지(우하단) + 숫자
        if (countBadge != null)
        {
            var cb = MakeImage("CountBadge", rt, countBadge);
            Anchor(cb.rectTransform, new Vector2(1f, 0f), new Vector2(-2f, 2f), new Vector2(32, 20));
        }
        _countText = MakeText("Count", rt, 15f, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(_countText.rectTransform, new Vector2(1f, 0f), new Vector2(-2f, 2f), new Vector2(32, 20));

        // 빈 슬롯 보조 텍스트(디자인 슬롯이 '비었음'을 표현하면 생략됨)
        _emptyText = MakeText("Empty", rt, 10f, FontStyles.Normal, TextAlignmentOptions.Center);
        StretchCenter(_emptyText.rectTransform, new Vector2(56, 28));
        _emptyText.color = TextDim;
        // Setup() 은 HUD 생성 때 1회만 돈다. 직접 번역해 넣으면 언어를 바꿔도 안 바뀐다.
        _emptyText.gameObject.AddComponent<LocalizedLabel>().SetKey("비어있음");

        RefreshNow();
    }

    private void Update()
    {
        if (_quick == null) return;
        Render();
    }

    private void RefreshNow() { _shownItemId = int.MinValue; Render(); }

    private void Render()
    {
        int id = _quick.RegisteredItemId;
        int count = _quick.Count;
        bool has = id > 0;

        // 아이콘은 등록 아이템이 바뀔 때만 갱신
        if (id != _shownItemId)
        {
            _shownItemId = id;
            var data = has ? ItemDatabase.GetItem(id) : null;
            var icon = data != null ? ItemDatabase.GetIcon(data.iconKey) : null;
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            if (_emptyText != null)
                _emptyText.gameObject.SetActive(!has && !_hasSlotSkin);
        }

        bool usable = has && count > 0;

        if (_hasSlotSkin)
            _frame.sprite = usable ? _slotReady : _slotEmpty;
        else
            _frame.color = usable ? FrameOn : FrameOff;

        if (_icon.enabled)
        {
            var c = _icon.color;
            c.a = usable ? 1f : 0.45f;
            _icon.color = c;
        }
        _countText.text = has ? count.ToString() : "";
        _countText.color = count > 0 ? TextMain : new Color32(220, 110, 110, 255);
    }

    // -- 생성 헬퍼 --
    private static Image MakeImage(string name, RectTransform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
        }
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI MakeText(string name, RectTransform parent, float size,
                                            FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.color = TextMain;
        t.raycastTarget = false;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    private static void StretchCenter(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    // 코너 앵커(배지 위치)
    private static void Anchor(RectTransform rt, Vector2 corner, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = corner;
        rt.pivot = corner;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static string KeyLabel(KeyCode key)
    {
        string s = key.ToString();
        if (s.StartsWith("Alpha")) s = s.Substring(5);   // Alpha1 -> 1
        return s;
    }
}
