using Avalonia.Platform;
using OpenTK.Graphics.OpenGL;
using Ryujinx.Ava.Ui.Controls;
using SPB.Graphics;
using SPB.Graphics.OpenGL;
using SPB.Platform;
using SPB.Windowing;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Ava.Ui.Backend.OpenGl
{
    public class OpenGlSurface : IDisposable
    {
        public OpenGLContextBase Context{ get; }
        public IPlatformHandle Handle { get; }
        public SwappableNativeWindowBase Window{ get; }

        private IntPtr _display = IntPtr.Zero;

        public OpenGlSurface(IPlatformHandle handle)
        {
            if (OperatingSystem.IsWindows())
            {
                Window = new SPB.Platform.WGL.WGLWindow(new NativeHandle(handle.Handle));
            }
            else if (OperatingSystem.IsLinux())
            {
                _display = XOpenDisplay(IntPtr.Zero);
                Window = new SPB.Platform.GLX.GLXWindow(new NativeHandle(_display), new NativeHandle(handle.Handle));
            }
            Handle = handle;
            
            Context = PlatformHelper.CreateOpenGLContext(FramebufferFormat.Default, 3, 0, OpenGLContextFlags.Compat);
            Context.Initialize(Window);
            MakeCurrent();
            GL.LoadBindings(new OpenToolkitBindingsContext(Context.GetProcAddress));
            UnsetCurrent();
        }

        public OpenGlSurfaceRenderingSession BeginDraw()
        {
            return new OpenGlSurfaceRenderingSession(this, 1);
        }

        public void MakeCurrent()
        {
            Context.MakeCurrent(Window);
        }

        public void UnsetCurrent()
        {
            Context.MakeCurrent(null);
        }

        public void SwapBuffers()
        {
            Window.SwapBuffers();
        }

        [DllImport("libX11.so.6")]
        public static extern IntPtr XOpenDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        public static extern int XCloseDisplay(IntPtr display);

        public void Dispose()
        {
            if (_display != IntPtr.Zero)
            {
                XCloseDisplay(_display);
            }
        }
    }
}