// SettingsMainMenuButtonBuilder.cs
// Tools/UI/Add MainMenu Button To Settings
// 설정창(Settings 패널) 하단에 "메인 메뉴로 돌아가기" 버튼을 추가합니다.

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;

public static class SettingsMainMenuButtonBuilder
{
    private const string MenuPath = "Tools/UI/Add MainMenu Button To Settings";
    private const string BtnName  = "Btn_MainMenu";

    [MenuItem(MenuPath)]
    static void AddMainMenuButton()
    {
        // ── 1. 타겟 오브젝트 탐색 ────────────────────────────────────
        var settingsGO = GameObject.Find("Settings");
        if (settingsGO == null)
        {
            Debug.LogError("[SettingsBtn] 'Settings' GameObject를 찾을 수 없습니다.");
            return;
        }

        var settingsMgr = Object.FindAnyObjectByType<GlobalSettingsManager>();
        if (settingsMgr == null)
        {
            Debug.LogError("[SettingsBtn] GlobalSettingsManager를 찾을 수 없습니다.");
            return;
        }

        // 이미 존재하면 덮어쓰기
        var existing = settingsGO.transform.Find(BtnName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
            Debug.Log("[SettingsBtn] 기존 Btn_MainMenu 삭제 후 재생성합니다.");
        }

        // ── 2. Btn_ClosePopup 을 복제해서 베이스로 사용 ────────────
        var closeBtn = settingsGO.transform.Find("Btn_ClosePopup");
        if (closeBtn == null)
        {
            Debug.LogError("[SettingsBtn] 'Btn_ClosePopup'을 찾을 수 없습니다. 수동으로 버튼을 생성해주세요.");
            return;
        }

        var newBtnGO = (GameObject)PrefabUtility.InstantiatePrefab(closeBtn.gameObject, settingsGO.transform);
        if (newBtnGO == null)
            newBtnGO = Object.Instantiate(closeBtn.gameObject, settingsGO.transform);

        Undo.RegisterCreatedObjectUndo(newBtnGO, "Add MainMenu Button");
        newBtnGO.name = BtnName;

        // ── 3. RectTransform: 패널 좌하단 배치 ─────────────────────
        // Settings 패널: 1000×828  중앙앵커(0.5,0.5)
        // 우하단 여백에 버튼 위치 — 닫기(X) 버튼과 대칭
        var rt = newBtnGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(300f, 60f);
        rt.anchoredPosition = new Vector2(0f, -358f);   // 패널 하단 중앙

        // ── 4. Image — 둥근 사각형 느낌으로 단색 변경 ──────────────
        var img = newBtnGO.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = null;                                          // 공(ball) 아이콘 제거
            img.color  = new Color(0.15f, 0.15f, 0.15f, 0.85f);       // 어두운 반투명 배경
        }

        // ── 5. TextMeshProUGUI: 텍스트 변경 및 레이아웃 조정 ────────
        var tmp = newBtnGO.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text      = "메인 메뉴로 돌아가기";
            tmp.fontSize  = 22f;
            tmp.color     = Color.white;

            // 텍스트 RectTransform 을 버튼 전체에 맞춤
            var trt = tmp.GetComponent<RectTransform>();
            trt.anchorMin        = Vector2.zero;
            trt.anchorMax        = Vector2.one;
            trt.offsetMin        = Vector2.zero;
            trt.offsetMax        = Vector2.zero;
            trt.anchoredPosition = Vector2.zero;
        }

        // ── 6. Button.onClick — QuitToMainMenu 연결 ──────────────────
        var btn = newBtnGO.GetComponent<Button>();
        if (btn != null)
        {
            Undo.RecordObject(btn, "Wire MainMenu Button onClick");
            btn.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(btn.onClick, settingsMgr.QuitToMainMenu);
            EditorUtility.SetDirty(btn);
        }

        // ── 7. 씬 저장 플래그 ────────────────────────────────────────
        EditorUtility.SetDirty(settingsGO);
        EditorSceneManager.MarkSceneDirty(settingsGO.scene);

        // 하이어라키에서 선택
        Selection.activeGameObject = newBtnGO;

        Debug.Log("[SettingsBtn] ✅ 'Btn_MainMenu' 생성 완료! GlobalSettingsManager.QuitToMainMenu() 연결됨.");
    }

    [MenuItem(MenuPath, true)]
    static bool ValidateAddMainMenuButton()
        => !Application.isPlaying;
}
#endif
