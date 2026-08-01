using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Media;
using WinIsland.IslandPages;
using static WinIsland.IslandPages.Weather;

namespace WinIsland
{
    public class Settings
    {
        // DO NOT SAVE THESE VALUES.
        public System.Windows.Media.Color borderColor;
        public Bitmap thumbnail;
        public static Settings instance;

        // Song Data
        public long lastMaxTick = 0;
        public long lastCurTick = 0;
        public string lastDuration = "";

        public string lastArtist = "";
        public string lastSongName = "";
        public Bitmap lastThumbnail;

        // Weather Data
        public List<Weather.WeatherDataTile> lastWeatherTiles;
        public ImageSource? lastWeatherIcon = null;
        public string lastMaxTemp = "";
        public string lastMinTemp = "";
        public string lastSunrise = "";
        public string lastSunset = "";
        public string lastCityName = "";

        public SettingsConfig? config;

        string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\WI_config.cfg";

        public Settings()
        {
            instance = this;
            loadConfig();
        }
        private void loadConfig()
        {
            MainWindow.logger.log("Loading config file...");
            if (!File.Exists(path))
            {
                loadDefaultValues();
            }
            else
            {
                string temp = File.ReadAllText(path);
                config = JsonConvert.DeserializeObject<SettingsConfig>(temp);
            }
        }
        public void saveConfig()
        {
            MainWindow.logger.log("Saving config file...");
            string temp = JsonConvert.SerializeObject(config);
            File.WriteAllText(path, temp);
            MainWindow.logger.log("Saved config.");
        }
        private void loadDefaultValues()
        {
            MainWindow.logger.logCritical("Settings file invalid or missing, creating a new one...");
            config = new SettingsConfig
            {
                blurEverywhere = false,
                ambientBGBlur = 40,
                cornerRadius = 10,
                clockHidden = false,
                batteryHidden = true,
                verboseLog = false,
                lat = "-7.9797",
                lon = "112.6308",
                city = "Malang",
                weatherCityName = "Indonesia"
            };
            saveConfig();
        }
        public class SettingsConfig()
        {
            public bool blurEverywhere { get; set; }
            public bool clockHidden { get; set; }
            public bool batteryHidden { get; set; }
            public bool verboseLog { get; set; }

            public float ambientBGBlur { get; set; }

            public int cornerRadius { get; set; }
            public string lat { get; set; } = "";
            public string lon { get; set; } = "";
            public string city { get; set; } = "";
            public string weatherCityName { get; set; } = "";
        }
    }
}

