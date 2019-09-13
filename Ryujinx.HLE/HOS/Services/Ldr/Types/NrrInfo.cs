﻿using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Ldr.Types
{
    class NrrInfo
    {
        public NrrHeader    Header     { get; private set; }
        public List<byte[]> Hashes     { get; private set; }
        public long         NrrAddress { get; private set; }

        public NrrInfo(long nrrAddress, NrrHeader header, List<byte[]> hashes)
        {
            NrrAddress = nrrAddress;
            Header     = header;
            Hashes     = hashes;
        }
    }
}