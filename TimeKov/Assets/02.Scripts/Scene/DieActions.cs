using UnityEngine;

public class DieActions : MonoBehaviour
{
    [SerializeField] private string baseSceneName = "Base_Scene";

    public void OnClickBackToBase()
    {
        Time.timeScale = 1f;
       
        

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadTo(baseSceneName);
    }

    public void OnClickQuit()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
