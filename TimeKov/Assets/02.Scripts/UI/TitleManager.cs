using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class TitleManager : MonoBehaviour
{
    public string nextSceneName = "GameScene";
    public TextMeshProUGUI startText;
    public float blinkSpeed = 2f;

    [Header("사운드 설정")]
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

        if (!_isStarting && Input.anyKeyDown)
        {
            _isStarting = true;
            StartCoroutine(StartGameRoutine());
        }
    }

    private IEnumerator StartGameRoutine()
    {
        if (startText != null)
        {
            startText.color = new Color(1, 1, 1, 1);
            startText.text = "로딩 중...";
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