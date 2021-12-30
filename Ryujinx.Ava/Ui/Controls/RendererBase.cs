using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using OpenTK.Graphics.OpenGL;
using SPB.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Controls
{
    public abstract class RendererBase : OpenGlControlBase
    {
        protected int Image { get; private set; }
        public SwappableNativeWindowBase Window { get; private set; }

        public event EventHandler<EventArgs> GlInitialized;
        public event EventHandler<Size> SizeChanged;

        private IntPtr _waitFence;

        public RendererBase()
        {
            IObservable<Rect> resizeObservable = this.GetObservable(BoundsProperty);

            resizeObservable.Subscribe(Resized);
        }

        private void Resized(Rect rect)
        {
            SizeChanged?.Invoke(this, rect.Size);
        }

        protected override void OnOpenGlInit(GlInterface gl, int fb)
        {
            base.OnOpenGlInit(gl, fb);

            if (OperatingSystem.IsWindows())
            {
                var window = ((this.VisualRoot as TopLevel).PlatformImpl as Avalonia.Win32.WindowImpl).Handle.Handle;

                Window = new SPB.Platform.WGL.WGLWindow(new NativeHandle(window));
            }
            else if (OperatingSystem.IsLinux())
            {
                var window = (IPlatformHandle)(this.VisualRoot as TopLevel).PlatformImpl.GetType().GetProperty("Handle").GetValue((this.VisualRoot as TopLevel).PlatformImpl);
                var display = (this.VisualRoot as TopLevel).PlatformImpl.GetType().GetField("_x11", System.Reflection.BindingFlags.NonPublic).GetValue((this.VisualRoot as TopLevel).PlatformImpl);
                var displayHandle = (IntPtr)display.GetType().GetProperty("Display").GetValue(display);

                Window = new SPB.Platform.GLX.GLXWindow(new NativeHandle(displayHandle), new NativeHandle(window.Handle));
            }
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            GL.ClientWaitSync(_waitFence, ClientWaitSyncFlags.SyncFlushCommandsBit, long.MaxValue);
            OnRender(gl, fb);
        }

        protected abstract void OnRender(GlInterface gl, int fb);

        protected override void OnOpenGlDeinit(GlInterface gl, int fb)
        {
            base.OnOpenGlDeinit(gl, fb);
            GL.DeleteSync(_waitFence);
        }

        protected void OnInitialized(GlInterface gl)
        {
            GL.LoadBindings(new OpenToolkitBindingsContext(gl.GetProcAddress));
            _waitFence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
            GlInitialized?.Invoke(this, EventArgs.Empty);
        }

        internal void Present(int image)
        {
            Image = image;
            GL.WaitSync(_waitFence, WaitSyncFlags.None, long.MaxValue);
            Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background).Wait();
        }
    }
}
