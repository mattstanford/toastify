﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;

namespace Toastify
{
    public partial class Toast : Window
    {
        private readonly string SETTINGS_FILE = "Toastify.xml";

        Timer watchTimer;
        System.Windows.Forms.NotifyIcon trayIcon;
        SettingsXml settings;
        string fullPathSettingsFile = "";

        internal List<Hotkey> HotKeys { get; set; }
        internal List<Toastify.Plugin.PluginBase> Plugins { get; set; }

        public Toast()
        {
            InitializeComponent();

            string applicationPath = new System.IO.FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName;
            fullPathSettingsFile = System.IO.Path.Combine(applicationPath, SETTINGS_FILE);

            if (!System.IO.File.Exists(fullPathSettingsFile))
            {
                settings = SettingsXml.Defaul;
                try
                {
                    settings.Save(fullPathSettingsFile);
                }
                catch (Exception)
                {
                    MessageBox.Show(@"Toastify was unable to create the settings file." + Environment.NewLine +
                                     "Make sure the application is executed from a folder with write access." + Environment.NewLine +
                                     Environment.NewLine + 
                                     "The application will now be started with default settings.", "Toastify", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                try
                {
                    settings = SettingsXml.Open(fullPathSettingsFile);
                }
                catch(Exception)
                {
                    MessageBox.Show(@"Toastify was unable to load the settings file." + Environment.NewLine +
                                     "Delete the Toastify.xml file and restart the application to recreate the settings file." + Environment.NewLine +
                                    Environment.NewLine + 
                                    "The application will now be started with default settings.", "Toastify", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            //Init toast(color settings)
            InitToast();

            //Init tray icon
            trayIcon = new System.Windows.Forms.NotifyIcon();
            trayIcon.Icon = Toastify.Properties.Resources.spotifyicon;
            trayIcon.Text = "Toastify";
            trayIcon.Visible = true;

            //Init tray icon menu
            System.Windows.Forms.MenuItem menuAbout = new System.Windows.Forms.MenuItem();
            menuAbout.Text = "About Toastify...";
            menuAbout.Click += (s, e) => { new About().ShowDialog(); };
            System.Windows.Forms.MenuItem menuExit = new System.Windows.Forms.MenuItem();
            menuExit.Text = "Exit";
            menuExit.Click += (s, e) => { this.Close(); };
            trayIcon.ContextMenu = new System.Windows.Forms.ContextMenu();
            trayIcon.ContextMenu.MenuItems.Add(menuAbout);
            trayIcon.ContextMenu.MenuItems.Add(menuExit);

            //Init watch timer
            watchTimer = new Timer(500);
            watchTimer.Elapsed += (s, e) =>
            {
                CheckTitle();
            };
        }

        private void InitToast()
        {
            const double MIN_WIDTH = 200.0;
            const double MIN_HEIGHT = 65.0;

            //If we find any invalid settings in the xml we skip it and use default.
            //User notification of bad settings will be implemented with the settings dialog.

            //This method is UGLY but we'll keep it until the settings dialog is implemented.


            ToastBorder.BorderThickness = new Thickness(settings.ToastBorderThickness);

            ColorConverter cc = new ColorConverter();
            if (!string.IsNullOrEmpty(settings.ToastBorderColor) && cc.IsValid(settings.ToastBorderColor))
                ToastBorder.BorderBrush = new SolidColorBrush((Color)cc.ConvertFrom(settings.ToastBorderColor));

            if (!string.IsNullOrEmpty(settings.ToastColorTop) && !string.IsNullOrEmpty(settings.ToastColorBottom) && cc.IsValid(settings.ToastColorTop) && cc.IsValid(settings.ToastColorBottom))
            {
                Color top = (Color)cc.ConvertFrom(settings.ToastColorTop);
                Color botton = (Color)cc.ConvertFrom(settings.ToastColorBottom);

                ToastBorder.Background = new LinearGradientBrush(top, botton, 90.0);
            }

            if (settings.ToastWidth >= MIN_WIDTH)
                this.Width = settings.ToastWidth;
            if (settings.ToastHeight >= MIN_HEIGHT)
                this.Height = settings.ToastHeight;


            if (!string.IsNullOrEmpty(settings.ToastBorderCornerRadious))
            {
                var culture = CultureInfo.CreateSpecificCulture("en-US");
                string[] parts = settings.ToastBorderCornerRadious.Split(',');
                if (parts.Length != 4)
                    return;

                double topleft, topright, bottomright, bottomleft;
                if (!double.TryParse(parts[0], NumberStyles.Float, culture, out topleft))
                    return;
                if (!double.TryParse(parts[1], NumberStyles.Float, culture, out topright))
                    return;
                if (!double.TryParse(parts[2], NumberStyles.Float, culture, out bottomright))
                    return;
                if (!double.TryParse(parts[3], NumberStyles.Float, culture, out bottomleft))
                    return;

                //If we made it this far we have all the values needed.
                ToastBorder.CornerRadius = new CornerRadius(topleft, topright, bottomright, bottomleft);
            }
        }

        string previousTitle = string.Empty;
        private void CheckTitle()
        {
            string currentTitle = Spotify.GetCurrentTrack();
            if (!string.IsNullOrEmpty(currentTitle) && currentTitle != previousTitle)
            {
                string part1, part2;
                if (SplitTitle(currentTitle, out part1, out part2))
                {
                    this.Dispatcher.Invoke((Action)delegate { Title1.Text = part2; Title2.Text = part1; }, System.Windows.Threading.DispatcherPriority.Normal);

                    foreach (var p in this.Plugins)
                    {
                        try
                        {
                            p.TrackChanged(part1, part2);
                        }
                        catch (Exception)
                        {
                            //For now we swallow any plugin errors.
                        }
                    }
                }

                previousTitle = currentTitle;
                this.Dispatcher.Invoke((Action)delegate { FadeIn(); }, System.Windows.Threading.DispatcherPriority.Normal);
            }
        }

        private bool SplitTitle(string title, out string part1, out string part2)
        {
            part1 = string.Empty;
            part2 = string.Empty;

            string[] parts = title.Split('\u2013'); //Spotify uses an en dash to separate Artist and Title
            if (parts.Length < 1 || parts.Length > 2)
                return false; //Invalid title

            if (parts.Length == 1)
                part2 = parts[0].Trim();
            else if (parts.Length == 2)
            {
                part1 = parts[0].Trim();
                part2 = parts[1].Trim();
            }

            return true;
        }

        private void FadeIn()
        {
            if (settings.DisableToast)
                return;

            System.Drawing.Rectangle workingArea = new System.Drawing.Rectangle((int)this.Left, (int)this.Height, (int)this.ActualWidth, (int)this.ActualHeight);
            workingArea = System.Windows.Forms.Screen.GetWorkingArea(workingArea);

            this.Left = workingArea.Right - this.ActualWidth - settings.OffsetRight;
            this.Top = workingArea.Bottom - this.ActualHeight - settings.OffsetBottom;

            DoubleAnimation anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(250));
            anim.Completed += (s, e) => { FadeOut(); };
            this.BeginAnimation(Window.OpacityProperty, anim);
        }

        private void FadeOut()
        {
            DoubleAnimation anim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(500));
            anim.BeginTime = TimeSpan.FromMilliseconds(settings.FadeOutTime);
            this.BeginAnimation(Window.OpacityProperty, anim);
        }

        KeyboardHook hook;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Remove from ALT+TAB
            WinHelper.AddToolWindowStyle(this);

            //Check if Spotify is running.
            EnsureSpotify();
            LoadPlugins();

            if (!settings.DisableToast)
                watchTimer.Enabled = true; //Only need to be enabled if we are going to show the toast.

            if (settings.GlobalHotKeys)
            {
                hook = new KeyboardHook();
                hook.KeyUp += new KeyboardHook.HookEventHandler(hook_KeyUp);
            }

            //Let the plugins now we're started.
            foreach (var p in this.Plugins)
            {
                try
                {
                    p.Started();
                }
                catch (Exception)
                {
                    //For now we swallow any plugin errors.
                }
            }
        }

        private void LoadPlugins()
        {
            //Load plugins
            this.Plugins = new List<Toastify.Plugin.PluginBase>();
            string applicationPath = new System.IO.FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName;

            foreach (var p in settings.Plugins)
            {
                try
                {
                    var plugin = Activator.CreateInstanceFrom(System.IO.Path.Combine(applicationPath, p.FileName), p.TypeName).Unwrap() as Toastify.Plugin.PluginBase;
                    plugin.Init(p.Settings);
                    this.Plugins.Add(plugin);
                }
                catch (Exception)
                {
                    //For now we swallow any plugin errors.
                }
                Console.WriteLine("Loaded " + p.TypeName);
            }
        }

        private void EnsureSpotify()
        {
            //Make sure Spotify is running when starting Toastify.
            //If not ask the user and try to start it.

            if (!Spotify.IsAvailable())
            {
                if ((settings.AlwaysStartSpotify.HasValue && settings.AlwaysStartSpotify.Value) || (MessageBox.Show("Spotify doesn't seem to be running.\n\nDo you want Toastify to try and start it for you?", "Toastify", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes))
                {
                    string spotifyPath = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Spotify", string.Empty, string.Empty).ToString();  //string.Empty = (Default) value

                    if (string.IsNullOrEmpty(spotifyPath))
                    {
                        MessageBox.Show("Unable to find Spotify. Make sure it is installed and/or start it manually.", "Toastify", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(System.IO.Path.Combine(spotifyPath, "Spotify.exe"));

                            if (!settings.AlwaysStartSpotify.HasValue)
                            {
                                var ret = MessageBox.Show("Do you always want to start Spotify if it's not already running?", "Toastify", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                settings.AlwaysStartSpotify = (ret == MessageBoxResult.Yes);
                                settings.Save(fullPathSettingsFile);
                            }
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("An unknown error occurd when trying to start Spotify.\nPlease start Spotify manually.", "Toastify", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            //Let the plugins now we're closing up.
            foreach (var p in this.Plugins)
            {
                try
                {
                    p.Closing();
                    p.Dispose();
                }
                catch (Exception)
                {
                    //For now we swallow any plugin errors.
                }
            }
            this.Plugins.Clear();

            //Ensure trayicon is removed on exit. (Thx Linus)
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayIcon = null;
            base.OnClosing(e);
        }

        private System.Windows.Input.Key ConvertKey(System.Windows.Forms.Keys key)
        {
            if (Enum.GetNames(typeof(System.Windows.Input.Key)).Contains(key.ToString()))
                return (System.Windows.Input.Key)Enum.Parse(typeof(System.Windows.Input.Key), key.ToString());
            else
                return Key.None;
        } 

        void hook_KeyUp(object sender, HookEventArgs e)
        {
            string currentTrack = string.Empty;
            var key = ConvertKey(e.Key);

            foreach (var hotkey in settings.HotKeys)
            {
                if (hotkey.Alt == e.Alt && hotkey.Ctrl == e.Control && hotkey.Shift == e.Shift && hotkey.Key == key)
                {
                    try
                    {
                        string trackBeforeAction = Spotify.GetCurrentTrack();
                        if (hotkey.Action == SpotifyAction.CopyTrackInfo && !string.IsNullOrEmpty(trackBeforeAction))
                            Clipboard.SetText(string.Format(settings.ClipboardTemplate, trackBeforeAction));
                        else
                            Spotify.SendAction(hotkey.Action);

                        DisplayAction(hotkey.Action, trackBeforeAction);
                    }
                    catch (Exception)
                    {
                        Title1.Text = "Unable to communicate with Spotify";
                        Title2.Text = "";
                        FadeIn();
                    }
                }
            }
        }

        private void DisplayAction(SpotifyAction action, string trackBeforeAction)
        {
            //Anything that changes track doesn't need to be handled since
            //that will be handled in the timer event.

            const string VOLUME_UP_TEXT = "Volume ++";
            const string VOLUME_DOWN_TEXT = "Volume --";
            const string MUTE_ON_OFF_TEXT = "Mute On/Off";
            const string NOTHINGS_PLAYING = "Nothings playing";
            const string PAUSED_TEXT = "Paused";
            const string STOPPED_TEXT = "Stopped";

            if (!Spotify.IsAvailable())
            {
                Title1.Text = "Spotify not available!";
                Title2.Text = string.Empty;
                FadeIn();
                return;
            }

            string currentTrack = Spotify.GetCurrentTrack();

            string prevTitle1 = Title1.Text;
            string prevTitle2 = Title2.Text;

            switch (action)
            {
                case SpotifyAction.PlayPause:
                    if (!string.IsNullOrEmpty(trackBeforeAction))
                    {
                        //We pressed pause
                        Title1.Text = "Paused";
                        Title2.Text = trackBeforeAction;
                        FadeIn();
                    }
                    previousTitle = string.Empty;  //If we presses play this will force a toast to display in next timer event.
                    break;
                case SpotifyAction.Stop:
                    previousTitle = string.Empty;
                    Title1.Text = "Stopped";
                    Title2.Text = trackBeforeAction;
                    FadeIn();
                    break;
                case SpotifyAction.NextTrack:      //No need to handle
                    break;
                case SpotifyAction.PreviousTrack:  //No need to handle
                    break;
                case SpotifyAction.VolumeUp:
                    Title1.Text = VOLUME_UP_TEXT;
                    Title2.Text = currentTrack;
                    FadeIn();
                    break;
                case SpotifyAction.VolumeDown:
                    Title1.Text = VOLUME_DOWN_TEXT;
                    Title2.Text = currentTrack;
                    FadeIn();
                    break;
                case SpotifyAction.Mute:
                    Title1.Text = MUTE_ON_OFF_TEXT;
                    Title2.Text = currentTrack;
                    FadeIn();
                    break;
                case SpotifyAction.ShowToast:
                    if (string.IsNullOrEmpty(currentTrack) && Title1.Text != PAUSED_TEXT && Title1.Text != STOPPED_TEXT)
                    {
                        Title1.Text = NOTHINGS_PLAYING;
                        Title2.Text = string.Empty;
                    }
                    else if (Title1.Text == VOLUME_UP_TEXT || Title1.Text == VOLUME_DOWN_TEXT || Title1.Text == MUTE_ON_OFF_TEXT)
                    {
                        string part1, part2;
                        if (SplitTitle(currentTrack, out part1, out part2))
                        {
                            Title1.Text = part2;
                            Title2.Text = part1;
                        }
                    }
                    FadeIn();
                    break;
                case SpotifyAction.ShowSpotify:  //No need to handle
                    break;
            }
        }
    }
}
