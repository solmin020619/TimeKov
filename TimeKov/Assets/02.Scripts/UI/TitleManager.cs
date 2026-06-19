using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

public class TitleManager : MonoBehaviour
{
    public string nextSceneName = "GameScene";
    public TextMeshProUGUI startText;
    public float blinkSpeed = 2f;

    [Header("���� ����")]
    public AudioSource audioSource;
    public AudioClip clickSound;

    private bool _isStarting = false;

    private void Update()
    {
        if (startText != null)
        {
            Color c = startText.color;
            c.a = Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed));
            startText.color = c;
        }

        // UI 버튼(게임 종료 등) 위를 클릭한 경우엔 게임을 시작하지 않는다.
        if (!_isStarting && Input.anyKeyDown && !IsPointerOverUI())
        {
            _isStarting = true;
            StartCoroutine(StartGameRoutine());
        }
    }

    private static bool IsPointerOverUI()
    {
        var es = EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    private IEnumerator StartGameRoutine()
    {
        if (startText != null)
        {
            startText.color = new Color(1, 1, 1, 1);
            startText.text = "�ε� ��...";
        }

        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
            yield return new WaitForSeconds(clickSound.length);
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        SceneManager.LoadScene(nextSceneName);
    }
}