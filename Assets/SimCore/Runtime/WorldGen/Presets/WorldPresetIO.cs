using UnityEngine;

public static class WorldPresetIO
{
    public static string CaptureSettingsJson(ScriptableObject settingsSO)
    {
        if (settingsSO == null) return string.Empty;
        return JsonUtility.ToJson(settingsSO, true);
    }

    public static void ApplySettingsJson(ScriptableObject settingsSO, string json)
    {
        if (settingsSO == null || string.IsNullOrWhiteSpace(json)) return;
        JsonUtility.FromJsonOverwrite(json, settingsSO);
    }
}
