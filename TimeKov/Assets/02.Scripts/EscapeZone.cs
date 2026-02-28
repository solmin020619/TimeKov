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

            if (currentTimer >= requiredTime)
            {
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