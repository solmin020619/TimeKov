using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuActions : MonoBehaviour
{
    public void OnClickNewGame()
    {
        GameFlow.StartNewGame();
        SceneLoader.Instance.LoadTo("Base_Scene");
    }

    public void OnClickLoadGame()
    {
        GameFlow.StartLoadGame();
        SceneLoader.Instance.LoadTo("Base_Scene");
    }

    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서 Play 종료
#else
        Application.Quit(); // 빌드에서는 게임 종료
#endif
    }
}
