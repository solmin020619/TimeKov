using UnityEngine;

public class InGameAudioManager : MonoBehaviour
{
    public AudioSource gameBGMSpeaker;
    public AudioSource gameSFXSpeaker;

    void Start()
    {
        if (gameBGMSpeaker != null)
        {
            gameBGMSpeaker.volume = PlayerPrefs.GetFloat("BGMVolume", 1.0f);
        }

        if (gameSFXSpeaker != null)
        {
            gameSFXSpeaker.volume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        }
    }
}