using ARMeilleure.Memory;

namespace Ryujinx.Cpu.AppleHv
{
    class HvCpuContext : ICpuContext
    {
        private readonly ITickSource _tickSource;
        private readonly HvMemoryManager _memoryManager;

#pragma warning disable IDE0060 // Remove unused parameter
        public HvCpuContext(ITickSource tickSource, IMemoryManager memory, bool for64Bit)
        {
            _tickSource = tickSource;
            _memoryManager = (HvMemoryManager)memory;
        }

#pragma warning disable IDE0051 // Remove unused private member
        private static void UnmapHandler(ulong address, ulong size)
        {
        }
#pragma warning restore IDE0060, IDE0051

        /// <inheritdoc/>
        public IExecutionContext CreateExecutionContext(ExceptionCallbacks exceptionCallbacks)
        {
            return new HvExecutionContext(_tickSource, exceptionCallbacks);
        }

        /// <inheritdoc/>
        public void Execute(IExecutionContext context, ulong address)
        {
            ((HvExecutionContext)context).Execute(_memoryManager, address);
        }

        /// <inheritdoc/>
        public void InvalidateCacheRegion(ulong address, ulong size)
        {
        }

        public IDiskCacheLoadState LoadDiskCache(string titleIdText, string displayVersion, bool enabled)
        {
            return new DummyDiskCacheLoadState();
        }

        public void PrepareCodeRange(ulong address, ulong size)
        {
        }
    }
}