using System;
using Avalonia.Platform;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Avalonia.Vulkan.Surfaces
{
    internal class AndroidVulkanPlatformSurface : IVulkanPlatformSurface
    {
        private readonly IntPtr _surface;
        private readonly ITopLevelImpl _topLevel;

        public AndroidVulkanPlatformSurface(IntPtr surface, ITopLevelImpl topLevel)
        {
            _surface = surface;
            _topLevel = topLevel;
        }

        public unsafe SurfaceKHR CreateSurface(VulkanInstance instance)
        {
            if (instance.Api.TryGetInstanceExtension(new Instance(instance.Handle), out KhrAndroidSurface surfaceExtension))
            {
                var createInfo = new AndroidSurfaceCreateInfoKHR() {
                    Window = (nint*)_surface, SType = StructureType.AndroidSurfaceCreateInfoKhr };

                surfaceExtension.CreateAndroidSurface(new Instance(instance.Handle), createInfo, null, out var surface).ThrowOnError();

                return surface;
            }

            throw new Exception("VK_KHR_android_surface is not available on this platform.");
        }

        public PixelSize SurfaceSize => PixelSize.FromSize(_topLevel.ClientSize, Scaling);

        public float Scaling => Math.Max(0, (float)_topLevel.RenderScaling);
    }
}
