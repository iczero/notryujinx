using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Ryujinx.Ava.Ui.Backend.Vulkan;
using Ryujinx.Ava.Ui.Vulkan;
using Ryujinx.Common.Configuration;
using Ryujinx.Graphics.Vulkan;
using Silk.NET.Vulkan;
using SkiaSharp;
using SPB.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Controls
{
    internal class VulkanRendererControl : RendererControl
    {
        private VulkanPlatformInterface _platformInterface;

        public VulkanRendererControl(GraphicsDebugLevel graphicsDebugLevel) : base(graphicsDebugLevel)
        {
            _platformInterface = AvaloniaLocator.Current.GetService<VulkanPlatformInterface>();
        }

        public override void DestroyBackgroundContext()
        {

        }

        protected override ICustomDrawOperation CreateDrawOperation()
        {
            return new VulkanDrawOperation(this);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
        }

        protected override void CreateWindow()
        {
        }

        internal override void MakeCurrent()
        {
        }

        internal override void MakeCurrent(SwappableNativeWindowBase window)
        {
        }

        internal override void Present(object image)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Image = image;
            });

            QueueRender();
        }

        private class VulkanDrawOperation : ICustomDrawOperation
        {
            public Rect Bounds { get; }

            private readonly VulkanRendererControl _control;
            private VulkanImage _stagingImage;
            private bool _isDestroyed;

            public VulkanDrawOperation(VulkanRendererControl control)
            {
                _control = control;
                Bounds = _control.Bounds;
            }

            public void Dispose()
            {
                if (_isDestroyed)
                {
                    return;
                }

                _isDestroyed = true;
                _control._platformInterface.Device.QueueWaitIdle();
                _stagingImage?.Dispose();
            }

            public bool Equals(ICustomDrawOperation other)
            {
                return other is VulkanDrawOperation operation && Equals(this, operation) && operation.Bounds == Bounds;
            }

            public bool HitTest(Point p)
            {
                return Bounds.Contains(p);
            }

            public unsafe void Render(IDrawingContextImpl context)
            {
                if (_isDestroyed || _control.Image == null || _control.RenderSize.Width == 0 || _control.RenderSize.Height == 0 ||
                    context is not ISkiaDrawingContextImpl skiaDrawingContextImpl)
                {
                    return;
                }

                var image = (PresentImageInfo)_control.Image;

                lock (image.State)
                {
                    if (!image.State.IsValid)
                    {
                        return;
                    }

                    if (_stagingImage != null && _stagingImage.InternalHandle == null)
                    {
                        return;
                    }

                    if (_stagingImage == null)
                    {
                        _stagingImage = new VulkanImage(_control._platformInterface.Device,
                            _control._platformInterface.PhysicalDevice,
                            _control._platformInterface.Device.CommandBufferPool,
                            (uint)Format.R8G8B8A8Unorm,
                            new PixelSize((int)_control.Bounds.Size.Width, (int)_control.Bounds.Size.Height),
                            1);

                        _stagingImage.TransitionLayout(ImageLayout.TransferDstOptimal, AccessFlags.AccessTransferWriteBit | AccessFlags.AccessTransferWriteBit);
                    }

                    var commandBuffer = _control._platformInterface.Device.CommandBufferPool.CreateCommandBuffer();

                    commandBuffer.BeginRecording();

                    image.CopyImage(_control._platformInterface.Device.InternalHandle,
                        _control._platformInterface.PhysicalDevice.InternalHandle, commandBuffer.InternalHandle, new Extent2D((uint)_stagingImage.Size.Width, (uint)_stagingImage.Size.Height),
                        _stagingImage.InternalHandle.Value);

                    commandBuffer.Submit();

                    commandBuffer.WaitForFence();

                    if (!image.State.IsValid)
                    {
                        return;
                    }
                }

                _control._platformInterface.Device.QueueWaitIdle();

                var gpu = AvaloniaLocator.Current.GetService<VulkanSkiaGpu>();

                var imageInfo = new GRVkImageInfo()
                {
                    CurrentQueueFamily = _control._platformInterface.PhysicalDevice.QueueFamilyIndex,
                    Format = (uint)Format.R8G8B8A8Unorm,
                    Image = _stagingImage.Handle,
                    ImageLayout = (uint)ImageLayout.TransferDstOptimal,
                    ImageTiling = (uint)ImageTiling.Optimal,
                    ImageUsageFlags = (uint)(ImageUsageFlags.ImageUsageColorAttachmentBit
                                             | ImageUsageFlags.ImageUsageTransferSrcBit
                                             | ImageUsageFlags.ImageUsageTransferDstBit),
                    LevelCount = 1,
                    SampleCount = 1,
                    Protected = false,
                    Alloc = new GRVkAlloc()
                    {
                        Memory = _stagingImage.MemoryHandle,
                        Flags = 0,
                        Offset = 0,
                        Size = _stagingImage.MemorySize
                    }
                };

                using var backendTexture = new GRBackendRenderTarget(
                    _stagingImage.Size.Width,
                    _stagingImage.Size.Height,
                    1,
                    imageInfo);

                using var surface = SKSurface.Create(
                    gpu.GrContext,
                    backendTexture,
                    GRSurfaceOrigin.TopLeft,
                    SKColorType.Rgba8888);

                if (surface == null)
                {
                    return;
                }

                var rect = new Rect(new Point(), _stagingImage.Size.ToSize(1));

                using var snapshot = surface.Snapshot();
                skiaDrawingContextImpl.SkCanvas.DrawImage(snapshot, rect.ToSKRect(), _control.Bounds.ToSKRect(),
                    new SKPaint());
            }
        }
    }
}
