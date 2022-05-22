using System;
using System.Collections.Concurrent;
using System.Linq;
using Avalonia;
using Ryujinx.Common.Configuration;
using Ryujinx.Ava.Ui.Vulkan.Surfaces;
using Silk.NET.Vulkan;

namespace Ryujinx.Ava.Ui.Vulkan
{
    public class VulkanPlatformInterface : IDisposable
    {
        private static VulkanOptions _options;

        private VulkanPlatformInterface(VulkanInstance instance)
        {
            Instance = instance;
            Api = instance.Api;
        }

        public VulkanPhysicalDevice PhysicalDevice { get; private set; }
        public VulkanInstance Instance { get; }
        public VulkanDevice Device { get; private set; }
        public Vk Api { get; private set; }

        public void Dispose()
        {
            Device?.Dispose();
            Instance?.Dispose();
            Api?.Dispose();
        }

        private static VulkanPlatformInterface TryCreate()
        {
            try
            {
                _options = AvaloniaLocator.Current.GetService<VulkanOptions>() ?? new VulkanOptions();

                var instance = VulkanInstance.Create(_options);

                return new VulkanPlatformInterface(instance);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static bool TryInitialize()
        {
            var feature = TryCreate();
            if (feature != null)
            {
                AvaloniaLocator.CurrentMutable.Bind<VulkanPlatformInterface>().ToConstant(feature);
                return true;
            }

            return false;
        }

        public VulkanSurfaceRenderTarget CreateRenderTarget(IVulkanPlatformSurface platformSurface)
        {
            var surface = VulkanSurface.CreateSurface(Instance, platformSurface);

            try
            {
                if (Device == null)
                {
                    PhysicalDevice = VulkanPhysicalDevice.FindSuitablePhysicalDevice(Instance, surface, _options.PreferDiscreteGpu, _options.PreferredDevice);
                    Device = VulkanDevice.Create(Instance, PhysicalDevice, _options);
                }
            }
            catch (Exception ex)
            {
                surface.Dispose();
            }

            return new VulkanSurfaceRenderTarget(this, surface);
        }
    }
}
