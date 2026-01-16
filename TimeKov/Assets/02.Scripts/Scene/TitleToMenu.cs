using UnityEngine;
using UnityEngine.SceneManagement;

// Title_Scene에서 "화면을 눌러 계속하기" 역할.
// 마우스 클릭 또는 아무 키 입력이 들어오면 MainMenu_Scene으로 이동한다.
public class TitleToMenu : MonoBehaviour
{
    [Tooltip("타이틀에서 넘어갈 씬 이름")]
    public string nextSceneName = "MainMenu_Scene";

  


    private void Update()
    {
        //  Title 씬이 아니면 절대 입력 처리하지 않기
        if (SceneManager.GetActiveScene().name != "Title_Scene")
            return;

        // 마우스 좌클릭 또는 어떤 키든 입력되면 진행
        if (Input.GetMouseButtonDown(0) || Input.anyKeyDown)
        {
            // 한 번 눌렀으면 자기 자신 비활성화(중복 방지)
            enabled = false;

            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadDirect(nextSceneName);
        }
    }
}
