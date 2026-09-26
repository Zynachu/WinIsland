using iNKORE.UI.WPF.Modern;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using Application = System.Windows.Application;

namespace WinIsland.Themes
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
    }

    public class CustomTheme
    {
        public string FolderPath { get; set; }
        public ThemeMetadata Metadata { get; set; }
    }

    public static class ThemeLoader
    {
        private static CustomTheme currentTheme;
        private static readonly string ThemesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes");

        /// <summary>
        /// Discover all available themes in the Themes folder
        /// </summary>
        public static List<CustomTheme> DiscoverThemes()
        {
            var themes = new List<CustomTheme>();

            if (!Directory.Exists(ThemesPath))
            {
                Directory.CreateDirectory(ThemesPath);
                return themes;
            }

            foreach (var folder in Directory.GetDirectories(ThemesPath))
            {
                var metadataPath = Path.Combine(folder, "metadata.json");
                if (File.Exists(metadataPath))
                {
                    try
                    {
                        var json = File.ReadAllText(metadataPath);
                        var metadata = JsonConvert.DeserializeObject<ThemeMetadata>(json);

                        themes.Add(new CustomTheme
                        {
                            FolderPath = folder,
                            Metadata = metadata
                        });
                    }
                    catch (Exception ex)
                    {
                        MainWindow.logger.log($"Failed to load theme from {folder}: {ex.Message}");
                        MainWindow.logger.log($"StackTrace: {ex.StackTrace}");
                    }
                }
            }

            return themes;
        }
        public static bool LoadTheme(CustomTheme theme)
        {
            if (theme == null) return false;

            currentTheme = theme;
            var currentMode = ThemeManager.Current.ApplicationTheme;

            return ApplyThemeForMode(currentMode ?? ApplicationTheme.Dark);
        }
        public static bool ApplyThemeForMode(ApplicationTheme mode)
        {
            if (currentTheme == null) return false;

            string themeFile;

            if (currentTheme.Metadata.Unified)
            {
                themeFile = Path.Combine(currentTheme.FolderPath, "Theme.xaml");
            }
            else
            {
                if (mode == ApplicationTheme.Light && currentTheme.Metadata.HasLight)
                {
                    themeFile = Path.Combine(currentTheme.FolderPath, "Light.xaml");
                }
                else if (mode == ApplicationTheme.Dark && currentTheme.Metadata.HasDark)
                {
                    themeFile = Path.Combine(currentTheme.FolderPath, "Dark.xaml");
                }
                else
                {
                    if (currentTheme.Metadata.HasDark)
                        themeFile = Path.Combine(currentTheme.FolderPath, "Dark.xaml");
                    else if (currentTheme.Metadata.HasLight)
                        themeFile = Path.Combine(currentTheme.FolderPath, "Light.xaml");
                    else
                        return false;
                }
            }

            if (!File.Exists(themeFile))
            {
                MainWindow.logger.log($"Theme file not found: {themeFile}");
                return false;
            }

            try
            {
                //var dict = new ResourceDictionary
                //{
                //    Source = new Uri(themeFile, UriKind.Absolute)
                //};

                var oldTheme = Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source?.OriginalString.Contains("\\Themes\\") == true);
                if (oldTheme != null)
                    Application.Current.Resources.MergedDictionaries.Remove(oldTheme);

                //var customSkin = (ResourceDictionary)XamlReader.Load(dict);

                using (FileStream fs = new FileStream(themeFile, FileMode.Open, FileAccess.Read))
                {
                    // Parse as a ResourceDictionary
                    var customSkin = (ResourceDictionary)XamlReader.Load(fs);

                    Application.Current.Resources.MergedDictionaries.Add(customSkin);

                    MainWindow.logger.log($"Loaded theme: {currentTheme.Metadata.Name} ({mode} mode)");
                }
                return true;
            }
            catch (Exception ex)
            {
                MainWindow.logger.log($"Failed to apply theme: {ex.Message}");
                MainWindow.logger.log($"StackTrace: {ex.StackTrace}");
                return false;
            }
        }

        public static CustomTheme GetCurrentTheme()
        {
            return currentTheme;
        }
    }
}
