﻿using ARMeilleure.State;
using Ryujinx.Cpu;
using Ryujinx.Horizon.Kernel.Svc;
using Ryujinx.Memory;

namespace Ryujinx.HLE.HOS
{
    class ArmProcessContext : IProcessContext
    {
        private readonly MemoryManager _memoryManager;
        private readonly CpuContext _cpuContext;

        public IAddressSpaceManager AddressSpace => _memoryManager;

        public ArmProcessContext(MemoryManager memoryManager)
        {
            _memoryManager = memoryManager;
            _cpuContext = new CpuContext(memoryManager);
        }

        public void Execute(ExecutionContext context, ulong codeAddress) => _cpuContext.Execute(context, codeAddress);
        public void Dispose() => _memoryManager.Dispose();
    }
}
