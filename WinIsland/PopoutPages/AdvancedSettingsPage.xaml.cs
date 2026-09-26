using iNKORE.UI.WPF.Modern;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WinIsland.PopoutPages
{
    /// <summary>
    /// Interaction logic for AdvancedSettingsPage.xaml
    /// </summary>
    public partial class AdvancedSettingsPage : Page
    {
        public AdvancedSettingsPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            corRadSlider.Value = Settings.instance.config.cornerRadius;
            corRadLabel.Content = Settings.instance.config.cornerRadius + "px (Def: 10px)";
            hideBatteryToggle.IsChecked = Settings.instance.config.batteryHidden;
            hideClockToggle.IsChecked = Settings.instance.config.clockHidden;

            if(ThemeManager.Current.ApplicationTheme == ApplicationTheme.Light)
                lightSelect.IsChecked = true;
            else if(ThemeManager.Current.ApplicationTheme == ApplicationTheme.Dark)
                darkSelect.IsChecked = true;

            // Load available themes into dropdown
            LoadThemeList();
        }

        private void LoadThemeList()
        {
            var themes = ThemeLoader.GetAvailableThemes();

            themeSelector.Items.Clear();

            foreach (var (folderPath, metadata) in themes)
            {
                var item = new ComboBoxItem
                {
                    Content = $"{metadata.Name} by {metadata.Author}",
                    Tag = folderPath
                };
                MainWindow.logger.logVerbose("Adding " + item.Content.ToString() + " to the styles.");
                themeSelector.Items.Add(item);

                // Select current theme
                string currentThemeFolder = "Themes/" + Settings.instance.config.currentThemeName;
                if (folderPath == currentThemeFolder)
                {
                    themeSelector.SelectedItem = item;
                }
            }
        }

        private void themeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (themeSelector.SelectedItem is ComboBoxItem item)
            {
                string themePath = item.Tag as string;
                string themeName = System.IO.Path.GetFileName(themePath);
                Settings.instance.config.currentThemeName = themeName;
                ThemeLoader.LoadTheme(themePath);
            }
        }

        private void lightSelect_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            // ThemeLoader.SwitchToMode() is called automatically via App.xaml.cs event handler
        }

        private void darkSelect_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            // ThemeLoader.SwitchToMode() is called automatically via App.xaml.cs event handler
        }

        private void corRadSlider_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {

        }

        private void corRadSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (e.OldValue != e.NewValue)
            {
                Settings.instance.config.cornerRadius = (int)e.NewValue;

                // Override the theme's corner radius
                System.Windows.Application.Current.Resources["WindowCornerRadius"] = new CornerRadius((int)e.NewValue);

                corRadLabel.Content = (int)e.NewValue + "px (Def: 10px)";
            }
        }

        private void hideBatteryToggle_Click(object sender, RoutedEventArgs e)
        {
            Settings.instance.config.batteryHidden = (bool)hideBatteryToggle.IsChecked;
            MainWindow.instance.battery.Visibility = (bool)hideBatteryToggle.IsChecked ? Visibility.Hidden : Visibility.Visible;

            if (MainWindow.instance.battery.Visibility == Visibility.Hidden)
            {
                MainWindow.instance.islandMini.Margin = new Thickness(0, 0, 15, 0);
                if (MainWindow.instance.clock.Visibility == Visibility.Hidden)
                {
                    MainWindow.instance.islandMini.Margin = new Thickness(0);
                    MainWindow.instance.islandMini.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                }
                else
                {
                    MainWindow.instance.islandMini.Margin = new Thickness(0, 0, 15, 0);
                    MainWindow.instance.islandMini.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                }
            }
            else
            {
                MainWindow.instance.islandMini.Margin = new Thickness(0, 0, 60, 0);
                MainWindow.instance.islandMini.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            }
        }

        private void hideClockToggle_Click(object sender, RoutedEventArgs e)
        {
            Settings.instance.config.clockHidden = (bool)hideClockToggle.IsChecked;
            MainWindow.instance.clock.Visibility = (bool)hideClockToggle.IsChecked ? Visibility.Hidden : Visibility.Visible;

            if (MainWindow.instance.battery.Visibility == Visibility.Hidden)
            {
                MainWindow.instance.islandMini.Margin = new Thickness(0, 0, 15, 0);
                if (MainWindow.instance.clock.Visibility == Visibility.Hidden)
                {
                    MainWindow.instance.islandMini.Margin = new Thickness(0);
                    MainWindow.instance.islandMini.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                }
                else
                {
                    MainWindow.instance.islandMini.Margin = new Thickness(0, 0, 15, 0);
                    MainWindow.instance.islandMini.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                }
            }
            else
            {
                MainWindow.instance.islandMini.Margin = new Thickness(0, 0, 60, 0);
                MainWindow.instance.islandMini.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Settings.instance.saveConfig();
        }
    }
}
