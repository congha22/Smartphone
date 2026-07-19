using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewValley;

namespace Smartphone
{
    public class PhoneSettingsData
    {
        public Dictionary<string, string> ComponentThemes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string CurrentPhoneBackground { get; set; } = "";
        public string CurrentPhoneLandscapeBackground { get; set; } = "";
        public string CurrentPhoneSound { get; set; } = "";
        public string CurrentPhoneTextColor { get; set; } = "";
        public string LockScreenPin1 { get; set; } = "builtin:phone";
        public string LockScreenPin2 { get; set; } = "builtin:camera";
    }
}

public class AssetHelper
{
    public const string DefaultPhoneThemeName = "default";

    private static Dictionary<string, string> componentThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "phone", DefaultPhoneThemeName },
        { "app_appstore", DefaultPhoneThemeName },
        { "app_calendar", DefaultPhoneThemeName },
        { "app_camera", DefaultPhoneThemeName },
        { "app_notification", DefaultPhoneThemeName },
        { "app_photo", DefaultPhoneThemeName },
        { "app_setting", DefaultPhoneThemeName }
    };

    public static string CurrentPhoneThemeName => GetComponentTheme("phone");

    public static string GetPhoneThemesRootPath()
    {
        return "phone_themes";
    }

    public static string GetPhoneThemeFolderPath()
    {
        return GetComponentThemeFolderPath("phone");
    }

    public static string GetComponentThemeFolderPath(string component)
    {
        return Path.Combine(GetPhoneThemesRootPath(), component, GetComponentTheme(component));
    }

    public static string GetComponentTheme(string component)
    {
        LoadSettings();
        if (componentThemes.TryGetValue(component, out string? theme))
            return theme;
        return DefaultPhoneThemeName;
    }

    public static void SetComponentTheme(string component, string theme)
    {
        componentThemes[component] = ResolveComponentThemeName(component, theme);
        SaveSettings();
    }

    public static List<string> GetAvailableThemeNamesForComponent(string component)
    {
        List<string> themeNames = new();

        var regApp = Smartphone.ModEntry.GetRegisteredPhoneAppsSnapshot().FirstOrDefault(a => a.CompositeId.Equals(component, StringComparison.OrdinalIgnoreCase));
        if (regApp != null && regApp.ThemedIconTextures != null)
        {
            themeNames.AddRange(regApp.ThemedIconTextures.Keys);
        }


        string componentRootPath = Path.Combine(GetAbsolutePhoneThemesRootPath(), component);

        if (Directory.Exists(componentRootPath))
        {
            foreach (string directoryPath in Directory.GetDirectories(componentRootPath))
            {
                string? folderName = Path.GetFileName(directoryPath);
                if (!string.IsNullOrWhiteSpace(folderName))
                    themeNames.Add(folderName);
            }
        }

        themeNames = themeNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        themeNames.RemoveAll(name => string.Equals(name, DefaultPhoneThemeName, StringComparison.OrdinalIgnoreCase));
        themeNames.Insert(0, DefaultPhoneThemeName);

        return themeNames;
    }

    public static List<string> GetAvailablePhoneThemeNames()
    {
        return GetAvailableThemeNamesForComponent("phone");
    }

    public static string ResolveComponentThemeName(string component, string? requestedThemeName)
    {
        List<string> availableThemes = GetAvailableThemeNamesForComponent(component);

        if (!string.IsNullOrWhiteSpace(requestedThemeName))
        {
            string? matchedTheme = availableThemes.FirstOrDefault(name =>
                string.Equals(name, requestedThemeName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(matchedTheme))
                return matchedTheme;
        }

        return DefaultPhoneThemeName;
    }

    public static string ResolvePhoneThemeName(string? requestedThemeName)
    {
        return ResolveComponentThemeName("phone", requestedThemeName);
    }

    public static bool SetCurrentPhoneTheme(string? requestedThemeName)
    {
        LoadSettings();
        if (!string.IsNullOrWhiteSpace(requestedThemeName) && !string.Equals(requestedThemeName, DefaultPhoneThemeName, StringComparison.OrdinalIgnoreCase))
        {
            if (componentThemes["phone"] == DefaultPhoneThemeName)
            {
                componentThemes["phone"] = ResolveComponentThemeName("phone", requestedThemeName);
                SaveSettings();
            }
        }
        return true;
    }

    private static string GetAbsolutePhoneThemesRootPath()
    {
        string? modFolderPath = Smartphone.ModEntry.Instance?.Helper?.DirectoryPath ?? Smartphone.ModEntry.SHelper?.DirectoryPath;
        if (string.IsNullOrWhiteSpace(modFolderPath))
            return GetPhoneThemesRootPath();

        return Path.Combine(modFolderPath, GetPhoneThemesRootPath());
    }

    private static bool settingsLoaded = false;
    private static string lastLoadedSave = null;

    public static void LoadSettings()
    {
        string currentSave = Smartphone.ModEntry.GetActiveSaveFolderName();
        if (settingsLoaded && lastLoadedSave == currentSave) return;

        // Reset to default value first
        Smartphone.ModEntry.currentPhoneSound = "getNewSpecialItem";
        Smartphone.ModEntry.lockScreenPin1 = "builtin:phone";
        Smartphone.ModEntry.lockScreenPin2 = "builtin:camera";

        try
        {
            string? modFolderPath = Smartphone.ModEntry.Instance?.Helper?.DirectoryPath ?? Smartphone.ModEntry.SHelper?.DirectoryPath;
            if (string.IsNullOrWhiteSpace(modFolderPath)) return;

            string path = Path.Combine(modFolderPath, Smartphone.ModEntry.GetSaveDataPath("settings.json"));
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);

                // Try parsing into the new complex settings class
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Smartphone.PhoneSettingsData>(json);

                // If the dictionary is completely empty, it might be the old flat dictionary format we just migrated
                if (data == null || (data.ComponentThemes.Count == 0 && string.IsNullOrEmpty(data.CurrentPhoneBackground)))
                {
                    try
                    {
                        var oldData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (oldData != null && oldData.ContainsKey("phone"))
                        {
                            foreach (var kv in oldData)
                                componentThemes[kv.Key] = kv.Value;
                        }
                    }
                    catch { }
                }

                if (data != null)
                {
                    if (data.ComponentThemes != null && data.ComponentThemes.Count > 0)
                    {
                        foreach (var kv in data.ComponentThemes)
                            componentThemes[kv.Key] = kv.Value;
                    }
                    if (!string.IsNullOrEmpty(data.CurrentPhoneBackground))
                        Smartphone.ModEntry.currentPhoneBackground = data.CurrentPhoneBackground;
                    if (!string.IsNullOrEmpty(data.CurrentPhoneLandscapeBackground))
                        Smartphone.ModEntry.currentPhoneLandscapeBackground = data.CurrentPhoneLandscapeBackground;
                    if (!string.IsNullOrEmpty(data.CurrentPhoneSound))
                        Smartphone.ModEntry.currentPhoneSound = data.CurrentPhoneSound;
                    else
                        Smartphone.ModEntry.currentPhoneSound = "getNewSpecialItem";
                    if (!string.IsNullOrEmpty(data.CurrentPhoneTextColor))
                        Smartphone.ModEntry.currentPhoneTextColor = data.CurrentPhoneTextColor;

                    if (!string.IsNullOrEmpty(data.LockScreenPin1))
                        Smartphone.ModEntry.lockScreenPin1 = data.LockScreenPin1;
                    else
                        Smartphone.ModEntry.lockScreenPin1 = "builtin:phone";

                    if (!string.IsNullOrEmpty(data.LockScreenPin2))
                        Smartphone.ModEntry.lockScreenPin2 = data.LockScreenPin2;
                    else
                        Smartphone.ModEntry.lockScreenPin2 = "builtin:camera";
                }
            }

            lastLoadedSave = currentSave;
            settingsLoaded = true;
        }
        catch { }
    }

    public static void SaveSettings()
    {
        try
        {
            string? modFolderPath = Smartphone.ModEntry.Instance?.Helper?.DirectoryPath ?? Smartphone.ModEntry.SHelper?.DirectoryPath;
            if (string.IsNullOrWhiteSpace(modFolderPath)) return;

            // Saves directly into ./userdata/{SaveName}/settings.json
            string path = Path.Combine(modFolderPath, Smartphone.ModEntry.GetSaveDataPath("settings.json"));
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var data = new Smartphone.PhoneSettingsData
            {
                ComponentThemes = componentThemes,
                CurrentPhoneBackground = Smartphone.ModEntry.currentPhoneBackground ?? "",
                CurrentPhoneLandscapeBackground = Smartphone.ModEntry.currentPhoneLandscapeBackground ?? "",
                CurrentPhoneSound = string.IsNullOrEmpty(Smartphone.ModEntry.currentPhoneSound) ? "getNewSpecialItem" : Smartphone.ModEntry.currentPhoneSound,
                CurrentPhoneTextColor = Smartphone.ModEntry.currentPhoneTextColor ?? "Black",
                LockScreenPin1 = Smartphone.ModEntry.lockScreenPin1 ?? "builtin:phone",
                LockScreenPin2 = Smartphone.ModEntry.lockScreenPin2 ?? "builtin:camera"
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(path, json);
        }
        catch { }
    }
}