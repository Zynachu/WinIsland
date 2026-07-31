using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Media.Playback;
using WinIsland.Properties;

namespace WinIsland.IslandPages
{
    /// <summary>
    /// Interaction logic for MusPlayer.xaml
    /// </summary>
    public partial class MusPlayer : Page
    {
        private bool mediaSessionEmpty = true; // Check if mediaSession is empty or equal to NULL.
        private bool sliderChangeIgnore = false;
        bool animatedCoverArt = false;

        private DispatcherTimer waitForMD = new DispatcherTimer();
        private DispatcherTimer Tick = new DispatcherTimer();

        private MainWindow mw = MainWindow.instance;
        public MusPlayer()
        {
            InitializeComponent();
            if(Settings.instance.lastThumbnail != null)
            {
                songTitle.Text = Settings.instance.lastSongName;
                songArtist.Text = Settings.instance.lastArtist;
                songThumbnail.Source = Helper.ConvertToImageSource(Settings.instance.lastThumbnail);

                sliderChangeIgnore = true;
                songProgress.Maximum = Settings.instance.lastMaxTick;
                songProgress.Value = Settings.instance.lastCurTick;
                songProgressLabel.Content = Settings.instance.lastDuration;
                sliderChangeIgnore = false; 

            }
            MainWindow.instance.busyRing.Visibility = Visibility.Visible;
            getMediaSession();
            Tick.Interval = new TimeSpan(0, 0, 0, 1);
            Tick.Tick += (e, a) =>
            {
                if (mw.sessionManager != null)
                {
                    if (mw.sessionManager.GetCurrentSession() != null)
                    {
                        try
                        {
                            if (mw.sessionManager.GetCurrentSession().GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                            {
                                toggleMediaControls(true);
                                playPause.Content = "\xE102";
                            }
                            else if (mw.sessionManager.GetCurrentSession().GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                            {
                                toggleMediaControls(true);
                                playPause.Content = "\xE769";
                            }
                        }
                        catch (NullReferenceException nfe)
                        {
                            playPause.Content = "\xE102";
                            MainWindow.logger.log("NullReferenceException");
                            MainWindow.logger.log(nfe.StackTrace);
                        }
                    }
                }
            };
            Tick.Start();
            DoubleAnimation animation = new DoubleAnimation
            {
                From = -10,
                To = 10,
                Duration = TimeSpan.FromSeconds(2),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            // 3. Begin the animation on the TranslateTransform's X property
            if(animatedCoverArt) floatAnim.BeginAnimation(TranslateTransform.YProperty, animation);
        }
        public void getMediaSession()
        {
            new Thread(startGetSessionThread).Start();
        }
        private async void startGetSessionThread()
        {
            MainWindow.logger.log("Getting media session...");
            Dispatcher.Invoke(() =>
            {
                MainWindow.instance.busyRing.Visibility = Visibility.Visible;
            });
            Task.Delay(1000).Wait();
            mw.sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if(mw.sessionManager == null)
            {
                mediaSessionEmpty = true;
                waitForMD.Interval = new TimeSpan(0, 0, 1);
                waitForMD.Tick += new EventHandler(async delegate (Object o, EventArgs args)
                {
                    MainWindow.logger.log("MediaSession is NULL!\nAttempting to look for one...");
                    if(mediaSessionEmpty != null)
                    {
                        waitForMD.Stop();
                        return;
                    }
                    MainWindow.logger.log("Requesting Session manager...");
                    mw.sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                    if (mw.sessionManager == null) {
                        MainWindow.logger.log("Unable to get session manager!");
                        MainWindow.logger.log("Reason: Session manager is NULL!");
                        return;
                    }
                    MainWindow.logger.log("Got session manager!");
                    MainWindow.logger.log("Setting up Session Manager events...");
                    mw.sessionManager.SessionsChanged += SessionManager_SessionsChanged;
                    mw.sessionManager.CurrentSessionChanged += SessionManager_CurrentSessionChanged;
                    mw.sessionManager.GetCurrentSession().PlaybackInfoChanged += MainWindow_PlaybackInfoChanged;
                    mw.sessionManager.GetCurrentSession().MediaPropertiesChanged += MainWindow_MediaPropertiesChanged;
                    mw.sessionManager.GetCurrentSession().TimelinePropertiesChanged += MainWindow_TimelinePropertiesChanged;
                    // Setup sessionManager events
                    // TODO: Set events for sessionManager
                    mediaSessionEmpty = false;
                    getMusicInfo(mw.sessionManager.GetCurrentSession(), "startGetSessionThread | onTick");
                });
                waitForMD.Start();
                return;
            }
            try
            {
                mw.sessionManager.SessionsChanged += SessionManager_SessionsChanged;
                mw.sessionManager.CurrentSessionChanged += SessionManager_CurrentSessionChanged;
                if(mw.sessionManager.GetCurrentSession() != null)
                {
                    mw.sessionManager.GetCurrentSession().PlaybackInfoChanged += MainWindow_PlaybackInfoChanged;
                    mw.sessionManager.GetCurrentSession().MediaPropertiesChanged += MainWindow_MediaPropertiesChanged;
                    mw.sessionManager.GetCurrentSession().TimelinePropertiesChanged += MainWindow_TimelinePropertiesChanged;
                }

                getMusicInfo(mw.sessionManager.GetCurrentSession(), "startGetSessionThread | onTry");
            }
            catch (NullReferenceException nfe)
            {
                // it happens, dont ask how.
                toggleMediaControls(false);
                mediaSessionEmpty = true;
                getMediaSession();
                MainWindow.logger.log("NullReferenceException");
                MainWindow.logger.log(nfe.StackTrace);
            }
            Dispatcher.Invoke(() =>
            {
                MainWindow.instance.busyRing.Visibility = Visibility.Collapsed;
            });
        }
        private async void playPauseAsync()
        {
            Dispatcher.Invoke(() =>
            {
                MainWindow.instance.busyRing.Visibility = Visibility.Visible;
            });
            try
            {
                mw.mediaProperties = await mw.sessionManager.GetCurrentSession().TryGetMediaPropertiesAsync();
                mw.sessionManager.GetCurrentSession().TryTogglePlayPauseAsync();
                MainWindow.logger.log(string.Format("{0} - {1}", mw.mediaProperties?.Artist, mw.mediaProperties?.Title));
                MainWindow.logger.log($"Status: {mw.sessionManager.GetCurrentSession().GetPlaybackInfo().PlaybackStatus}");
                await this.Dispatcher.Invoke(async () =>
                {
                    //var songInfo = await mw.sessionManager.GetCurrentSession().TryGetMediaPropertiesAsync();
                    //songTitle.Text = songInfo.Title;
                    //songArtist.Text = songInfo.Artist;
                    //songThumbnail.Source = Helper.GetThumbnail(songInfo.Thumbnail);
                    //if (Helper.GetBitmap(songInfo.Thumbnail) != null)
                    //    mw.renderGradient(Helper.GetBitmap(songInfo.Thumbnail));
                    toggleMediaControls(true);
                });
            }
            catch (NullReferenceException nfe)
            {
                toggleMediaControls(false);
                mediaSessionEmpty = true;
                getMediaSession();
                MainWindow.logger.log("NullReferenceException");
                MainWindow.logger.log(nfe.StackTrace);
            }
            Dispatcher.Invoke(() =>
            {
                MainWindow.instance.busyRing.Visibility = Visibility.Collapsed;
            });
        }
        private void toggleMediaControls(bool value, bool inAnotherThread = true)
        {
            if (inAnotherThread)
            {
                this.Dispatcher.Invoke(() =>
                {
                    beforeRewind.IsEnabled = value;
                    playPause.IsEnabled = value;
                    afterForward.IsEnabled = value;
                    mw.toggleMediaControls(value, inAnotherThread);
                });
                
            }
            else
            {
                beforeRewind.IsEnabled = value;
                playPause.IsEnabled = value;
                afterForward.IsEnabled = value;
                mw.toggleMediaControls(value, inAnotherThread);
            }
        }
        // GSMTC Events
        private async void SessionManager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            if(sender.GetCurrentSession() != null)
            {
                sender.GetCurrentSession().PlaybackInfoChanged += MainWindow_PlaybackInfoChanged;
                sender.GetCurrentSession().MediaPropertiesChanged += MainWindow_MediaPropertiesChanged;
                sender.GetCurrentSession().TimelinePropertiesChanged += MainWindow_TimelinePropertiesChanged;
                mediaSessionEmpty = false;
                getMusicInfo(sender.GetCurrentSession(), "SessionManager_CurrentSessionChanged");
            }

            try
            {
                mw.mediaProperties = await sender.GetCurrentSession().TryGetMediaPropertiesAsync();
                toggleMediaControls(true, true);
            }
            catch (NullReferenceException nfe)
            {
                toggleMediaControls(false);
                mediaSessionEmpty = true;
                getMediaSession();
                this.Dispatcher.Invoke(() =>
                {
                    songTitle.Text = "No media playing.";
                    songArtist.Text = "WinIsland by Charamellized.";
                    songThumbnail.Source = null;
                    toggleMediaControls(false);
                });
                MainWindow.logger.log("NullReferenceException");
                MainWindow.logger.log(nfe.StackTrace);
            }
            catch(COMException ce)
            {
                // bruhhh
            }
        }

        private async void SessionManager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            try
            {
                getMusicInfo(sender.GetCurrentSession(), "SessionManager_SessionChanged");
            }
            catch (NullReferenceException nfe)
            {
                toggleMediaControls(false);
                mediaSessionEmpty = true;
                getMediaSession();
                MainWindow.logger.log("NullReferenceException");
                MainWindow.logger.log(nfe.StackTrace);
            }
            catch
            {
                // COM Crash fuck off
            }
        }
        private async void MainWindow_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            // Logging this using the regular logger is NOT a good idea because it can fill up the user's drive with useless logs in the log file.
            //MainWindow.logger.log(sender.GetTimelineProperties().Position.ToString() + "/" + sender.GetTimelineProperties().MaxSeekTime.ToString());
            Dispatcher.Invoke(() =>
            {
                sliderChangeIgnore = true;
                songProgress.Maximum = sender.GetTimelineProperties().MaxSeekTime.Ticks;
                songProgress.Value = sender.GetTimelineProperties().Position.Ticks;
                songProgressLabel.Content = sender.GetTimelineProperties().Position.ToString(@"hh\:mm\:ss") + " / " + sender.GetTimelineProperties().MaxSeekTime.ToString(@"hh\:mm\:ss");
                
                Settings.instance.lastMaxTick = sender.GetTimelineProperties().MaxSeekTime.Ticks;
                Settings.instance.lastCurTick = sender.GetTimelineProperties().Position.Ticks;
                Settings.instance.lastDuration = sender.GetTimelineProperties().Position.ToString(@"hh\:mm\:ss") + " / " + sender.GetTimelineProperties().MaxSeekTime.ToString(@"hh\:mm\:ss");

                sliderChangeIgnore = false;
            });
        }
        private async void MainWindow_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            MainWindow.logger.log("MainWindow_MediaPropertiesChanged Event Called");
            getMusicInfo(sender, "MainWindow_MediaPropertiesChanged");
        }

        private async void MainWindow_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            MainWindow.logger.log("MainWindow_PlaybackInfoChanged Event Called");
            try
            {
                getMusicInfo(sender, "MainWindow_PlaybackInfoChanged");
            }
            catch
            {
                this.Dispatcher.Invoke(() =>
                {
                    songTitle.Text = "No media playing.";
                    songArtist.Text = "WinIsland by Charamellized.";
                    songThumbnail.Source = null;
                    toggleMediaControls(false);
                });
            }

        }
        private async void getMusicInfo(GlobalSystemMediaTransportControlsSession sender, string calledby = "noone")
        {
            Dispatcher.Invoke(() =>
            {
                MainWindow.instance.busyRing.Visibility = Visibility.Visible;
            });
            MainWindow.logger.log("Attempting to get Music Information from Session...");
            try
            {
                var songInfo = await sender.TryGetMediaPropertiesAsync();
                if (songInfo == null) {
                    MainWindow.logger.log("songInfo is null, aborting...");
                    return; 
                };
                MainWindow.logger.log("songInfo is NOT null, continuing...");
                this.Dispatcher.Invoke(() =>
                {
                    songTitle.Text = songInfo.Title;
                    songArtist.Text = songInfo.Artist.IsWhiteSpace() ? songInfo.AlbumArtist : songInfo.Artist;
                    songThumbnail.Source = Helper.GetThumbnail(songInfo.Thumbnail);
                    Settings.instance.thumbnail = Helper.GetBitmap(songInfo.Thumbnail);
                    Settings.instance.lastThumbnail = Helper.GetBitmap(songInfo.Thumbnail);
                    Settings.instance.lastArtist = songInfo.Artist.IsWhiteSpace() ? songInfo.AlbumArtist : songInfo.Artist;
                    Settings.instance.lastSongName = songInfo.Title;
                    if (Helper.GetBitmap(songInfo.Thumbnail) != null)
                    {
                        mw.renderGradient(Settings.instance.thumbnail, "getMusicInfo | " + calledby);
                    }
                    MainWindow.logger.log("Now Playing: " + songInfo.Title);
                    MainWindow.logger.log("Artist: " + (songInfo.Artist.IsWhiteSpace() ? songInfo.AlbumArtist : songInfo.Artist));

                    toggleMediaControls(true);
                });
            }
            catch(NullReferenceException nuEx){
                this.Dispatcher.Invoke(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        sliderChangeIgnore = true;
                        songProgress.Maximum = 0;
                        songProgress.Value = 0;
                        songProgressLabel.Content = "00:00 / 00:00";

                        Settings.instance.lastMaxTick = 0;
                        Settings.instance.lastCurTick = 0;
                        Settings.instance.lastDuration =  "00:00 / 00:00";

                        sliderChangeIgnore = false;
                    });

                    songTitle.Text = "No media playing.";
                    songArtist.Text = "WinIsland by Charamellized.";
                    songThumbnail.Source = null;
                    toggleMediaControls(false);
                });
            }
            catch(COMException comEx)
            {

            }
            Dispatcher.Invoke(() =>
            {
                MainWindow.instance.busyRing.Visibility = Visibility.Hidden;
            });
        }
        // Button Events
        private async void beforeRewind_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await mw.sessionManager.GetCurrentSession().TrySkipPreviousAsync();
            }
            catch (Exception ex)
            {

            }
        }

        private void playPause_Click(object sender, RoutedEventArgs e)
        {
            playPauseAsync();
        }
        private async void afterForward_Click(object sender, RoutedEventArgs e)
        {
            //currentSession.ControlSession.TrySkipNextAsync();
            try
            {
                await mw.sessionManager.GetCurrentSession().TrySkipNextAsync();
            }
            catch (Exception ex)
            {

            }
        }

        private async void songProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mw.sessionManager != null && !sliderChangeIgnore && e.NewValue != e.OldValue)
                // Some programs does not support this!
                // Do not accept bugs related to applications not changing their timeline with TryChangePlaybackPositionAsync.
                // This is because it is NOT related to this app and this function is tested to work on Spotify.
                // TODO: Add a notice about this feature that some apps may not support this function.
                await mw.sessionManager.GetCurrentSession().TryChangePlaybackPositionAsync((long)songProgress.Value);
        }
    }
}
