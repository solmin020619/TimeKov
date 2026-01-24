using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseOverlay; // PauseOverlay 연결
    [SerializeField] private string settingsSceneName = "Settings_Scene";
    [SerializeField] private string baseSceneName = "Base_Scene";

    private bool isPaused = false;

    private void Start()
    {
        if (pauseOverlay != null)
            pauseOverlay.SetActive(false);

        //  설정 갔다가 돌아왔을 때 "다시 멈춘 상태"로 복원
        if (GameFlow.ResumePausedAfterReturn &&
            SceneManager.GetActiveScene().name == GameFlow.ReturnSceneName)
        {
            Pause();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    public void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (pauseOverlay != null)
            pauseOverlay.SetActive(true);

        // 커서 처리는 나중에 한다 했으니 여기서는 안 건드려도 됨
    }

    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseOverlay != null)
            pauseOverlay.SetActive(false);

        // 돌아가면 더 이상복원필요 없음
        GameFlow.SetReturnScene("", false);
    }

    // ---------- 버튼용 ----------

    public void OnClickResume()
    {
        Resume();
    }

    public void OnClickSettings()
    {
        //  현재 씬 이름 저장 + 돌아오면 다시 pause 유지
        string current = SceneManager.GetActiveScene().name;
        GameFlow.SetReturnScene(current, true);

        // 일단 씬 이동 위해 timeScale은 1로 풀어두는 게 안전(로딩/코루틴 꼬임 방지)
        Time.timeScale = 1f;

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadTo(settingsSceneName);
    }

    public void OnClickBackToBase()
    {
        // 베이스로 갈 땐 보통 pause 풀고 이동
        GameFlow.SetReturnScene("", false);
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
