using ARMeilleure.Translation;
using ARMeilleure.Translation.PTC;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LibHac.Tools.FsSystem;
using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.Audio.Backends.OpenAL;
using Ryujinx.Audio.Backends.SDL2;
using Ryujinx.Audio.Integration;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Rsc.Vulkan;
using Ryujinx.Rsc.Controls;
using Ryujinx.Rsc.Models;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.System;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.GAL.Multithreading;
using Ryujinx.Graphics.Gpu;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.Input;
using Ryujinx.Input.HLE;
using Ryujinx.Rsc.Common.Configuration;
using Ryujinx.Rsc.Views;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Image = SixLabors.ImageSharp.Image;
using InputManager = Ryujinx.Input.HLE.InputManager;
using Key = Ryujinx.Input.Key;
using MouseButton = Ryujinx.Input.MouseButton;
using Size = Avalonia.Size;
using Switch = Ryujinx.HLE.Switch;
using WindowState = Avalonia.Controls.WindowState;

namespace Ryujinx.Rsc
{
    public class AppHost
    {
        private const int CursorHideIdleTime = 8; // Hide Cursor seconds

        private static readonly Cursor InvisibleCursor = new Cursor(StandardCursorType.None);

        private readonly AccountManager _accountManager;
        private UserChannelPersistence _userChannelPersistence;

        private readonly InputManager _inputManager;

        private readonly IKeyboard _keyboardInterface;

        private readonly GraphicsDebugLevel _glLogLevel;

        private bool _hideCursorOnIdle;
        private bool _isStopped;
        private bool _isActive;
        private long _lastCursorMoveTime;

        private KeyboardHotkeyState _prevHotkeyState;

        private IRenderer _renderer;
        private readonly Thread _renderingThread;

        private bool _isMouseInClient;
        private bool _renderingStarted;
        private bool _dialogShown;

        private WindowsMultimediaTimerResolution _windowsMultimediaTimerResolution;
        private KeyboardStateSnapshot _lastKeyboardSnapshot;

        private readonly CancellationTokenSource _gpuCancellationTokenSource;

        public event EventHandler AppExit;
        public event EventHandler<StatusUpdatedEventArgs> StatusUpdatedEvent;

        public RendererControl Renderer { get; }
        public VirtualFileSystem VirtualFileSystem { get; }
        public ContentManager ContentManager { get; }
        public Switch Device { get; set; }
        public NpadManager NpadManager { get; }
        public TouchScreenManager TouchScreenManager { get; }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public string ApplicationPath { get; private set; }

        private bool _isFirmwareTitle;

        public bool ScreenshotRequested { get; set; }

        private object _lockObject = new();
        private readonly MainView _parent;

        public AppHost(
            RendererControl renderer,
            InputManager inputManager,
            string applicationPath,
            VirtualFileSystem virtualFileSystem,
            ContentManager contentManager,
            AccountManager accountManager,
            UserChannelPersistence userChannelPersistence,
            MainView parent)
        {
            _parent = parent;
            _inputManager = inputManager;
            _accountManager = accountManager;
            _userChannelPersistence = userChannelPersistence;
            _renderingThread = new Thread(RenderLoop) { Name = "GUI.RenderThread" };
            _hideCursorOnIdle = ConfigurationState.Instance.HideCursorOnIdle;
            _lastCursorMoveTime = Stopwatch.GetTimestamp();
            _glLogLevel = ConfigurationState.Instance.Logger.GraphicsDebugLevel;
            _inputManager.SetMouseDriver(new AvaloniaMouseDriver(renderer));
            _keyboardInterface = (IKeyboard)_inputManager.KeyboardDriver.GetGamepad("0");
            _lastKeyboardSnapshot = _keyboardInterface.GetKeyboardStateSnapshot();

            NpadManager = _inputManager.CreateNpadManager();
            TouchScreenManager = _inputManager.CreateTouchScreenManager();
            Renderer = renderer;
            ApplicationPath = applicationPath;
            VirtualFileSystem = virtualFileSystem;
            ContentManager = contentManager;

            if (ApplicationPath.StartsWith("@SystemContent"))
            {
                ApplicationPath = _parent.VirtualFileSystem.SwitchPathToSystemPath(ApplicationPath);

                _isFirmwareTitle = true;
            }

            ConfigurationState.Instance.HideCursorOnIdle.Event += HideCursorState_Changed;

            _parent.PointerEnter += Parent_PointerEntered;
            _parent.PointerLeave += Parent_PointerLeft;
            _parent.PointerMoved += Parent_PointerMoved;

            ConfigurationState.Instance.System.IgnoreMissingServices.Event += UpdateIgnoreMissingServicesState;
            ConfigurationState.Instance.Graphics.AspectRatio.Event += UpdateAspectRatioState;
            ConfigurationState.Instance.System.EnableDockedMode.Event += UpdateDockedModeState;
            ConfigurationState.Instance.System.AudioVolume.Event += UpdateAudioVolumeState;

            _gpuCancellationTokenSource = new CancellationTokenSource();
        }

        private void Parent_PointerMoved(object sender, PointerEventArgs e)
        {
            _lastCursorMoveTime = Stopwatch.GetTimestamp();
        }

        private void Parent_PointerLeft(object sender, PointerEventArgs e)
        {
            Renderer.Cursor = ConfigurationState.Instance.Hid.EnableMouse ? InvisibleCursor : Cursor.Default;

            _isMouseInClient = false;
        }

        private void Parent_PointerEntered(object sender, PointerEventArgs e)
        {
            _isMouseInClient = true;
        }

        private void SetRendererWindowSize(Size size)
        {
            if (_renderer != null)
            {
                double scale = _parent.GetVisualRoot().RenderScaling;
                _renderer.Window.SetSize((int)(size.Width * scale), (int)(size.Height * scale));
            }
        }

        private unsafe void Renderer_ScreenCaptured(object sender, ScreenCaptureImageInfo e)
        {
            if (e.Data.Length > 0 && e.Height > 0 && e.Width > 0)
            {
                Task.Run(() =>
                {
                    lock (_lockObject)
                    {
                        var currentTime = DateTime.Now;
                        string filename = $"ryujinx_capture_{currentTime.Year}-{currentTime.Month:D2}-{currentTime.Day:D2}_{currentTime.Hour:D2}-{currentTime.Minute:D2}-{currentTime.Second:D2}.png";
                        string directory = AppDataManager.Mode switch
                        {
                            AppDataManager.LaunchMode.Portable => Path.Combine(AppDataManager.BaseDirPath, "screenshots"),
                            _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Ryujinx")
                        };

                        string path = Path.Combine(directory, filename);

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
            if (OperatingSystem.IsWindows())
            {
                _windowsMultimediaTimerResolution = new WindowsMultimediaTimerResolution(1);
            }

            DisplaySleep.Prevent();

            NpadManager.Initialize(Device, ConfigurationState.Instance.Hid.InputConfig, ConfigurationState.Instance.Hid.EnableKeyboard, ConfigurationState.Instance.Hid.EnableMouse);
            TouchScreenManager.Initialize(Device);

            _parent.ViewModel.IsGameRunning = true;

            string titleNameSection = string.IsNullOrWhiteSpace(Device.Application.TitleName)
                ? string.Empty
                : $" - {Device.Application.TitleName}";

            string titleVersionSection = string.IsNullOrWhiteSpace(Device.Application.DisplayVersion)
                ? string.Empty
                : $" v{Device.Application.DisplayVersion}";

            string titleIdSection = string.IsNullOrWhiteSpace(Device.Application.TitleIdText)
                ? string.Empty
                : $" ({Device.Application.TitleIdText.ToUpper()})";

            string titleArchSection = Device.Application.TitleIs64Bit
                ? " (64-bit)"
                : " (32-bit)";

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _parent.ViewModel.Title = $"Ryujinx Test {titleNameSection}{titleVersionSection}{titleIdSection}{titleArchSection}";
            });

            Renderer.SizeChanged += Window_SizeChanged;

            _isActive = true;

            _renderingThread.Start();

            MainLoop();

            Exit();
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
            Device?.System.ChangeDockedModeState(e.NewValue);
        }

        private void UpdateAudioVolumeState(object sender, ReactiveEventArgs<float> e)
        {
            Device?.SetVolume(e.NewValue);
        }

        public void Stop()
        {
            _isActive = false;
        }

        private void Exit()
        {
            (_keyboardInterface as AvaloniaKeyboard)?.Clear();

            if (_isStopped)
            {
                return;
            }

            _isStopped = true;
            _isActive = false;
        }

        public void DisposeContext()
        {
            Dispose();

            _isActive = false;

            _renderingThread.Join();

            DisplaySleep.Restore();

            Ptc.Close();
            PtcProfiler.Stop();
            NpadManager.Dispose();
            TouchScreenManager.Dispose();
            Device.Dispose();

            DisposeGpu();

            AppExit?.Invoke(this, EventArgs.Empty);
        }

        private void Dispose()
        {
            ConfigurationState.Instance.System.IgnoreMissingServices.Event -= UpdateIgnoreMissingServicesState;
            ConfigurationState.Instance.Graphics.AspectRatio.Event -= UpdateAspectRatioState;
            ConfigurationState.Instance.System.EnableDockedMode.Event -= UpdateDockedModeState;

            _gpuCancellationTokenSource.Cancel();
            _gpuCancellationTokenSource.Dispose();
        }

        public void DisposeGpu()
        {
            if (OperatingSystem.IsWindows())
            {
                _windowsMultimediaTimerResolution?.Dispose();
                _windowsMultimediaTimerResolution = null;
            }

            Device.DisposeGpu();
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

        public async Task<bool> LoadGuestApplication()
        {
            InitializeSwitchInstance();

            MainView.UpdateGraphicsConfig();

            SystemVersion firmwareVersion = ContentManager.GetCurrentFirmwareVersion();

            if (!SetupValidator.CanStartApplication(ContentManager, ApplicationPath, out UserError userError))
            {
                if (SetupValidator.CanFixStartApplication(ContentManager, ApplicationPath, userError, out firmwareVersion))
                {
                    if (userError == UserError.NoFirmware)
                    {
                        Device.Dispose();

                        return false;
                    }

                    if (!SetupValidator.TryFixStartApplication(ContentManager, ApplicationPath, userError, out _))
                    {
                        Device.Dispose();

                        return false;
                    }
                }
                else
                {
                    Device.Dispose();

                    return false;
                }
            }

            Logger.Notice.Print(LogClass.Application, $"Using Firmware Version: {firmwareVersion?.VersionString}");

            if (_isFirmwareTitle)
            {
                Logger.Info?.Print(LogClass.Application, "Loading as Firmware Title (NCA).");

                Device.LoadNca(ApplicationPath);
            }
            else if (Directory.Exists(ApplicationPath))
            {
                string[] romFsFiles = Directory.GetFiles(ApplicationPath, "*.istorage");

                if (romFsFiles.Length == 0)
                {
                    romFsFiles = Directory.GetFiles(ApplicationPath, "*.romfs");
                }

                if (romFsFiles.Length > 0)
                {
                    Logger.Info?.Print(LogClass.Application, "Loading as cart with RomFS.");

                    Device.LoadCart(ApplicationPath, romFsFiles[0]);
                }
                else
                {
                    Logger.Info?.Print(LogClass.Application, "Loading as cart WITHOUT RomFS.");

                    Device.LoadCart(ApplicationPath);
                }
            }
            else if (File.Exists(ApplicationPath))
            {
                switch (System.IO.Path.GetExtension(ApplicationPath).ToLowerInvariant())
                {
                    case ".xci":
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as XCI.");

                            Device.LoadXci(ApplicationPath);

                            break;
                        }
                    case ".nca":
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as NCA.");

                            Device.LoadNca(ApplicationPath);

                            break;
                        }
                    case ".nsp":
                    case ".pfs0":
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as NSP.");

                            Device.LoadNsp(ApplicationPath);

                            break;
                        }
                    default:
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as homebrew.");

                            try
                            {
                                Device.LoadProgram(ApplicationPath);
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

            _parent.ApplicationLibrary.LoadAndSaveMetaData(Device.Application.TitleIdText, appMetadata =>
            {
                appMetadata.LastPlayed = DateTime.UtcNow.ToString();
            });

            return true;
        }

        internal void Resume()
        {
            Device?.System.TogglePauseEmulation(false);
            _parent.ViewModel.IsPaused = false;
        }

        internal void Pause()
        {
            Device?.System.TogglePauseEmulation(true);
            _parent.ViewModel.IsPaused = true;
        }

        private void InitializeSwitchInstance()
        {
            VirtualFileSystem.ReloadKeySet();
            
            var vulkan = AvaloniaLocator.Current.GetService<VulkanPlatformInterface>();
            IRenderer renderer = new VulkanGraphicsDevice(vulkan.Instance.InternalHandle,
                vulkan.Device.InternalHandle,
                vulkan.PhysicalDevice.InternalHandle,
                vulkan.Device.Queue.InternalHandle,
                vulkan.PhysicalDevice.QueueFamilyIndex,
                vulkan.Device.Lock);

            IHardwareDeviceDriver deviceDriver = new DummyHardwareDeviceDriver();

            BackendThreading threadingMode = ConfigurationState.Instance.Graphics.BackendThreading;

            var isGALthreaded = threadingMode == BackendThreading.On || (threadingMode == BackendThreading.Auto && renderer.PreferThreading);

            if (isGALthreaded)
            {
                renderer = new ThreadedRenderer(renderer);
            }

            Logger.Info?.PrintMsg(LogClass.Gpu, $"Backend Threading ({threadingMode}): {isGALthreaded}");

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

                        deviceDriver = new OpenALHardwareDeviceDriver();
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

                        deviceDriver = new SDL2HardwareDeviceDriver();
                    }
                }
            }

            var memoryConfiguration = ConfigurationState.Instance.System.ExpandRam.Value
                ? HLE.MemoryConfiguration.MemoryConfiguration6GB
                : HLE.MemoryConfiguration.MemoryConfiguration4GB;

            IntegrityCheckLevel fsIntegrityCheckLevel = ConfigurationState.Instance.System.EnableFsIntegrityChecks ? IntegrityCheckLevel.ErrorOnInvalid : IntegrityCheckLevel.None;

            HLE.HLEConfiguration configuration = new HLE.HLEConfiguration(VirtualFileSystem,
                                                                          _parent.LibHacHorizonManager,
                                                                          ContentManager,
                                                                          _accountManager,
                                                                          _userChannelPersistence,
                                                                          renderer,
                                                                          deviceDriver,
                                                                          memoryConfiguration,
                                                                          null,//_parent.UiHandler,
                                                                          (SystemLanguage)ConfigurationState.Instance.System.Language.Value,
                                                                          (RegionCode)ConfigurationState.Instance.System.Region.Value,
                                                                          ConfigurationState.Instance.Graphics.EnableVsync,
                                                                          ConfigurationState.Instance.System.EnableDockedMode,
                                                                          ConfigurationState.Instance.System.EnablePtc,
                                                                          ConfigurationState.Instance.System.EnableInternetAccess,
                                                                          fsIntegrityCheckLevel,
                                                                          ConfigurationState.Instance.System.FsGlobalAccessLogMode,
                                                                          ConfigurationState.Instance.System.SystemTimeOffset,
                                                                          ConfigurationState.Instance.System.TimeZone,
                                                                          ConfigurationState.Instance.System.MemoryManagerMode,
                                                                          ConfigurationState.Instance.System.IgnoreMissingServices,
                                                                          ConfigurationState.Instance.Graphics.AspectRatio,
                                                                          ConfigurationState.Instance.System.AudioVolume);

            Device = new Switch(configuration);
        }

        private void Window_SizeChanged(object sender, Size e)
        {
            Width = (int)e.Width;
            Height = (int)e.Height;

            SetRendererWindowSize(e);
        }

        private void MainLoop()
        {
            while (_isActive)
            {
                UpdateFrame();

                // Polling becomes expensive if it's not slept
                Thread.Sleep(1);
            }
        }

        private unsafe void RenderLoop()
        {
            IRenderer renderer = Device.Gpu.Renderer;

            if (renderer is ThreadedRenderer tr)
            {
                renderer = tr.BaseRenderer;
            }

            _renderer = renderer;

            _renderer.ScreenCaptured += Renderer_ScreenCaptured;

            Device.Gpu.Renderer.Initialize(_glLogLevel);

            Width = (int)Renderer.Bounds.Width;
            Height = (int)Renderer.Bounds.Height;

            var scale = _parent.GetVisualRoot().RenderScaling;
            _renderer.Window.SetSize((int)(Width * scale), (int)(Height * scale));

            Device.Gpu.Renderer.RunLoop(() =>
            {
                Device.Gpu.SetGpuThread();
                Device.Gpu.InitializeShaderCache(_gpuCancellationTokenSource.Token);
                Translator.IsReadyForTranslation.Set();

                Renderer.Start();

                Renderer.QueueRender();

                while (_isActive)
                {
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
                            _parent.SwitchToGameControl();
                        }

                        Device.PresentFrame(Present);
                    }
                }

                Renderer.Stop();
            });

            Renderer.SizeChanged -= Window_SizeChanged;
        }

        private void Present(object image)
        {
            // Run a status update only when a frame is to be drawn. This prevents from updating the ui and wasting a render when no frame is queued
            string dockedMode = ConfigurationState.Instance.System.EnableDockedMode ? LocaleManager.Instance["Docked"] : LocaleManager.Instance["Handheld"];
            float scale = GraphicsConfig.ResScale;

            if (scale != 1)
            {
                dockedMode += $" ({scale}x)";
            }

            string vendor = _renderer is VulkanGraphicsDevice renderer ? renderer.GpuVendor : "";

            StatusUpdatedEvent?.Invoke(this, new StatusUpdatedEventArgs(
                Device.EnableDeviceVsync,
                Device.GetVolume(),
                dockedMode,
                ConfigurationState.Instance.Graphics.AspectRatio.Value.ToText(),
                LocaleManager.Instance["Game"] + $": {Device.Statistics.GetGameFrameRate():00.00} FPS ({Device.Statistics.GetGameFrameTime():00.00} ms)",
                $"FIFO: {Device.Statistics.GetFifoPercent():00.00} %",
                $"GPU: {vendor}"));

            Renderer.Present(image);
        }

        private void HandleScreenState(KeyboardStateSnapshot keyboard, KeyboardStateSnapshot lastKeyboard)
        {
            if (ConfigurationState.Instance.Hid.EnableMouse)
            {
                if (_isMouseInClient)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        _parent.Cursor = InvisibleCursor;
                    });
                }
            }
            else
            {
                if (_hideCursorOnIdle)
                {
                    long cursorMoveDelta = Stopwatch.GetTimestamp() - _lastCursorMoveTime;

                    Dispatcher.UIThread.Post(() =>
                    {
                        _parent.Cursor = cursorMoveDelta >= CursorHideIdleTime * Stopwatch.Frequency ? InvisibleCursor : Cursor.Default;
                    });
                }
            }
        }

        private bool UpdateFrame()
        {
            if (!_isActive)
            {
                return false;
            }
                Dispatcher.UIThread.Post(() =>
                {
                    KeyboardStateSnapshot keyboard = _keyboardInterface.GetKeyboardStateSnapshot();

                    HandleScreenState(keyboard, _lastKeyboardSnapshot);

                    if (keyboard.IsPressed(Key.Delete))
                    {
                            Ptc.Continue();
                    }

                    _lastKeyboardSnapshot = keyboard;
                });

            NpadManager.Update(ConfigurationState.Instance.Graphics.AspectRatio.Value.ToFloat());

            // parent.IsActive. IsVisible can't be access outside of ui thread
            if (true)
            {
                KeyboardHotkeyState currentHotkeyState = GetHotkeyState();

                if (currentHotkeyState != _prevHotkeyState)
                {
                    switch (currentHotkeyState)
                    {
                        case KeyboardHotkeyState.ToggleVSync:
                            Device.EnableDeviceVsync = !Device.EnableDeviceVsync;
                            break;
                        case KeyboardHotkeyState.Screenshot:
                            ScreenshotRequested = true;
                            break;
                        case KeyboardHotkeyState.Pause:
                            if (_parent.ViewModel.IsPaused)
                            {
                                Resume();
                            }
                            else
                            {
                                Pause();
                            }
                            break;
                        case KeyboardHotkeyState.ToggleMute:
                            if (Device.IsAudioMuted())
                            {
                                Device.SetVolume(ConfigurationState.Instance.System.AudioVolume);
                            }
                            else
                            {
                                Device.SetVolume(0);
                            }
                            break;
                        case KeyboardHotkeyState.None:
                            (_keyboardInterface as AvaloniaKeyboard).Clear();
                            break;
                    }
                }

                _prevHotkeyState = currentHotkeyState;

                if (ScreenshotRequested)
                {
                    ScreenshotRequested = false;
                    _renderer.Screenshot();
                }
            }

            // Touchscreen
            bool hasTouch = false;

            if (true && !ConfigurationState.Instance.Hid.EnableMouse)
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

        private KeyboardHotkeyState GetHotkeyState()
        {
            KeyboardHotkeyState state = KeyboardHotkeyState.None;

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ToggleVsync))
            {
                state = KeyboardHotkeyState.ToggleVSync;
            }
            else if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.Screenshot))
            {
                state = KeyboardHotkeyState.Screenshot;
            }
            else if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ShowUi))
            {
                state = KeyboardHotkeyState.ShowUi;
            }
            else if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.Pause))
            {
                state = KeyboardHotkeyState.Pause;
            }
            else if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ToggleMute))
            {
                state = KeyboardHotkeyState.ToggleMute;
            }

            return state;
        }
    }
}