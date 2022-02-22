using Ryujinx.Graphics.GAL;
using Silk.NET.Vulkan;
using System;
using System.Linq;
using VkFormat = Silk.NET.Vulkan.Format;

namespace Ryujinx.Graphics.Vulkan
{
    class Window : IWindow, IDisposable
    {
        private const int SurfaceWidth = 1280;
        private const int SurfaceHeight = 720;
        private const int ExternalImageCount = 2;

        private readonly VulkanGraphicsDevice _gd;
        private readonly SurfaceKHR? _surface;
        private readonly PhysicalDevice _physicalDevice;
        private readonly Device _device;
        private SwapchainKHR? _swapchain;

        private Image[] _swapchainImages;
        private Auto<DisposableImageView>[] _swapchainImageViews;

        private Semaphore _imageAvailableSemaphore;
        private Semaphore _renderFinishedSemaphore;
        private Semaphore[] _externalImageRenderedSemaphores = new Semaphore[ExternalImageCount];
        private Semaphore[] _externalImageAvailableSemaphores = new Semaphore[ExternalImageCount];

        private Fence _renderFinishedFence;

        private int _width;
        private int _height;
        private bool _isSizeChanged;
        private bool _isSemaphoreExported;
        private VkFormat _format;
        private DeviceMemory _memory;
        private ExternalImage[] _externalImages = new ExternalImage[ExternalImageCount];
        private uint _nextImage;

        public event EventHandler<ExternalMemoryObjectCreatedEvent> ExternalImageCreated;
        public event EventHandler<int> ExternalImageDestroyed;

        internal bool ScreenCaptureRequested { get; set; }

        public unsafe Window(VulkanGraphicsDevice gd, SurfaceKHR? surface, PhysicalDevice physicalDevice, Device device)
        {
            _gd = gd;
            _physicalDevice = physicalDevice;
            _device = device;
            _surface = surface;

            CreateSwapchain();

            var semaphoreCreateInfo = new SemaphoreCreateInfo()
            {
                SType = StructureType.SemaphoreCreateInfo
            };
            
            ExportSemaphoreCreateInfo exportSemaphoreCreateInfo;

            gd.Api.CreateSemaphore(device, semaphoreCreateInfo, null, out _imageAvailableSemaphore).ThrowOnError();
            gd.Api.CreateSemaphore(device, semaphoreCreateInfo, null, out _renderFinishedSemaphore).ThrowOnError();

            if (_gd.IsHeadless)
            {
                exportSemaphoreCreateInfo = new ExportSemaphoreCreateInfo()
                {
                    SType = StructureType.ExportSemaphoreCreateInfo,
                    HandleTypes = OperatingSystem.IsWindows() ? ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueWin32Bit 
                        : ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueFDBit
                };

                semaphoreCreateInfo.PNext = &exportSemaphoreCreateInfo;

                for(int i = 0; i < ExternalImageCount; i++)
                {
                    Semaphore semaphore;
                    gd.Api.CreateSemaphore(device, semaphoreCreateInfo, null, out semaphore).ThrowOnError();
                    _externalImageAvailableSemaphores[i] = semaphore;
                    gd.Api.CreateSemaphore(device, semaphoreCreateInfo, null, out semaphore).ThrowOnError();
                    _externalImageRenderedSemaphores[i] = semaphore;
                }
            }
        }

        private void RecreateSwapchain()
        {
            if (!_gd.IsHeadless)
            {
                for (int i = 0; i < _swapchainImageViews.Length; i++)
                {
                    _swapchainImageViews[i].Dispose();
                }

                CreateSwapchain();
            }
        }

        private unsafe void CreateSwapchain()
        {
            if(!_surface.HasValue)
            {
                return;
            }

            _gd.SurfaceApi.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface.Value, out var capabilities);

            uint surfaceFormatsCount;

            _gd.SurfaceApi.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface.Value, &surfaceFormatsCount, null);

            var surfaceFormats = new SurfaceFormatKHR[surfaceFormatsCount];

            fixed (SurfaceFormatKHR* pSurfaceFormats = surfaceFormats)
            {
                _gd.SurfaceApi.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface.Value, &surfaceFormatsCount, pSurfaceFormats);
            }

            uint presentModesCount;

            _gd.SurfaceApi.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface.Value, &presentModesCount, null);

            var presentModes = new PresentModeKHR[presentModesCount];

            fixed (PresentModeKHR* pPresentModes = presentModes)
            {
                _gd.SurfaceApi.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface.Value, &presentModesCount, pPresentModes);
            }

            uint imageCount = capabilities.MinImageCount + 1;
            if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
            {
                imageCount = capabilities.MaxImageCount;
            }

            var surfaceFormat = ChooseSwapSurfaceFormat(surfaceFormats);

            var extent = ChooseSwapExtent(capabilities);

            _width = (int)extent.Width;
            _height = (int)extent.Height;
            _format = surfaceFormat.Format;

            var oldSwapchain = _swapchain.HasValue ? _swapchain.Value : default(SwapchainKHR);

            var swapchainCreateInfo = new SwapchainCreateInfoKHR()
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = _surface.Value,
                MinImageCount = imageCount,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                ImageExtent = extent,
                ImageUsage = ImageUsageFlags.ImageUsageColorAttachmentBit | ImageUsageFlags.ImageUsageTransferDstBit,
                ImageSharingMode = SharingMode.Exclusive,
                ImageArrayLayers = 1,
                PreTransform = capabilities.CurrentTransform,
                CompositeAlpha = CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr,
                PresentMode = ChooseSwapPresentMode(presentModes),
                Clipped = true,
                OldSwapchain = oldSwapchain
            };

            _gd.SwapchainApi.CreateSwapchain(_device, swapchainCreateInfo, null, out var swapchain).ThrowOnError();

            _gd.SwapchainApi.GetSwapchainImages(_device, swapchain, &imageCount, null);

            _swapchainImages = new Image[imageCount];

            fixed (Image* pSwapchainImages = _swapchainImages)
            {
                _gd.SwapchainApi.GetSwapchainImages(_device, swapchain, &imageCount, pSwapchainImages);
            }

            _swapchainImageViews = new Auto<DisposableImageView>[imageCount];

            for (int i = 0; i < imageCount; i++)
            {
                _swapchainImageViews[i] = CreateImageView(_swapchainImages[i], surfaceFormat.Format);
            }

            _swapchain = swapchain;
        }

        private unsafe void CreateExternalImages()
        {
            for (int i = 0; i < ExternalImageCount; i++)
            {
                var image = _externalImages[i];

                if (image != null)
                {
                    image.Dispose();
                    ExternalImageDestroyed?.Invoke(this, image.ExternalTextureHandle);
                }

                _isSizeChanged = false;

                nint readySemaphoreHandle = IntPtr.Zero, completeSemaphoreHandle = IntPtr.Zero;

                image =
                    new ExternalImage(_gd, _device, _physicalDevice,
                        new Extent2D((uint?)_width, (uint?)_height));

                if (!_isSemaphoreExported)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        _gd.ExternalSemaphoreWin32.GetSemaphoreWin32Handle(_device,
                            new SemaphoreGetWin32HandleInfoKHR()
                            {
                                SType = StructureType.SemaphoreGetWin32HandleInfoKhr,
                                HandleType =
                                    ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueWin32Bit,
                                Semaphore = _externalImageAvailableSemaphores[i]
                            }, out readySemaphoreHandle);

                        _gd.ExternalSemaphoreWin32.GetSemaphoreWin32Handle(_device,
                            new SemaphoreGetWin32HandleInfoKHR()
                            {
                                SType = StructureType.SemaphoreGetWin32HandleInfoKhr,
                                HandleType =
                                    ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueWin32Bit,
                                Semaphore = _externalImageRenderedSemaphores[i]
                            }, out completeSemaphoreHandle);
                    }
                    else if (OperatingSystem.IsLinux())
                    {
                        int handle;

                        _gd.ExternalSemaphoreFd.GetSemaphoreF(_device,
                            new SemaphoreGetFdInfoKHR()
                            {
                                SType = StructureType.SemaphoreGetFDInfoKhr,
                                HandleType =
                                    ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueFDBit,
                                Semaphore = _externalImageAvailableSemaphores[i]
                            }, out handle);

                        readySemaphoreHandle = handle;

                        _gd.ExternalSemaphoreFd.GetSemaphoreF(_device,
                            new SemaphoreGetFdInfoKHR()
                            {
                                SType = StructureType.SemaphoreGetFDInfoKhr,
                                HandleType =
                                    ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueFDBit,
                                Semaphore = _externalImageRenderedSemaphores[i]
                            }, out handle);

                        completeSemaphoreHandle = handle;
                    }
                }

                ExternalMemoryObjectCreatedEvent e = new ExternalMemoryObjectCreatedEvent(image.Image.Handle,
                    image.MemorySize, image.ExternalImageMemoryHandle, readySemaphoreHandle,
                    completeSemaphoreHandle, Wait, i);

                ExternalImageCreated?.Invoke(this, e);

                image.ExternalTextureHandle = e.TextureHandle;

                image.ImageView = CreateImageView(image.Image, VkFormat.R8G8B8A8Unorm);

                _externalImages[i] = image;
            }

            _isSemaphoreExported = true;
        }

        private void Wait()
        {
            _gd.Api.WaitForFences(_device, new[] { _renderFinishedFence }, true, ulong.MaxValue);
        }

        private unsafe Auto<DisposableImageView> CreateImageView(Image image, VkFormat format)
        {
            var componentMapping = new ComponentMapping(
                ComponentSwizzle.R,
                ComponentSwizzle.G,
                ComponentSwizzle.B,
                ComponentSwizzle.A);

            var aspectFlags = ImageAspectFlags.ImageAspectColorBit;

            var subresourceRange = new ImageSubresourceRange(aspectFlags, 0, 1, 0, 1);

            var imageCreateInfo = new ImageViewCreateInfo()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = ImageViewType.ImageViewType2D,
                Format = format,
                Components = componentMapping,
                SubresourceRange = subresourceRange
            };

            _gd.Api.CreateImageView(_device, imageCreateInfo, null, out var imageView).ThrowOnError();
            return new Auto<DisposableImageView>(new DisposableImageView(_gd.Api, _device, imageView));
        }

        private static SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
        {
            if (availableFormats.Length == 1 && availableFormats[0].Format == VkFormat.Undefined)
            {
                return new SurfaceFormatKHR(VkFormat.B8G8R8A8Unorm, ColorSpaceKHR.ColorspaceSrgbNonlinearKhr);
            }

            foreach (var format in availableFormats)
            {
                if (format.Format == VkFormat.B8G8R8A8Unorm && format.ColorSpace == ColorSpaceKHR.ColorspaceSrgbNonlinearKhr)
                {
                    return format;
                }
            }

            return availableFormats[0];
        }

        private static PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes)
        {
            if (availablePresentModes.Contains(PresentModeKHR.PresentModeImmediateKhr))
            {
                return PresentModeKHR.PresentModeImmediateKhr;
            }
            else if (availablePresentModes.Contains(PresentModeKHR.PresentModeMailboxKhr))
            {
                return PresentModeKHR.PresentModeMailboxKhr;
            }
            else
            {
                return PresentModeKHR.PresentModeFifoKhr;
            }
        }

        public static Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.CurrentExtent.Width != uint.MaxValue)
            {
                return capabilities.CurrentExtent;
            }
            else
            {
                uint width = Math.Max(capabilities.MinImageExtent.Width, Math.Min(capabilities.MaxImageExtent.Width, SurfaceWidth));
                uint height = Math.Max(capabilities.MinImageExtent.Height, Math.Min(capabilities.MaxImageExtent.Height, SurfaceHeight));

                return new Extent2D(width, height);
            }
        }

        public unsafe void Present(ITexture texture, ImageCrop crop, Func<int, bool> swapBuffersCallback)
        {

            if (_gd.IsHeadless)
            {
                if (_isSizeChanged)
                {
                    _gd.Api.DeviceWaitIdle(_device);
                    CreateExternalImages();
                }
            }
            else
            {
                _nextImage = 0;
                while (true)
                {
                    var acquireResult = _gd.SwapchainApi.AcquireNextImage(
                        _device,
                        _swapchain.Value,
                        ulong.MaxValue,
                        _imageAvailableSemaphore,
                        new Fence(),
                        ref _nextImage);

                    if (acquireResult == Result.ErrorOutOfDateKhr ||
                        acquireResult == Result.SuboptimalKhr)
                    {
                        RecreateSwapchain();
                    }
                    else
                    {
                        acquireResult.ThrowOnError();
                        break;
                    }
                }
            }

            var currentImage = _gd.IsHeadless ? _externalImages[_nextImage].Image : _swapchainImages[_nextImage];

            _gd.FlushAllCommands();

            var cbs = _gd.CommandBufferPool.Rent();

            Transition(
                cbs.CommandBuffer,
                currentImage,
                0,
                AccessFlags.AccessTransferWriteBit,
                ImageLayout.Undefined,
                ImageLayout.General);

            var view = (TextureView)texture;

            int srcX0, srcX1, srcY0, srcY1;
            float scale = view.ScaleFactor;

            if (crop.Left == 0 && crop.Right == 0)
            {
                srcX0 = 0;
                srcX1 = (int)(view.Width / scale);
            }
            else
            {
                srcX0 = crop.Left;
                srcX1 = crop.Right;
            }

            if (crop.Top == 0 && crop.Bottom == 0)
            {
                srcY0 = 0;
                srcY1 = (int)(view.Height / scale);
            }
            else
            {
                srcY0 = crop.Top;
                srcY1 = crop.Bottom;
            }

            if (scale != 1f)
            {
                srcX0 = (int)(srcX0 * scale);
                srcY0 = (int)(srcY0 * scale);
                srcX1 = (int)Math.Ceiling(srcX1 * scale);
                srcY1 = (int)Math.Ceiling(srcY1 * scale);
            }

            if (ScreenCaptureRequested)
            {
                CaptureFrame(view, srcX0, srcY0, srcX1 - srcX0, srcY1 - srcY0, view.Info.Format.IsBgr(), crop.FlipX, crop.FlipY);

                ScreenCaptureRequested = false;
            }

            float ratioX = crop.IsStretched ? 1.0f : MathF.Min(1.0f, _height * crop.AspectRatioX / (_width * crop.AspectRatioY));
            float ratioY = crop.IsStretched ? 1.0f : MathF.Min(1.0f, _width * crop.AspectRatioY / (_height * crop.AspectRatioX));

            int dstWidth  = (int)(_width  * ratioX);
            int dstHeight = (int)(_height * ratioY);

            int dstPaddingX = (_width  - dstWidth)  / 2;
            int dstPaddingY = (_height - dstHeight) / 2;

            int dstX0 = crop.FlipX ? _width - dstPaddingX : dstPaddingX;
            int dstX1 = crop.FlipX ? dstPaddingX : _width - dstPaddingX;

            int dstY0 = crop.FlipY ? dstPaddingY : _height - dstPaddingY;
            int dstY1 = crop.FlipY ? _height - dstPaddingY : dstPaddingY;

            _gd.HelperShader.Blit(
                _gd,
                cbs,
                view,
                _gd.IsHeadless ? _externalImages[_nextImage].ImageView : _swapchainImageViews[_nextImage],
                _width,
                _height,
                _gd.IsHeadless ? VkFormat.R8G8B8A8Unorm : _format,
                new Extents2D(srcX0, srcY0, srcX1, srcY1),
                new Extents2D(dstX0, dstY1, dstX1, dstY0),
                true,
                true);

            Transition(
                cbs.CommandBuffer,
                currentImage,
                0,
                0,
                ImageLayout.General,
                _gd.IsHeadless ? ImageLayout.ColorAttachmentOptimal : ImageLayout.PresentSrcKhr);

            _gd.CommandBufferPool.Return(
                cbs,
                new[] { _gd.IsHeadless ? _externalImageAvailableSemaphores[_nextImage] : _imageAvailableSemaphore },
                new[] { PipelineStageFlags.PipelineStageColorAttachmentOutputBit },
                new[] { _gd.IsHeadless ? _externalImageRenderedSemaphores[_nextImage] : _renderFinishedSemaphore });

            // TODO: Present queue.
            var semaphore = _renderFinishedSemaphore;

            if (!_gd.IsHeadless)
            {
                var swapchain = _swapchain.Value;
                Result result;
                
                var nextImage = _nextImage;
                
                var presentInfo = new PresentInfoKHR()
                {
                    SType = StructureType.PresentInfoKhr,
                    WaitSemaphoreCount = 1,
                    PWaitSemaphores = &semaphore,
                    SwapchainCount = 1,
                    PSwapchains = &swapchain,
                    PImageIndices = &nextImage,
                    PResults = &result
                };

                lock (_gd.QueueLock)
                {
                    _gd.SwapchainApi.QueuePresent(_gd.Queue, presentInfo);
                }
            }
            else
            {
                _renderFinishedFence = _gd.CommandBufferPool.GetFence(cbs.CommandBufferIndex).GetUnsafe();

                var previous = _nextImage;
                
                _nextImage = swapBuffersCallback((int)_nextImage) ? ++_nextImage % ExternalImageCount : _nextImage;

                if(previous == _nextImage)
                {

                }
            }
        }

        private unsafe void Transition(
            CommandBuffer commandBuffer,
            Image image,
            AccessFlags srcAccess,
            AccessFlags dstAccess,
            ImageLayout srcLayout,
            ImageLayout dstLayout)
        {
            var subresourceRange = new ImageSubresourceRange(ImageAspectFlags.ImageAspectColorBit, 0, 1, 0, 1);

            var barrier = new ImageMemoryBarrier()
            {
                SType = StructureType.ImageMemoryBarrier,
                SrcAccessMask = srcAccess,
                DstAccessMask = dstAccess,
                OldLayout = srcLayout,
                NewLayout = dstLayout,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = image,
                SubresourceRange = subresourceRange
            };

            _gd.Api.CmdPipelineBarrier(
                commandBuffer,
                PipelineStageFlags.PipelineStageTopOfPipeBit,
                PipelineStageFlags.PipelineStageAllCommandsBit,
                0,
                0,
                null,
                0,
                null,
                1,
                barrier);
        }

        private void CaptureFrame(TextureView texture, int x, int y, int width, int height, bool isBgra, bool flipX, bool flipY)
        {
            byte[] bitmap = texture.GetData(x, y, width, height);

            _gd.OnScreenCaptured(new ScreenCaptureImageInfo(width, height, isBgra, bitmap, flipX, flipY));
        }

        public void SetSize(int width, int height)
        {
            if (_gd.IsHeadless)
            {
                _width = width;
                _height = height;

                _isSizeChanged = true;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                unsafe
                {
                    _gd.Api.DestroySemaphore(_device, _renderFinishedSemaphore, null);
                    _gd.Api.DestroySemaphore(_device, _imageAvailableSemaphore, null);

                    if (!_gd.IsHeadless)
                    {
                        for (int i = 0; i < _swapchainImageViews.Length; i++)
                        {
                            _swapchainImageViews[i].Dispose();
                        }

                        _gd.SwapchainApi.DestroySwapchain(_device, _swapchain.Value, null);
                    }
                    else
                    {
                        for(int i = 0; i < ExternalImageCount; i++)
                        {
                            _gd.Api.DestroySemaphore(_device, _externalImageAvailableSemaphores[i], null);
                            _gd.Api.DestroySemaphore(_device, _externalImageRenderedSemaphores[i], null);
                        }
                    }

                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
