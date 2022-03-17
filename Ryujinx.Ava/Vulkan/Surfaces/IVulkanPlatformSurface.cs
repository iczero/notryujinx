using Silk.NET.Vulkan;

namespace Avalonia.Vulkan.Surfaces
{
    public interface IVulkanPlatformSurface
    {
        float Scaling { get; }
        PixelSize SurfaceSize { get; }
        SurfaceKHR CreateSurface(VulkanInstance instance);
    }
}
