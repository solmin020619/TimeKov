// =====================================================================
// SettingsData.cs
// 설정값 묶음 — JSON으로 Application.persistentDataPath/settings.json 에 저장.
// GlobalSettingsManager가 로드/저장 주체. 다른 곳에서는 직접 건드리지 말 것.
// =====================================================================

using System;
using System.IO;
using UnityEngine;

[Serializable]
public class KeyBindingData
{
    public KeyCode jump      = KeyCode.Space;
    public KeyCode skill1    = KeyCode.Q;
    public KeyCode skill2    = KeyCode.E;
    public KeyCode skill3    = KeyCode.R;
    public KeyCode interact  = KeyCode.F;
    public KeyCode instant   = KeyCode.G;
    public KeyCode quickSlot = KeyCode.V;
}

[Serializable]
public class SettingsData
{
    public float masterVolume = 1f;
    public float bgmVolume    = 1f;
    public float sfxVolume    = 1f;
    public float sensitivity  = 1f;

    public int  resolutionWidth;
    public int  resolutionHeight;
    public bool fullscreen = true;

    public int qualityLevel;        // QualitySettings.SetQualityLevel
    public int shadowQualityLevel;  // 0=낮음 1=보통 2=높음 3=매우높음
    public int textureQualityLevel; // 0=매우높음 1=높음 2=보통 3=낮음 (globalTextureMipmapLimit)

    public KeyBindingData keyBindings = new KeyBindingData();

    private static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

    public static SettingsData Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<SettingsData>(json);
                if (data != null)
                {
                    if (data.keyBindings == null) data.keyBindings = new KeyBindingData();
                    return data;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SettingsData] 로드 실패, 기본값 사용: {e.Message}");
        }
        return CreateDefault();
    }

    public void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(this, true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SettingsData] 저장 실패: {e.Message}");
        }
    }

    private static SettingsData CreateDefault()
    {
        var res = Screen.currentResolution;
        return new SettingsData
        {
            resolutionWidth  = res.width,
            resolutionHeight = res.height,
            fullscreen       = true,
            qualityLevel     = QualitySettings.GetQualityLevel(),
            shadowQualityLevel  = 2,
            textureQualityLevel = 0,
            keyBindings = new KeyBindingData()
        };
    }
}
