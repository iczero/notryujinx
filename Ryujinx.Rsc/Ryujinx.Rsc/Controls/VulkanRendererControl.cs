using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Ryujinx.Rsc.Backend.Vulkan;
using Ryujinx.Ava.Vulkan;
using Ryujinx.Common.Configuration;
using Ryujinx.Graphics.Vulkan;
using Silk.NET.Vulkan;
using SkiaSharp;
using SPB.Graphics;
using SPB.Platform;
using SPB.Windowing;
using System;
using System.Threading;
using System.Threading.Tasks;


namespace Ryujinx.Rsc.Controls
{
    public class VulkanRendererControl : RendererControl
    {
        public event EventHandler<EventArgs> VulkanInitialized;
        public event EventHandler Rendered;

        private SwappableNativeWindowBase _gameBackgroundWindow;

        private int _drawId;
        private IntPtr _fence;

        private VulkanDrawOperation _vkDrawOperation;
        private VulkanPlatformInterface _platformInterface;
        private bool _isInitialized;

        public VulkanRendererControl(GraphicsDebugLevel graphicsDebugLevel)
        {
            DebugLevel = graphicsDebugLevel;

            Focusable = true;

            _platformInterface = AvaloniaLocator.Current.GetService<VulkanPlatformInterface>();
        }

        public override void Render(DrawingContext context)
        {
            if (!_isInitialized)
            {
                OnInitialized();
                _isInitialized = true;
            }
            
            if (_vkDrawOperation != null)
            {
                context.Custom(_vkDrawOperation);
            }

            base.Render(context);
        }

        protected override void Resized(Rect rect)
        {
            base.Resized(rect);

            _vkDrawOperation = new VulkanDrawOperation(this);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
        }

        internal override bool Present(object image)
        {
            var result = base.Present(image);

            QueueRender();

            return true;
        }
        public void DestroyBackgroundContext()
        {
            Image = null;

            if (_fence != IntPtr.Zero)
            {
                _vkDrawOperation.Dispose();
            }

            _gameBackgroundWindow?.Dispose();
        }

        private class VulkanDrawOperation : ICustomDrawOperation
        {
            private int _framebuffer;

            public Rect Bounds { get; }

            private readonly VulkanRendererControl _control;

            public VulkanDrawOperation(VulkanRendererControl control)
            {
                _control = control;
                Bounds = _control.Bounds;
            }

            public void Dispose()
            {

            }

            public bool Equals(ICustomDrawOperation other)
            {
                return other is VulkanDrawOperation operation && Equals(this, operation) && operation.Bounds == Bounds;
            }

            public bool HitTest(Point p)
            {
                return Bounds.Contains(p);
            }

            public void Render(IDrawingContextImpl context)
            {
                if (_control.Image == null)
                    return;

                var image = (PresentImageInfo)_control.Image;
                var fence = _control._fence;

                if (context is not ISkiaDrawingContextImpl skiaDrawingContextImpl)
                    return;

                _control._platformInterface.Device.QueueWaitIdle();

                var gpu = AvaloniaLocator.Current.GetService<VulkanSkiaGpu>();

                var imageInfo = new GRVkImageInfo()
                {
                    CurrentQueueFamily = _control._platformInterface.PhysicalDevice.QueueFamilyIndex,
                    Format = (uint)Format.R8G8B8A8Unorm,
                    Image = image.Image.Handle,
                    ImageLayout = (uint)ImageLayout.ColorAttachmentOptimal,
                    ImageTiling = (uint)ImageTiling.Optimal,
                    ImageUsageFlags = (uint)(ImageUsageFlags.ImageUsageColorAttachmentBit 
                                        | ImageUsageFlags.ImageUsageTransferSrcBit | ImageUsageFlags.ImageUsageTransferDstBit),
                    LevelCount = 1,
                    SampleCount = 1,
                    Protected = false,
                    Alloc = new GRVkAlloc()
                    {
                        Memory = image.Memory.Handle,
                        Flags = 0,
                        Offset = image.MemoryOffset,
                        Size = image.MemorySize
                    }
                };

                using (var backendTexture = new GRBackendRenderTarget((int)_control.RenderSize.Width, (int)_control.RenderSize.Height, 1,
                        imageInfo))
                using (var surface = SKSurface.Create(gpu.GrContext, backendTexture,
                    GRSurfaceOrigin.TopLeft,
                    SKColorType.Rgba8888, SKColorSpace.CreateSrgb()))
                {
                    // Again, silently ignore, if something went wrong it's not our fault
                    if (surface == null)
                        return;

                    var rect = new Rect(new Point(), _control.RenderSize);

                    using (var snapshot = surface.Snapshot())
                        skiaDrawingContextImpl.SkCanvas.DrawImage(snapshot, rect.ToSKRect(), _control.Bounds.ToSKRect(), new SKPaint());
                }
            }
        }
    }
}
