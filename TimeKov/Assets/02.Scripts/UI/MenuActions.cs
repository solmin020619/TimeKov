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

    public void OnClickSettings()
    {
        //  여기
        PlayerPrefs.SetString("settings_back_scene",
            SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();

        SceneLoader.Instance.LoadTo("Settings_Scene");
    }

    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
