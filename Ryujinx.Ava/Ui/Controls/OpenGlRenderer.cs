using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.Platform;
using Avalonia.Win32;
using OpenTK.Graphics.OpenGL;
using Ryujinx.Common.Configuration;
using SPB.Graphics;
using SPB.Graphics.OpenGL;
using SPB.Platform;
using SPB.Platform.Win32;
using SPB.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Controls
{
    internal class OpenGlRenderer : RendererControl
    {
        private IntPtr _handle;
        private SwappableNativeWindowBase _window;

        public int Major { get; }
        public int Minor { get; }
        public GraphicsDebugLevel DebugLevel { get; }

        private IntPtr _gameFence = IntPtr.Zero;

        public OpenGlRenderer(int major, int minor, GraphicsDebugLevel graphicsDebugLevel)
        {
            Major = major;
            Minor = minor;
            DebugLevel = graphicsDebugLevel;
        }

        protected override void OnRender(GlInterface gl, int fb)
        {
            if (_gameFence != IntPtr.Zero)
            {
                GL.ClientWaitSync(_gameFence, ClientWaitSyncFlags.SyncFlushCommandsBit, Int64.MaxValue);
                GL.DeleteSync(_gameFence);
                _gameFence = IntPtr.Zero;
            }
            
            if(Image == 0)
            {
                return;
            }

            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.ClearColor(0,0, 0, 0);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, Framebuffer);
            GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, Image, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fb);
            GL.BlitFramebuffer(0,
                               0,
                               (int)RenderSize.Width,
                               (int)RenderSize.Height,
                               0,
                               (int)RenderSize.Height,
                               (int)RenderSize.Width,
                               0,
                               ClearBufferMask.ColorBufferBit,
                               BlitFramebufferFilter.Linear);

            GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, 0, 0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb);

            GuiFence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
        }

        protected override void OnOpenGlDeinit(GlInterface gl, int fb)
        {
            base.OnOpenGlDeinit(gl, fb);

            if (GuiFence != IntPtr.Zero)
            {
                GL.DeleteSync(GuiFence);
            }

            if (_gameFence != IntPtr.Zero)
            {
                GL.DeleteSync(_gameFence);
            }
        }

        internal override bool Present(int image)
        {
            if (GuiFence != IntPtr.Zero)
            {
                GL.ClientWaitSync(GuiFence, ClientWaitSyncFlags.SyncFlushCommandsBit, Int64.MaxValue);
                GL.DeleteSync(GuiFence);
                GuiFence = IntPtr.Zero;
            }

            bool returnValue = Presented;

            if (Presented)
            {
                lock (this)
                {
                    Image = image;
                    Presented = false;
                }
            }

            if (_gameFence != IntPtr.Zero)
            {
                GL.DeleteSync(_gameFence);
            }

            _gameFence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);

            QueueRender();

            return returnValue;
        }

        public async override Task DestroyBackgroundContext()
        {
            await Task.Delay(1000);
            GL.DeleteFramebuffer(Framebuffer);
            // WGL hangs here when disposing context
            //Context?.Dispose();
            _window?.Dispose();
        }

        internal override void MakeCurrent()
        {
           Context.MakeCurrent(_window);
        }

        internal override void MakeCurrent(SwappableNativeWindowBase window)
        {
            Context.MakeCurrent(window);
        }

        protected override void CreateGlContext(OpenGLContextBase mainContext)
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
