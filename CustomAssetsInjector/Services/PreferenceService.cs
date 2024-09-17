using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CustomAssetsBackend.Misc;
using CustomAssetsInjector.Utils;

namespace CustomAssetsInjector.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PreferenceService.Preferences))]
[JsonSerializable(typeof(List<CachedAsset>))]
[JsonSerializable(typeof(CachedAsset))]
internal partial class PreferencesContext : JsonSerializerContext;

public static class PreferenceService
{
    public class Preferences
    {
        [JsonInclude] 
        public List<CachedAsset> AssetCache { get; set; } = new();
    }

    private static Preferences m_Preferences = new();

    private static bool m_IsInitialized;
    
    public static bool Initialize()
    {
        if (m_IsInitialized)
            return false;
        
        var preferencesPath = Path.Combine(CommonUtils.HomeAppDataPath, "preferences.json");
        try
        {
            if (!File.Exists(preferencesPath))
                return false;

            var contents = File.ReadAllText(preferencesPath);

            var prefs = JsonSerializer.Deserialize<Preferences>(contents, PreferencesContext.Default.Preferences);

            if (prefs != null)
            {
                m_Preferences = prefs;
                m_IsInitialized = true;
                return true;
            }
        }
        catch (Exception err)
        {
            Logger.Log("Preferences failed to deserialize! Settings will be reset to default.", Logger.LogLevel.Exception, err);
        }

        return false;
    }

    public static void SavePrefs()
    {
        var preferencesPath = Path.Combine(CommonUtils.HomeAppDataPath, "preferences.json");
        try
        {
            var prefs = JsonSerializer.Serialize(m_Preferences, PreferencesContext.Default.Preferences);
            
            File.WriteAllBytes(preferencesPath, Encoding.UTF8.GetBytes(prefs));
        }
        catch (Exception err)
        {
            Logger.Log("Preferences failed to serialize! Settings will not be saved.", Logger.LogLevel.Exception, err);
        }
    }

    public static Preferences GetPrefs() => m_Preferences;

    public static void SetPrefs(Preferences newPrefs)
    {
        m_Preferences = newPrefs;
    }
}