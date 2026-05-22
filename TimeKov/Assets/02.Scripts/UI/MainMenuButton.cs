// MainMenuButton.cs
// 설정창 "메인 메뉴로 돌아가기" 버튼에 부착
// Start()에서 GlobalSettingsManager.QuitToMainMenu() 를 onClick에 자동 연결

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MainMenuButton : MonoBehaviour
{
    void Start()
    {
        var btn = GetComponent<Button>();
        if (btn == null) return;

        btn.onClick.AddListener(() =>
        {
            var mgr = Object.FindAnyObjectByType<GlobalSettingsManager>();
            if (mgr != null)
                mgr.QuitToMainMenu();
            else
                Debug.LogWarning("[MainMenuButton] GlobalSettingsManager를 찾을 수 없습니다.");
        });
    }
}
