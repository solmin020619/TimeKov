using UnityEngine;

public class FlashlightController : MonoBehaviour
{
    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.T;

    [Header("References")]
    public Light flashlight;
    public AudioSource audioSource;
    public AudioClip toggleSound;

    private bool isOn = false;

    void Start()
    {
        if (flashlight != null)
        {
            isOn = false;
            flashlight.enabled = isOn;
        }
    }

    void Update()
    {
        if (!UIStateManager.GameplayInputEnabled) return;

        if (Input.GetKeyDown(toggleKey))
        {
            ToggleFlashlight();
        }
    }

    private void ToggleFlashlight()
    {
        isOn = !isOn;

        if (flashlight != null)
        {
            flashlight.enabled = isOn;
        }

        if (audioSource != null && toggleSound != null)
        {
            audioSource.PlayOneShot(toggleSound);
        }
    }
}