using Silk.NET.Vulkan;
using System;

namespace Ryujinx.Graphics.Vulkan;

internal class ExternalImage : IDisposable
{
    private readonly VulkanGraphicsDevice _gd;
    private readonly Device _device;
    private readonly PhysicalDevice _physicalDevice;
    private readonly Extent2D _extent;
    private Image _image;
    private DeviceMemory _memory;
    private nint _externalImageMemoryHandle;

    public Image Image
    {
        get => _image;
    }

    public Auto<DisposableImageView> ImageView { get; set; }

    public nint ExternalImageMemoryHandle => _externalImageMemoryHandle;
    public int ExternalTextureHandle { get; set; }
    public ulong MemorySize { get; set; }

    public ExternalImage(VulkanGraphicsDevice gd, Device device, PhysicalDevice physicalDevice, Extent2D extent)
    {
        _gd = gd;
        _device = device;
        _physicalDevice = physicalDevice;
        _extent = extent;
        
        CreateImage();
    }

    private unsafe void CreateImage()
    {
        var imageCreateInfo = new ImageCreateInfo()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.ImageType2D,
            Format = Format.R8G8B8A8Unorm,
            Extent = new Extent3D(_extent.Width, _extent.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.SampleCount1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.ImageUsageColorAttachmentBit,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
            Flags = ImageCreateFlags.ImageCreateMutableFormatBit
        };

        _gd.Api.CreateImage(_device, imageCreateInfo, null, out _image).ThrowOnError();
        _gd.Api.GetImageMemoryRequirements(_device, _image, out var memoryRequirements);
        MemoryAllocateInfo memoryAllocateInfo = new MemoryAllocateInfo()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memoryRequirements.Size,
            MemoryTypeIndex = (uint)MemoryAllocator.FindSuitableMemoryTypeIndex(_gd.Api, _physicalDevice,
                memoryRequirements.MemoryTypeBits, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit),
        };
        ExportMemoryAllocateInfo exportMemoryAllocateInfo = new ExportMemoryAllocateInfo()
        {
            SType = StructureType.ExportMemoryAllocateInfo,
            HandleTypes = OperatingSystem.IsWindows()
                ? ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueWin32Bit
                : ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueFDBit
        };

        memoryAllocateInfo.PNext = &exportMemoryAllocateInfo;

        _gd.Api.AllocateMemory(_device, memoryAllocateInfo, null, out _memory);
        _gd.Api.BindImageMemory(_device, _image, _memory, 0);

        MemorySize = memoryRequirements.Size;

        if (OperatingSystem.IsWindows())
        {
            _gd.ExternalMemoryWin32.GetMemoryWin32Handle(_device,
                new MemoryGetWin32HandleInfoKHR()
                {
                    SType = StructureType.MemoryGetWin32HandleInfoKhr,
                    HandleType = ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueWin32Bit,
                    Memory = _memory
                }, out _externalImageMemoryHandle).ThrowOnError();
        }
        else if (OperatingSystem.IsLinux())
        {
            _gd.ExternalMemoryFd.GetMemoryF(_device,
                new MemoryGetFdInfoKHR()
                {
                    SType = StructureType.MemoryGetFDInfoKhr,
                    HandleType = ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueFDBit,
                    Memory = _memory
                }, out int handle).ThrowOnError();

            _externalImageMemoryHandle = handle;
        }
    }


    public unsafe void Dispose()
    {
        _gd.Api.FreeMemory(_device, _memory, null);
        _gd.Api.DestroyImage(_device, _image, null);
        ImageView?.Dispose();
    }
}