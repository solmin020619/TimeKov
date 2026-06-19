// =====================================================================
// CodexUI.cs
// 도감(엔드필드 백과 풍) - 임시 골격(placeholder).
// K로 열고 게임 정지(전체화면). 상태/정지/입력차단/커서는 GameUIController(UIState.Codex)가 관리,
// 여기선 패널 표시/숨김만.
//
// 틀만: 상단 컬럼 탭(튜토리얼/몬스터/레시피) + 좌측 항목 리스트 + 메인 내용 영역.
// 실제 비주얼/내용/인터랙션은 클로드디자인 확정 후 교체 예정. (지금은 절차생성 회색 박스)
// 씬 세팅 불필요 - 최초 표시 시 런타임 자동 생성.
// =====================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CodexUI : MonoBehaviour
{
    private static CodexUI _i;
    private static bool _quitting;

    public static CodexUI I
    {
        get
        {
            if (_i == null && !_quitting)
            {
                var go = new GameObject("[CodexUI]");
                _i = go.AddComponent<CodexUI>();
                DontDestroyOnLoad(go);
                _i.Build();
            }
            return _i;
        }
    }

    // 켜질 때만 생성(한 번도 안 열면 비용 0). GameUIController.ApplyState에서 매 상태전환 호출.
    public static void SetVisible(bool on)
    {
        if (!on && _i == null) return;   // 안 열렸으면 숨김 호출은 무시(생성 방지)
        I.SetOpenInternal(on);
    }

    // 임시 색(인벤 간유리 톤 대충 맞춤 - 최종은 디자인 교체)
    private static readonly Color BgColor    = new Color32(10, 13, 18, 235);
    private static readonly Color PanelColor = new Color32(26, 30, 38, 240);
    private static readonly Color TabColor    = new Color32(40, 46, 58, 255);
    private static readonly Color TabSelColor = new Color32(64, 116, 150, 255);
    private static readonly Color TextColor  = new Color32(225, 232, 240, 255);
    private static readonly Color DimText    = new Color32(140, 150, 165, 255);

    private static readonly string[] Columns = { "튜토리얼", "몬스터", "레시피" };

    private GameObject _root;

    private void OnApplicationQuit() => _quitting = true;
    private void OnDestroy() { if (_i == this) _i = null; }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;   // HUD 위, 토스트(5500) 아래

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // 전체화면 배경(뒤 클릭 차단)
        _root = NewRect("Root", (RectTransform)transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        _root.AddComponent<Image>().color = BgColor;

        // 상단 컬럼 탭 (breadcrumb 대신 - 튜토리얼/몬스터/레시피)
        var tabBar = NewRect("Columns", (RectTransform)_root.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -110f), new Vector2(-40f, -40f));
        var hlg = tabBar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        for (int i = 0; i < Columns.Length; i++)
            MakeTab(tabBar.transform, Columns[i], i == 0);

        // 좌측 항목 리스트 (사진5 빨강 영역)
        var left = NewRect("EntryList", (RectTransform)_root.transform,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(40f, 40f), new Vector2(360f, -130f));
        left.AddComponent<Image>().color = PanelColor;
        AddLabel(left.transform, "항목 리스트\n(미발견 = ???)", DimText, 22f);

        // 메인 내용 (사진5 파랑 영역)
        var main = NewRect("Content", (RectTransform)_root.transform,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(376f, 40f), new Vector2(-40f, -130f));
        main.AddComponent<Image>().color = PanelColor;
        AddLabel(main.transform, "내용 영역 (디자인/내용 대기 중)\n\nK 또는 ESC로 닫기", DimText, 26f);

        _root.SetActive(false);
    }

    private void SetOpenInternal(bool on)
    {
        if (_root != null) _root.SetActive(on);
    }

    // ── 생성 헬퍼 ──
    private static GameObject NewRect(string name, RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
        return go;
    }

    private void MakeTab(Transform parent, string label, bool selected)
    {
        var go = new GameObject("Tab_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = selected ? TabSelColor : TabColor;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 160f; le.minWidth = 160f;
        AddLabel(go.transform, label, selected ? TextColor : DimText, 24f);
    }

    private void AddLabel(Transform parent, string text, Color color, float size)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.color = color;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.enableWordWrapping = true;
    }
}
