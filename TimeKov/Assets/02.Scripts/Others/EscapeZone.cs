using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class EscapeZone : MonoBehaviour
{
    public float requiredTime = 3f;
    public string nextSceneName = "Base";

    public GameObject escapeUIObject;
    public TMP_Text escapeText;

    private float currentTimer = 0f;
    private bool isPlayerInside = false;
    private bool hasLoaded = false; // ✅ 중복 로드 방지

    void Start()
    {
        if (escapeUIObject != null)
        {
            escapeUIObject.SetActive(false);
        }
    }

    void Update()
    {
        if (isPlayerInside)
        {
            currentTimer += Time.deltaTime;

            if (escapeText != null)
            {
                int remainingTime = Mathf.CeilToInt(requiredTime - currentTimer);
                escapeText.text = "Escape " + remainingTime.ToString();
            }

            if (currentTimer >= requiredTime && !hasLoaded)
            {
                hasLoaded = true;

                // ✅ 씬 이동 직전 세션 캡처 (인벤/장비/무기탄창 유지)
                if (PlayerSessionData.Instance != null)
                    PlayerSessionData.Instance.CaptureCurrent();

                SceneManager.LoadScene(nextSceneName);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;
            if (escapeUIObject != null)
            {
                escapeUIObject.SetActive(true);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            currentTimer = 0f;
            if (escapeUIObject != null)
            {
                escapeUIObject.SetActive(false);
            }
        }
    }
}