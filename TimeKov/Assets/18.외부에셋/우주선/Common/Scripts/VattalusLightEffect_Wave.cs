using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VattalusLightEffect_Wave : MonoBehaviour
{
    [Header("Intensity multiplier (relative to inensity set in the editor)")]
    public float minIntensity = 0.3f;
    public float maxIntensity = 1.2f;
    public float frequency = 2f;
    public bool constantSpeed = false;


    private Light _lightComponent;
    private float _defaultLightIntensity;
    private float _waveStartIntensity;
    private float _targetIntensity;
    private float _nextWaveTime;
    private float _waveDuration;
    // Start is called before the first frame update
    void Start()
    {
        //initialize values, make sure they are within correct/reasonable parameters
        if (minIntensity < 0f) minIntensity = 0f;
        if (maxIntensity > 10f) maxIntensity = 10f;

        //get the initial values
        _lightComponent = transform.GetComponent<Light>();
        if (_lightComponent != null) _defaultLightIntensity = _lightComponent.intensity;

        GenerateNewWave();
    }

    // Update is called once per frame
    void FixedUpdate()
    {

        if (Time.time > _nextWaveTime)
        {
            GenerateNewWave();
        }
        else
        {
            //actual animation of light intensity
            if (_lightComponent != null)
            {
                float _lerpPos = 1f - (_nextWaveTime - Time.time) / _waveDuration;
                _lightComponent.intensity = Mathf.Lerp(_waveStartIntensity, _targetIntensity, _lerpPos);
            }
        }
    }

    private float GetRandomLightIntensity()
    {
        return _defaultLightIntensity * Mathf.Lerp(minIntensity, maxIntensity, Random.Range(0f, 1f));
    }

    private void GenerateNewWave()
    {
        if (_lightComponent != null)
        {
            _waveStartIntensity = _lightComponent.intensity;

            //randomize starting value 
            _targetIntensity = GetRandomLightIntensity();
            float intensityFactor = Mathf.Lerp(0f, 1f, Mathf.Abs(_targetIntensity - _lightComponent.intensity) / (maxIntensity - minIntensity));
            if (constantSpeed)
            {
                _waveDuration = 1f / frequency * intensityFactor;
            }
            else
            {
                _waveDuration = 1f / frequency;
            }

            _nextWaveTime = Time.time + _waveDuration;
        }
    }
}
