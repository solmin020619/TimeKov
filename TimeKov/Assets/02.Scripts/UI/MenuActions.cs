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
        Application.Quit();
    }
}
