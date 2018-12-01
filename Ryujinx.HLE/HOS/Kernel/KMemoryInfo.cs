namespace Ryujinx.HLE.HOS.Kernel
{
    class KMemoryInfo
    {
        public ulong Address { get; private set; }
        public ulong Size    { get; private set; }

        public MemoryState      State      { get; private set; }
        public MemoryPermission Permission { get; private set; }
        public MemoryAttribute  Attribute  { get; private set; }

        public int IpcRefCount    { get; private set; }
        public int DeviceRefCount { get; private set; }

        public KMemoryInfo(
            ulong            address,
            ulong            size,
            MemoryState      state,
            MemoryPermission permission,
            MemoryAttribute  attribute,
            int              ipcRefCount,
            int              deviceRefCount)
        {
            this.Address        = address;
            this.Size           = size;
            this.State          = state;
            this.Attribute      = attribute;
            this.Permission     = permission;
            this.IpcRefCount    = ipcRefCount;
            this.DeviceRefCount = deviceRefCount;
        }
    }
}