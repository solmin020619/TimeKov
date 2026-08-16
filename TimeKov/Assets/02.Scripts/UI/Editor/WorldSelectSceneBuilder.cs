#if UNITY_EDITOR
// =====================================================================
// WorldSelectSceneBuilder.cs
// 월드 선택 화면에 모자란 UI 를 씬에 만들어 넣고 WorldSelectUI 에 연결한다.
//   Tools/TIMEKOV/UI/월드 선택 화면 보강
//
// [왜 코드가 아니라 씬에 만드나]
//   글자는 반드시 씬 오브젝트여야 한다 — 팀원이 씬을 훑어 번역할 문구를 모으기 때문에,
//   런타임에 만든 라벨은 그 수집에서 통째로 빠진다. 그래서 여기서 '한 번' 만들어 씬에
//   굽고, 실행 중에는 WorldSelectUI 가 색·크기·위치만 규격에 맞춘다(MenuModalStyle).
//
// [만드는 것]
//   1) CreateModal/Box       : Hint_WorldName, Counter_WorldName, Btn_Cancel(+_Border/Text)
//   2) DeleteConfirmModal    : 월드 삭제 확인창 한 벌
//   3) RowContainer/EmptyRow : 월드가 하나도 없을 때의 안내 줄
//   그리고 WorldSelectUI 의 인스펙터 칸 9개를 자동으로 연결한다.
//
// [이름 규칙] 씬에 이미 있는 것들을 그대로 따른다 —
//   모달 루트 <이름>Modal(CreateModal/QuitConfirmModal) · 딤 Backdrop · 본체 Box ·
//   제목 띠 LabelStrip(자식 Text) · 구분선 Sep · 버튼 Btn_X + Btn_X_Border(자식 Text) ·
//   목록 줄 XxxRow(자식 Label, CreateRow 와 동일) · 입력 관련 Xxx_WorldName(Input_WorldName 과 동일).
//   ★Btn_*_Border 는 실행 시 코드가 '이름으로' 찾으므로 반드시 이 형태여야 한다.
//
// 여러 번 돌려도 안전하다 — 같은 이름이 이미 있으면 그것을 재사용한다.
// 위치·크기·색은 실행 시 코드가 다시 잡으므로 여기서는 대충 잡아 둔다.
// =====================================================================

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class WorldSelectSceneBuilder
{
    const string MenuPath = "Tools/TIMEKOV/UI/월드 선택 화면 보강 (삭제 확인창·안내 문구)";

    [MenuItem(MenuPath)]
    static void Build()
    {
        var ui = Object.FindFirstObjectByType<WorldSelectUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("월드 선택 화면 보강",
                "열려 있는 씬에서 WorldSelectUI 를 찾지 못했습니다.\nMainMenu 씬을 열고 다시 실행해 주세요.", "확인");
            return;
        }

        var so = new SerializedObject(ui);
        var createModal = so.FindProperty("createModal").objectReferenceValue as GameObject;
        var input       = so.FindProperty("newWorldNameInput").objectReferenceValue as TMP_InputField;
        var rowContainer= so.FindProperty("rowContainer").objectReferenceValue as Transform;
        var createRow   = so.FindProperty("createRowButton").objectReferenceValue as Button;

        if (createModal == null || input == null)
        {
            EditorUtility.DisplayDialog("월드 선택 화면 보강",
                "WorldSelectUI 의 createModal / newWorldNameInput 이 비어 있습니다.\n먼저 그 둘을 연결해 주세요.", "확인");
            return;
        }

        var box = input.transform.parent as RectTransform;
        var font = input.textComponent != null ? input.textComponent.font : null;
        var panel = createModal.transform.parent as RectTransform;   // WorldSelectPanel

        Undo.SetCurrentGroupName("월드 선택 화면 보강");
        int group = Undo.GetCurrentGroup();

        // ── 1) 이름 입력 모달에 모자란 것들 ────────────────────────────
        // 안내 문구는 상황에 따라 내용이 바뀌어 코드가 .text 를 쓴다 → LocalizedLabel 을 붙이지 않는다.
        // 대신 씬에 대표 문구를 넣어 둬서, 문구 수집에는 잡히게 한다.
        var hint = MakeText(box, "Hint_WorldName", font, "이미 같은 이름의 월드가 있습니다.",
                            20, new Color(1f, 0.565f, 0.518f), TextAlignmentOptions.Left, localize: false);
        var counter = MakeText(box, "Counter_WorldName", font, "0 / 20",
                               19, new Color(0.604f, 0.604f, 0.604f), TextAlignmentOptions.Right, localize: false);

        var cancelCreate = MakeButton(box, "Btn_Cancel", font, "취소");
        ClearPlaceholder(input);
        BuildHeaderColumns(rowContainer, font);

        // ── 2) 삭제 확인창 ────────────────────────────────────────────
        var deleteModal = MakeDeleteModal(panel, font,
                                          out TMP_Text delMsg, out Button delOk, out Button delNo, out Button delBack);

        // ── 3) 목록이 비었을 때의 안내 줄 ─────────────────────────────
        GameObject emptyRow = null;
        if (rowContainer != null)
        {
            // 이름은 옆 형제와 맞춘다 — RowContainer 아래는 'CreateRow'(자식 'Label') 규칙이다.
            emptyRow = Find(rowContainer, "EmptyRow")?.gameObject;
            if (emptyRow == null)
            {
                emptyRow = new GameObject("EmptyRow", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(emptyRow, "EmptyRow");
                emptyRow.transform.SetParent(rowContainer, false);
            }
            // rowContainer 는 VerticalLayoutGroup 이라 높이를 LayoutElement 로 알려줘야 한다.
            var le = Ensure<LayoutElement>(emptyRow);
            le.minHeight = 84f; le.preferredHeight = 84f;

            var emptyText = MakeText((RectTransform)emptyRow.transform, "Label", font,
                                     "아직 만든 월드가 없습니다. 아래에서 새 월드를 만들어 보세요.",
                                     22, new Color(0.604f, 0.604f, 0.604f), TextAlignmentOptions.Center,
                                     localize: true);
            Stretch(emptyText.rectTransform);

            emptyRow.SetActive(false);
            if (createRow != null) createRow.transform.SetAsLastSibling();
        }

        // ── 연결 ──────────────────────────────────────────────────────
        Set(so, "cancelCreateButton", cancelCreate);
        Set(so, "nameHintText", hint);
        Set(so, "nameCounterText", counter);
        Set(so, "deleteModal", deleteModal);
        Set(so, "deleteMessageText", delMsg);
        Set(so, "deleteConfirmButton", delOk);
        Set(so, "deleteCancelButton", delNo);
        Set(so, "deleteBackdropButton", delBack);
        Set(so, "emptyRow", emptyRow);
        so.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        EditorUtility.SetDirty(ui);

        Debug.Log("[월드 선택 화면 보강] 완료. 씬을 저장하세요 (Ctrl+S).", ui);
        Selection.activeObject = ui.gameObject;
    }

    // 입력칸 안의 안내 글자(placeholder "새 월드 이름")를 없앤다.
    //   - 바로 위 제목 띠가 이미 같은 말을 하고 있어 두 번 읽힌다.
    //   - 그리고 이 글자는 번역 목록에 없다.
    // ★글자만 지우면 안 된다. 이 오브젝트에는 LocalizedLabel 이 달려 있어서, 실행하는 순간
    //   시트 값으로 되살아난다. 컴포넌트를 먼저 떼야 한다.
    static void ClearPlaceholder(TMP_InputField input)
    {
        var ph = input.placeholder as TMP_Text;
        if (ph == null) return;

        var loc = ph.GetComponent<LocalizedLabel>();
        if (loc != null) Undo.DestroyObjectImmediate(loc);

        Undo.RecordObject(ph, "placeholder 비우기");
        ph.text = string.Empty;
        EditorUtility.SetDirty(ph);
    }

    // 목록 머리글에 '마지막으로 플레이한 날짜' 열을 추가하고, 세 열을 규격대로 앉힌다.
    //   ★머리글 글자는 반드시 씬에 있어야 한다(팀원이 씬을 훑어 번역 문구를 모은다).
    //     그래서 실행 중에 만들지 않고 여기서 한 번 만들어 굽는다.
    //   각 줄의 값은 프리팹 인스턴스라 실행 중에 같은 x 로 앉힌다(WorldSelectRow).
    static void BuildHeaderColumns(Transform rowContainer, TMP_FontAsset font)
    {
        // 머리글은 목록과 형제다(RowContainer 안이 아니라 그 바깥).
        var panel = rowContainer != null ? rowContainer.parent as RectTransform : null;
        var header = Find(panel, "HeaderRow") as RectTransform;
        if (header == null) return;

        // ★가로 레이아웃 그룹을 먼저 끈다. 켜져 있으면 아래에서 잡아 준 x 를 매 레이아웃마다
        //   덮어쓰고 자식을 '형제 순서대로' 다시 늘어놓는다(경계선까지 같이 휩쓸린다).
        //   각 줄(WorldSelectRow)도 같은 이유로 자기 그룹을 끄고 수동 배치한다 — 그래야 열이 맞는다.
        var hlg = header.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) { Undo.RecordObject(hlg, "머리글 레이아웃"); hlg.enabled = false; }

        var name  = Find(header, "Col_월드명") as RectTransform;
        var level = Find(header, "Col_코어 레벨") as RectTransform;

        var date = MakeText(header, "Col_마지막 플레이", font, "마지막으로 플레이한 날짜",
                            WorldListLayout.FontHeader, new Color(0.804f, 0.847f, 0.898f),
                            TextAlignmentOptions.Left, localize: true);

        WorldListLayout.Place(name,  WorldListLayout.NameX,  WorldListLayout.NameW);
        WorldListLayout.Place(date.rectTransform, WorldListLayout.DateX, WorldListLayout.DateW);
        WorldListLayout.Place(level, WorldListLayout.LevelX, WorldListLayout.LevelW);

        // 경계선은 머리글 안에서만 긋는다. 줄마다 그으면 표가 아니라 격자가 된다.
        Separator(header, "Sep_1", WorldListLayout.Sep1X);
        Separator(header, "Sep_2", WorldListLayout.Sep2X);
    }

    static void Separator(RectTransform header, string name, float x)
    {
        var img = MakeImage(header, name, new Color(1f, 1f, 1f, 0.18f));
        WorldListLayout.Place(img.rectTransform, x, WorldListLayout.SepW, WorldListLayout.SepH);
        img.raycastTarget = false;
    }

    // ==================================================================
    //  삭제 확인창 한 벌
    // ==================================================================
    static GameObject MakeDeleteModal(RectTransform panel, TMP_FontAsset font,
                                      out TMP_Text message, out Button confirm,
                                      out Button cancel, out Button backdrop)
    {
        var root = Find(panel, "DeleteConfirmModal")?.gameObject;
        if (root == null)
        {
            root = new GameObject("DeleteConfirmModal", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "DeleteConfirmModal");
            root.transform.SetParent(panel, false);
            Stretch((RectTransform)root.transform);
        }
        var rootRt = (RectTransform)root.transform;

        // 딤 — 뒤쪽 클릭을 막는 역할이 본체다. 누르면 취소.
        var back = MakeImage(rootRt, "Backdrop", new Color(0f, 0f, 0f, 0.8f));
        Stretch(back.rectTransform);
        backdrop = Ensure<Button>(back.gameObject);
        backdrop.transition = Selectable.Transition.None;
        backdrop.targetGraphic = back;

        var box = MakeImage(rootRt, "Box", MenuModalStyle.Box).rectTransform;
        Place(box, Vector2.zero, new Vector2(880f, 320f));

        var strip = MakeImage(box, "LabelStrip", MenuModalStyle.Strip).rectTransform;
        Place(strip, new Vector2(0f, 114f), new Vector2(420f, 44f));
        Stretch(MakeText(strip, "Text", font, "월드 삭제", 23, Color.white,
                         TextAlignmentOptions.Center, localize: true).rectTransform);

        // 본문은 코드가 월드 이름을 넣어 다시 쓴다 → LocalizedLabel 을 붙이지 않는다.
        // 씬에 넣어 두는 이 문구가 곧 시트 키다({0} 자리에 이름이 들어간다).
        message = MakeText(box, "Message", font, "{0} 월드를 삭제할까요?", 26,
                           MenuModalStyle.TextBody, TextAlignmentOptions.Center, localize: false);
        Place(message.rectTransform, new Vector2(0f, 34f), new Vector2(760f, 76f));
        message.textWrappingMode = TextWrappingModes.Normal;

        var sub = MakeText(box, "SubMessage", font, "이 월드의 진행 상황은 되돌릴 수 없습니다.", 21,
                           MenuModalStyle.TextSub, TextAlignmentOptions.Center, localize: true);
        Place(sub.rectTransform, new Vector2(0f, -26f), new Vector2(760f, 34f));

        var sep = MakeImage(box, "Sep", MenuModalStyle.Sep).rectTransform;
        Place(sep, new Vector2(0f, -54f), new Vector2(760f, 1f));

        cancel  = MakeButton(box, "Btn_Cancel", font, "취소");
        confirm = MakeButton(box, "Btn_Confirm", font, "삭제");
        Place((RectTransform)cancel.transform, new Vector2(-MenuModalStyle.BtnOffsetX, -98f),
              new Vector2(MenuModalStyle.BtnW, MenuModalStyle.BtnH));
        Place((RectTransform)confirm.transform, new Vector2(MenuModalStyle.BtnOffsetX, -98f),
              new Vector2(MenuModalStyle.BtnW, MenuModalStyle.BtnH));

        root.SetActive(false);
        return root;
    }

    // ==================================================================
    //  조각 만들기 (이미 있으면 그대로 재사용)
    // ==================================================================

    // 버튼 = 테두리 판 + 본체 판 + 라벨. 테두리 이름은 실행 시 코드가 이름으로 찾으므로 고정이다.
    static Button MakeButton(RectTransform parent, string name, TMP_FontAsset font, string label)
    {
        var border = MakeImage(parent, name + "_Border", MenuModalStyle.BorderQuiet).rectTransform;
        Place(border, Vector2.zero, new Vector2(MenuModalStyle.BtnW + 2f, MenuModalStyle.BtnH + 2f));

        var bg = MakeImage(parent, name, MenuModalStyle.BtnFill);
        Place(bg.rectTransform, Vector2.zero, new Vector2(MenuModalStyle.BtnW, MenuModalStyle.BtnH));

        var text = MakeText(bg.rectTransform, "Text", font, label, 23, Color.white,
                            TextAlignmentOptions.Center, localize: true);
        Stretch(text.rectTransform);

        var btn = Ensure<Button>(bg.gameObject);
        btn.targetGraphic = bg;
        return btn;
    }

    static Image MakeImage(RectTransform parent, string name, Color color)
    {
        var t = Find(parent, name);
        GameObject go;
        if (t != null) go = t.gameObject;
        else
        {
            go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
        }
        var img = Ensure<Image>(go);
        img.color = color;
        return img;
    }

    /// <param name="localize">true 면 LocalizedLabel 을 붙인다. 코드가 .text 를 덮어쓰는
    /// 라벨(안내 문구·삭제 본문)에는 붙이면 안 된다 — 서로 덮어써서 둘 다 깨진다.</param>
    static TMP_Text MakeText(RectTransform parent, string name, TMP_FontAsset font, string content,
                             float size, Color color, TextAlignmentOptions align, bool localize)
    {
        var t = Find(parent, name);
        GameObject go;
        if (t != null) go = t.gameObject;
        else
        {
            go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
        }

        var tmp = go.GetComponent<TMP_Text>();
        if (tmp == null) tmp = Undo.AddComponent<TextMeshProUGUI>(go);
        if (font != null) tmp.font = font;
        tmp.text = content;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;

        var existing = go.GetComponent<LocalizedLabel>();
        if (localize) LocalizedLabelEditorUtil.Attach(go, content);
        else if (existing != null) Undo.DestroyObjectImmediate(existing);

        return tmp;
    }

    static Transform Find(Transform parent, string name) => parent != null ? parent.Find(name) : null;

    // ★?? 를 쓰지 않는다 — 유니티 오브젝트는 파괴돼도 ?? 가 null 로 잡지 못한다.
    static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Set(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[월드 선택 화면 보강] 필드를 못 찾음: {field}");
    }
}
#endif
