using Avalonia;
using Avalonia.Platform;
using Ryujinx.Rsc.Vulkan;
using Ryujinx.Rsc.Vulkan.Surfaces;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Rsc.Backend.Vulkan
{
    public class VulkanWindowSurface : BackendSurface, IVulkanPlatformSurface
    {
        public float Scaling => (float)Handle.Scaling;

        public PixelSize SurfaceSize => Size;
        
        private IntPtr _display = IntPtr.Zero;

        [DllImport("libX11.so.6")]
        public static extern IntPtr XOpenDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        public static extern int XCloseDisplay(IntPtr display);

        public VulkanWindowSurface(IPlatformNativeSurfaceHandle handle) : base(handle)
        {
        }

        public unsafe SurfaceKHR CreateSurface(VulkanInstance instance)
        {
            if (OperatingSystem.IsWindows())
            {
                if (instance.Api.TryGetInstanceExtension(new Instance(instance.Handle), out KhrWin32Surface surfaceExtension))
                {
                    var createInfo = new Win32SurfaceCreateInfoKHR() { Hinstance = 0, Hwnd = Handle.Handle, SType = StructureType.Win32SurfaceCreateInfoKhr };

                    surfaceExtension.CreateWin32Surface(new Instance(instance.Handle), createInfo, null, out var surface).ThrowOnError();

                    return surface;
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                if (instance.Api.TryGetInstanceExtension(new Instance(instance.Handle), out KhrXlibSurface surfaceExtension))
                {
                    _display = XOpenDisplay(IntPtr.Zero);
                    var createInfo = new XlibSurfaceCreateInfoKHR()
                    {
                        SType = StructureType.XlibSurfaceCreateInfoKhr,
                        Dpy = (nint*)_display,
                        Window = Handle.Handle
                    };

                    surfaceExtension.CreateXlibSurface(new Instance(instance.Handle), createInfo, null, out var surface).ThrowOnError();

                    return surface;
                }
            }
            else if (OperatingSystem.IsAndroid())
            {
                if (instance.Api.TryGetInstanceExtension(new Instance(instance.Handle), out KhrAndroidSurface surfaceExtension))
                {
                    var createInfo = new AndroidSurfaceCreateInfoKHR()
                    {
                        Window = (nint*)Handle.Handle,
                        SType = StructureType.AndroidSurfaceCreateInfoKhr
                    };

                    surfaceExtension.CreateAndroidSurface(new Instance(instance.Handle), createInfo, null, out var surface).ThrowOnError();

                    return surface;
                }

            }

            return new SurfaceKHR();
        }

        public override void Dispose()
        {
            base.Dispose();

            if (_display != IntPtr.Zero)
            {
                XCloseDisplay(_display);
            }
        }
    }
}