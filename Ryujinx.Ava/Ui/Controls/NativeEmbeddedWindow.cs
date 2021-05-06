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
        
        protected int Major { get; set; }
        protected int Minor { get; set; }
        protected GraphicsDebugLevel DebugLevel { get; set; }

        protected IntPtr WindowHandle { get; set; }
        protected IntPtr X11Display { get; set; }

        public GameWindow GLFWWindow { get; set; }

        public bool RendererFocused => GLFWWindow.IsFocused;

        public NativeEmbeddedWindow()
        {
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
                return GLFWWindow.IsFullscreen;
            }
            set
            {
                if (GLFWWindow != null)
                {
                    if (!value)
                    {
                        GLFWWindow.Size = new Vector2i((int)Bounds.Width, (int)Bounds.Height);
                    }
                    else
                    {
                        var mode = GLFW.GetVideoMode(GLFW.GetPrimaryMonitor());

                        GLFWWindow.Size = new Vector2i(mode->Width, mode->Height);
                        var position = this.PointToScreen(this.Bounds.Position);
                        GLFWWindow.Location = new Vector2i(position.X, position.Y);

                        Bounds = Bounds;
                    }
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

        private async void StateChanged(Rect rect)
        {
            if (!_init && WindowHandle != IntPtr.Zero)
            {
                _init = true;

                await Task.Run(() =>
                {
                    OnWindowCreated();

                    WindowCreated?.Invoke(this, WindowHandle);
                });
            }

            SizeChanged?.Invoke(this, rect.Size);
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return CreateLinux(parent);
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return CreateWin32(parent);
                }
            }
            finally
            {
                Task.Run(ProcessWindowEvents);
            }

            return base.CreateNativeControlCore(parent);
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            OnWindowDestroying();

            GLFWWindow.Dispose();

            OnWindowDestroyed();

            WindowHandle = IntPtr.Zero;
            X11Display = IntPtr.Zero;

            GLFW.Terminate();
        }

        private unsafe IPlatformHandle CreateLinux(IPlatformHandle parent)
        {
            CreateWindow();

            WindowHandle = (IntPtr)GLFW.GetX11Window(GLFWWindow.WindowPtr);
            X11Display = (IntPtr)GlfwGetX11Display(GLFWWindow.WindowPtr);

            return new PlatformHandle(WindowHandle, "X11");
            ;
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

        private unsafe IPlatformHandle CreateWin32(IPlatformHandle parent)
        {
            CreateWindow();

            WindowHandle = GLFW.GetWin32Window(GLFWWindow.WindowPtr);

            return new PlatformHandle(WindowHandle, "HWND");
        }

        public virtual void Present() { }
    }
}