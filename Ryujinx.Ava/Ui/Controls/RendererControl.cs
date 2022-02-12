using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using OpenTK.Graphics.OpenGL;
using SPB.Graphics.OpenGL;
using SPB.Windowing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Controls
{
    public abstract class RendererControl : OpenGlControlBase
    {
        
        internal IntPtr GuiFence { get; set; } = IntPtr.Zero;
        protected int Image { get; set; }
        public SwappableNativeWindowBase Window { get; private set; }

        public event EventHandler<EventArgs> GlInitialized;
        public event EventHandler<Size> SizeChanged;

        protected bool Presented { get; set; }

        protected Size RenderSize { get;private set; }
        
        protected int Framebuffer { get; set; }

        public static OpenGLContextBase PrimaryContext => 
                AvaloniaLocator.Current.GetService<IPlatformOpenGlInterface>().PrimaryContext.AsOpenGLContextBase();
        public OpenGLContextBase Context { get; set; }

        public RendererControl()
        {
            IObservable<Rect> resizeObservable = this.GetObservable(BoundsProperty);

            resizeObservable.Subscribe(Resized);

            Focusable = true;
        }

        private void Resized(Rect rect)
        {
            SizeChanged?.Invoke(this, rect.Size);

            RenderSize = rect.Size * Program.WindowScaleFactor;
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
                var platform = (this.VisualRoot as TopLevel).PlatformImpl;
                var window = (IPlatformHandle)platform.GetType().GetProperty("Handle").GetValue(platform);
                var display = platform.GetType().GetField("_x11", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(platform);
                var displayHandle = (IntPtr)display.GetType().GetProperty("Display").GetValue(display);

                Window = new SPB.Platform.GLX.GLXWindow(new NativeHandle(displayHandle), new NativeHandle(window.Handle));
            }

            OpenGLContextBase mainContext = PrimaryContext;

            CreateGlContext(mainContext);

            OnInitialized(gl);

            Framebuffer = GL.GenFramebuffer();
        }

        protected abstract void CreateGlContext(OpenGLContextBase mainContext);

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            lock (this)
            {
                OnRender(gl, fb);

                Presented = true;
            }
        }

        protected abstract void OnRender(GlInterface gl, int fb);

        protected void OnInitialized(GlInterface gl)
        {
            GL.LoadBindings(new OpenToolkitBindingsContext(gl.GetProcAddress));
            GlInitialized?.Invoke(this, EventArgs.Empty);
        }

        public void QueueRender()
        {
            Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Render);
        }

        internal abstract bool Present(int image);

        public abstract Task DestroyBackgroundContext();
        internal abstract void MakeCurrent();
        internal abstract void MakeCurrent(SwappableNativeWindowBase window);
    }
}
