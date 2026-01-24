using UnityEngine;

public static class SettingsData
{
    public const string KEY_SENS = "settings_mouse_sens";
    public const string KEY_VOL = "settings_master_vol";

    public static float MouseSensitivity
    {
        get => PlayerPrefs.GetFloat(KEY_SENS, 1.0f);
        set { PlayerPrefs.SetFloat(KEY_SENS, value); PlayerPrefs.Save(); }
    }

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(KEY_VOL, 0.8f);
        set { PlayerPrefs.SetFloat(KEY_VOL, value); PlayerPrefs.Save(); }
    }
}
