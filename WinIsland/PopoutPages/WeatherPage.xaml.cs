using iNKORE.UI.WPF.Modern.Controls;
using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinIsland.IslandPages;
using static System.Net.WebRequestMethods;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Page = System.Windows.Controls.Page;

namespace WinIsland.PopoutPages
{
    /// <summary>
    /// Interaction logic for WeatherPage.xaml
    /// </summary>
    public partial class WeatherPage : Page
    {
        public DispatcherTimer _debounceTimer = new DispatcherTimer();
        public WeatherPage()
        {
            InitializeComponent();
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _debounceTimer.Tick += DebounceTimer_Tick;

        }

        private async void getLocButton_Click(object sender, RoutedEventArgs e)
        {
            //MainWindow.logger.log("Getting location...");
            String[] temp = locationTextBox.Text.Split("|");
            ContentDialog dialog = new ContentDialog
            {
                Title = "Confirm Location",
                Content = "Is the provided location correct?\n" + temp[0],
                PrimaryButtonText = "Yea",
                SecondaryButtonText = "Nah",
                IsPrimaryButtonEnabled = true,
                IsSecondaryButtonEnabled = true
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                Settings.instance.config.city = temp[0];
                Settings.instance.config.weatherCityName = temp[0];
                temp = locationTextBox.Text.Replace(" ", "").Split("|");
                Settings.instance.config.lat = temp[1];
                Settings.instance.config.lon = temp[2];
                latLabel.Content = Settings.instance.config.lat;
                lonLabel.Content = Settings.instance.config.lon;

                if (MainWindow.instance.islandContent.Content is Weather)
                    MainWindow.instance.islandContent.Navigate(new Weather());
            }
        }

        private void locationTextBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if(args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }
        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            List<string> _temp = new List<string>();
            //locationTextBox.ItemsSource = getLocationSuggestion(locationTextBox.Text);
            foreach(locData loc in getLocationSuggestion(locationTextBox.Text))
            {
                _temp.Add(loc.display_name + $" | {loc.coords.latitude} | {loc.coords.longitude}");
            }
            locationTextBox.ItemsSource = _temp;
        }
        private List<locData> getLocationSuggestion(String input)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36");
                    string url = "https://nominatim.openstreetmap.org/search?q=" + locationTextBox.Text + "&format=jsonv2";
                    HttpResponseMessage response = client.GetAsync(url).Result;
                    MainWindow.logger.log("Getting location information from " + url);
                    string resp = response.Content.ReadAsStringAsync().Result;
                    MainWindow.logger.log("Received location information: " + resp);

                    //List<locData> locations = JsonConvert.DeserializeObject<List<locData>>(resp);
                    //MainWindow.logger.log("Received JSON");
                    //JsonNode node = JsonNode.Parse(JsonConvert.SerializeObject(locations));
                    //MainWindow.logger.log("Converted to JsonNode: \n" + JsonConvert.SerializeObject(locations));
                    bool gotData = false;

                    JArray nodes = JArray.Parse(resp);

                    List<locData> locList = new List<locData>();

                    foreach (JToken node in nodes)
                    {
                        if (!gotData)
                        {
                            string? name = node["name"]?.ToString();
                            string? displayName = node["display_name"]?.ToString();
                            double lat = node["lat"]?.Value<double>() ?? 0;
                            double lon = node["lon"]?.Value<double>() ?? 0;

                            MainWindow.logger.log($"{name}, {displayName} - {lat}, {lon}");
                            locData tempLoc = new locData
                            {
                                name = name,
                                display_name = displayName,
                                coords = new locData.Coordinates
                                {
                                    latitude = lat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                    longitude = lon.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                }
                            };
                            locList.Add(tempLoc);
                        }
                    }
                    return locList;

                }
                catch (Exception ex)
                {
                    MainWindow.logger.log("Error getting location information: " + ex.StackTrace);
                    new Thread(async () =>
                    {
                        ContentDialog errorDialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = "An error occurred while getting location information. Please try again.\nError message: " + ex.Message,
                            PrimaryButtonText = "OK",
                            IsPrimaryButtonEnabled = true
                        };
                        await errorDialog.ShowAsync();
                    }).Start();
                    return null;
                }
            }
        }
        public class locData
        {
            public string? name { get; set; }
            public string? display_name { get; set; }
            public string? country_code { get; set; }
            public Coordinates? coords { get; set; }
            public class Coordinates
            {
                public string? latitude { get; set; }
                public string? longitude { get; set; }
            }
        }
    }
}
