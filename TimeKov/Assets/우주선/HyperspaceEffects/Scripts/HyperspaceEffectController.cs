using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HyperspaceEffectController : MonoBehaviour
{
    public Renderer hyperspaceShellRenderer;
    private Material hyperspaceShellMaterial;

    [Header("Transition")]
    private float transitionStartTime = -9999f;
    private bool transitionDirection = true; // true = fade in, false = fade out
    public float transitionDuration = 3f;
    private bool transitionInProgress = false;
    public bool TransitionInProgress { get { return transitionInProgress; } }
    public AnimationCurve TransitionAnimCurve = new AnimationCurve();

    private float defaultShellOpaciy = 0.5f;
    public AnimationCurve TransitionShellOpacityIn = new AnimationCurve();
    public AnimationCurve TransitionShellOpacityOut = new AnimationCurve();

    [Header("Flashing (0 = OFF)")]
    public float flashingIntensity = 1f;
    private Color flashInitialColor = Color.white;
    private float currentFlashingIntensity = 0f;
    public float flashFadeSpeed = 5f;
    public Vector2 flashingPeriodRange = new Vector2(5f, 15f);

    private Color initialLight1Color;
    private Color initialLight2Color;

    //WIP: Additional particle effect
    //[Header("Particle Effects")]
    //public ParticleSystem particleEffects;
    //private Color particleInitialColor;

    [Header("Light / Lens Flare")]
    public Light hyperspaceLightSource;
    private LensFlare hyperspaceLensFlare;
    private Color lensLightSourceInitialColor;


    private Coroutine flashCoroutine;

    [Header("Sound Effect")]
    public AudioClip loopSfxClip;
    private AudioSource loopSfxAudioSource;
    public float loopSFXVolume = 1f;

    // Start is called before the first frame update
    void Awake()
    {
        Application.targetFrameRate = 120;

        if (hyperspaceShellRenderer != null)
        {
            hyperspaceShellMaterial = hyperspaceShellRenderer.material;

            defaultShellOpaciy = hyperspaceShellMaterial.GetFloat("_ShellOpacity_Value");

            flashInitialColor = hyperspaceShellMaterial.GetColor("_FlashingColor");
            flashInitialColor = new Color(flashInitialColor.r, flashInitialColor.g, flashInitialColor.b, 1f);
            initialLight1Color = hyperspaceShellMaterial.GetColor("_Light1_Color");
            initialLight2Color = hyperspaceShellMaterial.GetColor("_Light2_Color");
        }

        //if (particleEffects != null)
        //{
        //    particleInitialColor = particleEffects.main.startColor.color;
        //}

        if (hyperspaceLightSource != null)
        {
            lensLightSourceInitialColor = hyperspaceLightSource.color;
            hyperspaceLensFlare = hyperspaceLightSource.GetComponent<LensFlare>();
        }

        //SFX
        if (loopSfxClip != null)
        {
            GameObject LoopSfxGO = new GameObject("Loop SFX AudioSource");
            LoopSfxGO.transform.parent = transform;
            LoopSfxGO.transform.localPosition = new Vector3(0f, 0f, 300f); //position the audio source to be slightly forward to give the sense the sound is coming from the front

            //add and initialize audio source component
            loopSfxAudioSource = LoopSfxGO.AddComponent<AudioSource>();
            loopSfxAudioSource.clip = loopSfxClip;
            loopSfxAudioSource.loop = true;
            loopSfxAudioSource.spatialBlend = 0.65f;
            loopSfxAudioSource.dopplerLevel = 0f;
            loopSfxAudioSource.spread = 200f;
            loopSfxAudioSource.rolloffMode = AudioRolloffMode.Linear;
            loopSfxAudioSource.maxDistance = 750f;
            loopSfxAudioSource.volume = 0f; //volume is 0 by default

            loopSfxAudioSource.Play();
        }

        transitionInProgress = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (transitionInProgress)
        {
            //Transition
            float timeSinceAnimStart = Time.time - transitionStartTime;
            float progress = Mathf.Clamp01(timeSinceAnimStart / transitionDuration);
            SetTransitionEffects(progress);
        }

        //Light / Lens Flare
        if (hyperspaceLightSource != null && hyperspaceLensFlare != null)
        {
            hyperspaceLensFlare.color = hyperspaceLightSource.color;
            //hyperspaceLensFlare.brightness = hyperspaceLightSource.color.a * hyperspaceLightSource.intensity;
        }

        if (hyperspaceShellMaterial != null)
        {
            //Flashing
            if (currentFlashingIntensity > 0.01f)
                currentFlashingIntensity = Mathf.Lerp(currentFlashingIntensity, 0f, Time.deltaTime * flashFadeSpeed);

            hyperspaceShellMaterial.SetColor("_FlashingColor", flashInitialColor * currentFlashingIntensity);
        }

        //SFX
        if (loopSfxAudioSource != null)
        {
            float volumeFactorTransition = 1f - Mathf.Abs(hyperspaceShellRenderer.material.GetFloat("_Transition"));
            loopSfxAudioSource.volume = (volumeFactorTransition + hyperspaceShellRenderer.material.GetFloat("_ShellOpacity_Value")) / 2f;
        }
    }

    void OnEnable()
    {
        RestartFlashingCoroutine();
    }

    void OnDisable()
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
    }

    public void StartTransitionIn(bool instant = false)
    {
        transitionDirection = true;
        TransitionStarted(instant);

        //enable components
        if (hyperspaceShellRenderer != null) hyperspaceShellRenderer.enabled = true;
        //if (particleEffects != null) particleEffects.gameObject.SetActive(true);
        if (hyperspaceLightSource != null) hyperspaceLightSource.gameObject.SetActive(true);
    }

    public void StartTransitionOut(bool instant = false)
    {
        transitionDirection = false;
        TransitionStarted(instant);
    }

    private void TransitionStarted(bool instant)
    {
        transitionStartTime = Time.time;
        transitionInProgress = true;

        if (instant)
        {
            transitionStartTime = -9999f;
            SetTransitionEffects(1f);
        }
    }

    private void TransitionEnded()
    {
        transitionInProgress = false;

        if (transitionDirection == false)
        {
            //disable components
            if (hyperspaceShellRenderer != null) hyperspaceShellRenderer.enabled = false;
            //if (particleEffects != null) particleEffects.gameObject.SetActive(false);
            if (hyperspaceLightSource != null) hyperspaceLightSource.gameObject.SetActive(false);
        }

        RestartFlashingCoroutine(false);
    }

    //set the transition effects based on overall progress (0f->1f)
    private void SetTransitionEffects(float progress)
    {
        float transitionValue = transitionDirection ? Mathf.Lerp(-1f, 0f, TransitionAnimCurve.Evaluate(progress)) : Mathf.Lerp(0f, 1f, TransitionAnimCurve.Evaluate(progress));
        float shellOpacityValue = defaultShellOpaciy * (transitionDirection ? TransitionShellOpacityIn.Evaluate(progress) : TransitionShellOpacityOut.Evaluate(progress));

        //shader
        if (hyperspaceShellMaterial != null)
        {
            hyperspaceShellMaterial.SetFloat("_Transition", transitionValue);
            hyperspaceShellMaterial.SetFloat("_ShellOpacity_Value", Mathf.Clamp01(shellOpacityValue));

            //if shell opacity value exceeds 1, make the light layers brighter
            hyperspaceShellMaterial.SetColor("_Light1_Color", initialLight1Color * shellOpacityValue);
            hyperspaceShellMaterial.SetColor("_Light2_Color", initialLight2Color * shellOpacityValue);
        }

        //particles
        //if (particleEffects != null)
        //{
        //    ParticleSystem.MainModule main = particleEffects.main;
        //    main.startColor = particleInitialColor * shellOpacityValue;
        //}

        //light source
        if (hyperspaceLightSource != null)
        {
            hyperspaceLightSource.color = lensLightSourceInitialColor * (1f - Mathf.Abs(transitionValue) + shellOpacityValue) / 2f;
        }


        if (progress >= 1f)
        {
            TransitionEnded();
        }
    }


    public void RestartFlashingCoroutine(bool initialDelay = true)
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashCoroutine());
    }
    IEnumerator FlashCoroutine(bool initialDelay = true)
    {
        //start the flicker
        while (Application.isPlaying)
        {
            if (initialDelay) yield return new WaitForSeconds(Random.Range(flashingPeriodRange.x, flashingPeriodRange.y));

            if (flashingIntensity > 0f)
            {
                //trigger flash
                RandomizeFlashTexture();
                currentFlashingIntensity = Random.Range(flashingIntensity, flashingIntensity * 0.5f);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.07f));

                //random double flash
                if (Random.Range(0f, 1f) <= 0.35f)
                {
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
                    currentFlashingIntensity = Random.Range(flashingIntensity, flashingIntensity * 0.5f);
                }
            }
        }
    }

    private void RandomizeFlashTexture()
    {
        if (hyperspaceShellMaterial != null)
        {
            hyperspaceShellMaterial.SetTextureScale("_FlashingTexture", new Vector2(Random.Range(1f, 3f) * RandomSign(), Random.Range(1, 3) * RandomSign()));
            hyperspaceShellMaterial.SetTextureOffset("_FlashingTexture", new Vector2(Random.Range(0f, 1f), Random.Range(0f, 1f)));

        }
    }

    private float RandomSign()
    {
        return Random.Range(0, 2) * 2f - 1f;
    }
}
