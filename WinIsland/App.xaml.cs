using iNKORE.UI.WPF.Modern;
using System;
using System.Windows;
using Application = System.Windows.Application;

namespace WinIsland
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load settings
            var settings = new Settings();

            // Load saved theme (or default)
            string themeFolder = "Themes/" + settings.config.currentThemeName;
            ThemeLoader.LoadTheme(themeFolder);

            // Listen for light/dark mode changes
            ThemeManager.Current.ActualApplicationThemeChanged += OnApplicationThemeChanged;

            // Create and show MainWindow
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private void OnApplicationThemeChanged(ThemeManager sender, object e)
        {
            // When user toggles light/dark, switch to the appropriate variant
            ThemeLoader.SwitchToMode(sender.ApplicationTheme);
        }
    }
}
