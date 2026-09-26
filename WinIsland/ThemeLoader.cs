using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using iNKORE.UI.WPF.Modern;
using Newtonsoft.Json;
using Application = System.Windows.Application;

namespace WinIsland
{
    public class ThemeMetadata
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public bool Unified { get; set; }
        public bool HasLight { get; set; }
        public bool HasDark { get; set; }

        public static ThemeMetadata Load(string folderPath)
        {
            string metadataPath = Path.Combine(folderPath, "metadata.json");
            if (!File.Exists(metadataPath))
            {
                // Default metadata if missing
                return new ThemeMetadata
                {
                    Name = Path.GetFileName(folderPath),
                    Author = "Unknown",
                    Version = "1.0",
                    Unified = false,
                    HasLight = File.Exists(Path.Combine(folderPath, "Light.xaml")),
                    HasDark = File.Exists(Path.Combine(folderPath, "Dark.xaml"))
                };
            }

            string json = File.ReadAllText(metadataPath);
            return JsonConvert.DeserializeObject<ThemeMetadata>(json);
        }
    }

    public static class ThemeLoader
    {
        private static string _currentThemeFolder = "Themes/DefaultTheme";
        private static ResourceDictionary _currentThemeDict;

        public static string CurrentThemeFolder => _currentThemeFolder;

        /// <summary>
        /// Load a theme based on the current ApplicationTheme (Light/Dark)
        /// </summary>
        public static void LoadTheme(string themeFolderPath)
        {
            _currentThemeFolder = themeFolderPath;

            string themePath = GetThemePathForCurrentMode(themeFolderPath);
            ApplyTheme(themePath);
        }

        /// <summary>
        /// Switch between Light and Dark variants of the current theme
        /// Called when user toggles light/dark mode
        /// </summary>
        public static void SwitchToMode(ApplicationTheme? mode)
        {
            string themePath = GetThemePathForMode(_currentThemeFolder, mode ?? ApplicationTheme.Dark);
            ApplyTheme(themePath);
        }

        /// <summary>
        /// Get all available themes from the Themes directory
        /// </summary>
        public static List<(string FolderPath, ThemeMetadata Metadata)> GetAvailableThemes()
        {
            var themes = new List<(string, ThemeMetadata)>();
            string themesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes");

            if (!Directory.Exists(themesDir))
            {
                Directory.CreateDirectory(themesDir);
                return themes;
            }

            foreach (var dir in Directory.GetDirectories(themesDir))
            {
                try
                {
                    var metadata = ThemeMetadata.Load(dir);
                    string relativePath = "Themes/" + Path.GetFileName(dir);
                    themes.Add((relativePath, metadata));
                }
                catch (Exception ex)
                {
                    MainWindow.logger?.log($"Failed to load theme from {dir}: {ex.Message}");
                }
            }

            return themes;
        }

        private static string GetThemePathForCurrentMode(string themeFolderPath)
        {
            return GetThemePathForMode(themeFolderPath, ThemeManager.Current.ApplicationTheme ?? ApplicationTheme.Dark);
        }

        private static string GetThemePathForMode(string themeFolderPath, ApplicationTheme mode)
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, themeFolderPath);
            var metadata = ThemeMetadata.Load(fullPath);

            // Unified theme - use Theme.xaml regardless of mode
            if (metadata.Unified)
            {
                string unifiedPath = Path.Combine(themeFolderPath, "Theme.xaml");
                if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, unifiedPath)))
                    return unifiedPath;
            }

            // Mode-specific theme
            if (mode == ApplicationTheme.Light)
            {
                string lightPath = Path.Combine(themeFolderPath, "Light.xaml");
                if (metadata.HasLight && File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, lightPath)))
                    return lightPath;

                // Fallback to Dark if Light doesn't exist
                MainWindow.logger?.log($"Light theme not found, falling back to Dark");
                return Path.Combine(themeFolderPath, "Dark.xaml");
            }
            else
            {
                string darkPath = Path.Combine(themeFolderPath, "Dark.xaml");
                if (metadata.HasDark && File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, darkPath)))
                    return darkPath;

                // Fallback to Light if Dark doesn't exist
                MainWindow.logger?.log($"Dark theme not found, falling back to Light");
                return Path.Combine(themeFolderPath, "Light.xaml");
            }
        }

        private static void ApplyTheme(string themePath)
        {
            try
            {
                // Remove old theme dictionary
                if (_currentThemeDict != null && System.Windows.Application.Current.Resources.MergedDictionaries.Contains(_currentThemeDict))
                {
                    MainWindow.logger?.logVerbose($"Removing old theme.");
                    System.Windows.Application.Current.Resources.MergedDictionaries.Remove(_currentThemeDict);
                }

                // Load new theme
                using (FileStream fs = new FileStream(themePath, FileMode.Open, FileAccess.Read))
                {
                    // Parse as a ResourceDictionary
                    var customSkin = (ResourceDictionary)XamlReader.Load(fs);

                    Application.Current.Resources.MergedDictionaries.Add(customSkin);
                    _currentThemeDict = customSkin;
                }

                MainWindow.logger?.log($"Theme loaded: {themePath}");
            }
            catch (Exception ex)
            {
                MainWindow.logger?.logCritical($"Failed to load theme {themePath}: {ex.Message}");
                MainWindow.logger.log($"StackTrace: {ex.StackTrace}");
            }
        }
    }
}
