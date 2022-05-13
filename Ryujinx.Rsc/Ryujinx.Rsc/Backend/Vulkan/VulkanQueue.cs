using System;
using Silk.NET.Vulkan;

namespace Ryujinx.Rsc.Vulkan
{
    public class VulkanQueue
    {
        public VulkanQueue(VulkanDevice device, Queue apiHandle)
        {
            Device = device;
            InternalHandle = apiHandle;
        }

        public VulkanDevice Device { get; }
        public IntPtr Handle => InternalHandle.Handle;
        internal Queue InternalHandle { get; }
    }
}
