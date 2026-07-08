using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

//simple script that plays a SFX then disables or destroys the gameobject
public class VattalusOneShotSFX : MonoBehaviour
{
    private AudioSource audioSource;
    [Tooltip("Pitch randomization factor applied to audio source")]
    public float pitchVariance = 0f;
    private float pitchInitial = 1f;
    [Tooltip("Volume randomization factor applied to audio source")]
    public float volumeVariance = 0f;
    private float volumeInitial = 1f;
    [Tooltip("Will destroy GO when it is disabled. Set to false if you wish to use this in a pooling system to enable GO recycling")]
    public bool destroyOnDisable = true;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        //read the initial pitch and volume values. This protects the baseline values against repeated alterations in case of multiple enable/disable cycles
        if (audioSource != null)
        {
            pitchInitial = audioSource.pitch;
            volumeInitial = audioSource.volume;
        }
    }

    void OnEnable()
    {
        if (audioSource != null) Initialize(audioSource.clip, pitchInitial, pitchVariance, volumeInitial, volumeVariance);
    }

    void OnDisable()
    {
        if (destroyOnDisable) Destroy(gameObject);
    }

    void FixedUpdate()
    {
        //check if it needs disabling
        if (audioSource == null || audioSource.isPlaying == false)
        {
            gameObject.SetActive(false);
        }
    }

    public void Initialize(AudioClip SetAudioClip, [CanBeNull]float? SetPitch, float SetPitchVariance, [CanBeNull]float? SetVolume, float SetVolumeVariance, bool SetDestroyOnDisable = true)
    {
        if (audioSource != null)
        {
            if (SetAudioClip != null) audioSource.clip = SetAudioClip;
            if (SetPitch == null) SetPitch = pitchInitial;
            audioSource.pitch = Mathf.Clamp((float)SetPitch + UnityEngine.Random.Range(-1f, 1f) * SetPitchVariance / 2f, 0.2f, 4f);

            if (SetVolume == null) SetVolume = volumeInitial;
            audioSource.volume = Mathf.Clamp((float)SetVolume + UnityEngine.Random.Range(-1f, 1f) * SetVolumeVariance / 2f, 0.2f, 4f);

            if (audioSource.clip != null) audioSource.PlayOneShot(audioSource.clip);
        }
    }
}