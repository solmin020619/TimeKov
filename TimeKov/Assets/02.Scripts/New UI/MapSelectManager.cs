using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapSelectManager : MonoBehaviour
{
    [Header("Scene Settings")]
    public string loadingSceneName = "Loading";

    [Header("UI References")]
    public Button selectButton;      
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;   

    [Header("Sound")]
    public AudioSource sfxAudioSource;  
    public AudioClip clickSound; 

    private MapSlot currentSelectedSlot;
    private List<MapSlot> allSlots = new List<MapSlot>();

    void Start()
    {
        MapSlot[] slots = GetComponentsInChildren<MapSlot>();
        allSlots.AddRange(slots);

        if (selectButton != null)
        {
            selectButton.interactable = false;
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
            StartCoroutine(FadeIn());
        }
    }

    public void PlayClickSound()
    {
        if (sfxAudioSource != null && clickSound != null)
        {
            sfxAudioSource.PlayOneShot(clickSound);
        }
    }

    public void SelectMap(MapSlot clickedSlot)
    {
        PlayClickSound();
        currentSelectedSlot = clickedSlot;

        foreach (var slot in allSlots)
        {
            slot.SetSelected(false);
        }
        clickedSlot.SetSelected(true);

        if (selectButton != null)
        {
            if (clickedSlot.isLocked)
            {
                selectButton.interactable = false;
            }
            else
            {
                selectButton.interactable = true;
            }
        }
    }

    public void OnClickSelectButton()
    {
        if (currentSelectedSlot != null)
        {
            PlayClickSound();

            LoadingData.nextSceneName = currentSelectedSlot.sceneName;

            StartCoroutine(FadeOutAndLoad(loadingSceneName));
        }
    }

    // 뒤로가기 버튼용 (필요하면 사용)
    public void OnClickBack()
    {
        PlayClickSound();
        LoadingData.nextSceneName = "MainScene";
        StartCoroutine(FadeOutAndLoad(loadingSceneName));
    }

    IEnumerator FadeIn()
    {
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    IEnumerator FadeOutAndLoad(string targetScene)
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        SceneManager.LoadScene(targetScene);
    }
}