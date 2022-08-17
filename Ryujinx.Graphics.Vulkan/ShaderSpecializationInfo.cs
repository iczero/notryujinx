using System;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Vulkan
{
    internal class ShaderSpecializationInfo : IDisposable
    {
        public ShaderSpecializationInfo(IReadOnlyCollection<SpecializationEntry> entries, NativeArray<byte> data)
        {
            Entries = entries;
            Data = data;
        }

        public IReadOnlyCollection<SpecializationEntry> Entries { get; }
        public NativeArray<byte> Data { get; }

        public void Dispose()
        {
            Data.Dispose();
        }
    }

    public struct SpecializationEntry
    {
        public uint Location { get; }
        public uint Offset { get; }
        public uint Size { get; }

        public SpecializationEntry(uint location, uint offset, uint size)
        {
            Location = location;
            Offset = offset;
            Size = size;
        }
    }
}