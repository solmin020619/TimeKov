using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadingSceneController : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(LoadNext());
    }

    private IEnumerator LoadNext()
    {
        string nextScene = GameFlow.NextSceneName;

        if (string.IsNullOrEmpty(nextScene))
        {
            Debug.LogError("[LoadingScene] NextSceneName is empty, fallback MainMenu");
            nextScene = "MainMenu_Scene";
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
            yield return null;

        // 여기서 로딩 끝
        SceneLoader.Instance.NotifyLoadComplete();

        op.allowSceneActivation = true;
    }
}
