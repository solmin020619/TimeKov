using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴의 "게임 종료" 버튼. 클릭하면 게임을 종료한다.
/// onClick을 코드에서 자체 연결하므로 인스펙터 와이어링이 필요 없다.
/// </summary>
[RequireComponent(typeof(Button))]
public class MainMenuQuitButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Quit);
    }

    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
