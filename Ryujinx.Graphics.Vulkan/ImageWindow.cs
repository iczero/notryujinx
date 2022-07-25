using Ryujinx.Graphics.GAL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System;
using VkFormat = Silk.NET.Vulkan.Format;
using ResetEvent = System.Threading.ManualResetEventSlim;

namespace Ryujinx.Graphics.Vulkan
{
    class ImageWindow : WindowBase, IWindow, IDisposable
    {
        internal const VkFormat Format = VkFormat.R8G8B8A8Unorm;

        private const int ImageCount = 2;
        private const int SurfaceWidth = 1280;
        private const int SurfaceHeight = 720;

        private readonly VulkanRenderer _gd;
        private readonly PhysicalDevice _physicalDevice;
        private readonly Device _device;
        private readonly Instance _instance;

        private Auto<DisposableImage>[] _images;
        private Auto<DisposableImageView>[] _imageViews;
        private Auto<DisposableMemory>[] _imageMemory;
        private ImageState[] _states;
        private PresentImageInfo[] _presentedImages;
        private FenceHolder[] _fences;

        private ulong[] _imageSizes;
        private ulong[] _imageOffsets;

        private Semaphore[] _imageAvailableSemaphores;
        private Semaphore[] _renderFinishedSemaphores;
        private IntPtr[] _exportImageAvailableSemaphores;
        private IntPtr[] _exportRenderFinishedSemaphores;

        private int _width = SurfaceWidth;
        private int _height = SurfaceHeight;
        private bool _recreateImages;
        private int _nextImage;

        internal new bool ScreenCaptureRequested { get; set; }

        public unsafe ImageWindow(VulkanRenderer gd, Instance instance, PhysicalDevice physicalDevice, Device device)
        {
            _gd = gd;
            _physicalDevice = physicalDevice;
            _device = device;
            _instance = instance;

            _images = new Auto<DisposableImage>[ImageCount];
            _imageMemory = new Auto<DisposableMemory>[ImageCount];
            _imageSizes = new ulong[ImageCount];
            _imageOffsets = new ulong[ImageCount];
            _states = new ImageState[ImageCount];
            _presentedImages = new PresentImageInfo[ImageCount];
            _imageAvailableSemaphores = new Semaphore[ImageCount];
            _renderFinishedSemaphores = new Semaphore[ImageCount];
            _exportImageAvailableSemaphores = new IntPtr[ImageCount];
            _exportRenderFinishedSemaphores = new IntPtr[ImageCount];

            CreateImages();

            var exportSemaphoreCreateInfo = new ExportSemaphoreCreateInfo()
            {
                SType = StructureType.ExportSemaphoreCreateInfo,
                HandleTypes = OperatingSystem.IsWindows() ? ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueWin32Bit :
                    ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueFDBit
            };

            var semaphoreCreateInfo = new SemaphoreCreateInfo() { SType = StructureType.SemaphoreCreateInfo, PNext = &exportSemaphoreCreateInfo };

            for (int i = 0; i < ImageCount; i++)
            {
                gd.Api.CreateSemaphore(device, semaphoreCreateInfo, null, out var semaphore).ThrowOnError();
                _imageAvailableSemaphores[i] = semaphore;
                gd.Api.CreateSemaphore(device, semaphoreCreateInfo, null, out semaphore).ThrowOnError();
                _renderFinishedSemaphores[i] = semaphore;

                if (OperatingSystem.IsWindows())
                {
                    if (gd.Api.TryGetDeviceExtension<KhrExternalSemaphoreWin32>(_instance, _device, out var ext))
                    {
                        SemaphoreGetWin32HandleInfoKHR getInfo = new SemaphoreGetWin32HandleInfoKHR
                        {
                            SType = StructureType.SemaphoreGetWin32HandleInfoKhr,
                            Semaphore = _imageAvailableSemaphores[i],
                            HandleType = ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueWin32Bit
                        };

                        ext.GetSemaphoreWin32Handle(_device, getInfo, out var handle).ThrowOnError();

                        _exportImageAvailableSemaphores[i] = handle;

                        getInfo.Semaphore = _renderFinishedSemaphores[i];

                        ext.GetSemaphoreWin32Handle(_device, getInfo, out handle).ThrowOnError();

                        _exportRenderFinishedSemaphores[i] = handle;
                    }
                }
                else if (OperatingSystem.IsLinux())
                {
                    if (gd.Api.TryGetDeviceExtension<KhrExternalSemaphoreFd>(_instance, _device, out var ext))
                    {
                        SemaphoreGetFdInfoKHR getInfo = new SemaphoreGetFdInfoKHR
                        {
                            SType = StructureType.SemaphoreGetFDInfoKhr,
                            Semaphore = _imageAvailableSemaphores[i],
                            HandleType = ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueWin32Bit
                        };

                        ext.GetSemaphoreF(_device, getInfo, out var handle).ThrowOnError();

                        _exportImageAvailableSemaphores[i] = (IntPtr)handle;

                        getInfo.Semaphore = _renderFinishedSemaphores[i];

                        ext.GetSemaphoreF(_device, getInfo, out handle).ThrowOnError();

                        _exportRenderFinishedSemaphores[i] = (IntPtr)handle;
                    }
                }
            }
        }

        private void RecreateImages()
        {
            for (int i = 0; i < ImageCount; i++)
            {
                lock (_states[i])
                {
                    _states[i].IsValid = false;
                    _fences[i]?.Wait();
                    _fences[i]?.Put();
                    _imageViews[i]?.Dispose();
                    _imageMemory[i]?.Dispose();
                    _images[i]?.Dispose();
                    _presentedImages = null;
                }
            }

            CreateImages();
        }

        private void CreateImages()
        {
            _imageViews = new Auto<DisposableImageView>[ImageCount];
            _fences = new FenceHolder[ImageCount];
            _presentedImages = new PresentImageInfo[ImageCount];

            _nextImage = 0;

            unsafe
            {
                var cbs = _gd.CommandBufferPool.Rent();
                ExternalMemoryHandleTypeFlags flags = default;

                if (OperatingSystem.IsWindows())
                {
                    flags |= ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueWin32Bit;
                }
                else if (OperatingSystem.IsLinux())
                {
                    flags |= ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueFDBit;
                }

                var externalImageCreateInfo = new ExternalMemoryImageCreateInfo()
                {
                    SType = StructureType.ExternalMemoryImageCreateInfo,
                    HandleTypes = flags,
                };

                var exportMemoryAllocateInfo = new ExportMemoryAllocateInfo()
                {
                    SType = StructureType.ExportMemoryAllocateInfo,
                    HandleTypes = flags
                };

                var imageCreateInfo = new ImageCreateInfo
                {
                    SType = StructureType.ImageCreateInfo,
                    ImageType = ImageType.ImageType2D,
                    Format = Format,
                    Extent =
                        new Extent3D((uint?)_width,
                            (uint?)_height, 1),
                    MipLevels = 1,
                    ArrayLayers = 1,
                    Samples = SampleCountFlags.SampleCount1Bit,
                    Tiling = ImageTiling.Optimal,
                    Usage =
                        ImageUsageFlags.ImageUsageColorAttachmentBit | ImageUsageFlags.ImageUsageTransferSrcBit |
                        ImageUsageFlags.ImageUsageTransferDstBit,
                    SharingMode = SharingMode.Exclusive,
                    InitialLayout = ImageLayout.Undefined,
                    Flags = ImageCreateFlags.ImageCreateMutableFormatBit,
                    PNext = &externalImageCreateInfo
                };

                for (int i = 0; i < _images.Length; i++)
                {
                    _gd.Api.CreateImage(_device, imageCreateInfo, null, out var image).ThrowOnError();
                    _images[i] = new Auto<DisposableImage>(new DisposableImage(_gd.Api, _device, image));

                    _gd.Api.GetImageMemoryRequirements(_device, image,
                        out var memoryRequirements);

                    var memoryAllocateInfo = new MemoryAllocateInfo
                    {
                        SType = StructureType.MemoryAllocateInfo,
                        AllocationSize = memoryRequirements.Size,
                        MemoryTypeIndex = (uint)MemoryAllocator.FindSuitableMemoryTypeIndex(_gd.Api,
                            _physicalDevice,
                            memoryRequirements.MemoryTypeBits, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit),
                        PNext = &exportMemoryAllocateInfo
                    };

                    _gd.Api.AllocateMemory(_device, memoryAllocateInfo, null, out var memory);

                    _imageSizes[i] = memoryAllocateInfo.AllocationSize;
                    _imageOffsets[i] = 0;

                    _imageMemory[i] = new Auto<DisposableMemory>(new DisposableMemory(_gd.Api, _device, memory));

                    _gd.Api.BindImageMemory(_device, image, memory, 0);

                    _imageViews[i] = CreateImageView(_gd.Api, _device, image, Format);

                    Transition(
                        _gd.Api,
                        cbs.CommandBuffer,
                        image,
                        0,
                        0,
                        ImageLayout.Undefined,
                        ImageLayout.TransferSrcOptimal);

                    _states[i] = new ImageState();
                }

                _gd.CommandBufferPool.Return(cbs);
            }
        }

        internal static unsafe Auto<DisposableImageView> CreateImageView(Vk api, Device device, Image image,
            VkFormat format)
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

            api.CreateImageView(device, imageCreateInfo, null, out var imageView).ThrowOnError();
            return new Auto<DisposableImageView>(new DisposableImageView(api, device, imageView));
        }

        public override unsafe void Present(ITexture texture, ImageCrop crop, Action<object> swapBuffersCallback)
        {
            if (_recreateImages)
            {
                RecreateImages();
                _recreateImages = false;
            }

            var image = _images[_nextImage];

            _gd.FlushAllCommands();

            lock (_states[_nextImage])
            {
                var cbs = _gd.CommandBufferPool.Rent();

                Transition(
                    _gd.Api,
                    cbs.CommandBuffer,
                    image.GetUnsafe().Value,
                    0,
                    AccessFlags.AccessTransferWriteBit,
                    ImageLayout.TransferSrcOptimal,
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
                    CaptureFrame(view, srcX0, srcY0, srcX1 - srcX0, srcY1 - srcY0, view.Info.Format.IsBgr(), crop.FlipX,
                        crop.FlipY);

                    ScreenCaptureRequested = false;
                }

                float ratioX = crop.IsStretched
                    ? 1.0f
                    : MathF.Min(1.0f, _height * crop.AspectRatioX / (_width * crop.AspectRatioY));
                float ratioY = crop.IsStretched
                    ? 1.0f
                    : MathF.Min(1.0f, _width * crop.AspectRatioY / (_height * crop.AspectRatioX));

                int dstWidth = (int)(_width * ratioX);
                int dstHeight = (int)(_height * ratioY);

                int dstPaddingX = (_width - dstWidth) / 2;
                int dstPaddingY = (_height - dstHeight) / 2;

                int dstX0 = crop.FlipX ? _width - dstPaddingX : dstPaddingX;
                int dstX1 = crop.FlipX ? dstPaddingX : _width - dstPaddingX;

                int dstY0 = crop.FlipY ? dstPaddingY : _height - dstPaddingY;
                int dstY1 = crop.FlipY ? _height - dstPaddingY : dstPaddingY;

                _gd.HelperShader.Blit(
                    _gd,
                    cbs,
                    view,
                    _imageViews[_nextImage],
                    _width,
                    _height,
                    Format,
                    new Extents2D(srcX0, srcY0, srcX1, srcY1),
                    new Extents2D(dstX0, dstY1, dstX1, dstY0),
                    true,
                    true);

                Transition(
                    _gd.Api,
                    cbs.CommandBuffer,
                    image.GetUnsafe().Value,
                    0,
                    0,
                    ImageLayout.General,
                    ImageLayout.TransferSrcOptimal);

                _gd.CommandBufferPool.Return(
                    cbs,
                    new[] { _imageAvailableSemaphores[_nextImage] },
                    new[] { PipelineStageFlags.PipelineStageAllCommandsBit },
                    new[] { _renderFinishedSemaphores[_nextImage] });

                var j = _nextImage;

                _fences[_nextImage]?.Put();
                _fences[_nextImage] = cbs.GetFence();
                cbs.GetFence().Get();

                PresentImageInfo info = _presentedImages[_nextImage];
                if (info == null)
                {
                    info = new PresentImageInfo(
                        image.GetUnsafe().Value,
                        _imageMemory[_nextImage].GetUnsafe().Memory,
                        _device,
                        _instance,
                        _physicalDevice,
                        _imageSizes[_nextImage],
                        _imageOffsets[_nextImage],
                        _exportRenderFinishedSemaphores[_nextImage],
                        _exportImageAvailableSemaphores[_nextImage],
                        new Extent2D((uint)_width, (uint)_height),
                        _states[_nextImage],
                        cbs.GetFence());

                    _presentedImages[_nextImage] = info;
                }
                else
                {
                    info.Fence = cbs.GetFence();
                }

                swapBuffersCallback(info);
            }

            _nextImage = (_nextImage + 1) % ImageCount;
        }

        internal static unsafe void Transition(
            Vk api,
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

            api.CmdPipelineBarrier(
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

        public override void SetSize(int width, int height)
        {
            if (_width != width || _height != height)
            {
                _recreateImages = true;
            }

            _width = width;
            _height = height;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                unsafe
                {
                    for (int i = 0; i < ImageCount; i++)
                    {
                        _states[i].IsValid = false;
                        _fences[i]?.Wait();
                        _fences[i]?.Put();
                        _gd.Api.DestroySemaphore(_device, _renderFinishedSemaphores[i], null);
                        _gd.Api.DestroySemaphore(_device, _imageAvailableSemaphores[i], null);
                        _imageViews[i]?.Dispose();
                        _imageMemory[i]?.Dispose();
                        _images[i]?.Dispose();
                    }
                }
            }
        }

        public override void Dispose()
        {
            Dispose(true);
        }
    }

    public class ImageState
    {
        private bool _isValid = true;

        public bool IsValid
        {
            get => _isValid;
            internal set
            {
                _isValid = value;

                StateChanged?.Invoke(this, _isValid);
            }
        }

        public event EventHandler<bool> StateChanged;
    }

    public class PresentImageInfo
    {
        private Auto<DisposableMemory> _externalMemory;
        private Auto<DisposableImage> _externalImage;

        public Image Image { get; }
        public DeviceMemory Memory { get; }
        public Device Device { get; }
        public Instance Instance { get; }
        public PhysicalDevice PhysicalDevice { get; }
        public ulong MemorySize { get; }
        public ulong MemoryOffset { get; }
        public IntPtr ReadySemaphore { get; }
        public IntPtr AvailableSemaphore { get; }
        public Extent2D Extent { get; }
        public ImageState State { get; internal set; }
        internal FenceHolder Fence { get; set; }

        public Semaphore ImportImageAvailableSemaphore { get; private set; }
        public Semaphore ImportRenderingFinishedSemaphore { get; private set; }

        internal PresentImageInfo(
            Image image,
            DeviceMemory memory,
            Device device,
            Instance instance,
            PhysicalDevice physicalDevice,
            ulong memorySize,
            ulong memoryOffset,
            IntPtr readySemaphore,
            IntPtr availableSemaphore,
            Extent2D extent2D,
            ImageState state,
            FenceHolder fence)
        {
            Image = image;
            Memory = memory;
            Device = device;
            Instance = instance;
            PhysicalDevice = physicalDevice;
            MemorySize = memorySize;
            MemoryOffset = memoryOffset;
            ReadySemaphore = readySemaphore;
            AvailableSemaphore = availableSemaphore;
            Extent = extent2D;
            State = state;
            Fence = fence;

            state.StateChanged += StateChanged;
        }

        private unsafe void StateChanged(object sender, bool e)
        {
            lock (State)
            {
                if (!e)
                {
                    if (_externalImage != null)
                    {
                        _externalImage.Dispose();
                        _externalMemory.Dispose();

                        var api = Vk.GetApi();
                        api.DestroySemaphore(Device, ImportImageAvailableSemaphore, null);
                        api.DestroySemaphore(Device, ImportRenderingFinishedSemaphore, null);
                    }
                }
            }
        }

        public unsafe void CopyImage(Device device, PhysicalDevice physicalDevice, CommandBuffer commandBuffer, Extent2D imageSize,
            in Image stagingImage)
        {
            var api = Vk.GetApi();

            ImageBlit region;

            if (!State.IsValid)
            {
                return;
            }

            if (_externalImage != null)
            {
                region = new ImageBlit
                {
                    SrcOffsets = new ImageBlit.SrcOffsetsBuffer
                    {
                        Element0 = new Offset3D(0, 0, 0),
                        Element1 = new Offset3D((int?)Extent.Width, (int?)Extent.Height, 1),
                    },
                    DstOffsets = new ImageBlit.DstOffsetsBuffer
                    {
                        Element0 = new Offset3D(0, 0, 0),
                        Element1 = new Offset3D((int?)imageSize.Width, (int?)imageSize.Height, 1),
                    },
                    SrcSubresource = new ImageSubresourceLayers
                    {
                        AspectMask = ImageAspectFlags.ImageAspectColorBit,
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                        MipLevel = 0
                    },
                    DstSubresource = new ImageSubresourceLayers
                    {
                        AspectMask = ImageAspectFlags.ImageAspectColorBit,
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                        MipLevel = 0
                    }
                };

                api.CmdBlitImage(commandBuffer, _externalImage.GetUnsafe().Value,
                    ImageLayout.TransferSrcOptimal, stagingImage, ImageLayout.TransferDstOptimal, 1, region, Filter.Linear);

                return;
            }

            ExternalMemoryHandleTypeFlags flags = default;

            if (OperatingSystem.IsWindows())
            {
                flags |= ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueWin32Bit;
            }
            else if (OperatingSystem.IsLinux())
            {
                flags |= ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueFDBit;
            }

            var externalImageCreateInfo = new ExternalMemoryImageCreateInfo()
            {
                SType = StructureType.ExternalMemoryImageCreateInfo,
                HandleTypes = flags,
            };

            var imageCreateInfo = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.ImageType2D,
                Format = ImageWindow.Format,
                Extent =
                    new Extent3D(Extent.Width, Extent.Height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Samples = SampleCountFlags.SampleCount1Bit,
                Tiling = ImageTiling.Optimal,
                Usage =
                    ImageUsageFlags.ImageUsageColorAttachmentBit | ImageUsageFlags.ImageUsageTransferSrcBit |
                    ImageUsageFlags.ImageUsageTransferDstBit,
                SharingMode = SharingMode.Exclusive,
                InitialLayout = ImageLayout.Undefined,
                Flags = ImageCreateFlags.ImageCreateMutableFormatBit,
                PNext = &externalImageCreateInfo
            };

            Image image;

            try
            {
                api.CreateImage(device, imageCreateInfo, null, out image).ThrowOnError();
            }
            catch
            {
                return;
            }

            api.GetImageMemoryRequirements(device, image,
                out var memoryRequirements);

            var memoryAllocateInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memoryRequirements.Size,
                MemoryTypeIndex = (uint)MemoryAllocator.FindSuitableMemoryTypeIndex(api,
                    physicalDevice,
                    memoryRequirements.MemoryTypeBits, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit)
            };


            var exportSemaphoreCreateInfo = new ExportSemaphoreCreateInfo()
            {
                SType = StructureType.ExportSemaphoreCreateInfo,
                HandleTypes = OperatingSystem.IsWindows() ? ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueWin32Bit :
                    ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueFDBit
            };

            var semaphoreCreateInfo = new SemaphoreCreateInfo() { SType = StructureType.SemaphoreCreateInfo, PNext = &exportSemaphoreCreateInfo };

            api.CreateSemaphore(device, semaphoreCreateInfo, null, out var semaphore).ThrowOnError();

            ImportRenderingFinishedSemaphore = semaphore;

            api.CreateSemaphore(device, semaphoreCreateInfo, null, out semaphore).ThrowOnError();

            ImportImageAvailableSemaphore = semaphore;

            if (OperatingSystem.IsWindows())
            {
                nint handle = 0;
                if (api.TryGetDeviceExtension<KhrExternalMemoryWin32>(Instance, Device, out var win32Export))
                {
                    var getInfo = new MemoryGetWin32HandleInfoKHR()
                    {
                        SType = StructureType.MemoryGetWin32HandleInfoKhr,
                        HandleType = ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueWin32Bit,
                        Memory = Memory
                    };
                    win32Export.GetMemoryWin32Handle(Device, getInfo, out handle).ThrowOnError();
                }

                if (handle != 0)
                {
                    var getInfo = new ImportMemoryWin32HandleInfoKHR
                    {
                        SType = StructureType.ImportMemoryWin32HandleInfoKhr,
                        HandleType = ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueWin32Bit,
                        Handle = handle
                    };

                    memoryAllocateInfo.PNext = &getInfo;
                }
                else
                {
                    throw new Exception();
                }

                var importInfo = new ImportSemaphoreWin32HandleInfoKHR()
                {
                    SType = StructureType.ImportSemaphoreWin32HandleInfoKhr,
                    HandleType = ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueWin32Bit,
                    Semaphore = ImportImageAvailableSemaphore,
                    Handle = AvailableSemaphore,
                };

                if(api.TryGetDeviceExtension<KhrExternalSemaphoreWin32>(Instance, device, out var ext))
                {
                    ext.ImportSemaphoreWin32Handle(device, importInfo).ThrowOnError();

                    importInfo.Semaphore = ImportRenderingFinishedSemaphore;
                    importInfo.Handle = ReadySemaphore;

                    ext.ImportSemaphoreWin32Handle(device, importInfo).ThrowOnError();
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                int handle = 0;
                if (api.TryGetDeviceExtension<KhrExternalMemoryFd>(Instance, Device, out var fdExport))
                {
                    var getInfo = new MemoryGetFdInfoKHR()
                    {
                        SType = StructureType.MemoryGetFDInfoKhr,
                        HandleType = ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueFDBit,
                        Memory = Memory
                    };
                    fdExport.GetMemoryF(Device, &getInfo, out handle).ThrowOnError();
                }

                if (handle != 0)
                {
                    var getInfo = new ImportMemoryFdInfoKHR
                    {
                        SType = StructureType.ImportMemoryFDInfoKhr,
                        HandleType = ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueFDBit,
                        Fd = handle
                    };

                    memoryAllocateInfo.PNext = &getInfo;
                }
                else
                {
                    throw new Exception();
                }

                var importInfo = new ImportSemaphoreFdInfoKHR()
                {
                    SType = StructureType.ImportSemaphoreWin32HandleInfoKhr,
                    HandleType = ExternalSemaphoreHandleTypeFlags.ExternalSemaphoreHandleTypeOpaqueWin32Bit,
                    Semaphore = ImportImageAvailableSemaphore,
                    Fd = (int)AvailableSemaphore,
                };

                if (api.TryGetDeviceExtension<KhrExternalSemaphoreFd>(Instance, device, out var ext))
                {
                    ext.ImportSemaphoreF(device, importInfo).ThrowOnError();

                    importInfo.Semaphore = ImportRenderingFinishedSemaphore;
                    importInfo.Fd = (int)ReadySemaphore;

                    ext.ImportSemaphoreF(device, importInfo).ThrowOnError();
                }
            }

            DeviceMemory memory;
            try
            {
                api.AllocateMemory(device, memoryAllocateInfo, null,
                    out memory).ThrowOnError();
            }
            catch
            {
                api.DestroyImage(device, image, null);
                return;
            }

            api.BindImageMemory(device, image, memory, 0).ThrowOnError();

            _externalImage = new Auto<DisposableImage>(new DisposableImage(api, device, image));
            _externalMemory = new Auto<DisposableMemory>(new DisposableMemory(api, device, memory));

            ImageWindow.Transition(
                api,
                commandBuffer,
                image,
                0,
                0,
                ImageLayout.Undefined,
                ImageLayout.TransferSrcOptimal);

            region = new ImageBlit
            {
                SrcOffsets = new ImageBlit.SrcOffsetsBuffer
                {
                    Element0 = new Offset3D(0, 0, 0),
                    Element1 = new Offset3D((int?)imageCreateInfo.Extent.Width, (int?)imageCreateInfo.Extent.Height, 1),
                },
                DstOffsets = new ImageBlit.DstOffsetsBuffer
                {
                    Element0 = new Offset3D(0, 0, 0),
                    Element1 = new Offset3D((int?)imageSize.Width, (int?)imageSize.Height, 1),
                },
                SrcSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ImageAspectColorBit,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                    MipLevel = 0
                },
                DstSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ImageAspectColorBit,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                    MipLevel = 0
                }
            };

            api.CmdBlitImage(commandBuffer, _externalImage.GetUnsafe().Value,
                ImageLayout.TransferSrcOptimal, stagingImage, ImageLayout.TransferDstOptimal, 1, region, Filter.Linear);
        }
    }
}