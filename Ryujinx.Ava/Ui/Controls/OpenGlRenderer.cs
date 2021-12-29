using Avalonia;
using Avalonia.OpenGL;
using OpenTK.Graphics.OpenGL;
using Ryujinx.Common.Configuration;
using SPB.Graphics;
using SPB.Graphics.OpenGL;
using SPB.Platform;
using SPB.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Controls
{
    internal class OpenGlRenderer : RendererBase
    {
        private IntPtr _handle;
        private SwappableNativeWindowBase _window;

        public int Major { get; }
        public int Minor { get; }
        public GraphicsDebugLevel DebugLevel { get; }
        public OpenGLContextBase Context { get; set; }

        public OpenGlRenderer(int major, int minor, GraphicsDebugLevel graphicsDebugLevel)
        {
            Major = major;
            Minor = minor;
            DebugLevel = graphicsDebugLevel;
        }

        protected override void OnOpenGlInit(GlInterface gl, int fb)
        {
            base.OnOpenGlInit(gl, fb);

            var glInterface = AvaloniaLocator.Current.GetService<IPlatformOpenGlInterface>();
            var context = glInterface.PrimaryContext;
            _handle = (IntPtr)context.GetType().GetProperty("Handle").GetValue(context);

            OpenGLContextBase mainContext = null;

            if (OperatingSystem.IsWindows())
            {
                mainContext = new AvaloniaWglContext(_handle);
            }
            else if(OperatingSystem.IsLinux())
            {
                mainContext = new AvaloniaGlxContext(_handle);
            }

            CreateWindow(mainContext); 

            OnInitialized(gl);
        }

        protected override void OnRender(GlInterface gl, int fb)
        {
            if(Image == 0)
            {
                return;
            }

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, Image);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fb);
            GL.BlitFramebuffer(0,
                               0,
                               (int)Bounds.Width,
                               (int)Bounds.Height,
                               0,
                               (int)Bounds.Height,
                               (int)Bounds.Width,
                               0,
                               ClearBufferMask.ColorBufferBit,
                               BlitFramebufferFilter.Linear);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb);
        }

        protected override void OnOpenGlDeinit(GlInterface gl, int fb)
        {
            base.OnOpenGlDeinit(gl, fb);
            Context.Dispose();
            _window.Dispose();
        }

        internal void MakeCurrent()
        {
           Context.MakeCurrent(_window);
        }
        internal void MakeCurrent(SwappableNativeWindowBase window)
        {
            Context.MakeCurrent(window);
        }

        private void CreateWindow(OpenGLContextBase mainContext)
        {
            var flags = OpenGLContextFlags.Compat;
            if(DebugLevel != GraphicsDebugLevel.None)
            {
                flags |= OpenGLContextFlags.Debug;
            }
            _window = PlatformHelper.CreateOpenGLWindow(FramebufferFormat.Default, 0, 0, (int)Bounds.Width, (int)Bounds.Height);
            _window.Hide();

            Context = PlatformHelper.CreateOpenGLContext(FramebufferFormat.Default, Major, Minor, flags, shareContext: mainContext);
            Context.Initialize(_window);
        }
    }
}
