using OpenTK.Windowing.GraphicsLibraryFramework;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System;
using System.Collections.Generic;

namespace Ryujinx.Ava.Ui.Controls
{
    public class VulkanEmbeddedSurface : NativeEmbeddedWindow
    {
        public unsafe SurfaceKHR CreateSurface(Instance instance, Vk vk)
        {
            GLFW.CreateWindowSurface(new VkHandle(instance.Handle), GLFWWindow.WindowPtr, null, out VkHandle surface);

            return new SurfaceKHR((ulong)surface.Handle.ToInt64());
        }

        public string[] GetRequiredInstanceExtensions()
        {
            return GLFW.GetRequiredInstanceExtensions();
        }
    }
}