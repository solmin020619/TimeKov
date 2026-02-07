using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 전환을 위해 필수
using UnityEngine.UI; // UI 제어를 위해 필수

public class MainMenuManager : MonoBehaviour
{
    [Header("Panel Groups")]
    public GameObject mainButtonGroup;  // 메인 버튼들 (New, Load, Option, Quit)을 묶어둔 부모 오브젝트
    public GameObject optionPanel;      // 옵션 팝업 창
    public GameObject quitConfirmPanel; // 종료 확인 팝업 창
    public GameObject loadingPanel;     // 로딩 화면 패널

    [Header("Loading Settings")]
    public Slider loadingSlider;        // 로딩 게이지 슬라이더
    public TextMeshProUGUI loadingText;            // 로딩 % 텍스트 (예: 99%)
    public string sceneName = "Base_Scene"; // 이동할 씬 이름

    [Header("Sound Settings")]
    public AudioSource sfxAudioSource;
    public AudioClip clickSound;

    private void Start()
    {
        // 시작할 때 팝업들은 다 꺼두고 메인 버튼만 켜기
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (optionPanel != null) optionPanel.SetActive(false);
        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
        if (mainButtonGroup != null) mainButtonGroup.SetActive(true);
    }

    void Update()
    {
        // Esc 키가 눌렸는지 매 프레임 확인
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 1. 종료 확인 팝업(Quit Panel)이 켜져 있다면? -> 닫기 (No 버튼 누른 것과 동일)
            if (quitConfirmPanel.activeSelf)
            {
                OnClickQuitNo();
                return; // 팝업을 닫았으면 더 이상 아래 코드는 실행하지 않고 종료
            }

            // 2. (보너스) 만약 옵션 창(Option Panel)이 켜져 있다면? -> 닫기
            if (optionPanel.activeSelf)
            {
                OnClickCloseOption();
                return;
            }
        }
    }

    // ------------------- 버튼 연결 함수들 -------------------

    // 1. New Game 버튼
    public void OnClickNewGame()
    {
        PlayClickSound();
        StartCoroutine(LoadSceneProcess());
    }

    // 2. Load Game 버튼 (아직 기능 없음)
    public void OnClickLoadGame()
    {
        Debug.Log("로드 기능은 추후 구현 예정입니다.");
    }

    // 3. Option 버튼
    public void OnClickOption()
    {
        optionPanel.SetActive(true);
        // 옵션 창이 뜨면 뒤에 버튼이 안 눌리게 메인 버튼을 끌 수도 있음 (선택사항)
        // mainButtonGroup.SetActive(false); 
    }

    // 옵션 창 닫기 버튼 (옵션 패널 안에 있는 X버튼이나 Back버튼에 연결)
    public void OnClickCloseOption()
    {
        optionPanel.SetActive(false);
        // mainButtonGroup.SetActive(true);
    }

    // 4. Quit 버튼 (확인 팝업 띄우기)
    public void OnClickQuit()
    {
        quitConfirmPanel.SetActive(true);
    }

    // ------------------- 종료 확인 팝업 내부 버튼 -------------------

    // Quit -> Yes 버튼
    public void OnClickQuitYes()
    {
        Debug.Log("게임 종료!");
        Application.Quit(); // 빌드된 게임에서만 작동함 (에디터에선 안 꺼짐)

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서도 꺼지게 하는 코드
#endif
    }

    // Quit -> No 버튼
    public void OnClickQuitNo()
    {
        quitConfirmPanel.SetActive(false);
    }

    // ------------------- 로딩 게이지 로직 (핵심) -------------------

    IEnumerator LoadSceneProcess()
    {
        // 1. 로딩 UI 켜기
        loadingPanel.SetActive(true);
        mainButtonGroup.SetActive(false); // 로딩 중에 버튼 못 누르게 끄기

        // 2. 비동기 씬 로드 시작
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false; // 로딩이 끝나도 바로 넘어가지 않게 막음 (게이지 연출을 위해)

        float timer = 0.0f;

        // 3. 로딩이 완료될 때까지 반복
        while (!op.isDone)
        {
            yield return null; // 1프레임 대기

            timer += Time.deltaTime;

            // op.progress는 0.0 ~ 0.9까지만 찹니다. (0.9가 로딩 완료 상태)
            if (op.progress < 0.9f)
            {
                loadingSlider.value = Mathf.Lerp(loadingSlider.value, op.progress, timer);
                if (op.progress >= loadingSlider.value) timer = 0f;
            }
            else
            {
                // 로딩은 거의 끝났지만, 게이지를 100%까지 자연스럽게 채우기 위한 연출
                loadingSlider.value = Mathf.Lerp(loadingSlider.value, 1f, timer);

                // 게이지가 꽉 찼다면 씬 전환 허용
                if (loadingSlider.value >= 0.99f)
                {
                    op.allowSceneActivation = true;
                }
            }

            // 텍스트 업데이트 (0% ~ 100%)
            if (loadingText != null)
            {
                loadingText.text = ((int)(loadingSlider.value * 100)).ToString() + "%";
            }
        }
    }

    public void PlayClickSound()
    {
        if (sfxAudioSource != null && clickSound != null)
        {
            sfxAudioSource.PlayOneShot(clickSound);
        }
    }
}