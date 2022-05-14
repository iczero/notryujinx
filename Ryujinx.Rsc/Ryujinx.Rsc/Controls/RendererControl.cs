using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Ryujinx.Common.Configuration;
using Ryujinx.Rsc.Views;
using System;

namespace Ryujinx.Rsc.Controls
{
    public abstract class RendererControl : Control
    {
        public RendererControl()
        {
            IObservable<Rect> resizeObservable = this.GetObservable(BoundsProperty);

            resizeObservable.Subscribe(Resized);
        }

        public bool IsStarted { get; private set; }
        public GraphicsDebugLevel DebugLevel { get; protected set; }

        protected Size RenderSize { get; set; }
        protected object Image { get; set; }

        public event EventHandler<Size> SizeChanged;
        public event EventHandler<EventArgs> RendererInitialized;

        public void QueueRender()
        {
            try
            {
                InvalidateVisual();
            }
            catch (Exception ex) { }
        }

        internal virtual bool Present(object image)
        {
            Image = image;

            return true;
        }

        internal void Start()
        {
            IsStarted = true;
            QueueRender();
        }

        internal void Stop()
        {
            IsStarted = false;
        }

        protected virtual void Resized(Rect rect)
        {
            SizeChanged?.Invoke(this, rect.Size);

            RenderSize = rect.Size * MainView.Scaling;
        }

        protected void OnInitialized()
        {
            RendererInitialized?.Invoke(this, EventArgs.Empty);
        }

    }
}