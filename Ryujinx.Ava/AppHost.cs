using ARMeilleure.Translation;
using ARMeilleure.Translation.PTC;
using Avalonia.Input;
using Avalonia.Threading;
using LibHac.FsSystem;
using OpenTK.Windowing.Common;
using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.Audio.Backends.OpenAL;
using Ryujinx.Audio.Backends.SDL2;
using Ryujinx.Audio.Backends.SoundIo;
using Ryujinx.Audio.Integration;
using Ryujinx.Ava.Application.Module;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.System;
using Ryujinx.Configuration;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.GAL.Multithreading;
using Ryujinx.Graphics.Gpu;
using Ryujinx.Graphics.OpenGL;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.FileSystem.Content;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.HLE.Ui;
using Ryujinx.Input;
using Ryujinx.Input.Avalonia;
using Ryujinx.Input.HLE;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using InputManager = Ryujinx.Input.HLE.InputManager;
using MouseButton = Ryujinx.Input.MouseButton;
using Size = Avalonia.Size;
using Switch = Ryujinx.HLE.Switch;
using Key = Ryujinx.Input.Key;
using WindowState = Avalonia.Controls.WindowState;

namespace Ryujinx.Ava
{
    public class AppHost : IDisposable
    {
        private const int TargetFps          = 60;

        private const int CursorHideIdleTime = 8; // Hide Cursor seconds
        private static readonly Cursor InvisibleCursor = new Cursor(StandardCursorType.None);

        private readonly AccountManager _accountManager;
        private UserChannelPersistence _userChannelPersistence;
        private LibHacHorizonManager _libHacHorizonManager;
        private IHostUiHandler _hostUiHandler;

        private readonly Stopwatch _chrono;

        private readonly InputManager _inputManager;

        private readonly IKeyboard _keyboardInterface;

        private readonly StyleableWindow _parent;

        private readonly long _ticksPerFrame;

        private readonly GraphicsDebugLevel _glLogLevel;

        private bool _hideCursorOnIdle;
        private bool _isMouseInClient;
        private bool _isActive;
        private long _lastCursorMoveTime;
        private bool _lastCursorIdleState;

        private Thread _mainThread;


        private IRenderer _renderer;
        private readonly Thread _renderingThread;
        private Thread _nvStutterWorkaround;

        private long _ticks;

        private bool _renderingStarted;
        private WindowsMultimediaTimerResolution _windowsMultimediaTimerResolution;
        private double _windowScaleFactor;

        private KeyboardStateSnapshot _lastKeyboardSnapshot;
        private Key[] _enabledHotkeysWhileRunning;

        public event EventHandler AppExit;
        public event EventHandler<StatusUpdatedEventArgs> StatusUpdatedEvent;
        public event EventHandler<ApplicationLoader> OnStartAppTitle;
        public event EventHandler OnAppStartsRendering;
        public event EventHandler<bool> OnMouseEnterOrLeaveRenderWindow;
        public event EventHandler<bool> OnAppPauseModeChanged;
        public event EventHandler OnRefreshFirmwareStatusChanged;
        public event EventHandler<GpuContext> OnGpuContextChanged;
        public event EventHandler<KeyboardStateSnapshot> OnHotKeyPressed;
        public event EventHandler<Cursor> OnCursorChanged;

        public NativeEmbeddedWindow Window            { get; }
        public VirtualFileSystem    VirtualFileSystem { get; }
        public ContentManager       ContentManager    { get; }
        public Switch               Device  { get; set; }
        public NpadManager          NpadManager       { get; }
        public TouchScreenManager   TouchScreenManager { get; }

        public bool IsRunning => _isActive;
        public long LastCursorMoveTime => _lastCursorMoveTime;

        public int    Width   { get; private set; }
        public int    Height  { get; private set; }
        public string Path    { get; private set; }

        public bool ScreenshotRequested { get; set; }

        public AppHost(
            NativeEmbeddedWindow   window,
            InputManager           inputManager,
            string                 path,
            VirtualFileSystem      virtualFileSystem,
            ContentManager         contentManager,
            AccountManager         accountManager,
            UserChannelPersistence userChannelPersistence,
            LibHacHorizonManager   libHacHorizonManager,
            IHostUiHandler hostUiHandler,
            StyleableWindow parent)
        {
            _hostUiHandler = hostUiHandler;
            _libHacHorizonManager = libHacHorizonManager;
            _parent                 = parent;
            _inputManager           = inputManager;
            _accountManager         = accountManager;
            _userChannelPersistence = userChannelPersistence;
            _renderingThread        = new Thread(RenderLoop) { Name = "GUI.RenderThread" };
            _chrono                 = new Stopwatch();
            _hideCursorOnIdle       = ConfigurationState.Instance.HideCursorOnIdle;
            _lastCursorMoveTime     = Stopwatch.GetTimestamp();
            _ticksPerFrame          = Stopwatch.Frequency / TargetFps;
            _glLogLevel             = ConfigurationState.Instance.Logger.GraphicsDebugLevel;

            _inputManager.SetMouseDriver(new AvaloniaMouseDriver(window));
            NpadManager = _inputManager.CreateNpadManager();
            _keyboardInterface = (IKeyboard)_inputManager.KeyboardDriver.GetGamepad("0");
            TouchScreenManager = _inputManager.CreateTouchScreenManager();
            
            Window            = window;
            Path              = path;
            VirtualFileSystem = virtualFileSystem;
            ContentManager    = contentManager;

            ((AvaloniaKeyboardDriver)_inputManager.KeyboardDriver).AddControl(Window);

            window.MouseDown += Window_MouseDown;
            window.MouseUp   += Window_MouseUp;
            window.MouseMove += Window_MouseMove;

            ConfigurationState.Instance.HideCursorOnIdle.Event += HideCursorState_Changed;

            _parent.PointerEnter += Parent_PointerEntered;
            _parent.PointerLeave += Parent_PointerLeft;
            
            ConfigurationState.Instance.System.IgnoreMissingServices.Event += UpdateIgnoreMissingServicesState;
            ConfigurationState.Instance.Graphics.AspectRatio.Event         += UpdateAspectRatioState;
            ConfigurationState.Instance.System.EnableDockedMode.Event      += UpdateDockedModeState;

            ForceDpiAware.Windows();
            _windowScaleFactor = ForceDpiAware.GetWindowScaleFactor();
        }

        public void RegisterHotKeys(params Key[] hotkeys)
        {
            _enabledHotkeysWhileRunning = hotkeys;
        }

        private void Parent_PointerLeft(object sender, PointerEventArgs e)
        {
            Window.Cursor = ConfigurationState.Instance.Hid.EnableMouse ? InvisibleCursor : Cursor.Default;
            _isMouseInClient = false;
            OnMouseEnterOrLeaveRenderWindow?.Invoke(this, false);
        }

        private void Parent_PointerEntered(object sender, PointerEventArgs e)
        {
            _isMouseInClient = true;
            OnMouseEnterOrLeaveRenderWindow?.Invoke(this, true);
        }

        private void SetRendererWindowSize(Size size)
        {
            if (_renderer != null)
            {
                _renderer.Window.SetSize((int)(size.Width * _windowScaleFactor), (int)(size.Height * _windowScaleFactor));
            }
        }

        private unsafe void Renderer_ScreenCaptured(object sender, ScreenCaptureImageInfo e)
        {
            if (e.Data.Length > 0 && e.Height > 0 && e.Width > 0)
            {
                Task.Run(() =>
                {
                    lock (this)
                    {
                        var    currentTime = DateTime.Now;
                        string filename    = $"ryujinx_capture_{currentTime.Year}-{currentTime.Month:D2}-{currentTime.Day:D2}_{currentTime.Hour:D2}-{currentTime.Minute:D2}-{currentTime.Second:D2}.png";
                        string directory   = AppDataManager.Mode switch
                        {
                            AppDataManager.LaunchMode.Portable => System.IO.Path.Combine(AppDataManager.BaseDirPath, "screenshots"),
                            _ => System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures), "Ryujinx")
                        };

                        string path = System.IO.Path.Combine(directory, filename);

                        try
                        {
                            Directory.CreateDirectory(directory);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error?.Print(LogClass.Application, $"Failed to create directory at path {directory}. Error : {ex.GetType().Name}", "Screenshot");

                            return;
                        }

                        Image image = e.IsBgra ? Image.LoadPixelData<Bgra32>(e.Data, e.Width, e.Height)
                                               : Image.LoadPixelData<Rgba32>(e.Data, e.Width, e.Height);

                        if (e.FlipX)
                        {
                            image.Mutate(x => x.Flip(FlipMode.Horizontal));
                        }

                        if (e.FlipY)
                        {
                            image.Mutate(x => x.Flip(FlipMode.Vertical));
                        }

                        image.SaveAsPng(path, new PngEncoder()
                        {
                            ColorType = PngColorType.Rgb
                        });

                        image.Dispose();

                        Logger.Notice.Print(LogClass.Application, $"Screenshot saved to {path}", "Screenshot");
                    }
                });
            }
            else
            {
                Logger.Error?.Print(LogClass.Application, $"Screenshot is empty. Size : {e.Data.Length} bytes. Resolution : {e.Width}x{e.Height}", "Screenshot");
            }
        }

        public void Start()
        {
            if (LoadGuestApplication().Result)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _windowsMultimediaTimerResolution = new WindowsMultimediaTimerResolution(1);
                }

                DisplaySleep.Prevent();

                NpadManager.Initialize(Device, ConfigurationState.Instance.Hid.InputConfig, ConfigurationState.Instance.Hid.EnableKeyboard, ConfigurationState.Instance.Hid.EnableMouse);
                TouchScreenManager.Initialize(Device);

                OnStartAppTitle?.Invoke(this, Device.Application);
                OnGpuContextChanged?.Invoke(this, Device.Gpu);

                Window.SizeChanged += Window_SizeChanged;

                _isActive = true;

                _renderingThread.Start();

                _mainThread = new Thread(MainLoop)
                {
                    Name = "GUI.UpdateThread"
                };
                _mainThread.Start();

                _nvStutterWorkaround = null;

                if (_renderer is Graphics.OpenGL.Renderer)
                {
                    _nvStutterWorkaround = new Thread(NVStutterWorkaround)
                    {
                        Name = "GUI.NVStutterWorkaround"
                    };
                    _nvStutterWorkaround.Start();
                }
            }
        }
        
        private void UpdateIgnoreMissingServicesState(object sender, ReactiveEventArgs<bool> args)
        {
            if (Device != null)
            {
                Device.Configuration.IgnoreMissingServices = args.NewValue;
            }
        }

        private void UpdateAspectRatioState(object sender, ReactiveEventArgs<AspectRatio> args)
        {
            if (Device != null)
            {
                Device.Configuration.AspectRatio = args.NewValue;
            }
        }

        private void UpdateDockedModeState(object sender, ReactiveEventArgs<bool> e)
        {
            if (Device != null)
            {
                Device.System.ChangeDockedModeState(e.NewValue);
            }
        }

        public void Dispose()
        {
            (_keyboardInterface as AvaloniaKeyboard)?.Clear();

            ((AvaloniaKeyboardDriver)_inputManager.KeyboardDriver).RemoveControl(Window);

            if (!_isActive)
            {
                return;
            }

            if (Device.Application != null)
            {
                MainWindow.UpdateGameMetadata(Device.Application.TitleIdText);
            }

            _windowsMultimediaTimerResolution?.Dispose();
            _windowsMultimediaTimerResolution = null;
            DisplaySleep.Restore();

            _isActive = false;
            
            ConfigurationState.Instance.System.IgnoreMissingServices.Event += UpdateIgnoreMissingServicesState;
            ConfigurationState.Instance.Graphics.AspectRatio.Event         += UpdateAspectRatioState;
            ConfigurationState.Instance.System.EnableDockedMode.Event      += UpdateDockedModeState;

            _mainThread.Join();
            _renderingThread.Join();
            _nvStutterWorkaround?.Join();

            Ptc.Close();
            PtcProfiler.Stop();
            NpadManager.Dispose();
            TouchScreenManager.Dispose();
            Device.Dispose();

            Device.DisposeGpu();

            AppExit?.Invoke(this, EventArgs.Empty);
        }

        private void Window_MouseMove(object sender, (double X, double Y) e)
        {
            (_inputManager.MouseDriver as AvaloniaMouseDriver).SetPosition(e.X, e.Y);

            if (_hideCursorOnIdle)
            {
                _lastCursorMoveTime = Stopwatch.GetTimestamp();
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            (_inputManager.MouseDriver as AvaloniaMouseDriver).SetMouseReleased((MouseButton) e.Button);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            (_inputManager.MouseDriver as AvaloniaMouseDriver).SetMousePressed((MouseButton) e.Button);
        }

        private void HideCursorState_Changed(object sender, ReactiveEventArgs<bool> state)
        {
            Dispatcher.UIThread.InvokeAsync(delegate
            {
                _hideCursorOnIdle = state.NewValue;

                if (_hideCursorOnIdle)
                {
                    _lastCursorMoveTime = Stopwatch.GetTimestamp();
                }
                else
                {
                    _parent.Cursor = Cursor.Default;
                }
            });
        }

        private async Task<bool> LoadGuestApplication()
        {
            InitializeSwitchInstance();

            MainWindow.UpdateGraphicsConfig();

            SystemVersion firmwareVersion = ContentManager.GetCurrentFirmwareVersion();
            
            bool isFirmwareTitle = false;
            
            if (Path.StartsWith("@SystemContent"))
            {
                Path = VirtualFileSystem.SwitchPathToSystemPath(Path);

                isFirmwareTitle = true;
            }

            if (!SetupValidator.CanStartApplication(ContentManager, Path, out UserError userError))
            {
                if (SetupValidator.CanFixStartApplication(ContentManager, Path, userError, out firmwareVersion))
                {
                    if (userError == UserError.NoFirmware)
                    {
                        string message = string.Format(LocaleManager.Instance["DialogFirmwareInstallEmbeddedMessage"], firmwareVersion.VersionString);

                        UserResult result = await ContentDialogHelper.CreateConfirmationDialog(_parent, 
                            LocaleManager.Instance["DialogFirmwareNoFirmwareInstalledMessage"], message);

                        if (result != UserResult.Yes)
                        {
                            UserErrorDialog.ShowUserErrorDialog(userError, _parent);

                            Device.Dispose();

                            return false;
                        }
                    }

                    if (!SetupValidator.TryFixStartApplication(ContentManager, Path, userError, out _))
                    {
                        UserErrorDialog.ShowUserErrorDialog(userError, _parent);

                        Device.Dispose();

                        return false;
                    }

                    // Tell the user that we installed a firmware for them.
                    if (userError == UserError.NoFirmware)
                    {
                        firmwareVersion = ContentManager.GetCurrentFirmwareVersion();

                        OnRefreshFirmwareStatusChanged?.Invoke(this, null);

                        string message = String.Format(LocaleManager.Instance["DialogFirmwareInstallEmbeddedSuccessMessage"],firmwareVersion.VersionString);

                        await ContentDialogHelper.CreateInfoDialog(_parent, string.Format(LocaleManager.Instance["DialogFirmwareInstalledMessage"], firmwareVersion.VersionString), message);
                    }
                }
                else
                {
                    UserErrorDialog.ShowUserErrorDialog(userError, _parent);

                    Device.Dispose();

                    return false;
                }
            }

            Logger.Notice.Print(LogClass.Application, $"Using Firmware Version: {firmwareVersion?.VersionString}");
            
            if (isFirmwareTitle)
            {
                Logger.Info?.Print(LogClass.Application, "Loading as Firmware Title (NCA).");

                Device.LoadNca(Path);
            }
            else if (Directory.Exists(Path))
            {
                string[] romFsFiles = Directory.GetFiles(Path, "*.istorage");

                if (romFsFiles.Length == 0)
                {
                    romFsFiles = Directory.GetFiles(Path, "*.romfs");
                }

                if (romFsFiles.Length > 0)
                {
                    Logger.Info?.Print(LogClass.Application, "Loading as cart with RomFS.");

                    Device.LoadCart(Path, romFsFiles[0]);
                }
                else
                {
                    Logger.Info?.Print(LogClass.Application, "Loading as cart WITHOUT RomFS.");

                    Device.LoadCart(Path);
                }
            }
            else if (File.Exists(Path))
            {
                switch (System.IO.Path.GetExtension(Path).ToLowerInvariant())
                {
                    case ".xci":
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as XCI.");
                            Device.LoadXci(Path);

                            break;
                        }
                    case ".nca":
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as NCA.");

                            Device.LoadNca(Path);

                            break;
                        }
                    case ".nsp":
                    case ".pfs0":
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as NSP.");

                            Device.LoadNsp(Path);

                            break;
                        }
                    default:
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as homebrew.");

                            try
                            {
                                Device.LoadProgram(Path);
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                Logger.Error?.Print(LogClass.Application, "The specified file is not supported by Ryujinx.");

                                Dispose();

                                return false;
                            }

                            break;
                        }
                }
            }
            else
            {
                Logger.Warning?.Print(LogClass.Application, "Please specify a valid XCI/NCA/NSP/PFS0/NRO file.");

                Dispose();

                return false;
            }

            DiscordIntegrationModule.SwitchToPlayingState(Device.Application.TitleIdText, Device.Application.TitleName);

            ApplicationLibrary.LoadAndSaveMetaData(Device.Application.TitleIdText, appMetadata =>
            {
                appMetadata.LastPlayed = DateTime.UtcNow.ToString();
            });

            return true;
        }

        internal void Resume()
        {
            Device?.System?.TogglePauseEmulation(false);
            OnAppPauseModeChanged?.Invoke(this, false);
        }

        internal void Pause()
        {
            Device?.System?.TogglePauseEmulation(true);
            OnAppPauseModeChanged?.Invoke(this, true);
        }

        private void InitializeSwitchInstance()
        {
            VirtualFileSystem.ReloadKeySet();

            IRenderer             renderer     = new Renderer();
            IHardwareDeviceDriver deviceDriver = new DummyHardwareDeviceDriver();

            BackendThreading threadingMode = ConfigurationState.Instance.Graphics.BackendThreading;

            bool threadedGAL = threadingMode == BackendThreading.On || (threadingMode == BackendThreading.Auto && renderer.PreferThreading);

            if (threadedGAL)
            {
                renderer = new ThreadedRenderer(renderer);
            }

            Logger.Info?.PrintMsg(LogClass.Gpu, $"Backend Threading ({threadingMode}): {threadedGAL}");

            if (ConfigurationState.Instance.System.AudioBackend.Value == AudioBackend.SDL2)
            {
                if (SDL2HardwareDeviceDriver.IsSupported)
                {
                    deviceDriver = new SDL2HardwareDeviceDriver();
                }
                else
                {
                    Logger.Warning?.Print(LogClass.Audio, "SDL2 is not supported, trying to fall back to OpenAL.");

                    if (OpenALHardwareDeviceDriver.IsSupported)
                    {
                        Logger.Warning?.Print(LogClass.Audio, "Found OpenAL, changing configuration.");

                        ConfigurationState.Instance.System.AudioBackend.Value = AudioBackend.OpenAl;
                        MainWindow.SaveConfig();

                        deviceDriver = new OpenALHardwareDeviceDriver();
                    }
                    else
                    {
                        Logger.Warning?.Print(LogClass.Audio, "OpenAL is not supported, trying to fall back to SoundIO.");

                        if (SoundIoHardwareDeviceDriver.IsSupported)
                        {
                            Logger.Warning?.Print(LogClass.Audio, "Found SoundIO, changing configuration.");

                            ConfigurationState.Instance.System.AudioBackend.Value = AudioBackend.SoundIo;
                            MainWindow.SaveConfig();

                            deviceDriver = new SoundIoHardwareDeviceDriver();
                        }
                        else
                        {
                            Logger.Warning?.Print(LogClass.Audio, "SoundIO is not supported, falling back to dummy audio out.");
                        }
                    }
                }
            }
            else if (ConfigurationState.Instance.System.AudioBackend.Value == AudioBackend.SoundIo)
            {
                if (SoundIoHardwareDeviceDriver.IsSupported)
                {
                    deviceDriver = new SoundIoHardwareDeviceDriver();
                }
                else
                {
                    Logger.Warning?.Print(LogClass.Audio, "SoundIO is not supported, trying to fall back to SDL2.");

                    if (SDL2HardwareDeviceDriver.IsSupported)
                    {
                        Logger.Warning?.Print(LogClass.Audio, "Found SDL2, changing configuration.");

                        ConfigurationState.Instance.System.AudioBackend.Value = AudioBackend.SDL2;
                        MainWindow.SaveConfig();

                        deviceDriver = new SDL2HardwareDeviceDriver();
                    }
                    else
                    {
                        Logger.Warning?.Print(LogClass.Audio, "SDL2 is not supported, trying to fall back to OpenAL.");

                        if (OpenALHardwareDeviceDriver.IsSupported)
                        {
                            Logger.Warning?.Print(LogClass.Audio, "Found OpenAL, changing configuration.");

                            ConfigurationState.Instance.System.AudioBackend.Value = AudioBackend.OpenAl;
                            MainWindow.SaveConfig();

                            deviceDriver = new OpenALHardwareDeviceDriver();
                        }
                        else
                        {
                            Logger.Warning?.Print(LogClass.Audio, "OpenAL is not supported, falling back to dummy audio out.");
                        }
                    }
                }
            }
            else if (ConfigurationState.Instance.System.AudioBackend.Value == AudioBackend.OpenAl)
            {
                if (OpenALHardwareDeviceDriver.IsSupported)
                {
                    deviceDriver = new OpenALHardwareDeviceDriver();
                }
                else
                {
                    Logger.Warning?.Print(LogClass.Audio, "OpenAL is not supported, trying to fall back to SDL2.");

                    if (SDL2HardwareDeviceDriver.IsSupported)
                    {
                        Logger.Warning?.Print(LogClass.Audio, "Found SDL2, changing configuration.");

                        ConfigurationState.Instance.System.AudioBackend.Value = AudioBackend.SDL2;
                        MainWindow.SaveConfig();

                        deviceDriver = new SDL2HardwareDeviceDriver();
                    }
                    else
                    {
                        Logger.Warning?.Print(LogClass.Audio, "SDL2 is not supported, trying to fall back to SoundIO.");

                        if (SoundIoHardwareDeviceDriver.IsSupported)
                        {
                            Logger.Warning?.Print(LogClass.Audio, "Found SoundIO, changing configuration.");

                            ConfigurationState.Instance.System.AudioBackend.Value = AudioBackend.SoundIo;
                            MainWindow.SaveConfig();

                            deviceDriver = new SoundIoHardwareDeviceDriver();
                        }
                        else
                        {
                            Logger.Warning?.Print(LogClass.Audio, "SoundIO is not supported, falling back to dummy audio out.");
                        }
                    }
                }
            }

            var memoryConfiguration = ConfigurationState.Instance.System.ExpandRam.Value
                ? HLE.MemoryConfiguration.MemoryConfiguration6GB
                : HLE.MemoryConfiguration.MemoryConfiguration4GB;

            IntegrityCheckLevel fsIntegrityCheckLevel = ConfigurationState.Instance.System.EnableFsIntegrityChecks ? IntegrityCheckLevel.ErrorOnInvalid : IntegrityCheckLevel.None;
            
            HLE.HLEConfiguration configuration = new HLE.HLEConfiguration(VirtualFileSystem,
                                                                          _libHacHorizonManager,
                                                                          ContentManager,
                                                                          _accountManager,
                                                                          _userChannelPersistence,
                                                                          renderer,
                                                                          deviceDriver,
                                                                          memoryConfiguration,
                                                                          _hostUiHandler,
                                                                          (SystemLanguage)ConfigurationState.Instance.System.Language.Value,
                                                                          (RegionCode)ConfigurationState.Instance.System.Region.Value,
                                                                          ConfigurationState.Instance.Graphics.EnableVsync,
                                                                          ConfigurationState.Instance.System.EnableDockedMode,
                                                                          ConfigurationState.Instance.System.EnablePtc,
                                                                          fsIntegrityCheckLevel,
                                                                          ConfigurationState.Instance.System.FsGlobalAccessLogMode,
                                                                          ConfigurationState.Instance.System.SystemTimeOffset,
                                                                          ConfigurationState.Instance.System.TimeZone,
                                                                          ConfigurationState.Instance.System.MemoryManagerMode,
                                                                          ConfigurationState.Instance.System.IgnoreMissingServices,
                                                                          ConfigurationState.Instance.Graphics.AspectRatio);

            Device = new Switch(configuration);
        }

        private void Window_SizeChanged(object sender, Size e)
        {
            Width  = (int)e.Width;
            Height = (int)e.Height;

            SetRendererWindowSize(e);
        }

        private void MainLoop()
        {
            while (_isActive)
            {
                UpdateFrame();

                if (ScreenshotRequested)
                {
                    ScreenshotRequested = false;
                    _renderer.Screenshot();
                }

                // Polling becomes expensive if it's not slept
                Thread.Sleep(1);
            }
        }

        private void NVStutterWorkaround()
        {
            while (_isActive)
            {
                // When NVIDIA Threaded Optimization is on, the driver will snapshot all threads in the system whenever the application creates any new ones.
                // The ThreadPool has something called a "GateThread" which terminates itself after some inactivity.
                // However, it immediately starts up again, since the rules regarding when to terminate and when to start differ.
                // This creates a new thread every second or so.
                // The main problem with this is that the thread snapshot can take 70ms, is on the OpenGL thread and will delay rendering any graphics.
                // This is a little over budget on a frame time of 16ms, so creates a large stutter.
                // The solution is to keep the ThreadPool active so that it never has a reason to terminate the GateThread.

                // TODO: This should be removed when the issue with the GateThread is resolved.

                ThreadPool.QueueUserWorkItem(state => { });
                Thread.Sleep(300);
            }
        }

        private unsafe void RenderLoop()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Window.IsFullscreen = _parent.WindowState == WindowState.FullScreen;
            });

            IRenderer renderer = Device.Gpu.Renderer;

            if (renderer is ThreadedRenderer tr)
            {
                renderer = tr.BaseRenderer;
            }

            _renderer = renderer;

            _renderer.ScreenCaptured += Renderer_ScreenCaptured;

            if (Window is OpenGlEmbeddedWindow openGlEmbeddedWindow)
            {
                (_renderer as Renderer).InitializeBackgroundContext(AvaloniaOpenGLContextHelper.CreateBackgroundContext(Window.GlfwWindow.WindowPtr, _glLogLevel != GraphicsDebugLevel.None));

                openGlEmbeddedWindow.MakeCurrent();
            }

            Device.Gpu.Renderer.Initialize(_glLogLevel);

            Width  = (int)Window.Bounds.Width;
            Height = (int)Window.Bounds.Height;

            _renderer.Window.SetSize((int)(Width * _windowScaleFactor), (int)(Height * _windowScaleFactor));

            Device.Gpu.Renderer.RunLoop(() =>
            {
                Device.Gpu.InitializeShaderCache();
                Translator.IsReadyForTranslation.Set();

                while (_isActive)
                {
                    _ticks += _chrono.ElapsedTicks;

                    _chrono.Restart();

                    if (Device.WaitFifo())
                    {
                        Device.Statistics.RecordFifoStart();
                        Device.ProcessFrame();
                        Device.Statistics.RecordFifoEnd();
                    }

                    while (Device.ConsumeFrameAvailable())
                    {
                        if (!_renderingStarted)
                        {
                            _renderingStarted = true;
                            OnAppStartsRendering?.Invoke(this, null);
                        }
                        
                        Device.PresentFrame(Present);
                    }

                    if (_ticks >= _ticksPerFrame)
                    {
                        string dockedMode = ConfigurationState.Instance.System.EnableDockedMode ? "Docked" : "Handheld";
                        float scale = GraphicsConfig.ResScale;

                        if (scale != 1)
                        {
                            dockedMode += $" ({scale}x)";
                        }

                        string vendor = _renderer is Renderer renderer ? renderer.GpuVendor : "Vulkan Test";

                        StatusUpdatedEvent?.Invoke(this, new StatusUpdatedEventArgs(
                            Device.EnableDeviceVsync,
                            dockedMode,
                            ConfigurationState.Instance.Graphics.AspectRatio.Value.ToText(),
                            $"Game: {Device.Statistics.GetGameFrameRate():00.00} FPS ({Device.Statistics.GetGameFrameTime():00.00} ms)",
                            $"FIFO: {Device.Statistics.GetFifoPercent():00.00} %",
                            $"GPU: {vendor}"));

                        _ticks = Math.Min(_ticks - _ticksPerFrame, _ticksPerFrame);
                    }
                }
            });

            (Window as OpenGlEmbeddedWindow)?.MakeCurrent(null);

            Window.SizeChanged -= Window_SizeChanged;
        }

        private void Present()
        {
            Window.Present();
        }

        private bool UpdateFrame()
        {
            if (!_isActive)
            {
                return false;
            }

            NpadManager.Update(ConfigurationState.Instance.Graphics.AspectRatio.Value.ToFloat());


            // Handle hotkeys
            KeyboardStateSnapshot keyboard = _keyboardInterface.GetKeyboardStateSnapshot();
            bool needIvokeEvent = false;
            foreach (var currentHotkey in _enabledHotkeysWhileRunning)
            {
                var lastState = _lastKeyboardSnapshot?.IsPressed(currentHotkey);
                var currentState = keyboard.IsPressed(currentHotkey);
                if (lastState != currentState)
                {
                    needIvokeEvent = true;
                }
            }
            _lastKeyboardSnapshot = keyboard;
            if (needIvokeEvent)
            {
                OnHotKeyPressed?.Invoke(this, keyboard);
                (_keyboardInterface as AvaloniaKeyboard).Clear();
            }


            // Handle Mouse events
            if (_hideCursorOnIdle && !ConfigurationState.Instance.Hid.EnableMouse)
            {
                long cursorMoveDelta = Stopwatch.GetTimestamp() - _lastCursorMoveTime;

                var newCursorIdleState = (cursorMoveDelta >= CursorHideIdleTime * Stopwatch.Frequency);
                if (newCursorIdleState != _lastCursorIdleState)
                {
                    _lastCursorIdleState = newCursorIdleState;
                    OnCursorChanged?.Invoke(this, newCursorIdleState ? InvisibleCursor : Cursor.Default);
                }                
            }

            if (ConfigurationState.Instance.Hid.EnableMouse && _isMouseInClient)
            {
                OnCursorChanged?.Invoke(this, InvisibleCursor);
            }


            //Touchscreen
            bool hasTouch = false;

            // Get screen touch position from left mouse click
            // Get screen touch position
            if ((_parent.IsActive || Window.RendererFocused) && !ConfigurationState.Instance.Hid.EnableMouse)
            {
                hasTouch = TouchScreenManager.Update(true, (_inputManager.MouseDriver as AvaloniaMouseDriver).IsButtonPressed(MouseButton.Button1), ConfigurationState.Instance.Graphics.AspectRatio.Value.ToFloat());
            }

            if (!hasTouch)
            {
                Device.Hid.Touchscreen.Update();
            }

            Device.Hid.DebugPad.Update();

            return true;
        }
    }
}