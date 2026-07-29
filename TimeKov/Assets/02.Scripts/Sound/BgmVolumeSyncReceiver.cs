using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BgmVolumeSource))]
public class BgmVolumeSyncReceiver : MonoBehaviour
{
    private AudioSource _source;

    void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.volume = GlobalSettingsManager.CurrentBGMVolume;
    }

    void OnEnable()
    {
        GlobalSettingsManager.OnBGMVolumeChanged += SetVolume;
    }

    void OnDisable()
    {
        GlobalSettingsManager.OnBGMVolumeChanged -= SetVolume;
    }

    private void SetVolume(float vol)
    {
        if (_source != null)
            _source.volume = vol;
    }
}
