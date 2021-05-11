using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LibHac;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Ui.Applet;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.Ava.Ui.ViewModels;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Configuration;
using Ryujinx.Graphics.Gpu;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.FileSystem.Content;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.Input.Avalonia;
using Ryujinx.Input.SDL2;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InputManager = Ryujinx.Input.HLE.InputManager;
using ProgressBar = Avalonia.Controls.ProgressBar;

namespace Ryujinx.Ava.Ui.Windows
{
    public class MainWindow : StyleableWindow
    {
        public static UserChannelPersistence UserChannelPersistence;

        internal readonly AvaHostUiHandler UiHandler;

        private bool _canUpdate;
        private bool _isClosing;

        private bool _isLoading;

        private Control _mainViewContent;

        public AppHost AppHost;
        public InputManager Manager { get; }

        public MainWindow()
        {
            ViewModel = new MainWindowViewModel(this);

            DataContext = ViewModel;

            UiHandler = new AvaHostUiHandler(this);

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            IObservable<Size> resizeObserverable = this.GetObservable(ClientSizeProperty);

            resizeObserverable.Subscribe(Resized);

            IObservable<Rect> stateObserverable = this.GetObservable(BoundsProperty);

            stateObserverable.Subscribe(StateChanged);

            Title = $"Ryujinx {Program.Version}";

            Initialize();

            Manager = new InputManager(new AvaloniaKeyboardDriver(this), new SDL2GamepadDriver());

            LoadGameList();
        }

        public VirtualFileSystem VirtualFileSystem { get; private set; }
        public ContentManager ContentManager { get; private set; }

        public NativeEmbeddedWindow GlRenderer { get; private set; }
        public ContentControl ContentFrame { get; private set; }
        public TextBlock LoadStatus { get; private set; }
        public TextBlock FirmwareStatus { get; private set; }
        public TextBox SearchBox { get; private set; }
        public ProgressBar LoadProgressBar { get; private set; }
        public Menu Menu { get; private set; }
        public MenuItem UpdateMenuItem { get; private set; }

        public bool CanUpdate
        {
            get => _canUpdate;
            set
            {
                _canUpdate = value;

                Dispatcher.UIThread.InvokeAsync(() => UpdateMenuItem.IsEnabled = _canUpdate);
            }
        }

        public DataGrid GameList { get; private set; }

        public MainWindowViewModel ViewModel { get; set; }

        public AccountManager AccountManager { get; set; }

        public void LoadGameList()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;

            ViewModel.LoadApplications();

            _isLoading = false;
        }

        private void Update_StatusBar(object sender, StatusUpdatedEventArgs args)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (args.VSyncEnabled)
                {
                    ViewModel.VsyncColor = new SolidColorBrush(Color.Parse("#ff2eeac9"));
                }
                else
                {
                    ViewModel.VsyncColor = new SolidColorBrush(Color.Parse("#ffff4554"));
                }
            });

            ViewModel.DockedStatusText      = args.DockedMode;
            ViewModel.AspectRatioStatusText = args.AspectRatio;
            ViewModel.GameStatusText        = args.GameStatus;
            ViewModel.FifoStatusText        = args.FifoStatus;
            ViewModel.GpuStatusText         = args.GpuName;
        }

        public void UpdateGridColumns()
        {
            GameList.Columns[0].IsVisible = ViewModel.ShowFavoriteColumn;
            GameList.Columns[1].IsVisible = ViewModel.ShowIconColumn;
            GameList.Columns[2].IsVisible = ViewModel.ShowTitleColumn;
            GameList.Columns[3].IsVisible = ViewModel.ShowDeveloperColumn;
            GameList.Columns[4].IsVisible = ViewModel.ShowVersionColumn;
            GameList.Columns[5].IsVisible = ViewModel.ShowTimePlayedColumn;
            GameList.Columns[6].IsVisible = ViewModel.ShowLastPlayedColumn;
            GameList.Columns[7].IsVisible = ViewModel.ShowFileExtColumn;
            GameList.Columns[8].IsVisible = ViewModel.ShowFileSizeColumn;
            GameList.Columns[9].IsVisible = ViewModel.ShowFilePathColumn;
        }

        public void LoadGame(string path)
        {
            if (AppHost != null)
            {
                AvaDialog.CreateInfoDialog("A game has already been loaded",
                    "Please stop emulation or close the emulator before launching another game.", this);

                return;
            }

            _mainViewContent = ContentFrame.Content as Control;

            GlRenderer = new OpenGlEmbeddedWindow(3, 3, ConfigurationState.Instance.Logger.GraphicsDebugLevel);

            AppHost = new AppHost(GlRenderer, Manager, PlatformImpl.DesktopScaling, path, VirtualFileSystem,
                ContentManager, AccountManager, this);

            GlRenderer.WindowCreated += GlRenderer_Created;

            ContentFrame.Content = GlRenderer;
            
            AppHost.StatusUpdatedEvent += Update_StatusBar;
            AppHost.AppExit += AppHost_AppExit;
        }

        private void GlRenderer_Created(object sender, IntPtr e)
        {
            AppHost?.Start();
        }

        private void AppHost_AppExit(object sender, EventArgs e)
        {
            if (_isClosing)
            {
                return;
            }

            ViewModel.IsGameRunning = false;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ContentFrame.Content != _mainViewContent)
                {
                    ContentFrame.Content = _mainViewContent;
                }

                ViewModel.ShowMenuAndStatusBar = true;
            });
            GlRenderer.WindowCreated -= GlRenderer_Created;

            AppHost = null;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Title = $"Ryujinx {Program.Version}";
            });
        }

        private void Initialize()
        {
            UpdateGridColumns();
            UserChannelPersistence = new UserChannelPersistence();
            VirtualFileSystem = VirtualFileSystem.CreateInstance();
            ContentManager = new ContentManager(VirtualFileSystem);
            AccountManager = new AccountManager(VirtualFileSystem);

            VirtualFileSystem.Reload();

            ApplicationHelper.Initialize(VirtualFileSystem, this);

            RefreshFirmwareStatus();
        }

        public void RefreshFirmwareStatus()
        {
            SystemVersion version = ContentManager.GetCurrentFirmwareVersion();
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                string systemVersionFormat = Localizer.Instance[LocalizationStringKeys.StatusBarSystemVersion];
                FirmwareStatus.Text = string.Format(systemVersionFormat, version.VersionString);
            });
        }

        public void UpdateSizes(Size size)
        {
            //Workaround for gamelist not fitting parent
            if (GameList != null)
            {
                Control firstSibling = GameList.Parent.LogicalChildren[0] as Control;
                GameList.Height = GameList.Parent.Bounds.Height - GameList.Margin.Top - GameList.Margin.Bottom -
                                  firstSibling.Bounds.Height - firstSibling.Margin.Top - firstSibling.Margin.Bottom;
            }

            AppHost?.SetRendererWindowSize(GlRenderer.Bounds.Size, PlatformImpl.DesktopScaling);
        }

        private void Resized(Size size)
        {
            UpdateSizes(size);
        }


        private void StateChanged(Rect rect)
        {
            UpdateSizes(ClientSize);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            ContentFrame = this.FindControl<ContentControl>("Content");
            GameList = this.FindControl<DataGrid>("GameList");
            LoadStatus = this.FindControl<TextBlock>("LoadStatus");
            FirmwareStatus = this.FindControl<TextBlock>("FirmwareStatus");
            LoadProgressBar = this.FindControl<ProgressBar>("LoadProgressBar");
            SearchBox = this.FindControl<TextBox>("SearchBox");
            Menu = this.FindControl<Menu>("Menu");
            UpdateMenuItem = this.FindControl<MenuItem>("UpdateMenuItem");
        }

        public static void UpdateGraphicsConfig()
        {
            int resScale = ConfigurationState.Instance.Graphics.ResScale;
            float resScaleCustom = ConfigurationState.Instance.Graphics.ResScaleCustom;

            GraphicsConfig.ResScale = resScale == -1 ? resScaleCustom : resScale;
            GraphicsConfig.MaxAnisotropy = ConfigurationState.Instance.Graphics.MaxAnisotropy;
            GraphicsConfig.ShadersDumpPath = ConfigurationState.Instance.Graphics.ShadersDumpPath;
            GraphicsConfig.EnableShaderCache = ConfigurationState.Instance.Graphics.EnableShaderCache;
        }

        public static void SaveConfig()
        {
            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
        }

        public static void UpdateGameMetadata(string titleId)
        {
            ApplicationLibrary.LoadAndSaveMetaData(titleId, appMetadata =>
            {
                DateTime lastPlayedDateTime = DateTime.Parse(appMetadata.LastPlayed);
                double sessionTimePlayed = DateTime.UtcNow.Subtract(lastPlayedDateTime).TotalSeconds;

                appMetadata.TimePlayed += Math.Round(sessionTimePlayed, MidpointRounding.AwayFromZero);
            });
        }

        private void MenuBase_OnMenuOpened(object sender, RoutedEventArgs e)
        {
            object selection = GameList.SelectedItem;

            if (selection != null && selection is ApplicationData data)
            {
                if (sender is ContextMenu menu)
                {
                    bool canHaveUserSave = !Utilities.IsEmpty(data.ControlHolder.ByteSpan) &&
                                           data.ControlHolder.Value.UserAccountSaveDataSize > 0;
                    bool canHaveDeviceSave = !Utilities.IsEmpty(data.ControlHolder.ByteSpan) &&
                                             data.ControlHolder.Value.DeviceSaveDataSize > 0;
                    bool canHaveBcatSave = !Utilities.IsEmpty(data.ControlHolder.ByteSpan) &&
                                           data.ControlHolder.Value.BcatDeliveryCacheStorageSize > 0;

                    ((menu.Items as AvaloniaList<object>)[0] as MenuItem).IsEnabled = canHaveUserSave;
                    ((menu.Items as AvaloniaList<object>)[1] as MenuItem).IsEnabled = canHaveDeviceSave;
                    ((menu.Items as AvaloniaList<object>)[2] as MenuItem).IsEnabled = canHaveBcatSave;
                }
            }
        }

        private void GameList_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            PointerPoint currentPoint = e.GetCurrentPoint(GameList);

            if (currentPoint.Properties.IsRightButtonPressed)
            {
                DataGridRow row = ((IControl)e.Source).GetSelfAndVisualAncestors().OfType<DataGridRow>().FirstOrDefault();
                if (row != null)
                {
                    GameList.SelectedIndex = row.GetIndex();
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _isClosing = true;
            AppHost?.Exit();
            Manager.Dispose();
            Program.Exit();
            base.OnClosing(e);
        }

        private void GameList_OnDoubleTapped(object sender, RoutedEventArgs e)
        {
            object selection = GameList.SelectedItem;

            if (selection != null && selection is ApplicationData data)
            {
                string path = new FileInfo(data.Path).FullName;

                LoadGame(path);
            }
        }

        private void GameList_OnTapped(object sender, RoutedEventArgs e)
        {
            GameList.SelectedIndex = -1;
        }

        private void SearchButton_OnClick(object sender, RoutedEventArgs e)
        {
            LoadGameList();
        }

        private void SearchBox_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoadGameList();
            }
        }

        private void StopEmulation_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                AppHost?.Exit();
            });
        }

        private void ScanAmiiboMenuItem_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is MenuItem amiibo)
            {
                ViewModel.IsAmiiboRequested = AppHost.EmulationContext.System.SearchingForAmiibo(out int _);
            }
        }

        private void VsyncStatus_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            AppHost.EmulationContext.EnableDeviceVsync = !AppHost.EmulationContext.EnableDeviceVsync;

            Logger.Info?.Print(LogClass.Application, $"VSync toggled to: {AppHost.EmulationContext.EnableDeviceVsync}");
        }

        private void DockedStatus_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            ConfigurationState.Instance.System.EnableDockedMode.Value =
                !ConfigurationState.Instance.System.EnableDockedMode.Value;
        }

        private void AspectRatioStatus_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            AspectRatio aspectRatio = ConfigurationState.Instance.Graphics.AspectRatio.Value;

            ConfigurationState.Instance.Graphics.AspectRatio.Value =
                (int)aspectRatio + 1 > Enum.GetNames(typeof(AspectRatio)).Length - 1
                    ? AspectRatio.Fixed4x3
                    : aspectRatio + 1;
        }
    }
}