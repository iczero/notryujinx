using System;
using Avalonia.Platform;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Avalonia.Vulkan.Surfaces
{
    public class X11VulkanPlatformSurface : IVulkanPlatformSurface
    {
        private readonly IntPtr _display;
        private readonly IWindowImpl _window;

        internal X11VulkanPlatformSurface(IntPtr display, IWindowImpl windowImpl)
        {
            _display = display;
            _window = windowImpl;
        }
        
        public unsafe SurfaceKHR CreateSurface(VulkanInstance instance)
        {
            if (instance.Api.TryGetInstanceExtension(new Instance(instance.Handle), out KhrXlibSurface surfaceExtension))
            {
                var createInfo = new XlibSurfaceCreateInfoKHR()
                {
                    SType = StructureType.XlibSurfaceCreateInfoKhr,
                    Dpy = (nint*) _display.ToPointer(),
                    Window = _window.Handle.Handle
                };

                surfaceExtension.CreateXlibSurface(new Instance(instance.Handle), createInfo, null, out var surface).ThrowOnError();

                return surface;
            }

            throw new Exception("VK_KHR_xlib_surface is not available on this platform.");
        }

        public PixelSize SurfaceSize => new PixelSize((int)(_window.ClientSize.Width * Scaling), (int)(_window.ClientSize.Height * Scaling));

        public float Scaling => Math.Max(0, (float)_window.RenderScaling);
    }
}
