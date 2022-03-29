using Avalonia;
using Avalonia.Platform;
using OpenTK.Graphics.OpenGL;
using PInvoke;
using Ryujinx.Ava.Ui.Controls;
using SPB.Graphics;
using SPB.Graphics.OpenGL;
using SPB.Platform;
using SPB.Windowing;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Ava.Ui.Backend.OpenGl
{
    public class OpenGlSurface : IDisposable
    {
        public OpenGLContextBase Context{ get; }
        public IPlatformHandle Handle { get; }
        public SwappableNativeWindowBase Window{ get; }

        private IntPtr _display = IntPtr.Zero;

        public bool IsDisposed { get; private set; }

        public int Framebuffer { get; set; }
        private int _texture;

        private PixelSize _oldSize;

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

            var primaryContext = AvaloniaLocator.Current.GetService<OpenGLContextBase>();
            
            Context = primaryContext != null ? PlatformHelper.CreateOpenGLContext(FramebufferFormat.Default, 3, 2, OpenGLContextFlags.Compat, shareContext:primaryContext)
                : PlatformHelper.CreateOpenGLContext(FramebufferFormat.Default, 3, 2, OpenGLContextFlags.Compat);
            Context.Initialize(Window);
            MakeCurrent();
            GL.LoadBindings(new OpenToolkitBindingsContext(Context.GetProcAddress));
            UnsetCurrent();

            if(primaryContext == null)
            {
                AvaloniaLocator.CurrentMutable.Bind<OpenGLContextBase>().ToConstant(Context);
            }
        }

        public void CreateFramebuffer(PixelSize size)
        {
            if (Context.IsCurrent)
            {
                Framebuffer = GL.GenFramebuffer();
                _texture = GL.GenTexture();

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, Framebuffer);

                GL.BindTexture(TextureTarget.Texture2D, _texture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, size.Width, size.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _texture, 0);

                GL.BindTexture(TextureTarget.Texture2D, 0);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            }
        }

        private void DestroyFramebuffer()
        {
            if(Framebuffer != 0)
            {
                GL.DeleteTexture(_texture);
                GL.DeleteFramebuffer(Framebuffer);
                Framebuffer = 0;
            }
        }

        public PixelSize Size
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    GetClientRect(Handle.Handle, out var rect);

                    var size = new PixelSize(rect.right, rect.bottom);

                    if(size != _oldSize)
                    {
                        DestroyFramebuffer();
                        CreateFramebuffer(size);
                    }

                    _oldSize = size;

                    return size;
                }

                return new PixelSize();
            }
        }

        public PixelSize CurrentSize => _oldSize;

        public OpenGlSurfaceRenderingSession BeginDraw()
        {
            return new OpenGlSurfaceRenderingSession(this, (float)Program.WindowScaleFactor);
        }

        public void MakeCurrent()
        {
            Monitor.Enter(this);
            Context.MakeCurrent(Window);
        }

        public void UnsetCurrent()
        {
            Context.MakeCurrent(null);
            Monitor.Exit(this);
        }

        public void SwapBuffers()
        {
            Window.SwapBuffers();
        }

        #region Native Methods

        [DllImport("libX11.so.6")]
        public static extern IntPtr XOpenDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        public static extern int XCloseDisplay(IntPtr display);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hwnd, out RECT lpRect);

        #endregion

        public void Dispose()
        {
            Context?.Dispose();
            IsDisposed = true;
            if (_display != IntPtr.Zero)
            {
                XCloseDisplay(_display);
            }
        }
    }
}