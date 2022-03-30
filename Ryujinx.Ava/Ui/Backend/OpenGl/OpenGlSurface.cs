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
using Ryujinx.Ava.Ui.Backend;

namespace Ryujinx.Ava.Ui.Backend.OpenGl
{

    public class OpenGlSurface : BackendSurface
    {
        public OpenGLContextBase Context { get; }
        public SwappableNativeWindowBase Window { get; }

        public int Framebuffer { get; set; }
        private int _texture;

        public OpenGlSurface(IntPtr handle) : base(handle)
        {
            if (OperatingSystem.IsWindows())
            {
                Window = new SPB.Platform.WGL.WGLWindow(new NativeHandle(Handle));
            }
            else if (OperatingSystem.IsLinux())
            {
                Window = new SPB.Platform.GLX.GLXWindow(new NativeHandle(Display), new NativeHandle(Handle));
            }
            var primaryContext = AvaloniaLocator.Current.GetService<OpenGLContextBase>();

            Context = primaryContext != null ? PlatformHelper.CreateOpenGLContext(GetFramebufferFormat(), 3, 2, OpenGLContextFlags.Compat, shareContext: primaryContext)
                : PlatformHelper.CreateOpenGLContext(FramebufferFormat.Default, 3, 2, OpenGLContextFlags.Compat);
            Context.Initialize(Window);
            MakeCurrent();
            GL.LoadBindings(new OpenToolkitBindingsContext(Context.GetProcAddress));
            UnsetCurrent();

            if (primaryContext == null)
            {
                AvaloniaLocator.CurrentMutable.Bind<OpenGLContextBase>().ToConstant(Context);
            }
        }

        private FramebufferFormat GetFramebufferFormat()
        {
            return Environment.OSVersion.Platform == PlatformID.Unix ? new FramebufferFormat(new ColorFormat(8, 8, 8, 0), 16, 0, ColorFormat.Zero, 0, 2, false) : FramebufferFormat.Default;
        }

        public OpenGlSurfaceRenderingSession BeginDraw()
        {
            return new OpenGlSurfaceRenderingSession(this, (float)Program.WindowScaleFactor);
        }

        protected override void CreateFramebuffer(PixelSize size)
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

        public void MakeCurrent()
        {
            Monitor.Enter(this);
            Context.MakeCurrent(Window);
        }

        public void SwapBuffers()
        {
            Window.SwapBuffers();
        }

        public void UnsetCurrent()
        {
            Context.MakeCurrent(null);
            Monitor.Exit(this);
        }

        public override void Dispose()
        {
            Context?.Dispose();
            base.Dispose();
        }

        protected override void DestroyFramebuffer()
        {
            if (Framebuffer != 0)
            {
                GL.DeleteTexture(_texture);
                GL.DeleteFramebuffer(Framebuffer);
                Framebuffer = 0;
            }
        }
    }
}