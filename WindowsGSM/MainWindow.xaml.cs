﻿using ControlzEx.Theming;
using LiveCharts;
using LiveCharts.Wpf;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using NCrontab;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsGSM.DiscordBot;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using Label = System.Windows.Controls.Label;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;

[assembly: AssemblyVersion("1.25.1.3")]

namespace WindowsGSM {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, WindowShowStyle nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowText(IntPtr hWnd, string windowName);

        private static class RegistryKeyName
        {
            public const string HardWareAcceleration = "HardWareAcceleration";
            public const string UIAnimation = "UIAnimation";
            public const string DarkTheme = "DarkTheme";
            public const string StartOnBoot = "StartOnBoot";
            public const string RestartOnCrash = "RestartOnCrash";
            public const string DonorTheme = "DonorTheme";
            public const string DonorColor = "DonorColor";
            public const string DonorAuthKey = "DonorAuthKey";
            public const string SendStatistics = "SendStatistics";
            public const string Height = "Height";
            public const string Width = "Width";
            public const string DiscordBotAutoStart = "DiscordBotAutoStart";
        }

        public class ServerMetadata
        {
            public ServerStatus ServerStatus = ServerStatus.Stopped;
            public Process Process;
            public IntPtr MainWindow;
            public ServerConsole ServerConsole;

            // Basic Game Server Settings
            public bool AutoRestart;
            public bool AutoStart;
            public bool AutoUpdate;
            public bool UpdateOnStart;
            public bool BackupOnStart;

            // Discord Alert Settings
            public bool DiscordAlert;
            public string DiscordMessage;
            public string DiscordWebhook;
            public bool AutoRestartAlert;
            public bool AutoStartAlert;
            public bool AutoUpdateAlert;
            public bool RestartCrontabAlert;
            public bool CrashAlert;
            public bool AutoIpUpdateAlert;
            public bool SkipUserSetup;

            // Restart Crontab Settings
            public bool RestartCrontab;
            public string CrontabFormat;

            // Game Server Start Priority and Affinity
            public string CPUPriority;
            public string CPUAffinity;

            public bool EmbedConsole;
            public bool ShowConsole;
            public bool AutoScroll;

            //runntime member to determine if public Ip changed
            public string CurrentPublicIp;
        }

        private enum WindowShowStyle : uint
        {
            Hide = 0,
            ShowNormal = 1,
            Show = 5,
            Minimize = 6,
            ShowMinNoActivate = 7
        }

        public enum ServerStatus
        {
            Started = 0,
            Starting = 1,
            Stopped = 2,
            Stopping = 3,
            Restarted = 4,
            Restarting = 5,
            Updated = 6,
            Updating = 7,
            Backuped = 8,
            Backuping = 9,
            Restored = 10,
            Restoring = 11,
            Deleting = 12,
            Crashed = 13
        }

        public static readonly string WGSM_VERSION = "v" + string.Concat(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
        public static readonly int MAX_SERVER = 50;
        public static readonly string WGSM_PATH = Path.GetDirectoryName(Environment.ProcessPath);
        public static readonly string DEFAULT_THEME = "Cyan";

        private readonly NotifyIcon notifyIcon;
        private Process Installer;

        public static readonly Dictionary<int, ServerMetadata> _serverMetadata = [];
        public static ServerMetadata GetServerMetadata(object serverId) => _serverMetadata.TryGetValue(int.Parse(serverId.ToString()), out ServerMetadata s) ? s : null;

        public List<PluginMetadata> PluginsList = [];

        private readonly List<System.Windows.Controls.CheckBox> _checkBoxes = [];

        public string g_DonorType = string.Empty;

        private readonly DiscordBot.Bot g_DiscordBot = new();

        private long _lastAutoRestartTime = 0;
        private long _lastCrashTime = 0;
        private const long _webhookThresholdTimeInMs = 6000 * 5;
        public ServerStatus _latestWebhookSend = ServerStatus.Stopped;

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(new HwndSourceHook(HandleMessages));
        }


        protected override async void OnClosing(CancelEventArgs e)
        {
            // Don't overwrite cancellation for close
            if (e.Cancel == false)
            {
                // #2409: don't close window if there is a dialog still open
                BaseMetroDialog dialog = await this.GetCurrentDialogAsync<BaseMetroDialog>();
                e.Cancel = dialog != null && (this.ShowDialogsOverTitleBar || !dialog.DialogSettings.OwnerCanCloseWithDialog);

                //add a close confirmation
                const string message = "關閉並停止所有伺服器?";
                const string caption = "關閉 WindowsGSM";
                MessageBoxResult result = MessageBox.Show(message, caption,
                                             MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                    e.Cancel = true;
            }

            if (e.Cancel == false)
                StoppAllServers();

            base.OnClosing(e);
        }

        public async void StoppAllServers ()
        {
            foreach (ServerTable server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started)
                {
                    await GameServer_Stop(server);
                }
            }
            int secCounter = 0;
            int processesRunning = 0;
            //wait for all servers to stop
            while (secCounter < 30)
            {
                Thread.Sleep(1000);// just wait a fixed 30 sec
                processesRunning = 0;
                secCounter++;
                foreach (ServerTable server in ServerGrid.Items.Cast<ServerTable>().ToList())
                {
                    Process p = null;
                    try
                    {
                        p = Process.GetProcessById(int.Parse(server.PID)); // will fail wenn the process is completly closed by now
                        if (p != null && !p.HasExited)
                            processesRunning++;
                    }
                    catch (Exception){ }
                }
                if (processesRunning == 0) break;
            }
        }

        private IntPtr HandleMessages(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 0x0112 == WM_SYSCOMMAND, 'Window' command message.
            // 0xF020 == SC_MINIMIZE, command to minimize the window.
            if (msg == 0x0112 && checked((int)wParam & 0xFFF0) == 0xF020)
            {
                // Cancel the minimize.
                NotifyIcon_MouseClick(null, null);
                handled = true;
            }

            return IntPtr.Zero;
        }

        public MainWindow(bool showCrashHint)
        {
            //Add SplashScreen
            SplashScreen splashScreen = new("Images/SplashScreen.png");
            splashScreen.Show(false, true);
            DiscordWebhook.SendErrorLog();

            InitializeComponent();
            this.SourceInitialized += new EventHandler(OnSourceInitialized);

            Title = $"WindowsGSM {WGSM_VERSION}";

            //Close SplashScreen
            splashScreen.Close(new TimeSpan(0, 0, 1));

            // Add all themes to comboBox_Themes
            ThemeManager.Current.Themes.Select(t => Path.GetExtension(t.Name).Trim('.')).Distinct().OrderBy(x => x).ToList().ForEach(delegate (string name) { comboBox_Themes.Items.Add(name); });

            // Set up _serverMetadata
            for (int i = 0; i < MAX_SERVER; i++)
            {
                _serverMetadata[i] = new ServerMetadata
                {
                    ServerStatus = ServerStatus.Stopped,
                    ServerConsole = new ServerConsole(i)
                };
            }

            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM");
            if (key == null)
            {
                key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\WindowsGSM");
                key.SetValue(RegistryKeyName.HardWareAcceleration, "True");
                key.SetValue(RegistryKeyName.UIAnimation, "True");
                key.SetValue(RegistryKeyName.DarkTheme, "False");
                key.SetValue(RegistryKeyName.StartOnBoot, "False");
                key.SetValue(RegistryKeyName.RestartOnCrash, "False");
                key.SetValue(RegistryKeyName.DonorTheme, "False");
                key.SetValue(RegistryKeyName.DonorColor, DEFAULT_THEME);
                key.SetValue(RegistryKeyName.DonorAuthKey, "");
                key.SetValue(RegistryKeyName.SendStatistics, "True");
                key.SetValue(RegistryKeyName.Height, Height);
                key.SetValue(RegistryKeyName.Width, Width);
                key.SetValue(RegistryKeyName.DiscordBotAutoStart, "False");
            }

            MahAppSwitch_HardWareAcceleration.IsOn = (key.GetValue(RegistryKeyName.HardWareAcceleration) ?? true).ToString() == "True";
            MahAppSwitch_UIAnimation.IsOn = (key.GetValue(RegistryKeyName.UIAnimation) ?? true).ToString() == "True";
            MahAppSwitch_DarkTheme.IsOn = (key.GetValue(RegistryKeyName.DarkTheme) ?? false).ToString() == "True";
            MahAppSwitch_StartOnBoot.IsOn = (key.GetValue(RegistryKeyName.StartOnBoot) ?? false).ToString() == "True";
            MahAppSwitch_RestartOnCrash.IsOn = (key.GetValue(RegistryKeyName.RestartOnCrash) ?? false).ToString() == "True";
            MahAppSwitch_DonorConnect.Toggled -= DonorConnect_IsCheckedChanged;
            MahAppSwitch_DonorConnect.IsOn = (key.GetValue(RegistryKeyName.DonorTheme) ?? false).ToString() == "True";
            MahAppSwitch_DonorConnect.Toggled += DonorConnect_IsCheckedChanged;
            MahAppSwitch_SendStatistics.IsOn = (key.GetValue(RegistryKeyName.SendStatistics) ?? true).ToString() == "True";
            MahAppSwitch_DiscordBotAutoStart.IsOn = (key.GetValue(RegistryKeyName.DiscordBotAutoStart) ?? false).ToString() == "True";
            string color = (key.GetValue(RegistryKeyName.DonorColor) ?? string.Empty).ToString();
            comboBox_Themes.SelectionChanged -= ComboBox_Themes_SelectionChanged;
            comboBox_Themes.SelectedItem = comboBox_Themes.Items.Contains(color) ? color : DEFAULT_THEME;
            comboBox_Themes.SelectionChanged += ComboBox_Themes_SelectionChanged;

            if (MahAppSwitch_DonorConnect.IsOn)
            {
                string authKey = (key.GetValue(RegistryKeyName.DonorAuthKey) == null) ? string.Empty : key.GetValue(RegistryKeyName.DonorAuthKey).ToString();
                if (!string.IsNullOrWhiteSpace(authKey))
                {
#pragma warning disable 4014
                    AuthenticateDonor(authKey);
#pragma warning restore
                }
            }

            //double.parse can throw when the user changes locale after WindowsGsm setup
            //Height = (key.GetValue(RegistryKeyName.Height) == null) ? Height : double.Parse(key.GetValue(RegistryKeyName.Height).ToString());
            //Width = (key.GetValue(RegistryKeyName.Width) == null) ? Width : double.Parse(key.GetValue(RegistryKeyName.Width).ToString());
            if (InvariantTryParse(key.GetValue(RegistryKeyName.Height).ToString(), out double regValue))
                Height = regValue;
            if (InvariantTryParse(key.GetValue(RegistryKeyName.Width).ToString(), out regValue))
                Width = regValue;
            key.Close();

            RenderOptions.ProcessRenderMode = MahAppSwitch_HardWareAcceleration.IsOn ? System.Windows.Interop.RenderMode.SoftwareOnly : System.Windows.Interop.RenderMode.Default;
            WindowTransitionsEnabled = MahAppSwitch_UIAnimation.IsOn;
            ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem}");
            //Not required - it is set in windows settings
            //SetStartOnBoot(MahAppSwitch_StartOnBoot.IsChecked ?? false);
            if (MahAppSwitch_DiscordBotAutoStart.IsOn)
            {
                switch_DiscordBot.IsOn = true;
            }

            // Add items to Set Affinity Flyout
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                StackPanel stackPanel = new() {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(15, 0, 0, 0)
                };

                _checkBoxes.Add(new System.Windows.Controls.CheckBox());
                _checkBoxes[i].Focusable = false;
                Label label = new() {
                    Content = $"CPU {i}",
                    Padding = new Thickness(0, 5, 0, 5)
                };

                stackPanel.Children.Add(_checkBoxes[i]);
                stackPanel.Children.Add(label);
                StackPanel_SetAffinity.Children.Add(stackPanel);
            }

            // Add click listener on each checkBox
            foreach (System.Windows.Controls.CheckBox checkBox in _checkBoxes)
            {
                checkBox.Click += (sender, e) =>
                {
                    ServerTable server = (ServerTable)ServerGrid.SelectedItem;
                    if (server == null) { return; }

                CheckPrioity:
                    string priority = string.Empty;
                    for (int i = _checkBoxes.Count - 1; i >= 0; i--)
                    {
                        priority += (_checkBoxes[i].IsChecked ?? false) ? "1" : "0";
                    }

                    if (!priority.Contains('1'))
                    {
                        checkBox.IsChecked = true;
                        goto CheckPrioity;
                    }

                    textBox_SetAffinity.Text = Functions.CPU.Affinity.GetAffinityValidatedString(priority);

                    _serverMetadata[int.Parse(server.ID)].CPUAffinity = priority;
                    ServerConfig.SetSetting(server.ID, "cpuaffinity", priority);

                    if (GetServerMetadata(server.ID).Process != null && !GetServerMetadata(server.ID).Process.HasExited)
                    {
                        _serverMetadata[int.Parse(server.ID)].Process.ProcessorAffinity = Functions.CPU.Affinity.GetAffinityIntPtr(priority);
                    }
                };
            }

            notifyIcon = new NotifyIcon {
                BalloonTipTitle = "WindowsGSM",
                BalloonTipText = "WindowsGSM 正在背景執行",
                Text = "WindowsGSM",
                BalloonTipIcon = ToolTipIcon.Info,
                Visible = true,
                Icon = new System.Drawing.Icon(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Images/WindowsGSM-Icon.ico")).Stream)
            };
            notifyIcon.BalloonTipClicked += OnBalloonTipClick;
            notifyIcon.MouseClick += NotifyIcon_MouseClick;

            ServerPath.CreateAndFixDirectories();

            LoadPlugins(shouldAwait: false);
            AddGamesToComboBox();

            LoadServerTable();

            if (ServerGrid.Items.Count > 0)
            {
                ServerGrid.SelectedItem = ServerGrid.Items[0];
            }

            foreach (ServerTable server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                int pid = ServerCache.GetPID(server.ID);
                if (pid != -1)
                {
                    Process p = null;
                    try
                    {
                        p = Process.GetProcessById(pid);
                    }
                    catch
                    {
                        continue;
                    }

                    string pName = ServerCache.GetProcessName(server.ID);
                    if (!string.IsNullOrWhiteSpace(pName) && p.ProcessName == pName)
                    {
                        _serverMetadata[int.Parse(server.ID)].Process = p;
                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Started;
                        SetServerStatus(server, "已啟動");

                        /*// Get Console process - untested
                        Process console = GetConsoleProcess(pid);
                        if (console != null)
                        {
                            ReadConsoleOutput(server.ID, console);
                        }
                        */

                        _serverMetadata[int.Parse(server.ID)].MainWindow = ServerCache.GetWindowsIntPtr(server.ID);
                        p.Exited += (sender, e) => OnGameServerExited(server);

                        StartAutoUpdateCheck(server);
                        StartRestartCrontabCheck(server);
                        StartSendHeartBeat(server);
                        StartQuery(server);
                    }
                }
            }

            if (showCrashHint)
            {
                string logFile = $"CRASH_{DateTime.Now:yyyyMMdd}.log";
                Log("系統", $"WindowsGSM 先前不正常關閉, 請查看當機日誌 {logFile}");
            }

            AutoStartServer();

            if (MahAppSwitch_SendStatistics.IsOn)
            {
                SendGoogleAnalytics();
            }

            StartConsoleRefresh();

            StartServerTableRefresh();

            StartDashBoardRefresh();

            StartAutpIpUpdate();
        }

        private static Process GetConsoleProcess(int processId)
        {
            try
            {
                ManagementObjectSearcher mos = new($"Select * From Win32_Process Where ParentProcessID={processId}");
                foreach (ManagementObject mo in mos.Get().Cast<ManagementObject>())
                {
                    Process p = Process.GetProcessById(Convert.ToInt32(mo["ProcessID"]));
                    if (Equals(p, "conhost"))
                    {
                        return p;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        // Read console redirect output - not tested
        private static async void ReadConsoleOutput(string serverId, Process p)
        {
            await Task.Run(() =>
            {
                StreamReader reader = p.StandardOutput;
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        GetServerMetadata(serverId).ServerConsole.Add(line);
                    });
                }
            });
        }

        public void AddGamesToComboBox()
        {
            comboBox_InstallGameServer.Items.Clear();
            comboBox_ImportGameServer.Items.Clear();

            //Add games to ComboBox
            SortedList sortedList = [];
            List<DictionaryEntry> gameName = [.. GameServer.Data.Icon.ResourceManager.GetResourceSet(System.Globalization.CultureInfo.CurrentUICulture, true, true).Cast<DictionaryEntry>()];
            gameName.ForEach(delegate (DictionaryEntry entry) { sortedList.Add(entry.Key, $"/WindowsGSM;component/{entry.Value}"); });
            int pluginLoaded = 0;
            PluginsList.ForEach(delegate (PluginMetadata plugin)
            {
                if (plugin.IsLoaded)
                {
                    pluginLoaded++;
                    sortedList.Add(plugin.FullName, plugin.GameImage == PluginManagement.DefaultPluginImage ? plugin.GameImage.Replace("pack://application:,,,", "/WindowsGSM;component") : plugin.GameImage);
                }
            });

            label_GameServerCount.Content = $"{gameName.Count + pluginLoaded} 遊戲伺服器支援";

            for (int i = 0; i < sortedList.Count; i++)
            {
                Images.Row row = new() {
                    Image = sortedList.GetByIndex(i).ToString(),
                    Name = sortedList.GetKey(i).ToString()
                };

                comboBox_InstallGameServer.Items.Add(row);
                comboBox_ImportGameServer.Items.Add(row);
            }
        }

        public async void LoadPlugins(bool shouldAwait = true)
        {
            PluginManagement pm = new();
            PluginsList = await PluginManagement.LoadPlugins(shouldAwait);

            int loadedCount = 0;
            PluginsList.ForEach(delegate (PluginMetadata plugin)
            {
                if (!plugin.IsLoaded)
                {
                    Directory.CreateDirectory(ServerPath.GetLogs(ServerPath.FolderName.Plugins));
                    string logFile = ServerPath.GetLogs(ServerPath.FolderName.Plugins, $"{plugin.FileName}.log");
                    File.WriteAllText(ServerPath.GetLogs(logFile), plugin.Error);
                    Log("外掛", $"{plugin.FileName} 載入失敗. 請查看日誌: {logFile.Replace(WGSM_PATH, string.Empty)}");
                }
                else
                {
                    loadedCount++;
                    BrushConverter converter = new();
                    Brush brush;
                    try
                    {
                        brush = (Brush)converter.ConvertFromString(plugin.Plugin.color);
                    }
                    catch
                    {
                        brush = Brushes.DimGray;
                    }

                    Border borderBase = new() {
                        BorderBrush = brush,
                        Background = Brushes.SlateGray,
                        BorderThickness = new Thickness(1.5),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(6),
                        Margin = new Thickness(10, 0, 10, 10)
                    };
                    DockPanel.SetDock(borderBase, Dock.Top);
                    DockPanel dockPanelBase = new();
                    Border gameImage = new() {
                        BorderBrush = Brushes.White,
                        Background = new ImageBrush
                        {
                            Stretch = Stretch.Fill,
                            ImageSource = plugin.GameImage == PluginManagement.DefaultPluginImage ? PluginManagement.GetDefaultPluginBitmapSource() : new BitmapImage(new Uri(plugin.GameImage))
                        },
                        BorderThickness = new Thickness(0),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(10),
                        Width = 63,
                        Height = 63,
                        MinWidth = 63,
                        MinHeight = 63,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    dockPanelBase.Children.Add(gameImage);

                    DockPanel dockPanel = new() { Margin = new Thickness(0, 0, 3, 0) };
                    DockPanel.SetDock(dockPanel, Dock.Top);
                    Label label = new() { Content = $"v{plugin.Plugin.version}", Padding = new Thickness(0) };
                    DockPanel.SetDock(label, Dock.Right);
                    dockPanel.Children.Add(label);
                    label = new Label { Content = plugin.Plugin.name.Split('.')[1], Padding = new Thickness(0), FontSize = 14, FontWeight = FontWeights.Bold };
                    DockPanel.SetDock(label, Dock.Left);
                    dockPanel.Children.Add(label);
                    dockPanelBase.Children.Add(dockPanel);

                    TextBlock textBlock = new() { Text = plugin.Plugin.description };
                    DockPanel.SetDock(textBlock, Dock.Top);
                    dockPanelBase.Children.Add(textBlock);

                    StackPanel stackPanelBase = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
                    Border authorImage = new() {
                        Background = new ImageBrush
                        {
                            Stretch = Stretch.Fill,
                            ImageSource = plugin.AuthorImage == PluginManagement.DefaultUserImage ? PluginManagement.GetDefaultUserBitmapSource() : new BitmapImage(new Uri(plugin.AuthorImage))
                        },
                        BorderThickness = new Thickness(0),
                        CornerRadius = new CornerRadius(30),
                        Padding = new Thickness(10),
                        Width = 25,
                        Height = 25,
                        Margin = new Thickness(0, 0, 5, 0)
                    };
                    stackPanelBase.Children.Add(authorImage);
                    StackPanel stackPanel = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    label = new Label { Content = plugin.Plugin.author, Padding = new Thickness(0) };
                    DockPanel.SetDock(label, Dock.Top);
                    stackPanel.Children.Add(label);
                    label = new Label { Content = "•", Padding = new Thickness(0), Margin = new Thickness(5, 0, 5, 0) };
                    DockPanel.SetDock(label, Dock.Top);
                    stackPanel.Children.Add(label);
                    textBlock = new TextBlock();
                    Hyperlink hyperlink = new(new Run(plugin.Plugin.url)) { Foreground = brush };
                    try
                    {
                        hyperlink.NavigateUri = new Uri(plugin.Plugin.url);
                        hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                    }
                    catch { }

                    textBlock.Inlines.Add(hyperlink);
                    stackPanel.Children.Add(textBlock);
                    stackPanelBase.Children.Add(stackPanel);
                    dockPanelBase.Children.Add(stackPanelBase);

                    borderBase.Child = dockPanelBase;
                    StackPanel_PluginList.Children.Add(borderBase);
                }
            });

            AddGamesToComboBox();

            Label_PluginInstalled.Content = PluginsList.Count.ToString();
            Label_PluginLoaded.Content = loadedCount.ToString();
            Label_PluginFailed.Content = (PluginsList.Count - loadedCount).ToString();

            Log("外掛", $"已安裝: {PluginsList.Count}, 已載入: {loadedCount}, 失敗: {PluginsList.Count - loadedCount}");
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
        }

        private async void ImportPlugin_Click(object sender, RoutedEventArgs e)
        {
            // If a server is installing or import => return
            if (progressbar_InstallProgress.IsIndeterminate || progressbar_ImportProgress.IsIndeterminate)
            {
                MessageBox.Show("WindowsGSM 正在安裝/匯入伺服器!", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string pluginsDir = ServerPath.FolderName.Plugins;

            System.Windows.Forms.OpenFileDialog ofd = new() {
                Filter = "zip files (*.zip)|*.zip",
                InitialDirectory = pluginsDir
            };

            DialogResult dr = ofd.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK)
            {
                Button_ImportPlugin.IsEnabled = false;
                ProgressRing_LoadPlugins.Visibility = Visibility.Visible;
                Label_PluginInstalled.Content = "-";
                Label_PluginLoaded.Content = "-";
                Label_PluginFailed.Content = "-";
                StackPanel_PluginList.Children.Clear();

                /// This is relying on it keeps the naming shceme of the ZIP files that're downloaed from GitHub releases. Like WindowsGSM.Spigot-1.0.zip,
                /// Just by following WindowsGSM naming of plugins, and this will be fine!
                string zipPath = ofd.FileName;
                string dirName = ofd.SafeFileName.Split('.')[1].Split('-')[0] + ".cs";
                string knownPattern = ".cs";
                // Unziping plugin
                using (ZipArchive zip = System.IO.Compression.ZipFile.OpenRead(zipPath))
                {
                    IEnumerable<ZipArchiveEntry> result = from entry in zip.Entries
                                 where Path.GetDirectoryName(entry.FullName).Contains(knownPattern)
                                 where !String.IsNullOrEmpty(entry.Name)
                                 select entry;

                    Directory.CreateDirectory(Path.Combine(pluginsDir, dirName));
                    foreach (ZipArchiveEntry entryFile in result)
                    {
                        entryFile.ExtractToFile(Path.Combine(pluginsDir, dirName, entryFile.Name), true);
                    }
                }

                await Task.Delay(500);
                LoadPlugins();
                LoadServerTable();

                Button_ImportPlugin.IsEnabled = true;
                ProgressRing_LoadPlugins.Visibility = Visibility.Collapsed;
            }
        }

        private async void RefreshPlugins_Click(object sender, RoutedEventArgs e)
        {
            // If a server is installing or import => return
            if (progressbar_InstallProgress.IsIndeterminate || progressbar_ImportProgress.IsIndeterminate)
            {
                MessageBox.Show("WindowsGSM 正在安裝/匯入伺服器!", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Button_RefreshPlugins.IsEnabled = false;
            ProgressRing_LoadPlugins.Visibility = Visibility.Visible;
            Label_PluginInstalled.Content = "-";
            Label_PluginLoaded.Content = "-";
            Label_PluginFailed.Content = "-";
            StackPanel_PluginList.Children.Clear();

            await Task.Delay(500);
            LoadPlugins();
            LoadServerTable();

            Button_RefreshPlugins.IsEnabled = true;
            ProgressRing_LoadPlugins.Visibility = Visibility.Collapsed;
        }

        public void LoadServerTable()
        {
            string[] livePlayerData = new string[MAX_SERVER + 1];
            foreach (ServerTable item in ServerGrid.Items)
            {
                livePlayerData[int.Parse(item.ID)] = item.Maxplayers;
            }

            ServerTable selectedrow = (ServerTable)ServerGrid.SelectedItem;
            ServerGrid.Items.Clear();

            //Add server to datagrid
            for (int i = 1; i <= MAX_SERVER; i++)
            {
                string serverid_path = Path.Combine(WGSM_PATH, "servers", i.ToString());
                if (!Directory.Exists(serverid_path)) { continue; }

                string configpath = ServerPath.GetServersConfigs(i.ToString(), "WindowsGSM.cfg");
                if (!File.Exists(configpath)) { continue; }

                ServerConfig serverConfig = new(i.ToString());

                //If Game server not exist return
                if (GameServer.Data.Class.Get(serverConfig.ServerGame, pluginList: PluginsList) == null) { continue; }

                string status;
                switch (GetServerMetadata(i).ServerStatus)
                {
                    case ServerStatus.Started: status = "已啟動"; break;
                    case ServerStatus.Starting: status = "啟動中"; break;
                    case ServerStatus.Stopped: status = "已停止"; break;
                    case ServerStatus.Stopping: status = "停止中"; break;
                    case ServerStatus.Restarted: status = "已重啟"; break;
                    case ServerStatus.Restarting: status = "重啟中"; break;
                    case ServerStatus.Updated: status = "已更新"; break;
                    case ServerStatus.Updating: status = "更新中"; break;
                    case ServerStatus.Backuped: status = "已備份"; break;
                    case ServerStatus.Backuping: status = "備份中"; break;
                    case ServerStatus.Restored: status = "已還原"; break;
                    case ServerStatus.Restoring: status = "還原中"; break;
                    case ServerStatus.Deleting: status = "刪除中"; break;
                    default:
                        {
                            _serverMetadata[i].ServerStatus = ServerStatus.Stopped;
                            status = "已停止";
                            break;
                        }
                }

                try
                {
                    string icon = GameServer.Data.Icon.ResourceManager.GetString(serverConfig.ServerGame);
                    if (icon == null)
                    {
                        PluginsList.ForEach(delegate (PluginMetadata plugin)
                        {
                            if (plugin.FullName == serverConfig.ServerGame && plugin.IsLoaded)
                            {
                                icon = plugin.GameImage == PluginManagement.DefaultPluginImage
                                    ? plugin.GameImage.Replace("pack://application:,,,", "/WindowsGSM;component")
                                    : plugin.GameImage;
                            }
                        });
                    }
                    icon ??= PluginManagement.DefaultPluginImage.Replace("pack://application:,,,", "/WindowsGSM;component");

                    string serverId = i.ToString();
                    string pidString = string.Empty;

                    try
                    {
                        pidString = Process.GetProcessById(ServerCache.GetPID(serverId)).Id.ToString();
                    }
                    catch { }

                    ServerTable server = new() {
                        ID = i.ToString(),
                        PID = pidString,
                        Game = serverConfig.ServerGame,
                        Icon = icon,
                        Status = status,
                        Name = serverConfig.ServerName,
                        IP = serverConfig.ServerIP,
                        Port = serverConfig.ServerPort,
                        QueryPort = serverConfig.ServerQueryPort,
                        Defaultmap = serverConfig.ServerMap,
                        Maxplayers = (GetServerMetadata(i).ServerStatus != ServerStatus.Started) ? serverConfig.ServerMaxPlayer : livePlayerData[i]
                    };

                    SaveServerConfigToServerMetadata(i, serverConfig);
                    ServerGrid.Items.Add(server);

                    if (selectedrow != null)
                    {
                        if (selectedrow.ID == server.ID)
                        {
                            ServerGrid.SelectedItem = server;
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            grid_action.Visibility = (ServerGrid.Items.Count != 0) ? Visibility.Visible : Visibility.Hidden;
            label_select.Visibility = grid_action.Visibility == Visibility.Hidden ? Visibility.Visible : Visibility.Hidden;
        }

        private static void SaveServerConfigToServerMetadata(object serverId, ServerConfig serverConfig)
        {
            int i = int.Parse(serverId.ToString());

            // Basic Game Server Settings
            _serverMetadata[i].AutoRestart = serverConfig.AutoRestart;
            _serverMetadata[i].AutoStart = serverConfig.AutoStart;
            _serverMetadata[i].AutoUpdate = serverConfig.AutoUpdate;
            _serverMetadata[i].UpdateOnStart = serverConfig.UpdateOnStart;
            _serverMetadata[i].BackupOnStart = serverConfig.BackupOnStart;

            // Discord Alert Settings
            _serverMetadata[i].DiscordAlert = serverConfig.DiscordAlert;
            _serverMetadata[i].DiscordMessage = serverConfig.DiscordMessage;
            _serverMetadata[i].DiscordWebhook = serverConfig.DiscordWebhook;
            _serverMetadata[i].AutoRestartAlert = serverConfig.AutoRestartAlert;
            _serverMetadata[i].AutoStartAlert = serverConfig.AutoStartAlert;
            _serverMetadata[i].AutoUpdateAlert = serverConfig.AutoUpdateAlert;
            _serverMetadata[i].AutoIpUpdateAlert = serverConfig.AutoIpUpdate;
            _serverMetadata[i].SkipUserSetup = serverConfig.SkipUserSetup;
            _serverMetadata[i].RestartCrontabAlert = serverConfig.RestartCrontabAlert;
            _serverMetadata[i].CrashAlert = serverConfig.CrashAlert;

            // Restart Crontab Settings
            _serverMetadata[i].RestartCrontab = serverConfig.RestartCrontab;
            _serverMetadata[i].CrontabFormat = serverConfig.CrontabFormat;

            // Game Server Start Priority and Affinity
            _serverMetadata[i].CPUPriority = serverConfig.CPUPriority;
            _serverMetadata[i].CPUAffinity = serverConfig.CPUAffinity;

            _serverMetadata[i].EmbedConsole = serverConfig.EmbedConsole;
            _serverMetadata[i].ShowConsole = serverConfig.ShowConsole;
            _serverMetadata[i].AutoScroll = serverConfig.AutoScroll;
        }

        private async void AutoStartServer()
        {
            foreach (ServerTable server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                int serverId = int.Parse(server.ID);

                if (GetServerMetadata(serverId).AutoStart && GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped)
                {
                    await GameServer_Start(server, " | 自動啟動");

                    if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started)
                    {
                        if (GetServerMetadata(serverId).DiscordAlert && GetServerMetadata(serverId).AutoStartAlert)
                        {
                            DiscordWebhook webhook = new(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
                            await webhook.Send(server.ID, server.Game, "已啟動 | 自動啟動", server.Name, await GetPublicIP(), server.Port);
                            _latestWebhookSend = GetServerMetadata(serverId).ServerStatus;
                        }
                    }
                }
            }
        }


        private async Task SendCurrentPublicIPs()
        {
            string currentPublicIp = await GetPublicIP();
            foreach (ServerTable server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                try
                {
                    int serverId = int.Parse(server.ID);
                    Process p = GetServerMetadata(server.ID).Process;
                    if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started || GetServerMetadata(server.ID).ServerStatus == ServerStatus.Starting)
                    {

                        if (GetServerMetadata(serverId).DiscordAlert && GetServerMetadata(server.ID).AutoIpUpdateAlert)
                        {
                            if (_serverMetadata[serverId].CurrentPublicIp == string.Empty || _serverMetadata[serverId].CurrentPublicIp != currentPublicIp)
                            {
                                DiscordWebhook webhook = new(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
                                await webhook.Send(server.ID, server.Game, "目前公網 IP", server.Name, currentPublicIp, server.Port);
                                _serverMetadata[serverId].CurrentPublicIp = currentPublicIp;
                            }
                        }
                    }
                }
                catch (Exception) { }
            }
        }

        private async void StartServerTableRefresh()
        {
            while (true)
            {
                await Task.Delay(60 * 1000);
                ServerGrid.Items.Refresh();
            }
        }

        private async void StartConsoleRefresh()
        {
            while (true)
            {
                await Task.Delay(10);
                ServerTable row = (ServerTable)ServerGrid.SelectedItem;
                if (row != null)
                {
                    string text = GetServerMetadata(int.Parse(row.ID)).ServerConsole.Get();
                    if (text.Length != console.Text.Length && text != console.Text)
                    {
                        console.Text = text;

                        if (GetServerMetadata(row.ID).AutoScroll)
                        {
                            console.ScrollToEnd();
                        }
                    }
                }
            }
        }

        private async void StartDashBoardRefresh()
        {
            SystemMetrics system = new();

            // Get CPU info and Set
            await Task.Run(() => system.GetCPUStaticInfo());
            dashboard_cpu_type.Content = system.CPUType;

            // Get RAM info and Set
            await Task.Run(() => system.GetRAMStaticInfo());
            dashboard_ram_type.Content = system.RAMType;

            // Get Disk info and Set
            await Task.Run(() => system.GetDiskStaticInfo());
            dashboard_disk_name.Content = $"({system.DiskName})";
            dashboard_disk_type.Content = system.DiskType;

            while (true)
            {
                dashboard_cpu_bar.Value = await Task.Run(() => SystemMetrics.GetCPUUsage());
                dashboard_cpu_bar.Value = (dashboard_cpu_bar.Value > 100.0) ? 100.0 : dashboard_cpu_bar.Value;
                dashboard_cpu_usage.Content = $"{dashboard_cpu_bar.Value}%";

                dashboard_ram_bar.Value = await Task.Run(() => system.GetRAMUsage());
                dashboard_ram_bar.Value = (dashboard_ram_bar.Value > 100.0) ? 100.0 : dashboard_ram_bar.Value;
                dashboard_ram_usage.Content = $"{string.Format("{0:0.00}", dashboard_ram_bar.Value)}%";
                dashboard_ram_ratio.Content = SystemMetrics.GetMemoryRatioString(dashboard_ram_bar.Value, system.RAMTotalSize);

                dashboard_disk_bar.Value = await Task.Run(() => system.GetDiskUsage());
                dashboard_disk_bar.Value = (dashboard_disk_bar.Value > 100.0) ? 100.0 : dashboard_disk_bar.Value;
                dashboard_disk_usage.Content = $"{string.Format("{0:0.00}", dashboard_disk_bar.Value)}%";
                dashboard_disk_ratio.Content = SystemMetrics.GetDiskRatioString(dashboard_disk_bar.Value, system.DiskTotalSize);

                dashboard_servers_bar.Value = ServerGrid.Items.Count * 100.0 / MAX_SERVER;
                dashboard_servers_bar.Value = (dashboard_servers_bar.Value > 100.0) ? 100.0 : dashboard_servers_bar.Value;
                dashboard_servers_usage.Content = $"{string.Format("{0:0.00}", dashboard_servers_bar.Value)}%";
                dashboard_servers_ratio.Content = $"{ServerGrid.Items.Count}/{MAX_SERVER}";

                int startedCount = GetStartedServerCount();
                dashboard_started_bar.Value = ServerGrid.Items.Count == 0 ? 0 : startedCount * 100.0 / ServerGrid.Items.Count;
                dashboard_started_bar.Value = (dashboard_started_bar.Value > 100.0) ? 100.0 : dashboard_started_bar.Value;
                dashboard_started_usage.Content = $"{string.Format("{0:0.00}", dashboard_started_bar.Value)}%";
                dashboard_started_ratio.Content = $"{startedCount}/{ServerGrid.Items.Count}";

                dashboard_players_count.Content = GetActivePlayers().ToString();

                Refresh_DashBoard_LiveChart();

                await Task.Delay(2000);
            }
        }

        public int GetStartedServerCount()
        {
            return ServerGrid.Items.Cast<ServerTable>().Where(s => s.Status == "已啟動").Count();
        }

        public int GetActivePlayers()
        {
            return ServerGrid.Items.Cast<ServerTable>().Where(s => s.Maxplayers != null && s.Maxplayers.Contains('/')).Sum(s => int.TryParse(s.Maxplayers.Split('/')[0], out int count) ? count : 0);
        }

        private void Refresh_DashBoard_LiveChart()
        {
            // List<(ServerType, PlayerCount)> Example: ("Ricochet Dedicated Server", 0)
            List<(string, int)> typePlayers = [.. ServerGrid.Items.Cast<ServerTable>()
                .Where(s => s.Status == "已啟動" && s.Maxplayers != null && s.Maxplayers.Contains('/'))
                .Select(s => (type: s.Game, players: int.Parse(s.Maxplayers.Split('/')[0])))
                .GroupBy(s => s.type)
                .Select(s => (type: s.Key, players: s.Sum(p => p.players)))];

            // Ajust the maxvalue of axis Y base on PlayerCount
            if (typePlayers.Count > 0)
            {
                int maxValue = typePlayers.Select(s => s.Item2).Max() + 5;
                livechart_players_axisY.MaxValue = (maxValue > 10) ? maxValue : 10;
            }

            // Update the column data if updated, if ServerType doesn't exist remove
            for (int i = 0; i < livechart_players.Series.Count; i++)
            {
                if (typePlayers.Select(t => t.Item1).Contains(livechart_players.Series[i].Title))
                {
                    int currentPlayers = typePlayers.Where(t => t.Item1 == livechart_players.Series[i].Title).Select(t => t.Item2).FirstOrDefault();
                    if (((ChartValues<int>)livechart_players.Series[i].Values)[0] != currentPlayers)
                    {
                        livechart_players.Series[i].Values[0] = currentPlayers;
                    }

                    typePlayers.Remove((livechart_players.Series[i].Title, currentPlayers));
                }
                else
                {
                    livechart_players.Series.RemoveAt(i--);
                }
            }

            // Add ServerType Series if not exist
            foreach ((string, int) item in typePlayers)
            {
                livechart_players.Series.Add(new ColumnSeries
                {
                    Title = item.Item1,
                    Values = new ChartValues<int> { item.Item2 }
                });
            }
        }

        private static async void SendGoogleAnalytics()
        {
            GoogleAnalytics analytics = new();
            analytics.SendWindowsOS();
            analytics.SendWindowsGSMVersion();
            analytics.SendProcessorName();
            analytics.SendRAM();
            analytics.SendDisk();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save height and width
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue("Height", Height.ToString());
                key?.SetValue("Width", Width.ToString());
            }

            // Get rid of system tray icon
            notifyIcon.Visible = false;
            notifyIcon.Dispose();

            // Stop Discord Bot
            g_DiscordBot.Stop().ConfigureAwait(false);
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServerGrid.SelectedIndex != -1)
            {
                DataGrid_RefreshElements();
            }
        }

        private void DataGrid_RefreshElements()
        {
            ServerTable row = (ServerTable)ServerGrid.SelectedItem;

            if (row != null)
            {
#if DEBUG
                Console.WriteLine("Datagrid Changed");
#endif
                if (GetServerMetadata(row.ID).ServerStatus == ServerStatus.Stopped)
                {
                    button_Start.IsEnabled = true;
                    button_Stop.IsEnabled = false;
                    button_Restart.IsEnabled = false;
                    button_Console.IsEnabled = false;
                    button_Update.IsEnabled = true;
                    button_Backup.IsEnabled = true;

                    textbox_servercommand.IsEnabled = false;
                    button_servercommand.IsEnabled = false;
                }
                else if (GetServerMetadata(row.ID).ServerStatus == ServerStatus.Started)
                {
                    button_Start.IsEnabled = false;
                    button_Stop.IsEnabled = true;
                    button_Restart.IsEnabled = true;
                    Process p = GetServerMetadata(row.ID).Process;
                    try
                    {
                        button_Console.IsEnabled = p != null && !p.HasExited && !(p.StartInfo.CreateNoWindow || p.StartInfo.RedirectStandardOutput);
                    }
                    catch (Exception)
                    {
                        button_Console.IsEnabled = false;
                    }
                    button_Update.IsEnabled = false;
                    button_Backup.IsEnabled = false;

                    textbox_servercommand.IsEnabled = true;
                    button_servercommand.IsEnabled = true;
                }
                else
                {
                    button_Start.IsEnabled = false;
                    button_Stop.IsEnabled = false;
                    button_Restart.IsEnabled = false;
                    button_Console.IsEnabled = false;
                    button_Update.IsEnabled = false;
                    button_Backup.IsEnabled = false;

                    textbox_servercommand.IsEnabled = false;
                    button_servercommand.IsEnabled = false;
                }

                button_Kill.IsEnabled = GetServerMetadata(row.ID).ServerStatus switch {
                    ServerStatus.Restarting or ServerStatus.Restarted or ServerStatus.Started or ServerStatus.Starting or ServerStatus.Stopping => true,
                    _ => false,
                };
                button_ManageAddons.IsEnabled = ServerAddon.IsGameSupportManageAddons(row.Game);
                if (GetServerMetadata(row.ID).ServerStatus == ServerStatus.Deleting || GetServerMetadata(row.ID).ServerStatus == ServerStatus.Restoring)
                {
                    button_ManageAddons.IsEnabled = false;
                }

                slider_ProcessPriority.Value = Functions.CPU.Priority.GetPriorityInteger(GetServerMetadata(row.ID).CPUPriority);
                textBox_ProcessPriority.Text = Functions.CPU.Priority.GetPriorityByInteger((int)slider_ProcessPriority.Value);

                textBox_SetAffinity.Text = Functions.CPU.Affinity.GetAffinityValidatedString(GetServerMetadata(row.ID).CPUAffinity);
                string affinity = new([.. textBox_SetAffinity.Text.Reverse()]);
                for (int i = 0; i < _checkBoxes.Count; i++)
                {
                    _checkBoxes[i].IsChecked = affinity[i] == '1';
                }

                button_Status.Content = row.Status.ToUpper();
                button_Status.Background = (GetServerMetadata(row.ID).ServerStatus == ServerStatus.Started) ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Orange;

                dynamic gameServer = GameServer.Data.Class.Get(row.Game, pluginList: PluginsList);
                switch_embedconsole.IsEnabled = gameServer.AllowsEmbedConsole;
                switch_embedconsole.IsOn = gameServer.AllowsEmbedConsole ? GetServerMetadata(row.ID).EmbedConsole : false;
                Button_AutoScroll.Content = GetServerMetadata(row.ID).AutoScroll ? "✔️ 自動捲動" : "❌ 自動捲動";

                switch_autorestart.IsOn = GetServerMetadata(row.ID).AutoRestart;
                switch_restartcrontab.IsOn = GetServerMetadata(row.ID).RestartCrontab;
                switch_autostart.IsOn = GetServerMetadata(row.ID).AutoStart;
                switch_autoupdate.IsOn = GetServerMetadata(row.ID).AutoUpdate;
                switch_updateonstart.IsOn = GetServerMetadata(row.ID).UpdateOnStart;
                switch_backuponstart.IsOn = GetServerMetadata(row.ID).BackupOnStart;
                switch_discordalert.IsOn = GetServerMetadata(row.ID).DiscordAlert;
                button_discordtest.IsEnabled = switch_discordalert.IsOn;

                textBox_restartcrontab.Text = GetServerMetadata(row.ID).CrontabFormat;
                textBox_nextcrontab.Text = CrontabSchedule.TryParse(textBox_restartcrontab.Text)?.GetNextOccurrence(DateTime.Now).ToString("yyyy/MM/dd/ddd HH:mm:ss");

                MahAppSwitch_AutoStartAlert.IsOn = GetServerMetadata(row.ID).AutoStartAlert;
                MahAppSwitch_AutoRestartAlert.IsOn = GetServerMetadata(row.ID).AutoRestartAlert;
                MahAppSwitch_AutoUpdateAlert.IsOn = GetServerMetadata(row.ID).AutoUpdateAlert;
                MahAppSwitch_RestartCrontabAlert.IsOn = GetServerMetadata(row.ID).RestartCrontabAlert;
                MahAppSwitch_CrashAlert.IsOn = GetServerMetadata(row.ID).CrashAlert;
            }
        }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            if (ServerGrid.Items.Count >= MAX_SERVER)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            MahAppFlyout_InstallGameServer.IsOpen = true;

            if (!progressbar_InstallProgress.IsIndeterminate)
            {
                textbox_InstallServerName.IsEnabled = true;
                comboBox_InstallGameServer.IsEnabled = true;
                progressbar_InstallProgress.IsIndeterminate = false;
                textblock_InstallProgress.Text = string.Empty;
                button_Install.IsEnabled = true;

                ComboBox_InstallGameServer_SelectionChanged(sender, null);

                ServerConfig newServerConfig = new(null);
                textbox_InstallServerName.Text = $"WindowsGSM - 伺服器 #{newServerConfig.ServerID}";
            }
        }

        private async void Button_Install_Click(object sender, RoutedEventArgs e)
        {
            if (Installer != null)
            {
                if (!Installer.HasExited) { Installer.Kill(); }
                Installer = null;
            }

            Images.Row selectedgame = (Images.Row)comboBox_InstallGameServer.SelectedItem;
            if (string.IsNullOrWhiteSpace(textbox_InstallServerName.Text) || selectedgame == null) { return; }

            ServerConfig newServerConfig = new(null);
            string installPath = ServerPath.GetServersServerFiles(newServerConfig.ServerID);

            if (Directory.Exists(installPath))
            {
                Log(newServerConfig.ServerID, "[錯誤] 伺服器資料夾已存在, 但是無法在 WGSM 內中載入. " +
                    "你有可能搞砸了 Windowsgsm.cfg, 或更換了伺服器的外掛." +
                    "請手動備份並考慮移除, 然後重新安裝伺服器.");
                for (int i = int.Parse(newServerConfig.ServerID); i < 100; i++)
                {
                    newServerConfig = new ServerConfig(i.ToString());
                    installPath = ServerPath.GetServersServerFiles(newServerConfig.ServerID);
                    if (!Directory.Exists(installPath))
                    {
                        break;
                    }
                }

                Log(newServerConfig.ServerID, "[注意] 發現一個空閒 ServerID");
            }

            Directory.CreateDirectory(installPath);

            //Installation start
            textbox_InstallServerName.IsEnabled = false;
            comboBox_InstallGameServer.IsEnabled = false;
            progressbar_InstallProgress.IsIndeterminate = true;
            textblock_InstallProgress.Text = "安裝中";
            button_Install.IsEnabled = false;
            textbox_InstallLog.Text = string.Empty;

            string servername = textbox_InstallServerName.Text;
            string servergame = selectedgame.Name;

            newServerConfig.CreateServerDirectory();

            dynamic gameServer = GameServer.Data.Class.Get(servergame, newServerConfig, PluginsList);
            Installer = await gameServer.Install();

            if (Installer != null)
            {
                //Wait installer exit. Example: steamcmd.exe
                await Task.Run(() =>
                {
                    StreamReader reader = Installer.StandardOutput;
                    while (!reader.EndOfStream)
                    {
                        string nextLine = reader.ReadLine();
                        if (nextLine.Contains("正在登入使用者 "))
                        {
                            nextLine += Environment.NewLine + "請發送登入 Token:";
                        }

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            textbox_InstallLog.AppendText(nextLine + Environment.NewLine);
                            textbox_InstallLog.ScrollToEnd();
                        });
                    }

                    Installer?.WaitForExit();
                });
            }

            if (gameServer.IsInstallValid())
            {
                newServerConfig.ServerIP = ServerConfig.GetIPAddress();
                newServerConfig.ServerPort = newServerConfig.GetAvailablePort(gameServer.Port, gameServer.PortIncrements);

                // Create WindowsGSM.cfg
                newServerConfig.SetData(servergame, servername, gameServer);
                newServerConfig.CreateWindowsGSMConfig();

                // Create WindowsGSM.cfg and game server config
                try
                {
                    gameServer = GameServer.Data.Class.Get(servergame, newServerConfig, PluginsList);
                    gameServer.CreateServerCFG();
                }
                catch
                {
                    // ignore
                }

                LoadServerTable();
                Log(newServerConfig.ServerID, "安裝: 成功");

                MahAppFlyout_InstallGameServer.IsOpen = false;
                textbox_InstallServerName.IsEnabled = true;
                comboBox_InstallGameServer.IsEnabled = true;
                progressbar_InstallProgress.IsIndeterminate = false;

                if (MahAppSwitch_SendStatistics.IsOn)
                {
                    GoogleAnalytics analytics = new();
                    analytics.SendGameServerInstall(newServerConfig.ServerID, servergame);
                }
            }
            else
            {
                textbox_InstallServerName.IsEnabled = true;
                comboBox_InstallGameServer.IsEnabled = true;
                progressbar_InstallProgress.IsIndeterminate = false;
                textblock_InstallProgress.Text = "安裝";
                button_Install.IsEnabled = true;

                textblock_InstallProgress.Text = Installer != null ? "安裝失敗 [錯誤] 退出代碼: " + Installer.ExitCode : $"安裝失敗 [錯誤] {gameServer.Error}";
            }
        }

        private void ComboBox_InstallGameServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Set the elements visibility of Install Server Flyout
            Images.Row selectedgame = (Images.Row)comboBox_InstallGameServer.SelectedItem;
            button_InstallSetAccount.IsEnabled = false;
            textBox_InstallToken.Visibility = Visibility.Hidden;
            button_InstallSendToken.Visibility = Visibility.Hidden;
            if (selectedgame == null) { return; }

            try
            {
                dynamic gameServer = GameServer.Data.Class.Get(selectedgame.Name, pluginList: PluginsList);
                if (!gameServer.loginAnonymous)
                {
                    button_InstallSetAccount.IsEnabled = true;
                    textBox_InstallToken.Visibility = Visibility.Visible;
                    button_InstallSendToken.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                // ignore
            }
        }

        private void Button_SetAccount_Click(object sender, RoutedEventArgs e)
        {
            //Installer.SteamCMD steamCMD = new();
            WindowsGSM.Installer.SteamCMD.CreateUserDataTxtIfNotExist();

            string userDataPath = ServerPath.GetBin("steamcmd", "userData.txt");
            if (File.Exists(userDataPath))
            {
                Process.Start(userDataPath);
            }
        }

        private void Button_SendToken_Click(object sender, RoutedEventArgs e)
        {
            Installer?.StandardInput.WriteLine(textBox_InstallToken.Text);

            textBox_InstallToken.Text = string.Empty;
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            if (ServerGrid.Items.Count >= MAX_SERVER)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            MahAppFlyout_ImportGameServer.IsOpen = true;

            if (!progressbar_ImportProgress.IsIndeterminate)
            {
                textbox_ImportServerName.IsEnabled = true;
                comboBox_ImportGameServer.IsEnabled = true;
                progressbar_ImportProgress.IsIndeterminate = false;
                textblock_ImportProgress.Text = string.Empty;
                button_Import.Content = "匯入";

                ServerConfig newServerConfig = new(null);
                textbox_ImportServerName.Text = $"WindowsGSM - 伺服器 #{newServerConfig.ServerID}";
            }
        }

        private async void Button_Import_Click(object sender, RoutedEventArgs e)
        {
            Images.Row selectedgame = (Images.Row)comboBox_ImportGameServer.SelectedItem;
            label_ServerDirWarn.Content = Directory.Exists(textbox_ServerDir.Text) ? string.Empty : "伺服器資料夾無效";
            if (string.IsNullOrWhiteSpace(textbox_ImportServerName.Text) || selectedgame == null) { return; }

            string servername = textbox_ImportServerName.Text;
            string servergame = selectedgame.Name;

            ServerConfig newServerConfig = new(null);
            dynamic gameServer = GameServer.Data.Class.Get(servergame, newServerConfig, PluginsList);

            if (!gameServer.IsImportValid(textbox_ServerDir.Text))
            {
                label_ServerDirWarn.Content = gameServer.Error;
                return;
            }

            string importPath = ServerPath.GetServersServerFiles(newServerConfig.ServerID);
            if (Directory.Exists(importPath))
            {
                try
                {
                    Directory.Delete(importPath, true);
                }
                catch
                {
                    System.Windows.Forms.MessageBox.Show(importPath + " 無法存取!", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            //Import start
            textbox_ImportServerName.IsEnabled = false;
            comboBox_ImportGameServer.IsEnabled = false;
            textbox_ServerDir.IsEnabled = false;
            button_Browse.IsEnabled = false;
            progressbar_ImportProgress.IsIndeterminate = true;
            textblock_ImportProgress.Text = "匯入中";

            string sourcePath = textbox_ServerDir.Text;
            string importLog = await Task.Run(() =>
            {
                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(sourcePath, importPath);

                    // Scary error while moving the directory, some files may lost - Risky
                    //Microsoft.VisualBasic.FileIO.FileSystem.MoveDirectory(sourcePath, importPath);

                    // This doesn't work on cross drive - Not working on cross drive
                    //Directory.Move(sourcePath, importPath);

                    return null;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            });

            if (importLog != null)
            {
                textbox_ImportServerName.IsEnabled = true;
                comboBox_ImportGameServer.IsEnabled = true;
                textbox_ServerDir.IsEnabled = true;
                button_Browse.IsEnabled = true;
                progressbar_ImportProgress.IsIndeterminate = false;
                textblock_ImportProgress.Text = "[錯誤] 匯入失敗";
                MessageBox.Show($"複製資料夾失敗.\n{textbox_ServerDir.Text}\n到\n{importPath}\n\n你可以嘗試安裝新的伺服器，並將舊檔案複製到新伺服器\n\n例外: {importLog}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Create WindowsGSM.cfg
            newServerConfig.SetData(servergame, servername, gameServer);
            newServerConfig.CreateWindowsGSMConfig();

            LoadServerTable();
            Log(newServerConfig.ServerID, "匯入: 成功");

            MahAppFlyout_ImportGameServer.IsOpen = false;
            textbox_ImportServerName.IsEnabled = true;
            comboBox_ImportGameServer.IsEnabled = true;
            textbox_ServerDir.IsEnabled = true;
            button_Browse.IsEnabled = true;
            progressbar_ImportProgress.IsIndeterminate = false;
            textblock_ImportProgress.Text = string.Empty;
        }

        private void Button_Browse_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new();
            folderBrowserDialog.ShowDialog();

            if (!string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
            {
                textbox_ServerDir.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            MessageBoxResult result = MessageBox.Show("刪除這個伺服器?\n(沒有回頭路)", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) { return; }

            await GameServer_Delete(server);
        }

        private async void Button_DiscordEdit_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string webhookUrl = ServerConfig.GetSetting(server.ID, Functions.ServerConfig.SettingName.DiscordWebhook);

            MetroDialogSettings settings = new() {
                AffirmativeButtonText = "儲存",
                DefaultText = webhookUrl
            };

            webhookUrl = await this.ShowInputAsync("Discord Webhook 連結", "請輸入 discord webhook 連結", settings);
            if (webhookUrl == null) { return; } //If pressed cancel

            _serverMetadata[int.Parse(server.ID)].DiscordWebhook = webhookUrl;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.DiscordWebhook, webhookUrl);
        }

        private async void Button_DiscordSetMessage_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string message = ServerConfig.GetSetting(server.ID, ServerConfig.SettingName.DiscordMessage);

            MetroDialogSettings settings = new() {
                AffirmativeButtonText = "儲存",
                DefaultText = message
            };

            message = await this.ShowInputAsync("Discord 自定訊息", "請輸入自訂訊息\n\n例如 ping message <@discorduserid>:\n<@348921660361146380>", settings);
            if (message == null) { return; } //If pressed cancel

            _serverMetadata[int.Parse(server.ID)].DiscordMessage = message;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.DiscordMessage, message);
        }

        private async void Button_DiscordWebhookTest_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            int serverId = int.Parse(server.ID);
            if (!GetServerMetadata(serverId).DiscordAlert) { return; }

            DiscordWebhook webhook = new(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
            await webhook.Send(server.ID, server.Game, "Webhook 測試警報", server.Name, await GetPublicIP(), server.Port);
        }

        private async void Button_ServerCommand_Click(object sender, RoutedEventArgs e)
        {
            string command = textbox_servercommand.Text;
            textbox_servercommand.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(command)) { return; }

            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            await SendCommandAsync(server, command);
        }

        private void Textbox_ServerCommand_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (textbox_servercommand.Text.Length != 0)
                {
                    GetServerMetadata(0).ServerConsole.Add(textbox_servercommand.Text);
                }

                Button_ServerCommand_Click(this, new RoutedEventArgs());
            }
        }

        private void Textbox_ServerCommand_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.IsDown && e.Key == Key.Up)
            {
                e.Handled = true;
                textbox_servercommand.Text = GetServerMetadata(0).ServerConsole.GetPreviousCommand();
            }
            else if (e.IsDown && e.Key == Key.Down)
            {
                e.Handled = true;
                textbox_servercommand.Text = GetServerMetadata(0).ServerConsole.GetNextCommand();
            }
        }

        #region Actions - Button Click
        private void Actions_Crash_Click(object sender, RoutedEventArgs e)
        {
            int test = 0;
            _ = 0 / test; // Crash
        }

        private async void Actions_Start_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            // Reload WindowsGSM.cfg on start
            SaveServerConfigToServerMetadata(server.ID, new ServerConfig(server.ID));

            await GameServer_Start(server);
        }

        private async void Actions_Stop_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            await GameServer_Stop(server);
        }

        private async void Actions_Restart_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            await GameServer_Restart(server);
        }

        private async void Actions_Kill_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            switch (GetServerMetadata(server.ID).ServerStatus)
            {
                case ServerStatus.Restarting:
                case ServerStatus.Restarted:
                case ServerStatus.Started:
                case ServerStatus.Starting:
                case ServerStatus.Stopping:
                    Process p = GetServerMetadata(server.ID).Process;
                    if (p != null && !p.HasExited)
                    {
                        Log(server.ID, "操作: 強制關閉");
                        p.Kill();

                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                        Log(server.ID, "伺服器: 強制關閉");
                        SetServerStatus(server, "已停止");
                        _serverMetadata[int.Parse(server.ID)].ServerConsole.Clear();
                        _serverMetadata[int.Parse(server.ID)].Process = null;
                    }

                    break;
            }
        }

        private void Actions_ToggleConsole_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            Process p = GetServerMetadata(server.ID).Process;
            if (p == null) { return; }

            //If console is useless, return
            //if (p.StartInfo.RedirectStandardOutput) { return; }
            _serverMetadata[int.Parse(server.ID)].ShowConsole = !_serverMetadata[int.Parse(server.ID)].ShowConsole;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ShowConsole, GetServerMetadata(server.ID).ShowConsole ? "1" : "0");

            WindowShowStyle style = _serverMetadata[int.Parse(server.ID)].ShowConsole ? WindowShowStyle.ShowNormal : WindowShowStyle.Hide;
            IntPtr hWnd = GetServerMetadata(server.ID).MainWindow;

            //ShowWindow(hWnd, ShowWindow(hWnd, WindowShowStyle.Hide) ? WindowShowStyle.Hide : WindowShowStyle.ShowNormal);
            ShowWindow(hWnd, WindowShowStyle.Hide);
            Thread.Sleep(500);
            ShowWindow(hWnd, style);
        }

        private async void Actions_StartAllServers_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerTable server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped)
                {
                    await GameServer_Start(server);
                }
            }
        }

        private async void Actions_StartServersWithAutoStartEnabled_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerTable server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped && GetServerMetadata(server.ID).AutoStart)
                {
                    await GameServer_Start(server);
                }
            }
        }

        private async void Actions_StopAllServers_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerTable server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started)
                {
                    await GameServer_Stop(server);
                }
            }
        }

        private async void Actions_RestartAllServers_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerTable server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started)
                {
                    await GameServer_Restart(server);
                }
            }
        }

        private async void Actions_Update_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            MessageBoxResult result = System.Windows.MessageBox.Show("更新這個伺服器?", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) { return; }

            await GameServer_Update(server);
        }

        private async void Actions_UpdateValidate_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            MessageBoxResult result = System.Windows.MessageBox.Show("驗證這個伺服器?", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) { return; }

            await GameServer_Update(server, notes: " | 驗證", validate: true);
        }

        private async void Actions_Backup_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            MessageBoxResult result = System.Windows.MessageBox.Show("備份這個伺服器?", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) { return; }

            await GameServer_Backup(server);
        }

        private async void Actions_RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            listbox_RestoreBackup.Items.Clear();
            BackupConfig backupConfig = new(server.ID);
            if (Directory.Exists(backupConfig.BackupLocation))
            {
                string zipFileName = $"WGSM-Backup-Server-{server.ID}-";
                foreach (FileInfo fi in new DirectoryInfo(backupConfig.BackupLocation).GetFiles("*.zip").Where(x => x.Name.Contains(zipFileName)).OrderByDescending(x => x.LastWriteTime))
                {
                    listbox_RestoreBackup.Items.Add(fi.Name);
                }
            }

            if (listbox_RestoreBackup.Items.Count > 0)
            {
                listbox_RestoreBackup.SelectedIndex = 0;
            }

            label_RestoreBackupServerName.Content = server.Name;
            MahAppFlyout_RestoreBackup.IsOpen = true;
        }

        private async void Button_RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            if (listbox_RestoreBackup.SelectedIndex >= 0)
            {
                MahAppFlyout_RestoreBackup.IsOpen = false;
                await GameServer_RestoreBackup(server, listbox_RestoreBackup.SelectedItem.ToString());
            }
        }

        private void Actions_ManageAddons_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            ListBox_ManageAddons_Refresh();
            ToggleMahappFlyout(MahAppFlyout_ManageAddons);
        }
        #endregion

        private void ListBox_ManageAddonsLeft_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listBox_ManageAddonsLeft.SelectedItem != null)
            {
                ServerTable server = (ServerTable)ServerGrid.SelectedItem;
                if (server == null) { return; }

                string item = listBox_ManageAddonsLeft.SelectedItem.ToString();
                listBox_ManageAddonsLeft.Items.Remove(listBox_ManageAddonsLeft.Items[listBox_ManageAddonsLeft.SelectedIndex]);
                listBox_ManageAddonsRight.Items.Add(item);
                ServerAddon serverAddon = new(server.ID, server.Game);
                serverAddon.AddToRight([.. listBox_ManageAddonsRight.Items.OfType<string>()], item);

                ListBox_ManageAddons_Refresh();

                foreach (object selected in listBox_ManageAddonsRight.Items)
                {
                    if (selected.ToString() == item)
                    {
                        listBox_ManageAddonsRight.SelectedItem = selected;
                    }
                }
            }
        }

        private void ListBox_ManageAddonsRight_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listBox_ManageAddonsRight.SelectedItem != null)
            {
                ServerTable server = (ServerTable)ServerGrid.SelectedItem;
                if (server == null) { return; }

                string item = listBox_ManageAddonsRight.SelectedItem.ToString();
                listBox_ManageAddonsRight.Items.Remove(listBox_ManageAddonsRight.Items[listBox_ManageAddonsRight.SelectedIndex]);
                listBox_ManageAddonsLeft.Items.Add(item);
                ServerAddon serverAddon = new(server.ID, server.Game);
                serverAddon.AddToLeft([.. listBox_ManageAddonsRight.Items.OfType<string>()], item);

                ListBox_ManageAddons_Refresh();

                foreach (object selected in listBox_ManageAddonsLeft.Items)
                {
                    if (selected.ToString() == item)
                    {
                        listBox_ManageAddonsLeft.SelectedItem = selected;
                    }
                }
            }
        }

        private void ListBox_ManageAddons_Refresh()
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            ServerAddon serverAddon = new(server.ID, server.Game);
            label_ManageAddonsName.Content = server.Name;
            label_ManageAddonsGame.Content = server.Game;
            label_ManageAddonsType.Content = serverAddon.GetModsName();

            listBox_ManageAddonsLeft.Items.Clear();
            foreach (string item in serverAddon.GetLeftListBox())
            {
                listBox_ManageAddonsLeft.Items.Add(item);
            }

            listBox_ManageAddonsRight.Items.Clear();
            foreach (string item in serverAddon.GetRightListBox())
            {
                listBox_ManageAddonsRight.Items.Add(item);
            }
        }

        public async Task<dynamic> Server_BeginStart(ServerTable server)
        {
            dynamic gameServer = GameServer.Data.Class.Get(server.Game, new ServerConfig(server.ID), PluginsList);
            if (gameServer == null) { return null; }

            //End All Running Process
            await EndAllRunningProcess(server.ID);
            await Task.Delay(500);

            //Add Start File to WindowsFirewall before start
            string startPath = ServerPath.GetServersServerFiles(server.ID, gameServer.StartPath);
            if (!string.IsNullOrWhiteSpace(gameServer.StartPath))
            {
                WindowsFirewall firewall = new(Path.GetFileName(startPath), startPath);
                if (!await firewall.IsRuleExist())
                {
                    await firewall.AddRule();
                }
            }

            gameServer.AllowsEmbedConsole = GetServerMetadata(server.ID).EmbedConsole;
            Process p = await gameServer.Start();
            //Fail to start
            if (p == null)
            {
                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "伺服器: 啟動失敗");
                Log(server.ID, "[錯誤] " + gameServer.Error);
                SetServerStatus(server, "已停止");

                return null;
            }

            _serverMetadata[int.Parse(server.ID)].Process = p;
            p.Exited += (sender, e) => OnGameServerExited(server);

            await Task.Run(() =>
            {
                try
                {
                    if (!p.StartInfo.CreateNoWindow)
                    {
                        while (!p.HasExited && !ShowWindow(p.MainWindowHandle, WindowShowStyle.Minimize))
                        {
                            Thread.Sleep(500);
                            //Debug.WriteLine("Try Setting ShowMinNoActivate Console Window");
                        }

                        Debug.WriteLine("Set ShowMinNoActivate Console Window");

                        //Save MainWindow
                        _serverMetadata[int.Parse(server.ID)].MainWindow = p.MainWindowHandle;
                    }

                    p.WaitForInputIdle(10000);

                    ShowWindow(p.MainWindowHandle, WindowShowStyle.Hide);
                    Thread.Sleep(500);
                    ShowWindow(p.MainWindowHandle, _serverMetadata[int.Parse(server.ID)].ShowConsole ? WindowShowStyle.ShowNormal : WindowShowStyle.Hide);

                }
                catch
                {
                    Debug.WriteLine("No Window require to hide");
                }
            });

            //An error may occur on ShowWindow if not adding this
            if (p == null || p.HasExited)
            {
                _serverMetadata[int.Parse(server.ID)].Process = null;

                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "伺服器: 啟動失敗");
                Log(server.ID, "[錯誤] 退出代碼: " + p.ExitCode.ToString());
                SetServerStatus(server, "已停止");

                return null;
            }

            // Set Priority
            p = Functions.CPU.Priority.SetProcessWithPriority(p, Functions.CPU.Priority.GetPriorityInteger(GetServerMetadata(server.ID).CPUPriority));

            // Set Affinity
            try
            {
                p.ProcessorAffinity = Functions.CPU.Affinity.GetAffinityIntPtr(GetServerMetadata(server.ID).CPUAffinity);
            }
            catch (Exception e)
            {
                Log(server.ID, $"[注意] 設定親和性失敗. ({e.Message})");
            }

            // Save Cache
            ServerCache.SavePID(server.ID, p.Id);
            ServerCache.SaveProcessName(server.ID, p.ProcessName);
            ServerCache.SaveWindowsIntPtr(server.ID, GetServerMetadata(server.ID).MainWindow);

            _ = SetWindowText(p.MainWindowHandle, server.Name);

            ShowWindow(p.MainWindowHandle, _serverMetadata[int.Parse(server.ID)].ShowConsole ? WindowShowStyle.ShowNormal : WindowShowStyle.Hide);

            StartAutoUpdateCheck(server);

            StartRestartCrontabCheck(server);

            StartSendHeartBeat(server);

            StartQuery(server);

            if (MahAppSwitch_SendStatistics.IsOn)
            {
                GoogleAnalytics analytics = new();
                analytics.SendGameServerStart(server.ID, server.Game);
            }

            return gameServer;
        }

        private static bool InvariantTryParse(string input, out double value) => double.TryParse(input.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);

        public async Task<bool> Server_BeginStop(ServerTable server, Process p)
        {
            _serverMetadata[int.Parse(server.ID)].Process = null;

            dynamic gameServer = GameServer.Data.Class.Get(server.Game, pluginList: PluginsList);
            await gameServer.Stop(p);

            for (int i = 0; i < 10; i++)
            {
                if (p == null || p.HasExited) { break; }
                await Task.Delay(1000);
            }

            _serverMetadata[int.Parse(server.ID)].ServerConsole.Clear();

            // Save Cache
            ServerCache.SavePID(server.ID, -1);
            ServerCache.SaveProcessName(server.ID, string.Empty);
            ServerCache.SaveWindowsIntPtr(server.ID, (IntPtr)0);

            if (p != null && !p.HasExited)
            {
                p.Kill();
                return false;
            }

            return true;
        }

        private async Task<(Process, string, dynamic)> Server_BeginUpdate(ServerTable server, bool silenceCheck, bool forceUpdate, bool validate = false, string custum = null)
        {
            dynamic gameServer = GameServer.Data.Class.Get(server.Game, new ServerConfig(server.ID), PluginsList);

            string localVersion = gameServer.GetLocalBuild();
            if (string.IsNullOrWhiteSpace(localVersion) && !silenceCheck)
            {
                Log(server.ID, $"[注意] {gameServer.Error}");
            }

            string remoteVersion = await gameServer.GetRemoteBuild();
            if (string.IsNullOrWhiteSpace(remoteVersion) && !silenceCheck)
            {
                Log(server.ID, $"[注意] {gameServer.Error}");
            }

            if (!silenceCheck)
            {
                Log(server.ID, $"檢查中: 版本 ({localVersion}) => ({remoteVersion})");
            }

            if ((!string.IsNullOrWhiteSpace(localVersion) && !string.IsNullOrWhiteSpace(remoteVersion) && localVersion != remoteVersion) || forceUpdate)
            {
                try
                {
                    return (await gameServer.Update(validate, custum), remoteVersion, gameServer);
                }
                catch
                {
                    return (await gameServer.Update(), remoteVersion, gameServer);
                }
            }

            return (null, remoteVersion, gameServer);
        }

        #region Actions - Game Server
        private async Task GameServer_Start(ServerTable server, string notes = "")
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            string error = string.Empty;
            if (!string.IsNullOrWhiteSpace(server.IP) && !IsValidIPAddress(server.IP))
            {
                error += " IP 位址無效.";
            }

            if (!string.IsNullOrWhiteSpace(server.Port) && !IsValidPort(server.Port))
            {
                error += " 連接埠無效.";
            }

            if (error != string.Empty)
            {
                Log(server.ID, "伺服器: 啟動失敗");
                Log(server.ID, "[錯誤] " + error);

                return;
            }

            Process p = GetServerMetadata(server.ID).Process;
            if (p != null) { return; }

            if (GetServerMetadata(server.ID).BackupOnStart)
            {
                await GameServer_Backup(server, " | 啟動時備份");
            }

            if (GetServerMetadata(server.ID).UpdateOnStart)
            {
                await GameServer_Update(server, " | 啟動時更新");
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Starting;
            Log(server.ID, "操作: 啟動" + notes);
            SetServerStatus(server, "啟動中");

            dynamic gameServer = await Server_BeginStart(server);
            if (gameServer == null)
            {
                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "伺服器: 啟動失敗");
                SetServerStatus(server, "已停止");
                return;
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Started;
            Log(server.ID, "伺服器: 已啟動");
            if (!string.IsNullOrWhiteSpace(gameServer.Notice))
            {
                Log(server.ID, "[注意] " + gameServer.Notice);
            }
            SetServerStatus(server, "已啟動", ServerCache.GetPID(server.ID).ToString());
        }

        private async Task GameServer_Stop(ServerTable server)
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Started) { return; }

            Process p = GetServerMetadata(server.ID).Process;
            if (p == null) { return; }

            //Begin stop
            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopping;
            Log(server.ID, "操作: 停止");
            SetServerStatus(server, "停止中");

            bool stopGracefully = await Server_BeginStop(server, p);

            Log(server.ID, "伺服器: 已停止");
            if (!stopGracefully)
            {
                Log(server.ID, "[注意] 伺服器非正常停止");
            }
            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
            SetServerStatus(server, "已停止");
        }

        private async Task GameServer_Restart(ServerTable server)
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Started) { return; }

            Process p = GetServerMetadata(server.ID).Process;
            if (p == null) { return; }

            _serverMetadata[int.Parse(server.ID)].Process = null;

            //Begin Restart
            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Restarting;
            Log(server.ID, "操作: 重啟");
            SetServerStatus(server, "重啟中");

            await Server_BeginStop(server, p);

            await Task.Delay(500);

            if (GetServerMetadata(server.ID).UpdateOnStart)
            {
                await GameServer_Update(server, " | 啟動時更新");
            }

            await Task.Delay(500);

            dynamic gameServer = await Server_BeginStart(server);
            if (gameServer == null)
            {
                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                SetServerStatus(server, "已停止");
                return;
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Started;
            Log(server.ID, "伺服器: 已重啟");
            if (!string.IsNullOrWhiteSpace(gameServer.Notice))
            {
                Log(server.ID, "[注意] " + gameServer.Notice);
            }
            SetServerStatus(server, "已啟動", ServerCache.GetPID(server.ID).ToString());
        }

        public async Task<bool> GameServer_Update(ServerTable server, string notes = "", bool validate = false)
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped)
            {
                return false;
            }

            //Begin Update
            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Updating;
            Log(server.ID, "操作: 更新" + notes);
            SetServerStatus(server, "更新中");

            (Process p, string remoteVersion, dynamic gameServer) = await Server_BeginUpdate(server, silenceCheck: validate, forceUpdate: true, validate: validate);

            if (p == null && string.IsNullOrEmpty(gameServer.Error)) // Update success (non-steamcmd server)
            {
                Log(server.ID, $"伺服器: 已更新 {(validate ? "驗證 " : string.Empty)}({remoteVersion})");
            }
            else if (p != null) // p stores process of steamcmd
            {
                await Task.Run(() => { p.WaitForExit(); });
                Log(server.ID, $"伺服器: 已更新 {(validate ? "驗證 " : string.Empty)}({remoteVersion})");
            }
            else
            {
                Log(server.ID, "伺服器: 更新失敗");
                Log(server.ID, "[錯誤] " + gameServer.Error);
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
            SetServerStatus(server, "已停止");

            return true;
        }

        private async Task<bool> GameServer_Backup(ServerTable server, string notes = "")
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped)
            {
                return false;
            }

            //Begin backup
            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Backuping;
            Log(server.ID, "操作: 備份" + notes);
            SetServerStatus(server, "備份中");

            //End All Running Process
            await EndAllRunningProcess(server.ID);
            await Task.Delay(1000);

            string backupLocation = ServerPath.GetBackups(server.ID);
            if (!Directory.Exists(backupLocation))
            {
                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "伺服器: 備份失敗");
                Log(server.ID, "[錯誤] 找不到備份位置");
                SetServerStatus(server, "已停止");
                return false;
            }

            string zipFileName = $"WGSM-Backup-Server-{server.ID}-";

            // Remove the oldest Backup file
            BackupConfig backupConfig = new(server.ID);
            foreach (FileInfo fi in new DirectoryInfo(backupLocation).GetFiles("*.zip").Where(x => x.Name.Contains(zipFileName)).OrderByDescending(x => x.LastWriteTime).Skip(backupConfig.MaximumBackups - 1))
            {
                string ex = string.Empty;
                await Task.Run(() =>
                {
                    try
                    {
                        fi.Delete();
                    }
                    catch (Exception e)
                    {
                        ex = e.Message;
                    }
                });

                if (ex != string.Empty)
                {
                    _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                    Log(server.ID, "伺服器: 備份失敗");
                    Log(server.ID, $"[錯誤] {ex}");
                    SetServerStatus(server, "已停止");
                    return false;
                }
            }

            string startPath = ServerPath.GetServers(server.ID);
            string zipFile = Path.Combine(ServerPath.GetBackups(server.ID), $"{zipFileName}{DateTime.Now:yyyyMMddHHmmss}.zip");

            string error = string.Empty;
            await Task.Run(() =>
            {
                try
                {
                    ZipFile.CreateFromDirectory(startPath, zipFile);
                }
                catch (Exception e)
                {
                    error = e.Message;
                }
            });

            if (error != string.Empty)
            {
                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "伺服器: 備份失敗");
                Log(server.ID, $"[錯誤] {error}");
                SetServerStatus(server, "已停止");

                return false;
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
            Log(server.ID, "伺服器: 已備份");
            SetServerStatus(server, "已停止");

            return true;
        }

        private async Task<bool> GameServer_RestoreBackup(ServerTable server, string backupFile)
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped)
            {
                return false;
            }

            string backupLocation = ServerPath.GetBackups(server.ID);
            string backupPath = Path.Combine(backupLocation, backupFile);
            if (!File.Exists(backupPath))
            {
                Log(server.ID, "伺服器: 還原備份失敗");
                Log(server.ID, "[錯誤] 找不到備份");
                return false;
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Restoring;
            Log(server.ID, "操作: 還原備份");
            SetServerStatus(server, "還原中");

            string extractPath = ServerPath.GetServers(server.ID);
            if (Directory.Exists(extractPath))
            {
                string ex = string.Empty;
                await Task.Run(() =>
                {
                    try
                    {
                        Directory.Delete(extractPath, true);
                    }
                    catch (Exception e)
                    {
                        ex = e.Message;
                    }
                });

                if (ex != string.Empty)
                {
                    _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                    Log(server.ID, "伺服器: 還原備份失敗");
                    Log(server.ID, $"[錯誤] {ex}");
                    SetServerStatus(server, "已停止");
                    return false;
                }
            }

            string error = string.Empty;
            await Task.Run(() =>
            {
                try
                {
                    ZipFile.ExtractToDirectory(backupPath, extractPath);
                }
                catch (Exception e)
                {
                    error = e.Message;
                }
            });

            if (error != string.Empty)
            {
                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "伺服器: 還原備份失敗");
                Log(server.ID, $"[錯誤] {error}");
                SetServerStatus(server, "已停止");
                return false;
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
            Log(server.ID, "伺服器: 已還原");
            SetServerStatus(server, "已停止");

            return true;
        }

        private async Task<bool> GameServer_Delete(ServerTable server)
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped)
            {
                return false;
            }

            //Begin delete
            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Deleting;
            Log(server.ID, "操作: 刪除");
            SetServerStatus(server, "刪除中");

            //Remove firewall rule
            WindowsFirewall firewall = new(null, ServerPath.GetServers(server.ID));
            firewall.RemoveRuleEx();

            //End All Running Process
            await EndAllRunningProcess(server.ID);
            await Task.Delay(1000);

            string serverPath = ServerPath.GetServers(server.ID);

            await Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(serverPath))
                    {
                        Directory.Delete(serverPath, true);
                    }
                }
                catch
                {

                }
            });

            await Task.Delay(1000);

            if (Directory.Exists(serverPath))
            {
                string wgsmCfgPath = ServerPath.GetServersConfigs(server.ID, "WindowsGSM.cfg");
                if (File.Exists(wgsmCfgPath))
                {
                    Log(server.ID, "伺服器: 刪除伺服器失敗");
                    Log(server.ID, "[錯誤] 無法存取資料夾");

                    _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                    SetServerStatus(server, "已停止");

                    return false;
                }
            }

            Log(server.ID, "伺服器: 已刪除伺服器");

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
            SetServerStatus(server, "已停止");

            LoadServerTable();

            return true;
        }
        #endregion

        private async void OnGameServerExited(ServerTable server)
        {
            if (System.Windows.Application.Current == null) { return; }

            await System.Windows.Application.Current.Dispatcher.Invoke(async () =>
            {
                int serverId = int.Parse(server.ID);

                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started)
                {
                    bool autoRestart = GetServerMetadata(serverId).AutoRestart;
                    _serverMetadata[int.Parse(server.ID)].ServerStatus = autoRestart ? ServerStatus.Restarting : ServerStatus.Stopped;
                    Log(server.ID, "伺服器: 當機");
                    SetServerStatus(server, autoRestart ? "重啟中" : "已停止");

                    if (GetServerMetadata(serverId).DiscordAlert && GetServerMetadata(serverId).CrashAlert)
                    {
                        if (CheckWebhookThreshold(ref _lastCrashTime) || _latestWebhookSend != ServerStatus.Crashed)
                        {
                            DiscordWebhook webhook = new(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
                            await webhook.Send(server.ID, server.Game, "當機", server.Name, await GetPublicIP(), server.Port);
                            _latestWebhookSend = ServerStatus.Crashed;
                        }
                    }

                    _serverMetadata[int.Parse(server.ID)].Process = null;

                    if (autoRestart)
                    {
                        if (GetServerMetadata(server.ID).BackupOnStart)
                        {
                            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                            await GameServer_Backup(server, " | 啟動時備份");
                        }

                        if (GetServerMetadata(server.ID).UpdateOnStart)
                        {
                            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                            await GameServer_Update(server, " | 啟動時更新");
                        }

                        dynamic gameServer = await Server_BeginStart(server);
                        if (gameServer == null)
                        {
                            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                            return;
                        }

                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Started;
                        Log(server.ID, "伺服器: 已啟動 | 自動重啟");
                        if (!string.IsNullOrWhiteSpace(gameServer.Notice))
                        {
                            Log(server.ID, "[注意] " + gameServer.Notice);
                        }
                        SetServerStatus(server, "已啟動", ServerCache.GetPID(server.ID).ToString());

                        if (GetServerMetadata(serverId).DiscordAlert && GetServerMetadata(serverId).AutoRestartAlert)
                        {
                            //Only send Webhook_Start if there wasn't a retry in the last X min
                            if (CheckWebhookThreshold(ref _lastAutoRestartTime))
                            {
                                DiscordWebhook webhook = new(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
                                await webhook.Send(server.ID, server.Game, "已啟動 | 自動重啟", server.Name, await GetPublicIP(), server.Port);
                                _latestWebhookSend = GetServerMetadata(serverId).ServerStatus;
                            }
                        }
                    }
                }
            });
        }

        const int UPDATE_INTERVAL_MINUTE = 30;
        const int IP_UPDATE_INTERVAL_MINUTE = 2;
        private async void StartAutpIpUpdate()
        {
            await Task.Run(async () =>
            {
                await Task.Delay(30000); //delay initial check so the servers can start
                while (true)
                {
                    await SendCurrentPublicIPs();
                    await Task.Delay(60000 * IP_UPDATE_INTERVAL_MINUTE); //check every minute
                }
            });
        }
        private async void StartAutoUpdateCheck(ServerTable server)
        {
            int serverId = int.Parse(server.ID);

            //Save the process of game server
            Process p = GetServerMetadata(server.ID).Process;

            dynamic gameServer = GameServer.Data.Class.Get(server.Game, new ServerConfig(server.ID), PluginsList);

            string localVersion = gameServer.GetLocalBuild();

            while (p != null && !p.HasExited)
            {
                await Task.Delay(60000 * UPDATE_INTERVAL_MINUTE);

                if (!GetServerMetadata(server.ID).AutoUpdate || GetServerMetadata(server.ID).ServerStatus == ServerStatus.Updating)
                {
                    continue;
                }

                if (p == null || p.HasExited) { break; }

                //Try to get local build again if not found just now
                if (string.IsNullOrWhiteSpace(localVersion))
                {
                    localVersion = gameServer.GetLocalBuild();
                }

                //Get remote build
                string remoteVersion = await gameServer.GetRemoteBuild();

                //Continue if success to get localVersion and remoteVersion
                if (!string.IsNullOrWhiteSpace(localVersion) && !string.IsNullOrWhiteSpace(remoteVersion))
                {
                    if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Started)
                    {
                        break;
                    }

                    Log(server.ID, $"檢查中: 版本 ({localVersion}) => ({remoteVersion})");

                    if (localVersion != remoteVersion)
                    {
                        _serverMetadata[int.Parse(server.ID)].Process = null;

                        //Begin stop
                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopping;
                        SetServerStatus(server, "已停止");

                        //Stop the server
                        await Server_BeginStop(server, p);

                        if (p != null && !p.HasExited)
                        {
                            p.Kill();
                        }

                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Updating;
                        SetServerStatus(server, "更新中");

                        //Update the server
                        await gameServer.Update();

                        if (string.IsNullOrWhiteSpace(gameServer.Error))
                        {
                            Log(server.ID, $"伺服器: 已更新 ({remoteVersion})");

                            if (GetServerMetadata(serverId).DiscordAlert && GetServerMetadata(serverId).AutoUpdateAlert)
                            {
                                DiscordWebhook webhook = new(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
                                await webhook.Send(server.ID, server.Game, "已更新 | 自動更新", server.Name, await GetPublicIP(), server.Port);
                                _latestWebhookSend = GetServerMetadata(serverId).ServerStatus;
                            }
                        }
                        else
                        {
                            Log(server.ID, "伺服器: 更新失敗");
                            Log(server.ID, "[錯誤] " + gameServer.Error);
                        }

                        //Start the server
                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Starting;
                        SetServerStatus(server, "啟動中");

                        dynamic gameServerStart = await Server_BeginStart(server);
                        if (gameServerStart == null) { return; }

                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Started;
                        SetServerStatus(server, "已啟動", ServerCache.GetPID(server.ID).ToString());

                        break;
                    }
                }
                else if (string.IsNullOrWhiteSpace(localVersion))
                {
                    Log(server.ID, $"[注意] 無法取得本機建置.");
                }
                else if (string.IsNullOrWhiteSpace(remoteVersion))
                {
                    Log(server.ID, $"[注意] 無法取得遠端建置.");
                }
            }
        }

        private async void StartRestartCrontabCheck(ServerTable server)
        {
            CrontabManager crontabManager = new(this, server, GetServerMetadata(server.ID).Process);

            await crontabManager.MainLoop();
        }

        private async void StartSendHeartBeat(ServerTable server)
        {
            //Save the process of game server
            Process p = GetServerMetadata(server.ID).Process;

            while (p != null && !p.HasExited)
            {
                if (MahAppSwitch_SendStatistics.IsOn)
                {
                    GoogleAnalytics analytics = new();
                    analytics.SendGameServerHeartBeat(server.Game, server.Name);
                }

                await Task.Delay(300000);
            }
        }

        private async void StartQuery(ServerTable server)
        {
            if (string.IsNullOrWhiteSpace(server.IP) || string.IsNullOrWhiteSpace(server.QueryPort)) { return; }

            // Check the server support Query Method
            dynamic gameServer = GameServer.Data.Class.Get(server.Game, pluginList: PluginsList);
            if (gameServer == null) { return; }
            if (gameServer.QueryMethod == null) { return; }

            // Save the process of game server
            Process p = GetServerMetadata(server.ID).Process;

            // Query server every 5 seconds
            while (p != null && !p.HasExited)
            {
                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped)
                {
                    break;
                }

                if (!IsValidIPAddress(server.IP) || !IsValidPort(server.QueryPort))
                {
                    continue;
                }


                IQueryTemplate query = gameServer.QueryMethod as IQueryTemplate;
                query.SetAddressPort(server.IP, int.Parse(server.QueryPort));
                try
                {
                    string players = await query.GetPlayersAndMaxPlayers();

                    if (players != null)
                    {
                        server.Maxplayers = players;

                        for (int i = 0; i < ServerGrid.Items.Count; i++)
                        {
                            if (server.ID == ((ServerTable)ServerGrid.Items[i]).ID)
                            {
                                int selectedIndex = ServerGrid.SelectedIndex;
                                ServerGrid.Items[i] = server;
                                ServerGrid.SelectedIndex = selectedIndex;
                                ServerGrid.Items.Refresh();
                                break;
                            }
                        }
                    }
                }
                catch { }
                try
                {
                    List<PlayerData> playerData = await query.GetPlayersData();
                    if(playerData != null && playerData.Count != 0)
                    {
                        if (int.TryParse(server.ID, out int serverId))
                        {
                            server.PlayerList = playerData;
                        }
                    }

                    Dictionary<string, string> serverInfo = await query.GetInfo();
                    if (serverInfo != null ) {
                        server.Defaultmap = serverInfo["Map"];
                    }
                }
                catch { }

                await Task.Delay(5000);
            }
        }

        private static async Task EndAllRunningProcess(string serverId)
        {
            await Task.Run(() =>
            {
                //LINQ query for windowsgsm old processes
                List<Process> processes = [.. (from p in Process.GetProcesses()
                                 where ((Predicate<Process>)(p_ =>
                                 {
                                     try
                                     {
                                         return p_.MainModule.FileName.Contains(Path.Combine(WGSM_PATH, "servers", serverId) + "\\");
                                     }
                                     catch
                                     {
                                         return false;
                                     }
                                 }))(p)
                                 select p)];

                // Kill all processes
                foreach (Process process in processes)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        //ignore
                    }
                }
            });
        }

        public void SetServerStatus(ServerTable server, string status, string pid = null)
        {
            server.Status = status;
            if (pid != null)
            {
                server.PID = pid;
            }
            if (status == "已停止")
            {
                server.PID = string.Empty;
            }

            if (server.Status != "已啟動" && server.Maxplayers.Contains('/'))
            {
                ServerConfig serverConfig = new(server.ID);
                server.Maxplayers = serverConfig.ServerMaxPlayer;
            }

            for (int i = 0; i < ServerGrid.Items.Count; i++)
            {
                if (server.ID == ((ServerTable)ServerGrid.Items[i]).ID)
                {
                    int selectedIndex = ServerGrid.SelectedIndex;
                    ServerGrid.Items[i] = server;
                    ServerGrid.SelectedIndex = selectedIndex;
                    ServerGrid.Items.Refresh();
                    break;
                }
            }

            DataGrid_RefreshElements();
        }

        public void Log(string serverId, string logText)
        {
            string title = int.TryParse(serverId, out int i) ? $"#{i}" : serverId;
            string log = $"[{DateTime.Now:yyyy/MM/dd-HH:mm:ss}][{title}] {logText}" + Environment.NewLine;
            string logPath = ServerPath.GetLogs();
            Directory.CreateDirectory(logPath);
            string logFile = Path.Combine(logPath, $"L{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(logFile, log);

            textBox_wgsmlog.AppendText(log);
            textBox_wgsmlog.Text = RemovedOldLog(textBox_wgsmlog.Text);
            textBox_wgsmlog.ScrollToEnd();
        }

        public void DiscordBotLog(string logText)
        {
            string log = $"[{DateTime.Now:yyyy/MM/dd-HH:mm:ss}] {logText}" + Environment.NewLine;
            string logPath = ServerPath.GetLogs();
            Directory.CreateDirectory(logPath);

            string logFile = Path.Combine(logPath, $"L{DateTime.Now:yyyyMMdd}-DiscordBot.log");
            File.AppendAllText(logFile, log);

            textBox_DiscordBotLog.AppendText(log);
            textBox_DiscordBotLog.Text = RemovedOldLog(textBox_DiscordBotLog.Text);
            textBox_DiscordBotLog.ScrollToEnd();
        }

        private static string RemovedOldLog(string logText)
        {
            const int MAX_LOG_LINE = 50;
            int lineCount = logText.Count(f => f == '\n');
            return (lineCount > MAX_LOG_LINE) ? string.Join("\n", [.. logText.Split('\n').Skip(lineCount - MAX_LOG_LINE)]) : logText;
        }

        private void Button_ClearServerConsole_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            _serverMetadata[int.Parse(server.ID)].ServerConsole.Clear();
            console.Clear();
        }

        private void Button_ClearWGSMLog_Click(object sender, RoutedEventArgs e)
        {
            textBox_wgsmlog.Clear();
        }

        public async Task<string> SendCommandAsync(ServerTable server, string command, int waitForDataInMs = 0)
        {
            Process p = GetServerMetadata(server.ID).Process;
            _ = int.TryParse(server.ID, out int id);
            if (p == null) { return ""; }

            textbox_servercommand.Focusable = false;
            _serverMetadata[id].ServerConsole.StartRecorder();
            _serverMetadata[id].ServerConsole.Input(p, command, GetServerMetadata(server.ID).MainWindow);
            textbox_servercommand.Focusable = true;

            if (waitForDataInMs != 0)
            {
                await Task.Delay(waitForDataInMs);
                return _serverMetadata[id].ServerConsole.StopRecorder();
            }
            else {
                return "已發送!";
            }
        }

        private static bool IsValidIPAddress(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return false;
            }

            string[] splitValues = ip.Split('.');
            return splitValues.Length == 4 && splitValues.All(r => byte.TryParse(r, out byte tempForParsing));
        }

        private static bool IsValidPort(string port)
        {
            return int.TryParse(port, out int portnum) && portnum > 1 && portnum < 65535;
        }

        #region Menu - Browse
        private void Browse_ServerBackups_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (Functions.ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            Process.Start("explorer", Functions.ServerPath.GetBackups(server.ID));
        }

        private void Browse_BackupFiles_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (Functions.ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            BackupConfig backupConfig = new(server.ID);
            backupConfig.Open();
        }

        private void Browse_ServerConfigs_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (Functions.ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string path = Functions.ServerPath.GetServersConfigs(server.ID);
            if (Directory.Exists(path))
            {
                Process.Start("explorer",path);
            }
        }

        private void Browse_ServerFiles_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (Functions.ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string path = Functions.ServerPath.GetServersServerFiles(server.ID);
            if (Directory.Exists(path))
            {
                Process.Start("explorer", path);
            }
        }
        #endregion

        #region Top Bar Button
        private void Button_Website_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo ps = new("https://windowsgsm.com/") {
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(ps);
        }

        private void Button_Discord_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo ps = new("https://discord.gg/bGc7t2R") {
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(ps);
        }

        private void Button_Patreon_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo ps = new("https://www.patreon.com/WindowsGSM/") {
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(ps);
        }

        private void Button_Settings_Click(object sender, RoutedEventArgs e)
        {
            ToggleMahappFlyout(MahAppFlyout_Settings);
        }

        private void Button_Hide_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(0);
            notifyIcon.Visible = false;
            notifyIcon.Visible = true;
        }
        #endregion

        #region Settings Flyout
        private void HardWareAcceleration_IsCheckedChanged(object sender, EventArgs e)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.HardWareAcceleration, MahAppSwitch_HardWareAcceleration.IsOn.ToString());
            }

            RenderOptions.ProcessRenderMode = MahAppSwitch_HardWareAcceleration.IsOn ? System.Windows.Interop.RenderMode.SoftwareOnly : System.Windows.Interop.RenderMode.Default;
        }

        private void UIAnimation_IsCheckedChanged(object sender, EventArgs e)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.UIAnimation, MahAppSwitch_UIAnimation.IsOn.ToString());
            }

            WindowTransitionsEnabled = MahAppSwitch_UIAnimation.IsOn;
        }

        private void DarkTheme_IsCheckedChanged(object sender, EventArgs e)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.DarkTheme, MahAppSwitch_DarkTheme.IsOn.ToString());
            }

            ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem ?? DEFAULT_THEME}");
        }

        private void StartOnLogin_IsCheckedChanged(object sender, EventArgs e)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.StartOnBoot, MahAppSwitch_StartOnBoot.IsOn.ToString());
            }

            SetStartOnBoot(MahAppSwitch_StartOnBoot.IsOn);
        }

        private void RestartOnCrash_IsCheckedChanged(object sender, EventArgs e)
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true);
            key?.SetValue(RegistryKeyName.RestartOnCrash, MahAppSwitch_RestartOnCrash.IsOn.ToString());
        }

        private void SendStatistics_IsCheckedChanged(object sender, EventArgs e)
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true);
            key?.SetValue(RegistryKeyName.SendStatistics, MahAppSwitch_SendStatistics.IsOn.ToString());
        }

        private static void SetStartOnBoot(bool enable)
        {
            string taskName = "WindowsGSM";
            string wgsmPath = Environment.ProcessPath;

            Process schtasks = new() {
                StartInfo =
                {
                    FileName = "schtasks",
                    Arguments = enable ? $"/create /tn {taskName} /tr \"{wgsmPath}\" /sc onlogon /delay 0000:10 /rl HIGHEST /f" : $"/delete /tn {taskName} /f",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };
            schtasks.Start();
        }
        #endregion

        #region Donor Connect
        private async void DonorConnect_IsCheckedChanged(object sender, EventArgs e)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true);

            //If switch is checked
            if (!MahAppSwitch_DonorConnect.IsOn)
            {
                g_DonorType = string.Empty;
                comboBox_Themes.SelectedItem = DEFAULT_THEME;
                comboBox_Themes.IsEnabled = false;

                //Set theme
                ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem}");

                key.SetValue(RegistryKeyName.DonorTheme, MahAppSwitch_DonorConnect.IsOn.ToString());
                key.SetValue(RegistryKeyName.DonorColor, DEFAULT_THEME);
                key.Close();
                return;
            }

            //If switch is not checked
            key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true);
            string authKey = (key.GetValue(RegistryKeyName.DonorAuthKey) == null) ? string.Empty : key.GetValue(RegistryKeyName.DonorAuthKey).ToString();

            MetroDialogSettings settings = new() {
                AffirmativeButtonText = "Activate",
                DefaultText = authKey
            };

            authKey = await this.ShowInputAsync("Donor Connect (Patreon)", "Please enter the activation key.", settings);

            //If pressed cancel or key is null or whitespace
            if (string.IsNullOrWhiteSpace(authKey))
            {
                MahAppSwitch_DonorConnect.IsOn = false;
                key.Close();
                return;
            }

            ProgressDialogController controller = await this.ShowProgressAsync("Authenticating...", "Please wait...");
            controller.SetIndeterminate();
            (bool success, string name) = await AuthenticateDonor(authKey);
            await controller.CloseAsync();

            if (success)
            {
                key.SetValue(RegistryKeyName.DonorTheme, "True");
                key.SetValue(RegistryKeyName.DonorAuthKey, authKey);
                await this.ShowMessageAsync("成功!", $"感謝你的贊助 {name}, 你的支持幫助我們!\n你可以在設定中選擇任何你想要的主題!");
            }
            else
            {
                key.SetValue(RegistryKeyName.DonorTheme, "False");
                key.SetValue(RegistryKeyName.DonorAuthKey, "");
                await this.ShowMessageAsync("啟動失敗", "請瀏覽 https://windowsgsm.com/patreon/ 取得金鑰.");

                MahAppSwitch_DonorConnect.IsOn = false;
            }
            key.Close();
        }

        private async Task<(bool, string)> AuthenticateDonor(string authKey)
        {
            try
            {
                //using WebClient webClient = new();
                string json = await App.httpClient.GetStringAsync($"https://windowsgsm.com/patreon/patreonAuth.php?auth={authKey}");
                //string json = await webClient.DownloadStringTaskAsync($"https://windowsgsm.com/patreon/patreonAuth.php?auth={authKey}");
                bool success = JObject.Parse(json)["success"].ToString() == "True";

                if (success) {
                    string name = JObject.Parse(json)["name"].ToString();
                    string type = JObject.Parse(json)["type"].ToString();

                    g_DonorType = type;

                    g_DiscordBot.SetDonorType(g_DonorType);
                    comboBox_Themes.IsEnabled = true;

                    ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem}");

                    return (true, name);
                }

                MahAppSwitch_DonorConnect.IsOn = false;

                //Set theme
                ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem}");
            }
            catch
            {
                // ignore
            }

            //Set theme
            ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem}");

            return (false, string.Empty);
        }

        private void ComboBox_Themes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.DonorColor, comboBox_Themes.SelectedItem.ToString());
            }

            //Set theme
            ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem}");
        }
        #endregion

        #region Menu - Help
        private void Help_OnlineDocumentation_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo ps = new("https://docs.windowsgsm.com") {
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(ps);
        }

        private void Help_ReportIssue_Click(object sender, RoutedEventArgs e) {
            ProcessStartInfo ps = new("https://github.com/WindowsGSM/WindowsGSM/issues") {
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(ps);
        }

        private async void Help_SoftwareUpdates_Click(object sender, RoutedEventArgs e)
        {
            ProgressDialogController controller = await this.ShowProgressAsync("檢查更新...", "請稍後...");
            controller.SetIndeterminate();
            string latestVersion = await GetLatestVersion();
            await controller.CloseAsync();

            if (string.IsNullOrEmpty(latestVersion))
            {
                await this.ShowMessageAsync("軟體更新", "更新失敗，請稍後再試");
                return;
            }

            if (latestVersion == WGSM_VERSION)
            {
                await this.ShowMessageAsync("軟體更新", "WindowsGSM 已是最新版");
                return;
            }

            MetroDialogSettings settings = new() {
                AffirmativeButtonText = "更新",
                DefaultButtonFocus = MessageDialogResult.Affirmative
            };

            MessageDialogResult result = await this.ShowMessageAsync("軟體更新", $"有新版本 {latestVersion} 可使用, 立即更新?\n\n警告: 所有伺服器會被關閉!", MessageDialogStyle.AffirmativeAndNegative, settings);

            if (result.ToString().Equals("Affirmative"))
            {
                string installPath = ServerPath.GetBin();
                Directory.CreateDirectory(installPath);

                string filePath = Path.Combine(installPath, "WindowsGSM-Updater.exe");

                if (!File.Exists(filePath))
                {
                    //Download WindowsGSM-Updater.exe
                    controller = await this.ShowProgressAsync("下載 WindowsGSM-Updater...", "請稍後...");
                    controller.SetIndeterminate();
                    _ = await DownloadWindowsGSMUpdater();
                    await controller.CloseAsync();
                }

                if (File.Exists(filePath))
                {
                    //Kill all the server
                    for (int i = 0; i <= MAX_SERVER; i++)
                    {
                        if (GetServerMetadata(i) == null || GetServerMetadata(i).Process == null)
                        {
                            continue;
                        }

                        if (!GetServerMetadata(i).Process.HasExited)
                        {
                            _serverMetadata[i].Process.Kill();
                        }
                    }

                    //Run WindowsGSM-Updater.exe
                    Process updater = new() {
                        StartInfo =
                        {
                            WorkingDirectory = installPath,
                            FileName = filePath,
                            Arguments = "-autostart -forceupdate"
                        }
                    };
                    updater.Start();

                    Close();
                }
                else
                {
                    await this.ShowMessageAsync("軟體更新", $"WindowsGSM-Updater.exe 下載失敗");
                }
            }
        }

        private static async Task<string> GetLatestVersion()
        {
            try {
                HttpResponseMessage request = await App.httpClient.GetAsync("https://api.github.com/repos/WindowsGSM/WindowsGSM/releases/latest");
                using StreamReader responseReader = new(request.Content.ReadAsStream());
                return JObject.Parse(responseReader.ReadToEnd())["tag_name"].ToString();
                //HttpWebRequest webRequest = WebRequest.Create("https://api.github.com/repos/WindowsGSM/WindowsGSM/releases/latest") as HttpWebRequest;
                //webRequest.Method = "GET";
                //webRequest.UserAgent = "Anything";
                //webRequest.ServicePoint.Expect100Continue = false;
                //WebResponse response = await webRequest.GetResponseAsync();
                //using StreamReader responseReader = new(response.GetResponseStream());
                //return JObject.Parse(responseReader.ReadToEnd())["tag_name"].ToString();
            } catch {
                return null;
            }
        }

        private static async Task<bool> DownloadWindowsGSMUpdater()
        {
            string filePath = ServerPath.GetBin("WindowsGSM-Updater.exe");

            try
            {
                Stream stream = await App.httpClient.GetStreamAsync("https://github.com/WindowsGSM/WindowsGSM-Updater/releases/latest/download/WindowsGSM-Updater.exe");
                using FileStream fileStream = File.Create(filePath);
                //using WebClient webClient = new();
                //await webClient.DownloadFileTaskAsync("https://github.com/WindowsGSM/WindowsGSM-Updater/releases/latest/download/WindowsGSM-Updater.exe", filePath);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Github.WindowsGSM-Updater.exe {e}");
            }

            return File.Exists(filePath);
        }

        private async void Help_AboutWindowsGSM_Click(object sender, RoutedEventArgs e)
        {
            MetroDialogSettings settings = new() {
                AffirmativeButtonText = "Patreon",
                NegativeButtonText = "Ok",
                DefaultButtonFocus = MessageDialogResult.Negative
            };

            MessageDialogResult result = await this.ShowMessageAsync("關於 WindowsGSM", $"產品:\t\tWindowsGSM\n版本:\t\t{WGSM_VERSION[1..]}\n作者:\t\tTatLead\n\n如果你喜歡 WindowsGSM, 請考慮贊助!", MessageDialogStyle.AffirmativeAndNegative, settings);

            if (result == MessageDialogResult.Affirmative)
            {
                Process.Start("https://www.patreon.com/WindowsGSM/");
            }
        }
        #endregion

        #region Menu - Tools
        private async void Tools_GlobalServerListCheck_Click(object sender, RoutedEventArgs e)
        {
            ServerTable row = (ServerTable)ServerGrid.SelectedItem;
            if (row == null) { return; }

            if (row.Game == GameServer.MCPE.FullName || row.Game == GameServer.MC.FullName)
            {
                Log(row.ID, $"這個功能不適用於 {row.Game}");
                return;
            }

            string publicIP = await GetPublicIP();
            if (publicIP == null)
            {
                Log(row.ID, "檢查失敗. 原因: 獲取公網 IP 失敗.");
                return;
            }

            string messageText = $"伺服器名稱: {row.Name}\n公網 IP: {publicIP}\n查詢埠: {row.QueryPort}";
            if (await GlobalServerList.IsServerOnSteamServerList(publicIP, row.QueryPort))
            {
                MessageBox.Show(messageText + "\n\n結果: 在線\n\n你的伺服器在全球伺服器名單!", "全球伺服器名單檢查", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(messageText + "\n\n結果: 離線\n\n你的伺服器沒有在全球伺服器名單.", "全球伺服器名單檢查", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Tool_InstallAMXModXMetamodP_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string messageTitle = "工具 - 安裝 AMX Mod X & MetaMod-P";

            bool? existed = InstallAddons.IsAMXModXAndMetaModPExists(server);
            if (existed == null)
            {
                await this.ShowMessageAsync(messageTitle, $"不支援 {server.Game} (ID: {server.ID})");
                return;
            }

            if (existed == true)
            {
                await this.ShowMessageAsync(messageTitle, $"已安裝 (ID: {server.ID})");
                return;
            }

            MessageDialogResult result = await this.ShowMessageAsync(messageTitle, $"確定要安裝? (ID: {server.ID})", MessageDialogStyle.AffirmativeAndNegative);
            if (result == MessageDialogResult.Affirmative)
            {
                ProgressDialogController controller = await this.ShowProgressAsync("安裝中...", "請稍後...");
                controller.SetIndeterminate();
                bool installed = await InstallAddons.AMXModXAndMetaModP(server);
                await controller.CloseAsync();

                string message = installed ? $"安裝成功" : $"安裝失敗";
                await this.ShowMessageAsync(messageTitle, $"{message} (ID: {server.ID})");
            }
        }

        private async void Tools_InstallSourcemodMetamod_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string messageTitle = "工具 - 安裝 SourceMod & MetaMod";

            bool? existed = InstallAddons.IsSourceModAndMetaModExists(server);
            if (existed == null)
            {
                await this.ShowMessageAsync(messageTitle, $"不支援 {server.Game} (ID: {server.ID})");
                return;
            }

            if (existed == true)
            {
                await this.ShowMessageAsync(messageTitle, $"已安裝 (ID: {server.ID})");
                return;
            }

            MessageDialogResult result = await this.ShowMessageAsync(messageTitle, $"確定要安裝? (ID: {server.ID})", MessageDialogStyle.AffirmativeAndNegative);
            if (result == MessageDialogResult.Affirmative)
            {
                ProgressDialogController controller = await this.ShowProgressAsync("安裝中...", "請稍後...");
                controller.SetIndeterminate();
                bool installed = await InstallAddons.SourceModAndMetaMod(server);
                await controller.CloseAsync();

                string message = installed ? $"安裝成功" : $"安裝失敗";
                await this.ShowMessageAsync(messageTitle, $"{message} (ID: {server.ID})");
            }
        }

        private async void Tools_InstallDayZSALModServer_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string messageTitle = "工具 - 安裝 DayZSAL Mod Server";

            bool? existed = InstallAddons.IsDayZSALModServerExists(server);
            if (existed == null)
            {
                await this.ShowMessageAsync(messageTitle, $"不支援 {server.Game} (ID: {server.ID})");
                return;
            }

            if (existed == true)
            {
                await this.ShowMessageAsync(messageTitle, $"已安裝 (ID: {server.ID})");
                return;
            }

            MessageDialogResult result = await this.ShowMessageAsync(messageTitle, $"確定要安裝? (ID: {server.ID})", MessageDialogStyle.AffirmativeAndNegative);
            if (result == MessageDialogResult.Affirmative)
            {
                ProgressDialogController controller = await this.ShowProgressAsync("安裝中...", "請稍後...");
                controller.SetIndeterminate();
                bool installed = await InstallAddons.DayZSALModServer(server);
                await controller.CloseAsync();

                string message = installed ? $"安裝成功" : $"安裝失敗";
                await this.ShowMessageAsync(messageTitle, $"{message} (ID: {server.ID})");
            }
        }

        private async void Tools_InstallOxideMod_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string messageTitle = "工具 - 安裝 OxideMod";

            bool? existed = InstallAddons.IsOxideModExists(server);
            if (existed == null)
            {
                await this.ShowMessageAsync(messageTitle, $"不支援 {server.Game} (ID: {server.ID})");
                return;
            }

            if (existed == true)
            {
                await this.ShowMessageAsync(messageTitle, $"已安裝 (ID: {server.ID})");
                return;
            }

            MessageDialogResult result = await this.ShowMessageAsync(messageTitle, $"確定要安裝? (ID: {server.ID})", MessageDialogStyle.AffirmativeAndNegative);
            if (result == MessageDialogResult.Affirmative)
            {
                ProgressDialogController controller = await this.ShowProgressAsync("安裝中...", "請稍後...");
                controller.SetIndeterminate();
                bool installed = await InstallAddons.OxideMod(server);
                await controller.CloseAsync();

                string message = installed ? $"安裝成功" : $"安裝失敗";
                await this.ShowMessageAsync(messageTitle, $"{message} (ID: {server.ID})");
            }
        }
        #endregion

        public static async Task<string> GetPublicIP()
        {
            try
            {
                string html = await App.httpClient.GetStringAsync("https://ipinfo.io/ip");
                return html.Replace("\n", string.Empty);
                //using WebClient webClient = new();
                //return webClient.DownloadString("https://ipinfo.io/ip").Replace("\n", string.Empty);
            }
            catch
            {
                return null;
            }
        }

        private void OnBalloonTipClick(object sender, EventArgs e)
        {
        }

        private void NotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                WindowState = WindowState.Normal;
                Show();
            }
        }

        #region Left Buttom Grid
        private void Slider_ProcessPriority_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            _serverMetadata[int.Parse(server.ID)].CPUPriority = ((int)slider_ProcessPriority.Value).ToString();
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.CPUPriority, GetServerMetadata(server.ID).CPUPriority);
            textBox_ProcessPriority.Text = Functions.CPU.Priority.GetPriorityByInteger((int)slider_ProcessPriority.Value);

            if (GetServerMetadata(server.ID).Process != null && !GetServerMetadata(server.ID).Process.HasExited)
            {
                _serverMetadata[int.Parse(server.ID)].Process = Functions.CPU.Priority.SetProcessWithPriority(GetServerMetadata(server.ID).Process, (int)slider_ProcessPriority.Value);
            }
        }

        private void Button_SetAffinity_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            ToggleMahappFlyout(MahAppFlyout_SetAffinity);
        }

        private void Button_EditConfig_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (Refresh_EditConfig_Data(server.ID))
            {
                ToggleMahappFlyout(MahAppFlyout_EditConfig);
            }
            else
            {
                MahAppFlyout_EditConfig.IsOpen = false;
            }
        }

        private bool Refresh_EditConfig_Data(string serverId)
        {
            ServerConfig serverConfig = new(serverId);
            if (string.IsNullOrWhiteSpace(serverConfig.ServerGame)) { return false; }
            dynamic gameServer = GameServer.Data.Class.Get(serverConfig.ServerGame, pluginList: PluginsList);
            if (gameServer == null) { return false; }

            textbox_EC_ServerID.Text = serverConfig.ServerID;
            textbox_EC_ServerGame.Text = serverConfig.ServerGame;
            textbox_EC_ServerName.Text = serverConfig.ServerName;
            textbox_EC_ServerIP.Text = serverConfig.ServerIP;
            numericUpDown_EC_ServerMaxplayer.Value = int.TryParse(serverConfig.ServerMaxPlayer, out int maxplayer) ? maxplayer : int.Parse(gameServer.Maxplayers);
            numericUpDown_EC_ServerPort.Value = int.TryParse(serverConfig.ServerPort, out int port) ? port : int.Parse(gameServer.Port);
            numericUpDown_EC_ServerQueryPort.Value = int.TryParse(serverConfig.ServerQueryPort, out int queryPort) ? queryPort : int.Parse(gameServer.QueryPort);
            textbox_EC_ServerMap.Text = serverConfig.ServerMap;
            textbox_EC_ServerGSLT.Text = serverConfig.ServerGSLT;
            textbox_EC_ServerParam.Text = serverConfig.ServerParam;
            return true;
        }

        private void Button_EditConfig_Save_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerGame, textbox_EC_ServerGame.Text.Trim());
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerName, textbox_EC_ServerName.Text.Trim());
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerIP, textbox_EC_ServerIP.Text.Trim());
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerMaxPlayer, numericUpDown_EC_ServerMaxplayer.Value.ToString());
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerPort, numericUpDown_EC_ServerPort.Value.ToString());
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerQueryPort, numericUpDown_EC_ServerQueryPort.Value.ToString());
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerMap, textbox_EC_ServerMap.Text.Trim());
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerGSLT, textbox_EC_ServerGSLT.Text.Trim());
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerParam, textbox_EC_ServerParam.Text.Trim());

            LoadServerTable();
            MahAppFlyout_EditConfig.IsOpen = false;
        }

        private void Button_RestartCrontab_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].RestartCrontab = switch_restartcrontab.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.RestartCrontab, GetServerMetadata(server.ID).RestartCrontab ? "1" : "0");
        }

        private void Button_EmbedConsole_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].EmbedConsole = switch_embedconsole.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.EmbedConsole, GetServerMetadata(server.ID).EmbedConsole ? "1" : "0");
        }

        private void Button_AutoRestart_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoRestart = switch_autorestart.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoRestart, GetServerMetadata(server.ID).AutoRestart ? "1" : "0");
        }

        private void Button_AutoStart_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoStart = switch_autostart.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoStart, GetServerMetadata(server.ID).AutoStart ? "1" : "0");
        }

        private void Button_AutoUpdate_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoUpdate = switch_autoupdate.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoUpdate, GetServerMetadata(server.ID).AutoUpdate ? "1" : "0");
        }

        private async void Button_DiscordAlertSettings_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            ToggleMahappFlyout(MahAppFlyout_DiscordAlert);
        }

        private void Button_UpdateOnStart_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].UpdateOnStart = switch_updateonstart.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.UpdateOnStart, GetServerMetadata(server.ID).UpdateOnStart ? "1" : "0");
        }

        private void Button_BackupOnStart_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].BackupOnStart = switch_backuponstart.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.BackupOnStart, GetServerMetadata(server.ID).BackupOnStart ? "1" : "0");
        }

        private void Button_DiscordAlert_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].DiscordAlert = switch_discordalert.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.DiscordAlert, GetServerMetadata(server.ID).DiscordAlert ? "1" : "0");
            button_discordtest.IsEnabled = GetServerMetadata(server.ID).DiscordAlert;
        }

        private async void Button_CrontabEdit_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string crontabFormat = ServerConfig.GetSetting(server.ID, ServerConfig.SettingName.CrontabFormat);

            MetroDialogSettings settings = new() {
                AffirmativeButtonText = "儲存",
                DefaultText = crontabFormat
            };

            crontabFormat = await this.ShowInputAsync("Crontab Format", "Please enter the crontab expressions", settings);
            if (crontabFormat == null) { return; } //If pressed cancel

            _serverMetadata[int.Parse(server.ID)].CrontabFormat = crontabFormat;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.CrontabFormat, crontabFormat);

            textBox_restartcrontab.Text = crontabFormat;
            textBox_nextcrontab.Text = CrontabSchedule.TryParse(crontabFormat)?.GetNextOccurrence(DateTime.Now).ToString("yyyy/MM/dd/ddd HH:mm:ss") ?? string.Empty;
        }
        #endregion

        #region Switches
        private void Switch_AutoStartAlert_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoStartAlert = MahAppSwitch_AutoStartAlert.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoStartAlert, GetServerMetadata(server.ID).AutoStartAlert ? "1" : "0");
        }

        private void Switch_AutoRestartAlert_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoRestartAlert = MahAppSwitch_AutoRestartAlert.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoRestartAlert, GetServerMetadata(server.ID).AutoRestartAlert ? "1" : "0");
        }

        private void Switch_AutoUpdateAlert_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoUpdateAlert = MahAppSwitch_AutoUpdateAlert.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoUpdateAlert, GetServerMetadata(server.ID).AutoUpdateAlert ? "1" : "0");
        }

        private void Switch_RestartCrontabAlert_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].RestartCrontabAlert = MahAppSwitch_RestartCrontabAlert.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.RestartCrontabAlert, GetServerMetadata(server.ID).RestartCrontabAlert ? "1" : "0");
        }

        private void Switch_CrashAlert_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].CrashAlert = MahAppSwitch_CrashAlert.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.CrashAlert, GetServerMetadata(server.ID).CrashAlert ? "1" : "0");
        }

        private void Switch_AutoIpUpdate_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoIpUpdateAlert = MahAppSwitch_AutoIpUpdate.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoIpUpdateAlert, GetServerMetadata(server.ID).AutoIpUpdateAlert ? "1" : "0");
        }

        private void Switch_SkipUserSetup_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].SkipUserSetup = MahAppSwitch_SkipUserSetup.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.SkipUserSetup, GetServerMetadata(server.ID).SkipUserSetup ? "1" : "0");
        }
        #endregion

        private async void Window_Activated(object sender, EventArgs e)
        {
            if (MahAppFlyout_ManageAddons.IsOpen)
            {
                ListBox_ManageAddons_Refresh();
            }

            // Fix the windows cannot toggle issue because of LoadServerTable
            await Task.Delay(1);

            if (ShowActivated)
            {
                LoadServerTable();
            }
        }

        #region Discord Bot
        private async void Switch_DiscordBot_Toggled(object sender, RoutedEventArgs e)
        {
            if (!switch_DiscordBot.IsEnabled) { return; }

            if (switch_DiscordBot.IsOn)
            {
                switch_DiscordBot.IsEnabled = false;
                button_DiscordBotInvite.IsEnabled = switch_DiscordBot.IsOn = await g_DiscordBot.Start();
                DiscordBotLog("Discord Bot " + (switch_DiscordBot.IsOn ? "已啟動." : "啟動失敗. 原因: Bot Token 無效."));
                switch_DiscordBot.IsEnabled = true;
            }
            else
            {
                button_DiscordBotInvite.IsEnabled = switch_DiscordBot.IsEnabled = false;
                await g_DiscordBot.Stop();
                DiscordBotLog("Discord Bot 已停止.");
                switch_DiscordBot.IsEnabled = true;
            }
        }

        private void Button_DiscordBotPrefixEdit_Click(object sender, RoutedEventArgs e)
        {
            if (button_DiscordBotPrefixEdit.Content.ToString() == "Edit")
            {
                button_DiscordBotPrefixEdit.Content = "儲存";
                textBox_DiscordBotPrefix.IsEnabled = true;
                textBox_DiscordBotName.IsEnabled = true;
                textBox_DiscordBotPrefix.Focus();
                textBox_DiscordBotPrefix.SelectAll();
            }
            else
            {
                button_DiscordBotPrefixEdit.Content = "Edit";
                textBox_DiscordBotPrefix.IsEnabled = false;
                textBox_DiscordBotName.IsEnabled = false;

                DiscordBot.Configs.SetBotPrefix(textBox_DiscordBotPrefix.Text);
                DiscordBot.Configs.SetBotName(textBox_DiscordBotName.Text);

                label_DiscordBotCommands.Content = DiscordBot.Configs.GetCommandsList();
            }
        }

        private void Button_DiscordBotTokenEdit_Click(object sender, RoutedEventArgs e)
        {
            if (button_DiscordBotTokenEdit.Content.ToString() == "Edit")
            {
                rectangle_DiscordBotTokenSpoiler.Visibility = Visibility.Hidden;
                button_DiscordBotTokenEdit.Content = "儲存";
                textBox_DiscordBotToken.IsEnabled = true;
                textBox_DiscordBotToken.Focus();
                textBox_DiscordBotToken.SelectAll();
            }
            else
            {
                rectangle_DiscordBotTokenSpoiler.Visibility = Visibility.Visible;
                button_DiscordBotTokenEdit.Content = "Edit";
                textBox_DiscordBotToken.IsEnabled = false;
                DiscordBot.Configs.SetBotToken(textBox_DiscordBotToken.Text);
            }
        }

        /*
        private void Button_DiscordBotDashboardEdit_Click(object sender, RoutedEventArgs e)
        {
            if (button_DiscordBotDashboardEdit.Content.ToString() == "Edit")
            {
                button_DiscordBotDashboardEdit.Content = "儲存";
                textBox_DiscordBotDashboard.IsEnabled = true;
                textBox_DiscordBotDashboard.Focus();
                textBox_DiscordBotDashboard.SelectAll();
            }
            else
            {
                button_DiscordBotDashboardEdit.Content = "Edit";
                textBox_DiscordBotDashboard.IsEnabled = false;
                DiscordBot.Configs.SetDashboardChannel(textBox_DiscordBotDashboard.Text);
            }
        }
       

        private void NumericUpDown_DiscordRefreshRate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            double rate = numericUpDown_DiscordRefreshRate.Value ?? 5;
            DiscordBot.Configs.SetDashboardRefreshRate((int)rate);
        }
        */

        private async void Button_DiscordBotAddID_Click(object sender, RoutedEventArgs e)
        {
            MetroDialogSettings settings = new() {
                AffirmativeButtonText = "Add"
            };

            string newAdminID = await this.ShowInputAsync("Add Admin ID", "Please enter the discord user ID.", settings);
            if (newAdminID == null) { return; } //If pressed cancel

            List<(string, string)> adminList = DiscordBot.Configs.GetBotAdminList();
            adminList.Add((newAdminID, "0"));
            DiscordBot.Configs.SetBotAdminList(adminList);
            Refresh_DiscordBotAdminList(listBox_DiscordBotAdminList.SelectedIndex);
        }

        private async void Button_DiscordBotEditServerID_Click(object sender, RoutedEventArgs e)
        {
            AdminListItem adminListItem = (DiscordBot.AdminListItem)listBox_DiscordBotAdminList.SelectedItem;
            if (adminListItem == null) { return; }

            MetroDialogSettings settings = new() {
                AffirmativeButtonText = "儲存",
                DefaultText = adminListItem.ServerIds
            };

            string example = "0 - Grant All servers Permission.\n\nExamples:\n0\n1,2,3,4,5\n";
            string newServerIds = await this.ShowInputAsync($"Edit Server IDs ({adminListItem.AdminId})", $"Please enter the server Ids where admin has access to the server.\n{example}", settings);
            if (newServerIds == null) { return; } //If pressed cancel

            List<(string, string)> adminList = DiscordBot.Configs.GetBotAdminList();
            for (int i = 0; i < adminList.Count; i++)
            {
                if (adminList[i].Item1 == adminListItem.AdminId)
                {
                    adminList.RemoveAt(i);
                    adminList.Insert(i, (adminListItem.AdminId, newServerIds.Trim()));
                    break;
                }
            }
            DiscordBot.Configs.SetBotAdminList(adminList);
            Refresh_DiscordBotAdminList(listBox_DiscordBotAdminList.SelectedIndex);
        }

        private void Button_DiscordBotRemoveID_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_DiscordBotAdminList.SelectedIndex >= 0)
            {
                List<(string, string)> adminList = DiscordBot.Configs.GetBotAdminList();
                try
                {
                    adminList.RemoveAt(listBox_DiscordBotAdminList.SelectedIndex);
                }
                catch
                {
                    Console.WriteLine($"Fail to delete item {listBox_DiscordBotAdminList.SelectedIndex} in adminIDs.txt");
                }
                DiscordBot.Configs.SetBotAdminList(adminList);

                listBox_DiscordBotAdminList.Items.Remove(listBox_DiscordBotAdminList.Items[listBox_DiscordBotAdminList.SelectedIndex]);
            }
        }

        public void Refresh_DiscordBotAdminList(int selectIndex = 0)
        {
            listBox_DiscordBotAdminList.Items.Clear();
            foreach ((string adminID, string serverIDs) in DiscordBot.Configs.GetBotAdminList())
            {
                listBox_DiscordBotAdminList.Items.Add(new DiscordBot.AdminListItem { AdminId = adminID, ServerIds = serverIDs });
            }
            listBox_DiscordBotAdminList.SelectedIndex = listBox_DiscordBotAdminList.Items.Count >= 0 ? selectIndex : -1;
        }

        private static bool CheckWebhookThreshold(ref long lastWebhookTimeInMs)
        {
            bool ret = false;
            //init counter
            if (lastWebhookTimeInMs == 0)
            {
                lastWebhookTimeInMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                return true;
            }
            if ((DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastWebhookTimeInMs) > _webhookThresholdTimeInMs)
                ret = true;

            //reset counter
            lastWebhookTimeInMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            return ret;
        }

        public int GetServerCount()
        {
            return ServerGrid.Items.Count;
        }

        public List<(string, string, string)> GetServerList()
        {
            List<(string, string, string)> list = [];

            for (int i = 0; i < ServerGrid.Items.Count; i++)
            {
                ServerTable server = (ServerTable)ServerGrid.Items[i];
                list.Add((server.ID, server.Status, server.Name));
            }

            return list;
        }

        public List<(string, string, string)> GetServerList(string userId)
        {
            List<string> serverIds = Configs.GetServerIdsByAdminId(userId);
            List<ServerTable> serverList = [.. ServerGrid.Items.Cast<ServerTable>()];

            return serverIds.Contains("0")
                ? [.. serverList.Select(server => (server.ID, server.Status, server.Name))]
                : [.. serverList
                .Where(server => serverIds.Contains(server.ID))
                .Select(server => (server.ID, server.Status, server.Name))];
        }

        public List<(string, string, string)> GetServerListByUserId(string userId)
        {
            List<string> serverIds = Configs.GetServerIdsByAdminId(userId);
            List<ServerTable> serverList = [.. ServerGrid.Items.Cast<ServerTable>()];

            return serverIds.Contains("0")
                ? [.. serverList.Select(server => (server.ID, server.Status, server.Name))]
                : [.. serverList
                .Where(server => serverIds.Contains(server.ID))
                .Select(server => (server.ID, server.Status, server.Name))];
        }

        public bool IsServerExist(string serverId)
        {
            for (int i = 0; i < ServerGrid.Items.Count; i++)
            {
                ServerTable server = (ServerTable)ServerGrid.Items[i];
                if (server.ID == serverId) { return true; }
            }

            return false;
        }

        public static ServerStatus GetServerStatus(string serverId)
        {
            return GetServerMetadata(serverId).ServerStatus;
        }

        public string GetServerName(string serverId)
        {
            ServerTable server = GetServerTableById(serverId);
            return server?.Name ?? string.Empty;
        }

        public ServerTable GetServerTableById(string serverId)
        {
            for (int i = 0; i < ServerGrid.Items.Count; i++)
            {
                ServerTable server = (ServerTable)ServerGrid.Items[i];
                if (server.ID == serverId) { return server; }
            }

            return null;
        }

        public async Task<bool> StartServerById(string serverId, string adminID, string adminName)
        {
            ServerTable server = GetServerTableById(serverId);
            if (server == null) { return false; }

            DiscordBotLog($"Discord: 接收啟動操作 | {adminName} ({adminID})");
            await GameServer_Start(server);
            return GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started;
        }

        public async Task<bool> StopServerById(string serverId, string adminID, string adminName)
        {
            ServerTable server = GetServerTableById(serverId);
            if (server == null) { return false; }

            DiscordBotLog($"Discord: 接收停止操作 | {adminName} ({adminID})");
            await GameServer_Stop(server);
            return GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped;
        }

        public async Task<bool> RestartServerById(string serverId, string adminID, string adminName)
        {
            ServerTable server = GetServerTableById(serverId);
            if (server == null) { return false; }

            DiscordBotLog($"Discord: 接收重啟操作 | {adminName} ({adminID})");
            await GameServer_Restart(server);
            return GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started;
        }

        public Task<string> SendCommandById(string serverId, string command, string adminID, string adminName, int waitForDataInMs = 0)
        {
            ServerTable server = GetServerTableById(serverId);
            if (server == null) { return Task.FromResult(""); }

            DiscordBotLog($"Discord: 接收**發送**操作 | {adminName} ({adminID}) | {command}");
            return SendCommandAsync(server, command, waitForDataInMs);
        }

        public async Task<bool> BackupServerById(string serverId, string adminID, string adminName)
        {
            ServerTable server = GetServerTableById(serverId);
            if (server == null) { return false; }

            DiscordBotLog($"Discord: 接收**備份*操作 | {adminName} ({adminID})");
            await GameServer_Backup(server);
            return GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped;
        }

        public async Task<bool> UpdateServerById(string serverId, string adminID, string adminName)
        {
            ServerTable server = GetServerTableById(serverId);
            if (server == null) { return false; }

            DiscordBotLog($"Discord: 接收**更新**操作 | {adminName} ({adminID})");
            await GameServer_Update(server);
            return GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped;
        }

        private void Switch_DiscordBotAutoStart_Click(object sender, RoutedEventArgs e)
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true);
            key?.SetValue("DiscordBotAutoStart", MahAppSwitch_DiscordBotAutoStart.IsOn.ToString());
        }

        private void Button_DiscordBotInvite_Click(object sender, RoutedEventArgs e)
        {
            string inviteLink = g_DiscordBot.GetInviteLink();
            if (!string.IsNullOrWhiteSpace(inviteLink))
            {
                Process.Start(g_DiscordBot.GetInviteLink());
            }
        }
        #endregion

        /// <summary>Hide others Flyout and toggle the flyout</summary>
        /// <param name="flyout"></param>
        private void ToggleMahappFlyout(Flyout flyout)
        {
            MahAppFlyout_DiscordAlert.IsOpen = flyout == MahAppFlyout_DiscordAlert && !MahAppFlyout_DiscordAlert.IsOpen;
            MahAppFlyout_EditConfig.IsOpen = flyout == MahAppFlyout_EditConfig && !MahAppFlyout_EditConfig.IsOpen;
            MahAppFlyout_ImportGameServer.IsOpen = flyout == MahAppFlyout_ImportGameServer && !MahAppFlyout_ImportGameServer.IsOpen;
            MahAppFlyout_InstallGameServer.IsOpen = flyout == MahAppFlyout_InstallGameServer && !MahAppFlyout_InstallGameServer.IsOpen;
            MahAppFlyout_ManageAddons.IsOpen = flyout == MahAppFlyout_ManageAddons && !MahAppFlyout_ManageAddons.IsOpen;
            MahAppFlyout_RestoreBackup.IsOpen = flyout == MahAppFlyout_RestoreBackup && !MahAppFlyout_RestoreBackup.IsOpen;
            MahAppFlyout_SetAffinity.IsOpen = flyout == MahAppFlyout_SetAffinity && !MahAppFlyout_SetAffinity.IsOpen;
            MahAppFlyout_Settings.IsOpen = flyout == MahAppFlyout_Settings && !MahAppFlyout_Settings.IsOpen;
            MahAppFlyout_ViewPlugins.IsOpen = flyout == MahAppFlyout_ViewPlugins && !MahAppFlyout_ViewPlugins.IsOpen;
        }

        private void HamburgerMenu_ItemClick(object sender, ItemClickEventArgs e)
        {
            HamburgerMenuControl.IsPaneOpen = false;

            hMenu_Home.Visibility = (HamburgerMenuControl.SelectedIndex == 0) ? Visibility.Visible : Visibility.Hidden;
            hMenu_Dashboard.Visibility = (HamburgerMenuControl.SelectedIndex == 1) ? Visibility.Visible : Visibility.Hidden;
            hMenu_Discordbot.Visibility = (HamburgerMenuControl.SelectedIndex == 2) ? Visibility.Visible : Visibility.Hidden;

            if (HamburgerMenuControl.SelectedIndex == 2)
            {
                label_DiscordBotCommands.Content = DiscordBot.Configs.GetCommandsList();
                button_DiscordBotPrefixEdit.Content = "編輯";
                textBox_DiscordBotPrefix.IsEnabled = false;
                textBox_DiscordBotPrefix.Text = DiscordBot.Configs.GetBotPrefix();
                textBox_DiscordBotName.Text = DiscordBot.Configs.GetBotName();

                button_DiscordBotTokenEdit.Content = "編輯";
                textBox_DiscordBotToken.IsEnabled = false;
                textBox_DiscordBotToken.Text = DiscordBot.Configs.GetBotToken();
                //textBox_DiscordBotDashboard.Text = DiscordBot.Configs.GetDashboardChannel();
                //numericUpDown_DiscordRefreshRate.Value = DiscordBot.Configs.GetDashboardRefreshRate();

                Refresh_DiscordBotAdminList(listBox_DiscordBotAdminList.SelectedIndex);

                if (listBox_DiscordBotAdminList.Items.Count > 0 && listBox_DiscordBotAdminList.SelectedItem == null)
                {
                    listBox_DiscordBotAdminList.SelectedItem = listBox_DiscordBotAdminList.Items[0];
                }
            }
        }

        private async void HamburgerMenu_OptionsItemClick(object sender, ItemClickEventArgs e)
        {
            if (HamburgerMenuControl.SelectedOptionsIndex == 0)
            {
                ToggleMahappFlyout(MahAppFlyout_ViewPlugins);
            }
            else if (HamburgerMenuControl.SelectedOptionsIndex == 1)
            {
                ToggleMahappFlyout(MahAppFlyout_Settings);
            }

            HamburgerMenuControl.SelectedOptionsIndex = -1;

            await Task.Delay(1); // Delay 0.001 sec due to UI not sync
            if (hMenu_Home.Visibility == Visibility.Visible)
            {
                HamburgerMenuControl.SelectedIndex = 0;
            }
            else if (hMenu_Dashboard.Visibility == Visibility.Visible)
            {
                HamburgerMenuControl.SelectedIndex = 1;
            }
            else if (hMenu_Discordbot.Visibility == Visibility.Visible)
            {
                HamburgerMenuControl.SelectedIndex = 2;
            }
        }

        private async void HamburgerMenu_Loaded(object sender, RoutedEventArgs e)
        {
            HamburgerMenuControl.Visibility = Visibility.Visible;
            hMenu_Home.Visibility = Visibility.Visible;
            hMenu_Dashboard.Visibility = Visibility.Hidden;
            hMenu_Discordbot.Visibility = Visibility.Hidden;

            await Task.Delay(1); // Delay 0.001 sec due to a bug
            HamburgerMenuControl.SelectedIndex = 0;
        }

        private void Button_AutoScroll_Click(object sender, RoutedEventArgs e)
        {
            ServerTable server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            Button_AutoScroll.Content = Button_AutoScroll.Content.ToString() == "✔️ 自動捲動" ? "❌ 自動捲動" : "✔️ 自動捲動";
            _serverMetadata[int.Parse(server.ID)].AutoScroll = Button_AutoScroll.Content.ToString().Contains("✔️");
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoScroll, GetServerMetadata(server.ID).AutoScroll ? "1" : "0");
        }

        /** 
         * Updates the Crontab Gui Element for the next time it a restart scedule will trigger. It is not necesarly the one set in the gui if you set up other restart crontabs
         * You can not call this from any onther thread than the main one. Anything accessing GUI components besides Main thread will immediatly kill that thread without exception or trace
         */
        public void UpdateCrontabTime(string id, string expression)
        {
            ServerTable currentRow = (ServerTable)ServerGrid.SelectedItem;
            if (currentRow.ID == id)
            {
                textBox_nextcrontab.Text = CrontabSchedule.TryParse(expression)?.GetNextOccurrence(DateTime.Now).ToString("yyyy/MM/dd/ddd HH:mm:ss");
            }

        }
    }
}
