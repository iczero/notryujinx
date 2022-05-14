using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Ryujinx.Memory.MemoryManagerUnixHelper;

namespace Ryujinx.Memory
{
    public unsafe static class MemoryManagerAndroidHelper
    {
        [DllImport("android")]
        internal static extern int ASharedMemory_create(byte* name, ulong size);

        [DllImport("android", SetLastError = true)]
        public static extern int close(int fd);

        [DllImport("android", EntryPoint = "mmap", SetLastError = true)]
        private static extern IntPtr Android_mmap(IntPtr address, ulong length, MmapProts prot, int flags, int fd, long offset);

        [DllImport("android", EntryPoint = "munmap", SetLastError = true)]
        public static extern int Android_munmap(IntPtr address, ulong length);

        [DllImport("android", EntryPoint = "mprotect",  SetLastError = true)]
        public static extern int Android_mprotect(IntPtr address, ulong length, MmapProts prot);

        [DllImport("android", EntryPoint = "madvise", SetLastError = true)]
        public static extern int Android_madvise(IntPtr address, ulong size, int advice);

        public static IntPtr mmap(IntPtr address, ulong length, MmapProts prot, MmapFlags flags, int fd, long offset)
        {
            return Android_mmap(address, length, prot, MmapFlagsToSystemFlags(flags), fd, offset);
        }

    }
}
