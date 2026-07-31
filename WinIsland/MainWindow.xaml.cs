using iNKORE.UI.WPF.Modern;
using NAudio.CoreAudioApi;
using NAudio.Gui;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Navigation;
using System.Windows.Threading;
using Windows.Media.Control;
using WinIsland.IslandPages;
using WinIsland.PluginSystem;
using WinRT;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;
using static WinIsland.PInvoke;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Grid = System.Windows.Controls.Grid;
using Label = System.Windows.Controls.Label;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;
using Timer = System.Timers.Timer;

namespace WinIsland
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        double firstPos = 0;
        bool firstLaunch = false;
        bool isFirstTime = false; // Used for showing welcome screen etc.
        bool showing = false; // Is the overlay showing (small or expanded)
        bool isInTargetArea = false; // Is mouse in the target area (show island)
        bool isAnimating = false; // Is an animation playing
        bool ignoreMouseEvent = false;
        bool isExpanded = false;
        bool mouseOver = false;
        bool expandAnimFinished = false;
        bool islandLoaded = false; // Is the island fully loaded?
        bool volumeBarShown = false;
        bool smallEventShown = false;
        // Timer for events
        private DispatcherTimer mouseCheckTimer;
        private DispatcherTimer tick; // Do an event for every tick (duration is DispatcherTimer default duration)
        private DispatcherTimer waitForMedia;
        private Stopwatch ignoreVolumeEvent = new Stopwatch(); // Temporarily ignore volume event
        private Stopwatch ignoreVolumeEvent2 = new Stopwatch(); // Temporarily ignore slider event
        // Global Values
        double animDurationGlobal = 0.2D; // Change this to be customizable later.
        bool mediaSessionIsNull = false;
        // Media Player
        // Long ass name, like what the fuck? who made this shit, couldn't you just name it like GSMTC?
        public GlobalSystemMediaTransportControlsSessionManager? sessionManager; // For getting the current media session
        public GlobalSystemMediaTransportControlsSessionMediaProperties? mediaProperties; // For getting media player properties (music name, thumbnail, player state etc)
        // Volume Controls
        private static MMDeviceEnumerator enumer = new MMDeviceEnumerator();
        private MMDevice dev = enumer.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        // Settings
        Settings settings = new Settings();

        public static MainWindow instance;
        public static Logger logger = new Logger();

        PluginManager pluginManager = new PluginManager();

        public MainWindow()
        {
            logger.log("Initializing island...");
            Stopwatch initDuration = logger.startCounter();
            instance = this;
            InitializeComponent();
            logger.log("Main UI Component initialized.");

            // PluginInit.loadAll(pluginManager);

            volumeSlider.Value = (double)dev.AudioEndpointVolume.MasterVolumeLevelScalar;
            volumeSlider.ValueChanged += volumeSlider_ValueChanged;
            logger.log("Volume Slider Initialized.");

            StartMouseTracking();
            logger.log("Mouse Tracker Initialized.");
            initPagesAndPlugins();
            logger.log("Main Page Initialized (MusPlayer.xaml in source code)");
            toggleMediaControls(false);
            logger.log("Media Controls has been set to false.");
            settingsButton.IsEnabled = false;
            settingsButton.Opacity = 0;
            settingsButton.Visibility = Visibility.Hidden;
            setupEvents();
            logger.log("Events initialized.");
            systemEventSmall.Visibility = Visibility.Hidden;
            sysEventTimer.Interval = 3000;
            islandContent.Visibility = Visibility.Hidden;
            contentFrame.Visibility = Visibility.Hidden;
            logger.log("Setting UI visibility.");

            logger.log("Main Island has been initialized.");
            logger.stopCounter(initDuration, "MainWindow.Init");
            // Next stage: Window_Loaded
        }
        Timer sysEventTimer = new Timer();
        private void setupEvents()
        {
            dev.AudioEndpointVolume.OnVolumeNotification += (data) =>
            {
                Dispatcher.Invoke(new Action(() => {
                    if(ignoreVolumeEvent.ElapsedMilliseconds >= 300 || !ignoreVolumeEvent.IsRunning)
                        triggerSystemEvent(0, data.ChannelVolume.First());
                }));
            };
            logger.log("Volume event registered.");
            sysEventTimer.Elapsed += (sender, e) =>
            {
                Dispatcher.Invoke(new Action(() => {
                    if (!isExpanded) ignoreMouseEvent = false;
                    BlurEffect be = new BlurEffect();
                    be.RenderingBias = RenderingBias.Performance;
                    be.Radius = 0;
                    miniIslandGrid.Effect = be;
                    DoubleAnimation da = new DoubleAnimation
                    {
                        From = 20,
                        To = 0,
                        Duration = TimeSpan.FromSeconds(0.2)
                    };
                    DoubleAnimation hideEvent = new DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        Duration = TimeSpan.FromSeconds(0.3)
                    };
                    hideEvent.Completed += (sender, e) =>
                    {
                        systemEventSmall.Visibility = Visibility.Hidden;
                        sysEventTimer.Stop();
                        volumeBarShown = false;
                        smallEventShown = false;    
                    };
                    be.BeginAnimation(BlurEffect.RadiusProperty, da);
                    systemEventSmall.BeginAnimation(Grid.OpacityProperty, hideEvent);
                }));
            };
            ThemeManager.Current.ActualApplicationThemeChanged += (sender, e) =>
            {
                renderGradient(settings.thumbnail);
            };
        }
        private void initPagesAndPlugins()
        {
            // Load default pages
            pluginManager.registerPage(new MusPlayer());
            pluginManager.registerPage(new Weather());
            // Load plugins here for no reason at all :3
            PluginInit.loadAll(pluginManager);
            islandContent.Navigate(new MusPlayer());
        }
        private void triggerSystemEvent(int id, double volume = 0)
        {
            switch (id){
                case 0:
                    if (!ignoreVolumeEvent2.IsRunning)
                        ignoreVolumeEvent2.Start();
                    ignoreVolumeEvent2.Restart();
                    sysEventTimer.Stop();
                    logger.log("Triggered Volume Event!");
                    ignoreMouseEvent = true;
                    smallEventShown = true;
                    BlurEffect be = new BlurEffect();
                    be.RenderingBias = RenderingBias.Performance;
                    be.Radius = 0;
                    systemEventSmall.Visibility = Visibility.Visible;
                    miniIslandGrid.Effect = be;
                    if (!volumeBarShown)
                    {
                        volumeBarShown = true;
                        DoubleAnimation da = new DoubleAnimation
                        {
                            From = 0,
                            To = 20,
                            Duration = TimeSpan.FromSeconds(0.3)
                        };
                        DoubleAnimation showEvent = new DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromSeconds(0.3)
                        };
                        be.BeginAnimation(BlurEffect.RadiusProperty, da);
                        systemEventSmall.BeginAnimation(Grid.OpacityProperty, showEvent);
                    }
                    else
                    {
                        be.Radius = 20;
                    }
                    if (!showing) Window_MouseEnter();
                    volumeSlider.Value = volume;
                    sysEventTimer.Start();
                    break;
            }
        }
        public void renderGradient(Bitmap bmp, string calledby = "noone")
        {
            Stopwatch renderDuration = logger.startCounter();
            logger.log("Getting gradient... [" + calledby + "]");
            Color color = Helper.CalculateAverageColor(bmp);
            if(ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Light)
            {
                color = Helper.Lighten(color, 1f);
                gridBG.Visibility = Visibility.Collapsed;
                gridBG2.Visibility = Visibility.Collapsed;
            }
            LinearGradientBrush gradientBrush = new LinearGradientBrush(color, Color.FromArgb(0, 0, 0, 0), new Point(0.0, 1), new Point(0.5, 1));
            LinearGradientBrush gradientBrush2 = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), color, new Point(0.5, 1), new Point(1, 1));
            gridBG.Background = gradientBrush;
            gridBG2.Background = gradientBrush2;
            windowBorder.BorderBrush = new SolidColorBrush(color);
            settings.borderColor = color;
            dropShadowEffect.Color = color;
            if (settings.thumbnail != null)
                renderBackground(bmp);
            logger.stopCounter(renderDuration, "renderGradient");
        }
        public void renderBackground(Bitmap bmp)
        {
            Bitmap blurredBitmap = Helper.CreateBlurredBitmap(bmp, (int)settings.config.ambientBGBlur);
            bg.Source = Helper.ConvertToImageSource(blurredBitmap);
            bg.Effect = null;
            blurredBitmap.Dispose();
        }

        BlurEffect be = new BlurEffect { Radius = 0, RenderingBias = RenderingBias.Performance };
        BlurEffect be2 = new BlurEffect { Radius = 0, RenderingBias = RenderingBias.Performance };
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Stopwatch initCounter = logger.startCounter();
            logger.log("Attempting to get focus.");
            Focus();
            tick = new DispatcherTimer();
            tick.Tick += Tick_Tick;
            tick.Interval = new TimeSpan(0, 0, 0, 3);
            tick.Start();
            firstPos = Left;
            //MakeWindowClickThrough(false);
            logger.log("Attempting to be the top most window.");
            Topmost = true;
            Top = 0;
            ShowInTaskbar = false;
            mainContent.Effect = be;
            windowContent.Effect = be2;

            logger.log("Setting window styles...");
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
            logger.log("Window Attributes: Main window Style | WS_EX_TOOLWINDOW");

            logger.log("Reading UI settings...");

            mainWindowB.CornerRadius = new CornerRadius(settings.config.cornerRadius);
            windowBorder.CornerRadius = new CornerRadius(settings.config.cornerRadius);

            logger.log("Set the Corner Radius to " + settings.config.cornerRadius);
            logger.log("Clock is " + (settings.config.clockHidden ? "Hidden" : "Visible"));
            logger.log("Battery icon is " + (settings.config.batteryHidden ? "Hidden" : "Visible"));

            clock.Visibility = settings.config.clockHidden ? Visibility.Hidden : Visibility.Visible;
            battery.Visibility = settings.config.batteryHidden ? Visibility.Hidden : Visibility.Visible;

            if (battery.Visibility == Visibility.Hidden)
            {
                islandMini.Margin = new Thickness(0, 0, 15, 0);
                if (clock.Visibility == Visibility.Hidden)
                {
                    islandMini.Margin = new Thickness(0);
                    islandMini.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                }
                else
                {
                    islandMini.Margin = new Thickness(0, 0, 15, 0);
                    islandMini.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                }
            }
            else
            {
                islandMini.Margin = new Thickness(0, 0, 60, 0);
                islandMini.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            }

            Window_MouseLeave();
            Focus();
            islandLoaded = true;
            logger.stopCounter(initCounter, "MainWindow.Loaded");
        }

        private void Tick_Tick(object? sender, EventArgs e)
        {
            clock.Content = DateTime.Now.ToString("hh:mm tt");
            PowerStatus p = SystemInformation.PowerStatus;
            //clock.Content = batteryPercentage;
            if (p.BatteryChargeStatus == BatteryChargeStatus.NoSystemBattery)
            {
                setBatteryPercentage(200, true);
            }
            else
                if (p.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online)
                setBatteryPercentage((int)(p.BatteryLifePercent * 100), true);
            else
                setBatteryPercentage((int)(p.BatteryLifePercent * 100));

            if (sessionManager != null)
            {
                if(sessionManager.GetCurrentSession() != null)
                {
                    try
                    {
                        if (sessionManager.GetCurrentSession().GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                        {
                            //playPause.Content = "\xE102";
                            playPause2.Content = "\xE102";
                        }
                        else if (sessionManager.GetCurrentSession().GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        {
                            //playPause.Content = "\xE769";
                            playPause2.Content = "\xE769";
                        }
                    }
                    catch (NullReferenceException nfe)
                    {
                        //playPause.Content = "\xE102";
                        playPause2.Content = "\xE102";
                        logger.log("NullReferenceException");
                        logger.log(nfe.StackTrace);
                    }
                }
            }
        }
        public String[] batteryIcon = ["\xE85A", "\xE85B", "\xE85C", "\xE85D", "\xE85E", "\xE85F", "\xE860", "\xE861", "\xE862", "\xE83E", "\xEA93"];
        public String[] batteryIconCharging = [""];
        // TODO: Move this to Utils.cs with the function returning a string
        private void setBatteryPercentage(int percentage, bool isCharging = false)
        {
            int percentageL = percentage / 10;
            if (isCharging)
            {
                switch (percentageL)
                {
                    case 0:
                        battery.Content = "\xE85A";
                        break;
                    case 1:
                        battery.Content = "\xE85B";
                        break;
                    case 2:
                        battery.Content = "\xE85C";
                        break;
                    case 3:
                        battery.Content = "\xE85D";
                        break;
                    case 4:
                        battery.Content = "\xE85E";
                        break;
                    case 5:
                        battery.Content = "\xE85F";
                        break;
                    case 6:
                        battery.Content = "\xE860";
                        break;
                    case 7:
                        battery.Content = "\xE861";
                        break;
                    case 8:
                        battery.Content = "\xE862";
                        break;
                    case 9:
                        battery.Content = "\xE83E";
                        break;
                    case 10:
                        battery.Content = "\xEA93";
                        break;
                    case 20:
                        battery.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
                        battery.Content = "PC";
                        battery.FontSize = 17;
                        break;
                }
                return;
            }
            switch (percentageL)
            {
                case 0:
                    battery.Content = "\xE850";
                    break;
                case 1:
                    battery.Content = "\xE851";
                    break;
                case 2:
                    battery.Content = "\xE852";
                    break;
                case 3:
                    battery.Content = "\xE853";
                    break;
                case 4:
                    battery.Content = "\xE854";
                    break;
                case 5:
                    battery.Content = "\xE855";
                    break;
                case 6:
                    battery.Content = "\xE856";
                    break;
                case 7:
                    battery.Content = "\xE857";
                    break;
                case 8:
                    battery.Content = "\xE858";
                    break;
                case 9:
                    battery.Content = "\xE859";
                    break;
                case 10:
                    battery.Content = "\xE83F";
                    break;
            }
        }
        private void Window_MouseEnter()
        {
            if (!islandLoaded) return;
            this.Visibility = Visibility.Visible;
            isInTargetArea = true;
            showing = true;
            //bg.Visibility = Visibility.Collapsed;
            gridBG.Visibility = Visibility.Collapsed;
            gridBG2.Visibility = Visibility.Collapsed;
            //thumbnailGlow.Visibility = Visibility.Collapsed;
            double currentY = -40.0D;
            WindowTransform.BeginAnimation(TranslateTransform.YProperty, null);
            WindowTransform.Y = currentY;
            // Create an animation for the TranslateTransform
            DoubleAnimation moveUpAnimation = new DoubleAnimation
            {
                From = currentY,
                To = 0,
                Duration = TimeSpan.FromSeconds(animDurationGlobal),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            DoubleAnimation blurAnim = new DoubleAnimation
            {
                From = 20,
                To = 0,
                Duration = TimeSpan.FromSeconds(animDurationGlobal)
            };
            DoubleAnimation generic = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(animDurationGlobal)
            };
            moveUpAnimation.CurrentTimeInvalidated += (sender, e) =>
            {
                isAnimating = true;
            };

            moveUpAnimation.Completed += (sender, e) =>
            {
                isAnimating = false;
            };
            // Apply animation to the TranslateTransform
            WindowTransform.BeginAnimation(TranslateTransform.YProperty, moveUpAnimation);
            be.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
            mainContent.BeginAnimation(Grid.OpacityProperty, generic);
        }

        private void Window_MouseLeave()
        {
            isInTargetArea = false;
            showing = false;
            double currentY = 0.0D;
            WindowTransform.BeginAnimation(TranslateTransform.YProperty, null);
            WindowTransform.Y = currentY;

            // Create an animation for the TranslateTransform
            DoubleAnimation moveUpAnimation = new DoubleAnimation
            {
                From = currentY,
                To = -60,
                Duration = TimeSpan.FromSeconds(animDurationGlobal),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            DoubleAnimation blurAnim = new DoubleAnimation
            {
                From = 0,
                To = 20,
                Duration = TimeSpan.FromSeconds(animDurationGlobal)
            };
            DoubleAnimation generic = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(animDurationGlobal)
            };
            moveUpAnimation.CurrentTimeInvalidated += (sender, e) =>
            {
                isAnimating = true;
            };

            moveUpAnimation.Completed += (sender, e) =>
            {
                isAnimating = false;
                this.Visibility = Visibility.Hidden;
            };

            // Apply animation to the TranslateTransform
            WindowTransform.BeginAnimation(TranslateTransform.YProperty, moveUpAnimation);
            be.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
            mainContent.BeginAnimation(Grid.OpacityProperty, generic);
        }

        private void StartMouseTracking()
        {
            mouseCheckTimer = new DispatcherTimer();
            mouseCheckTimer.Interval = TimeSpan.FromMilliseconds(300); // Check every 100ms
            mouseCheckTimer.Tick += CheckMousePosition;
            mouseCheckTimer.Start();
        }
        private void CheckMousePosition(object sender, EventArgs e)
        {
            if (ignoreMouseEvent) return;
            POINT mousePos;
            if (GetCursorPos(out mousePos))
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                RECT windowRect;
                if (!GetWindowRect(hwnd, out windowRect))
                    return;
                bool inRange = (mousePos.X >= windowRect.Left && mousePos.X <= windowRect.Right) &&
                          (showing ? (mousePos.Y >= 0 && mousePos.Y <= ((41) + Screen.PrimaryScreen.WorkingArea.Top)) : (mousePos.Y >= 0 && mousePos.Y <= (1 + Screen.PrimaryScreen.WorkingArea.Top)));
                mouseOver = inRange;
                if (inRange && !isInTargetArea && !showing)
                {
                    isAnimating = true;
                    //if (!inRange) return;
                    Window_MouseEnter();
                }
                else if (!inRange && isInTargetArea && showing)
                {
                    Window_MouseLeave();
                }
            }
        }
        private void Window_Activated(object sender, EventArgs e)
        {

        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            ignoreMouseEvent = false;
            shrinkIsland();
        }
        private void Border_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!firstLaunch)
            {
                firstLaunch = true;
                return;
            }
            if (isExpanded)
            {
                shrinkIsland();
                return;
            }
            if (ignoreMouseEvent && !smallEventShown) return;
            expandIsland();
            //Height = 250;
            //Width = 902;
        }
        private async void AnimateWindowSize(int width, int height, int left, bool easeoutback = true, double bounceRadius = 2)
        {
            Stopwatch animDuration = MainWindow.logger.startCounter();
            isAnimating = true;
            double dpiScale = Helper.GetDpiScale(this);
            ThicknessAnimation ta;

            if (isExpanded)
            {
                ta = new ThicknessAnimation
                {
                    From = new Thickness(0, 5, 15, 0),
                    To = new Thickness(0, 5, 37, 0),
                    Duration = new TimeSpan(0, 0, 0, 0, 300),
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
                };
                settingsButton.IsEnabled = true;
                settingsButton.Visibility = Visibility.Visible;
            }
            else if (!isExpanded)
            {
                ta = new ThicknessAnimation
                {
                    From = new Thickness(0, 5, 37, 0),
                    To = new Thickness(0, 5, 15, 0),
                    Duration = new TimeSpan(0, 0, 0, 0, 300),
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
                };
                settingsButton.IsEnabled = false;
                settingsButton.Visibility = Visibility.Hidden;

            }
            else
            {
                ta = new ThicknessAnimation
                {
                    From = new Thickness(0, 5, 37, 0),
                    To = new Thickness(0, 5, 15, 0),
                    Duration = new TimeSpan(0, 0, 0, 0, 300),
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
                };
                settingsButton.IsEnabled = false;
            }
            DoubleAnimation resetOpacity = new DoubleAnimation
                {
                    From = 1,
                    To = 0.0,
                    Duration = TimeSpan.FromSeconds(0)
                };
            DoubleAnimation opacityShowAnim = new DoubleAnimation
            {
                From = 0.0,
                To = 1,
                Duration = TimeSpan.FromSeconds(animDurationGlobal)
            };
            DoubleAnimation blurIslandContentAnim = new DoubleAnimation
            {
                From = 20,
                To = 0,
                Duration = TimeSpan.FromSeconds(animDurationGlobal)
            };
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            var stopwatch = Stopwatch.StartNew();

            int duration = ((int)(animDurationGlobal*1000))+100;
            int startWidth = (int)(this.Width * dpiScale);
            int startHeight = (int)(this.Height * dpiScale);
            int startLeft = (int)(this.Left * dpiScale);

            int targetWidth = (int)(width * dpiScale);
            int targetHeight = (int)(height * dpiScale);
            int targetLeft = (int)(left * dpiScale);
            int delay = 0; // Delay between steps (ms)

            islandContent.BeginAnimation(Grid.OpacityProperty, resetOpacity);
            islandMini.BeginAnimation(Grid.OpacityProperty, resetOpacity);
            gradientBG.BeginAnimation(Grid.OpacityProperty, resetOpacity);
            settingsButton.BeginAnimation(Button.OpacityProperty, resetOpacity);
            if (settings.config.blurEverywhere)
            {
                be.BeginAnimation(BlurEffect.RadiusProperty, blurIslandContentAnim);
                be2.BeginAnimation(BlurEffect.RadiusProperty, blurIslandContentAnim);
            }
            if (!isExpanded)
                battery.BeginAnimation(Label.MarginProperty, ta);

            while (stopwatch.ElapsedMilliseconds < duration)
            {
                double t = (double)stopwatch.ElapsedMilliseconds / duration;
                double easeT = 0; // Apply easing function
                if (easeoutback)
                    easeT = Easing.EaseOutBack(t, bounceRadius);
                else
                {
                    easeT = Easing.EaseInOutCubic(t);
                }


                double newWidth = startWidth + (targetWidth - startWidth) * easeT;
                double newHeight = startHeight + (targetHeight - startHeight) * easeT;
                double newLeft = startLeft + (targetLeft - startLeft) * easeT;

                SetWindowPos(hwnd, IntPtr.Zero, (int)Math.Round(newLeft), Screen.PrimaryScreen.WorkingArea.Top, (int)Math.Round(newWidth), (int)Math.Round(newHeight), SWP_NOZORDER | SWP_NOACTIVATE);
                await Task.Delay(delay);
            }
            SetWindowPos(hwnd, IntPtr.Zero, targetLeft, Screen.PrimaryScreen.WorkingArea.Top, targetWidth, targetHeight, SWP_NOZORDER | SWP_NOACTIVATE);
            if (isExpanded)
            {
                settingsButton.BeginAnimation(Button.OpacityProperty, opacityShowAnim);
                battery.BeginAnimation(Label.MarginProperty, ta);
            }
            islandContent.BeginAnimation(Grid.OpacityProperty, opacityShowAnim);
            islandMini.BeginAnimation(Grid.OpacityProperty, opacityShowAnim);
            gradientBG.BeginAnimation(Grid.OpacityProperty, opacityShowAnim);
            isAnimating = false;
            MainWindow.logger.stopCounter(animDuration, "AnimateWindowSize");
        }
        
        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {

        }

        private async void beforeRewind_Click(object sender, RoutedEventArgs e)
        {
            await sessionManager.GetCurrentSession().TrySkipPreviousAsync();
        }

        private void playPause_Click(object sender, RoutedEventArgs e)
        {
            playPauseAsync();
        }

        private async void playPauseAsync()
        {
            Stopwatch ppDuration = logger.startCounter();
            try
            {
                mediaProperties = await sessionManager.GetCurrentSession().TryGetMediaPropertiesAsync();
                sessionManager.GetCurrentSession().TryTogglePlayPauseAsync();
                MainWindow.logger.log(String.Format("{0} - {1}", mediaProperties?.Artist, mediaProperties?.Title));
                MainWindow.logger.log($"Status: {sessionManager.GetCurrentSession().GetPlaybackInfo().PlaybackStatus}");
            }catch(NullReferenceException nfe)
            {
                toggleMediaControls(false);
                mediaSessionIsNull = true;
                logger.log("NullReferenceException");
                logger.log(nfe.StackTrace);
            }
            logger.stopCounter(ppDuration, "PlayPause");
        }
        private void expandIsland()
        {
            isExpanded = true;
            Topmost = false;
            Topmost = true;
            ignoreMouseEvent = true;
            AnimateWindowSize(872, 370, (int)firstPos - 255);
            new Thread(() =>
            {
                while (isAnimating) Thread.Sleep(100);
                this.Dispatcher.Invoke(() =>
                {
                    //bg.Visibility = Visibility.Visible;
                    islandContent.Visibility = Visibility.Visible;
                    contentFrame.Visibility = Visibility.Visible;
                    if(ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark)
                    {
                        gridBG.Visibility = Visibility.Visible;
                        gridBG2.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        gridBG.Visibility = Visibility.Collapsed;
                        gridBG2.Visibility = Visibility.Collapsed;
                    }
                    islandMini.Visibility = Visibility.Collapsed;
                });
            }).Start();
        }
        private void shrinkIsland()
        {
            isExpanded = false;
            gridBG.Visibility = Visibility.Collapsed;
            gridBG2.Visibility = Visibility.Collapsed;
            islandContent.Visibility = Visibility.Hidden;
            contentFrame.Visibility = Visibility.Hidden;
            islandMini.Visibility = Visibility.Visible;
            ignoreMouseEvent = false;
            AnimateWindowSize(351, 71, (int)firstPos, true, 1);
        }
        private void afterForward_Click(object sender, RoutedEventArgs e)
        {
            //currentSession.ControlSession.TrySkipNextAsync();
            sessionManager.GetCurrentSession().TrySkipNextAsync();
        }
        public bool mediaControl = false;
        public void toggleMediaControls(bool value, bool inAnotherThread = true)
        {
            if (inAnotherThread)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    beforeRewind2.IsEnabled = value;
                    playPause2.IsEnabled = value;
                    afterForward2.IsEnabled = value;
                    mediaControl = value;
                });
            }
            else
            {
                beforeRewind2.IsEnabled = value;
                playPause2.IsEnabled = value;
                afterForward2.IsEnabled = value;
                mediaControl = value;
            }
        }

        private void settingsButton_Click(object sender, RoutedEventArgs e)
        {
            PopoutWindow pw = new PopoutWindow();
            pw.Show();
        }
        private void volumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if( ignoreVolumeEvent2.ElapsedMilliseconds >= 300 || !ignoreVolumeEvent2.IsRunning)
            {
                if (!ignoreVolumeEvent.IsRunning)
                    ignoreVolumeEvent.Start();
                sysEventTimer.Stop();
                dev.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(volumeSlider.Value);
                ignoreVolumeEvent.Restart();
                sysEventTimer.Start();
                //triggerSystemEvent(0, volumeSlider.Value);
            }
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(WndProc);
        }
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // This does not work yet. Might try to fix later.
            const int WM_DPICHANGED = 0x02E0;

            if (msg == WM_DPICHANGED)
            {
                MessageBox.Show("We have noticed that you changed your DPI.\nPlease restart WinIsland to fix the blur or the island not in center top.", "Blurred UI-DPI Changed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return IntPtr.Zero;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            
        }
        public bool prevAnim = false;
        private void previousPageButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Somehow make this not hardcoded?
            //Navigate(islandContent.Content is MusPlayer ? new Weather() : new MusPlayer(), true);
            pluginManager.navigatePage(islandContent, true);
        }

        private void nextPageButton_Click(object sender, RoutedEventArgs e)
        {
            //Navigate(islandContent.Content is Weather ? new MusPlayer() : new Weather(), false);
            pluginManager.navigatePage(islandContent, false);
        }
        

        private void islandContent_Navigated(object sender, NavigationEventArgs e)
        {
            if (Settings.instance.config.blurEverywhere)
            {
                DoubleAnimation blur = new DoubleAnimation
                {
                    From = 20,
                    To = 0,
                    Duration = new TimeSpan(0, 0, 0, 0, 350),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                BlurEffect be = new BlurEffect();
                be.RenderingBias = RenderingBias.Performance;
                be.BeginAnimation(BlurEffect.RadiusProperty, blur);

                islandContent.Effect = be;
            }
            prevAnim = false;
        }

        private void islandContent_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            DoubleAnimation ta = new DoubleAnimation
            {
                From = prevAnim ? ActualWidth : -ActualWidth,
                To = 0,
                Duration = new TimeSpan(0, 0, 0, 0, 500),
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            };
            if (Settings.instance.config.blurEverywhere)
            {
                DoubleAnimation blur = new DoubleAnimation
                {
                    From = 20,
                    To = 0,
                    Duration = new TimeSpan(0, 0, 0, 0, 250),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                BlurEffect be = new BlurEffect();
                be.RenderingBias = RenderingBias.Performance;
                be.BeginAnimation(BlurEffect.RadiusProperty, blur);

                islandContent.Effect = be;
            }

            frameAnimation.BeginAnimation(TranslateTransform.XProperty, ta);
        }
    }
}