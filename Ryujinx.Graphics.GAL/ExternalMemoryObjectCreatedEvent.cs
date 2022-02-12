using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Graphics.GAL
{
    public class ExternalMemoryObjectCreatedEvent : EventArgs
    {
        public ulong ImageHandle { get; }
        public IntPtr MemoryHandle { get; }
        public int TextureHandle { get; set; }
        public int Index { get; }
        public ulong MemorySize { get; }
        
        public IntPtr ReadySemaphoreHandle { get; }
        public IntPtr CompleteSemaphoreHandle { get; }
        public Action WaitAction { get; }

        public ExternalMemoryObjectCreatedEvent(ulong imageHandle, ulong memorySize, nint memoryHandle, IntPtr readySemaphoreHandle, IntPtr completeSemaphoreHandle, Action waitAction, int index)
        {
            ImageHandle = imageHandle;
            MemorySize = memorySize;
            MemoryHandle = memoryHandle;
            ReadySemaphoreHandle = readySemaphoreHandle;
            CompleteSemaphoreHandle = completeSemaphoreHandle;
            WaitAction = waitAction;
            Index = index;
        }
    }
}
