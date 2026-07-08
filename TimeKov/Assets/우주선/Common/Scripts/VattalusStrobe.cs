using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VattalusStrobe : MonoBehaviour
{
    public float intensityModifier = 1f;
    [Header("Strobe Timing")]
    public GameObject strobeGameObject;
    [Tooltip("Time between main strobe pulses")]
    public float strobePeriod;
    [Tooltip("Amount of time the strobe is activated")]
    public float strobeOnTime = 0.1f;
    [Tooltip("Time between main pulse and second pulse (set to 0 to disable)")]
    public float secondStrobeDelay;
    [Tooltip("Initial delay of the strobe process")]
    public float initialDelay;

    private LensFlare flareComponent;


    // Start is called before the first frame update
    void OnEnable()
    {
        if (strobePeriod <= 0.05f) strobePeriod = 0.05f;

        if (strobeGameObject)
        {
            flareComponent = strobeGameObject.GetComponent<LensFlare>();
            StartCoroutine(StartStrobe());
        }

    }

    private IEnumerator StartStrobe()
    {
        //disable the strobe GO
        //strobeGameObject.SetActive(false);
        strobeGameObject.SetActive(false);

        if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);

        while (Application.isPlaying)
        {
            //do the first pulse
            strobeGameObject.SetActive(true); StrobeOn();
            yield return new WaitForSeconds(strobeOnTime);
            strobeGameObject.SetActive(false);

            //second pulse
            if (secondStrobeDelay > 0f)
            {
                yield return new WaitForSeconds(secondStrobeDelay);

                StrobeOn();
                yield return new WaitForSeconds(strobeOnTime);
                strobeGameObject.SetActive(false);
            }

            yield return new WaitForSeconds(strobePeriod);
        }
    }

    //method to turn the strobe on
    private void StrobeOn()
    {
        //regulate lens flare intensity based on distance
        if (flareComponent != null)
        {
            flareComponent.brightness = 0.05f + 2f / Vector3.Distance(Camera.main.transform.position, flareComponent.transform.position) * intensityModifier;
        }


        strobeGameObject.SetActive(true);
    }
}
