// TopViewBannerBuilder.cs
// 건축(탑뷰) 모드 상단 배너 "탑뷰 모드 나가기 [B]" 를 다시 만든다.
//
// [왜 만들었나]
//   원래 이 배너는 글자가 통째로 구워진 PNG(Title(Quick).png) 한 장이었다. 이미지라서
//   로컬라이징이 불가능했고 모드별로 문구를 바꿀 수도 없었다.
//   프리팹 안에는 원래 조립(알약 + TMP 글자 + 키 박스)이 남아 있었지만
//   씬 인스턴스에서 셋 다 삭제되고 구운 이미지로 대체돼 있었다.
//
// [수치 근거]
//   구운 PNG(1868x842)를 픽셀로 재서 역산했다. 알약 세로 48.7, 키박스 65.2x23.4,
//   키 글자 높이 13.4(fontSize 18) 가 원본 실측값이다.
//   가로 폭은 고정하지 않는다 - TopViewBannerLabel 이 문구 길이에 맞춰 알약을 늘렸다 줄인다.
//   (모드가 바뀌면 문구 글자 수가 달라지고, 영어로 바꾸면 훨씬 길어지기 때문)
//
// [일회성]
//   한 번 돌려서 결과가 프리팹에 저장되면 이 파일은 지워도 된다.
//   단 런타임 컴포넌트 TopViewBannerLabel 은 계속 필요하다(지우지 말 것).

using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public static class TopViewBannerBuilder
{
    private const string PrefabPath = "Assets/05.Prefabs/UI/QuickSlotPanel.prefab";
    private const string BannerName = "Top_B";

    // 스프라이트
    // 알약 배경은 새로 만든 것을 쓴다. 옛 Title(Quick)X 는 좌우 끝이 완전한 반원(캡슐)이라
    // 각진 느낌이 안 났고, 텍스처의 45.6% / 69.9% 만 실제 그림이라 크기 계산도 번거로웠다.
    // Title(Quick)_Wide 는 원본의 세로 그라데이션(위 236 -> 아래 146)을 그대로 뽑아 쓰고
    // 모서리 반경만 높이의 23% 로 줄인 것. 여백 없이 텍스처를 꽉 채워서 sizeDelta = 보이는 크기다.
    private const string PillSprite = "Assets/Resources/Image/UI_Icon/QuickSlot/Title(Quick)_Wide.png";
    private const string KeySprite  = "Assets/Resources/Image/UI_Icon/QuickSlot/Square(Quick).png";

    // 폰트(경로 대신 guid - 폰트 파일명이 바뀌어도 안 깨진다)
    private const string BodyFontGuid = "c7f114f86ee5f4a499eb96a28800df18";
    private const string KeyFontGuid  = "8f586378b4e144a9851e7b34d9b748ee";

    private const float PillH = 48f;    // 알약 세로 (구운 원본 실측 48.73)
    private const float Gap   = 40f;    // 여백/간격. 이 값 하나가 알약 폭을 정한다(작을수록 타이트)

    private const float LabelFontSize = 26f;
    private static readonly Vector2 LabelSize = new Vector2(240f, 32f);   // 넉넉히(넘침 허용이라 잘리지 않음)

    // 키박스는 스프라이트에 투명 여백이 있어 RectTransform 이 보이는 크기보다 크다(가로 39.16% / 세로 64.38%).
    private const float KeyBoxVisibleW = 65.24f;
    private static readonly Vector2 KeyBoxSize  = new Vector2(166.6f, 36.4f);
    private static readonly Vector2 KeyTextSize = new Vector2(60f, 30f);
    private const float KeyFontSize = 18f;

    private static readonly Vector2 BannerPos = new Vector2(0f, 460f);

    private const string LabelDefault = "탑뷰 모드 나가기";
    private const string KeyDefault   = "B";

    [MenuItem("Tools/TIMEKOV/UI/탑뷰 배너 재생성")]
    public static void Build()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null) { Debug.LogError($"[탑뷰배너] 프리팹을 못 열었다: {PrefabPath}"); return; }

        try
        {
            var banner = FindDeep(root.transform, BannerName) as RectTransform;
            if (banner == null)
            {
                Debug.LogError($"[탑뷰배너] '{BannerName}' 을 못 찾았거나 RectTransform 이 아니다.");
                return;
            }

            // 기존 내용물 제거(재실행 안전)
            for (int i = banner.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(banner.GetChild(i).gameObject);

            var bodyFont = LoadFont(BodyFontGuid);
            var keyFont  = LoadFont(KeyFontGuid);

            // 1) 알약 배경 (폭은 아래에서 문구 길이에 맞춰 정한다)
            var pill = NewImage("Image", banner, Vector2.zero, new Vector2(100f, PillH), PillSprite);
            pill.color = Color.white;   // 옛 값이 0.934 회색이라 누렇게 보였다

            // 2) 문구
            var label = NewText("Text", banner, Vector2.zero, LabelSize, LabelDefault, bodyFont, LabelFontSize);
            label.color = Color.black;

            // 3) 키 박스 + 그 안의 글자
            var keyBox = NewImage("X_IMG", banner, Vector2.zero, KeyBoxSize, KeySprite);
            keyBox.color = Color.white;
            var keyText = NewText("Text (TMP)", (RectTransform)keyBox.transform,
                                  Vector2.zero, KeyTextSize, KeyDefault, keyFont, KeyFontSize);
            keyText.color = Color.white;

            // 4) 런타임 컴포넌트 - 모드별 문구 교체 + 폭 자동조절
            var ctrl = banner.GetComponent<TopViewBannerLabel>();
            if (ctrl == null) ctrl = banner.gameObject.AddComponent<TopViewBannerLabel>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("pill").objectReferenceValue    = pill.rectTransform;
            so.FindProperty("label").objectReferenceValue   = label;
            so.FindProperty("keyBox").objectReferenceValue  = (RectTransform)keyBox.transform;
            so.FindProperty("keyText").objectReferenceValue = keyText;
            so.FindProperty("gap").floatValue               = Gap;
            so.FindProperty("keyBoxVisibleWidth").floatValue = KeyBoxVisibleW;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 5) 에디터에서도 바로 제대로 보이게 여기서 한 번 배치해둔다(런타임엔 컴포넌트가 다시 한다).
            float pillW = LayoutNow(banner, pill.rectTransform, label, (RectTransform)keyBox.transform);
            SetRect(banner, BannerPos, new Vector2(pillW, PillH));

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[탑뷰배너] 재생성 완료 -> {PrefabPath}\n" +
                      $"  알약 {pillW:0.0} x {PillH} (여백 {Gap} 균등) / 문구 fontSize {LabelFontSize} / 키 fontSize {KeyFontSize}\n" +
                      $"  해제 모드 진입 시 \"해제 모드 [X]\" 로 바뀐다(TopViewBannerLabel).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 컴포넌트의 Relayout 과 같은 계산. 알약 폭을 돌려준다.
    private static float LayoutNow(RectTransform banner, RectTransform pill, TextMeshProUGUI label, RectTransform keyBox)
    {
        label.ForceMeshUpdate();
        float labelW = label.preferredWidth;
        float pillW = Gap + labelW + Gap + KeyBoxVisibleW + Gap;

        pill.sizeDelta = new Vector2(pillW, PillH);
        float left = -pillW * 0.5f;
        label.rectTransform.anchoredPosition = new Vector2(left + Gap + labelW * 0.5f, 0f);
        keyBox.anchoredPosition              = new Vector2(left + Gap + labelW + Gap + KeyBoxVisibleW * 0.5f, 0f);
        return pillW;
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindDeep(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static TMP_FontAsset LoadFont(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"[탑뷰배너] 폰트 guid {guid} 를 못 찾았다. TMP 기본 폰트로 대체된다.");
            return null;
        }
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }

    private static void SetRect(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private static Image NewImage(string name, RectTransform parent, Vector2 pos, Vector2 size, string spritePath)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        SetRect(rt, pos, size);

        var img = go.GetComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (img.sprite == null) Debug.LogWarning($"[탑뷰배너] 스프라이트를 못 찾았다: {spritePath}");
        img.raycastTarget = false;   // 배너는 클릭 대상이 아니다
        return img;
    }

    private static TextMeshProUGUI NewText(string name, RectTransform parent, Vector2 pos, Vector2 size,
                                           string text, TMP_FontAsset font, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        SetRect(rt, pos, size);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = false;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }
}
