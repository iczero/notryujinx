﻿namespace Ryujinx.Graphics.Nvdec.Types.Vp9
{
    struct FrameSize
    {
#pragma warning disable CS0649 // Field is never assigned to
        public ushort Width;
        public ushort Height;
        public ushort LumaPitch;
        public ushort ChromaPitch;
#pragma warning restore CS0649
    }
}
