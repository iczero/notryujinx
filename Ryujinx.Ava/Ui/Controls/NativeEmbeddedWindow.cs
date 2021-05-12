using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Ryujinx.Ava.Input.Glfw;
using Ryujinx.Common.Configuration;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Window = OpenTK.Windowing.GraphicsLibraryFramework.Window;

namespace Ryujinx.Ava.Ui.Controls
{
    public class NativeEmbeddedWindow : NativeControlHost
    {
        public event EventHandler<KeyEventArgs> KeyPressed;
        public event EventHandler<KeyEventArgs> KeyReleased;
        public event EventHandler MouseDown;
        public event EventHandler MouseUp;
        public event EventHandler<(double X, double Y)> MouseMove;
        public event EventHandler<IntPtr> WindowCreated;
        public event EventHandler<Size> SizeChanged;

        private bool _init;
        private double _scale;
        private bool _isFullScreen;

        protected int Major { get; set; }
        protected int Minor { get; set; }
        protected GraphicsDebugLevel DebugLevel { get; set; }

        protected IntPtr WindowHandle { get; set; }
        protected IntPtr X11Display { get; set; }

        public GameWindow GLFWWindow { get; set; }

        private IPlatformHandle _handle;

        public bool RendererFocused => GLFWWindow.IsFocused;

        public NativeEmbeddedWindow(double scale)
        {
            _scale = scale;

            IObservable<Rect> stateObservable = this.GetObservable(BoundsProperty);

            MinHeight = 720;
            MinWidth = 1280;

            Margin = new Thickness();

            stateObservable.Subscribe(StateChanged);

            IObservable<Rect> resizeObserverable = this.GetObservable(BoundsProperty);

            resizeObserverable.Subscribe(Resized);

            GLFW.Init();
        }

        private void Resized(Rect rect)
        {
            SizeChanged?.Invoke(this, rect.Size);
        }

        public unsafe bool IsFullscreen
        {
            get
            {
                return _isFullScreen;
            }
            set
            {
                _isFullScreen = value;

                UpdateSizes(_scale);
            }
        }

        public unsafe void UpdateSizes(double scale)
        {
            _scale = scale;

            if (GLFWWindow != null)
            {
                if (!_isFullScreen)
                {
                    GLFWWindow.Size = new Vector2i((int)(Bounds.Width * _scale), (int)(Bounds.Height * _scale));
                }
                else
                {
                    var mode = GLFW.GetVideoMode(GLFW.GetPrimaryMonitor());

                    GLFWWindow.Size = new Vector2i(mode->Width, mode->Height);

                    if (VisualRoot != null)
                    {
                        var position = this.PointToScreen(Bounds.Position);
                        GLFWWindow.Location = new Vector2i(position.X, position.Y);
                    }

                    Bounds = Bounds;
                }
            }
        }

        protected virtual void OnWindowDestroyed() { }

        protected virtual void OnWindowDestroying()
        {
            WindowHandle = IntPtr.Zero;
            X11Display = IntPtr.Zero;
        }

        public virtual void OnWindowCreated()
        {
        }

        public void Start()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _handle = CreateLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _handle = CreateWin32();
            }
        }

        public void Destroy()
        {
            OnWindowDestroying();

            Task.Run(async () =>
            {
                // Delay deleting the actual window, because avalonia does not release it early enough
                await Task.Delay(2000);
                GLFWWindow.Dispose();

                OnWindowDestroyed();

                GLFW.Terminate();
            });
        }

        private async void StateChanged(Rect rect)
        {
            SizeChanged?.Invoke(this, rect.Size);

            if (!_init && WindowHandle != IntPtr.Zero && rect.Size != default)
            {
                _init = true;
                await Task.Run(() =>
                {
                    OnWindowCreated();

                    WindowCreated?.Invoke(this, WindowHandle);
                });
            }
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            try
            {
                if (_handle != null)
                {
                    return _handle;
                }
            }
            finally
            {
                Task.Run(ProcessWindowEvents);
            }

            return base.CreateNativeControlCore(parent);
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control) { }

        private unsafe IPlatformHandle CreateLinux()
        {
            CreateWindow();

            WindowHandle = (IntPtr)GLFW.GetX11Window(GLFWWindow.WindowPtr);
            X11Display = (IntPtr)GlfwGetX11Display(GLFWWindow.WindowPtr);

            return new PlatformHandle(WindowHandle, "X11");
        }

        private unsafe IPlatformHandle CreateWin32()
        {
            CreateWindow();

            WindowHandle = GLFW.GetWin32Window(GLFWWindow.WindowPtr);

            return new PlatformHandle(WindowHandle, "HWND");
        }

        [DllImport("libglfw.so.3", EntryPoint = "glfwGetX11Display")]
        public static extern unsafe uint GlfwGetX11Display(Window* window);

        private unsafe void CreateWindow()
        {
            ContextFlags flags = DebugLevel != GraphicsDebugLevel.None
                ? ContextFlags.Debug
                : ContextFlags.ForwardCompatible;
            flags |= ContextFlags.ForwardCompatible;
            GLFWWindow = new GameWindow(
                new GameWindowSettings {IsMultiThreaded = true, RenderFrequency = 60, UpdateFrequency = 60},
                new NativeWindowSettings
                {
                    API = this is OpenGlEmbeddedWindow ? ContextAPI.OpenGL : ContextAPI.NoAPI,
                    APIVersion = new Version(Major, Minor),
                    Profile = ContextProfile.Core,
                    IsEventDriven = true,
                    Flags = flags,
                    AutoLoadBindings = false,
                    Size = new Vector2i(200, 200),
                    StartVisible = false,
                    Title = "Renderer"
                });

            GLFWWindow.WindowBorder = WindowBorder.Hidden;

            GLFW.MakeContextCurrent(null);

            GLFWWindow.MouseDown += Window_MouseDown;
            GLFWWindow.MouseUp += Window_MouseUp;
            GLFWWindow.MouseMove += Window_MouseMove;

            // Glfw Mouse Passthrough doesn't work on linux, so we pass events to the keyboard driver the hard way
            GLFWWindow.KeyDown += Window_KeyDown;
            GLFWWindow.KeyUp += Window_KeyUp;
        }

        private void RefocusMainWindow()
        {
            if (Avalonia.Application.Current.ApplicationLifetime is ClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow.Activate();
            }
        }

        private void Window_KeyUp(KeyboardKeyEventArgs obj)
        {
            GlfwKey key = Enum.Parse<GlfwKey>(obj.Key.ToString());
            KeyEventArgs keyEvent = new() {Key = (Key)key};

            KeyReleased?.Invoke(this, keyEvent);
        }

        private void Window_KeyDown(KeyboardKeyEventArgs obj)
        {
            GlfwKey key = Enum.Parse<GlfwKey>(obj.Key.ToString());
            KeyEventArgs keyEvent = new() {Key = (Key)key};

            KeyPressed?.Invoke(this, keyEvent);
        }

        public void ProcessWindowEvents()
        {
            while (WindowHandle != IntPtr.Zero)
            {
                GLFWWindow.ProcessEvents();
            }
        }

        private void Window_MouseMove(MouseMoveEventArgs obj)
        {
            MouseMove?.Invoke(this, (obj.X, obj.Y));
        }

        private void Window_MouseUp(MouseButtonEventArgs obj)
        {
            MouseUp?.Invoke(this, EventArgs.Empty);

            RefocusMainWindow();
        }

        private void Window_MouseDown(MouseButtonEventArgs obj)
        {
            MouseDown?.Invoke(this, EventArgs.Empty);
        }

        public virtual void Present() { }
    }
}