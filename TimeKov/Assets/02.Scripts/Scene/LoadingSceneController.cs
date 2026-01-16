using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadingSceneController : MonoBehaviour
{
    [SerializeField] private LoadingUIController ui; // 인스펙터 연결

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

        //  로딩 끝 → 0.5초 딜레이 + 페이드아웃 연출
        if (ui != null)
            yield return ui.PlayCompleteAndFadeOut();
        else
            yield return new WaitForSeconds(0.5f);

        // 충돌 방지해서 if 문 추가
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.NotifyLoadComplete();


        //  페이드 끝났으니 씬 전환
        op.allowSceneActivation = true;
    }
}
