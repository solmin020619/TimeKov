using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VattalusLightController : MonoBehaviour
{
    //States and settings
    public bool currentState = true;

    private Material lightMaterial;
    private Color initialColor;

    private class LightClass
    {
        public Light lightComponent;
        public float initialIntensity;
    }

    private List<LightClass> Lights = new List<LightClass>();
    public List<Light> AttachedLightComponents = new List<Light>();

    public MeshRenderer lightMesh;

    //Flickering
    public bool enableFlickering = false;
    public Vector2 flickerIntensityRange = new Vector2(0f, 0.4f);
    public Vector2 flickerPeriodRange = new Vector2(2f, 10f); //how long between flickers
    public Vector2 flickerDurationRange = new Vector2(0.01f, 0.07f); //how long do the flickers last
    private Coroutine flickerCoroutine;

    void Start()
    {
        //Initialize material
        MeshRenderer renderer = lightMesh;
        if (renderer == null) renderer = GetComponent<MeshRenderer>();
        if (renderer != null) lightMaterial = renderer.material;
        if (lightMaterial != null) initialColor = lightMaterial.GetColor("_EmissionColor");


        //Initialize list of attached light components
        Lights = new List<LightClass>();
        foreach (Light lightComponent in GetComponentsInChildren<Light>(true))
        {
            LightClass newLight = new LightClass();
            newLight.lightComponent = lightComponent;
            newLight.initialIntensity = lightComponent.intensity;

            Lights.Add(newLight);
        }

        foreach (Light attachedLight in AttachedLightComponents)
        {
            LightClass newLight = new LightClass();
            newLight.lightComponent = attachedLight;
            newLight.initialIntensity = attachedLight.intensity;

            Lights.Add(newLight);
        }


        //make sure everything is set up correctly based on initial state value
        ChangeState(currentState);
    }

    void OnEnable()
    {
        ChangeState(currentState);
    }

    void OnDisable()
    {

    }

    //Sets the light state to the provided bool variable, if variable is missing, it will toggle to the opposite state
    public void ChangeState()
    {
        ChangeState(!currentState);
    }

    public void ChangeState(bool newState)
    {
        currentState = newState;

        //update LightComponent values
        if (currentState == true) ResetLightIntensity(); else SetLightIntensity(0f);

        UpdateFlickering();
    }

    private void SetLightIntensity(float intensityFactor)
    {
        if (lightMaterial != null) lightMaterial.SetColor("_EmissionColor", initialColor * intensityFactor);
        foreach (LightClass light in Lights)
        {
            light.lightComponent.intensity = light.initialIntensity * intensityFactor;
        }
    }

    private void ResetLightIntensity()
    {
        if (lightMaterial != null) lightMaterial.SetColor("_EmissionColor", initialColor);
        foreach (LightClass light in Lights)
        {
            light.lightComponent.intensity = light.initialIntensity;
        }
    }


    //Start / Stop Flickering based on current state
    private void UpdateFlickering()
    {
        if (currentState == true && enableFlickering) StartFlickering(); else StopFlickering();
    }
    private void StartFlickering()
    {
        if (flickerCoroutine != null) StopCoroutine(flickerCoroutine);
        flickerCoroutine = StartCoroutine(FlickerCoroutine());
    }

    private void StopFlickering()
    {
        if (flickerCoroutine != null) StopCoroutine(flickerCoroutine);
    }

    IEnumerator FlickerCoroutine()
    {
        //start the flicker
        while (Application.isPlaying)
        {
            yield return new WaitForSeconds(Random.Range(flickerPeriodRange.x, flickerPeriodRange.y));

            //trigger flicker
            SetLightIntensity(Random.Range(flickerIntensityRange.x, flickerIntensityRange.y));
            yield return new WaitForSeconds(Random.Range(flickerDurationRange.x, flickerDurationRange.y));
            ResetLightIntensity();

            //random double flicker
            if (Random.Range(0f, 1f) <= 0.35f)
            {
                yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
                SetLightIntensity(Random.Range(flickerIntensityRange.x, flickerIntensityRange.y));
                yield return new WaitForSeconds(Random.Range(flickerDurationRange.x, flickerDurationRange.y));
                ResetLightIntensity();
            }
        }
    }
}
