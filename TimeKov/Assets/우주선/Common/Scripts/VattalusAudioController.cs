using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//The Audio Controller offers more control over SFX, such as volume/pitch variations during looped playback, and also random/periodic triggers for non-looped SFX.
public class VattalusAudioController : MonoBehaviour
{
    private AudioSource audioSourceComponent;

    //initial volume/pitch values are saved at startup further manipulations are relative to them.
    private float _initialVolume;
    private float _initialPitch;

    private float _externalVolFactor = 1f;
    private float _externalPitchFactor = 1f;

    [Header("Volume modulation parameters")]
    public bool modulateVolume = false;
    [Tooltip("Randomly select an AudioClip (only when 'resetWithEachModulation' is true, and audiosource is not looping (aka intermittent sfx))")]
    public List<AudioClip> IntermittentSounds = new List<AudioClip>();
    [Tooltip("This can be used for random repeated/periodic SFXs (for example random beeps)")]
    public bool resetWithEachModulation = false;
    public Vector2 volumeFactorRange = new Vector2(1f, 1f);
    public Vector2 volumeDurationRange = new Vector2(2f, 3f);
    private float _nextVolModulationTime;
    private float _volModulationDuration;
    private Vector2 _volModulationInterval; //volume will modulate from x value to y value during the '_volModulationDuration' duration
    private float _currentVol;

    [Header("Pitch modulation parameters")]
    public bool modulatePitch = false;
    public Vector2 pitchFactorRange = new Vector2(1f, 1f);
    public Vector2 pitchDurationRange = new Vector2(2f, 3f);
    private float _nextPitchModulationTime;
    private float _pitchModulationDuration;
    private Vector2 _pitchModulationInterval; //pitch will modulate from x value to y value during the '_volModulationDuration' duration
    private float _currentPitch;

    void Awake()
    {
        //initialize values
        audioSourceComponent = GetComponent<AudioSource>();

        if (audioSourceComponent != null)
        {
            _initialVolume = audioSourceComponent.volume;
            _initialPitch = audioSourceComponent.pitch;
        }
    }

    void OnEnable()
    {
        //Initialization
        if (audioSourceComponent != null)
        {
            _currentVol = audioSourceComponent.volume;
            _currentPitch = audioSourceComponent.pitch;
        }
    }

    void FixedUpdate()
    {
        if (audioSourceComponent != null)
        {
            //VOLUME MODULATION
            if (modulateVolume) ModulateVolume();


            //PITCH MODULATION
            if (modulatePitch) ModulatePitch();


            //Apply the calculated volume and pitch modulations, while applying external factors
            audioSourceComponent.volume = _currentVol * _externalVolFactor;
            audioSourceComponent.pitch = _currentPitch * _externalPitchFactor;
        }
    }

    private void ModulateVolume()
    {
        if (Time.time >= _nextVolModulationTime)
        {
            //time for new modulation
            NewVolModulation();
        }
        else
        {
            //actual modulation of volume
            float _lerpPos = 1f - (_nextVolModulationTime - Time.time) / _volModulationDuration;
            _currentVol = Mathf.Lerp(_volModulationInterval.x, _volModulationInterval.y, _lerpPos);
        }
    }

    private void ModulatePitch()
    {
        if (Time.time >= _nextPitchModulationTime)
        {
            //time for new modulation
            NewPitchModulation();
        }
        else
        {
            //actual modulation of pitch
            float _lerpPos = 1f - (_nextPitchModulationTime - Time.time) / _pitchModulationDuration;
            _currentPitch = Mathf.Lerp(_pitchModulationInterval.x, _pitchModulationInterval.y, _lerpPos);
        }
    }

    //This function calculates the next volume modulation duration and target volume value
    private void NewVolModulation()
    {
        //determine the duration of the current modulation
        _volModulationDuration = Random.Range(volumeDurationRange.x, volumeDurationRange.y);
        _nextVolModulationTime = Time.time + _volModulationDuration;
        _volModulationInterval = new Vector2(audioSourceComponent.volume, Mathf.Lerp(_initialVolume * volumeFactorRange.x, _initialVolume * volumeFactorRange.y, Random.Range(0f, 1f)));

        if (resetWithEachModulation)
        {
            //when this sfx type is set to intermittent (non-looping and resetWithEachModulation==true) we check if the list of random sounds is populated, if so, randomly select a sound clip from there
            if (audioSourceComponent.loop == false && IntermittentSounds != null && IntermittentSounds.Count > 0)
            {
                audioSourceComponent.clip = IntermittentSounds[Random.Range(0, IntermittentSounds.Count - 1)];
            }

            audioSourceComponent.enabled = false;
            audioSourceComponent.enabled = true;
        }
    }

    //This function calculates the next pitch modulation duration and target pitch value
    private void NewPitchModulation()
    {
        //determine the duration of the current modulation
        _pitchModulationDuration = Random.Range(pitchDurationRange.x, pitchDurationRange.y);
        _nextPitchModulationTime = Time.time + _pitchModulationDuration;
        _pitchModulationInterval = new Vector2(audioSourceComponent.pitch, Mathf.Lerp(_initialPitch * pitchFactorRange.x, _initialPitch * pitchFactorRange.y, Random.Range(0f, 1f)));
    }

    //this method is called externall in order to influence the AudioSource final volume
    public void SetVolumeFactor(float newVolFactor)
    {
        _externalVolFactor = Mathf.Max(0f, newVolFactor);
    }

    //this method is called externall in order to influence the AudioSource final pitch
    public void SetPitchFactor(float newPitchFactor)
    {
        _externalPitchFactor = Mathf.Max(0f, newPitchFactor);
    }
}
