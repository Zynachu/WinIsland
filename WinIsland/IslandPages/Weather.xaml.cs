using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net.Http;
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
using static WinIsland.Settings;

namespace WinIsland.IslandPages
{
    /// <summary>
    /// Interaction logic for Weather.xaml
    /// </summary>
    public partial class Weather : Page
    {
        public Weather()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            WeatherListView.Items.Clear();
            if (Settings.instance.lastWeatherTiles != null)
            {
                waitLabel.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                waitLabel.Margin = new Thickness(0, 0, 0, 25);
                waitLabel.Content = "Refreshing weather information...";
                waitLabel.Visibility = Visibility.Hidden;
                foreach(WeatherDataTile tile in Settings.instance.lastWeatherTiles)
                    WeatherListView.Items.Add(tile);
                cityName.Content = Settings.instance.lastCityName;
                curWeatherIcon.Source = Settings.instance.lastWeatherIcon;
                curTempMax.Content = Settings.instance.lastMaxTemp;
                curTempMin.Content = Settings.instance.lastMinTemp;
                curSunrise.Content = Settings.instance.lastSunrise;
                curSunset.Content = Settings.instance.lastSunset;
            }
            else
            {
                waitLabel.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                waitLabel.Margin = new Thickness(0, 0, 0, 0);
                waitLabel.Visibility = Visibility.Visible;
                waitLabel.Content = "Loading weather information...";
            }
            fetchWeather();
        }
        private void fetchWeather()
        {
            new Thread(async () =>
            {
                Dispatcher.Invoke(() =>
                {
                    MainWindow.instance.busyRing.Visibility = Visibility.Visible;
                });
                using (HttpClient client = new HttpClient())
                {
                    try
                    {
                        // The location are hardcoded for testing purposes (Malang, Indonesia)
                        // TODO: Make the user be able to select their own city/country.
                        string url = "https://api.open-meteo.com/v1/forecast?latitude=" + Settings.instance.config.lat + "&longitude=" + Settings.instance.config.lon +
                        "&daily=weather_code,temperature_2m_max,temperature_2m_min,uv_index_clear_sky_max,sunrise,sunset,apparent_temperature_min,apparent_temperature_max" +
                        "&current=temperature_2m,is_day,weather_code,apparent_temperature" +
                        "&timezone=auto";
                        Dispatcher.Invoke(() =>
                        {
                            waitLabel.Content = "Loading weather information...\nDownloading weather data...";
                        });
                        HttpResponseMessage response = await client.GetAsync(url);
                        MainWindow.logger.log("Getting weather information from " + url);
                        string resp = await response.Content.ReadAsStringAsync();
                        MainWindow.logger.log("Received weather information: " + resp);

                        MainWindow.logger.log("Attempting to make JSON...");
                        JsonNode node = JsonNode.Parse(resp);
                        
                        Dispatcher.Invoke(() =>
                        {
                            waitLabel.Content = "Loading weather information...\nParsing weather data...";
                        });

                        MainWindow.logger.log("Converted to JsonNode successfully!");
                        if (resp.Contains("\"reason\":"))
                        {
							MainWindow.logger.logCritical("Failed !");
                            MainWindow.logger.logCritical(node["reason"].ToString());

                            Dispatcher.Invoke(() =>
                            {
                                waitLabel.Content = "Failed to get weather data!\nCheck logs for more information.";
                            });
                            
                            return;
						}
                        parseWeather(node);
                        Dispatcher.Invoke(() =>
                        {
                            waitLabel.Visibility = Visibility.Hidden;
                        });
                    }
                    catch(Exception err)
                    {
                        MainWindow.logger.logCritical("Failed !");
                        MainWindow.logger.logCritical(err.Message);
                        MainWindow.logger.logCritical(err.StackTrace);
                    }
                }
                Dispatcher.Invoke(() =>
                {
                    MainWindow.instance.busyRing.Visibility = Visibility.Collapsed;
                });
            }).Start();
        }
        private void parseWeather(JsonNode node)
        {
            MainWindow.logger.logVerbose("Reading Icon Json Data...");
            string jsonIconPath = AppContext.BaseDirectory + "\\Assets\\weather.json";
            string iconJsonData = File.ReadAllText(jsonIconPath);
            JsonNode icons = JsonNode.Parse(iconJsonData);
            MainWindow.logger.log("Succesfully read Icon Json Data.");

            MainWindow.logger.log("Parsing JsonNode");
			double elevationD = node["elevation"].GetValue<Double>();
            daily daily = JsonConvert.DeserializeObject<daily>(node["daily"].ToJsonString());
			current current = JsonConvert.DeserializeObject<current>(node["current"].ToJsonString());
			MainWindow.logger.log("Parsed succesfully!");

            /**
             * ===================================
             *      Today Weather Data Info
             * ===================================
             **/

            Dispatcher.Invoke(() =>
            {
                curWeatherIcon.Source = Helper.ConvertToImageSource(Helper.getImageFromUrl(icons[daily.weather_code[0]]["day"]["image"].ToString(), ImageFormat.Png));
                curDay.Content = "Today";
                curTempMax.Content = (int)daily.temperature_2m_max[0] + "° Max";
                curTempMin.Content = (int)daily.temperature_2m_min[0] + "° Min";
                string[] sunrise = daily.sunrise[0].Split("T");
                string[] sunset = daily.sunset[0].Split("T");
                curSunrise.Content = sunrise[1];
                curSunset.Content = sunset[1];
                cityName.Content = Settings.instance.config.weatherCityName;

                Settings.instance.lastCityName = (string)cityName.Content;
                Settings.instance.lastWeatherIcon = curWeatherIcon.Source;
                Settings.instance.lastMaxTemp = (string)curTempMax.Content;
                Settings.instance.lastMinTemp = (string)curTempMin.Content;
                Settings.instance.lastSunrise = (string)curSunrise.Content;
                Settings.instance.lastSunset = (string)curSunset.Content;
            });

            /**
             * ===================================
             *      Weekly Weather Data Info
             * ===================================
             **/

            List<WeatherDataTile> weatherTiles = new List<WeatherDataTile>();

            for(int i = 0; i <= 6; i++)
            {
                if (i == 0) continue;
                WeatherDataTile temp = new WeatherDataTile();
                string dateString = daily.time[i];
                DayOfWeek day = GetDayOfWeekFromDateString(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                temp.dayOfWeek = day.ToString();
                //temp.imageURL = "";
                temp.image = Helper.ConvertToImageSource(Helper.getImageFromUrl(icons[daily.weather_code[i]]["day"]["image"].ToString(), ImageFormat.Png));
                MainWindow.logger.log("Downloading weather icons for code " + daily.weather_code[i] + " from " + icons[daily.weather_code[i]]["day"]["image"].ToString());
                
                Dispatcher.Invoke(() =>
                {
                    waitLabel.Content = "Loading weather information...\nDownloading weather icons for code " + daily.weather_code[i] + " from " + icons[daily.weather_code[i]]["day"]["image"].ToString();
                });

                temp.weatherCode = daily.weather_code[i];
                temp.tmax = (int)daily.temperature_2m_max[i] + "°";
                temp.tmin = (int)daily.temperature_2m_min[i] + "°";
                //string[] sunrise = daily.sunrise[i].Split("T");
                //string[] sunset = daily.sunset[i].Split("T");
                //temp.sunrise = sunrise[1];
				//temp.sunset = sunset[1];
                weatherTiles.Add(temp);
            }
            Settings.instance.lastWeatherTiles = weatherTiles;
            Dispatcher.Invoke(() =>
            {
                WeatherListView.Items.Clear();
            });
            int j = 0;
            foreach (WeatherDataTile tile in weatherTiles)
            {
                MainWindow.logger.log("Tile " + j + " has these data.");
                MainWindow.logger.log("Weather Code: " + tile.weatherCode);
                MainWindow.logger.log("Max Temp: " + tile.tmax);
				MainWindow.logger.log("Min Temp: " + tile.tmin);
				MainWindow.logger.log("Sunrise: " + tile.sunrise);
				MainWindow.logger.log("Sunset: " + tile.sunset);
                j++;

                Dispatcher.Invoke(() =>
                {
                    WeatherListView.Items.Add(tile);
                });
			}
        }
        public DayOfWeek GetDayOfWeekFromDateString(string dateString, string format, CultureInfo cultureInfo)
        {
            DateTime dateValue;
            // Use TryParseExact for safer parsing to avoid exceptions if the format is incorrect
            if (DateTime.TryParseExact(dateString, format, cultureInfo, DateTimeStyles.None, out dateValue))
            {
                return dateValue.DayOfWeek;
            }
            else
            {
                // Handle the error case (e.g., throw an exception, return a default value, or show an error in UI)
                throw new FormatException($"Unable to convert {dateString} to a valid date using format {format}.");
            }
        }
        public class WeatherDataTile
        {
            public string weatherCode { get; set; }
            public BitmapImage image { get; set;}
            public string dayOfWeek { get; set; }
            public string tmax { get; set; }
			public string tmin { get; set; }
            public string sunrise { get; set; }
            public string sunset { get; set; }
		}
        public class daily
        {
            public string[] time { get; set; }
            public string[] weather_code { get; set; }
            public double[] temperature_2m_max { get; set; }
			public double[] temperature_2m_min { get; set; }
            public double[] uv_index_clear_sky_max { get; set; }
			public string[] sunrise { get; set; }
			public string[] sunset { get; set; }
		}

		public class current
		{
			public string time { get; set; }
			public double interval { get; set; }
			public double temperature_2m { get; set; }
			public double is_day { get; set; }
			public double weather_code { get; set; }
		}
	}
}

