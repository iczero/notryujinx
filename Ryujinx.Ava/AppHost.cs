using ARMeilleure.Translation;
using ARMeilleure.Translation.PTC;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MessageBoxSlim.Avalonia;
using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.Audio.Backends.OpenAL;
using Ryujinx.Audio.Backends.SDL2;
using Ryujinx.Audio.Backends.SoundIo;
using Ryujinx.Audio.Integration;
using Ryujinx.Ava.Application.Module;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Configuration;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu;
using Ryujinx.Graphics.OpenGL;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.FileSystem.Content;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.Input;
using Ryujinx.Input.Avalonia;
using Ryujinx.Input.HLE;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using InputManager = Ryujinx.Input.HLE.InputManager;
using Key = Ryujinx.Input.Key;
using Switch = Ryujinx.HLE.Switch;

namespace Ryujinx.Ava
{
    public class AppHost : IDisposable
    {
        private const int SwitchPanelHeight  = 720;
        private const int TargetFps          = 60;
        private const int CursorHideIdleTime = 8; // Hide Cursor seconds

        private static readonly Cursor InvisibleCursor = new Cursor(StandardCursorType.None);

        private readonly AccountManager _accountManager;

        private readonly Stopwatch _chrono;

        private readonly InputManager _inputManager;

        private readonly IKeyboard _keyboardInterface;

        private readonly MainWindow _parent;

        private readonly long _ticksPerFrame;

        private readonly GraphicsDebugLevel _glLogLevel;

        private bool _hideCursorOnIdle;
        private bool _isActive;
        private long _lastCursorMoveTime;

        private Thread _mainThread;

        private bool _mousePressed;
        private int _mouseX;
        private int _mouseY;

        private KeyboardHotkeyState _prevHotkeyState;

        private IRenderer _renderer;
        private readonly Thread _renderingThread;

        private long _ticks;

        private bool _toggleDockedMode;
        private bool _toggleFullscreen;

        public event EventHandler AppExit;
        public event EventHandler<StatusUpdatedEventArgs> StatusUpdatedEvent;

        public NativeEmbeddedWindow Window            { get; }
        public VirtualFileSystem    VirtualFileSystem { get; }
        public ContentManager       ContentManager    { get; }
        public Switch               EmulationContext  { get; set; }
        public NpadManager          NpadManager       { get; }

        public int    Width   { get; private set; }
        public int    Height  { get; private set; }
        public double Scaling { get; set; }
        public string Path    { get; }

        public AppHost(
            NativeEmbeddedWindow window,
            InputManager         inputManager,
            double               scaling,
            string               path,
            VirtualFileSystem    virtualFileSystem,
            ContentManager       contentManager,
            AccountManager       accountManager,
            MainWindow           parent)
        {
            _parent             = parent;
            _inputManager       = inputManager;
            _accountManager     = accountManager;
            _keyboardInterface  = (IKeyboard)_inputManager.KeyboardDriver.GetGamepad("0");
            _renderingThread    = new Thread(RenderLoop) { Name = "GUI.RenderThread" };
            _chrono             = new Stopwatch();
            _hideCursorOnIdle   = ConfigurationState.Instance.HideCursorOnIdle;
            _lastCursorMoveTime = Stopwatch.GetTimestamp();
            _ticksPerFrame      = Stopwatch.Frequency / TargetFps;
            _glLogLevel         = ConfigurationState.Instance.Logger.GraphicsDebugLevel;

            Window            = window;
            NpadManager       = _inputManager.CreateNpadManager();
            Scaling           = scaling;
            Path              = path;
            VirtualFileSystem = virtualFileSystem;
            ContentManager    = contentManager;

            ((AvaloniaKeyboardDriver)_inputManager.KeyboardDriver).AddControl(Window);

            NpadManager.ReloadConfiguration(ConfigurationState.Instance.Hid.InputConfig.Value.ToList());

            parent.PointerMoved    += Parent_PointerMoved;
            parent.PointerPressed  += Parent_PointerPressed;
            parent.PointerReleased += Parent_PointerReleased;
            parent.Deactivated     += Parent_Deactivate;

            window.MouseDown += Window_MouseDown;
            window.MouseUp   += Window_MouseUp;
            window.MouseMove += Window_MouseMove;

            ConfigurationState.Instance.HideCursorOnIdle.Event += HideCursorState_Changed;
        }

        private void SetRendererWindowSize(Size size, double scale)
        {
            if (_renderer != null)
            {
                _renderer.Window.SetSize((int)(size.Width * scale), (int)(size.Height * scale));

                Scaling = scale;
            }
        }

        public void Start()
        {
            if (LoadGuestApplication())
            {
                _parent.ViewModel.IsGameRunning = true;

                string titleNameSection = string.IsNullOrWhiteSpace(EmulationContext.Application.TitleName)
                    ? string.Empty
                    : $" - {EmulationContext.Application.TitleName}";

                string titleVersionSection = string.IsNullOrWhiteSpace(EmulationContext.Application.DisplayVersion)
                    ? string.Empty
                    : $" v{EmulationContext.Application.DisplayVersion}";

                string titleIdSection = string.IsNullOrWhiteSpace(EmulationContext.Application.TitleIdText)
                    ? string.Empty
                    : $" ({EmulationContext.Application.TitleIdText.ToUpper()})";

                string titleArchSection = EmulationContext.Application.TitleIs64Bit
                    ? " (64-bit)"
                    : " (32-bit)";

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _parent.Title = $"Ryujinx {Program.Version}{titleNameSection}{titleVersionSection}{titleIdSection}{titleArchSection}";
                });

                _parent.ViewModel.HandleShaderProgress(EmulationContext);

                Window.SizeChanged += Window_SizeChanged;

                _isActive = true;

                _renderingThread.Start();

                _mainThread = new Thread(MainLoop)
                {
                    Name = "GUI.UpdateThread"
                };
                _mainThread.Start();

                Thread nvStutterWorkaround = new Thread(NVStutterWorkaround)
                {
                    Name = "GUI.NVStutterWorkaround"
                };
                nvStutterWorkaround.Start();
            }
        }

        public void Exit()
        {
            ((AvaloniaKeyboardDriver)_inputManager.KeyboardDriver).RemoveControl(Window);

            if (!_isActive)
            {
                return;
            }

            _isActive = false;

            _mainThread.Join();
            _renderingThread.Join();

            EmulationContext.DisposeGpu();
            NpadManager.Dispose();
            EmulationContext.Dispose();

            AppExit?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Exit();
        }

        private void Window_MouseMove(object sender, (double X, double Y) e)
        {
            _mouseX = (int)e.X;
            _mouseY = (int)e.Y;

            if (_hideCursorOnIdle)
            {
                _lastCursorMoveTime = Stopwatch.GetTimestamp();
            }
        }

        private void Window_MouseUp(object sender, EventArgs e)
        {
            _mousePressed = false;
        }

        private void Window_MouseDown(object sender, EventArgs e)
        {
            _mousePressed = true;
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

        private void Parent_Deactivate(object sender, EventArgs e)
        {
            _mousePressed = false;
        }

        private void Parent_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _mousePressed = false;
        }

        private void Parent_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            _mousePressed = true;
        }

        private void Parent_PointerMoved(object sender, PointerEventArgs e)
        {
            Point position = e.GetPosition(_parent.GlRenderer);

            _mouseX = (int)position.X;
            _mouseY = (int)position.Y;

            if (_hideCursorOnIdle)
            {
                _lastCursorMoveTime = Stopwatch.GetTimestamp();
            }
        }

        private bool LoadGuestApplication()
        {
            InitializeSwitchInstance();

            MainWindow.UpdateGraphicsConfig();

            SystemVersion firmwareVersion = ContentManager.GetCurrentFirmwareVersion();

            if (!SetupValidator.CanStartApplication(ContentManager, Path, out UserError userError))
            {
                if (SetupValidator.CanFixStartApplication(ContentManager, Path, userError, out firmwareVersion))
                {
                    if (userError == UserError.NoFirmware)
                    {
                        string message = $"Would you like to install the firmware embedded in this game? (Firmware {firmwareVersion.VersionString})";

                        AvaDialog dialog = AvaDialog.CreateConfirmationDialog("No Firmware Installed", message, _parent);

                        UserResult response = dialog.Run().Result;

                        if (response != UserResult.Yes)
                        {
                            UserErrorDialog.CreateUserErrorDialog(userError, _parent);

                            EmulationContext.Dispose();

                            return false;
                        }
                    }

                    if (!SetupValidator.TryFixStartApplication(ContentManager, Path, userError, out _))
                    {
                        UserErrorDialog.CreateUserErrorDialog(userError, _parent);

                        EmulationContext.Dispose();

                        return false;
                    }

                    // Tell the user that we installed a firmware for them.
                    if (userError == UserError.NoFirmware)
                    {
                        firmwareVersion = ContentManager.GetCurrentFirmwareVersion();

                        _parent.RefreshFirmwareStatus();

                        string message = $"No installed firmware was found but Ryujinx was able to install firmware {firmwareVersion.VersionString} from the provided game.\nThe emulator will now start.";

                        AvaDialog.CreateInfoDialog($"Firmware {firmwareVersion.VersionString} was installed", message, _parent);
                    }
                }
                else
                {
                    UserErrorDialog.CreateUserErrorDialog(userError, _parent);

                    EmulationContext.Dispose();

                    return false;
                }
            }

            Logger.Notice.Print(LogClass.Application, $"Using Firmware Version: {firmwareVersion?.VersionString}");

            if (Directory.Exists(Path))
            {
                string[] romFsFiles = Directory.GetFiles(Path, "*.istorage");

                if (romFsFiles.Length == 0)
                {
                    romFsFiles = Directory.GetFiles(Path, "*.romfs");
                }

                if (romFsFiles.Length > 0)
                {
                    Logger.Info?.Print(LogClass.Application, "Loading as cart with RomFS.");

                    EmulationContext.LoadCart(Path, romFsFiles[0]);
                }
                else
                {
                    Logger.Info?.Print(LogClass.Application, "Loading as cart WITHOUT RomFS.");

                    EmulationContext.LoadCart(Path);
                }
            }
            else if (File.Exists(Path))
            {
                switch (System.IO.Path.GetExtension(Path).ToLowerInvariant())
                {
                    case ".xci":
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as XCI.");

                            EmulationContext.LoadXci(Path);

                            break;
                        }
                    case ".nca":
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as NCA.");

                            EmulationContext.LoadNca(Path);

                            break;
                        }
                    case ".nsp":
                    case ".pfs0":
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as NSP.");

                            EmulationContext.LoadNsp(Path);

                            break;
                        }
                    default:
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as homebrew.");

                            try
                            {
                                EmulationContext.LoadProgram(Path);
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                Logger.Error?.Print(LogClass.Application, "The specified file is not supported by Ryujinx.");

                                Exit();

                                return false;
                            }

                            break;
                        }
                }
            }
            else
            {
                Logger.Warning?.Print(LogClass.Application, "Please specify a valid XCI/NCA/NSP/PFS0/NRO file.");

                Exit();

                return false;
            }

            DiscordIntegrationModule.SwitchToPlayingState(EmulationContext.Application.TitleIdText, EmulationContext.Application.TitleName);

            ApplicationLibrary.LoadAndSaveMetaData(EmulationContext.Application.TitleIdText, appMetadata =>
            {
                appMetadata.LastPlayed = DateTime.UtcNow.ToString();
            });

            return true;
        }

        private void InitializeSwitchInstance()
        {
            VirtualFileSystem.Reload();

            IRenderer             renderer     = new Renderer();
            IHardwareDeviceDriver deviceDriver = new DummyHardwareDeviceDriver();

            if (ConfigurationState.Instance.System.AudioBackend.Value == AudioBackend.SDL2)
            {
                if (SDL2HardwareDeviceDriver.IsSupported)
                {
                    deviceDriver = new SDL2HardwareDeviceDriver();
                }
                else
                {
                    Logger.Warning?.Print(LogClass.Audio, "SDL2 audio is not supported, falling back to dummy audio out.");
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
                    Logger.Warning?.Print(LogClass.Audio, "SoundIO is not supported, falling back to dummy audio out.");
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

            MemoryConfiguration memoryConfiguration = ConfigurationState.Instance.System.ExpandRam.Value
                ? MemoryConfiguration.MemoryConfiguration6GB
                : MemoryConfiguration.MemoryConfiguration4GB;

            EmulationContext = new Switch(VirtualFileSystem, ContentManager, _accountManager, MainWindow.UserChannelPersistence, renderer, deviceDriver, memoryConfiguration)
            {
                UiHandler = _parent.UiHandler
            };

            EmulationContext.Initialize();
        }

        private void Window_SizeChanged(object sender, Size e)
        {
            Width  = (int)e.Width;
            Height = (int)e.Height;

            SetRendererWindowSize(e, Scaling);
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
                if (_parent.ViewModel.StartGamesInFullscreen)
                {
                    _parent.WindowState = WindowState.FullScreen;
                }

                if (_parent.WindowState == WindowState.FullScreen)
                {
                    _parent.ViewModel.ShowMenuAndStatusBar = false;
                }

                Window.IsFullscreen = _parent.WindowState == WindowState.FullScreen;
            });

            _renderer = EmulationContext.Gpu.Renderer;

            if (Window is OpenGlEmbeddedWindow openGlEmbeddedWindow)
            {
                (_renderer as Renderer).InitializeBackgroundContext(AvaloniaOpenGLContextHelper.CreateBackgroundContext(Window.GLFWWindow.WindowPtr, _glLogLevel != GraphicsDebugLevel.None));

                openGlEmbeddedWindow.MakeCurrent();
            }

            EmulationContext.Gpu.Renderer.Initialize(_glLogLevel);
            EmulationContext.Gpu.InitializeShaderCache();

            Translator.IsReadyForTranslation.Set();

            Width  = (int)Window.Bounds.Width;
            Height = (int)Window.Bounds.Height;

            _renderer.Window.SetSize((int)(Width * Scaling), (int)(Height * Scaling));

            while (_isActive)
            {
                _ticks += _chrono.ElapsedTicks;

                _chrono.Restart();

                if (EmulationContext.WaitFifo())
                {
                    EmulationContext.Statistics.RecordFifoStart();
                    EmulationContext.ProcessFrame();
                    EmulationContext.Statistics.RecordFifoEnd();
                }

                while (EmulationContext.ConsumeFrameAvailable())
                {
                    EmulationContext.PresentFrame(Present);
                }

                if (_ticks >= _ticksPerFrame)
                {
                    string dockedMode = ConfigurationState.Instance.System.EnableDockedMode ? "Docked" : "Handheld";
                    float  scale      = GraphicsConfig.ResScale;

                    if (scale != 1)
                    {
                        dockedMode += $" ({scale}x)";
                    }

                    string vendor = _renderer is Renderer renderer ? renderer.GpuVendor : "Vulkan Test";

                    StatusUpdatedEvent?.Invoke(this, new StatusUpdatedEventArgs(
                        EmulationContext.EnableDeviceVsync,
                        dockedMode,
                        ConfigurationState.Instance.Graphics.AspectRatio.Value.ToText(),
                        $"Game: {EmulationContext.Statistics.GetGameFrameRate():00.00} FPS",
                        $"FIFO: {EmulationContext.Statistics.GetFifoPercent():00.00} %",
                        $"GPU: {vendor}"));

                    _ticks = Math.Min(_ticks - _ticksPerFrame, _ticksPerFrame);
                }
            }

            if (Window is OpenGlEmbeddedWindow window)
            {
                window.MakeCurrent(null);
            }

            Window.SizeChanged -= Window_SizeChanged;
        }

        private void Present()
        {
            Window.Present();
        }

        private async void HandleScreenState(KeyboardStateSnapshot keyboard)
        {
            bool toggleFullscreen = keyboard.IsPressed(Key.F11)
                || ((keyboard.IsPressed(Key.AltLeft) || keyboard.IsPressed(Key.AltRight)) && keyboard.IsPressed(Key.Enter))
                || keyboard.IsPressed(Key.Escape);

            bool fullScreenToggled = _parent.WindowState == WindowState.FullScreen;

            if (toggleFullscreen != _toggleFullscreen)
            {
                if (toggleFullscreen)
                {
                    if (fullScreenToggled)
                    {
                        _parent.WindowState                    = WindowState.Normal;
                        _parent.ViewModel.ShowMenuAndStatusBar = true;
                    }
                    else
                    {
                        if (keyboard.IsPressed(Key.Escape))
                        {
                            if (!ConfigurationState.Instance.ShowConfirmExit)
                            {
                                Exit();
                            }
                            else
                            {
                                bool shouldExit = await AvaDialog.CreateExitDialog(_parent);
                                if (shouldExit)
                                {
                                    Exit();
                                }
                            }
                        }
                        else
                        {
                            _parent.WindowState                    = WindowState.FullScreen;
                            _parent.ViewModel.ShowMenuAndStatusBar = false;
                        }
                    }
                }
            }

            Window.IsFullscreen                    = fullScreenToggled;
            _parent.ViewModel.ShowMenuAndStatusBar = !fullScreenToggled;
            _toggleFullscreen                      = toggleFullscreen;

            bool toggleDockedMode = keyboard.IsPressed(Key.F9);

            if (toggleDockedMode != _toggleDockedMode)
            {
                if (toggleDockedMode)
                {
                    ConfigurationState.Instance.System.EnableDockedMode.Value = !ConfigurationState.Instance.System.EnableDockedMode.Value;
                }
            }

            _toggleDockedMode = toggleDockedMode;

            if (_hideCursorOnIdle)
            {
                long cursorMoveDelta = Stopwatch.GetTimestamp() - _lastCursorMoveTime;
                Dispatcher.UIThread.Post(() =>
                {
                    _parent.Cursor = cursorMoveDelta >= CursorHideIdleTime * Stopwatch.Frequency ? InvisibleCursor : Cursor.Default;
                });
            }
        }

        private bool UpdateFrame()
        {
            if (!_isActive)
            {
                return true;
            }

            if (_parent.IsActive)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    KeyboardStateSnapshot keyboard = _keyboardInterface.GetKeyboardStateSnapshot();

                    HandleScreenState(keyboard);

                    if (keyboard.IsPressed(Key.Delete))
                    {
                        if (_parent.WindowState != WindowState.FullScreen)
                        {
                            Ptc.Continue();
                        }
                    }
                });
            }

            NpadManager.Update(EmulationContext.Hid, EmulationContext.TamperMachine);

            if (_parent.IsActive)
            {
                KeyboardHotkeyState currentHotkeyState = GetHotkeyState();

                if (currentHotkeyState.HasFlag(KeyboardHotkeyState.ToggleVSync) && !_prevHotkeyState.HasFlag(KeyboardHotkeyState.ToggleVSync))
                {
                    EmulationContext.EnableDeviceVsync = !EmulationContext.EnableDeviceVsync;
                }

                _prevHotkeyState = currentHotkeyState;
            }

            //Touchscreen
            bool hasTouch = false;

            // Get screen touch position from left mouse click
            // OpenTK always captures mouse events, even if out of focus, so check if window is focused.
            if ((_parent.IsActive || Window.RendererFocused) && _mousePressed)
            {
                float aspectWidth = SwitchPanelHeight * ConfigurationState.Instance.Graphics.AspectRatio.Value.ToFloat();

                int screenWidth     = (int)Window.Bounds.Width;
                int screenHeight    = (int)Window.Bounds.Height;
                int allocatedWidth  = (int)Window.Bounds.Width;
                int allocatedHeight = (int)Window.Bounds.Height;

                if (allocatedWidth > allocatedHeight * aspectWidth / SwitchPanelHeight)
                {
                    screenWidth = (int)(allocatedHeight * aspectWidth) / SwitchPanelHeight;
                }
                else
                {
                    screenHeight = allocatedWidth * SwitchPanelHeight / (int)aspectWidth;
                }

                int startX = (allocatedWidth  - screenWidth)  >> 1;
                int startY = (allocatedHeight - screenHeight) >> 1;

                int endX = startX + screenWidth;
                int endY = startY + screenHeight;


                if (_mouseX >= startX &&
                    _mouseY >= startY &&
                    _mouseX < endX &&
                    _mouseY < endY)
                {
                    int screenMouseX = _mouseX - startX;
                    int screenMouseY = _mouseY - startY;

                    int mX = screenMouseX * (int)aspectWidth / screenWidth;
                    int mY = screenMouseY * SwitchPanelHeight / screenHeight;

                    TouchPoint currentPoint = new()
                    {
                        X = (uint)mX,
                        Y = (uint)mY,

                        // Placeholder values till more data is acquired
                        DiameterX = 10,
                        DiameterY = 10,
                        Angle = 90
                    };

                    hasTouch = true;

                    EmulationContext.Hid.Touchscreen.Update(currentPoint);
                }
            }

            if (!hasTouch)
            {
                EmulationContext.Hid.Touchscreen.Update();
            }

            EmulationContext.Hid.DebugPad.Update();

            return true;
        }

        private KeyboardHotkeyState GetHotkeyState()
        {
            KeyboardHotkeyState state = KeyboardHotkeyState.None;

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ToggleVsync))
            {
                state |= KeyboardHotkeyState.ToggleVSync;
            }

            return state;
        }
    }
}