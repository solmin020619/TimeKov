using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    public string nextSceneName = "GameScene";

    public AudioSource audioSource;
    public AudioClip clickSound;

    [Tooltip("할당 시: 씬을 바로 로드하는 대신 이 패널을 띄워 월드를 선택/생성하게 한다. 비어있으면 기존 동작(바로 nextSceneName 로드).")]
    public WorldSelectUI worldSelectUI;

    private bool _isStarting = false;

    /// <summary>메뉴 리스트의 "게임 시작" 항목이 호출. 기존 any-key 시작과 동일한 코루틴을 탄다.</summary>
    public void OnClickStart()
    {
        if (_isStarting) return;
        _isStarting = true;
        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        // 메인메뉴가 timeScale=0(정지)이면 WaitForSeconds(스케일타임)는 영원히 안 끝나 LoadScene에 도달 못함.
        // -> Realtime 으로 대기해야 함. (좌클릭해도 안 넘어가던 진짜 원인)
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
            yield return new WaitForSecondsRealtime(clickSound.length);
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.5f);
        }

        Time.timeScale = 1f;   // 게임 시작 전 시간 정상화(메뉴에서 멈춰 있었을 수 있음)

        if (worldSelectUI != null)
            worldSelectUI.Show();   // 월드 선택/생성 후 그 안에서 씬 전환
        else
            SceneManager.LoadScene(nextSceneName);   // 폴백(미연결 시 기존 동작)
    }
}