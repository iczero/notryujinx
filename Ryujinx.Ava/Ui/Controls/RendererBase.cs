using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Controls
{
    public abstract class RendererBase : OpenGlControlBase
    {
        protected int Image { get; private set; }

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
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
        }
    }
}
