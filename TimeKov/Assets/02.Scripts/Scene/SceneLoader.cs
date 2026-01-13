using UnityEngine;
using UnityEngine.SceneManagement;

// 모든 씬 이동을 한 곳에서만 담당하는 로더
// 어디서든 LoadTo("SceneName") 호출
// 항상 Loading_Scene을 먼저 로드
// Loading_Scene이 Async로 목표 씬을 로드
public class SceneLoader : MonoBehaviour
{
    // 전역 접근용 싱글톤 인스턴스
    public static SceneLoader Instance { get; private set; }

    [Header("Scene Name")]
    [Tooltip("항상 경유할 로딩 씬 이름")]
    public string loadingSceneName = "Loading_Scene";

    // 씬 전환 중 중복 호출 방지 플래그.
    private bool isLoading = false;

    private void Awake()
    {
        // 이미 존재하는 Instance가 있으면 새로 생성된 것은 파괴
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 싱글톤 설정 + 씬이 바뀌어도 유지
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    // 목표 씬으로 이동 요청
    // 항상 Loading_Scene을 먼저 로드하고, 실제 목표 씬 로드는 LoadingSceneController가 수행한다.
    public void LoadTo(string targetSceneName)
    {
        // 로딩 중이면 추가 요청 무시(중복 클릭 방지)
        if (isLoading) return;

        // 씬 이름이 바뀌어있으면 에러
        if (string.IsNullOrEmpty(targetSceneName))
        {
            return;
        }

        // 잠금 
        isLoading = true;

        // Loading_Scene이 읽어갈 목표 씬 이름 저장
        GameFlow.SetNextScene(targetSceneName);

        // Loading 씬으로 이동
        SceneManager.LoadScene(loadingSceneName);
    }

    // LoadingSceneController가 목표 씬 로드를 완료했을 때 호출.
    // 다음 로드 요청을 받을 수 있도록 잠금 해제.
    public void NotifyLoadComplete()
    {
        isLoading = false;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Loading_Scene이 로드되었을 때는 아직 유지
        if (scene.name != loadingSceneName)
        {
            isLoading = false;
        }
    }
}
