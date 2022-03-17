using System;
using Avalonia.Utilities;
using Silk.NET.Vulkan;

namespace Avalonia.Vulkan.Skia
{
    public class VulkanBitmapAttachment
    {
        private readonly VulkanPlatformInterface _platformInterface;
        private readonly DisposableLock _lock = new DisposableLock();
        private bool _disposed;

        public VulkanImage Image { get; set; }

        private int _referenceCount;

        public VulkanBitmapAttachment(VulkanPlatformInterface platformInterface, uint format, PixelSize size)
        {
            _platformInterface = platformInterface;

            Image = new VulkanImage(platformInterface.Device, platformInterface.PhysicalDevice, platformInterface.Device.CommandBufferPool, format, size, 1);

            _referenceCount = 1;
        }

        public void Dispose()
        {
            _referenceCount--;

            if (_referenceCount == 0)
            {
                Image.Dispose();
                Image = null;
                _disposed = true;
            }
        }

        public void Present()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(VulkanBitmapAttachment));
            Image.TransitionLayout(ImageLayout.TransferSrcOptimal, 0);
            _referenceCount++;
        }

        public IDisposable Lock() => _lock.Lock();
    }

}
