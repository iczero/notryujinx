﻿using System;

namespace Ryujinx.Graphics.Video
{
    public interface IVp9Decoder : IDecoder
    {
        void Decode(
            ref Vp9PictureInfo pictureInfo,
            ISurface output,
            ReadOnlySpan<byte> bitstream,
            ReadOnlySpan<MvRef> mvsIn,
            Span<MvRef> mvsOut);
    }
}
